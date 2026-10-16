using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length != 2)throw new ArgumentException("Usage: OwnerBankBrowser <qa-directory> <base-url>");
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
    await Command("Emulation.setDeviceMetricsOverride",new {width=1440,height=1000,deviceScaleFactor=1,mobile=false});
    await Login("CHU_NHA"); await Navigate("/HoSo/CuaToi");
    await Wait("document.querySelector('[data-bank-edit]')?.disabled===false");
    await Eval("document.querySelector('[data-bank-edit]').click();true");
    await Eval("document.querySelector('[name=TenNganHang]').value='';document.querySelector('[name=SoTaiKhoan]').value='';document.querySelector('[data-bank-form]').requestSubmit();true");
    await Wait("document.querySelector('[data-bank-error=TenNganHang]').textContent.length>0 && document.querySelector('[data-bank-error=SoTaiKhoan]').textContent.length>0");
    await Eval("document.querySelector('[name=TenNganHang]').value='QA Bank';document.querySelector('[name=SoTaiKhoan]').value='000123456789';true");
    var dom=await Command("DOM.getDocument",new {});var rootNode=dom.GetProperty("root").GetProperty("nodeId").GetInt32();
    var input=await Command("DOM.querySelector",new {nodeId=rootNode,selector="#bank-file"});
    await Command("DOM.setFileInputFiles",new {nodeId=input.GetProperty("nodeId").GetInt32(),files=new[]{Path.Combine(folder,"qr-test.png")}});
    await Wait("!document.querySelector('[data-bank-preview]').hidden");
    await Eval("document.querySelector('[data-bank-form]').requestSubmit();true");
    await Wait("document.querySelector('[data-bank-form]').hidden && !document.querySelector('[data-bank-qr]').hidden");
    await Wait("document.querySelector('[data-bank-qr]').complete && document.querySelector('[data-bank-qr]').naturalWidth>0");
    await Eval("document.querySelector('[data-bank-settings]').scrollIntoView();true");await Shot("owner-bank");await Mobile("owner-bank-360");
    await Login("KHACH_THUE");
    // Invoice code is selected on the isolated database by the preparatory script.
    var code=(await File.ReadAllTextAsync(Path.Combine(folder,"invoice-code.txt"))).Trim();
    await Command("Page.navigate",new {url=url+"/ThongBao/ChiTiet?maHoaDon="+Uri.EscapeDataString(code)});
    await Wait("document.querySelector('[data-payment-report]')?.hidden===false");
    await Eval("var m=document.querySelector('[name=hinhThucThanhToan][value=CHUYEN_KHOAN]');m.checked=true;m.dispatchEvent(new Event('change'));true");
    await Wait("!document.querySelector('[data-bank-details]').hidden && document.querySelector('[data-bank-number]').textContent==='000123456789'");
    await Wait("document.querySelector('[data-bank-qr]').naturalWidth>0");
    await Eval("document.querySelector('[data-invoice-bank]').scrollIntoView();true");await Shot("tenant-transfer");await Mobile("tenant-transfer-360");
    await Eval("var m=document.querySelector('[name=hinhThucThanhToan][value=TIEN_MAT]');m.checked=true;m.dispatchEvent(new Event('change'));true");
    if(!(await Eval("document.querySelector('[data-invoice-bank]').hidden")).GetBoolean())throw new Exception("Bank block visible for cash.");
    await Login("CHU_NHA");await Navigate("/HoSo/CuaToi");await Wait("document.querySelector('[data-bank-edit]')?.disabled===false");
    await Eval("document.querySelector('[data-bank-edit]').click();document.querySelector('[data-bank-remove]').click();document.querySelector('[data-bank-cancel]').click();true");
    if(!(await Eval("!document.querySelector('[data-bank-qr]').hidden")).GetBoolean())throw new Exception("Cancel did not restore QR.");
    Console.WriteLine("Browser passed: save/upload, current owner bank, transfer/cash toggle, cancel, 360px overflow checks. Screenshots: "+output);
}
finally {try{browser.Kill(true);}catch{}}
