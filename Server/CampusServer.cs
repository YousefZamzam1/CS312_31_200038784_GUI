using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SmartCampus;

public sealed class CampusServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private CancellationTokenSource _stop = new();
    private readonly List<CampusNotice> _notices = [];
    private readonly object _sync = new();
    private Task? _loop;
    public int Port { get; }
    public bool IsRunning => _listener.IsListening;
    public IReadOnlyList<CampusNotice> Notices { get { lock (_sync) return _notices.ToArray(); } }
    public event Action<string>? ClientConnected;

    public CampusServer(int preferredPort = 5088)
    {
        Port = FindPort(preferredPort);
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
    }

    public void Start()
    {
        if (IsRunning) return;
        if (_stop.IsCancellationRequested)
        {
            _stop.Dispose();
            _stop = new CancellationTokenSource();
        }
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_stop.Token));
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _stop.Cancel();
        _listener.Stop();
    }

    public void Publish(CampusNotice notice) { lock (_sync) _notices.Insert(0, notice); }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleAsync(context, token), token);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            if (context.Request.HttpMethod == "OPTIONS") { context.Response.StatusCode = 204; context.Response.Close(); return; }
            object response;
            if (context.Request.Url?.AbsolutePath == "/health") response = new { status = "online", service = "SmartCampus", port = Port };
            else if (context.Request.Url?.AbsolutePath == "/notices" && context.Request.HttpMethod == "GET")
            { lock (_sync) response = _notices.ToArray(); }
            else if (context.Request.Url?.AbsolutePath == "/notices" && context.Request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync(token);
                var notice = JsonSerializer.Deserialize<CampusNotice>(body);
                if (notice is null) { context.Response.StatusCode = 400; response = new { error = "Invalid notice" }; }
                else { Publish(notice); ClientConnected?.Invoke("Notice received over TCP/IP"); response = new { ok = true }; }
            }
            else { context.Response.StatusCode = 404; response = new { error = "Not found", endpoints = new[] { "/health", "/notices" } }; }
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, token);
            context.Response.Close();
        }
        catch (Exception) { try { context.Response.Abort(); } catch { } }
    }

    private static int FindPort(int preferred)
    {
        for (var port = preferred; port < preferred + 20; port++)
        {
            var probe = new TcpListener(IPAddress.Loopback, port);
            try { probe.Start(); probe.Stop(); return port; }
            catch (SocketException) { probe.Stop(); }
        }
        throw new InvalidOperationException("No available port found for the campus server.");
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
        _stop.Dispose();
    }
}
