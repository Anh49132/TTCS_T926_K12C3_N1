using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
    private sealed class PaymentMailFake:IPaymentEmailSender
    {
        public bool Fail{get;set;}
        public List<(string Email,string Subject,string Body,string Path)> Calls{get;}=[];
        public Task SendPaymentAsync(string email,string subject,string body,string path)
        {Calls.Add((email,subject,body,path));if(Fail)throw new IOException("private-smtp-error");return Task.CompletedTask;}
    }
    [Fact]
    public async Task PaymentEmailReportAndConfirmNotifyCorrectPeopleWithoutChangingAmountsOrDuplicating()
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"EMAIL-CONFIRM",false);
        await WorkflowReports(db).GuiCoGhiChuAsync(3,id,250000,new(2026,10,13),"TIEN_MAT",null,"private memo",default);
        var payment=await db.ThanhToans.SingleAsync();
        var clock=CollectionClock();var mail=new PaymentMailFake();var dispatcher=new PaymentEmailDispatcher(db,mail,clock);
        await PaymentWorkflow.NotifyAsync(db,payment.Id,"BAO_MOI",clock.UtcNow,default);
        Assert.Single(await db.EmailGuiLogs.Where(x=>x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix)).ToListAsync());
        Assert.Equal(1,await dispatcher.DispatchAsync());Assert.Equal(0,await dispatcher.DispatchAsync());
        Assert.Equal("owner@example.test",mail.Calls[0].Email);Assert.StartsWith("/HoaDonDichVu/Details/",mail.Calls[0].Path);
        Assert.DoesNotContain("private memo",mail.Calls[0].Body);
        var owner=new GhiThuHoaDonService(db,clock);Assert.Equal(1000000,(await owner.ChiTietAsync(1,id)).ConPhaiTra);
        var key=Guid.NewGuid().ToString("N");await owner.XacNhanBaoCaoAsync(1,payment.Id,key);await owner.XacNhanBaoCaoAsync(1,payment.Id,key);
        Assert.Equal(2,await db.EmailGuiLogs.CountAsync(x=>x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix)));
        Assert.Equal(1,await dispatcher.DispatchAsync());Assert.Equal("tenant@example.test",mail.Calls[1].Email);Assert.StartsWith("/ThongBao/HoaDon/",mail.Calls[1].Path);
        var total=await owner.ChiTietAsync(1,id);Assert.Equal(250000,total.TongDaThu);Assert.Equal(750000,total.ConPhaiTra);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PaymentEmailRejectOrCancelSuppressesStaleNewReportAndKeepsDebt(bool cancel)
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"EMAIL-OUTCOME",false);
        var reports=WorkflowReports(db);await reports.GuiAsync(3,id,200000,new(2026,10,13),"TIEN_MAT");var payment=await db.ThanhToans.SingleAsync();
        var owner=new GhiThuHoaDonService(db,CollectionClock());
        if(cancel){await reports.HuyAsync(3,id);await reports.HuyAsync(3,id);}
        else {await owner.TuChoiBaoCaoAsync(1,payment.Id,"Private rejection reason");await owner.TuChoiBaoCaoAsync(1,payment.Id,"Private rejection reason");}
        var mail=new PaymentMailFake();Assert.Equal(1,await new PaymentEmailDispatcher(db,mail,CollectionClock()).DispatchAsync());
        Assert.Single(mail.Calls);Assert.Equal(cancel?"owner@example.test":"tenant@example.test",mail.Calls[0].Email);
        Assert.DoesNotContain("Private rejection reason",mail.Calls[0].Body);
        Assert.Equal(1,await db.EmailGuiLogs.CountAsync(x=>x.TrangThai=="DA_HUY" && x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix)));
        Assert.Equal(1000000,(await owner.ChiTietAsync(1,id)).ConPhaiTra);
    }
    [Fact]
    public async Task PaymentEmailRetryPersistsAcrossRestartStopsAfterFourAttemptsAndHidesProviderError()
    {
        using(var db=Context()){var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"EMAIL-RETRY",false);await WorkflowReports(db).GuiAsync(3,id,200000,new(2026,10,13),"TIEN_MAT");}
        var clock=CollectionClock();var mail=new PaymentMailFake{Fail=true};
        for(var i=0;i<4;i++){
            using var db=Context();var worker=new PaymentEmailDispatcher(db,mail,clock);await worker.DispatchAsync();Assert.Equal(i+1,mail.Calls.Count);
            await worker.DispatchAsync();Assert.Equal(i+1,mail.Calls.Count);clock.UtcNow=clock.UtcNow.AddMinutes(20);
        }
        using var check=Context();await new PaymentEmailDispatcher(check,mail,clock).DispatchAsync();Assert.Equal(4,mail.Calls.Count);
        var row=await check.EmailGuiLogs.SingleAsync(x=>x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix));Assert.Equal("THAT_BAI",row.TrangThai);Assert.DoesNotContain("private-smtp-error",row.LoiGanNhat);
    }
    [Fact]
    public async Task PaymentEmailRevokedRightsPreventDelivery()
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"EMAIL-REVOKED",false);
        await WorkflowReports(db).GuiAsync(3,id,200000,new(2026,10,13),"TIEN_MAT");
        await db.Database.ExecuteSqlRawAsync("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='TAI_CHINH'");
        var mail=new PaymentMailFake();Assert.Equal(0,await new PaymentEmailDispatcher(db,mail,CollectionClock()).DispatchAsync());Assert.Empty(mail.Calls);
        Assert.Equal("DA_HUY",(await db.EmailGuiLogs.AsNoTracking().SingleAsync(x=>x.LoaiThongBao.StartsWith(PaymentEmailQueue.Prefix))).TrangThai);
    }
    [Fact]
    public async Task PaymentEmailQueueFailureRollsBackPaymentAndConfirmation()
    {
        int id,payment;
        using(var db=Context()){
            var f=await CollectionFixtureAsync(db);id=await AddPublishedTenantInvoiceAsync(db,f.A,"EMAIL-ROLLBACK",false);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_payment_email BEFORE INSERT ON email_gui_log WHEN NEW.loai_thong_bao LIKE 'THANH_TOAN:%' BEGIN SELECT RAISE(ABORT,'queue failed'); END");
            await Assert.ThrowsAsync<DbUpdateException>(()=>WorkflowReports(db).GuiAsync(3,id,200000,new(2026,10,13),"TIEN_MAT"));
        }
        using(var db=Context()){
            Assert.Empty(await db.ThanhToans.ToListAsync());Assert.Empty(await db.ThongBaoThanhToans.ToListAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_payment_email");await WorkflowReports(db).GuiAsync(3,id,200000,new(2026,10,13),"TIEN_MAT");payment=(await db.ThanhToans.SingleAsync()).Id;
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_payment_email BEFORE INSERT ON email_gui_log WHEN NEW.loai_thong_bao LIKE 'THANH_TOAN:%' BEGIN SELECT RAISE(ABORT,'queue failed'); END");
            await Assert.ThrowsAsync<DbUpdateException>(()=>new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(1,payment,Guid.NewGuid().ToString("N")));
        }
        using var check=Context();Assert.Equal("CHO_XAC_NHAN",(await check.ThanhToans.SingleAsync()).TrangThai);Assert.Empty(await check.LanThuHoaDons.ToListAsync());
    }
}
