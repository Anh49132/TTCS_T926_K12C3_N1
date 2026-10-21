using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private async Task MonthlyFiftyFixture(AppDbContext db)
    {
        var f = await MonthlyFixture(db);
        for (var id = 100; id < 148; id++)
        {
            var code = $"B{id}";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES({id},1,{code},2,25,1000000,4,'DANG_THUE','2026-01-01')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,ngay_chot_hang_thang,nguoi_lap_id,ngay_tao) VALUES({id},{"HD-BULK-"+id},{id},1,'DANG_HIEU_LUC',31,1,'2026-01-01')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES({id},1,'2026-01-01','2028-12-31',36,1000000,1,'2026-01-01')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id) SELECT {id},id FROM dich_vu_toa_nha WHERE toa_nha_id=1 AND dich_vu_id IN ({f.Dien},{f.Nuoc})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES({id},{f.Dien},'2026-10-01','2026-10-31','100','150',1,'2026-10-08'),({id},{f.Nuoc},'2026-10-01','2026-10-31','20','25',1,'2026-10-08')");
        }
    }

    [Fact]
    public async Task MonthlyBulkRetryClassifiesExistingAndNewlyCompletedRoomWithoutDuplicates()
    {
        using var db=Context(); var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B} AND dich_vu_id={f.Nuoc}");
        var service=new HoaDonDichVuService(db,new(db));
        var first=await service.PhatHanhDanhSachAsync(1,1,2026,10);
        Assert.Equal(1,first.SoDaPhatHanh);Assert.Equal(1,first.SoThieuChiSo);Assert.Equal(0,first.SoDaCoHoaDon);
        var invoice=await db.HoaDons.AsNoTracking().SingleAsync();
        var retry=await service.PhatHanhDanhSachAsync(1,1,2026,10);
        Assert.Equal(0,retry.SoDaPhatHanh);Assert.Equal(1,retry.SoDaCoHoaDon);Assert.Equal(1,retry.SoThieuChiSo);
        Assert.Equal(invoice.Id,retry.Phongs.Single(x=>x.TrangThai=="DA_CO_HOA_DON").HoaDonId);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES({f.B},{f.Nuoc},'2026-10-01','2026-10-31','20','25',1,'2026-10-08')");
        var completed=await service.PhatHanhDanhSachAsync(1,1,2026,10);
        Assert.Equal(1,completed.SoDaPhatHanh);Assert.Equal(1,completed.SoDaCoHoaDon);Assert.Equal(0,completed.SoThieuChiSo);
        Assert.Equal(1280000m,completed.TongTien);Assert.Equal(2,await db.HoaDons.CountAsync());
        Assert.Equal(invoice.NgayPhatHanh,(await db.HoaDons.AsNoTracking().SingleAsync(x=>x.Id==invoice.Id)).NgayPhatHanh);
    }

    [Fact]
    public async Task MonthlyBulkMixedNewMissingExistingAndUnselectedAreCountedSeparately()
    {
        using var db=Context();await MonthlyFiftyFixture(db);var service=new HoaDonDichVuService(db,new(db));
        await service.PhatHanhDanhSachAsync(1,1,2026,10,[100]);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id=101");
        var result=await service.PhatHanhDanhSachAsync(1,1,2026,10,[102]);
        Assert.Equal(1,result.SoDaPhatHanh);Assert.Equal(1,result.SoDaCoHoaDon);Assert.Equal(1,result.SoThieuChiSo);Assert.Equal(47,result.SoBoQuaKhac);
        Assert.Equal(50,result.Phongs.Count);Assert.Equal(2,await db.HoaDons.CountAsync());
        Assert.False(await db.HoaDons.AnyAsync(x=>x.HopDongId==101));
    }

    [Fact]
    public async Task MonthlyBulkConcurrentFiftyRoomsHaveExactlyFiftyInvoicesUnderThirtySeconds()
    {
        using(var seed=Context()) await MonthlyFiftyFixture(seed);
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<QL_PhongTro.ViewModels.KetQuaPhatHanhThang> Run() => Task.Run(async()=>
        {
            using var db=Context();await gate.Task;
            return await new HoaDonDichVuService(db,new(db)).PhatHanhDanhSachAsync(1,1,2026,10);
        });
        var a=Run();var b=Run();var timer=Stopwatch.StartNew();gate.SetResult();
        var runs=await Task.WhenAll(a,b);timer.Stop();
        Assert.Equal(50,runs.Sum(x=>x.SoDaPhatHanh));Assert.Equal(50,runs.Sum(x=>x.SoDaCoHoaDon));
        Assert.True(timer.Elapsed.TotalSeconds<30,$"Concurrent 50-room issuance took {timer.Elapsed.TotalSeconds:F3}s");
        using var verify=Context();Assert.Equal(50,await verify.HoaDons.CountAsync());Assert.Equal(150,await verify.ChiTietHoaDons.CountAsync());
        Assert.Equal(100,await verify.ChiSoDienNuocs.CountAsync(x=>x.DaKhoa));
        Assert.False(await verify.HoaDons.GroupBy(x=>new{x.HopDongId,x.Nam,x.Thang}).AnyAsync(x=>x.Count()>1));
    }

    [Fact]
    public async Task MonthlyBulkHttpFiftyConfirmationThroughResultUnderThirtySecondsAndSafeRetry()
    {
        using(var seed=Context()) await MonthlyFiftyFixture(seed);
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var html=await client.GetStringAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10");
        var fields=new List<KeyValuePair<string,string>> { new("toaNhaId","1"),new("nam","2026"),new("thang","10"),new("xacNhan","true"),new("reviewToken",FormValue(html,"reviewToken")),new("__RequestVerificationToken",FormValue(html,"__RequestVerificationToken")) };
        using(var db=Context()) fields.AddRange((await db.HopDongs.Select(x=>x.Id).ToListAsync()).Select(id=>new KeyValuePair<string,string>("selectedIds",id.ToString())));
        var timer=Stopwatch.StartNew();var response=await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("Results",response.Headers.Location!.ToString());
        var result=WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));timer.Stop();
        Assert.True(timer.Elapsed.TotalSeconds<30,$"HTTP confirmation-to-result took {timer.Elapsed.TotalSeconds:F3}s");
        Assert.Contains("Đã tạo nháp 50 hóa đơn",result);Assert.Contains("Thông tin lần chạy",result);Assert.DoesNotContain("mi-steps",result);
        using(var db=Context()) {
            Assert.Equal(50,await db.HoaDons.CountAsync(x=>x.TrangThai=="NHAP"));
            Assert.False(await db.ChiSoDienNuocs.AnyAsync(x=>x.DaKhoa));
            Assert.Empty(await db.ThongBaoHoaDons.ToListAsync());
        }
        var runQuery=new Uri(new Uri("http://localhost"),response.Headers.Location).Query;
        var csv=await client.GetAsync("/HoaDonDichVu/ExportResults"+runQuery);Assert.Equal(HttpStatusCode.OK,csv.StatusCode);
        Assert.Equal(51,(await csv.Content.ReadAsStringAsync()).Split("\r\n").Length);
        // The exact original POST is safe to repeat, even before loading a fresh review.
        var duplicate=await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(fields));
        var again=WebUtility.HtmlDecode(await client.GetStringAsync(duplicate.Headers.Location));Assert.Contains("Đã kiểm tra lại kỳ hóa đơn",again);
        using(var db=Context()) Assert.Equal(50,await db.HoaDons.CountAsync());
        // A fresh review with zero candidates may still run and show a complete result.
        html=await client.GetStringAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10");
        var emptyFields=new Dictionary<string,string>{{"toaNhaId","1"},{"nam","2026"},{"thang","10"},{"xacNhan","true"},{"reviewToken",FormValue(html,"reviewToken")},{"__RequestVerificationToken",FormValue(html,"__RequestVerificationToken")}};
        var emptyRetry=await client.PostAsync("/HoaDonDichVu/IssueMonthly",new FormUrlEncodedContent(emptyFields));Assert.Contains("Results",emptyRetry.Headers.Location!.ToString());
        // Cached reports/downloads recheck ownership at each request.
        using(var db=Context()) await db.Database.ExecuteSqlRawAsync("UPDATE toa_nha SET chu_nha_id=2 WHERE id=1");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(response.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/HoaDonDichVu/ExportResults"+runQuery)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/HoaDonDichVu/Results?runId=bad")).StatusCode);
    }
}
