using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "s305-" + Guid.NewGuid().ToString("N") + ".sqlite");
    private readonly string app;
    private const string Password = "MeterTest!2026";
    private sealed class Clock : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
    }
    private readonly Clock clock = new();
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtectionProvider meterProtection = new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();

    public MeterReadingListTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "QL_PhongTro", "Data"))) root = root.Parent;
        app = Path.Combine(root!.FullName, "QL_PhongTro");
        LocalDatabaseInitializer.Create(path, Path.Combine(app, "Data", "permissions.seed.json"));
        RentalRequestSchema.Initialize(path);
        DichVuSchemaInitializer.InitializeInvoices(path);
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            VALUES(1,'Owner','owner@meter.test','0900000001',$hash,'CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                  (2,'Manager','manager@meter.test','0900000002',$hash,'QUAN_LY',1,1,'2026-01-01','2026-01-01'),
                  (3,'Other manager','other@meter.test','0900000003',$hash,'QUAN_LY',1,1,'2026-01-01','2026-01-01'),
                  (4,'Tenant','tenant@meter.test','0900000004',$hash,'KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
                  (5,'Admin','admin@meter.test','0900000005',$hash,'ADMIN',1,1,'2026-01-01','2026-01-01');
            INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang)
            VALUES(1,1,2,'Assigned building','Test',31),(2,1,3,'Foreign building','Test',1);
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES(1,1,'B202',2,20,1000000,2,'DANG_THUE','2026-01-01'),
                  (2,1,'A002',0,20,1000000,2,'DANG_THUE','2026-01-01'),
                  (3,1,'A001',0,20,1000000,2,'DANG_THUE','2026-01-01'),
                  (4,1,'EMPTY',1,20,1000000,2,'TRONG','2026-01-01'),
                  (5,2,'SECRET',0,20,1000000,2,'DANG_THUE','2026-01-01');
            INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Signer','2026-01-01');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES
                (1,'CURRENT-1',1,1,'DANG_HIEU_LUC'),(2,'CURRENT-2',2,1,'DANG_HIEU_LUC'),
                (3,'CURRENT-3',3,1,'DANG_HIEU_LUC'),(4,'EMPTY-4',4,1,'DANG_HIEU_LUC'),
                (5,'FOREIGN-5',5,1,'DANG_HIEU_LUC'),(6,'OLD-1',1,1,'DA_KET_THUC');
            INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES
                (1,'2026-08-01','2026-12-31',1000000),(2,'2026-08-01','2026-12-31',1000000),
                (3,'2026-08-01','2026-12-31',1000000),(4,'2026-08-01','2026-12-31',1000000),
                (5,'2026-08-01','2026-12-31',1000000),(6,'2026-01-01','2026-07-31',1000000);
            INSERT INTO dich_vu(id,ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES(1,'DIEN','Electricity',1),(2,'NUOC','Water',1);
            """;
        cmd.Parameters.AddWithValue("$hash", BCrypt.Net.BCrypt.HashPassword(Password, 4)); cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open() { var c = new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False"); c.Open(); return c; }
    private void Execute(string sql) { using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
    private async Task<ChiSoDienNuocViewModel> List(int actor = 2, int? building = 1)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options);
        return await new ChiSoDienNuocService(db, clock, meterProtection).DanhSachAsync(actor, building);
    }
    private void Handover(int contract = 1) => Execute($"INSERT INTO hop_dong_chi_so_dau_ky(hop_dong_id,ngay_ban_giao,chi_so_dien,chi_so_nuoc,nguoi_nhap_id,ngay_nhap) VALUES({contract},'2026-08-01','0','12.345',1,'2026-08-01')");
    private void Invoice(int id, int contract, int month, string status = "DA_PHAT_HANH", decimal? electricity = 100, decimal? water = 20, string type = "THEO_CHI_SO", bool issued = false)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        var start = new DateOnly(2026, month, 1); var end = start.AddMonths(1).AddDays(-1);
        cmd.CommandText = """
            INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,nam,thang,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
            VALUES($id,$code,$contract,2026,$month,$start,$end,$end,1,'2026-01-01',$end,0,'NHAP',1);
            INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,dich_vu_id,loai_khoan,ten_khoan,cach_tinh_ap_dung,so_luong,don_gia,chi_so_cuoi,thanh_tien)
            VALUES($id,1,1,'DICH_VU','Electricity',$type,'999',0,$electricity,0),($id,2,2,'DICH_VU','Water',$type,'999',0,$water,0);
            UPDATE hoa_don SET trang_thai=$status,
                ngay_phat_hanh=CASE WHEN $issued THEN '2026-10-07' ELSE NULL END,
                nguoi_phat_hanh_id=CASE WHEN $issued THEN 1 ELSE NULL END
            WHERE id=$id;
            """;
        foreach (var p in new (string, object?)[] { ("id",id),("code","INVOICE-"+id),("contract",contract),("month",month),
            ("start",start.ToString("yyyy-MM-dd")),("end",end.ToString("yyyy-MM-dd")),("type",type),("status",status),
            ("issued",issued),
            ("electricity",electricity?.ToString(System.Globalization.CultureInfo.InvariantCulture)),("water",water?.ToString(System.Globalization.CultureInfo.InvariantCulture)) })
            cmd.Parameters.AddWithValue("$"+p.Item1,p.Item2 ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    [Fact] public async Task AssignedBuildingSortedOccupiedRoomsOnly()
    {
        var vm = await List(); Assert.Single(vm.ToaNhas); Assert.Equal(1, vm.ToaNhaId);
        Assert.Equal(new[] { "A001", "A002", "B202" }, vm.Phongs.Select(x => x.MaPhong));
        Assert.All(vm.Phongs, x => { Assert.Null(x.Dien.GiaTri); Assert.Null(x.Nuoc.GiaTri); Assert.False(x.DaChot); });
    }
    [Theory][InlineData("NHAP")][InlineData("DA_HUY")][InlineData("DA_KET_THUC")][InlineData("CHO_HIEU_LUC")]
    public async Task NonActiveContractsExcluded(string status) { Execute($"UPDATE hop_dong SET trang_thai='{status}' WHERE id=1"); Assert.DoesNotContain((await List()).Phongs,x=>x.PhongId==1); }
    [Theory][InlineData("2026-11-01","2027-01-01",false)][InlineData("2026-01-01","2026-09-30",false)]
    [InlineData("2026-10-31","2027-01-01",true)][InlineData("2026-01-01","2026-10-01",true)]
    public async Task InclusivePeriodOverlap(string start,string end,bool included)
    { Execute($"UPDATE ky_hop_dong SET ngay_bat_dau='{start}',ngay_ket_thuc='{end}' WHERE hop_dong_id=1");Assert.Equal(included,(await List()).Phongs.Any(x=>x.PhongId==1)); }
    [Fact] public async Task TermsAndSuccessiveContractsDoNotDuplicateOrMixReadings()
    {
        Execute("INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(1,'2026-10-01','2026-12-31',1000000); UPDATE hop_dong SET trang_thai='DANG_HIEU_LUC' WHERE id=6; UPDATE ky_hop_dong SET ngay_ket_thuc='2026-10-01' WHERE hop_dong_id=6;");
        Invoice(1,6,9,electricity:999,water:888); var vm=await List();Assert.Equal(3,vm.Phongs.Count);var row=Assert.Single(vm.Phongs,x=>x.PhongId==1);Assert.Equal(1,row.HopDongId);Assert.Null(row.Dien.GiaTri);
    }
    [Fact] public async Task PreviousPublishedReadingsAndPerServiceFallback()
    {
        Handover(); Invoice(1,1,8,electricity:50,water:10); Invoice(2,1,9,electricity:0,water:null);
        Invoice(3,1,10,electricity:500,water:60); Invoice(4,1,11,electricity:900,water:100);
        var row=Assert.Single((await List()).Phongs,x=>x.PhongId==1);Assert.Equal(0,row.Dien.GiaTri);Assert.False(row.Dien.LaBanGiao);Assert.Equal(10,row.Nuoc.GiaTri);Assert.True(row.DaChot);
    }
    [Theory][InlineData("NHAP","THEO_CHI_SO")][InlineData("DA_HUY","THEO_CHI_SO")][InlineData("DA_PHAT_HANH","THEO_NGUOI")][InlineData("DA_PHAT_HANH","CO_DINH")]
    public async Task DraftCancelledAndNonMeterSnapshotsNotReadings(string status,string type)
    { Invoice(1,1,9,status,type:type);var row=Assert.Single((await List()).Phongs,x=>x.PhongId==1);Assert.Null(row.Dien.GiaTri);Assert.Null(row.Nuoc.GiaTri);Assert.False(row.DaChot); }
    [Fact] public async Task HandoverZeroAndMissingDataPreserved()
    { Handover();Invoice(1,1,9,electricity:45,water:null);var row=Assert.Single((await List()).Phongs,x=>x.PhongId==1);Assert.Equal(45,row.Dien.GiaTri);Assert.False(row.Dien.LaBanGiao);Assert.Equal(12.345m,row.Nuoc.GiaTri);Assert.True(row.Nuoc.LaBanGiao); }
    [Fact] public async Task HandoverFutureExcludedAndRealZeroKept()
    { Handover();var row=Assert.Single((await List()).Phongs,x=>x.PhongId==1);Assert.Equal(0,row.Dien.GiaTri);Assert.True(row.Dien.LaBanGiao);Execute("UPDATE hop_dong_chi_so_dau_ky SET ngay_ban_giao='2026-11-01'");Assert.Null(Assert.Single((await List()).Phongs,x=>x.PhongId==1).Dien.GiaTri); }
    [Theory][InlineData(null,null,"THEO_CHI_SO")][InlineData(10,null,"THEO_CHI_SO")][InlineData(10,20,"THEO_NGUOI")]
    public async Task CurrentInvoiceWithoutBothRealMetersRemainsUnclosed(int? electricity,int? water,string type)
    {Invoice(1,1,10,electricity:electricity,water:water,type:type);Assert.False(Assert.Single((await List()).Phongs,x=>x.PhongId==1).DaChot);}
    [Fact] public async Task ForeignBuildingAndUnsupportedRoleForbidden()
    { await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>List(building:2));Assert.Equal(1,(await List(actor:1)).ToaNhaId);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>List(actor:4));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>List(actor:5)); }
    [Fact] public async Task OptionalInvoiceModuleMissingStillListsRooms()
    {
        var optionalPath = Path.Combine(Path.GetTempPath(), "s305-no-invoices-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            LocalDatabaseInitializer.Create(optionalPath, Path.Combine(app,"Data","permissions.seed.json"));
            RentalRequestSchema.Initialize(optionalPath);
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={optionalPath};Pooling=False").Options);
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                VALUES(1,'Owner','owner@optional.test','0900000001','test','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                      (2,'Manager','manager@optional.test','0900000002','test','QUAN_LY',1,1,'2026-01-01','2026-01-01');
                INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(1,1,2,'Optional','Test');
                INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(1,1,'A1',0,20,1000000,2,'DANG_THUE','2026-01-01');
                INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Signer','2026-01-01');
                INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(1,'OPTIONAL',1,1,'DANG_HIEU_LUC');
                INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(1,'2026-01-01','2026-12-31',1000000);
                INSERT INTO hop_dong_chi_so_dau_ky(hop_dong_id,ngay_ban_giao,chi_so_dien,chi_so_nuoc,nguoi_nhap_id,ngay_nhap) VALUES(1,'2026-01-01','0','7',1,'2026-01-01');
                """);
            var row = Assert.Single((await new ChiSoDienNuocService(db,clock,meterProtection).DanhSachAsync(2,1)).Phongs);
            Assert.Equal(0,row.Dien.GiaTri);Assert.True(row.Dien.LaBanGiao);Assert.False(row.DaChot);
        }
        finally { DeleteDatabaseArtifacts(optionalPath); }
    }
    [Fact] public async Task UnassignedInactiveAndReturnedContractsExcluded()
    { Execute("UPDATE hop_dong SET ngay_tra_phong='2026-09-30' WHERE id=1");Assert.DoesNotContain((await List()).Phongs,x=>x.PhongId==1);Execute("UPDATE toa_nha SET dang_hoat_dong=0 WHERE id=1");Assert.Empty((await List(building:null)).ToaNhas);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>List()); }
    [Theory][InlineData(2026,9,30,16,59,2026,9)][InlineData(2026,9,30,17,0,2026,10)][InlineData(2026,12,31,17,0,2027,1)]
    public async Task VietnamCalendarMonth(int year,int month,int day,int hour,int minute,int expectedYear,int expectedMonth)
    {clock.UtcNow=new(year,month,day,hour,minute,0,DateTimeKind.Utc);Assert.Equal(new DateOnly(expectedYear,expectedMonth,1),(await List()).DauKy);}
    public void Dispose() => DeleteDatabaseArtifacts(path);

    private static void DeleteDatabaseArtifacts(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(databasePath)!;
        var prefix = Path.GetFileName(databasePath);
        foreach (var file in Directory.EnumerateFiles(directory, prefix + "*"))
            File.Delete(file);
    }
}
