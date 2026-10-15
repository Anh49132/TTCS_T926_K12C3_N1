using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using Xunit;
namespace QL_PhongTro.Tests;

// Every case creates its own synthetic v30 database. No local demo or backup is required.
public sealed partial class BaoHongChiPhiSchemaTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; }
        public string Path { get; }
        public SqliteConnection Db { get; }
        public int Manager { get; }
        public int Contract { get; }
        public int Owner { get; }
        public string Seed => System.IO.Path.Combine(Root,"QL_PhongTro","Data","permissions.seed.json");
        public Fixture(bool upgrade = true)
        {
            Root = Directory.GetCurrentDirectory();
            while (!File.Exists(System.IO.Path.Combine(Root,"AGENTS.md"))) Root = Directory.GetParent(Root)!.FullName;
            var directory = System.IO.Path.Combine(Root,"data","s407-cost","schema-tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); Path = System.IO.Path.Combine(directory,"test.sqlite");
            LocalDatabaseInitializer.Create(Path, Seed);
            RentalRequestSchema.Initialize(Path);
            SchemaTestFixture.RemoveTatToanExtensions(Path);
            SchemaTestFixture.RemoveChiPhiExtensions(Path);
            using (var setup = new SqliteConnection($"Data Source={Path};Foreign Keys=True;Pooling=False"))
            {
                setup.Open(); using var command = setup.CreateCommand();
                command.CommandText = """
                    DELETE FROM app_schema_version WHERE version>=31;
                    INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat) VALUES
                    (1,'Owner','owner@cost.test','0900000001','hash','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
                    (2,'Tenant','tenant@cost.test','0900000002','hash','KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
                    (3,'Manager','manager@cost.test','0900000003','hash','QUAN_LY',1,1,'2026-01-01','2026-01-01');
                    INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,2,'Tenant','2026-01-01');
                    INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(1,1,3,'Cost test','Test address');
                    INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES
                    (1,1,'A101',1,25,3000000,3,'DANG_THUE','2026-01-01'),(2,1,'A102',1,25,3000000,3,'DANG_THUE','2026-01-01');
                    INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES
                    (1,'HD-COST-1',1,1,'DANG_HIEU_LUC'),(2,'HD-COST-2',2,1,'DANG_HIEU_LUC');
                    INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES
                    (1,'2026-10-01','2026-12-31',3000000),(2,'2026-10-01','2026-12-31',3000000);
                    INSERT INTO bao_hong(id,ma_bao_hong,nguoi_bao_id,khach_thue_id,phong_id,hop_dong_id,loai_thiet_bi,mo_ta,muc_do,trang_thai,ngay_tao,khoa_chong_trung) VALUES
                    (1,'BH-202610-0001',2,1,2,2,'Tap','Test','THUONG','HOAN_THANH','2026-10-14 05:00:00','11111111111111111111111111111111'),
                    (2,'BH-202610-0002',2,1,2,2,'Tap','Test','THUONG','HOAN_THANH','2026-10-14 05:00:00','22222222222222222222222222222222'),
                    (3,'BH-202610-0003',2,1,2,2,'Tap','Test','THUONG','TU_CHOI','2026-10-14 05:00:00','33333333333333333333333333333333'),
                    (4,'BH-202610-0004',2,1,2,2,'Tap','Test','THUONG','DANG_XU_LY','2026-10-14 05:00:00','44444444444444444444444444444444');
                    """;
                command.ExecuteNonQuery();
            }
            DichVuSchemaInitializer.InitializeInvoices(Path);
            if (upgrade) DatabaseUpdates.Update(Path,Seed);
            Db = new SqliteConnection($"Data Source={Path};Foreign Keys=True;Pooling=False;Default Timeout=20"); Db.Open();
            using var cmd = Db.CreateCommand(); cmd.CommandText = """
                SELECT t.quan_ly_id,b.hop_dong_id,t.chu_nha_id FROM bao_hong b
                JOIN phong_tro p ON p.id=b.phong_id JOIN toa_nha t ON t.id=p.toa_nha_id WHERE b.id=1
                """;
            using var reader=cmd.ExecuteReader(); Assert.True(reader.Read());
            Manager=reader.GetInt32(0); Contract=reader.GetInt32(1); Owner=reader.GetInt32(2);
        }
        public long Scalar(string sql) { using var cmd=Db.CreateCommand(); cmd.CommandText=sql; return Convert.ToInt64(cmd.ExecuteScalar()); }
        public void Execute(string sql,SqliteTransaction? tx=null) { using var cmd=Db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;cmd.ExecuteNonQuery(); }
        public int Cost(string amount="350000",string side="KHACH_THUE",int report=1,int? actor=null,string? period="2026-11-01",SqliteTransaction? tx=null)
        {
            using var cmd=Db.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=$"""
                INSERT INTO chi_phi_bao_hong(bao_hong_id,hop_dong_id,so_tien,ben_chiu_phi,cho_thu_khach,ky_du_kien,
                    nguoi_tao_id,nguoi_cap_nhat_id,ngay_tao,ngay_cap_nhat,phien_ban)
                VALUES({report},{Contract},{amount},$side,$charge,$period,$actor,$actor,'2026-10-14 05:00:00','2026-10-14 05:00:00',1) RETURNING id
                """;
            cmd.Parameters.AddWithValue("$side",side);cmd.Parameters.AddWithValue("$charge",side=="KHACH_THUE"?1:0);
            cmd.Parameters.AddWithValue("$period",(object?)period??DBNull.Value);cmd.Parameters.AddWithValue("$actor",actor??Manager);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public string History(int cost,SqliteTransaction? tx=null,string? key=null)
        {
            key??=Guid.NewGuid().ToString("N"); using var cmd=Db.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText="""
                INSERT INTO lich_su_chi_phi_bao_hong(chi_phi_id,nguoi_cap_nhat_id,thoi_diem,loai,so_tien_sau,
                    ben_chiu_phi_sau,ky_du_kien_sau,phien_ban_truoc,phien_ban_sau,khoa_chong_trung)
                SELECT id,nguoi_cap_nhat_id,ngay_cap_nhat,'TAO',so_tien,ben_chiu_phi,ky_du_kien,0,1,$key
                FROM chi_phi_bao_hong WHERE id=$id
                """;
            cmd.Parameters.AddWithValue("$key",key);cmd.Parameters.AddWithValue("$id",cost);cmd.ExecuteNonQuery();return key;
        }
        public int Invoice(int month=11,int? contract=null,int? replacement=null,SqliteTransaction? tx=null)
        {
            using var cmd=Db.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText="""
                INSERT INTO hoa_don(ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,
                    ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id,thay_the_hoa_don_id)
                VALUES($code,$contract,$month,2026,$first,$last,$first,1,'2026-10-14 05:00:00','2026-11-07',350000,'NHAP',$owner,$replacement) RETURNING id
                """;
            cmd.Parameters.AddWithValue("$code","COST-TEST-"+Guid.NewGuid().ToString("N"));cmd.Parameters.AddWithValue("$contract",contract??Contract);
            cmd.Parameters.AddWithValue("$month",month);cmd.Parameters.AddWithValue("$first",new DateOnly(2026,month,1).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$last",new DateOnly(2026,month,1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$owner",Owner);cmd.Parameters.AddWithValue("$replacement",(object?)replacement??DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public void Line(int invoice,int order=1,int report=1,long amount=350000,SqliteTransaction? tx=null) => Execute($"""
            INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,bao_hong_id,loai_khoan,ten_khoan,so_luong,don_gia,thanh_tien,ghi_chu)
            VALUES({invoice},{order},{report},'PHAT_SINH','Repair cost','1',{amount},{amount},'Repair source snapshot')
            """,tx);
        public string Status() { using var cmd=Db.CreateCommand();cmd.CommandText="SELECT trang_thai_phan_bo FROM chi_phi_bao_hong_trang_thai WHERE bao_hong_id=1";return (string)cmd.ExecuteScalar()!; }
        public void Dispose() => Db.Dispose();
    }
    private static void Rejected(Action action) => Assert.Equal(19,Assert.Throws<SqliteException>(action).SqliteErrorCode);

    [Fact]
    public void UpgradePreservesEveryOldRowAndRerunDoesNotChangeDatabase()
    {
        using var f=new Fixture(false);
        static Dictionary<string,string> Snapshot(SqliteConnection db)
        {
            var result=new Dictionary<string,string>(); var names=new List<string>();
            using(var cmd=db.CreateCommand()) {cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'app_schema_version'";using var r=cmd.ExecuteReader();while(r.Read())names.Add(r.GetString(0));}
            foreach(var name in names) {using var cmd=db.CreateCommand();cmd.CommandText=$"SELECT * FROM \"{name}\"";using var r=cmd.ExecuteReader();var rows=new List<string>();while(r.Read()){var values=new object[r.FieldCount];r.GetValues(values);rows.Add(System.Text.Json.JsonSerializer.Serialize(values));}result[name]=string.Join("\n",rows.Order());}
            return result;
        }
        var old=Snapshot(f.Db); DatabaseUpdates.Update(f.Path,f.Seed); DatabaseUpdates.Check(f.Path);
        var after=Snapshot(f.Db); foreach(var row in old)Assert.Equal(row.Value,after[row.Key]);
        Assert.Equal(34,f.Scalar("SELECT MAX(version) FROM app_schema_version"));
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM chi_phi_bao_hong"));Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM lich_su_chi_phi_bao_hong"));
        f.Db.Close(); SqliteConnection.ClearAllPools();
        var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f.Path));
        DatabaseUpdates.Update(f.Path,f.Seed);
        SqliteConnection.ClearAllPools();
        Assert.Equal(hash,System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f.Path)));
        f.Db.Open();
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM app_schema_version WHERE version=31"));
    }
    [Theory]
    [InlineData("-1")][InlineData("0")][InlineData("NULL")][InlineData("350000.5")][InlineData("'invalid'")][InlineData("9223372036854775808")]
    public void RejectsInvalidAmounts(string amount) {using var f=new Fixture();Rejected(()=>f.Cost(amount));Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM chi_phi_bao_hong"));}
    [Theory]
    [InlineData("")][InlineData("OTHER")]
    public void RejectsInvalidPayer(string side) {using var f=new Fixture();Rejected(()=>f.Cost(side:side,period:null));}
    [Theory]
    [InlineData(3)][InlineData(4)]
    public void RejectsRejectedOrUnfinishedReport(int report) {using var f=new Fixture();Rejected(()=>f.Cost(report:report));}
    [Fact]
    public void CompletedOnlyAndOriginalContractAndPermissionAreEnforced()
    {
        using var f=new Fixture();Rejected(()=>f.Cost(actor:f.Owner));
        f.Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        Rejected(()=>f.Cost());f.Execute("UPDATE role_permission SET AccessLevel='WRITE' WHERE RoleCode='QUAN_LY' AND ModuleCode='BAO_HONG'");
        f.Execute($"UPDATE toa_nha SET quan_ly_id=NULL WHERE quan_ly_id={f.Manager}");Rejected(()=>f.Cost());
    }
    [Fact]
    public void OneTotalPayerFlagPeriodAndEndedContract()
    {
        using var f=new Fixture();Rejected(()=>f.Cost(period:"2026-10-01"));Rejected(()=>f.Cost(period:"2026-11-02"));
        f.Execute($"UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id={f.Contract}");
        var id=f.Cost(period:null);Assert.Equal("CHO_DUA_VAO_HOA_DON",f.Status());Rejected(()=>f.Cost());
        f.Execute($"UPDATE chi_phi_bao_hong SET ben_chiu_phi='CHU_NHA',cho_thu_khach=0,ky_du_kien=NULL,phien_ban=2 WHERE id={id}");
        Assert.Equal("KHONG_THU_KHACH",f.Status());
        Rejected(()=>f.Execute($"UPDATE chi_phi_bao_hong SET cho_thu_khach=1,phien_ban=3 WHERE id={id}"));
        Rejected(()=>f.Execute($"UPDATE chi_phi_bao_hong SET hop_dong_id=1,phien_ban=3 WHERE id={id}"));
    }
    [Fact]
    public void HistoryHasUniqueKeyVersionSnapshotAndIsAppendOnly()
    {
        using var f=new Fixture();var id=f.Cost();var key=f.History(id);
        Rejected(()=>f.History(id,key:key));Rejected(()=>f.History(id));
        Rejected(()=>f.Execute("UPDATE lich_su_chi_phi_bao_hong SET so_tien_sau=1"));Rejected(()=>f.Execute("DELETE FROM lich_su_chi_phi_bao_hong"));
        Rejected(()=>f.Execute("DELETE FROM chi_phi_bao_hong"));
        f.Execute("UPDATE chi_phi_bao_hong SET so_tien=360000,phien_ban=2,ngay_cap_nhat='2026-10-14 05:01:00'");
        f.Execute($"""
            INSERT INTO lich_su_chi_phi_bao_hong(chi_phi_id,nguoi_cap_nhat_id,thoi_diem,loai,so_tien_truoc,ben_chiu_phi_truoc,
              ky_du_kien_truoc,so_tien_sau,ben_chiu_phi_sau,ky_du_kien_sau,phien_ban_truoc,phien_ban_sau,khoa_chong_trung)
            VALUES({id},{f.Manager},'2026-10-14 05:01:00','SUA',350000,'KHACH_THUE','2026-11-01',360000,'KHACH_THUE','2026-11-01',1,2,'{Guid.NewGuid():N}')
            """);
        Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM lich_su_chi_phi_bao_hong"));
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Db).Options);
        Assert.Equal(360000,(db.ChiPhiBaoHongs.AsNoTracking().Single()).SoTien);
        Assert.Equal(2,db.LichSuChiPhiBaoHongs.AsNoTracking().Count());
    }
    [Fact]
    public void DraftReservesSourceAndLocksCostAndProtectsSnapshot()
    {
        using var f=new Fixture();f.Cost();var a=f.Invoice();var b=f.Invoice(12);
        f.Line(a);Assert.Equal("TRONG_HOA_DON_NHAP",f.Status());
        Rejected(()=>f.Line(a,2));Rejected(()=>f.Line(b));
        Rejected(()=>f.Execute("UPDATE chi_phi_bao_hong SET so_tien=360000,phien_ban=2"));
        Rejected(()=>f.Execute($"UPDATE chi_tiet_hoa_don SET bao_hong_id=NULL WHERE hoa_don_id={a}"));
        Rejected(()=>f.Execute($"DELETE FROM chi_tiet_hoa_don WHERE hoa_don_id={a}"));
        Rejected(()=>f.Execute($"UPDATE chi_tiet_hoa_don SET don_gia=1,thanh_tien=1 WHERE hoa_don_id={a}"));
    }
    [Fact]
    public void OwnerCostWrongContractAndWrongAmountCannotBeCharged()
    {
        using var f=new Fixture();var cost=f.Cost(side:"CHU_NHA",period:null);var invoice=f.Invoice();Rejected(()=>f.Line(invoice));
        f.Execute($"UPDATE chi_phi_bao_hong SET ben_chiu_phi='KHACH_THUE',cho_thu_khach=1,ky_du_kien='2026-11-01',phien_ban=2 WHERE id={cost}");
        Rejected(()=>f.Line(invoice,amount:1));var other=f.Invoice(12,contract:1);Rejected(()=>f.Line(other));
        Rejected(()=>f.Line(invoice,report:2));
    }
    [Fact]
    public void IssuedCancelledAndReplacementKeepOneEffectiveChargeAndHistory()
    {
        using var f=new Fixture();f.Cost();var old=f.Invoice();f.Line(old);
        f.Execute($"UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh='2026-10-14 05:00:00',ngay_phat_hanh_nghiep_vu='2026-10-14',nguoi_phat_hanh_id={f.Owner},phien_ban=1 WHERE id={old}");
        Assert.Equal("DA_DUA_VAO_HOA_DON",f.Status());
        f.Execute($"UPDATE hoa_don SET trang_thai='DA_HUY',nguoi_huy_id={f.Owner},ngay_huy='2026-10-14 05:02:00',ly_do_huy='Schema test',phien_ban=2 WHERE id={old}");
        Assert.Equal("HOA_DON_DA_HUY",f.Status());
        Rejected(()=>f.Execute("UPDATE chi_phi_bao_hong SET so_tien=360000,phien_ban=2"));
        var replacement=f.Invoice(replacement:old);f.Line(replacement);Assert.Equal("TRONG_HOA_DON_NHAP",f.Status());
        var future=f.Invoice(12);Rejected(()=>f.Line(future));
        Assert.Equal(2,f.Scalar("SELECT COUNT(*) FROM chi_tiet_hoa_don WHERE bao_hong_id=1"));
        Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM chi_tiet_hoa_don d JOIN hoa_don h ON h.id=d.hoa_don_id WHERE d.bao_hong_id=1 AND h.trang_thai<>'DA_HUY'"));
    }
    [Fact]
    public void WholeTransactionRollbackLeavesNoCostHistoryInvoiceOrCharge()
    {
        using var f=new Fixture();var invoices=f.Scalar("SELECT COUNT(*) FROM hoa_don");
        using(var tx=f.Db.BeginTransaction()){var id=f.Cost(tx:tx);f.History(id,tx);var invoice=f.Invoice(tx:tx);f.Line(invoice,tx:tx);tx.Rollback();}
        Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM chi_phi_bao_hong"));Assert.Equal(0,f.Scalar("SELECT COUNT(*) FROM lich_su_chi_phi_bao_hong"));
        Assert.Equal(invoices,f.Scalar("SELECT COUNT(*) FROM hoa_don"));
    }
    [Fact]
    public async Task ConcurrentCreationHasOneSourceAndStaleEditsCannotOverwrite()
    {
        using var f=new Fixture();
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>{
            using var c=new SqliteConnection($"Data Source={f.Path};Foreign Keys=True;Pooling=False;Default Timeout=20");c.Open();
            using var tx=c.BeginTransaction(deferred:false);using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=$"""
                INSERT INTO chi_phi_bao_hong(bao_hong_id,hop_dong_id,so_tien,ben_chiu_phi,cho_thu_khach,
                  nguoi_tao_id,nguoi_cap_nhat_id,ngay_tao,ngay_cap_nhat,phien_ban)
                VALUES(1,{f.Contract},350000,'KHACH_THUE',1,{f.Manager},{f.Manager},'2026-10-14 05:00:00','2026-10-14 05:00:00',1)
                """;
            try{cmd.ExecuteNonQuery();tx.Commit();return true;}catch(SqliteException ex) when(ex.SqliteErrorCode==19){return false;}
        })));
        Assert.Single(results,x=>x);Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM chi_phi_bao_hong"));
        var edits=await Task.WhenAll(new[]{360000,370000}.Select(amount=>Task.Run(()=>{
            using var c=new SqliteConnection($"Data Source={f.Path};Foreign Keys=True;Pooling=False;Default Timeout=20");c.Open();
            using var tx=c.BeginTransaction(deferred:false);using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=$"UPDATE chi_phi_bao_hong SET so_tien={amount},phien_ban=2 WHERE bao_hong_id=1 AND phien_ban=1";
            var count=cmd.ExecuteNonQuery();tx.Commit();return count;
        })));
        Assert.Equal(1,edits.Sum());Assert.Equal(2,f.Scalar("SELECT phien_ban FROM chi_phi_bao_hong"));
    }
    [Fact]
    public async Task ConcurrentInvoiceAllocationHasOneEffectiveCharge()
    {
        using var f=new Fixture();f.Cost();var invoices=new[]{f.Invoice(),f.Invoice(12)};
        var outcomes=await Task.WhenAll(invoices.Select(invoice=>Task.Run(()=>{
            using var c=new SqliteConnection($"Data Source={f.Path};Foreign Keys=True;Pooling=False;Default Timeout=20");c.Open();
            using var tx=c.BeginTransaction(deferred:false);using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=$"""
                INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,bao_hong_id,loai_khoan,ten_khoan,so_luong,don_gia,thanh_tien,ghi_chu)
                VALUES({invoice},1,1,'PHAT_SINH','Repair','1',350000,350000,'Concurrent schema check')
                """;
            try{cmd.ExecuteNonQuery();tx.Commit();return true;}catch(SqliteException ex) when(ex.SqliteErrorCode==19){return false;}
        })));
        Assert.Single(outcomes,x=>x);Assert.Equal(1,f.Scalar("SELECT COUNT(*) FROM chi_tiet_hoa_don WHERE bao_hong_id=1"));
    }
    [Fact]
    public void OriginalContractForeignKeysAndHistorySnapshotsCannotBeForged()
    {
        using var f=new Fixture();Rejected(()=>f.Execute($"""
            INSERT INTO chi_phi_bao_hong(bao_hong_id,hop_dong_id,so_tien,ben_chiu_phi,cho_thu_khach,nguoi_tao_id,nguoi_cap_nhat_id,ngay_tao,ngay_cap_nhat,phien_ban)
            VALUES(1,1,350000,'KHACH_THUE',1,{f.Manager},{f.Manager},'2026-10-14 05:00:00','2026-10-14 05:00:00',1)
            """));
        var id=f.Cost();Rejected(()=>f.Execute($"""
            INSERT INTO lich_su_chi_phi_bao_hong(chi_phi_id,nguoi_cap_nhat_id,thoi_diem,loai,so_tien_sau,ben_chiu_phi_sau,phien_ban_truoc,phien_ban_sau,khoa_chong_trung)
            VALUES({id},{f.Manager},'2026-10-14 05:00:00','TAO',1,'KHACH_THUE',0,1,'{Guid.NewGuid():N}')
            """));
        Rejected(()=>f.Execute($"DELETE FROM hop_dong WHERE id={f.Contract}"));
        Rejected(()=>f.Execute("DELETE FROM bao_hong WHERE id=1"));
        Rejected(()=>f.Execute("UPDATE chi_phi_bao_hong SET so_tien=360000,phien_ban=1"));
    }
    [Fact]
    public void MissingGuardAndUnversionedSchemaConflictAreNotSilentlyRepaired()
    {
        using(var f=new Fixture()){f.Execute("DROP TRIGGER chi_phi_invoice_insert");Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Check(f.Path));}
        using(var f=new Fixture(false)){f.Execute("CREATE TABLE chi_phi_bao_hong(dummy INTEGER)");Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Update(f.Path,f.Seed));Assert.Equal(30,f.Scalar("SELECT MAX(version) FROM app_schema_version"));}
    }
}
