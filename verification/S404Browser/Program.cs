using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if(args.Length!=2)throw new ArgumentException("Usage: S404Browser <demo-directory> <base-url>");
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
    {var result=await Command("Page.captureScreenshot",new {format="png",captureBeyondViewport=true});await File.WriteAllBytesAsync(Path.Combine(output,name),Convert.FromBase64String(result.GetProperty("data").GetString()!));}
    async Task Viewport(int width,int height,bool mobile)=>await Command("Emulation.setDeviceMetricsOverride",new {width,height,mobile,deviceScaleFactor=1});
    await Command("Page.enable",new {});await Viewport(1440,1000,false);await Login("Owner");await Navigate("/TatToan/Nhap/1");
    await Wait("document.getElementById('st-result-value')?.textContent.includes('3.486.258')===true");await Screenshot("owner-desktop.png");
    await Eval("document.getElementById('st-add').click();const row=document.querySelector('#st-damage-rows tr:last-child');row.querySelector('[data-field=TenKhoan]').value='Khoản kiểm thử';const amount=row.querySelector('[data-field=SoTien]');amount.value='100000';amount.dispatchEvent(new Event('input',{bubbles:true}));true");
    await Wait("document.getElementById('st-result-value').textContent.includes('3.386.258')");
    await Eval("document.querySelector('#st-damage-rows tr:last-child .st-remove').click();true");await Wait("document.getElementById('st-result-value').textContent.includes('3.486.258')");
    await Eval("const input=document.querySelector('[name=\"Input.KhauTru[0].SoTien\"]');input.value='300000';input.dispatchEvent(new Event('input',{bubbles:true}));true");await Wait("document.getElementById('st-result-value').textContent.includes('3.436.258')");
    await Eval("const input2=document.querySelector('[name=\"Input.KhauTru[0].SoTien\"]');input2.value='250000';input2.dispatchEvent(new Event('input',{bubbles:true}));true");await Wait("document.getElementById('st-result-value').textContent.includes('3.486.258')");
    await Viewport(360,900,true);await Task.Delay(400);
    var ownerWidth=await Eval("({viewport:innerWidth,document:document.documentElement.scrollWidth})");await Screenshot("owner-mobile-360.png");
    await Eval("document.getElementById('settlement-form').requestSubmit();true");await Wait("location.pathname==='/TatToan/ChiTiet/1' && document.readyState==='complete'");
    await Login("Tenant");await Navigate("/TatToan/ChiTiet/1");
    var tenantCheck=await Eval("({sameTotal:document.body.textContent.includes('3.486.258'),canEdit:document.body.textContent.includes('Sửa bảng nháp'),viewport:innerWidth,document:document.documentElement.scrollWidth})");
    var forbidden=(await Eval("(async()=>{const r=await fetch('/TatToan/Nhap/1');return r.status})()" )).GetInt32();await Screenshot("tenant-mobile-360.png");
    await Login("ForeignTenant");var foreign=(await Eval("(async()=>{const r=await fetch('/TatToan/ChiTiet/1');return r.status})()" )).GetInt32();
    var report=new {OwnerMobile=ownerWidth,Tenant=tenantCheck,TenantEditStatus=forbidden,ForeignTenantStatus=foreign,DynamicAddEditDelete=true,OwnerSave=true};
    await File.WriteAllTextAsync(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(report));Console.WriteLine("Browser artifacts: "+output);
    if(ownerWidth.GetProperty("document").GetInt32()>360 || tenantCheck.GetProperty("document").GetInt32()>360 || !tenantCheck.GetProperty("sameTotal").GetBoolean() || tenantCheck.GetProperty("canEdit").GetBoolean() || forbidden!=403 || foreign!=403)throw new Exception("Browser verification failed; see report.");
}
finally {if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
