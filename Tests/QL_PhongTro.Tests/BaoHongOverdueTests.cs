using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class BaoHongOverdueRuleTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void OverdueUsesStrictTwentyFourHourBoundary(long extraTicks, bool expected)
    {
        // SQLite materializes UTC values as Unspecified. Crossing VN midnight is irrelevant.
        var created = new DateTime(2026, 10, 14, 16, 59, 59, DateTimeKind.Unspecified);
        var report = new BaoHong { MucDo = "GAP", TrangThai = "MOI", NgayTao = created };
        var now = DateTime.SpecifyKind(created.AddHours(24).AddTicks(extraTicks), DateTimeKind.Utc);
        Assert.Equal(expected, BaoHongCanhBaoService.DanhGia(report, now) is not null);
    }

    [Theory]
    [InlineData("THUONG", "MOI")]
    [InlineData("THUONG", "DANG_XU_LY")]
    [InlineData("GAP", "DANG_XU_LY")]
    [InlineData("GAP", "HOAN_THANH")]
    [InlineData("GAP", "TU_CHOI")]
    public void OrdinaryAndHandledReportsNeverWarn(string priority, string status)
    {
        var created = new DateTime(2026, 10, 14, 5, 0, 0, DateTimeKind.Utc);
        Assert.Null(BaoHongCanhBaoService.DanhGia(new BaoHong
        { MucDo = priority, TrangThai = status, NgayTao = created }, created.AddHours(100)));
    }

    [Theory]
    [InlineData(26, 0, "26 giờ 0 phút")]
    [InlineData(50, 35, "50 giờ 35 phút")]
    public void WaitingDurationKeepsTotalHoursAndMinutes(int hours, int minutes, string expected)
    {
        var created = new DateTime(2026, 10, 14, 5, 0, 0, DateTimeKind.Utc);
        var warning = BaoHongCanhBaoService.DanhGia(new BaoHong
        { MucDo = "GAP", TrangThai = "MOI", NgayTao = created }, created.AddHours(hours).AddMinutes(minutes));
        Assert.NotNull(warning);
        Assert.Equal(expected, warning.ThoiGianChoHienThi);
    }

    [Fact]
    public void FutureAndRecentReportsDoNotWarn()
    {
        var now = new DateTime(2026, 10, 14, 5, 0, 0, DateTimeKind.Utc);
        foreach (var created in new[] { now.AddHours(1), now, now.AddHours(-23) })
            Assert.Null(BaoHongCanhBaoService.DanhGia(new BaoHong
            { MucDo = "GAP", TrangThai = "MOI", NgayTao = created }, now));
    }
}

public sealed partial class BaoHongTests
{
    [Fact]
    public async Task OverdueListAndDetailRefreshFromClockWithoutWritingAndRespectAssignment()
    {
        using var f = new Fixture(); Managers(f); int urgent, ordinary, foreign;
        using (var db = f.Context())
        {
            var sender = new BaoHongService(db, f.Time);
            urgent = (await sender.GuiAsync(2, Input(level: "GAP"))).Id;
            ordinary = (await sender.GuiAsync(2, Input())).Id;
            foreign = (await sender.GuiAsync(3, Input(3, "GAP"))).Id;
        }
        using var check = f.Context(); var service = Management(check, f);
        Assert.All(await service.DanhSachAsync(5, 1), x => Assert.Null(x.CanhBaoQuaHan));
        f.Time.UtcNow = f.Time.UtcNow.AddHours(24);
        Assert.Null((await service.ChiTietAsync(5, urgent))!.CanhBaoQuaHan);
        Assert.All(await service.DanhSachAsync(5, 1), x => Assert.Null(x.CanhBaoQuaHan));
        f.Time.UtcNow = f.Time.UtcNow.AddTicks(1);
        Assert.NotNull((await service.ChiTietAsync(5, urgent))!.CanhBaoQuaHan);
        f.Time.UtcNow = f.Time.UtcNow.AddHours(2).AddTicks(-1);
        var audits = f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong");
        var history = f.Scalar("SELECT count(*) FROM bao_hong_trao_doi");
        var list = await service.DanhSachAsync(5, 1);
        var detail = (await service.ChiTietAsync(5, urgent))!;
        Assert.Equal("26 giờ 0 phút", detail.CanhBaoQuaHan!.ThoiGianChoHienThi);
        Assert.Equal(detail.CanhBaoQuaHan, list.Single(x => x.BaoHong.Id == urgent).CanhBaoQuaHan);
        Assert.Null(list.Single(x => x.BaoHong.Id == ordinary).CanhBaoQuaHan);
        Assert.DoesNotContain(list, x => x.BaoHong.Id == foreign);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(5, foreign));
        f.Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        Assert.NotNull((await service.ChiTietAsync(5, urgent))!.CanhBaoQuaHan);
        Assert.Equal(audits, f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong"));
        Assert.Equal(history, f.Scalar("SELECT count(*) FROM bao_hong_trao_doi"));
        Assert.Equal(0, await check.BaoHongs.Where(x => x.Id == urgent).Select(x => x.PhienBan).SingleAsync());
        f.Execute("UPDATE toa_nha SET quan_ly_id=6 WHERE id=1");
        Assert.Empty(await service.DanhSachAsync(5, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChiTietAsync(5, urgent));
    }

    [Theory]
    [InlineData("DANG_XU_LY")]
    [InlineData("HOAN_THANH")]
    [InlineData("TU_CHOI")]
    public async Task OverdueWarningEndsAfterRealTransitionButReplyDoesNotResetWaiting(string target)
    {
        using var f = new Fixture(); Managers(f); int id;
        using (var db = f.Context()) id = (await new BaoHongService(db, f.Time).GuiAsync(2, Input(level: "GAP"))).Id;
        f.Time.UtcNow = f.Time.UtcNow.AddHours(26);
        using (var db = f.Context())
        {
            var service = Management(db, f);
            await service.PhanHoiAsync(5, id, new() { NoiDung = "Đã nhận thông tin, chưa bắt đầu xử lý" });
            Assert.Equal("26 giờ 0 phút", (await service.ChiTietAsync(5, id))!.CanhBaoQuaHan!.ThoiGianChoHienThi);
            await service.CapNhatAsync(5, id, Update(target == "HOAN_THANH" ? "DANG_XU_LY" : target));
            if (target == "HOAN_THANH") await service.CapNhatAsync(5, id, Update(target, 1));
        }
        f.Time.UtcNow = f.Time.UtcNow.AddHours(50);
        using var fresh = f.Context(); var reader = Management(fresh, f);
        Assert.Null((await reader.ChiTietAsync(5, id))!.CanhBaoQuaHan);
        Assert.Null(Assert.Single(await reader.DanhSachAsync(5, 1)).CanhBaoQuaHan);
        Assert.Equal(target, await fresh.BaoHongs.Where(x => x.Id == id).Select(x => x.TrangThai).SingleAsync());
    }
}
