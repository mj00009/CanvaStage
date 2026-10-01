using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    private readonly string _cacheRoot;
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);
    private long _lastProgressTimestamp;

    public event EventHandler<CanvaLoadProgress>? LoadProgressChanged;

    public CanvaConnectService(CanvaCredentialsStore credentialsStore, HttpClient? httpClient = null, string? cacheRoot = null)
    {
        _credentialsStore = credentialsStore;
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.BaseAddress ??= new Uri(ApiBaseUrl);
        _cacheRoot = cacheRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage", "cache");
        string version = typeof(CanvaConnectService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"CanvaStage/{version}");
    }

    public bool HasSavedConfiguration => _credentialsStore.Load() is not null;
    public bool IsConnected => !string.IsNullOrWhiteSpace(_credentialsStore.Load()?.RefreshToken);
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
        if (IsConnected)
        {
            try
            {
                await TestAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (CanvaAuthorizationRequiredException)
            {
                // Only an explicit Connect action may open authorization again.
            }
        }
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

                results.Add(ToPresentation(design));
            }

            continuation = response.Continuation;
        }
        while (!string.IsNullOrWhiteSpace(continuation) && results.Count < 500);

        return results;
    }

    public async Task<CanvaPresentation> GetPresentationAsync(string presentationId, CancellationToken cancellationToken = default)
    {
        GetDesignResponse response = await GetJsonAsync<GetDesignResponse>($"designs/{Uri.EscapeDataString(presentationId)}", cancellationToken).ConfigureAwait(false);
        DesignResponse design = response.Design ?? throw new CanvaConnectException("Canva returned an unreadable presentation response.");
        if (!IsPresentation(design)) throw new CanvaConnectException("The selected Canva design is not a presentation.");
        return ToPresentation(design);
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
        PrivacySafeLog.Write("Canva content import started.");
        GetDesignResponse designResponse = await GetJsonAsync<GetDesignResponse>($"designs/{Uri.EscapeDataString(presentationId)}", cancellationToken).ConfigureAwait(false);
        DesignResponse design = designResponse.Design ?? throw new CanvaConnectException("Canva returned an unreadable presentation response.");
        if (!IsPresentation(design))
        {
            throw new CanvaConnectException("The selected Canva design is not a presentation.");
        }

        byte[] presentationFile = await ExportPptxAsync(presentationId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PptxSlideContent> extractedSlides = PptxSlideTextExtractor.ExtractSlides(presentationFile);
        IReadOnlyList<PptxSlideContent> visibleSlides = extractedSlides.Where(static slide => !slide.IsHidden).ToList();
        int hiddenSlideCount = extractedSlides.Count - visibleSlides.Count;
        // Metadata supplies stable page IDs and dimensions, not the Mirror image.
        // A separate render job avoids treating a preview thumbnail as a fresh export.
        ReportLoadProgress("Reading Canva page details…", null);
        IReadOnlyList<PageResponse> pageMetadata = await GetPageMetadataAsync(presentationId, cancellationToken).ConfigureAwait(false);
        var pages = new List<PresentationPage>();
        // Each import is immutable. A preview or another app instance may still hold the
        // previous generation open; never overwrite images belonging to a working deck.
        string safePresentationId = string.Concat(presentationId.Where(char.IsLetterOrDigit));
        string imageDirectory = Path.Combine(_cacheRoot, safePresentationId, Guid.NewGuid().ToString("N"));
        int totalPageCount = hiddenSlideCount > 0 ? visibleSlides.Count : Math.Max(extractedSlides.Count, pageMetadata.Count);
        if (totalPageCount == 0)
            throw new CanvaConnectException("Canva returned a presentation with no visible pages.");
        int[] sourcePageNumbers = Enumerable.Range(0, totalPageCount)
            .Select(index => index < visibleSlides.Count ? visibleSlides[index].SourcePageNumber : index + 1).ToArray();
        IReadOnlyList<string> imageUrls = await ExportPageImagesAsync(presentationId, sourcePageNumbers, pageMetadata, cancellationToken).ConfigureAwait(false);
        string hiddenSlideProgress = hiddenSlideCount > 0 ? $"; {hiddenSlideCount} hidden skipped" : string.Empty;
        ReportLoadProgress($"Caching Canva page images (0 of {totalPageCount}{hiddenSlideProgress})…", 0d);

        for (int index = 0; index < totalPageCount; index++)
        {
            int pageNumber = index + 1;
            PptxSlideContent? slide = index < visibleSlides.Count ? visibleSlides[index] : null;
            int sourcePageNumber = sourcePageNumbers[index];
            IReadOnlyList<string> textBlocks = slide?.TextBlocks ?? [];
            PageResponse? metadata = pageMetadata.FirstOrDefault(page => page.PageNumber == sourcePageNumber);
            string fallbackImagePath = await DownloadPageImageAsync(imageDirectory, sourcePageNumber, imageUrls[index], cancellationToken).ConfigureAwait(false);
            string title = textBlocks.FirstOrDefault() ?? $"Page {pageNumber}";
            pages.Add(new PresentationPage(pageNumber, title, textBlocks, "#0D0E10", "#75E6C8", textBlocks.Count > 0, fallbackImagePath, metadata?.Id));
            ReportLoadProgress($"Caching Canva page images ({pageNumber} of {totalPageCount}{hiddenSlideProgress})…", (double)pageNumber / totalPageCount);
        }

        PrivacySafeLog.Write($"Canva content import completed: {pages.Count} visible pages, {hiddenSlideCount} hidden skipped; fresh image export downloaded.");
        ReportLoadProgress("Presentation is ready.", 1d);
        return new PresentationDocument(presentationId, design.Title ?? "Untitled presentation", pages, DateTimeOffset.UtcNow, ToDateTimeOffset(design.UpdatedAt), UsesRenderedPageImages: true);
    }

    private CanvaCredentials RequireCredentials() => _credentialsStore.Load() ?? throw new CanvaConnectException("Configure your Canva Client ID and Client Secret first.");

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken, string? rejectedAccessToken = null)
    {
        await _tokenRefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CanvaCredentials credentials = RequireCredentials();
            if (!string.IsNullOrWhiteSpace(credentials.AccessToken) && credentials.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)
                && credentials.AccessToken != rejectedAccessToken)
            {
                return credentials.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
            {
                throw new CanvaAuthorizationRequiredException("Canva authorization is needed. Choose Connect Canva once to authorize this Windows account.");
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
            TokenResponse token;
            try
            {
                token = await SendTokenRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (CanvaAuthorizationRequiredException)
            {
                _credentialsStore.Save(credentials with { AccessToken = null, RefreshToken = null, AccessTokenExpiresAt = null });
                throw;
            }
            CanvaCredentials updated = credentials with
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            };
            _credentialsStore.Save(updated);
            return updated.AccessToken!;
        }
        finally { _tokenRefreshLock.Release(); }
    }

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, path), cancellationToken).ConfigureAwait(false);
        return await ReadJsonResponseAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        string accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; attempt++)
        {
            using HttpRequestMessage request = createRequest();
            if (request.Method == HttpMethod.Get)
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
            response.Dispose();
            if (attempt > 0) throw new CanvaAuthorizationRequiredException("Canva did not accept the renewed connection. Choose Connect Canva to authorize again.");
            accessToken = await GetAccessTokenAsync(cancellationToken, accessToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<PageResponse>> GetPageMetadataAsync(string presentationId, CancellationToken cancellationToken)
    {
        var pages = new List<PageResponse>();
        var seenPageNumbers = new HashSet<int>();
        int offset = 1;
        while (true)
        {
            PageListResponse response = await GetJsonAsync<PageListResponse>($"designs/{Uri.EscapeDataString(presentationId)}/pages?offset={offset}&limit=200", cancellationToken).ConfigureAwait(false);
            if (response.Items is null)
            {
                throw new CanvaConnectException("Canva returned page details in an unsupported format. Try refreshing again after updating CanvaStage.");
            }
            foreach (PageResponse page in response.Items)
            {
                if (page.PageNumber is < 1 or > 500 || !seenPageNumbers.Add(page.PageNumber))
                {
                    throw new CanvaConnectException("Canva returned inconsistent page numbers. The selected presentation was not changed; try Apply again.");
                }
                pages.Add(page);
            }
            if (response.Items.Count < 200)
            {
                return pages;
            }

            offset += response.Items.Count;
        }
    }

    private async Task<IReadOnlyList<string>> ExportPageImagesAsync(string presentationId, int[] sourcePages,
        IReadOnlyList<PageResponse> metadata, CancellationToken cancellationToken)
    {
        Dictionary<string, object> format = new()
        {
            ["type"] = "jpg", ["quality"] = 70, ["export_quality"] = "regular", ["pages"] = sourcePages
        };
        double?[] widths = sourcePages.Select(number => metadata.FirstOrDefault(page => page.PageNumber == number)?.Dimensions?.Width).ToArray();
        // Bound reference-image downloads without upscaling small designs on Free plans.
        if (widths.All(width => width is >= 40 and <= 25000))
            format["width"] = (int)Math.Min(1280, Math.Floor(widths.Min()!.Value));
        IReadOnlyList<string> urls = await ExportAsync(presentationId, format, "Mirror image", cancellationToken).ConfigureAwait(false);
        if (urls.Count != sourcePages.Length || urls.Any(string.IsNullOrWhiteSpace))
            throw new CanvaConnectException("Canva returned an unexpected number of rendered page images. The previous presentation was kept; try Re-download again.");
        return urls;
    }

    private async Task<byte[]> ExportPptxAsync(string presentationId, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> urls = await ExportAsync(presentationId, new { type = "pptx" }, "PowerPoint", cancellationToken).ConfigureAwait(false);
        return await DownloadExportAsync(urls[0], cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> ExportAsync(string presentationId, object format, string description, CancellationToken cancellationToken)
    {
        using HttpResponseMessage createResponse = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Post, "exports")
        {
            Content = JsonContent.Create(new { design_id = presentationId, format })
        }, cancellationToken).ConfigureAwait(false);
        ExportResponse export = await ReadJsonResponseAsync<ExportResponse>(createResponse, cancellationToken).ConfigureAwait(false);
        ExportJobResponse job = export.Job ?? throw new CanvaConnectException("Canva did not return an export job.");
        ReportLoadProgress($"Canva is preparing the {description} export…", null);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(ExportPreparationTimeout);
        TimeSpan pollDelay = TimeSpan.FromSeconds(1);

        while (true)
        {
            if (string.Equals(job.Status, "success", StringComparison.OrdinalIgnoreCase) && job.Urls is { Count: > 0 })
            {
                return job.Urls;
            }

            if (string.Equals(job.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new CanvaConnectException($"Canva could not prepare the {description} export. The previous presentation was kept; try Re-download again.");
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
            ReportLoadProgress($"Canva is still preparing the {description} export…", null);
        }

        throw new CanvaConnectException($"Canva is still preparing the {description} export after five minutes. The previous presentation was kept; try Re-download again.");
    }

    private async Task<string> DownloadPageImageAsync(string directory, int pageNumber, string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            byte[] image = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using MemoryStream input = new(image, false);
            _ = System.Windows.Media.Imaging.BitmapDecoder.Create(input,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"page-{pageNumber}.jpg");
            await File.WriteAllBytesAsync(path, image, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception exception) when (exception is HttpRequestException or NotSupportedException or FileFormatException)
        {
            PrivacySafeLog.Write($"Canva page {pageNumber} rendered image download or validation failed.");
            throw new CanvaConnectException($"CanvaStage could not retrieve the new image for page {pageNumber}. The previous presentation was kept; try Re-download again.", exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CanvaConnectException($"Canva page {pageNumber} image download timed out. The previous presentation was kept; try Re-download again.");
        }
    }

    private async Task<byte[]> DownloadExportAsync(string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = totalBytes is > 0 and <= int.MaxValue ? new MemoryStream((int)totalBytes.Value) : new MemoryStream();
        byte[] buffer = new byte[81920];
        long receivedBytes = 0;
        ReportLoadProgress(CreateDownloadMessage(receivedBytes, totalBytes), totalBytes is > 0 ? 0d : null, receivedBytes, totalBytes);

        while (true)
        {
            int bytesRead = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            receivedBytes += bytesRead;
            double? fraction = totalBytes is > 0 ? Math.Clamp((double)receivedBytes / totalBytes.Value, 0d, 1d) : null;
            ReportLoadProgress(CreateDownloadMessage(receivedBytes, totalBytes), fraction, receivedBytes, totalBytes);
        }

        return output.ToArray();
    }

    private void ReportLoadProgress(string message, double? fraction, long? bytesReceived = null, long? totalBytes = null)
    {
        long now = Stopwatch.GetTimestamp();
        if (bytesReceived is > 0 && fraction != 1d
            && Stopwatch.GetElapsedTime(_lastProgressTimestamp, now) < TimeSpan.FromMilliseconds(100)) return;
        _lastProgressTimestamp = now;
        LoadProgressChanged?.Invoke(this, new CanvaLoadProgress(message, fraction, bytesReceived, totalBytes));
    }

    private static string CreateDownloadMessage(long bytesReceived, long? totalBytes) => totalBytes is > 0
        ? $"Downloading presentation: {FormatFileSize(bytesReceived)} of {FormatFileSize(totalBytes.Value)} ({Math.Round((double)bytesReceived / totalBytes.Value * 100d):0}%)"
        : $"Downloading presentation: {FormatFileSize(bytesReceived)} received…";

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes / 1024d / 1024d:0.0} MB"
    };

    private async Task<AuthorizationResponse> ReceiveAuthorizationResponseAsync(HttpListener listener, string expectedState, CancellationToken cancellationToken)
    {
        Task<HttpListenerContext> pendingContext = listener.GetContextAsync();
        try
        {
            HttpListenerContext context = await pendingContext.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
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
        finally
        {
            if (!pendingContext.IsCompleted)
            {
                listener.Stop();
            }
            try { await pendingContext.ConfigureAwait(false); }
            catch (HttpListenerException) { /* Stopping a pending callback is expected on timeout or cancellation. */ }
            catch (ObjectDisposedException) { /* Observe the pending callback before listener disposal. */ }
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
        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using JsonDocument error = JsonDocument.Parse(content);
                if ((error.RootElement.TryGetProperty("error", out JsonElement code) || error.RootElement.TryGetProperty("code", out code))
                    && code.ValueKind == JsonValueKind.String && code.GetString() is "invalid_grant" or "invalid_refresh_token")
                    throw new CanvaAuthorizationRequiredException("The saved Canva authorization was revoked or expired. Choose Connect Canva to authorize again; the cached presentation is still available.");
            }
            catch (JsonException) { /* The safe HTTP error below handles non-JSON responses. */ }
        }
        TokenResponse token = await ReadJsonResponseAsync<TokenResponse>(response, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken) || token.ExpiresIn <= 0)
            throw new CanvaConnectException("Canva returned incomplete authorization data. The saved connection was not replaced; try again.");
        return token;
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
    private static CanvaPresentation ToPresentation(DesignResponse design) => new(
        design.Id,
        design.Title ?? "Untitled presentation",
        design.PageCount ?? 0,
        design.Thumbnail?.Url,
        ToDateTimeOffset(design.UpdatedAt));
    private static DateTimeOffset? ToDateTimeOffset(long? unixSeconds) => unixSeconds is > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value) : null;

    private sealed record AuthorizationResponse(string Code);
    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record DesignListResponse(IReadOnlyList<DesignResponse> Items, string? Continuation);
    private sealed record DesignResponse(string Id, string? Title, IReadOnlyList<string>? DesignTypes, int? PageCount, ThumbnailResponse? Thumbnail, long? UpdatedAt);
    private sealed record GetDesignResponse(DesignResponse? Design);
    private sealed record PageListResponse(IReadOnlyList<PageResponse> Items);
    private sealed record PageResponse(int PageNumber, string? Id, ThumbnailResponse? Thumbnail, PageDimensionsResponse? Dimensions);
    private sealed record PageDimensionsResponse(double Width, double Height);
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

public class CanvaConnectException : Exception
{
    public CanvaConnectException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class CanvaAuthorizationRequiredException(string message) : CanvaConnectException(message);

public sealed record CanvaLoadProgress(string Message, double? Fraction, long? BytesReceived = null, long? TotalBytes = null);
