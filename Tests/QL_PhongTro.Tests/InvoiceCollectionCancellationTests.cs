using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private static HuyLanThuHoaDonInput CancelReceipt(int id, string? reason = "Nhập nhầm số tiền", int? version = 0) =>
        new() { ThanhToanId = id, LyDo = reason, PhienBan = version };

    [Fact]
    public async Task CollectionCancellationPartialThenRecollectPreservesHistoryAndOriginalInvoice()
    {
        using var db = Context(); var f = await CollectionFixtureAsync(db);
        var id = await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-PARTIAL",false);
        var service = new GhiThuHoaDonService(db,CollectionClock());
        await service.GhiAsync(1,Receipt(id,400_000)); var originalInput=Receipt(id,600_000);
        var full=await service.GhiAsync(1,originalInput); var receipt=full.LanThus[1];
        var snapshot=JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x=>x.Id==id));
        var recording=JsonSerializer.Serialize(await db.LanThuHoaDons.AsNoTracking().SingleAsync(x=>x.ThanhToanId==receipt.Id));
        var partial=await service.HuyAsync(1,CancelReceipt(receipt.Id,"  Nhập nhầm số tiền  "));
        Assert.Equal(400_000,partial.TongDaThu); Assert.Equal(600_000,partial.ConPhaiTra);
        Assert.Equal("THANH_TOAN_MOT_PHAN",partial.TrangThai); Assert.True(partial.ChoPhepGhiThu);
        Assert.Equal(2,partial.LanThus.Count); var canceled=partial.LanThus.Single(x=>x.Id==receipt.Id);
        Assert.True(canceled.DaHuy); Assert.False(canceled.ChoPhepHuy); Assert.Equal(1,canceled.PhienBan);
        Assert.Equal("Nhập nhầm số tiền",canceled.LyDoHuy); Assert.Equal("Owner",canceled.NguoiHuy); Assert.Equal(CollectionClock().UtcNow,canceled.ThoiDiemHuy);
        Assert.Equal(recording,JsonSerializer.Serialize(await db.LanThuHoaDons.AsNoTracking().SingleAsync(x=>x.ThanhToanId==receipt.Id)));
        Assert.Equal(snapshot,JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x=>x.Id==id)));
        // Replaying the original recording never revives the canceled receipt.
        Assert.Equal(400_000,(await service.GhiAsync(1,originalInput)).TongDaThu);
        var replenished=await service.GhiAsync(1,Receipt(id,600_000));
        Assert.Equal("DA_THANH_TOAN",replenished.TrangThai); Assert.Equal(0,replenished.ConPhaiTra);
        Assert.Equal(3,replenished.LanThus.Count); Assert.Equal(replenished.TongDaThu,replenished.LanThus.Where(x=>!x.DaHuy).Sum(x=>x.SoTien));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HuyAsync(1,CancelReceipt(receipt.Id)));
        Assert.Single(await db.HuyLanThuHoaDons.ToListAsync());
        var audit=await db.NhatKyHoatDongs.AsNoTracking().Where(x=>x.LoaiDoiTuong=="thanh_toan"&&x.HanhDong=="HUY").SingleAsync();
        Assert.DoesNotContain("Nhập nhầm số tiền",JsonSerializer.Serialize(audit));
        using var reloaded=Context(); var persisted=await new GhiThuHoaDonService(reloaded,CollectionClock()).ChiTietAsync(1,id);
        Assert.Equal("Nhập nhầm số tiền",persisted.LanThus.Single(x=>x.DaHuy).LyDoHuy);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(500)]
    public async Task CollectionCancellationOnlyReceiptBecomesUnpaidWithoutAgeOrRecorderRestriction(int length)
    {
        int id,receiptId;
        using(var db=Context()) {
            var f=await CollectionFixtureAsync(db); id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-SINGLE",false);
            var full=await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,1_000_000)); receiptId=full.LanThus.Single().Id;
            await db.Database.ExecuteSqlRawAsync("UPDATE toa_nha SET chu_nha_id=2 WHERE id=1");
        }
        using var formerOwner=Context();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new GhiThuHoaDonService(formerOwner,CollectionClock()).HuyAsync(1,CancelReceipt(receiptId)));
        using var nextOwner=Context(2);
        var yearsLater=new MockTimeProvider{UtcNow=new(2028,10,13,5,0,0,DateTimeKind.Utc)};
        var result=await new GhiThuHoaDonService(nextOwner,yearsLater).HuyAsync(2,CancelReceipt(receiptId,new string('a',length)));
        Assert.Equal(0,result.TongDaThu); Assert.Equal(1_000_000,result.ConPhaiTra); Assert.Equal("CHUA_THANH_TOAN",result.TrangThai);
        Assert.Equal("Owner",result.LanThus.Single().NguoiXacNhan); Assert.Equal("Other",result.LanThus.Single().NguoiHuy);
        Assert.Equal(yearsLater.UtcNow,result.LanThus.Single().ThoiDiemHuy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    [InlineData("abcd")]
    [InlineData("  abcd  ")]
    [InlineData("LONG")]
    public async Task CollectionCancellationRejectsInvalidReasonWithoutChangingMoney(string? reason)
    {
        using var db=Context(); var f=await CollectionFixtureAsync(db); var id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-REASON",false);
        var service=new GhiThuHoaDonService(db,CollectionClock()); var full=await service.GhiAsync(1,Receipt(id,1_000_000));
        if(reason=="LONG")reason=new string('a',501);
        var ex=await Assert.ThrowsAsync<GhiThuValidationException>(()=>service.HuyAsync(1,CancelReceipt(full.LanThus[0].Id,reason)));
        Assert.Contains("LyDo",ex.Errors.Keys); Assert.Empty(await db.HuyLanThuHoaDons.ToListAsync());
        Assert.Equal("DA_XAC_NHAN",(await db.ThanhToans.AsNoTracking().SingleAsync()).TrangThai);
        Assert.Equal(1_000_000,(await service.ChiTietAsync(1,id)).TongDaThu);
    }

    [Fact]
    public async Task CollectionCancellationConcurrentRequestsOnlyOneTransitionAndAudit()
    {
        int id,receiptId;
        using(var db=Context()) { var f=await CollectionFixtureAsync(db);id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-RACE",false);
            receiptId=(await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,1_000_000))).LanThus[0].Id; }
        using var barrier=new Barrier(2);
        async Task<bool> Run()=>await Task.Run(async()=>{
            using var context=Context();barrier.SignalAndWait();
            try{await new GhiThuHoaDonService(context,CollectionClock()).HuyAsync(1,CancelReceipt(receiptId));return true;}
            catch(InvalidOperationException){return false;}
        });
        var results=await Task.WhenAll(Run(),Run());Assert.Single(results,x=>x);
        using var verify=Context();Assert.Single(await verify.HuyLanThuHoaDons.ToListAsync());
        Assert.Equal(1,await verify.NhatKyHoatDongs.CountAsync(x=>x.LoaiDoiTuong=="thanh_toan"&&x.HanhDong=="HUY"));
        Assert.Equal(0,(await new GhiThuHoaDonService(verify,CollectionClock()).ChiTietAsync(1,id)).TongDaThu);
    }

    private sealed class CancelReceiptDuringSave(CancellationTokenSource source) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,CancellationToken cancellationToken=default)
        {
            if(eventData.Context!.ChangeTracker.Entries<HuyLanThuHoaDon>().Any(x=>x.State==EntityState.Added))source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("metadata")]
    [InlineData("disconnect")]
    public async Task CollectionCancellationRollbackRestoresReceiptAndBalance(string fault)
    {
        int id,receiptId;
        using(var db=Context()) {
            var f=await CollectionFixtureAsync(db);id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-ROLLBACK",false);
            receiptId=(await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,1_000_000))).LanThus[0].Id;
            if(fault=="audit")await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER cancel_test_fault BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'forced fault'); END;");
            if(fault=="metadata")await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER cancel_test_fault BEFORE INSERT ON huy_lan_thu_hoa_don BEGIN SELECT RAISE(ABORT,'forced fault'); END;");
        }
        using(var source=new CancellationTokenSource()){
            var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(new SqliteConnectionStringBuilder{DataSource=path,ForeignKeys=true,Pooling=false}.ToString());
            if(fault=="disconnect")options.AddInterceptors(new CancelReceiptDuringSave(source));
            using var context=new AppDbContext(options.Options);
            await Assert.ThrowsAnyAsync<Exception>(()=>new GhiThuHoaDonService(context,CollectionClock()).HuyAsync(1,CancelReceipt(receiptId),source.Token));
        }
        using var verify=Context();Assert.Equal("DA_XAC_NHAN",(await verify.ThanhToans.SingleAsync()).TrangThai);
        Assert.Empty(await verify.HuyLanThuHoaDons.ToListAsync());
        Assert.Equal(0,await verify.NhatKyHoatDongs.CountAsync(x=>x.HanhDong=="HUY"&&x.LoaiDoiTuong=="thanh_toan"));
        Assert.Equal(1_000_000,(await new GhiThuHoaDonService(verify,CollectionClock()).ChiTietAsync(1,id)).TongDaThu);
    }

    [Fact]
    public async Task CollectionCancellationSupportsLegacyAndDatabaseRejectsRestoreOrHistoryDeletion()
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-LEGACY",false);
        var legacy=new ThanhToanHoaDon{HoaDonId=id,SoTien=1_000_000,NgayThanhToan=new(2020,1,1),TrangThai="DA_XAC_NHAN"};
        db.ThanhToans.Add(legacy);await db.SaveChangesAsync();
        var result=await new GhiThuHoaDonService(db,CollectionClock()).HuyAsync(1,CancelReceipt(legacy.Id));
        Assert.Equal(0,result.TongDaThu);Assert.Contains("dữ liệu cũ",result.LanThus.Single().NguoiXacNhan);
        Assert.Equal("Owner",result.LanThus.Single().NguoiHuy);Assert.Empty(await db.LanThuHoaDons.ToListAsync());
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE thanh_toan SET trang_thai='DA_XAC_NHAN'"));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE huy_lan_thu_hoa_don SET ly_do_huy='Changed reason'"));
        await Assert.ThrowsAsync<SqliteException>(()=>db.Database.ExecuteSqlRawAsync("DELETE FROM huy_lan_thu_hoa_don"));
        Assert.Equal(1,await db.ThanhToans.CountAsync());Assert.Equal(1,await db.HuyLanThuHoaDons.CountAsync());
    }

    [Fact]
    public async Task CollectionCancellationHttpValidatesRightsCsrfAndPersistsCanceledHistory()
    {
        int id,receiptId;
        using(var db=Context()){var f=await CollectionFixtureAsync(db);id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-HTTP",false);
            receiptId=(await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,1_000_000))).LanThus[0].Id;}
        using var factory=BillingWeb(CollectionClock());
        using var anonymous=factory.CreateClient(new(){AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Redirect,(await anonymous.PostAsync("/HoaDonDichVu/CancelCollection",new FormUrlEncodedContent([]))).StatusCode);
        using var owner=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(owner,"owner");
        var values=new Dictionary<string,string>{["ThanhToanId"]=receiptId.ToString(),["PhienBan"]="0",["LyDo"]="Nhập nhầm số tiền",["NguoiHuyId"]="2"};
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/HoaDonDichVu/CancelCollection",new FormUrlEncodedContent(values))).StatusCode);
        using var other=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(other,"other");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(other,"/HoaDonDichVu/CancelCollection",new(values))).StatusCode);
        using var tenant=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(tenant,"tenant");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(tenant,"/HoaDonDichVu/CancelCollection",new(values))).StatusCode);
        var invalid=await Post(owner,"/HoaDonDichVu/CancelCollection",new(values){["LyDo"]="abcd"});Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        var accepted=await Post(owner,"/HoaDonDichVu/CancelCollection",values);Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);
        using var json=JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());Assert.Equal("CHUA_THANH_TOAN",json.RootElement.GetProperty("trangThai").GetString());
        Assert.Equal(1_000_000,json.RootElement.GetProperty("conPhaiTra").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict,(await Post(owner,"/HoaDonDichVu/CancelCollection",values)).StatusCode);
        var html=WebUtility.HtmlDecode(await owner.GetStringAsync($"/HoaDonDichVu/Details/{id}"));
        Assert.Contains("<del>",html);Assert.Contains("Đã hủy",html);Assert.Contains("Nhập nhầm số tiền",html);Assert.Contains("Người hủy: Owner",html);
        Assert.Contains("id=\"ic-open\"",html);Assert.DoesNotContain("id=\"ic-open\" hidden",html);
        using var verify=Context();Assert.Equal(1,(await verify.HuyLanThuHoaDons.SingleAsync()).NguoiHuyId);
    }

    [Fact]
    public async Task CollectionCancellationRejectsWrongVersionAndRevokedFinancePermission()
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);var id=await AddPublishedTenantInvoiceAsync(db,f.A,"CANCEL-PERMISSION",false);
        var service=new GhiThuHoaDonService(db,CollectionClock());var receipt=(await service.GhiAsync(1,Receipt(id))).LanThus[0];
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HuyAsync(1,CancelReceipt(receipt.Id,version:null)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HuyAsync(1,CancelReceipt(receipt.Id,version:1)));
        await db.Database.ExecuteSqlRawAsync("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='TAI_CHINH'");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.HuyAsync(1,CancelReceipt(receipt.Id)));
        Assert.Empty(await db.HuyLanThuHoaDons.ToListAsync());
    }
}
