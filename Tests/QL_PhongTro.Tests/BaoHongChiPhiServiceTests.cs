using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongChiPhiSchemaTests
{
    private sealed class CostClock : ITimeProvider { public DateTime UtcNow => new(2026,10,14,12,0,0,DateTimeKind.Utc); }
    private static AppDbContext Context(Fixture f, int? actor = null) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={f.Path};Foreign Keys=True;Pooling=False;Default Timeout=20").Options,
        new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,(actor ?? f.Manager).ToString())],"test")) } });
    private static BaoHongChiPhiInput Input(string amount="350000",string side="KHACH_THUE",int version=0) => new() { SoTien=amount,BenChiuPhi=side,PhienBan=version };

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("-1")] [InlineData("0")] [InlineData("350000.5")]
    [InlineData("350.000")] [InlineData("abc")] [InlineData("9223372036854775808")]
    public void ServiceRejectsInvalidMoney(string? value) => Assert.Throws<BaoHongValidationException>(() => BaoHongChiPhiService.ParseAmount(value));

    [Fact]
    public async Task ServiceSavesEditsReplaysAndDisplaysForAllRoles()
    {
        using var f=new Fixture(); using var db=Context(f); var service=new BaoHongChiPhiService(db,new CostClock());
        var input=Input(); var cost=await service.LuuAsync(f.Manager,1,input);
        Assert.Equal(350000,cost.SoTien); Assert.True(cost.ChoThuKhach); Assert.Equal(f.Contract,cost.HopDongId);
        Assert.Equal(1,f.Scalar("SELECT count(*) FROM lich_su_chi_phi_bao_hong"));
        Assert.Equal(cost.Id,(await service.LuuAsync(f.Manager,1,input)).Id);
        Assert.Equal(1,f.Scalar("SELECT count(*) FROM chi_phi_bao_hong"));
        Assert.Equal("CHO_DUA_VAO_HOA_DON",f.Status());
        var tenant=new BaoHongService(db,new CostClock());
        var manager=new QuanLyBaoHongService(db,new CostClock(),tenant);
        Assert.Equal(350000,(await manager.ChiTietAsync(f.Manager,1))!.ChiPhi!.SoTien);
        var reporter=await db.BaoHongs.Where(x=>x.Id==1).Select(x=>x.NguoiBaoId).SingleAsync();
        Assert.Equal(350000,(await tenant.ChiTietAsync(reporter,1))!.ChiPhi!.SoTien);
        Assert.Equal(350000,(await new ChuNhaBaoHongService(db,tenant).ChiTietAsync(f.Owner,1))!.ChiPhi!.SoTien);
        await service.LuuAsync(f.Manager,1,Input(side:"CHU_NHA",version:1));
        Assert.Equal("KHONG_THU_KHACH",f.Status()); Assert.Equal(2,f.Scalar("SELECT count(*) FROM lich_su_chi_phi_bao_hong"));
        Assert.Equal(2,f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='chi_phi_bao_hong'"));
        using var fresh=Context(f); var saved=await fresh.ChiPhiBaoHongs.SingleAsync(); Assert.False(saved.ChoThuKhach); Assert.Null(saved.KyDuKien);
        Assert.Equal(0,f.Scalar("SELECT count(*) FROM chi_tiet_hoa_don WHERE bao_hong_id IS NOT NULL"));
    }

    [Theory] [InlineData(3)] [InlineData(4)]
    public async Task ServiceRejectsNonCompleted(int report)
    { using var f=new Fixture();using var db=Context(f);await Assert.ThrowsAsync<BaoHongValidationException>(()=>new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,report,Input())); }

    [Theory] [InlineData("")] [InlineData("OTHER")] [InlineData(null)]
    public async Task ServiceRejectsPayer(string? payer)
    { using var f=new Fixture();using var db=Context(f);var input=Input();input.BenChiuPhi=payer;await Assert.ThrowsAsync<BaoHongValidationException>(()=>new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,input)); }

    [Fact]
    public async Task ServiceRejectsRolesReadOnlyAndWrongAssignment()
    {
        using var f=new Fixture();using var db=Context(f);var s=new BaoHongChiPhiService(db,new CostClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.LuuAsync(f.Owner,1,Input()));
        var tenant=await db.BaoHongs.Where(x=>x.Id==1).Select(x=>x.NguoiBaoId).SingleAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.LuuAsync(tenant,1,Input()));
        f.Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.LuuAsync(f.Manager,1,Input()));
        f.Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        f.Execute("UPDATE toa_nha SET quan_ly_id=NULL WHERE id=(SELECT toa_nha_id FROM phong_tro WHERE id=(SELECT phong_id FROM bao_hong WHERE id=1))");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.LuuAsync(f.Manager,1,Input()));
    }

    [Fact]
    public async Task ServiceLocksInvoiceLinkedCostAndRejectsStaleAndChangedReplay()
    {
        using var f=new Fixture();using var db=Context(f);var s=new BaoHongChiPhiService(db,new CostClock());var input=Input();
        await s.LuuAsync(f.Manager,1,input);
        await Assert.ThrowsAsync<ChiPhiConflictException>(()=>s.LuuAsync(f.Manager,1,Input()));
        input.SoTien="1";await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.LuuAsync(f.Manager,1,input));
        f.Line(f.Invoice());
        await Assert.ThrowsAsync<ChiPhiConflictException>(()=>s.LuuAsync(f.Manager,1,Input(version:1)));
    }

    [Fact]
    public async Task ServiceAuditFailureRollsBackCostAndHistory()
    {
        using var f=new Fixture();f.Execute("CREATE TRIGGER reject_cost_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='chi_phi_bao_hong' BEGIN SELECT RAISE(ABORT,'test audit failure'); END;");
        using var db=Context(f);
        await Assert.ThrowsAsync<SqliteException>(()=>new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input()));
        Assert.Equal(0,f.Scalar("SELECT count(*) FROM chi_phi_bao_hong"));Assert.Equal(0,f.Scalar("SELECT count(*) FROM lich_su_chi_phi_bao_hong"));
    }

    [Fact]
    public async Task ServiceConcurrentCreateHasOneWinner()
    {
        using var f=new Fixture();
        async Task<bool> Save(){using var db=Context(f);try{await new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input());return true;}catch(ChiPhiConflictException){return false;}}
        var results=await Task.WhenAll(Task.Run(Save),Task.Run(Save));Assert.Single(results,x=>x);
        Assert.Equal(1,f.Scalar("SELECT count(*) FROM chi_phi_bao_hong"));Assert.Equal(1,f.Scalar("SELECT count(*) FROM lich_su_chi_phi_bao_hong"));
    }
    [Fact]
    public async Task ServiceSelectsNextUnissuedOriginalPeriodAndAllowsDraft()
    {
        using var f=new Fixture(); f.Execute($"UPDATE ky_hop_dong SET ngay_ket_thuc='2027-01-31' WHERE hop_dong_id={f.Contract}");
        var november=f.Invoice(); f.Execute($"UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh='2026-10-14',nguoi_phat_hanh_id={f.Owner} WHERE id={november}");
        f.Invoice(12);
        using var db=Context(f);var c=await new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input());
        Assert.Equal(new DateOnly(2026,12,1),c.KyDuKien);Assert.Equal(f.Contract,c.HopDongId);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task ServiceStoresPendingWithoutPeriodForEndedOrExpiredContract(bool ended)
    {
        using var f=new Fixture();
        if(ended) f.Execute($"UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id={f.Contract}");
        else f.Execute($"UPDATE ky_hop_dong SET ngay_ket_thuc='2026-10-31' WHERE hop_dong_id={f.Contract}");
        using var db=Context(f);var c=await new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input());
        Assert.Null(c.KyDuKien);Assert.True(c.ChoThuKhach);Assert.Equal(f.Contract,c.HopDongId);
    }
    [Fact]
    public void CostEndpointRequiresRoleWriteAndCsrf()
    {
        var type=typeof(QL_PhongTro.Controllers.QuanLyBaoHongController);
        Assert.Contains(type.GetCustomAttributes(false),x=>x is Microsoft.AspNetCore.Authorization.AuthorizeAttribute a && a.Roles=="QUAN_LY");
        var method=type.GetMethod("ChiPhi")!;
        Assert.Contains(method.GetCustomAttributes(false),x=>x is Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute);
        Assert.Contains(method.GetCustomAttributes(false),x=>x is QL_PhongTro.Authorization.ModuleAccessAttribute);
    }
    [Fact]
    public async Task InvoiceServicePreservesRepairOnDraftCancelAndReplacement()
    {
        using var f=new Fixture(); using(var db=Context(f)) await new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input());
        var invoice=f.Invoice();f.Line(invoice);
        using(var db=Context(f,f.Owner))
        {
            var service=new HoaDonDichVuService(db,new DichVuService(db),new CostClock());
            var line=await db.ChiTietHoaDons.SingleAsync(x=>x.HoaDonId==invoice);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuNhapAsync(f.Owner,new(){Id=invoice,XoaKhoanIds=[line.Id]}));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuNhapAsync(f.Owner,new(){Id=invoice,Khoan=[new(){Id=line.Id,TenKhoan="Changed",SoTien=1,GhiChu="test"}]}));
            await service.LuuNhapAsync(f.Owner,new(){Id=invoice,HanThanhToan=new(2026,11,8)});
            var version=await db.HoaDons.Where(x=>x.Id==invoice).Select(x=>x.PhienBan).SingleAsync();
            await service.PhatHanhNhapAsync(f.Owner,new(){Id=invoice,PhienBan=version,XacNhan=true,NgayPhatHanh=new(2026,11,1),HanThanhToan=new(2026,11,8)});
            version=await db.HoaDons.Where(x=>x.Id==invoice).Select(x=>x.PhienBan).SingleAsync();
            await service.HuyAsync(f.Owner,new(){Id=invoice,PhienBan=version,XacNhan=true,LyDo="Test replacement on isolated copy"});
            version=await db.HoaDons.Where(x=>x.Id==invoice).Select(x=>x.PhienBan).SingleAsync();
            var replacement=await service.TaoNhapThayTheAsync(f.Owner,invoice,version);
            Assert.Equal(replacement,await service.TaoNhapThayTheAsync(f.Owner,invoice,version));
        }
        Assert.Equal(2,f.Scalar("SELECT count(*) FROM chi_tiet_hoa_don WHERE bao_hong_id=1"));
        Assert.Equal(1,f.Scalar("SELECT count(*) FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id WHERE bao_hong_id=1 AND h.trang_thai<>'DA_HUY'"));
        using(var db=Context(f)) await Assert.ThrowsAsync<ChiPhiConflictException>(()=>new BaoHongChiPhiService(db,new CostClock()).LuuAsync(f.Manager,1,Input(version:1)));
    }
}
