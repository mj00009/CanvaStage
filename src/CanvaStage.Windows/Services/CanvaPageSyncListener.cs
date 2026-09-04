using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace CanvaStage.Windows.Services;

public sealed class CanvaPageSyncListener : IDisposable
{
    public const string EndpointUrl = "http://127.0.0.1:17334/sync/page";

    private static readonly HashSet<string> LocalDevelopmentOrigins = new(StringComparer.Ordinal)
    {
        "http://localhost:8080",
        "http://127.0.0.1:8080"
    };

    private readonly object _syncRoot = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private string? _pairingCode;

    public event EventHandler<CanvaPageSyncUpdate>? PageReceived;

    public bool IsListening
    {
        get
        {
            lock (_syncRoot)
            {
                return _listener?.IsListening == true;
            }
        }
    }

    public string? PairingCode
    {
        get
        {
            lock (_syncRoot)
            {
                return _pairingCode;
            }
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_listener?.IsListening == true)
            {
                return;
            }

            _pairingCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:17334/");
            _listener.Start();
            _ = ListenAsync(_listener, _cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        HttpListener? listener;
        CancellationTokenSource? cancellationTokenSource;
        lock (_syncRoot)
        {
            listener = _listener;
            cancellationTokenSource = _cancellationTokenSource;
            _listener = null;
            _cancellationTokenSource = null;
            _pairingCode = null;
        }

        cancellationTokenSource?.Cancel();
        listener?.Close();
        cancellationTokenSource?.Dispose();
    }

    public void Dispose() => Stop();

    private async Task ListenAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await ProcessRequestAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Canva page-sync listener failure", exception);
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (context.Request.RemoteEndPoint is null || !IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address))
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.Forbidden, "Loopback connections only.", cancellationToken).ConfigureAwait(false);
                return;
            }

            string? origin = context.Request.Headers["Origin"];
            if (!IsAllowedOrigin(origin))
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.Forbidden, "Unapproved browser origin.", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(origin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Vary"] = "Origin";
            }

            if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
                context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
                await WriteResponseAsync(context.Response, HttpStatusCode.NoContent, string.Empty, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) || context.Request.Url?.AbsolutePath != "/sync/page")
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.NotFound, "Not found.", cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.ContentLength64 is < 1 or > 4096)
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.BadRequest, "Unexpected request size.", cancellationToken).ConfigureAwait(false);
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, leaveOpen: false);
            string content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            CanvaPageSyncRequest? request = JsonSerializer.Deserialize<CanvaPageSyncRequest>(content, JsonOptions);
            if (request is null || !HasMatchingPairingCode(request.PairingCode) || string.IsNullOrWhiteSpace(request.DesignId) || string.IsNullOrWhiteSpace(request.PageId))
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.Unauthorized, "A valid pairing code, design ID, and page ID are required.", cancellationToken).ConfigureAwait(false);
                return;
            }

            PageReceived?.Invoke(this, new CanvaPageSyncUpdate(request.DesignId, request.PageId, DateTimeOffset.UtcNow));
            await WriteResponseAsync(context.Response, HttpStatusCode.OK, "Page received.", cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await WriteResponseAsync(context.Response, HttpStatusCode.BadRequest, "Invalid JSON.", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Canva page-sync request failure", exception);
            await WriteResponseAsync(context.Response, HttpStatusCode.InternalServerError, "CanvaStage could not process the page update.", cancellationToken).ConfigureAwait(false);
        }
    }

    private bool HasMatchingPairingCode(string? pairingCode)
    {
        string? expectedCode = PairingCode;
        return !string.IsNullOrWhiteSpace(pairingCode)
            && !string.IsNullOrWhiteSpace(expectedCode)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pairingCode), Encoding.UTF8.GetBytes(expectedCode));
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        if (LocalDevelopmentOrigins.Contains(origin))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.Host.EndsWith(".canva-apps.com", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string message, CancellationToken cancellationToken)
    {
        byte[] body = Encoding.UTF8.GetBytes(message);
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private sealed record CanvaPageSyncRequest(string? PairingCode, string? DesignId, string? PageId);

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
}

public sealed record CanvaPageSyncUpdate(string DesignId, string PageId, DateTimeOffset ReceivedAt);
