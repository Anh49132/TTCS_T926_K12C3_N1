using System.Security.Claims;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

public sealed partial class ContractCreationTests : IDisposable
{
    readonly string path = Path.Combine(Path.GetTempPath(), "s301-" + Guid.NewGuid().ToString("N") + ".sqlite");
    readonly string seed;
    public ContractCreationTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !Directory.Exists(Path.Combine(root.FullName,"QL_PhongTro","Data"))) root=root.Parent;
        seed=Path.Combine(root!.FullName,"QL_PhongTro","Data","permissions.seed.json");
        LocalDatabaseInitializer.Create(path,seed);
        RentalRequestSchema.Initialize(path);
        using var c=Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="""
          INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
          VALUES(1,'Owner','owner@s301.test','0900000001','test','CHU_NHA',1,1,'2026-01-01','2026-01-01'),(2,'Other','other@s301.test','0900000002','test','CHU_NHA',1,1,'2026-01-01','2026-01-01');
          INSERT INTO khach_thue(id,ho_ten,so_dien_thoai,ngay_tao) VALUES(1,'Tenant','0901234567','2026-01-01');
          INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Green House','Test');
          INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(1,1,'A203',2,25,3500000,3500000,2,'TRONG','2026-01-01');
          INSERT INTO tin_dang(id,phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES(1,1,1,'Room','DANG_HIEN_THI','2026-01-01');
          INSERT INTO yeu_cau_thue(id,ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao)
          VALUES(1,'YC-approved',1,1,'THUE_NGAY','2026-11-05',1,'DA_DUYET','2026-01-01'),(2,'YC-new',1,1,'THUE_NGAY','2026-11-05',1,'MOI','2026-01-01');
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

    [Fact] public async Task ApprovedRequestPrefillsCorrectTenantRoom(){using var db=Context();var result=Assert.IsType<ViewResult>(await Controller(db).Create(1,default));var vm=Assert.IsType<HopDongCreateViewModel>(result.Model);Assert.Equal("Tenant",vm.DaChon!.Khach);Assert.Equal("A203",vm.DaChon.Phong);Assert.Single(vm.YeuCaus);Assert.Equal(3500000,vm.GiaThue);}
    [Theory][InlineData(1,2)][InlineData(2,1)] public async Task UnapprovedOrOtherOwnerCannotSelect(int actor,int request){using var db=Context(actor);var result=await Controller(db,actor).Create(request,default);if(actor==2)Assert.IsType<ForbidResult>(result);else Assert.IsType<NotFoundResult>(result);}
    [Fact] public async Task SavesContractPeriodMetersAndRentedRoom(){using var db=Context();Assert.IsType<RedirectToActionResult>(await Controller(db).Create(Valid(),default));var h=await db.HopDongs.SingleAsync();var k=await db.KyHopDongs.SingleAsync();Assert.Equal(1,h.KhachDungTenId);Assert.Equal(1,h.PhongId);Assert.Equal("DANG_HIEU_LUC",h.TrangThai);Assert.Matches(@"^HD-\d{4}-\d{4}$",h.MaHopDong);Assert.Equal(new DateOnly(2027,11,4),k.NgayKetThuc);Assert.Equal("DANG_THUE",(await db.PhongTros.SingleAsync()).TrangThai);var meter=await db.HopDongChiSoDauKys.SingleAsync();Assert.Equal(1240,meter.ChiSoDien);Assert.Equal(356,meter.ChiSoNuoc);Assert.Equal(h.NgayTao,meter.NgayNhap);Assert.Equal(new DateOnly(2026,11,5),meter.NgayBanGiao);Assert.Equal(4,await db.NhatKyHoatDongs.CountAsync());}
    [Fact] public async Task MissingRequiredDoesNotSave(){using var db=Context();var vm=Valid();vm.TienCoc=null;Assert.IsType<ViewResult>(await Controller(db).Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task CannotPostUnapprovedRequest(){using var db=Context();var vm=Valid();vm.YeuCauId=2;Assert.IsType<ViewResult>(await Controller(db).Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Theory][InlineData(0,5)][InlineData(-1,5)][InlineData(12,0)][InlineData(12,32)]
    public async Task InvalidDurationOrCutoffDoesNotSave(int months,int cutoff){using var db=Context();var vm=Valid();vm.SoThang=months;vm.NgayChot=cutoff;Assert.IsType<ViewResult>(await Controller(db).Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task RepeatedSaveCannotDuplicate(){using var db=Context();await Controller(db).Create(Valid(),default);Assert.IsType<ViewResult>(await Controller(db).Create(Valid(),default));Assert.Single(await db.HopDongs.ToListAsync());}
    [Theory][InlineData(2026,1,31,1,2026,2,27)][InlineData(2024,1,31,1,2024,2,28)][InlineData(2026,11,5,12,2027,11,4)]
    public void InclusiveEndDateClampsMonthEnd(int y,int m,int d,int months,int ey,int em,int ed)=>Assert.Equal(new DateOnly(ey,em,ed),HopDongCreateViewModel.TinhNgayKetThuc(new(y,m,d),months));
    [Fact] public void EndDateCannotBeAssigned()=>Assert.Null(typeof(HopDongCreateViewModel).GetProperty(nameof(HopDongCreateViewModel.NgayKetThuc))!.SetMethod);
    [Fact] public async Task HttpFormRendersAndIgnoresForgedEndDate()
    {
        const string password="ContractTest!2026";
        using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="UPDATE tai_khoan SET mat_khau=$hash WHERE id=1";cmd.Parameters.AddWithValue("$hash",BCrypt.Net.BCrypt.HashPassword(password,4));cmd.ExecuteNonQuery();}
        using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseContentRoot(Path.GetDirectoryName(Path.GetDirectoryName(seed))!);
            builder.UseEnvironment("Development");builder.UseSetting("DatabasePath",path);
            builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.AddSingleton<ITimeProvider>(new FormClock());});
        });
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var login=await client.GetStringAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"TaiKhoanDangNhap","owner@s301.test"},{"MatKhau",password},{"__RequestVerificationToken",Token(login)}}))).StatusCode);
        var page=await client.GetStringAsync("/HopDong/Create?yeuCauId=1");
        Assert.Contains("contracts.css",page);Assert.Contains("A203",page);Assert.Contains("Tenant",page);
        var phoneInput = Regex.Match(page, "<input[^>]*id=\"customer-phone\"[^>]*>").Value;
        Assert.Contains("value=\"******4567\"", phoneInput);
        Assert.Contains("readonly", phoneInput);
        Assert.DoesNotContain("name=", phoneInput);
        Assert.Matches("id=\"end-date\"[^>]*readonly",page);
        Assert.DoesNotContain("readonly", Regex.Match(page, "<input[^>]*id=\"GiaThue\"[^>]*>").Value);
        Assert.Contains("min=\"2026-10-07\"",page);
        var values=new Dictionary<string,string>{{"YeuCauId","1"},{"GiaThue","3200000"},{"TienCoc","1500000"},{"SoThangCoc","0"},{"NgayBatDau","2026-11-05"},{"SoThang","12"},{"NgayChot","5"},{"NgayKetThuc","2099-01-01"},{"ChiSoDien","1240"},{"ChiSoNuoc","356"},{"PhienBanPhong","0"},{"__RequestVerificationToken",Token(page)}};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/HopDong/Create",new FormUrlEncodedContent(new Dictionary<string,string>{{"YeuCauId","1"}}))).StatusCode);
        var conflicts = await client.GetAsync("/HopDong/KiemTra?yeuCauId=1&ngayBatDau=2026-11-05&soThang=12");
        Assert.Equal(HttpStatusCode.OK,conflicts.StatusCode);Assert.Equal("[]",await conflicts.Content.ReadAsStringAsync());
        values["ChiSoDien"]="1240.125";values["ChiSoNuoc"]="356.5";
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/HopDong/Create",new FormUrlEncodedContent(values))).StatusCode);
        using var db=Context();Assert.Equal(new DateOnly(2027,11,4),(await db.KyHopDongs.SingleAsync()).NgayKetThuc);
        Assert.Equal(3200000,(await db.KyHopDongs.SingleAsync()).GiaThue);Assert.Equal(1500000,(await db.HopDongs.SingleAsync()).TienCoc);Assert.Equal(3500000,(await db.PhongTros.SingleAsync()).GiaThue);
        var meter=await db.HopDongChiSoDauKys.SingleAsync();Assert.Equal(1240.125m,meter.ChiSoDien);Assert.Equal(356.5m,meter.ChiSoNuoc);
        var index=WebUtility.HtmlDecode(await client.GetStringAsync("/HopDong"));Assert.Contains((await db.HopDongs.SingleAsync()).MaHopDong,index);Assert.Contains("Đang thuê",index);
    }
    [Fact] public async Task UpgradePreservesRowsAndIsIdempotent(){using(var db=Context()){await Controller(db).Create(Valid(),default);}using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="DELETE FROM app_schema_version WHERE version=14";cmd.ExecuteNonQuery();}DatabaseUpdates.Update(path,seed);DatabaseUpdates.Update(path,seed);using var check=Context();Assert.Single(await check.HopDongs.ToListAsync());Assert.Equal(3500000,(await check.KyHopDongs.SingleAsync()).GiaThue);DatabaseUpdates.Check(path);using var connection=Open();using var integrity=connection.CreateCommand();integrity.CommandText="PRAGMA integrity_check";Assert.Equal("ok",integrity.ExecuteScalar());integrity.CommandText="PRAGMA foreign_key_check";using var reader=integrity.ExecuteReader();Assert.False(reader.Read());}
    [Fact] public void UpgradeAddsMissingColumnsWithoutReplacingLegacyContract()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        using(var c=Open()){using var cmd=c.CreateCommand();cmd.CommandText="""
            PRAGMA foreign_keys=OFF;
            DROP TABLE ky_hop_dong; DROP TABLE hop_dong;
            CREATE TABLE hop_dong(id INTEGER PRIMARY KEY,ma_hop_dong TEXT NOT NULL UNIQUE,phong_id INTEGER NOT NULL,trang_thai TEXT NOT NULL,ngay_tra_phong TEXT);
            CREATE TABLE ky_hop_dong(id INTEGER PRIMARY KEY,hop_dong_id INTEGER NOT NULL,ngay_bat_dau TEXT NOT NULL,ngay_ket_thuc TEXT NOT NULL,gia_thue INTEGER NOT NULL);
            INSERT INTO hop_dong VALUES(8,'LEGACY-8',1,'DANG_HIEU_LUC',NULL);
            INSERT INTO ky_hop_dong VALUES(9,8,'2026-01-01','2026-12-31',2500000);
            DELETE FROM app_schema_version WHERE version>=14;
            """;cmd.ExecuteNonQuery();}
        DatabaseUpdates.Update(path,seed);DatabaseUpdates.Check(path);
        using var db=Context();var h=db.HopDongs.Single();Assert.Equal(8,h.Id);Assert.Equal("LEGACY-8",h.MaHopDong);Assert.Equal("DANG_HIEU_LUC",h.TrangThai);Assert.Null(h.YeuCauThueId);Assert.Equal(2500000,db.KyHopDongs.Single().GiaThue);
    }
    public void Dispose(){SqliteConnection.ClearAllPools();File.Delete(path);foreach(var backup in Directory.EnumerateFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".before-*.bak"))File.Delete(backup);}
}
