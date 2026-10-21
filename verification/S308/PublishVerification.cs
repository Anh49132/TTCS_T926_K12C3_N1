using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

internal static class PublishVerification
{
    private sealed class Clock : ITimeProvider { public DateTime UtcNow { get; } = new(2026, 10, 9, 1, 0, 0, DateTimeKind.Utc); }
    private sealed class Sender(Func<Task> verify) : IInvoiceEmailSender {
        public int Calls; public bool Fail;
        public async Task SendIssuedAsync(string email, string subject, string body, string path) {
            await verify(); Calls++;
            if (Fail) throw new InvalidOperationException("Synthetic email failure");
        }
    }
    public static async Task Run(Func<AppDbContext> context, string database, string root, Action<bool, string> check)
    {
        var clock = new Clock();
        int id;
        using (var db = context()) {
            await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=4 WHERE id=1");
            id = await db.HoaDons.Where(x => x.Thang == 12 && x.TrangThai == "NHAP").Select(x => x.Id).SingleAsync();
            check(!await db.ThongBaoHoaDons.AnyAsync(), "drafts create no in-app notifications");
            await db.Database.ExecuteSqlRawAsync("UPDATE hoa_don SET ghi_chu='Retained invoice note' WHERE id={0}",id);
            await db.Database.ExecuteSqlRawAsync("UPDATE chi_tiet_hoa_don SET chi_so_id=123,bao_hong_id=456,so_ngay_tinh_tien=10,so_ngay_trong_thang=31 WHERE hoa_don_id={0}",id);
        }
        async Task<PhatHanhNhapViewModel> Input() {
            using var db = context(); var bill = await db.HoaDons.FindAsync(id);
            return new() { Id = id, PhienBan = bill!.PhienBan, NgayPhatHanh = new(2026, 10, 9), HanThanhToan = new(2026, 10, 16), XacNhan = true };
        }
        async Task Publish(PhatHanhNhapViewModel input, int actor = 1) {
            using var db = context(); await new HoaDonDichVuService(db, new DichVuService(db), clock).PhatHanhNhapAsync(actor, input);
        }
        async Task Reject(PhatHanhNhapViewModel input, string name, int actor = 1) {
            using var db = context();
            var before = JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id));
            var noticeCount = await db.ThongBaoHoaDons.CountAsync();
            var meters = JsonSerializer.Serialize(await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x => x.Id).ToListAsync());
            try { await Publish(input, actor); throw new Exception("Unexpected success: " + name); }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or DbUpdateException) { }
            db.ChangeTracker.Clear();
            check(before == JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id))
                && noticeCount == await db.ThongBaoHoaDons.CountAsync()
                && meters == JsonSerializer.Serialize(await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x => x.Id).ToListAsync()), name + " rollback, no notification");
        }
        var uncheckedInput = await Input(); uncheckedInput.XacNhan = false; await Reject(uncheckedInput, "publish without confirmation");
        var stale = await Input(); stale.PhienBan--; await Reject(stale, "publish stale invoice");
        await Reject(await Input(), "other owner cannot publish", 2);
        var wrongDate = await Input(); wrongDate.HanThanhToan = new(2026, 10, 8); await Reject(wrongDate, "invalid due date");
        using (var db = context()) await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=NULL WHERE id=1");
        await Reject(await Input(), "missing recipient");
        using (var db = context()) {
            await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=4 WHERE id=1");
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_publish_notice BEFORE INSERT ON thong_bao BEGIN SELECT RAISE(ABORT,'synthetic failure'); END");
        }
        await Reject(await Input(), "notification insert failure");
        using (var db = context()) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_publish_notice");
        var accepted = await Input(); await Publish(accepted);
        using (var db = context()) {
            var bill = await db.HoaDons.FindAsync(id);
            check(bill!.TrangThai == "DA_PHAT_HANH" && bill.NgayPhatHanh == clock.UtcNow && bill.NguoiPhatHanhId == 1
                && bill.NgayPhatHanhNghiepVu == accepted.NgayPhatHanh && bill.PhienBan == accepted.PhienBan + 1, "publication status, actor, UTC time and version");
            var notice = await db.ThongBaoHoaDons.SingleAsync();
            check(notice.HoaDonId == id && notice.NguoiNhanId == 4 && notice.TrangThaiEmail == "CHO_GUI", "in-app notice and queued email created after publication");
            check(await db.ChiSoDienNuocs.Where(x => x.TuNgay == new DateOnly(2026, 12, 1)).AllAsync(x => x.DaKhoa && x.ChiSoCuoi == 110), "publication locks original meters without rewriting values");
        }
        await Reject(accepted, "repeated publication");
        var sender = new Sender(async () => { using var db = context(); check((await db.HoaDons.FindAsync(id))!.TrangThai == "DA_PHAT_HANH", "email sees committed publication"); });
        using (var db = context()) check(await new InvoiceNotificationDispatcher(db, sender, clock).DispatchAsync() == 1, "queued email delivered");
        using (var db = context()) {
            check(await new InvoiceNotificationDispatcher(db, sender, clock).DispatchAsync() == 0 && sender.Calls == 1, "email not sent twice on rerun");
            var bill = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == id);
            var edit = new SuaHoaDonNhapViewModel { Id = id, PhienBan = bill.PhienBan,
                ChiSo = bill.ChiTiet.Where(x => x.ChiSoDau.HasValue).Select(x => new ChiSoNhapInput { Id = x.Id, ChiSoDau = 100, ChiSoCuoi = 140 }).ToList(),
                Khoan = [new() { LoaiKhoan = "PHAT_SINH", TenKhoan = "Forbidden change", SoTien = 1000, GhiChu = "Test" }] };
            try { await new HoaDonDichVuService(db, new DichVuService(db)).LuuNhapAsync(1, edit); throw new Exception("Unexpected editable issued invoice"); }
            catch (InvalidOperationException) { check(true, "issued draft edits rejected by service"); }
        }
        using (var db = context()) {
            try { await db.Database.ExecuteSqlRawAsync("UPDATE chi_tiet_hoa_don SET thanh_tien=1 WHERE hoa_don_id={0}", id); throw new Exception("Unexpected mutable line"); }
            catch (SqliteException) { check(true, "issued lines immutable at database level"); }
        }
        using (var db = context()) {
            var january = await db.HoaDons.SingleAsync(x => x.Nam == 2027 && x.Thang == 1);
            await new HoaDonDichVuService(db, new DichVuService(db), clock).PhatHanhNhapAsync(1,
                new() { Id = january.Id, PhienBan = january.PhienBan, XacNhan = true, NgayPhatHanh = new(2026,10,9), HanThanhToan = new(2026,10,16) });
        }
        sender.Fail = true;
        using (var db = context()) {
            check(await new InvoiceNotificationDispatcher(db, sender, clock).DispatchAsync() == 0, "SMTP failure reported as undelivered");
            check(await db.ThongBaoHoaDons.CountAsync(x => x.TrangThaiEmail == "THAT_BAI") == 1
                && await db.HoaDons.CountAsync(x => x.TrangThai == "DA_PHAT_HANH") == 3, "email failure preserves committed publication and in-app notice");
        }
        // Upgrader runs only on an isolated v20 copy. Compare every financial row before/after, and rerun it.
        var copy = database + ".v20-copy.sqlite";
        using (var source = new SqliteConnection("Data Source=" + database)) {
            source.Open(); using var target = new SqliteConnection("Data Source=" + copy); target.Open(); source.BackupDatabase(target);
        }
        QL_PhongTro.Verification.LegacySchemaFixture.RemoveRoomExtensions(copy);
        using (var c = new SqliteConnection("Data Source=" + copy)) {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "DROP TABLE thong_bao; DELETE FROM app_schema_version WHERE version>=21"; cmd.ExecuteNonQuery();
        }
        async Task<string> Snapshot(string path) {
            using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + path).Options);
            return JsonSerializer.Serialize(new { Invoices = await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).OrderBy(x => x.Id).ToListAsync(),
                Readings = await db.ChiSoDienNuocs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Accounts = await db.TaiKhoans.AsNoTracking().OrderBy(x => x.Id).ToListAsync() });
        }
        var snapshot = await Snapshot(copy);
        SqliteConnection.ClearAllPools();
        var sourceHash = SHA256.HashData(await File.ReadAllBytesAsync(database));
        var seed = Path.Combine(root, "QL_PhongTro/Data/permissions.seed.json");
        DatabaseUpdates.Update(copy, seed); DatabaseUpdates.Check(copy);
        check(snapshot == await Snapshot(copy), "v20 to v21 preserves every invoice, line, meter and account");
        DatabaseUpdates.Update(copy, seed);
        check(snapshot == await Snapshot(copy), "v21 updater rerun preserves data");
        SqliteConnection.ClearAllPools();
        var afterHash = SHA256.HashData(await File.ReadAllBytesAsync(database));
        check(sourceHash.SequenceEqual(afterHash), "upgrade leaves source database unchanged");
        using (var c = new SqliteConnection("Data Source=" + copy)) {
            c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA integrity_check";
            check((string?)cmd.ExecuteScalar() == "ok", "v21 integrity"); cmd.CommandText = "PRAGMA foreign_key_check";
            using var reader = cmd.ExecuteReader(); check(!reader.Read(), "v21 foreign keys");
        }
    }
}
