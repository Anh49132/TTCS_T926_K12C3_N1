using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    private void UsageFixture(int count = 3, int consumption = 10, bool waterMeter = true)
    {
        MeterFixture(waterMeter: waterMeter);
        Execute("UPDATE ky_hop_dong SET ngay_bat_dau='2026-01-01' WHERE hop_dong_id=1;UPDATE cau_hinh_dich_vu SET tu_ngay='2026-01-01'");
        var months = new[] { 5, 7, 9 };
        for (var index = 0; index < count; index++)
            foreach (var service in waterMeter ? new[] { 1, 2 } : new[] { 1 })
                Execute($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,{service},'2026-{months[index]:00}-01','{new DateOnly(2026, months[index], DateTime.DaysInMonth(2026, months[index])):yyyy-MM-dd}','{index * consumption}','{(index + 1) * consumption}',2,'2026-09-30')");
    }
    private async Task<LuuChiSoInput> UsageInput(decimal electric, decimal water)
    {
        var row = (await List()).Phongs.Single(x => x.PhongId == 1);
        return SaveInput(row.Dien.GiaTri!.Value + electric, row.Nuoc.GiaTri!.Value + water);
    }
    private async Task<bool[]> ConfirmationFlags()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
        return await db.ChiSoDienNuocs.Where(x => x.HopDongId == 1 && x.TuNgay == new DateOnly(2026,10,1)).OrderBy(x => x.DichVuId).Select(x => x.DaXacNhanBatThuong).ToArrayAsync();
    }

    [Theory][InlineData(10,10)][InlineData(20,20)]
    public async Task UsageNormalAndExactThresholdSaveDirectly(int electric, int water)
    {
        UsageFixture(); Assert.Empty(await Save(await UsageInput(electric,water)));
        Assert.Equal(new[] { false, false }, await ConfirmationFlags()); Progress(await List(),2,1);
    }
    [Theory][InlineData(21,10,"DIEN")][InlineData(10,21,"NUOC")][InlineData(21,21,"both")]
    public async Task UsageWarnConfirmAndPersistPerService(int electric, int water, string code)
    {
        UsageFixture(); var input = await UsageInput(electric,water); var before = Count("chi_so_dien_nuoc");
        Assert.Contains("Anomaly",(await Save(input)).Keys); Assert.Equal(before,Count("chi_so_dien_nuoc")); Progress(await List(),2,0);
        Assert.Equal(code == "both" ? new[] { "DIEN", "NUOC" } : new[] { code },input.CanhBaos.Select(x => x.Ma).Order());
        Assert.All(input.CanhBaos,x => { Assert.Equal(10,x.TrungBinh); Assert.Equal(20,x.Nguong); Assert.Equal(10,x.TieuThuKyTruoc); });
        input.XacNhanBatThuong = true; Assert.Empty(await Save(input));
        Assert.Equal(new[] { electric > 20, water > 20 },await ConfirmationFlags()); Progress(await List(),2,1);
        Assert.False((await List()).Phongs.Single(x => x.PhongId == 2).DaChot);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public async Task UsageInsufficientHistoryDoesNotRequireConfirmation(int count)
    {
        UsageFixture(count); Assert.Empty(await Save(await UsageInput(100,100)));
        Assert.Equal(new[] { false, false },await ConfirmationFlags());
    }
    [Theory][InlineData(0,false)][InlineData(1,true)]
    public async Task UsageZeroAverageNeverDividesByZero(int consumption,bool warning)
    {
        UsageFixture(consumption:0);var input=await UsageInput(consumption,consumption);
        var result=await Save(input);Assert.Equal(warning,result.ContainsKey("Anomaly"));
        if(warning){Assert.All(input.CanhBaos,x=>Assert.Equal(0,x.TrungBinh));input.XacNhanBatThuong=true;Assert.Empty(await Save(input));}
        Assert.Equal(new[]{warning,warning},await ConfirmationFlags());
    }
    [Fact] public async Task UsageChangedReadingNormalClearsOldConfirmation()
    {
        UsageFixture();var input=await UsageInput(21,10);Assert.NotEmpty(await Save(input));
        input.DienMoi=40;input.XacNhanBatThuong=true;Assert.Empty(await Save(input));
        Assert.Equal(new[]{false,false},await ConfirmationFlags());
    }
    [Fact] public async Task UsageForgedChangedAndExpiredTokensRejected()
    {
        UsageFixture();var input=await UsageInput(21,10);input.XacNhanBatThuong=true;input.MaXacNhan="forged";
        Assert.NotEmpty(await Save(input)); var firstToken=input.MaXacNhan;
        input.DienMoi=52;input.XacNhanBatThuong=true;Assert.NotEmpty(await Save(input));Assert.NotEqual(firstToken,input.MaXacNhan);
        clock.UtcNow=clock.UtcNow.AddMinutes(16);input.XacNhanBatThuong=true;Assert.NotEmpty(await Save(input));
        Assert.Empty(await ConfirmationFlags());Progress(await List(),2,0);
    }
    [Fact] public async Task UsageHistoryVersionAndRoomVersionInvalidateConfirmation()
    {
        UsageFixture();var input=await UsageInput(21,10);Assert.NotEmpty(await Save(input));
        Execute("UPDATE chi_so_dien_nuoc SET phien_ban=phien_ban+1 WHERE hop_dong_id=1 AND dich_vu_id=1 AND tu_ngay='2026-05-01'");
        input.XacNhanBatThuong=true;Assert.NotEmpty(await Save(input));
        Execute("UPDATE phong_tro SET phien_ban=1 WHERE id=1");input.XacNhanBatThuong=true;
        Assert.Contains("Phong",(await Save(input)).Keys);Assert.Empty(await ConfirmationFlags());
    }
    [Fact] public async Task UsageConfirmationCannotBypassInvalidLockedOrStale()
    {
        UsageFixture();var input=await UsageInput(21,21);Assert.NotEmpty(await Save(input));
        input.XacNhanBatThuong=true;input.DienMoi=29;Assert.Contains("DienMoi",(await Save(input)).Keys);Assert.Empty(await ConfirmationFlags());
        input=await UsageInput(21,21);Assert.NotEmpty(await Save(input));input.XacNhanBatThuong=true;
        Assert.Empty(await Save(await UsageInput(10,10)));Assert.Contains("DienMoi",(await Save(input)).Keys);
        Execute("UPDATE chi_so_dien_nuoc SET da_khoa=1 WHERE tu_ngay='2026-10-01'");
        input.DienPhienBan=input.NuocPhienBan=0;Assert.Contains("DienMoi",(await Save(input)).Keys);
    }
    [Fact] public async Task UsageConfirmedSaveAuditFailureRollsBackBothMeters()
    {
        UsageFixture();var input=await UsageInput(21,21);Assert.NotEmpty(await Save(input));input.XacNhanBatThuong=true;
        Execute("CREATE TRIGGER fail_usage_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>Save(input));Assert.Empty(await ConfirmationFlags());Progress(await List(),2,0);
    }
    [Fact] public async Task UsageWaterPerPersonDoesNotParticipate()
    {
        UsageFixture(waterMeter:false);var input=SaveInput(51,null);Assert.NotEmpty(await Save(input));
        Assert.Equal("DIEN",Assert.Single(input.CanhBaos).Ma);input.XacNhanBatThuong=true;Assert.Empty(await Save(input));
        Assert.Single(await ConfirmationFlags());Progress(await List(),2,1);
    }
    private void LegacyUsage(int id,int month,decimal? start,decimal? end,string status="DA_PHAT_HANH",int contract=1)
    {
        Invoice(id,contract,month,status:"NHAP",electricity:end,water:end);
        if(start.HasValue)Execute($"UPDATE chi_tiet_hoa_don SET chi_so_dau='{start.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}' WHERE hoa_don_id={id}");
        Execute($"UPDATE hoa_don SET trang_thai='{status}' WHERE id={id}");
    }
    [Fact] public async Task UsageLegacyFallbackDeduplicatesAndPrefersMeter()
    {
        UsageFixture(count:1);LegacyUsage(1,5,0,100);LegacyUsage(2,7,10,20);LegacyUsage(3,9,20,30);
        var input=await UsageInput(21,10);Assert.NotEmpty(await Save(input));Assert.Equal(10,Assert.Single(input.CanhBaos).TrungBinh);
    }
    [Fact] public async Task UsageInvalidDraftForeignHistoryAndHandoverNotCounted()
    {
        UsageFixture(count:0);LegacyUsage(1,5,null,10);LegacyUsage(2,7,20,10);LegacyUsage(3,9,0,10,"NHAP");
        LegacyUsage(4,8,0,10,contract:6);
        Assert.Empty(await Save(await UsageInput(100,100)));Assert.Equal(new[]{false,false},await ConfirmationFlags());
    }
    [Fact] public async Task UsageLatestThreeValidNonConsecutivePeriodsOnly()
    {
        UsageFixture();Execute("INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,1,'2026-03-01','2026-03-31','0','1000',2,'2026-03-31')");
        var input=await UsageInput(21,10);Assert.NotEmpty(await Save(input));Assert.Equal(10,Assert.Single(input.CanhBaos).TrungBinh);
    }
    [Fact] public async Task UsageHistoryDoesNotMixServicesOrContracts()
    {
        UsageFixture(count:0);
        foreach(var month in new[]{5,7,9})
        {
            var end=new DateOnly(2026,month,1).AddMonths(1).AddDays(-1);
            Execute($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,1,'2026-{month:00}-01','{end:yyyy-MM-dd}','0','10',2,'2026-09-30'),(6,2,'2026-{month:00}-01','{end:yyyy-MM-dd}','0','1',2,'2026-09-30')");
        }
        var input=await UsageInput(21,100);
        Assert.NotEmpty(await Save(input));Assert.Equal("DIEN",Assert.Single(input.CanhBaos).Ma);
        input.XacNhanBatThuong=true;Assert.Empty(await Save(input));Assert.Equal(new[]{true,false},await ConfirmationFlags());
    }
    [Fact] public async Task UsageConfirmationRechecksAssignmentAndMissingReference()
    {
        UsageFixture();var input=await UsageInput(21,21);Assert.NotEmpty(await Save(input));input.XacNhanBatThuong=true;
        Execute("UPDATE toa_nha SET quan_ly_id=3 WHERE id=1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Save(input));Assert.Empty(await ConfirmationFlags());
        Execute("UPDATE toa_nha SET quan_ly_id=2 WHERE id=1;DELETE FROM chi_so_dien_nuoc;DELETE FROM hop_dong_chi_so_dau_ky");
        Assert.Contains("DienDau",(await Save(input)).Keys);Assert.Empty(await ConfirmationFlags());
    }
    [Fact] public async Task UsageHttpForgedWarningCorrectionConfirmationReload()
    {
        UsageFixture();using var factory=Factory();using var manager=await Login(factory,"manager@meter.test");
        var fields=await MeterForm(manager);fields["DienMoi"]="51";fields["NuocMoi"]="40";fields["XacNhanBatThuong"]="true";fields["MaXacNhan"]="forged";fields["average"]="999999";
        async Task<string> Post()=>WebUtility.HtmlDecode(await (await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields))).Content.ReadAsStringAsync());
        var html=await Post();Assert.Contains("Mức tiêu thụ bất thường — phòng B202",html);Assert.Contains("Đã chốt: 0/2",html);Assert.Empty(await ConfirmationFlags());
        Assert.Contains("<strong>Điện:</strong>",html);Assert.DoesNotContain("<strong>Nước:</strong>",html);
        var token=Regex.Match(html,"name=\"MaXacNhan\" value=\"([^\"]+)\"").Groups[1].Value;Assert.NotEmpty(token);
        fields["MaXacNhan"]=token;fields["DienMoi"]="52";html=await Post();Assert.Contains("data-usage-warning",html);Assert.Empty(await ConfirmationFlags());
        fields["MaXacNhan"]=Regex.Match(html,"name=\"MaXacNhan\" value=\"([^\"]+)\"").Groups[1].Value;
        var response=await manager.PostAsync("/ChiSoDienNuoc/Save",new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        html=WebUtility.HtmlDecode(await manager.GetStringAsync(response.Headers.Location));Assert.Contains("Đã chốt: 1/2",html);Assert.Equal(new[]{true,false},await ConfirmationFlags());
        Assert.Contains("Đã chốt: 1/2",WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1")));
    }
}
