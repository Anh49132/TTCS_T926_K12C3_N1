using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    private static ChuNhaBaoHongService Ownership(AppDbContext db, Fixture f) => new(db, new BaoHongService(db, f.Time));

    [Fact]
    public async Task OwnerSeesSenderPhotosAndOrderedProgressAfterContractEndsWithoutWriteControls()
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context())
        {
            var input = Input(); input.Images = [Photo()];
            id = (await new BaoHongService(db, f.Time).GuiAsync(2, input)).Id;
            await Management(db, f).CapNhatAsync(5, id, Update("DANG_XU_LY"));
            // Same timestamp: ID provides deterministic history ordering.
            await Management(db, f).CapNhatAsync(5, id, Update("HOAN_THANH", 1));
        }
        f.Execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=1");
        using var check = f.Context(); var service = Ownership(check, f);
        Assert.Single(await service.DanhSachAsync(1, 1));
        var detail = (await service.ChiTietAsync(1, id))!;
        Assert.True(detail.LaChuNha); Assert.False(detail.LaQuanLy); Assert.False(detail.ChoPhepXuLy);
        Assert.Empty(detail.TrangThaiTiepTheo); Assert.Equal("Tenant", detail.TenNguoiBao);
        Assert.Equal("HOAN_THANH", detail.BaoHong.TrangThai); Assert.Single(detail.Images);
        Assert.Equal(2, detail.TraoDois.Count);
        Assert.Equal(detail.TraoDois.OrderBy(x => x.ThoiDiem).ThenBy(x => x.Id).Select(x => x.Id), detail.TraoDois.Select(x => x.Id));
        Assert.All(detail.TraoDois, x => { Assert.Equal(5, x.NguoiGuiId); Assert.Equal(f.Time.UtcNow, x.ThoiDiem); });
        Assert.Null(await service.ChiTietAsync(1, int.MaxValue));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Management(check, f).CapNhatAsync(1, id, Update("DANG_XU_LY", 2)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Management(check, f).PhanHoiAsync(1, id, new() { NoiDung = "Không được gửi" }));
        Assert.Equal(2, f.Scalar("SELECT COUNT(*) FROM bao_hong_trao_doi"));
    }

    [Fact]
    public async Task OwnershipTransferImmediatelyRevokesOldOwnerAndAllowsNewOwner()
    {
        using var f = new Fixture(); using var db = f.Context();
        var id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        f.Execute("""
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            VALUES(7,'New owner','newowner@test.local','0900000007','hash','CHU_NHA',1,1,'2026-01-01','2026-01-01');
            """);
        var service = Ownership(db, f);
        Assert.Empty(await service.DanhSachAsync(7, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(7, id));
        f.Execute("UPDATE toa_nha SET chu_nha_id=7 WHERE id=1");
        Assert.Empty(await service.DanhSachAsync(1, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(1, id));
        Assert.NotNull(await service.ChiTietAsync(7, id));
    }

    [Theory]
    [InlineData("UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=1")]
    [InlineData("UPDATE tai_khoan SET is_deleted=1 WHERE id=1")]
    [InlineData("UPDATE tai_khoan SET email_confirmed=0 WHERE id=1")]
    [InlineData("UPDATE tai_khoan SET must_change_password=1 WHERE id=1")]
    [InlineData("UPDATE tai_khoan SET vai_tro='ADMIN' WHERE id=1")]
    [InlineData("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='BAO_HONG'")]
    [InlineData("DELETE FROM role_permission WHERE RoleCode='CHU_NHA' AND ModuleCode='BAO_HONG'")]
    public async Task OwnerReadChecksLiveAccountAndModulePermission(string change)
    {
        using var f = new Fixture(); using var db = f.Context();
        var id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input())).Id;
        f.Execute(change); var service = Ownership(db, f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DanhSachAsync(1, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(1, id));
    }

    [Fact]
    public async Task ReadOnlyModulePermissionAllowsOwnerViewingAndPaginationKeepsScope()
    {
        using var f = new Fixture(); using var db = f.Context(); var tenant = new BaoHongService(db, f.Time);
        for (var i = 0; i < 22; i++) await tenant.GuiAsync(2, Input());
        f.Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='CHU_NHA' AND ModuleCode='BAO_HONG'");
        var service = Ownership(db, f);
        Assert.Equal(21, (await service.DanhSachAsync(1, 1)).Count);
        Assert.Equal(2, (await service.DanhSachAsync(1, 2)).Count);
        Assert.Empty(await service.DanhSachAsync(1, 3));
        Assert.NotNull(await service.ChiTietAsync(1, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(2, 1));
    }
}
