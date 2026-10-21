using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length != 2)throw new ArgumentException("Usage: InvoicePrintBrowser <qa-directory> <base-url>");
var folder=Path.GetFullPath(args[0]);var url=args[1].TrimEnd('/');
var access=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"access.json"))).RootElement;
var output=Path.Combine(folder,"browser-check-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(output);
var edge=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft/Edge/Application/msedge.exe");
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
var psi=new ProcessStartInfo(edge) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
foreach(var arg in new[] {"--headless=new","--no-sandbox","--disable-gpu","--no-first-run","--no-default-browser-check","--remote-debugging-port="+port,"--user-data-dir="+Path.Combine(output,"profile"),"about:blank"})psi.ArgumentList.Add(arg);
using var browser=Process.Start(psi)??throw new Exception("Cannot start Edge.");
try
{
    using var http=new HttpClient(new HttpClientHandler {UseProxy=false});
    string? target=null;
    for(var i=0;i<80 && target is null;i++){try{using var response=await http.PutAsync($"http://127.0.0.1:{port}/json/new?about:blank",null);target=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("webSocketDebuggerUrl").GetString();}catch{await Task.Delay(250);}}
    if(target is null)throw new Exception("Browser debugging endpoint unavailable.");
    using var ws=new ClientWebSocket();await ws.ConnectAsync(new Uri(target),CancellationToken.None);var nextId=0;
    async Task<JsonElement> Command(string method,object parameters)
    {
        var id=++nextId;await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {id,method,@params=parameters})),WebSocketMessageType.Text,true,CancellationToken.None);
        var buffer=new byte[65536];
        while(true){using var stream=new MemoryStream();WebSocketReceiveResult frame;do{frame=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),CancellationToken.None);if(frame.MessageType==WebSocketMessageType.Close)throw new Exception("Browser disconnected.");stream.Write(buffer,0,frame.Count);}while(!frame.EndOfMessage);
            using var message=JsonDocument.Parse(stream.ToArray());var root=message.RootElement;if(root.TryGetProperty("id",out var responseId) && responseId.GetInt32()==id){if(root.TryGetProperty("error",out _))throw new Exception("Browser command failed: "+method);return root.GetProperty("result").Clone();}}
    }
    async Task<JsonElement> Eval(string expression)
    {var response=await Command("Runtime.evaluate",new {expression,awaitPromise=true,returnByValue=true});if(response.TryGetProperty("exceptionDetails",out _))throw new Exception("Browser JavaScript evaluation failed.");return response.GetProperty("result").GetProperty("value").Clone();}
    async Task Wait(string expression)
    {for(var i=0;i<80;i++){try{if((await Eval(expression)).ValueKind==JsonValueKind.True)return;}catch{}await Task.Delay(250);}throw new Exception("Browser condition timed out: "+expression);}
    async Task Navigate(string path)
    {await Command("Page.navigate",new {url=url+path});await Wait($"location.pathname+location.search+location.hash==={JsonSerializer.Serialize(path)} && document.readyState==='complete'");}
    async Task Login(string role)
    {
        await Command("Network.clearBrowserCookies",new {});await Navigate("/Account/Login");
        var account=access.GetProperty("accounts").EnumerateArray().First(x=>x.GetProperty("role").GetString()==role);
        var data=JsonSerializer.Serialize(new {TaiKhoanDangNhap=account.GetProperty("email").GetString(),MatKhau=account.GetProperty("password").GetString()});
        var expression="(async()=>{const data="+data+";data.__RequestVerificationToken=document.querySelector('[name=__RequestVerificationToken]').value;const r=await fetch('/Account/Login',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams(data)});return r.status;})()";
        if((await Eval(expression)).GetInt32()!=200)throw new Exception("QA login failed.");
    }
    async Task Shot(string name)
    {
        await Eval("new Promise(resolve=>setTimeout(()=>resolve(true),600))");
        var capture=await Command("Page.captureScreenshot",new {format="png",captureBeyondViewport=false});
        await File.WriteAllBytesAsync(Path.Combine(output,name+".png"),Convert.FromBase64String(capture.GetProperty("data").GetString()!));
    }
    await Command("Page.enable",new {});await Login("CHU_NHA");
    foreach(var width in new[]{1672,360}) {
        await Command("Emulation.setDeviceMetricsOverride",new {width,height=1100,deviceScaleFactor=1,mobile=false});
        var watch=Stopwatch.StartNew();await Navigate("/BaoCao?tuThang=1&tuNam=2026&denThang=12&denNam=2026");
        await Wait("document.querySelector('.bc-series-chart')!==null");watch.Stop();
        if((await Eval("document.documentElement.scrollWidth>innerWidth+2")).GetBoolean())throw new Exception("Report overflow "+width);
        if((await Eval("document.querySelectorAll('.bc-series-bar').length")).GetInt32()!=18)throw new Exception("Missing series");
        await Navigate("/BaoCao");await Wait("document.querySelectorAll('.bc-series-bar').length===18");await Shot("revenue-dashboard-"+width);Console.WriteLine("Report load "+width+": "+watch.ElapsedMilliseconds+"ms");
    }
    Console.WriteLine("Revenue browser checks passed. Output: "+output);
}
finally {try{browser.Kill(true);}catch{}}
