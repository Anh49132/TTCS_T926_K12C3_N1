using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length != 2)throw new ArgumentException("Usage: TenantInvoiceBrowser <qa-directory> <base-url>");
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
    {await Command("Page.navigate",new {url=url+path});await Wait($"location.pathname==={JsonSerializer.Serialize(path)} && document.readyState==='complete'");}
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
    async Task Mobile(string name)
    {
        await Command("Emulation.setDeviceMetricsOverride",new {width=360,height=800,deviceScaleFactor=1,mobile=true});
        await Eval("new Promise(r=>setTimeout(()=>r(true),200))");
        await Eval("(document.querySelector('[data-invoice-bank]') || document.querySelector('[data-bank-settings]'))?.scrollIntoView({block:'start'});true");
        if(!(await Eval("document.documentElement.scrollWidth<=innerWidth")).GetBoolean())throw new Exception("Horizontal overflow: "+name);
        await Shot(name);
        await Command("Emulation.setDeviceMetricsOverride",new {width=1440,height=1000,deviceScaleFactor=1,mobile=false});
    }
    await Command("Page.enable",new {});
    await Command("Emulation.setDeviceMetricsOverride",new {width=1540,height=1050,deviceScaleFactor=1,mobile=false});
    await Login("KHACH_THUE");await Navigate("/ThongBao/DanhSach");
    await Wait("document.querySelectorAll('#tenant-invoice-list-rows tr').length===10");
    await Wait("document.querySelector('[data-total]').textContent!=='undefined'");
    var link=(await Eval("document.querySelector('.til-view').getAttribute('href')")).GetString()!;
    if(!System.Text.RegularExpressions.Regex.IsMatch(link,@"/ThongBao/HoaDon/\d+$"))throw new Exception("Detail link does not use invoice ID.");
    await Shot("tenant-invoice-list");await Mobile("tenant-invoice-list-360");
    await Command("Emulation.setDeviceMetricsOverride",new {width=360,height=800,deviceScaleFactor=1,mobile=true});
    await Eval("document.querySelector('#tenant-invoice-list-results').scrollIntoView();true");await Shot("tenant-invoice-cards-360");
    await Command("Emulation.setDeviceMetricsOverride",new {width=1540,height=1050,deviceScaleFactor=1,mobile=false});
    await Eval("document.querySelector('.til-view').click();true");
    await Wait("/^\\/ThongBao\\/HoaDon\\/\\d+$/.test(location.pathname) && document.readyState==='complete'");
    if((await Eval("document.querySelector('script[src*=tenant-invoice-detail]')!==null")).GetBoolean())throw new Exception("Legacy screen loaded.");
    await Shot("tenant-canonical-detail");
    await Navigate("/ThongBao/DanhSach");await Wait("document.querySelectorAll('#tenant-invoice-list-rows tr').length===10");
    await Eval("var size=document.querySelector('#til-page-size');size.value='20';size.dispatchEvent(new Event('change'));true");
    await Wait("document.querySelectorAll('#tenant-invoice-list-rows tr').length===20");
    await Eval("document.querySelector('[name=timKiem]').value='NO-MATCH-QA-TEST';document.querySelector('.tenant-invoice-filters').requestSubmit();true");
    await Wait("document.querySelector('#tenant-invoice-list-no-match')?.hidden===false");
    await Navigate("/ThongBao/DanhSach");await Wait("document.querySelectorAll('#tenant-invoice-list-rows tr').length===10");
    await Eval("document.querySelector('[data-group=DA_THANH_TOAN]').click();true");
    await Wait("document.querySelector('[data-group=DA_THANH_TOAN]').classList.contains('active') && document.querySelectorAll('#tenant-invoice-list-rows tr').length>0");
    if(!(await Eval("[...document.querySelectorAll('.til-debt')].every(x=>x.textContent.replace(/[^0-9]/g,'')==='0')")).GetBoolean())throw new Exception("Paid tab contains debt.");
    Console.WriteLine("Browser passed: redesigned list, direct detail, page size, search, paid tab, desktop/360px. Screenshots: "+output);
}
finally {try{browser.Kill(true);}catch{}}
