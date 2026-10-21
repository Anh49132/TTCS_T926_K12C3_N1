using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class ContractActivationTests : IDisposable
{
    static ContractActivationTests()
    {
        // Enable error simulation for rollback tests; nothing else in the solution
        // reads this variable, so the process-wide setting is safe.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    readonly string path = Path.Combine(Path.GetTempPath(), "s304-" + Guid.NewGuid().ToString("N") + ".sqlite");
    readonly string seed;
    public ContractActivationTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !Directory.Exists(Path.Combine(root.FullName,"QL_PhongTro","Data"))) root=root.Parent;
        seed=Path.Combine(root!.FullName,"QL_PhongTro","Data","permissions.seed.json");
        LocalDatabaseInitializer.Create(path,seed);
        RentalRequestSchema.Initialize(path);
        using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="""
          INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
          VALUES(1,'Owner','owner@s304.test','0900000001','test','CHU_NHA',1,1,'2026-01-01','2026-01-01'),(2,'Other','other@s304.test','0900000002','test','CHU_NHA',1,1,'2026-01-01','2026-01-01');
          INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,ngay_tao) VALUES(1,'Tenant','0901234567','2026-01-01');
          INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Green House','Test'),(2,2,'Other House','Test');
          INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
          VALUES(1,1,'A203',2,25,3500000,3500000,2,'TRONG','2026-01-01'),(2,1,'A204',2,25,3500000,3500000,2,'TRONG','2026-01-01'),(3,1,'B101',1,25,3000000,3000000,2,'DANG_THUE','2026-01-01'),(4,1,'A205',2,25,3500000,3500000,2,'TRONG','2026-01-01');
          INSERT INTO tin_dang(id,phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao)
          VALUES(1,1,1,'A203 a','DANG_HIEN_THI','2026-01-01'),(2,1,1,'A203 b','NHAP','2026-01-01'),(3,1,1,'A203 c','TAM_AN','2026-01-01'),(4,3,1,'B101','DANG_HIEN_THI','2026-01-01'),(5,4,1,'A205','DANG_HIEN_THI','2026-01-01');
          INSERT INTO yeu_cau_thue(id,ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao)
          VALUES(1,'YC-approved',5,1,'THUE_NGAY','2026-11-05',1,'DA_DUYET','2026-01-01');
          INSERT INTO hop_dong(id,ma_hop_dong,phong_id,trang_thai,khach_dung_ten_id,tien_coc_thoa_thuan,ngay_chot_hang_thang,nguoi_lap_id,ngay_tao)
          VALUES(1,'HD-2026-0001',1,'NHAP',1,0,1,1,'2026-01-01T00:00:00'),(2,'HD-2026-0002',2,'NHAP',1,0,1,1,'2026-01-01T00:00:00'),(3,'HD-2026-0003',3,'DANG_HIEU_LUC',1,0,1,1,'2026-01-01T00:00:00'),(4,'HD-2026-0004',3,'NHAP',1,0,1,1,'2026-01-01T00:00:00');
          INSERT INTO ky_hop_dong(id,hop_dong_id,so_thu_tu,so_thang,ngay_bat_dau,ngay_ket_thuc,gia_thue,nguoi_lap_id,ngay_tao)
          VALUES(1,1,1,12,'2026-11-01','2027-10-31',3500000,1,'2026-01-01T00:00:00'),(2,2,1,12,'2026-11-01','2027-10-31',3500000,1,'2026-01-01T00:00:00'),(3,3,1,12,'2026-11-01','2027-10-31',3000000,1,'2026-01-01T00:00:00'),(4,4,1,12,'2026-12-01','2027-11-30',3000000,1,'2026-01-01T00:00:00');
          """;
        cmd.ExecuteNonQuery();
    }
    SqliteConnection Open(){ var c=new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False");c.Open();return c; }
    AppDbContext Context(int actor=1)=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False").Options,
      new HttpContextAccessor{HttpContext=new DefaultHttpContext{User=Principal(actor)}});
    static ClaimsPrincipal Principal(int id)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim(ClaimTypes.Role,"CHU_NHA")],"test"));
    sealed class FormClock : ITimeProvider { public DateTime UtcNow => new(2026,10,7,0,0,0,DateTimeKind.Utc); }
    HopDongController Controller(AppDbContext db,int actor=1){var http=new DefaultHttpContext{User=Principal(actor)};return new(db,new FormClock()){ControllerContext=new(){HttpContext=http},TempData=new TempDataDictionary(http,new MemoryTempData())};}
    sealed class MemoryTempData:ITempDataProvider{public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> values){} }
    static HopDongCreateViewModel Valid()=>new(){YeuCauId=1,GiaThue=3500000,TienCoc=0,SoThangCoc=0,NgayBatDau=new(2026,11,5),SoThang=12,NgayChot=5,ChiSoDien=1240,ChiSoNuoc=356,PhienBanPhong=0};

    [Fact] public async Task ActivationSyncsContractRoomAndAllListings()
    {
        using var db=Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db).KichHoat(1, null, default));
        using var check=Context();
        Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal(3, await check.TinDangs.CountAsync(t=>t.PhongId==1 && t.TrangThai=="DA_CHO_THUE"));
        Assert.All(await check.TinDangs.Where(t=>t.PhongId==1).ToListAsync(), t=>Assert.Equal("DA_CHO_THUE",t.TrangThai));
    }

    [Fact] public async Task ActivationWithoutListingOnlyChangesRoom()
    {
        using var db=Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db).KichHoat(2, null, default));
        using var check=Context();
        Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(h=>h.Id==2)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==2)).TrangThai);
        Assert.Empty(await check.TinDangs.Where(t=>t.PhongId==2).ToListAsync());
    }

    [Fact] public async Task ReactivatingEffectiveContractIsBlocked()
    {
        using var db=Context();
        await Controller(db).KichHoat(1, null, default);
        using var db2=Context();
        var controller=Controller(db2);
        var result=Assert.IsType<ViewResult>(await controller.KichHoat(1, null, default));
        Assert.False(controller.ModelState.IsValid);
        using var check=Context();
        Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal(3, await check.TinDangs.CountAsync(t=>t.PhongId==1 && t.TrangThai=="DA_CHO_THUE"));
    }

    [Fact] public async Task ActivationBlockedWhenRoomHasOtherEffectiveContract()
    {
        using var db=Context();
        var controller=Controller(db);
        var result=Assert.IsType<ViewResult>(await controller.KichHoat(4, null, default));
        Assert.False(controller.ModelState.IsValid);
        using var check=Context();
        Assert.Equal("NHAP",(await check.HopDongs.SingleAsync(h=>h.Id==4)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==3)).TrangThai);
        Assert.Equal("DANG_HIEN_THI",(await check.TinDangs.SingleAsync(t=>t.Id==4)).TrangThai);
    }

    [Fact] public async Task NonOwnerCannotActivate()
    {
        using var db=Context(2);
        Assert.IsType<ForbidResult>(await Controller(db,2).KichHoat(1, null, default));
        using var check=Context();
        Assert.Equal("NHAP",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("TRONG",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal("DANG_HIEN_THI",(await check.TinDangs.SingleAsync(t=>t.Id==1)).TrangThai);
    }

    [Fact] public async Task CreatePromotesContractAndSyncsListings()
    {
        using var db=Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db).Create(Valid(), default));
        using var check=Context();
        var contract=await check.HopDongs.SingleAsync(h=>h.YeuCauThueId==1);
        Assert.Equal("DANG_HIEU_LUC",contract.TrangThai);
        Assert.Equal(4,contract.PhongId);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==4)).TrangThai);
        Assert.Equal("DA_CHO_THUE",(await check.TinDangs.SingleAsync(t=>t.Id==5)).TrangThai);
    }

    [Fact] public async Task RollbackWhenContractStepFails()
    {
        using var db=Context();
        var controller=Controller(db);
        var result=Assert.IsType<ViewResult>(await controller.KichHoat(1, "contract", default));
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[""]?.Errors ?? [], e=>e.ErrorMessage.Contains("giả lập"));
        using var check=Context();
        Assert.Equal("NHAP",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("TRONG",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal("DANG_HIEN_THI",(await check.TinDangs.SingleAsync(t=>t.Id==1)).TrangThai);
        Assert.Equal("NHAP",(await check.TinDangs.SingleAsync(t=>t.Id==2)).TrangThai);
    }

    [Fact] public async Task RollbackWhenRoomStepFails()
    {
        using var db=Context();
        var controller=Controller(db);
        var result=Assert.IsType<ViewResult>(await controller.KichHoat(1, "room", default));
        Assert.False(controller.ModelState.IsValid);
        using var check=Context();
        Assert.Equal("NHAP",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("TRONG",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal(0,(await check.PhongTros.SingleAsync(p=>p.Id==1)).PhienBan);
        Assert.Equal("DANG_HIEN_THI",(await check.TinDangs.SingleAsync(t=>t.Id==1)).TrangThai);
    }

    [Fact] public async Task RollbackWhenListingStepFails()
    {
        using var db=Context();
        var controller=Controller(db);
        var result=Assert.IsType<ViewResult>(await controller.KichHoat(1, "listing", default));
        Assert.False(controller.ModelState.IsValid);
        using var check=Context();
        Assert.Equal("NHAP",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("TRONG",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal("DANG_HIEN_THI",(await check.TinDangs.SingleAsync(t=>t.Id==1)).TrangThai);
        Assert.Equal("NHAP",(await check.TinDangs.SingleAsync(t=>t.Id==2)).TrangThai);
        Assert.Equal("TAM_AN",(await check.TinDangs.SingleAsync(t=>t.Id==3)).TrangThai);
    }

    [Fact] public async Task RetryAfterSimulatedFailureSucceeds()
    {
        using var db=Context();
        var controller=Controller(db);
        Assert.IsType<ViewResult>(await controller.KichHoat(1, "listing", default));
        using var db2=Context();
        Assert.IsType<RedirectToActionResult>(await Controller(db2).KichHoat(1, null, default));
        using var check=Context();
        Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal(3, await check.TinDangs.CountAsync(t=>t.PhongId==1 && t.TrangThai=="DA_CHO_THUE"));
    }

    [Fact]
    public async Task ConcurrentActivationOnlyOneSucceeds()
    {
        using var db1=Context();
        using var db2=Context();
        var c1=Controller(db1);
        var c2=Controller(db2);
        var results=await Task.WhenAll(Task.Run(() => c1.KichHoat(1, null, default)),
            Task.Run(() => c2.KichHoat(1, null, default)));
        // Exactly one activation commits; the loser sees the contract already effective.
        Assert.Equal(1, results.Count(r=>r is RedirectToActionResult));
        Assert.Equal(1, results.Count(r=>r is ViewResult));
        using var check=Context();
        Assert.Equal("DANG_HIEU_LUC",(await check.HopDongs.SingleAsync(h=>h.Id==1)).TrangThai);
        Assert.Equal("DANG_THUE",(await check.PhongTros.SingleAsync(p=>p.Id==1)).TrangThai);
        Assert.Equal(3, await check.TinDangs.CountAsync(t=>t.PhongId==1 && t.TrangThai=="DA_CHO_THUE"));
    }

    public void Dispose(){SqliteConnection.ClearAllPools();File.Delete(path);foreach(var backup in Directory.EnumerateFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".before-*.bak"))File.Delete(backup);}
}
