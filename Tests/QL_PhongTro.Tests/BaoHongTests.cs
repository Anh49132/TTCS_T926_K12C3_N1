using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed class BaoHongTests
{
    private sealed class Clock : ITimeProvider { public DateTime UtcNow { get; set; } = new(2026,10,14,5,0,0,DateTimeKind.Utc); }
    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "s406-" + Guid.NewGuid().ToString("N") + ".sqlite");
        public Clock Time { get; } = new();
        public Fixture()
        {
            var root = Directory.GetCurrentDirectory();
            while (!File.Exists(System.IO.Path.Combine(root,"QL_PhongTro","Data","permissions.seed.json"))) root = Directory.GetParent(root)!.FullName;
            LocalDatabaseInitializer.Create(Path, System.IO.Path.Combine(root,"QL_PhongTro","Data","permissions.seed.json"));
            RentalRequestSchema.Initialize(Path);
            Execute("""
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                VALUES(1,'Owner','owner@test.local','0900000001','hash','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                (2,'Tenant','tenant@test.local','0900000002','hash','KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
                (3,'Other','other@test.local','0900000003','hash','KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
                (4,'Roommate','roommate@test.local','0900000004','hash','KHACH_THUE',1,1,'2026-01-01','2026-01-01');
                INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,2,'Tenant','2026-01-01'),(2,3,'Other','2026-01-01'),(3,4,'Roommate','2026-01-01');
                INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Test building','Test address');
                INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
                VALUES(1,1,'A101',1,25,3000000,3,'DANG_THUE','2026-01-01'),(2,1,'A102',1,25,3000000,3,'DANG_THUE','2026-01-01'),(3,1,'A103',1,25,3000000,3,'DANG_THUE','2026-01-01');
                INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(1,'HD-TEST-1',1,1,'DANG_HIEU_LUC'),(2,'HD-TEST-2',2,1,'DANG_HIEU_LUC'),(3,'HD-TEST-3',3,2,'DANG_HIEU_LUC');
                INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(1,'2026-10-01','2026-11-30',3000000),(2,'2026-10-01','2026-11-30',3000000),(3,'2026-10-01','2026-11-30',3000000);
                INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ngay_ra) VALUES(1,3,'2026-10-01','2026-10-14');
                """);
        }
        public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path};Pooling=False;Default Timeout=20").Options);
        public void Execute(string sql) { using var c=new SqliteConnection($"Data Source={Path};Foreign Keys=True;Pooling=False"); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText=sql; cmd.ExecuteNonQuery(); }
        public long Scalar(string sql) { using var c=new SqliteConnection($"Data Source={Path};Pooling=False"); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText=sql; return Convert.ToInt64(cmd.ExecuteScalar()); }
        public void Dispose() { SqliteConnection.ClearAllPools(); foreach(var file in Directory.GetFiles(System.IO.Path.GetDirectoryName(Path)!,System.IO.Path.GetFileName(Path)+"*")) File.Delete(file); }
    }
    private static BaoHongInput Input(int contract = 1, string level = "THUONG") => new() { HopDongId=contract,LoaiThietBi=" Vòi nước ",MoTa=" Nước rò dưới bồn rửa ",MucDo=level };

    [Fact]
    public async Task OrdinaryUrgentMultipleRoomsAndOwnHistory()
    {
        using var f=new Fixture(); using var db=f.Context(); var s=new BaoHongService(db,f.Time);
        Assert.Equal(2,(await s.PhongsAsync(2)).Count);
        var a=await s.GuiAsync(2,Input()); var b=await s.GuiAsync(2,Input(2,"GAP"));
        Assert.Equal("BH-202610-0001",a.MaBaoHong); Assert.Equal("BH-202610-0002",b.MaBaoHong);
        Assert.Equal("MOI",a.TrangThai); Assert.Equal("Vòi nước",a.LoaiThietBi); Assert.Equal(2,b.PhongId);
        Assert.Equal(2,(await s.DanhSachAsync(2,1)).Count); Assert.Empty(await s.DanhSachAsync(3,1));
        Assert.Equal(a.Id,(await s.ChiTietAsync(2,a.Id))!.BaoHong.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.ChiTietAsync(3,a.Id));
        Assert.Null(await s.ChiTietAsync(2,999));
        Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='bao_hong'"));
    }
    [Theory]
    [InlineData(null,"Valid","THUONG","LoaiThietBi")]
    [InlineData("   ","Valid","THUONG","LoaiThietBi")]
    [InlineData("Valid",null,"THUONG","MoTa")]
    [InlineData("Valid","  ","THUONG","MoTa")]
    [InlineData("Valid","Valid",null,"MucDo")]
    [InlineData("Valid","Valid","INVALID","MucDo")]
    public async Task RequiredFieldsRejectWithoutAllocatingCode(string? device,string? description,string? level,string field)
    {
        using var f=new Fixture(); using var db=f.Context(); var input=Input(); input.LoaiThietBi=device;input.MoTa=description;input.MucDo=level;
        Assert.Equal(field,(await Assert.ThrowsAsync<BaoHongValidationException>(()=>new BaoHongService(db,f.Time).GuiAsync(2,input))).Field);
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong_so_ma"));
    }
    [Fact]
    public async Task RejectsForeignRoomAndContractExpiredBetweenFormAndSend()
    {
        using var f=new Fixture(); using var db=f.Context(); var s=new BaoHongService(db,f.Time);
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.GuiAsync(2,Input(3)));
        Assert.Equal(2,(await s.PhongsAsync(2)).Count);
        f.Execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=1");
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.GuiAsync(2,Input()));
        f.Execute("UPDATE ky_hop_dong SET ngay_ket_thuc='2026-10-13' WHERE hop_dong_id=2");
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.GuiAsync(2,Input(2)));
        Assert.Empty(await s.PhongsAsync(2)); Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong"));
    }
    [Theory]
    [InlineData("NHAP")][InlineData("CHO_HIEU_LUC")][InlineData("DA_HUY")][InlineData("DA_KET_THUC")]
    public async Task RejectsNonActiveContract(string status)
    {
        using var f=new Fixture(); f.Execute($"UPDATE hop_dong SET trang_thai='{status}' WHERE id=1");using var db=f.Context();
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>new BaoHongService(db,f.Time).GuiAsync(2,Input()));
    }
    [Fact]
    public async Task RoommateCanSendOnLeavingDayButNotFollowingDayAndKeepsHistory()
    {
        using var f=new Fixture(); using var db=f.Context();var s=new BaoHongService(db,f.Time);
        Assert.Single(await s.PhongsAsync(4));var issue=await s.GuiAsync(4,Input());
        f.Time.UtcNow=f.Time.UtcNow.AddDays(1); Assert.Empty(await s.PhongsAsync(4));
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.GuiAsync(4,Input()));
        Assert.NotNull(await s.ChiTietAsync(4,issue.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.ChiTietAsync(2,issue.Id));
    }
    [Fact]
    public async Task VietnamMonthResetsAndCapacityBoundaryRejects()
    {
        using var f=new Fixture(); using var db=f.Context();var s=new BaoHongService(db,f.Time);
        f.Time.UtcNow=new(2026,10,31,16,59,59,DateTimeKind.Utc);
        Assert.Equal("BH-202610-0001",(await s.GuiAsync(2,Input())).MaBaoHong);
        f.Time.UtcNow=f.Time.UtcNow.AddSeconds(1);
        Assert.Equal("BH-202611-0001",(await s.GuiAsync(2,Input())).MaBaoHong);
        f.Execute("UPDATE bao_hong_so_ma SET so_cuoi=9998 WHERE thang='202611'");
        Assert.Equal("BH-202611-9999",(await s.GuiAsync(2,Input())).MaBaoHong);
        await Assert.ThrowsAsync<BaoHongValidationException>(()=>s.GuiAsync(2,Input()));
        Assert.Equal(3,f.Scalar("SELECT COUNT(*) FROM bao_hong"));
    }
    [Fact]
    public async Task ParallelRequestsHaveUniqueCodesAndRetriesHaveOneRecord()
    {
        using var f=new Fixture();
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(async()=>{using var db=f.Context();return await new BaoHongService(db,f.Time).GuiAsync(2,Input());})));
        Assert.Equal(8,results.Select(x=>x.MaBaoHong).Distinct().Count());
        var input=Input();var repeated=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(async()=>{using var db=f.Context();return await new BaoHongService(db,f.Time).GuiAsync(2,input);})));
        Assert.Equal(repeated[0].Id,repeated[1].Id);Assert.Equal(9,f.Scalar("SELECT COUNT(*) FROM bao_hong"));
    }
    [Fact]
    public async Task AuditFailureRollsBackReportAndCounter()
    {
        using var f=new Fixture();f.Execute("CREATE TRIGGER fail_report_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='bao_hong' BEGIN SELECT RAISE(ABORT,'test audit failure'); END;");
        using(var db=f.Context()) await Assert.ThrowsAsync<SqliteException>(()=>new BaoHongService(db,f.Time).GuiAsync(2,Input()));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong"));Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM bao_hong_so_ma"));
    }
    [Fact]
    public async Task RevokedPermissionAndInactiveAccountAreDenied()
    {
        using var f=new Fixture();using var db=f.Context();var s=new BaoHongService(db,f.Time);
        f.Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='KHACH_THUE' AND ModuleCode='BAO_HONG'");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.GuiAsync(2,Input()));
        f.Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='KHACH_THUE' AND ModuleCode='BAO_HONG'; UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=2");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.GuiAsync(2,Input()));
    }
    [Fact]
    public void UpgradePreservesExistingDataAndRerunIsSafe()
    {
        using var f=new Fixture();f.Execute("DROP TABLE bao_hong; DROP TABLE bao_hong_so_ma; DELETE FROM app_schema_version WHERE version=28");
        var accounts=f.Scalar("SELECT COUNT(*) FROM tai_khoan");
        var root=Directory.GetCurrentDirectory();while(!File.Exists(System.IO.Path.Combine(root,"QL_PhongTro","Data","permissions.seed.json")))root=Directory.GetParent(root)!.FullName;
        var seed=System.IO.Path.Combine(root,"QL_PhongTro","Data","permissions.seed.json");
        DatabaseUpdates.Update(f.Path,seed); DatabaseUpdates.Update(f.Path,seed);DatabaseUpdates.Check(f.Path);
        Assert.Equal(accounts,f.Scalar("SELECT COUNT(*) FROM tai_khoan"));Assert.Equal(28,f.Scalar("SELECT MAX(version) FROM app_schema_version"));
        Assert.Equal(3,f.Scalar("SELECT COUNT(*) FROM hop_dong"));
        using var c=new SqliteConnection($"Data Source={f.Path}");c.Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA foreign_key_check";using var reader=cmd.ExecuteReader();Assert.False(reader.Read());
    }
}
