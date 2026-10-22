using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task DeploymentBoundaryLeavesOldInvoiceQueueUntouchedAndProcessesNewNotice()
    {
        using var db=Context();var fixture=await CollectionFixtureAsync(db);
        var invoice=await AddPublishedTenantInvoiceAsync(db,fixture.A,"DEPLOY-INVOICE",false);
        var boundary=await db.ThongBaoHoaDons.MaxAsync(x=>x.Id);
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Deployment:EmailAfterInvoiceNoticeId"]=boundary.ToString()}).Build();
        var sender=new FailingInvoiceSender();var worker=new InvoiceNotificationDispatcher(db,sender,CollectionClock(),configuration);
        Assert.Equal(0,await worker.DispatchAsync());Assert.Equal(0,sender.Attempts);
        Assert.All(await db.ThongBaoHoaDons.AsNoTracking().ToListAsync(),x=>Assert.Equal(0,x.SoLanGui));
        db.ThongBaoHoaDons.Add(new(){HoaDonId=invoice,NguoiNhanId=3,EmailNhan="tenant@example.test",LoaiThongBao="QUA_HAN",NgayTao=CollectionClock().UtcNow});await db.SaveChangesAsync();
        await worker.DispatchAsync();Assert.Equal(1,sender.Attempts);
        Assert.All(await db.ThongBaoHoaDons.AsNoTracking().Where(x=>x.Id<=boundary).ToListAsync(),x=>Assert.Equal(0,x.SoLanGui));
    }
    [Fact]
    public async Task DeploymentBoundaryDoesNotClaimHistoricalPaymentEmails()
    {
        using var db=Context();var fixture=await CollectionFixtureAsync(db);var invoice=await AddPublishedTenantInvoiceAsync(db,fixture.A,"DEPLOY-PAYMENT",false);
        await WorkflowReports(db).GuiCoGhiChuAsync(3,invoice,250000,new(2026,10,13),"TIEN_MAT",null,null,default);
        var payment=await db.ThanhToans.SingleAsync();await PaymentWorkflow.NotifyAsync(db,payment.Id,"BAO_MOI",CollectionClock().UtcNow,default);
        var boundary=await db.EmailGuiLogs.MaxAsync(x=>x.Id);
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Deployment:EmailAfterPaymentLogId"]=boundary.ToString()}).Build();
        var sender=new PaymentMailFake();Assert.Equal(0,await new PaymentEmailDispatcher(db,sender,CollectionClock(),configuration).DispatchAsync());Assert.Empty(sender.Calls);
        Assert.All(await db.EmailGuiLogs.AsNoTracking().ToListAsync(),x=>Assert.Equal(0,x.SoLanThu));
        Assert.Equal(1,await new PaymentEmailDispatcher(db,sender,CollectionClock()).DispatchAsync());Assert.Single(sender.Calls);
    }
}

public sealed partial class BaoHongTests
{
    [Fact]
    public async Task DeploymentBoundaryKeepsHistoricalRepairMailAndSendsNewUpdates()
    {
        using var fixture=new Fixture();Managers(fixture);using var db=fixture.Context();
        var report=await new BaoHongService(db,fixture.Time).GuiAsync(2,Input());
        var boundary=fixture.Scalar("SELECT max(id) FROM email_bao_hong");
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Deployment:EmailAfterRepairId"]=boundary.ToString()}).Build();
        var sender=new RepairMailFake();var worker=new RepairEmailDispatcher(db,sender,fixture.Time,configuration);
        Assert.Equal(0,await worker.DispatchAsync());Assert.Empty(sender.Calls);
        await Management(db,fixture).CapNhatAsync(5,report.Id,Update("DANG_XU_LY"));
        var expected=fixture.Scalar("SELECT COUNT(*) FROM email_bao_hong WHERE id>"+boundary);
        Assert.Equal(expected,await worker.DispatchAsync());Assert.Equal(expected,sender.Calls.Count);
        Assert.Equal(0,fixture.Scalar("SELECT sum(so_lan_gui) FROM email_bao_hong WHERE id<="+boundary));
    }
}
