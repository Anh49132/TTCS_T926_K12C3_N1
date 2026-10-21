using System.Net;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    private static void Progress(ChiSoDienNuocViewModel model, int total, int closed)
    {
        Assert.Equal(total, model.TongPhongCanChot);
        Assert.Equal(closed, model.SoPhongDaChot);
        Assert.Equal(total - closed, model.SoPhongConLai);
    }

    [Fact] public async Task ProgressZeroPartialAllAndRepeatedSave()
    {
        MeterFixture(); Progress(await List(), 2, 0);
        Assert.Empty(await Save(SaveInput(1, 13))); Progress(await List(), 2, 1);
        Assert.False((await List()).Phongs.Single(x => x.PhongId == 2).DaChot);
        var update = SaveInput(2, 14); update.DienPhienBan = update.NuocPhienBan = 0;
        Assert.Empty(await Save(update)); Progress(await List(), 2, 1);
        var second = SaveInput(3, 15); second.PhongId = second.HopDongId = 2;
        Assert.Empty(await Save(second)); Progress(await List(), 2, 2);
    }

    [Fact] public async Task ProgressPerPersonNeedsOnlyElectricity()
    {
        MeterFixture(waterMeter: false); Progress(await List(), 2, 0);
        Assert.Empty(await Save(SaveInput(1, null))); Progress(await List(), 2, 1);
        Assert.Equal(1, Count("chi_so_dien_nuoc"));
    }

    [Fact] public async Task ProgressPartialMetersAndWrongPeriodContract()
    {
        MeterFixture();
        Execute("""
            INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap)
            VALUES(1,1,'2026-10-01','2026-10-31','0','1',2,'2026-10-07'),
                  (1,2,'2026-09-01','2026-09-30','12.345','13',2,'2026-09-30'),
                  (6,2,'2026-07-01','2026-07-31','0','9',2,'2026-07-31');
            """);
        Progress(await List(), 2, 0);
        Execute("INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,2,'2026-10-01','2026-10-31','13','14',2,'2026-10-07')");
        Progress(await List(), 2, 1);
    }

    [Fact] public async Task ProgressMissingReferenceAndInvalidSaveStayRemaining()
    {
        MeterFixture(handover: false); Progress(await List(), 2, 0);
        Assert.NotEmpty(await Save(SaveInput())); Progress(await List(), 2, 0);
        Assert.Equal(0, Count("chi_so_dien_nuoc"));
        Assert.Null((await List()).Phongs.Single(x => x.PhongId == 1).Dien.GiaTri);
    }

    [Fact] public async Task ProgressExcludesNoMetersWithoutChangingLegacyStatus()
    {
        Invoice(1, 3, 10); MeterFixture(); var model = await List();
        Assert.True(model.Phongs.Single(x => x.PhongId == 3).DaChot);
        Progress(model, 2, 0);
        Execute("UPDATE cau_hinh_dich_vu SET cach_tinh='THEO_NGUOI'");
        model = await List(); Progress(model, 0, 0);
        Assert.True(model.Phongs.Single(x => x.PhongId == 3).DaChot);
    }

    [Fact] public async Task ProgressDoesNotDuplicateRoomsOrMixBuildings()
    {
        MeterFixture();
        Execute("INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(1,'2026-10-01','2026-12-31',1000000)");
        Progress(await List(), 2, 0); Progress(await List(3, 2), 0, 0);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => List(2, 2));
    }

    [Fact] public async Task ProgressConcurrentStaleAndRollbackDoNotOvercount()
    {
        MeterFixture();
        var results = await Task.WhenAll(Task.Run(() => Save(SaveInput())), Task.Run(() => Save(SaveInput())));
        Assert.Single(results, x => x.Count == 0); Progress(await List(), 2, 1);
        Assert.NotEmpty(await Save(SaveInput())); Progress(await List(), 2, 1);
        Execute("CREATE TRIGGER fail_progress_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        var second = SaveInput(); second.PhongId = second.HopDongId = 2;
        await Assert.ThrowsAnyAsync<Exception>(() => Save(second)); Progress(await List(), 2, 1);
        Assert.False((await List()).Phongs.Single(x => x.PhongId == 2).DaChot);
    }

    [Fact] public async Task ProgressHttpInvalidRedirectReloadAndNoMeterMessage()
    {
        MeterFixture(waterMeter: false); using var factory = Factory();
        using var manager = await Login(factory, "manager@meter.test");
        async Task<string> Page() => WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1"));
        Assert.Contains("Đã chốt: 0/2 · Còn lại: 2 phòng", await Page());
        var fields = await MeterForm(manager); fields["DienMoi"] = "-1";
        var response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Đã chốt: 0/2 · Còn lại: 2 phòng", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        fields["DienMoi"] = "1";
        response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Đã chốt: 1/2 · Còn lại: 1 phòng", WebUtility.HtmlDecode(await manager.GetStringAsync(response.Headers.Location)));
        Assert.Contains("Đã chốt: 1/2 · Còn lại: 1 phòng", await Page());
        Execute("UPDATE cau_hinh_dich_vu SET cach_tinh='THEO_NGUOI'");
        var html = await Page(); Assert.Contains("Không có phòng cần ghi chỉ số trong kỳ này.", html);
        Assert.DoesNotContain("Đã chốt: 0/0", html); Progress(await List(), 0, 0);
    }
}
