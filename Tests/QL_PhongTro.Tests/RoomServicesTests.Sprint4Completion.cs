using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private sealed class FailingInvoiceSender : IQueuedInvoiceEmailSender
    {
        public int Attempts { get; private set; }
        public Task SendIssuedAsync(string email, string subject, string body, string path)
        { Attempts++; throw new InvalidOperationException("Synthetic SMTP failure"); }
    }

    [Fact]
    public async Task RemindersIncludePeriodTotalBalanceAreUniqueAndEmailStopsAfterThreeRetries()
    {
        var clock=CollectionClock(); int due,overdue;
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db);
            due=await AddPublishedTenantInvoiceAsync(db,f.A,"REMIND-DUE",false,8,2026,new(2026,10,15));
            overdue=await AddPublishedTenantInvoiceAsync(db,f.A,"REMIND-OVERDUE",false,9,2026,new(2026,10,12));
            var paid=await AddPublishedTenantInvoiceAsync(db,f.B,"REMIND-PAID",false,8,2026,new(2026,10,15));
            db.ThanhToans.Add(new(){HoaDonId=due,SoTien=250000,NgayThanhToan=new(2026,10,12)});
            db.ThanhToans.Add(new(){HoaDonId=paid,SoTien=1000000,NgayThanhToan=new(2026,10,12)});
            db.ThongBaoHoaDons.RemoveRange(await db.ThongBaoHoaDons.ToListAsync());
            await db.SaveChangesAsync();
        }
        var services=new ServiceCollection(); services.AddScoped(_=>Context());
        using var provider=services.BuildServiceProvider();
        var reminders=new NhacHanService(provider.GetRequiredService<IServiceScopeFactory>(),clock);
        Assert.Equal(2,(await reminders.ChayNhacHanAsync()).Tong);
        Assert.Equal(0,(await reminders.ChayNhacHanAsync()).Tong);
        using(var db=Context())
        {
            var notices=await db.ThongBaoHoaDons.OrderBy(x=>x.Id).ToListAsync();
            Assert.Equal(2,notices.Count);
            var upcoming=Assert.Single(notices,x=>x.HoaDonId==due);
            Assert.Contains("08/2026",upcoming.NoiDung); Assert.Contains("1.000.000",upcoming.NoiDung); Assert.Contains("750.000",upcoming.NoiDung);
            Assert.Contains("15/10/2026",upcoming.NoiDung); Assert.Equal($"/ThongBao/HoaDon/{due}",upcoming.DuongDan);
            // Isolate one queued message to verify the complete retry lifecycle.
            var other=await db.ThongBaoHoaDons.SingleAsync(x=>x.HoaDonId==overdue); other.TrangThaiEmail="DA_HUY"; await db.SaveChangesAsync();
        }
        var sender=new FailingInvoiceSender();
        for(var attempt=1;attempt<=4;attempt++)
        {
            using var db=Context(); var dispatcher=new InvoiceNotificationDispatcher(db,sender,clock);
            Assert.Equal(0,await dispatcher.DispatchAsync());
            var notice=await db.ThongBaoHoaDons.AsNoTracking().SingleAsync(x=>x.HoaDonId==due);
            Assert.Equal(attempt,notice.SoLanGui);
            Assert.Equal(attempt==4?"THAT_BAI":"CHO_GUI",notice.TrangThaiEmail);
            var log=await db.EmailGuiLogs.AsNoTracking().SingleAsync(); Assert.Equal(attempt,log.SoLanThu); Assert.NotNull(log.LoiGanNhat);
            if(attempt<4) clock.UtcNow=notice.LanGuiTiepTheo!.Value;
        }
        using(var db=Context()) { clock.UtcNow=clock.UtcNow.AddDays(1); await new InvoiceNotificationDispatcher(db,sender,clock).DispatchAsync(); }
        Assert.Equal(4,sender.Attempts);
        using var web=BillingWeb(clock);
        using var tenant=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(tenant,"tenant");
        using var unread=JsonDocument.Parse(await tenant.GetStringAsync("/api/thong-bao/chua-doc"));
        Assert.Equal(2,unread.RootElement.GetProperty("count").GetInt32());
        var html=await tenant.GetStringAsync($"/ThongBao/HoaDon/{due}");
        int noticeId; using(var db=Context()) noticeId=(await db.ThongBaoHoaDons.SingleAsync(x=>x.HoaDonId==due)).Id;
        var marked=await tenant.PostAsync($"/api/thong-bao/{noticeId}/da-doc",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=FormValue(html,"__RequestVerificationToken")}));
        Assert.Equal(HttpStatusCode.OK,marked.StatusCode);
        using var after=JsonDocument.Parse(await marked.Content.ReadAsStringAsync());
        Assert.Equal(1,after.RootElement.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task DebtFivePartialPaymentsTwoMultiPeriodOverduesFiltersAndCsvMatch()
    {
        var clock = CollectionClock(); // 13/10 VN, 12/10 UTC
        using (var db = Context())
        {
            var f = await CollectionFixtureAsync(db);
            await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=NULL WHERE tai_khoan_id=3; INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(100,3,'Nguyễn Người Thuê','2026-01-01'); UPDATE hop_dong SET trang_thai='NHAP'; UPDATE hop_dong SET khach_dung_ten_id=100; UPDATE hop_dong SET trang_thai='DANG_HIEU_LUC'");
            var contracts = new List<int> { f.A, f.B };
            for (var i = 2; i < 5; i++)
            {
                var room = await AddRoom(db, "NỢ-" + i);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES({room.Id},{"HD-NO-" + i},{room.Id},100,'DANG_HIEU_LUC',1,'2026-01-01')");
                contracts.Add(room.Id);
            }
            for (var i = 0; i < 5; i++)
            {
                var bill = await AddPublishedTenantInvoiceAsync(db, contracts[i], "DEBT-" + i, false, 1, 2026, new(2026, 10, 1));
                db.ThanhToans.Add(new() { HoaDonId = bill, SoTien = 900000 - i * 100000, NgayThanhToan = new(2026,10,2) });
                db.ThanhToans.Add(new() { HoaDonId = bill, SoTien = 1, NgayThanhToan = new(2026,10,2), TrangThai = "DA_HUY" });
                if (i < 2)
                {
                    var old = await AddPublishedTenantInvoiceAsync(db, contracts[i], "DEBT-OLD-" + i, false, 2, 2026, new(2026,9,1));
                    db.ThanhToans.Add(new() { HoaDonId = old, SoTien = 500000, NgayThanhToan = new(2026,9,2) });
                }
                if (i == 0)
                {
                    db.GiaoDichCocs.Add(new() { Ma="DEPOSIT-DEBT", KhoaChongTrung=Guid.NewGuid().ToString("N"), HopDongId=contracts[i],
                        Loai="THU_COC", HinhThuc="TIEN_MAT", SoTien=50000, NguoiThucHienId=1, NgayGiaoDich=clock.UtcNow });
                    db.GiaoDichCocs.Add(new() { Ma="OFFSET-DEBT", KhoaChongTrung=Guid.NewGuid().ToString("N"), HopDongId=contracts[i], HoaDonId=bill,
                        Loai="CAN_TRU", SoTien=50000, NguoiThucHienId=1, NgayGiaoDich=clock.UtcNow });
                }
            }
            await db.SaveChangesAsync();
            var service = new CongNoService(db, clock);
            var all = await service.LayAsync(1, false, null, null, null, null, null);
            Assert.Equal(5, all.Rows.Count);
            Assert.Equal(7, all.TotalInvoices);
            Assert.Equal(2450000, all.TotalOutstanding);
            Assert.All(all.Rows, x => Assert.Equal("Nguyễn Người Thuê", x.NguoiDungTen));
            Assert.Equal(new long[] {550000,700000,300000,400000,500000}, all.Rows.Select(x=>x.TongConThieu));
            var overdue = await service.LayAsync(1, false, 1, 40, null, null, null);
            Assert.Equal(2, overdue.Rows.Count);
            Assert.All(overdue.Rows, x=>Assert.Equal(42, x.SoNgayQuaHanDaiNhat));
            Assert.All(overdue.Rows, x=>Assert.Equal(2, x.SoHoaDonChuaTraHet));
            Assert.Equal(1250000, overdue.TotalOutstanding);
            var filtered = await service.LayAsync(1, false, 1, 0, 20, 350000, 500000);
            Assert.Equal(900000, filtered.TotalOutstanding);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.LayAsync(1,false,2,null,null,null,null));
            Assert.Empty((await service.LayAsync(2,false,null,null,null,null,null)).Rows);
        }
        using var web = BillingWeb(clock);
        using var owner = web.CreateClient(new() {AllowAutoRedirect=false});
        await Login(owner,"owner");
        const string filter="?toaNhaId=1&quaHanTu=0&quaHanDen=20&tienTu=350000&tienDen=500000";
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync("/CongNo"+filter)).StatusCode);
        var csv = await owner.GetAsync("/CongNo/ExportCsv"+filter);
        Assert.Equal(HttpStatusCode.OK,csv.StatusCode);
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
        var text=Encoding.UTF8.GetString(bytes);
        Assert.Contains("Phòng,Tòa nhà,Người đứng tên",text);
        Assert.Contains("Nguyễn Người Thuê",text);
        Assert.Contains("900000",text);
        Assert.Equal(5,text.Split("\r\n").Length); // header, two rows, total, trailing newline
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/CongNo?toaNhaId=2")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/CongNo/ExportCsv?toaNhaId=2")).StatusCode);
        using var tenant=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(tenant,"tenant");
        foreach(var url in new[]{"/CongNo","/CongNo/ExportCsv","/Modules/TAI_CHINH?section=cong-no"})
            Assert.Equal(HttpStatusCode.Forbidden,(await tenant.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task ConfirmReportedPaymentKeepsOriginalRecordReplaysAndTenantCannotCancel()
    {
        int id,payment;
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db); id=await AddPublishedTenantInvoiceAsync(db,f.A,"CONFIRM-REPORT",false);
            await new BaoCaoThanhToanService(db).GuiAsync(3,id,250000,new(2026,10,1),"TIEN_MAT");
            payment=(await db.ThanhToans.SingleAsync()).Id;
            var service=new GhiThuHoaDonService(db,CollectionClock()); var key=Guid.NewGuid().ToString("N");
            var result=await service.XacNhanBaoCaoAsync(1,payment,key);
            Assert.Equal(250000,result.TongDaThu); Assert.Equal(750000,result.ConPhaiTra);
            Assert.Equal("THANH_TOAN_MOT_PHAN",result.TrangThai);
            await service.XacNhanBaoCaoAsync(1,payment,key);
            Assert.Single(await db.ThanhToans.ToListAsync());
            Assert.Single(await db.LanThuHoaDons.ToListAsync());
            Assert.Equal(new DateOnly(2026,10,1),(await db.ThanhToans.SingleAsync()).NgayThanhToan);
            Assert.False((await new BaoCaoThanhToanService(db).HuyAsync(3,id)).DaHuy);
        }
        using var web=BillingWeb(CollectionClock());
        using var tenant=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(tenant,"tenant");
        var page=WebUtility.HtmlDecode(await tenant.GetStringAsync($"/ThongBao/HoaDon/{id}"));
        Assert.DoesNotContain("đang chờ xác nhận",page,StringComparison.OrdinalIgnoreCase);
        using var owner=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(owner,"owner");
        var missingCsrf=await owner.PostAsync("/HoaDonDichVu/ConfirmPaymentReport",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=id.ToString(),["paymentId"]=payment.ToString(),["khoaChongTrung"]=Guid.NewGuid().ToString("N")}));
        Assert.Equal(HttpStatusCode.BadRequest,missingCsrf.StatusCode);
    }

    [Fact]
    public async Task ConfirmReportRechecksDebtOwnershipAndRollsBackWhenAuditFails()
    {
        int id,payment;
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db); id=await AddPublishedTenantInvoiceAsync(db,f.A,"CONFIRM-ROLLBACK",false);
            await new BaoCaoThanhToanService(db).GuiAsync(3,id,750000,new(2026,10,1),"TIEN_MAT");
            payment=(await db.ThanhToans.SingleAsync()).Id;
        }
        using(var db=Context())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(2,payment,Guid.NewGuid().ToString("N")));
        using(var db=Context())
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_confirmation BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'audit failed'); END;");
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(1,payment,Guid.NewGuid().ToString("N")));
        }
        using(var db=Context())
        {
            Assert.Equal("CHO_XAC_NHAN",(await db.ThanhToans.SingleAsync()).TrangThai); Assert.Empty(await db.LanThuHoaDons.ToListAsync());
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_confirmation");
            await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,500000));
            await Assert.ThrowsAsync<QL_PhongTro.ViewModels.GhiThuValidationException>(()=>new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(1,payment,Guid.NewGuid().ToString("N")));
        }
    }

    [Fact]
    public async Task ConfirmReportHttpRequiresOwnerFinanceWriteAndRejectsEveryOtherRole()
    {
        int invoice,payment;
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db); invoice=await AddPublishedTenantInvoiceAsync(db,f.A,"REPORT-HTTP-RIGHTS",false);
            await new BaoCaoThanhToanService(db).GuiAsync(3,invoice,250000,new(2026,10,1),"TIEN_MAT");
            payment=(await db.ThanhToans.SingleAsync()).Id;
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                SELECT 5,'Manager','manager@example.test','0900000005',mat_khau,'QUAN_LY',1,1,'2026-01-01','2026-01-01' FROM tai_khoan WHERE id=1;
                INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                SELECT 6,'Admin','admin@example.test','0900000006',mat_khau,'ADMIN',1,1,'2026-01-01','2026-01-01' FROM tai_khoan WHERE id=1;
                """);
        }
        using var web=BillingWeb(CollectionClock());
        var fields=new Dictionary<string,string>{["id"]=invoice.ToString(),["paymentId"]=payment.ToString(),["khoaChongTrung"]=Guid.NewGuid().ToString("N")};
        foreach(var login in new[]{"other","tenant","manager","admin"})
        {
            using var client=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(client,login);
            Assert.Equal(HttpStatusCode.Forbidden,(await Post(client,"/HoaDonDichVu/ConfirmPaymentReport",new(fields))).StatusCode);
        }
        using var owner=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(owner,"owner");
        using(var db=Context()) await db.RolePermissions.Where(x=>x.RoleCode=="CHU_NHA" && x.ModuleCode=="TAI_CHINH")
            .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.AccessLevel,"READ"));
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(owner,"/HoaDonDichVu/ConfirmPaymentReport",new(fields))).StatusCode);
        using(var db=Context())
        {
            Assert.Equal("CHO_XAC_NHAN",(await db.ThanhToans.SingleAsync()).TrangThai); Assert.Empty(await db.LanThuHoaDons.ToListAsync());
            await db.RolePermissions.Where(x=>x.RoleCode=="CHU_NHA" && x.ModuleCode=="TAI_CHINH")
                .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.AccessLevel,"FULL"));
        }
        Assert.Equal(HttpStatusCode.Redirect,(await Post(owner,"/HoaDonDichVu/ConfirmPaymentReport",fields)).StatusCode);
        var html=WebUtility.HtmlDecode(await owner.GetStringAsync($"/HoaDonDichVu/Details/{invoice}"));
        Assert.Contains("Đã xác nhận báo thanh toán",html);
        Assert.DoesNotContain("action=\"/HoaDonDichVu/ConfirmPaymentReport\"",html);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReportConfirmationAndCancellationAreSerialized(bool cancel)
    {
        int invoice,payment;
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db); invoice=await AddPublishedTenantInvoiceAsync(db,f.A,"REPORT-RACE",false);
            await new BaoCaoThanhToanService(db).GuiAsync(3,invoice,750000,new(2026,10,1),"TIEN_MAT");
            payment=(await db.ThanhToans.SingleAsync()).Id;
        }
        using var barrier=new Barrier(2);
        async Task<bool> Confirm()=>await Task.Run(async()=>
        {
            using var db=Context(); barrier.SignalAndWait();
            try { await new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(1,payment,Guid.NewGuid().ToString("N"));return true; }
            catch(InvalidOperationException){return false;}
        });
        async Task<bool> Other()=>await Task.Run(async()=>
        {
            using var db=Context(); barrier.SignalAndWait();
            if(cancel)return (await new BaoCaoThanhToanService(db).HuyAsync(3,invoice)).DaHuy;
            await new GhiThuHoaDonService(db,CollectionClock()).XacNhanBaoCaoAsync(1,payment,Guid.NewGuid().ToString("N"));return true;
        });
        var results=await Task.WhenAll(Confirm(),Other());
        Assert.Equal(cancel?1:2,results.Count(x=>x));
        using var verify=Context(); var report=Assert.Single(await verify.ThanhToans.ToListAsync());
        var count=await verify.LanThuHoaDons.CountAsync();
        Assert.Equal(report.TrangThai=="DA_XAC_NHAN"?1:0,count);
        Assert.Contains(report.TrangThai,new[]{"DA_XAC_NHAN","DA_HUY"});
    }

    [Theory]
    [InlineData(35)] [InlineData(36)]
    public async Task Sprint4V37ReconcilesPartialBranchesPreservesPaymentsAndRerunsSafely(int version)
    {
        using(var db=Context())
        {
            var f=await CollectionFixtureAsync(db); var bill=await AddPublishedTenantInvoiceAsync(db,f.A,"SCHEMA-S4",false);
            db.ThanhToans.Add(new(){HoaDonId=bill,SoTien=123456,NgayThanhToan=new(2026,10,1)}); await db.SaveChangesAsync();
        }
        var target=Path.Combine(folder,"branch-"+version+".sqlite");
        using(var source=Open())
        using(var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+target))
        {copy.Open();source.BackupDatabase(copy);}
        string Snapshot()
        {
            using var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+target);copy.Open();
            using var command=copy.CreateCommand();command.CommandText="SELECT id,hoa_don_id,so_tien,ngay_thanh_toan,trang_thai FROM thanh_toan ORDER BY id";
            using var reader=command.ExecuteReader(); var rows=new List<string>();
            while(reader.Read()) rows.Add(string.Join('|',Enumerable.Range(0,reader.FieldCount).Select(reader.GetValue)));
            return string.Join('\n',rows);
        }
        var before=Snapshot();
        using(var copy=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+target))
        {
            copy.Open();using var command=copy.CreateCommand();command.CommandText=$"""
                DROP INDEX ix_hop_dong_report_room_status_return;
                DROP TABLE email_gui_log;
                DROP INDEX ux_thanh_toan_anh_chung_tu_key;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_key;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_content_type;
                ALTER TABLE thanh_toan DROP COLUMN anh_chung_tu_so_byte;
                DELETE FROM app_schema_version WHERE version>=35;
                INSERT INTO app_schema_version(version,applied_at) VALUES({version},'2026-10-15');
                """;command.ExecuteNonQuery();
        }
        var seed=Path.Combine(app,"Data","permissions.seed.json");
        DatabaseUpdates.Update(target,seed); DatabaseUpdates.Check(target);
        Assert.Equal(before,Snapshot());
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(target)));
        DatabaseUpdates.Update(target,seed);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Assert.Equal(hash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(target))));
        Assert.NotEmpty(Directory.GetFiles(folder,"branch-"+version+".sqlite.before-update-*.bak"));
        using var verify=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+target);verify.Open();
        using var check=verify.CreateCommand();check.CommandText="PRAGMA integrity_check";Assert.Equal("ok",check.ExecuteScalar());
        check.CommandText="PRAGMA foreign_key_check";using var fk=check.ExecuteReader();Assert.False(fk.Read());
    }
}
