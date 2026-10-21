using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace QL_PhongTro.Tests;
public sealed partial class BaoHongTests
{
    private sealed class RepairMailEnvironment:Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName{get;set;}="Development";
        public string ApplicationName{get;set;}="Test";
        public string WebRootPath{get;set;}="";
        public string ContentRootPath{get;set;}="";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider{get;set;}=new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
    private sealed class RepairMailFake:IRepairEmailSender
    {
        public bool Fail {get;set;}
        public List<(string Email,string Subject,string Body,string Path)> Calls {get;}=[];
        public Task SendRepairAsync(string email,string subject,string body,string path)
        { Calls.Add((email,subject,body,path));if(Fail)throw new IOException("secret-provider-error");return Task.CompletedTask; }
    }
    [Fact]
    public async Task RepairEmailNewAndUpdatesQueueCorrectRecipientsAndNeverDuplicate()
    {
        using var f=new Fixture();Managers(f);using var db=f.Context();var input=Input();var service=new BaoHongService(db,f.Time);
        var report=await service.GuiAsync(2,input);await service.GuiAsync(2,input);
        Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM email_bao_hong"));
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE nguoi_nhan_id=1 AND duong_dan LIKE '/ChuNhaBaoHong/%'"));
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE nguoi_nhan_id=5 AND duong_dan LIKE '/QuanLyBaoHong/%'"));
        var update=Update("DANG_XU_LY");await Management(db,f).CapNhatAsync(5,report.Id,update);await Management(db,f).CapNhatAsync(5,report.Id,update);
        Assert.Equal(4,f.Scalar("SELECT COUNT(*) FROM email_bao_hong"));
        await Management(db,f).PhanHoiAsync(5,report.Id,new(){NoiDung="Private reply"});
        Assert.Equal(6,f.Scalar("SELECT COUNT(*) FROM email_bao_hong"));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE nguoi_nhan_id IN(3,4,6)"));
        var mail=new RepairMailFake();var dispatcher=new RepairEmailDispatcher(db,mail,f.Time);
        Assert.Equal(6,await dispatcher.DispatchAsync());Assert.Equal(0,await dispatcher.DispatchAsync());
        Assert.Equal(6,mail.Calls.Count);Assert.All(mail.Calls,x=>{Assert.Contains(report.MaBaoHong,x.Body);Assert.DoesNotContain("Private reply",x.Body);});
        Assert.Equal(2,mail.Calls.Count(x=>x.Email=="tenant@test.local" && x.Path.StartsWith("/BaoHong/Details/")));
    }
    [Fact]
    public async Task RepairEmailRetriesThreeTimesAcrossScopesThenStopsAndSanitizesErrors()
    {
        using var f=new Fixture();using(var db=f.Context())await new BaoHongService(db,f.Time).GuiAsync(2,Input());
        var mail=new RepairMailFake{Fail=true};
        for(var i=0;i<4;i++)
        {
            using var db=f.Context();var dispatcher=new RepairEmailDispatcher(db,mail,f.Time);
            Assert.Equal(0,await dispatcher.DispatchAsync());Assert.Equal(i+1,mail.Calls.Count);
            await dispatcher.DispatchAsync();Assert.Equal(i+1,mail.Calls.Count);
            f.Time.UtcNow=f.Time.UtcNow.AddMinutes(20);
        }
        using(var db=f.Context())await new RepairEmailDispatcher(db,mail,f.Time).DispatchAsync();
        Assert.Equal(4,mail.Calls.Count);Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE trang_thai='THAT_BAI' AND so_lan_gui=4"));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE loi LIKE '%secret-provider-error%'"));
    }
    [Fact]
    public async Task RepairEmailCancelsWhenManagerReassignedOrAccountLocked()
    {
        using var f=new Fixture();Managers(f);using var db=f.Context();await new BaoHongService(db,f.Time).GuiAsync(2,Input());
        f.Execute("UPDATE toa_nha SET quan_ly_id=6 WHERE id=1; UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=1");
        var mail=new RepairMailFake();Assert.Equal(0,await new RepairEmailDispatcher(db,mail,f.Time).DispatchAsync());
        Assert.Empty(mail.Calls);Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE trang_thai='DA_HUY'"));
    }
    [Fact]
    public async Task RepairEmailQueueFailureRollsBackReportAndUpdateTogether()
    {
        using var f=new Fixture();Managers(f);
        f.Execute("CREATE TRIGGER fail_email_queue BEFORE INSERT ON email_bao_hong BEGIN SELECT RAISE(ABORT,'test failure'); END");
        using(var db=f.Context())await Assert.ThrowsAnyAsync<Exception>(()=>new BaoHongService(db,f.Time).GuiAsync(2,Input()));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong"));
        f.Execute("DROP TRIGGER fail_email_queue");int id;
        using(var db=f.Context())id=(await new BaoHongService(db,f.Time).GuiAsync(2,Input())).Id;
        f.Execute("CREATE TRIGGER fail_email_queue BEFORE INSERT ON email_bao_hong BEGIN SELECT RAISE(ABORT,'test failure'); END");
        using(var db=f.Context())await Assert.ThrowsAnyAsync<Exception>(()=>Management(db,f).CapNhatAsync(5,id,Update("DANG_XU_LY")));
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM bao_hong WHERE trang_thai='MOI' AND phien_ban=0"));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
        Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM email_bao_hong"));
    }
    [Fact]
    public async Task RepairEmailExpiredLeaseResumesButNeverAttemptsFifthDelivery()
    {
        using var f=new Fixture();using var db=f.Context();await new BaoHongService(db,f.Time).GuiAsync(2,Input());
        f.Execute("UPDATE email_bao_hong SET trang_thai='DANG_GUI',so_lan_gui=1,khoa_den='2026-01-01T00:00:00.0000000Z'");
        var mail=new RepairMailFake();Assert.Equal(1,await new RepairEmailDispatcher(db,mail,f.Time).DispatchAsync());
        f.Execute("UPDATE email_bao_hong SET trang_thai='DANG_GUI',so_lan_gui=4,khoa_den='2026-01-01T00:00:00.0000000Z'");
        Assert.Equal(0,await new RepairEmailDispatcher(db,mail,f.Time).DispatchAsync());Assert.Single(mail.Calls);
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE trang_thai='THAT_BAI'"));
    }
    [Fact]
    public async Task RepairEmailPickupUsesConfiguredBaseUrlAndRejectsUnsafeLinks()
    {
        using var f=new Fixture();var folder=f.Path+".mail";Directory.CreateDirectory(folder);
        try
        {
            var sender=new PasswordEmailSender(Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions{
                From="noreply@example.test",PickupDirectory=folder,PublicBaseUrl="https://nhatot.example.test"}),new RepairMailEnvironment());
            await sender.SendRepairAsync("recipient@example.test","Repair update","Private update available","/BaoHong/Details/42");
            var files=Directory.GetFiles(folder,"*.txt");Assert.Single(files);
            var text=await File.ReadAllTextAsync(files[0]);Assert.Contains("https://nhatot.example.test/BaoHong/Details/42",text);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>sender.SendRepairAsync("recipient@example.test","Bad","Body","//evil.test/42"));
        }
        finally{Directory.Delete(folder,true);}
    }
    [Fact]
    public async Task RepairEmailAdminFailureLogShowsQueueAndDeniesOtherRoles()
    {
        using var f=new Fixture();Managers(f);using(var db=f.Context())await new BaoHongService(db,f.Time).GuiAsync(2,Input());
        var password=Guid.NewGuid().ToString("N")+"Aa1!";
        f.Execute("INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat) VALUES(7,'Admin','admin@test.local','0900000007','hash','ADMIN',1,1,'2026-01-01','2026-01-01')");
        f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password,4)}'; UPDATE email_bao_hong SET trang_thai='THAT_BAI',so_lan_gui=4,loi='Delivery failed'");
        using var web=FilterWeb(f).WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<IRepairEmailSender>();s.AddSingleton<IRepairEmailSender>(new RepairMailFake());}));
        using var admin=await FilterLogin(web,password,"admin@test.local");
        var html=await admin.GetStringAsync("/EmailGuiLog/BaoHong");Assert.Contains("owner@test.local",html);Assert.Contains("manager@test.local",html);Assert.Contains("Delivery failed",html);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.GetAsync("/EmailGuiLog/BaoHong?trangThai=invalid")).StatusCode);
        using var manager=await FilterLogin(web,password);Assert.Equal(HttpStatusCode.Forbidden,(await manager.GetAsync("/EmailGuiLog/BaoHong")).StatusCode);
    }
}
