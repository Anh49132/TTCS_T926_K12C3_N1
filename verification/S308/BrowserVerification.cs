using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

internal sealed class BrowserVerification : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private Process? chrome;
    private int sequence;
    public async Task Start(string folder)
    {
        var executable = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google/Chrome/Application/chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google/Chrome/Application/chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft/Edge/Application/msedge.exe")
        }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Chrome or Edge is required for browser verification.");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "--headless=new", "--disable-gpu", "--no-sandbox", "--no-first-run", "--no-default-browser-check",
            "--remote-debugging-port=" + port, "--user-data-dir=" + Path.Combine(folder, "chrome-profile"), "about:blank" }) start.ArgumentList.Add(arg);
        chrome = Process.Start(start)!;
        using var http = new HttpClient();
        string? endpoint = null;
        for (var i = 0; i < 100; i++) {
            try {
                using var list = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{port}/json/list"));
                endpoint = list.RootElement.EnumerateArray().First(x => x.GetProperty("type").GetString() == "page").GetProperty("webSocketDebuggerUrl").GetString(); break;
            } catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException) { await Task.Delay(100); }
        }
        if (endpoint is null) throw new Exception("Chrome debugging endpoint did not start.");
        await socket.ConnectAsync(new Uri(endpoint), CancellationToken.None);
        await Command("Page.enable", new { });
        await SetViewport(360, 800);
    }
    public async Task SetViewport(int width, int height) =>
        await Command("Emulation.setDeviceMetricsOverride", new { width, height, deviceScaleFactor = 1, mobile = width <= 600 });
    private async Task<JsonElement> Command(string method, object parameters)
    {
        var id = ++sequence;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true) {
            using var message = new MemoryStream();
            var buffer = new byte[16384]; WebSocketReceiveResult received;
            do { received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token); message.Write(buffer, 0, received.Count); } while (!received.EndOfMessage);
            using var doc = JsonDocument.Parse(message.ToArray());
            var json = doc.RootElement;
            if (!json.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id) continue;
            if (json.TryGetProperty("error", out var error)) throw new Exception(error.ToString());
            return json.GetProperty("result").Clone();
        }
    }
    public async Task<JsonElement> Evaluate(string expression)
    {
        var result = await Command("Runtime.evaluate", new { expression, awaitPromise = true, returnByValue = true });
        if (result.TryGetProperty("exceptionDetails", out var error)) throw new Exception(error.ToString());
        return result.GetProperty("result").TryGetProperty("value", out var value) ? value.Clone() : default;
    }
    public async Task Wait(string expression)
    {
        for (var i = 0; i < 100; i++) {
            try { if ((await Evaluate(expression)).ValueKind == JsonValueKind.True) return; }
            catch (Exception ex) when (ex.Message.Contains("context", StringComparison.OrdinalIgnoreCase)) { }
            await Task.Delay(100);
        }
        throw new Exception("Browser condition timed out: " + expression);
    }
    public async Task Navigate(string url)
    {
        await Command("Page.navigate", new { url });
        await Wait("location.href === " + JsonSerializer.Serialize(url) + " && document.readyState === 'complete'");
    }
    public async Task Login(string url, string email)
    {
        await Navigate(url + "/Account/Login");
        await Evaluate("document.querySelector('[name=TaiKhoanDangNhap]').value=" + JsonSerializer.Serialize(email)
            + "; document.querySelector('[name=MatKhau]').value='S308-Test-a1'; document.querySelector('[name=MatKhau]').form.requestSubmit(); true");
        await Wait("location.pathname === '/' && document.readyState === 'complete'");
    }
    public async Task Screenshot(string path)
    {
        var result = await Command("Page.captureScreenshot", new { format = "png", captureBeyondViewport = false });
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(result.GetProperty("data").GetString()!));
    }
    public async Task ClearCookies() => await Command("Network.clearBrowserCookies", new { });
    public async ValueTask DisposeAsync()
    {
        socket.Dispose();
        if (chrome is not null) {
            if (!chrome.HasExited) chrome.Kill(entireProcessTree: true);
            await chrome.WaitForExitAsync(); chrome.Dispose();
        }
    }
}
