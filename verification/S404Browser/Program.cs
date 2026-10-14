using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length is <2 or >3)throw new ArgumentException("Usage: S404Browser <demo-directory> <base-url>");
var task4=args.Length==3 && args[2]=="task4";
var task3=args.Length==3 && args[2] is "task3" or "task4";
var folder=Path.GetFullPath(args[0]);var url=args[1].TrimEnd('/');
var access=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"access.json"))).RootElement;
var output=Path.Combine(folder,"browser-check-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(output);
var edge=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft/Edge/Application/msedge.exe");
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
var psi=new ProcessStartInfo(edge) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
foreach(var arg in new[] {"--headless=new","--disable-gpu","--no-first-run","--no-default-browser-check","--remote-debugging-port="+port,"--user-data-dir="+Path.Combine(output,"profile"),"about:blank"})psi.ArgumentList.Add(arg);
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
    async Task Login(string key)
    {
        await Command("Network.clearBrowserCookies",new {});await Navigate("/Account/Login");
        var data=JsonSerializer.Serialize(new {TaiKhoanDangNhap=access.GetProperty(key).GetString(),MatKhau=access.GetProperty("Password").GetString()});
        var expression="(async()=>{const data="+data+";data.__RequestVerificationToken=document.querySelector('[name=__RequestVerificationToken]').value;const r=await fetch('/Account/Login',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams(data)});return r.status;})()";
        if((await Eval(expression)).GetInt32()!=200)throw new Exception("Demo login failed.");
    }
    async Task Screenshot(string name)
    {await Eval("document.activeElement?.blur();scrollTo({top:0,left:0,behavior:'instant'});true");await Wait("scrollY===0");var result=await Command("Page.captureScreenshot",new {format="png",captureBeyondViewport=true});await File.WriteAllBytesAsync(Path.Combine(output,name),Convert.FromBase64String(result.GetProperty("data").GetString()!));}
    async Task Viewport(int width,int height,bool mobile)=>await Command("Emulation.setDeviceMetricsOverride",new {width,height,mobile,deviceScaleFactor=1});
    await Command("Page.enable",new {});await Viewport(1440,1000,false);await Login("Owner");
    if(task3)
    {
        await Navigate("/TraPhong/Nhap/2");
        await Eval("const f=document.getElementById('checkout-input');f.elements['Input.NgayTra'].value='2026-10-13';f.elements['Input.ChiSoDien'].value='1330';f.elements['Input.ChiSoNuoc'].value='91';f.elements['Input.LyDo'].value='KHACH_CHUYEN_DI';f.querySelector('[name=\"Input.ApDungPhat\"][value=true]').checked=true;f.elements['Input.TienPhat'].value='3500000';f.elements['Input.LyDo'].dispatchEvent(new Event('input',{bubbles:true}));true");
        await Wait("document.getElementById('checkout-estimate').textContent.includes('1.863.742')");await Screenshot("checkout-owner-desktop.png");
        await Viewport(360,900,true);await Screenshot("checkout-owner-mobile-360.png");
        if((await Eval("document.documentElement.scrollWidth")).GetInt32()>360)throw new Exception("Checkout overflows mobile.");
        await Viewport(1440,1000,false);
    }
    await Navigate("/TatToan/Nhap/1");
    if(task3)await Eval("const initialNo=document.querySelector('[name=\"Input.ApDungPhat\"][value=false]');initialNo.checked=true;initialNo.dispatchEvent(new Event('input',{bubbles:true}));true");
    await Wait("document.getElementById('st-result-value')?.textContent.includes('3.486.258')===true");await Screenshot("owner-desktop.png");
    await Eval("document.getElementById('st-add').click();const row=document.querySelector('#st-damage-rows tr:last-child');row.querySelector('[data-field=TenKhoan]').value='Khoản kiểm thử';const amount=row.querySelector('[data-field=SoTien]');amount.value='100000';amount.dispatchEvent(new Event('input',{bubbles:true}));true");
    await Wait("document.getElementById('st-result-value').textContent.includes('3.386.258')");
    await Eval("document.querySelector('#st-damage-rows tr:last-child .st-remove').click();true");await Wait("document.getElementById('st-result-value').textContent.includes('3.486.258')");
    await Eval("const input=document.querySelector('[name=\"Input.KhauTru[0].SoTien\"]');input.value='300000';input.dispatchEvent(new Event('input',{bubbles:true}));true");await Wait("document.getElementById('st-result-value').textContent.includes('3.436.258')");
    await Eval("const input2=document.querySelector('[name=\"Input.KhauTru[0].SoTien\"]');input2.value='250000';input2.dispatchEvent(new Event('input',{bubbles:true}));true");await Wait("document.getElementById('st-result-value').textContent.includes('3.486.258')");
    if(task3)
    {
        var invalid=await Eval("(async()=>{const f=new FormData(document.getElementById('settlement-form'));f.delete('Input.LyDo');const r=await fetch('/TatToan/UocTinh',{method:'POST',body:f});return r.status;})()");
        if(invalid.GetInt32()!=400)throw new Exception("Missing reason was accepted.");
        await Eval("document.querySelector('[name=\"Input.ApDungPhat\"][value=true]').checked=true;const fine=document.getElementById('Input_TienPhat');fine.value='3500000';fine.dispatchEvent(new Event('input',{bubbles:true}));true");
        await Wait("document.getElementById('st-result-value').textContent.includes('13.742') && document.getElementById('st-result-label').textContent.includes('trả thêm')");
        await Screenshot("penalty-owner-desktop.png");
        await Eval("const no=document.querySelector('[name=\"Input.ApDungPhat\"][value=false]');no.checked=true;no.dispatchEvent(new Event('input',{bubbles:true}));true");
        await Wait("document.getElementById('st-result-value').textContent.includes('3.486.258')");
        await Eval("const yes=document.querySelector('[name=\"Input.ApDungPhat\"][value=true]');yes.checked=true;yes.dispatchEvent(new Event('input',{bubbles:true}));true");
        await Wait("document.getElementById('st-result-value').textContent.includes('13.742')");
    }
    await Viewport(360,900,true);await Task.Delay(400);
    var ownerWidth=await Eval("({viewport:innerWidth,document:document.documentElement.scrollWidth})");await Screenshot("owner-mobile-360.png");
    await Eval("document.getElementById('settlement-form').requestSubmit();true");await Wait("location.pathname==='/TatToan/ChiTiet/1' && document.readyState==='complete'");
    await Login("Tenant");await Navigate("/TatToan/ChiTiet/1");
    var expectedTotal=task3?"13.742":"3.486.258";
    var tenantCheck=await Eval("({sameTotal:document.body.textContent.includes("+JsonSerializer.Serialize(expectedTotal)+"),canEdit:document.body.textContent.includes('Sửa bảng nháp'),viewport:innerWidth,document:document.documentElement.scrollWidth})");
    if(task3 && (await Eval("document.querySelectorAll('[data-deduction-type=PHAT_COC]').length")).GetInt32()!=1)throw new Exception("Expected exactly one penalty row.");
    var forbidden=(await Eval("(async()=>{const r=await fetch('/TatToan/Nhap/1');return r.status})()" )).GetInt32();await Screenshot("tenant-mobile-360.png");
    await Login("ForeignTenant");var foreign=(await Eval("(async()=>{const r=await fetch('/TatToan/ChiTiet/1');return r.status})()" )).GetInt32();
    var repeatedConfirmation=0;
    if(task4)
    {
        await Login("Owner");await Viewport(1440,1000,false);await Navigate("/TatToan/XacNhan/1");
        await Screenshot("confirm-review-desktop.png");await Viewport(360,900,true);await Screenshot("confirm-review-mobile-360.png");
        if((await Eval("document.documentElement.scrollWidth")).GetInt32()>360)throw new Exception("Confirmation review overflows mobile.");
        await Eval("const check=document.querySelector('[name=\"Confirm.DaKiemTra\"]');check.checked=true;check.closest('form').requestSubmit();true");
        await Wait("document.querySelector('[role=dialog]')!==null && document.readyState==='complete'");await Screenshot("confirmation-modal-mobile-360.png");
        if((await Eval("document.documentElement.scrollWidth")).GetInt32()>360)throw new Exception("Confirmation dialog overflows mobile.");
        var confirmForm=await Eval("const consent=document.querySelector('[name=\"Confirm.DongY\"]');consent.checked=true;Array.from(new FormData(consent.closest('form')))");
        await Eval("document.querySelector('[name=\"Confirm.DongY\"]').closest('form').requestSubmit();true");
        await Wait("location.pathname==='/TatToan/KetQua/1' && document.readyState==='complete'");
        await Screenshot("confirmed-result-mobile-360.png");
        if((await Eval("document.documentElement.scrollWidth")).GetInt32()>360)throw new Exception("Confirmed result overflows mobile.");
        await Viewport(1440,1000,false);await Screenshot("confirmed-result-desktop.png");
        repeatedConfirmation=(await Eval("(async()=>{const r=await fetch('/TatToan/HoanTat',{method:'POST',body:new URLSearchParams("+confirmForm.GetRawText()+")});return r.status})()")).GetInt32();
        if(repeatedConfirmation!=409)throw new Exception("Repeated confirmation did not fail closed.");
        if(!(await Eval("document.body.textContent.includes('Đã kết thúc') && document.body.textContent.includes('13.742') && !document.body.textContent.includes('Sửa bảng nháp')")).GetBoolean())throw new Exception("Confirmed result mismatch.");
        await Navigate("/HopDong/Details/1");
        if(!(await Eval("document.body.textContent.includes('Đã kết thúc')")).GetBoolean())throw new Exception("Contract did not show ended status.");
        await Login("Tenant");await Viewport(360,900,true);await Navigate("/TatToan/ChiTiet/1");await Screenshot("confirmed-tenant-mobile-360.png");
        if(!(await Eval("document.body.textContent.includes('Đã kết thúc') && document.body.textContent.includes('13.742') && !document.body.textContent.includes('Hoàn tất trả phòng') && document.querySelectorAll('[data-deduction-type=PHAT_COC]').length===1")).GetBoolean())throw new Exception("Tenant final snapshot mismatch.");
        var denied=(await Eval("(async()=>{return (await fetch('/TatToan/XacNhan/1')).status})()")).GetInt32();
        if(denied!=403)throw new Exception("Tenant can confirm.");

    }
    var report=new {Task4=task4,RepeatedConfirmationStatus=repeatedConfirmation,Task3=task3,OwnerMobile=ownerWidth,Tenant=tenantCheck,TenantEditStatus=forbidden,ForeignTenantStatus=foreign,DynamicAddEditDelete=true,OwnerSave=true};
    await File.WriteAllTextAsync(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(report));Console.WriteLine("Browser artifacts: "+output);
    if(ownerWidth.GetProperty("document").GetInt32()>360 || tenantCheck.GetProperty("document").GetInt32()>360 || !tenantCheck.GetProperty("sameTotal").GetBoolean() || tenantCheck.GetProperty("canEdit").GetBoolean() || forbidden!=403 || foreign!=403)throw new Exception("Browser verification failed; see report.");
}
finally {if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
