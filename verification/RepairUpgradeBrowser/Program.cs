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
    await Command("Page.enable",new {});
    foreach(var role in new[]{"KHACH_THUE","QUAN_LY"}) {
        await Login(role);
        var list=role=="KHACH_THUE"?"/BaoHong":"/QuanLyBaoHong";
        foreach(var width in new[]{1440,360}) {
            await Command("Emulation.setDeviceMetricsOverride",new {width,height=1000,deviceScaleFactor=1,mobile=false});
            await Navigate(list);await Wait("document.querySelector('table')!==null");
            var detail=await Eval("Array.from(document.querySelectorAll('a[href]')).map(x=>x.getAttribute('href')).find(x=>x.includes('/Details/'))");
            await Shot(role+"-list-"+width);
            if((await Eval("document.documentElement.scrollWidth>innerWidth+2")).GetBoolean())throw new Exception("List overflows "+role+width);
            if(detail.ValueKind==JsonValueKind.String){await Navigate(detail.GetString()!);await Shot(role+"-detail-"+width);if((await Eval("document.documentElement.scrollWidth>innerWidth+2")).GetBoolean())throw new Exception("Detail overflows "+role+width);}
            if(role=="KHACH_THUE"){await Navigate("/BaoHong/Create");await Wait("document.querySelector('.repair-create')!==null");await Shot("tenant-create-"+width);if((await Eval("document.documentElement.scrollWidth>innerWidth+2")).GetBoolean())throw new Exception("Create overflows "+width);}
        }
    }
    await Login("KHACH_THUE");await Navigate("/BaoHong/Create");
    var live=await Eval("(()=>{const f=document.querySelector('[data-repair-create]');f.querySelector('select').selectedIndex=1;const d=f.querySelector('[name=\"Input.LoaiThietBi\"]');d.value='QA tap';d.dispatchEvent(new Event('input',{bubbles:true}));return document.querySelector('[data-summary-device]').textContent==='QA tap'&&document.querySelector('[data-summary-room]').textContent!=='Chua chon';})()");
    if(!live.GetBoolean())throw new Exception("Live summary did not update");
    var upload=await Eval("(()=>{const input=document.querySelector('#repair-images');const pick=n=>{const dt=new DataTransfer();for(let i=0;i<n;i++)dt.items.add(new File(['test'],'qa'+i+'.png',{type:'image/png'}));input.files=dt.files;input.dispatchEvent(new Event('change',{bubbles:true}));};pick(2);if(input.files.length!==2)return false;pick(2);if(input.files.length!==2||!document.querySelector('#repair-image-error').textContent)return false;document.querySelector('#repair-image-preview button').click();if(input.files.length!==1)return false;pick(2);return input.files.length===3&&document.querySelector('[data-summary-images]').textContent==='3/3';})()");
    if(!upload.GetBoolean())throw new Exception("Upload limit/removal failed");
    await Login("QUAN_LY");await Navigate("/QuanLyBaoHong/Details/1");
    var quick=await Eval("(()=>{const b=document.querySelector('[data-repair-target]');if(!b)return false;b.click();return document.querySelector('[name=\"CapNhat.TrangThai\"]').value===b.dataset.repairTarget;})()");
    if(!quick.GetBoolean())throw new Exception("Quick action selection failed");
    Console.WriteLine("Browser checks passed. Output: "+output);
}
finally {try{browser.Kill(true);}catch{}}

