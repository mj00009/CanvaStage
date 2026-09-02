using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace CanvaStage.Windows.Services;

public sealed class CanvaConnectService : ICanvaPresentationProvider
{
    public const string RedirectUrl = "http://127.0.0.1:27843/callback/";
    private const string ApiBaseUrl = "https://api.canva.com/rest/v1/";
    private static readonly TimeSpan ExportPreparationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumExportPollDelay = TimeSpan.FromSeconds(5);
    private readonly CanvaCredentialsStore _credentialsStore;
    private readonly HttpClient _httpClient;
    private string? _lastPresentationId;

    public CanvaConnectService(CanvaCredentialsStore credentialsStore)
    {
        _credentialsStore = credentialsStore;
        _httpClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CanvaStage/0.3");
    }

    public bool HasSavedConfiguration => _credentialsStore.Load() is not null;
    public bool IsConnected => _credentialsStore.Load() is { RefreshToken: not null };
    public string? ConfiguredClientId => _credentialsStore.Load()?.ClientId;

    public void SaveConfiguration(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new CanvaConnectException("Enter the Canva Client ID before connecting.");
        }

        CanvaCredentials? existing = _credentialsStore.Load();
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            if (existing is not null && string.Equals(existing.ClientId, clientId.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            throw new CanvaConnectException("Enter the Canva Client Secret the first time this Windows user configures CanvaStage.");
        }

        bool credentialsChanged = existing is null || !string.Equals(existing.ClientId, clientId.Trim(), StringComparison.Ordinal) || !string.Equals(existing.ClientSecret, clientSecret, StringComparison.Ordinal);
        _credentialsStore.Save(credentialsChanged
            ? new CanvaCredentials(clientId.Trim(), clientSecret, null, null, null)
            : existing!);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        CanvaCredentials credentials = RequireCredentials();
        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUrl);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException exception)
        {
            throw new CanvaConnectException($"CanvaStage could not listen on the configured redirect URL. Close any app using port 27843, then try again. ({exception.ErrorCode})");
        }

        string verifier = CreateRandomUrlSafeValue();
        string state = CreateRandomUrlSafeValue();
        string challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string authorizationUrl = BuildAuthorizationUrl(credentials.ClientId, challenge, state);

        try
        {
            Process.Start(new ProcessStartInfo(authorizationUrl) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            throw new CanvaConnectException("CanvaStage could not open the browser for Canva authorization. Copy the redirect URL into the Canva portal, then try again.", exception);
        }

        AuthorizationResponse response = await ReceiveAuthorizationResponseAsync(listener, state, cancellationToken);
        TokenResponse token = await ExchangeCodeAsync(credentials, response.Code, verifier, cancellationToken);
        _credentialsStore.Save(credentials with
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
        });
    }

    public async Task TestAsync(CancellationToken cancellationToken = default)
    {
        _ = await ListPresentationsAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        CanvaCredentials? credentials = _credentialsStore.Load();
        if (credentials is null)
        {
            return;
        }

        try
        {
            string token = credentials.RefreshToken ?? credentials.AccessToken ?? string.Empty;
            if (!string.IsNullOrEmpty(token))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/revoke")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token })
                };
                request.Headers.Authorization = CreateBasicAuthentication(credentials);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException)
        {
            // Local removal still ends access for this Windows user if Canva is unreachable.
            PrivacySafeLog.Write("Canva token revocation could not reach the service; local credentials were removed.");
        }
        finally
        {
            _credentialsStore.Clear();
        }
    }

    public async Task<IReadOnlyList<CanvaPresentation>> ListPresentationsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CanvaPresentation>();
        string? continuation = null;

        do
        {
            string path = "designs?ownership=any&sort_by=modified_descending&limit=100";
            if (!string.IsNullOrWhiteSpace(continuation))
            {
                path += "&continuation=" + Uri.EscapeDataString(continuation);
            }

            DesignListResponse response = await GetJsonAsync<DesignListResponse>(path, cancellationToken).ConfigureAwait(false);
            foreach (DesignResponse design in response.Items)
            {
                if (!IsPresentation(design))
                {
                    continue;
                }

                results.Add(new CanvaPresentation(design.Id, design.Title ?? "Untitled presentation", design.PageCount ?? 0, design.Thumbnail?.Url));
            }

            continuation = response.Continuation;
        }
        while (!string.IsNullOrWhiteSpace(continuation) && results.Count < 500);

        return results;
    }

    public async Task<PresentationDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_lastPresentationId))
        {
            throw new CanvaConnectException("Choose a Canva presentation before loading it.");
        }

        return await LoadAsync(_lastPresentationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PresentationDocument> LoadAsync(string presentationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentationId))
        {
            throw new CanvaConnectException("Choose a Canva presentation before applying it.");
        }

        _lastPresentationId = presentationId;
        GetDesignResponse designResponse = await GetJsonAsync<GetDesignResponse>($"designs/{Uri.EscapeDataString(presentationId)}", cancellationToken).ConfigureAwait(false);
        DesignResponse design = designResponse.Design ?? throw new CanvaConnectException("Canva returned an unreadable presentation response.");
        if (!IsPresentation(design))
        {
            throw new CanvaConnectException("The selected Canva design is not a presentation.");
        }

        IReadOnlyList<PageResponse> pageMetadata = await GetPageMetadataAsync(presentationId, cancellationToken).ConfigureAwait(false);
        byte[] presentationFile = await ExportPptxAsync(presentationId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<IReadOnlyList<string>> slideTexts = ExtractSlideText(presentationFile);
        var pages = new List<PresentationPage>();

        for (int index = 0; index < Math.Max(slideTexts.Count, pageMetadata.Count); index++)
        {
            int pageNumber = index + 1;
            IReadOnlyList<string> textBlocks = index < slideTexts.Count ? slideTexts[index] : [];
            PageResponse? metadata = pageMetadata.FirstOrDefault(page => page.PageNumber == pageNumber);
            string? fallbackImagePath = metadata?.Thumbnail?.Url is not null
                ? await DownloadPageImageAsync(presentationId, pageNumber, metadata.Thumbnail.Url, cancellationToken).ConfigureAwait(false)
                : null;
            string title = textBlocks.FirstOrDefault() ?? $"Page {pageNumber}";
            pages.Add(new PresentationPage(pageNumber, title, textBlocks, "#0D0E10", "#75E6C8", textBlocks.Count > 0, fallbackImagePath));
        }

        if (pages.Count == 0)
        {
            throw new CanvaConnectException("Canva returned a presentation with no pages.");
        }

        return new PresentationDocument(presentationId, design.Title ?? "Untitled presentation", pages, DateTimeOffset.UtcNow);
    }

    private CanvaCredentials RequireCredentials() => _credentialsStore.Load() ?? throw new CanvaConnectException("Configure your Canva Client ID and Client Secret first.");

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        CanvaCredentials credentials = RequireCredentials();
        if (!string.IsNullOrWhiteSpace(credentials.AccessToken) && credentials.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return credentials.AccessToken;
        }

        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            throw new CanvaConnectException("Connect Canva before refreshing presentations.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = credentials.RefreshToken
            })
        };
        request.Headers.Authorization = CreateBasicAuthentication(credentials);
        TokenResponse token = await SendTokenRequestAsync(request, cancellationToken).ConfigureAwait(false);
        CanvaCredentials updated = credentials with
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
        };
        _credentialsStore.Save(updated);
        return updated.AccessToken!;
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        string accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadJsonResponseAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PageResponse>> GetPageMetadataAsync(string presentationId, CancellationToken cancellationToken)
    {
        var pages = new List<PageResponse>();
        int offset = 1;
        while (true)
        {
            PageListResponse response = await GetJsonAsync<PageListResponse>($"designs/{Uri.EscapeDataString(presentationId)}/pages?offset={offset}&limit=200", cancellationToken).ConfigureAwait(false);
            pages.AddRange(response.Items);
            if (response.Items.Count < 200)
            {
                return pages;
            }

            offset += response.Items.Count;
        }
    }

    private async Task<byte[]> ExportPptxAsync(string presentationId, CancellationToken cancellationToken)
    {
        string accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "exports")
        {
            Content = JsonContent.Create(new { design_id = presentationId, format = new { type = "pptx" } })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage createResponse = await _httpClient.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
        ExportResponse export = await ReadJsonResponseAsync<ExportResponse>(createResponse, cancellationToken).ConfigureAwait(false);
        ExportJobResponse job = export.Job ?? throw new CanvaConnectException("Canva did not return an export job.");

        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(ExportPreparationTimeout);
        TimeSpan pollDelay = TimeSpan.FromSeconds(1);

        while (true)
        {
            if (string.Equals(job.Status, "success", StringComparison.OrdinalIgnoreCase) && job.Urls is { Count: > 0 })
            {
                return await _httpClient.GetByteArrayAsync(job.Urls[0], cancellationToken).ConfigureAwait(false);
            }

            if (string.Equals(job.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new CanvaConnectException("Canva could not export this presentation. Check that it can be exported as PowerPoint and try again.");
            }

            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(pollDelay <= remaining ? pollDelay : remaining, cancellationToken).ConfigureAwait(false);
            ExportResponse poll = await GetJsonAsync<ExportResponse>($"exports/{Uri.EscapeDataString(job.Id)}", cancellationToken).ConfigureAwait(false);
            job = poll.Job ?? throw new CanvaConnectException("Canva returned an invalid export job response.");
            pollDelay = TimeSpan.FromTicks(Math.Min(pollDelay.Ticks * 2, MaximumExportPollDelay.Ticks));
        }

        throw new CanvaConnectException("Canva is still preparing this presentation after five minutes. The selected presentation was not changed; try Apply again.");
    }

    private async Task<string?> DownloadPageImageAsync(string presentationId, int pageNumber, string url, CancellationToken cancellationToken)
    {
        try
        {
            byte[] image = await _httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            string safePresentationId = string.Concat(presentationId.Where(char.IsLetterOrDigit));
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage", "cache", safePresentationId);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"page-{pageNumber}.png");
            await File.WriteAllBytesAsync(path, image, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<AuthorizationResponse> ReceiveAuthorizationResponseAsync(HttpListener listener, string expectedState, CancellationToken cancellationToken)
    {
        try
        {
            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
            string? code = context.Request.QueryString["code"];
            string? state = context.Request.QueryString["state"];
            string? error = context.Request.QueryString["error"];
            await WriteCallbackResponseAsync(context.Response, string.IsNullOrWhiteSpace(error) ? "CanvaStage is connected. You can close this browser tab and return to the app." : "Canva authorization was not completed. Return to CanvaStage and try again.").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new CanvaConnectException("Canva authorization was cancelled or denied.");
            }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state ?? string.Empty), Encoding.UTF8.GetBytes(expectedState)) || string.IsNullOrWhiteSpace(code))
            {
                throw new CanvaConnectException("Canva returned an invalid authorization response. Try connecting again.");
            }

            return new AuthorizationResponse(code);
        }
        catch (TimeoutException)
        {
            throw new CanvaConnectException("Canva authorization timed out. Try Connect again when you are ready.");
        }
    }

    private async Task<TokenResponse> ExchangeCodeAsync(CanvaCredentials credentials, string code, string verifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = RedirectUrl
            })
        };
        request.Headers.Authorization = CreateBasicAuthentication(credentials);
        return await SendTokenRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenResponse> SendTokenRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadJsonResponseAsync<TokenResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadJsonResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new CanvaConnectException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Canva did not accept the saved credentials or connection. Check setup, then connect again.",
                HttpStatusCode.Forbidden => "Canva denied this request. Confirm the integration has design:meta:read and design:content:read scopes.",
                _ => $"Canva request failed ({(int)response.StatusCode}). Try again, or use Test setup for the connection details."
            });
        }

        return JsonSerializer.Deserialize<T>(content, CanvaJson.Options) ?? throw new CanvaConnectException("Canva returned an unreadable response.");
    }

    private static AuthenticationHeaderValue CreateBasicAuthentication(CanvaCredentials credentials)
    {
        string value = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ClientId}:{credentials.ClientSecret}"));
        return new AuthenticationHeaderValue("Basic", value);
    }

    private static string BuildAuthorizationUrl(string clientId, string challenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "s256",
            ["scope"] = "design:meta:read design:content:read",
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["state"] = state,
            ["redirect_uri"] = RedirectUrl
        };
        return "https://www.canva.com/api/oauth/authorize?" + string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractSlideText(byte[] presentationFile)
    {
        using var stream = new MemoryStream(presentationFile);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        XNamespace drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
        return archive.Entries
            .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => SlideNumber(entry.FullName))
            .Select(entry =>
            {
                using Stream entryStream = entry.Open();
                XDocument document = XDocument.Load(entryStream);
                return (IReadOnlyList<string>)document.Descendants(presentation + "sp")
                    .Select(shape => string.Join(" ", shape.Descendants(drawing + "t").Select(text => text.Value).Where(text => !string.IsNullOrWhiteSpace(text))).Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
            })
            .ToList();
    }

    private static int SlideNumber(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(filename["slide".Length..], out int number) ? number : int.MaxValue;
    }

    private static async Task WriteCallbackResponseAsync(HttpListenerResponse response, string message)
    {
        byte[] content = Encoding.UTF8.GetBytes($"<!doctype html><html><body style='font-family:Segoe UI,sans-serif;padding:3rem;background:#101113;color:#f7f8fa'><h1>{WebUtility.HtmlEncode(message)}</h1></body></html>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = content.Length;
        await response.OutputStream.WriteAsync(content);
        response.Close();
    }

    private static string CreateRandomUrlSafeValue() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string Base64UrlEncode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool IsPresentation(DesignResponse design) => design.DesignTypes?.Any(type => string.Equals(type, "presentation", StringComparison.OrdinalIgnoreCase)) == true;

    private sealed record AuthorizationResponse(string Code);
    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record DesignListResponse(IReadOnlyList<DesignResponse> Items, string? Continuation);
    private sealed record DesignResponse(string Id, string? Title, IReadOnlyList<string>? DesignTypes, int? PageCount, ThumbnailResponse? Thumbnail);
    private sealed record GetDesignResponse(DesignResponse? Design);
    private sealed record PageListResponse(IReadOnlyList<PageResponse> Items);
    private sealed record PageResponse(int PageNumber, ThumbnailResponse? Thumbnail);
    private sealed record ThumbnailResponse(string Url);
    private sealed record ExportResponse(ExportJobResponse? Job);
    private sealed record ExportJobResponse(string Id, string Status, IReadOnlyList<string>? Urls);

    private static class CanvaJson
    {
        public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }
}

public sealed class CanvaConnectException : Exception
{
    public CanvaConnectException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
