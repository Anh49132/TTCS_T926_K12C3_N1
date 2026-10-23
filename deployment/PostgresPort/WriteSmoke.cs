using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Npgsql;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class WriteSmoke
{
    private sealed class Clock : ITimeProvider { public DateTime UtcNow => new(2026,10,23,0,0,0,DateTimeKind.Utc); }
    public static async Task RunAsync(string configPath, string sqlPath)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var uri = new Uri(config.RootElement.GetProperty("ConnectionUri").GetString()!);
        if (config.RootElement.GetProperty("BranchId").GetString() != "br-spring-smoke-b33dbe1e"
            || config.RootElement.GetProperty("HostSha256").GetString() != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.Host))))
            throw new InvalidOperationException("Only the pinned test branch is allowed.");
        var user = uri.UserInfo.Split(':',2);
        // Session-scoped test schemas require a direct connection, not transaction
        // pooling (which can switch server sessions between commands).
        var cs = new NpgsqlConnectionStringBuilder { Host=uri.Host.Replace("-pooler.","."), Database=uri.AbsolutePath.Trim('/'), Username=Uri.UnescapeDataString(user[0]), Password=Uri.UnescapeDataString(user[1]), SslMode=SslMode.VerifyFull, IncludeErrorDetail=false, Timeout=30, CommandTimeout=120 };
        await using var connection = new NpgsqlConnection(cs.ConnectionString); await connection.OpenAsync();
        var schema = "synthetic_" + Guid.NewGuid().ToString("N");
        var sql = await File.ReadAllTextAsync(sqlPath);
        if (sql.Contains("DROP ",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        await using (var tx = await connection.BeginTransactionAsync())
        {
            using var command = new NpgsqlCommand("CREATE SCHEMA \""+schema+"\"; SET LOCAL search_path TO \""+schema+"\",pg_catalog; " + sql + """
                INSERT INTO app_schema_version VALUES(42,'2026-10-23');
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,ngay_tao,ngay_cap_nhat)
                VALUES(1,'Synthetic Owner','owner@example.test','0900000001','synthetic-not-a-login','CHU_NHA','2026-01-01','2026-01-01'),
                (2,'Synthetic Other','other@example.test','0900000002','synthetic-not-a-login','CHU_NHA','2026-01-01','2026-01-01');
                INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Synthetic','Test');
                INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Synthetic Tenant','2026-01-01');
                INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,ngay_tao) VALUES(1,1,'TEST',1,20,1000000,2,'2026-01-01');
                INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id) VALUES(1,'HD-SYNTHETIC',1,1,'DANG_HIEU_LUC',1);
                INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue,so_thang,nguoi_lap_id) VALUES(1,'2026-01-01','2028-12-31',1000000,36,1);
                INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
                SELECT n,'HD-SYNTHETIC-'||n,1,n,2026,make_date(2026,n,1),(make_date(2026,n,1)+interval '1 month -1 day')::date,make_date(2026,n,1),1,'2026-01-01',make_date(2026,n,7),1000000,'DA_PHAT_HANH',1 FROM generate_series(1,3) n;
                INSERT INTO anh_phong(phong_id,duong_dan,thu_tu,ngay_tao) SELECT 1,'/synthetic/'||n,n,'2026-01-01' FROM generate_series(1,8) n;
                """, connection,tx);
            await command.ExecuteNonQueryAsync(); await tx.CommitAsync();
        }
        cs.Options = "-c search_path=" + schema + ",pg_catalog";
        await using (var inspect = new NpgsqlConnection(cs.ConnectionString))
        {
            await inspect.OpenAsync();
            using var probe = new NpgsqlCommand("SELECT current_schema(),(SELECT max(version) FROM app_schema_version),(SELECT count(*) FROM information_schema.tables WHERE table_schema=current_schema() AND table_type='BASE TABLE'),(SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=current_schema() AND NOT t.tgisinternal)",inspect);
            await using var r = await probe.ExecuteReaderAsync(); await r.ReadAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { schema=r.GetString(0),version=r.IsDBNull(1)?(long?)null:r.GetInt64(1),tables=r.GetInt64(2),triggers=r.GetInt64(3) }));
        }
        AppDbContext Db(int actorId=1) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).AddInterceptors(new PostgreSqlCommandInterceptor()).Options,
            new HttpContextAccessor { HttpContext=new DefaultHttpContext { User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actorId.ToString())],"synthetic")) } });
        await PostgreSqlStartup.CheckAsync(cs.ConnectionString);
        using (var db = Db())
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,ngay_tao,ngay_cap_nhat)
                SELECT n,'Synthetic Requester','tenant'||n||'@example.test','090000000'||n,'synthetic-not-a-login','KHACH_THUE','2026-01-01','2026-01-01' FROM generate_series(3,5) n;
                UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1;
                SELECT setval(pg_get_serial_sequence('khach_thue','id'),1,true);
                INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,ngay_tao) VALUES(2,1,'REQUEST',1,20,1000000,2,'2026-01-01');
                INSERT INTO tin_dang(id,phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES(1,2,1,'Synthetic listing','DANG_HIEN_THI','2026-01-01');
                """);
        }
        GuiYeuCauViewModel Request(int people=2) => new() { LoaiYeuCau="XEM_PHONG",NgayMongMuon=new(2026,10,23),SoNguoiDuKien=people,LoiNhan="Synthetic test" };
        using (var db = Db(3))
        {
            var service = new YeuCauThueService(db,new Clock());
            if ((await service.Send(1,3,Request()))?.MaYeuCau!="YC-202610-0001") throw new Exception("First request failed.");
        }
        using (var db = Db(4))
        {
            var service = new YeuCauThueService(db,new Clock());
            try { await service.Send(1,3,Request()); throw new Exception("Duplicate request accepted."); } catch(OpenRequestExistsException) { }
            try { await service.Send(1,4,Request(3)); throw new Exception("Capacity guard missing."); } catch(RoomCapacityException) { }
            if ((await service.Send(1,4,Request()))?.MaYeuCau!="YC-202610-0002") throw new Exception("Counter increment/profile creation failed.");
            await db.Database.ExecuteSqlRawAsync("UPDATE rental_request_counter SET so_cuoi=9999 WHERE thang='202610'");
        }
        using (var db = Db(5))
        {
            try { await new YeuCauThueService(db,new Clock()).Send(1,5,Request()); throw new Exception("Counter limit ignored."); } catch(RequestCodeExhaustedException) { }
        }
        using (var db = Db())
        {
            if (await db.YeuCauThues.CountAsync()!=2 || await db.KhachThues.AnyAsync(x=>x.TaiKhoanId==5)) throw new Exception("Request rollback failed.");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO app_role("Code","Name","SortOrder") VALUES('KHACH_THUE','Synthetic tenant',1);
                INSERT INTO app_module("Code","Name","GroupName","SortOrder") VALUES('BAO_HONG','Synthetic repairs','Synthetic',1);
                INSERT INTO role_permission("RoleCode","ModuleCode","AccessLevel","OwnDataOnly") VALUES('KHACH_THUE','BAO_HONG','WRITE',1);
                """);
            var repairs = new BaoHongService(db,new Clock(),new BaoHongImageStore(Path.Combine(Path.GetTempPath(),schema)));
            BaoHongInput Repair() => new() { HopDongId=1,LoaiThietBi="Synthetic",MoTa="Synthetic test",MucDo="THUONG",KhoaChongTrung=Guid.NewGuid().ToString("N") };
            if ((await repairs.GuiAsync(3,Repair())).MaBaoHong!="BH-202610-0001" || (await repairs.GuiAsync(3,Repair())).MaBaoHong!="BH-202610-0002") throw new Exception("Repair counter failed.");
        }
        Console.WriteLine("Synthetic rental request: insert, increment, duplicate, capacity, exhaustion rollback; repair counter: insert and increment passed. No emails sent.");
        GhiThuHoaDonInput Receipt(int id, long amount, string? key=null) => new() { HoaDonId=id, SoTien=amount, NgayThu=new(2026,10,23), HinhThuc="TIEN_MAT", KhoaChongTrung=key??Guid.NewGuid().ToString("N") };
        using (var db = Db())
        {
            var service = new GhiThuHoaDonService(db,new Clock()); var input=Receipt(1,400000);
            if ((await service.GhiAsync(1,input)).ConPhaiTra!=600000 || (await service.GhiAsync(1,input)).LanThus.Count!=1) throw new InvalidOperationException("Receipt/replay failed.");
            if ((await service.GhiAsync(1,Receipt(1,600000))).ConPhaiTra!=0) throw new InvalidOperationException("Final receipt failed.");
            try { await service.GhiAsync(2,Receipt(2,1)); throw new Exception("Ownership guard missing."); } catch (UnauthorizedAccessException) { }
            var sessions = new SessionVersionStore(db);
            if (sessions.Capture(1,"synthetic-not-a-login") != "0" || !sessions.IsValid(1,"0") || sessions.IsValid(1,"wrong")) throw new InvalidOperationException("Session check failed.");
        }
        async Task<bool> Collect()
        {
            using var db=Db();
            try { await new GhiThuHoaDonService(db,new Clock()).GhiAsync(1,Receipt(2,700000)); return true; }
            catch (GhiThuValidationException) { return false; }
        }
        var races = await Task.WhenAll(Task.Run(Collect),Task.Run(Collect));
        if (races.Count(x=>x)!=1) throw new InvalidOperationException("Concurrent overcollection guard failed.");
        using (var db = Db())
        {
            if ((await new GhiThuHoaDonService(db,new Clock()).ChiTietAsync(1,2)).TongDaThu!=700000) throw new InvalidOperationException();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION synthetic_audit_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.du_lieu_sau::json->>'hoa_don_id'='3' THEN RAISE EXCEPTION USING ERRCODE='23514',MESSAGE='Synthetic fault'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER synthetic_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong FOR EACH ROW EXECUTE FUNCTION synthetic_audit_failure();
                """);
            try { await new GhiThuHoaDonService(db,new Clock()).GhiAsync(1,Receipt(3,100)); throw new Exception("Audit fault ignored."); }
            catch (PostgresException ex) when(ex.SqlState=="23514") { }
        }
        using(var db=Db())
        {
            if(await db.ThanhToans.AnyAsync(x=>x.HoaDonId==3)) throw new InvalidOperationException("Audit failure did not roll back receipt.");
            foreach(var statement in new[]{"UPDATE hoa_don SET tong_tien=1 WHERE id=1","UPDATE nhat_ky_hoat_dong SET hanh_dong='SUA'"})
            {
                try { await db.Database.ExecuteSqlRawAsync(statement); throw new Exception("Immutability guard missing."); }
                catch(PostgresException ex) when(ex.SqlState=="23514") { }
            }
            await db.Database.OpenConnectionAsync();
            await using var transaction=db.Database.GetDbConnection().BeginPortableTransaction();
            await PostgreSqlImageOrdering.ApplyAsync(db.Database.GetDbConnection(),transaction,1,Enumerable.Range(1,8).Reverse().ToArray(),default);
            await transaction.CommitAsync();
        }
        Console.WriteLine(JsonSerializer.Serialize(new { syntheticSchema=schema, receiptCommit=true,replay=true,ownership=true,sessionRevocation=true,concurrentOvercollectionPrevented=true,auditRollback=true,immutableGuards=true,imageReorder=true,personalDataCopied=false }));
    }
}
