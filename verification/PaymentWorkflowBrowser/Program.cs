using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length != 2)throw new ArgumentException("Usage: PaymentWorkflowBrowser <qa-directory> <base-url>");
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
    var caseInfo=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"case.json"))).RootElement;
    var invoiceId=caseInfo.GetProperty("invoiceId").GetInt32();var code=caseInfo.GetProperty("code").GetString()!;
    async Task Go(string path)
    {await Command("Page.navigate",new {url=url+path});await Eval("new Promise(r=>setTimeout(()=>r(true),300))");await Wait("document.readyState==='complete'");}
    async Task Upload()
    {
        var dom=await Command("DOM.getDocument",new {});var rootNode=dom.GetProperty("root").GetProperty("nodeId").GetInt32();
        var input=await Command("DOM.querySelector",new {nodeId=rootNode,selector="#payment-report-proof"});
        await Command("DOM.setFileInputFiles",new {nodeId=input.GetProperty("nodeId").GetInt32(),files=new[]{Path.Combine(folder,"proof-test.png")}});
        await Wait("document.querySelector('[data-proof-preview]')?.hidden===false");
    }
    async Task Submit()
    {await Eval("document.querySelector('[name=soTien]').value='250000';document.querySelector('[name=ghiChu]').value='QA browser report';true");await Upload();await Eval("document.querySelector('[data-payment-report]').requestSubmit();true");await Wait("document.querySelector('.pw-pending-report')!==null && !document.querySelector('[data-payment-report]')");}
    async Task<JsonElement> Balance()
    {return await Eval("(async()=>{const r=await fetch('/ThongBao/ChiTietDuLieu?maHoaDon='+encodeURIComponent("+JsonSerializer.Serialize(code)+"));return await r.json();})()");}
    async Task CheckMobile(string name)
    {await Command("Emulation.setDeviceMetricsOverride",new {width=360,height=800,deviceScaleFactor=1,mobile=true});await Eval("scrollTo(0,0);true");await Eval("new Promise(r=>setTimeout(()=>r(true),300))");if(!(await Eval("document.documentElement.scrollWidth<=innerWidth")).GetBoolean())throw new Exception("Horizontal overflow: "+name);await Shot(name);await Command("Emulation.setDeviceMetricsOverride",new {width=1540,height=1100,deviceScaleFactor=1,mobile=false});}
    await Command("Page.enable",new {});await Command("Emulation.setDeviceMetricsOverride",new {width=1540,height=1100,deviceScaleFactor=1,mobile=false});
    await Login("KHACH_THUE");await Go("/ThongBao/HoaDon/"+invoiceId);await Wait("document.querySelector('[data-payment-report]')!==null");
    var before=await Balance();await Shot("tenant-payment-form");await CheckMobile("tenant-payment-form-360");
    await Submit();await Wait("document.querySelector('.pw-evidence img')?.naturalWidth>0");
    var after=await Balance();if(before.GetProperty("soConPhaiTra").GetInt64()!=after.GetProperty("soConPhaiTra").GetInt64())throw new Exception("Pending report reduced debt.");
    var paymentId=(await Eval("Number(document.querySelector('.pw-pending-report').parentElement.id.replace('payment-report-',''))")).GetInt32();
    await Shot("tenant-payment-pending");await CheckMobile("tenant-payment-pending-360");
    await Login("CHU_NHA");await Go("/HoaDonDichVu/Management?trangThai=CHO_XAC_NHAN");await Wait("document.querySelector('.payment-bell-toggle')!==null");
    await Eval("document.querySelector('.payment-bell-toggle').click();true");await Wait("document.querySelector('.payment-bell-panel a[href*=\"payment-report-"+paymentId+"\"]')!==null");await Shot("owner-management-notification");
    await Eval("document.querySelector('.payment-bell-panel a[href*=\"payment-report-"+paymentId+"\"]').click();true");await Wait("document.querySelector('[data-confirm-payment]')!==null");await Wait("document.querySelector('.pw-evidence img')?.naturalWidth>0");
    if(!(await Eval("document.querySelector('[data-confirm-payment] button').disabled")).GetBoolean())throw new Exception("Confirm enabled without checks.");
    await Shot("owner-payment-review");await CheckMobile("owner-payment-review-360");
    await Eval("window.confirm=()=>true;document.querySelector('.pw-reject').open=true;document.querySelector('[name=lyDo]').value='QA: transfer not received';document.querySelector('[data-reject-payment]').requestSubmit();true");
    await Wait("document.querySelector('[data-confirm-payment]')===null && document.body.textContent.includes('QA: transfer not received')");
    await Login("KHACH_THUE");await Go("/ThongBao/HoaDon/"+invoiceId);await Wait("document.querySelector('[data-payment-report]')!==null && document.body.textContent.includes('QA: transfer not received')");await Shot("tenant-payment-rejected");
    await Submit();
    await Login("CHU_NHA");await Go("/HoaDonDichVu/Details/"+invoiceId);await Wait("document.querySelector('[data-confirm-payment]')!==null");
    await Eval("window.confirm=()=>true;document.querySelectorAll('[data-confirm-payment] input[type=checkbox]').forEach(x=>{x.checked=true;x.dispatchEvent(new Event('change'));});document.querySelector('[data-confirm-payment]').requestSubmit();true");
    await Wait("document.querySelector('[data-confirm-payment]')===null && document.querySelector('[data-ic-paid]')?.textContent.replace(/[^0-9]/g,'')==='250000'");await Shot("owner-payment-confirmed");
    await Login("KHACH_THUE");await Go("/ThongBao/HoaDon/"+invoiceId);await Wait("document.querySelector('[data-payment-report]')!==null");var confirmed=await Balance();
    if(confirmed.GetProperty("soConPhaiTra").GetInt64()!=before.GetProperty("soConPhaiTra").GetInt64()-250000)throw new Exception("Confirmed debt mismatch.");
    if(!(await Eval("(async()=>{const r=await fetch('/api/thong-bao/danh-sach');return JSON.stringify(await r.json()).includes('DA_XAC_NHAN');})()")).GetBoolean())throw new Exception("Tenant outcome notification missing.");
    await Shot("tenant-payment-confirmed");
    Console.WriteLine("Payment workflow browser passed: customer upload/pending, owner bell/review/reject, resend/confirm, debt invariants, private previews, 360px. Screenshots: "+output);
}
finally {try{browser.Kill(true);}catch{}}
