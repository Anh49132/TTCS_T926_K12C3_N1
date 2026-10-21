using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

if (args.Length == 3 && args[0] == "--prepare-demo") { await PrepareDemo.Run(args[1], args[2]); return; }
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var folder = Path.Combine(Path.GetTempPath(), "s308-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var path = Path.Combine(folder, "test.sqlite");
LocalDatabaseInitializer.Create(path, Path.Combine(root, "QL_PhongTro/Data/permissions.seed.json"));
RentalRequestSchema.Initialize(path);
DichVuSchemaInitializer.InitializeInvoices(path);
AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(
    new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString()).Options,
    new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "CHU_NHA")], "test")) } });
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
using (var setup = Context()) {
    await setup.Database.ExecuteSqlRawAsync("""
        INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
        VALUES(1,'Owner','owner@example.test','0900000001','unused','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
              (2,'Other','other@example.test','0900000002','unused','CHU_NHA',1,1,'2026-01-01','2026-01-01'),
              (3,'Manager','manager@example.test','0900000003','unused','QUAN_LY',1,1,'2026-01-01','2026-01-01'),
              (4,'Tenant','tenant@example.test','0900000004','unused','KHACH_THUE',1,1,'2026-01-01','2026-01-01'),
              (5,'Admin','admin@example.test','0900000005','unused','ADMIN',1,1,'2026-01-01','2026-01-01'),
              (6,'Other tenant','tenant2@example.test','0900000006','unused','KHACH_THUE',1,1,'2026-01-01','2026-01-01');
        INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(1,1,'Synthetic building','Test');
        INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
        VALUES(1,1,'TEST',1,20,1000000,1000000,3,'DANG_THUE','2026-01-01');
        INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(1,'Synthetic tenant','2026-01-01');
        INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao)
        VALUES(1,'HD-TEST',1,1,'DANG_HIEU_LUC',1,'2026-01-01');
        INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
        VALUES(1,1,'2026-01-01','2028-12-31',36,1000000,1,'2026-01-01');
        """);
    var hash = BCrypt.Net.BCrypt.HashPassword("S308-Test-a1", 4);
    await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE tai_khoan SET mat_khau={hash}");
}
foreach (var (quantity, price, expected) in new (decimal, long, long)[] {
    (0, 3500, 0), (10, 3500, 35000), (.001m, 3500, 4), (.003m, 3500, 11),
    (1.234m, 3500, 4319), (12.345m, 3500, 43208), (2, 20000, 40000),
    (1, 1000000, 1000000), (.49m, 1, 0), (.5m, 1, 1) })
    Check(HoaDonDichVuService.ThanhTien(quantity, price) == expected, $"round {quantity} x {price} = {expected}");
int id;
using (var db = Context()) {
    id = await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhAsync(1,
        new LapHoaDonDichVuViewModel { ToaNhaId = 1, HopDongId = 1, NgayApDung = new(2026, 10, 1) }, taoNhap: true);
    var invoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == id);
    Check(invoice.TrangThai == "NHAP" && invoice.NgayPhatHanh == null && invoice.NguoiPhatHanhId == null, "new invoice remains draft");
    invoice.ChiTiet.Add(new ChiTietHoaDon { SoThuTu = 2, TenKhoan = "Synthetic meter", CachTinhApDung = "THEO_CHI_SO",
        ChiSoDau = 100, ChiSoCuoi = 110, SoLuong = 10, DonGia = 3500, ThanhTien = 35000 });
    invoice.TongTien += 35000;
    await db.SaveChangesAsync();
}
async Task<SuaHoaDonNhapViewModel> Input() {
    using var db = Context(); var invoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == id);
    return new() { Id = id, PhienBan = invoice.PhienBan,
        ChiSo = invoice.ChiTiet.Where(x => x.ChiSoDau.HasValue).Select(x => new ChiSoNhapInput { Id = x.Id, ChiSoDau = 100, ChiSoCuoi = 120 }).ToList() };
}
async Task Save(SuaHoaDonNhapViewModel model, int actor = 1) {
    using var db = Context(); await new HoaDonDichVuService(db, new DichVuService(db)).LuuNhapAsync(actor, model);
}
async Task Reject(SuaHoaDonNhapViewModel model, string name, int actor = 1) {
    using var before = Context(); var original = await before.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id);
    var count = await before.ChiTietHoaDons.CountAsync(x => x.HoaDonId == id);
    try { await Save(model, actor); throw new Exception("Unexpected successful save: " + name); }
    catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or OverflowException) { }
    using var after = Context(); var current = await after.HoaDons.AsNoTracking().SingleAsync(x => x.Id == id);
    Check(current.TongTien == original.TongTien && current.PhienBan == original.PhienBan
        && count == await after.ChiTietHoaDons.CountAsync(x => x.HoaDonId == id), name + " rollback");
}
var edit = await Input();
edit.Khoan = [new() { LoaiKhoan = "PHAT_SINH", TenKhoan = "Repair", SoTien = 50000, GhiChu = "Agreed repair" },
    new() { LoaiKhoan = "GIAM_TRU", TenKhoan = "Discount", SoTien = 20000, GhiChu = "Agreed discount" }];
await Save(edit);
using (var db = Context()) {
    var invoice = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == id);
    Check(invoice.TongTien == 1100000 && invoice.PhienBan == 1 && invoice.TrangThai == "NHAP", "meter + surcharge - discount total");
    Check(invoice.ChiTiet.Where(x => x.LoaiKhoan == "GIAM_TRU").Single().ThanhTien == 20000, "discount stored positive");
}
await Reject(edit, "stale version");
await Reject(await Input(), "other owner", 2);
foreach (var type in new[] { "PHAT_SINH", "GIAM_TRU" }) {
    var invalid = await Input(); invalid.Khoan = [new() { LoaiKhoan = type, TenKhoan = "Missing note", SoTien = 1000 }];
    await Reject(invalid, type + " missing note");
}
var negative = await Input(); negative.ChiSo[0].ChiSoCuoi = 99; await Reject(negative, "end below start");
var excessive = await Input(); excessive.Khoan = [new() { LoaiKhoan = "GIAM_TRU", TenKhoan = "Too much", SoTien = 2000000, GhiChu = "Test" }];
await Reject(excessive, "negative total");
var overflow = await Input(); overflow.Khoan = [new() { LoaiKhoan = "PHAT_SINH", TenKhoan = "Overflow", SoTien = long.MaxValue, GhiChu = "Test" }];
await Reject(overflow, "overflow");
using (var db = Context()) {
    Check(!await db.HoaDons.AnyAsync(x => x.NgayPhatHanh != null), "draft never published");
    await db.Database.ExecuteSqlRawAsync("UPDATE hoa_don SET trang_thai='DA_PHAT_HANH' WHERE id={0}", id);
}
await Reject(await Input(), "issued invoice immutable");
using (var c = new SqliteConnection("Data Source=" + path)) {
    c.Open(); using var command = c.CreateCommand(); command.CommandText = "PRAGMA integrity_check";
    Check((string?)command.ExecuteScalar() == "ok", "integrity"); command.CommandText = "PRAGMA foreign_key_check";
    using var reader = command.ExecuteReader(); Check(!reader.Read(), "foreign keys");
}
Console.WriteLine("All S3-08 checks passed on fresh temporary DB: " + path);

using (var db = Context()) {
    foreach (var code in new[] { "DIEN", "NUOC" }) {
        var service = new DichVu { MaDichVu = code, TenDichVu = code };
        var catalog = new DichVuToaNha { ToaNhaId = 1, DichVu = service, ApDungMacDinh = true };
        db.DichVuToaNhas.Add(catalog);
        db.CauHinhDichVus.Add(new() { ToaNhaId = 1, DichVu = service, CachTinh = "THEO_CHI_SO", DonViTinh = "unit",
            DonGia = 3500, TuNgay = new(2026, 1, 1), NguoiTaoId = 1, NgayTao = DateTime.UtcNow });
        db.DichVuPhongs.Add(new() { PhongId = 1, DichVuToaNha = catalog });
        await db.SaveChangesAsync();
        foreach (var date in new[] { new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 1) })
            db.ChiSoDienNuocs.Add(new() { HopDongId = 1, DichVuId = service.Id, TuNgay = date, DenNgay = date.AddMonths(1).AddDays(-1),
                ChiSoDau = 100, ChiSoCuoi = 110, NguoiNhapId = 1, NgayNhap = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
    var svc = new HoaDonDichVuService(db, new DichVuService(db));
    var monthlyId = await svc.TaoNhapThangAsync(1, 1, 1, 2026, 12);
    Check((await db.HoaDons.FindAsync(monthlyId))!.TrangThai == "NHAP", "monthly single creates draft");
}
using (var db = Context()) {
    var draft = await db.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Thang == 12);
    await new HoaDonDichVuService(db, new DichVuService(db)).LuuNhapAsync(1, new() { Id = draft.Id, PhienBan = draft.PhienBan,
        ChiSo = draft.ChiTiet.Where(x => x.ChiSoDau.HasValue).Select(x => new ChiSoNhapInput { Id = x.Id, ChiSoDau = 100, ChiSoCuoi = 120 }).ToList() });
    Check(!await db.ChiSoDienNuocs.AnyAsync(x => x.ChiSoCuoi != 110 || x.DaKhoa), "editing draft preserves original meter readings");
}
using (var db = Context()) {
    var result = await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhDanhSachAsync(1, 1, 2027, 1, taoNhap: true);
    Check(result.SoNhap == 1 && result.SoDaPhatHanh == 0 && result.TongTien == 1070000, "monthly batch creates draft with correct total");
    Check(!await db.ChiSoDienNuocs.AnyAsync(x => x.DaKhoa), "draft creation leaves source meters unlocked");
}
using (var db = Context()) {
    var result = await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhDanhSachAsync(1, 1, 2027, 1, taoNhap: true);
    Check(result.SoNhap == 0 && result.SoDaCoHoaDon == 1, "monthly batch repeat does not duplicate draft");
}

// Exercise the real MVC binding, authorization and antiforgery on this same disposable fixture.
int webId;
using (var db = Context()) {
    webId = await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhAsync(1,
        new() { ToaNhaId = 1, HopDongId = 1, NgayApDung = new(2026, 11, 1) }, taoNhap: true);
}
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var url = "http://127.0.0.1:" + port;
var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "QL_PhongTro.dll"), "--contentRoot", Path.Combine(root, "QL_PhongTro"),
    "--environment", "Development", "--urls", url, "--DatabasePath", path, "--DataProtectionKeysPath", Path.Combine(folder, "keys"),
    "--PasswordReset:PickupDirectory", Path.Combine(folder, "mail"), "--Logging:EventLog:LogLevel:Default", "None", "--Logging:LogLevel:Default", "Warning" }) start.ArgumentList.Add(arg);
using var server = Process.Start(start)!;
var output = server.StandardOutput.ReadToEndAsync(); var errors = server.StandardError.ReadToEndAsync();
HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(url) };
string Token(string page) => WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
async Task Login(HttpClient client, string email) {
    var page = await client.GetStringAsync("/Account/Login");
    var result = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
        ["TaiKhoanDangNhap"] = email, ["MatKhau"] = "S308-Test-a1", ["__RequestVerificationToken"] = Token(page) }));
    Check(result.StatusCode == HttpStatusCode.Redirect, "HTTP login " + email);
}
try {
    using var owner = Client();
    var ready = false;
    for (var attempt = 0; attempt < 100; attempt++) {
        try { if ((await owner.GetAsync("/Account/Login")).IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { }
        if (server.HasExited) throw new Exception(await errors);
        await Task.Delay(100);
    }
    Check(ready, "HTTP server ready");
    await Login(owner, "owner@example.test");
    var page = await owner.GetStringAsync("/HoaDonDichVu/Details/" + webId);
    Check(page.Contains("Sửa hóa đơn Nháp"), "draft editor rendered");
    var fields = new Dictionary<string, string> { ["Id"] = webId.ToString(), ["PhienBan"] = "0",
        ["Khoan[0].LoaiKhoan"] = "PHAT_SINH", ["Khoan[0].TenKhoan"] = "HTTP charge", ["Khoan[0].SoTien"] = "10000", ["Khoan[0].GhiChu"] = "HTTP note",
        ["Khoan[1].LoaiKhoan"] = "GIAM_TRU", ["Khoan[1].TenKhoan"] = "", ["Khoan[1].SoTien"] = "", ["Khoan[1].GhiChu"] = "",
        ["__RequestVerificationToken"] = Token(page) };
    var saved = await owner.PostAsync("/HoaDonDichVu/SaveDraft", new FormUrlEncodedContent(fields));
    Check(saved.StatusCode == HttpStatusCode.Redirect, "HTTP save redirect");
    using (var db = Context()) Check((await db.HoaDons.FindAsync(webId))!.TongTien == 1010000, "HTTP binding accepts empty optional adjustment");
    fields["PhienBan"] = "1"; fields["Khoan[0].GhiChu"] = "";
    var missingNote = await owner.PostAsync("/HoaDonDichVu/SaveDraft", new FormUrlEncodedContent(fields));
    using (var db = Context()) Check((await db.HoaDons.FindAsync(webId))!.PhienBan == 1, "HTTP missing note not saved");
    fields.Remove("__RequestVerificationToken");
    var csrf = await owner.PostAsync("/HoaDonDichVu/SaveDraft", new FormUrlEncodedContent(fields));
    Check(csrf.StatusCode == HttpStatusCode.BadRequest, "HTTP missing CSRF rejected");
    using var other = Client(); await Login(other, "other@example.test");
    Check((await other.GetAsync("/HoaDonDichVu/Details/" + webId)).StatusCode == HttpStatusCode.Forbidden, "HTTP other owner cannot view");
    var home = await other.GetStringAsync("/"); fields["__RequestVerificationToken"] = Token(home);
    Check((await other.PostAsync("/HoaDonDichVu/SaveDraft", new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.Forbidden, "HTTP other owner cannot save");
    foreach (var email in new[] { "manager@example.test", "tenant@example.test", "admin@example.test" }) {
        using var denied = Client(); await Login(denied, email);
        Check((await denied.PostAsync("/HoaDonDichVu/SaveDraft", new FormUrlEncodedContent(fields))).StatusCode == HttpStatusCode.Forbidden,
            "HTTP write forbidden for " + email);
    }
    Check(!Directory.Exists(Path.Combine(folder, "mail")) || !Directory.EnumerateFiles(Path.Combine(folder, "mail"), "*", SearchOption.AllDirectories).Any(), "draft sends no email");
    Console.WriteLine("All S3-08 HTTP checks passed.");
} finally {
    if (!server.HasExited) server.Kill(entireProcessTree: true);
    await server.WaitForExitAsync();
}
await PublishVerification.Run(Context, path, root, Check);
await CancelVerification.Run(Context, path, root, Check);
await HistoryVerification.Run(Context, Check);

// A second host verifies publication and the tenant experience against the disposable fixture.
start.ArgumentList.Add("--PasswordReset:From"); start.ArgumentList.Add("noreply@example.test");
start.ArgumentList.Add("--PasswordReset:PublicBaseUrl"); start.ArgumentList.Add(url);
using var publishServer = Process.Start(start)!;
var publishOut = publishServer.StandardOutput.ReadToEndAsync(); var publishError = publishServer.StandardError.ReadToEndAsync();
try {
    using var owner = Client();
    for (var i = 0; i < 100; i++) {
        try { if ((await owner.GetAsync("/Account/Login")).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
        if (publishServer.HasExited) throw new Exception(await publishError);
        await Task.Delay(100);
    }
    await Login(owner, "owner@example.test");
    var detail = await owner.GetStringAsync("/HoaDonDichVu/Details/" + webId);
    var publishFields = new Dictionary<string,string> { ["Id"] = webId.ToString(), ["PhienBan"] = "0", ["XacNhan"] = "true",
        ["NgayPhatHanh"] = "2026-10-09", ["HanThanhToan"] = "2026-10-16", ["__RequestVerificationToken"] = Token(detail) };
    await owner.PostAsync("/HoaDonDichVu/PublishDraft", new FormUrlEncodedContent(publishFields));
    using (var db = Context()) Check((await db.HoaDons.FindAsync(webId))!.TrangThai == "NHAP"
        && !await db.ThongBaoHoaDons.AnyAsync(x => x.HoaDonId == webId), "HTTP stale publication remains draft without notification");
    publishFields.Remove("__RequestVerificationToken");
    Check((await owner.PostAsync("/HoaDonDichVu/PublishDraft", new FormUrlEncodedContent(publishFields))).StatusCode == HttpStatusCode.BadRequest, "HTTP publication CSRF enforced");
    foreach (var email in new[] { "other@example.test", "manager@example.test", "tenant@example.test", "admin@example.test" }) {
        using var denied = Client(); await Login(denied, email);
        var home = await denied.GetStringAsync("/"); publishFields["__RequestVerificationToken"] = Token(home);
        Check((await denied.PostAsync("/HoaDonDichVu/PublishDraft", new FormUrlEncodedContent(publishFields))).StatusCode == HttpStatusCode.Forbidden, "HTTP publication denied for " + email);
    }
    await using var browser = new BrowserVerification(); await browser.Start(folder);
    await browser.Login(url, "owner@example.test");
    int historyInvoiceId;
    using(var db=Context()) historyInvoiceId=await db.HoaDons.Where(x=>x.MaHoaDon=="HD-HISTORY").Select(x=>x.Id).SingleAsync();
    await browser.Navigate(url+"/HoaDonDichVu/Details/"+historyInvoiceId);
    Check((await browser.Evaluate("document.querySelector('#draft-history').innerText.includes('<script>test</script>') && !Array.from(document.scripts).some(x=>x.textContent==='test')")).GetBoolean(),"browser history notes are HTML encoded");
    Check((await browser.Evaluate("document.documentElement.scrollWidth<=innerWidth")).GetBoolean(),"history repeated edits fits 360px");
    await browser.Screenshot(Path.Combine(folder,"draft-history-360.png"));
    await browser.Navigate(url + "/HoaDonDichVu/Details/" + webId);
    var originalHistory=await browser.Evaluate("document.querySelector('#draft-history').innerText");
    await browser.Evaluate("let field=document.querySelector('[name=\"Khoan[0].TenKhoan\"]');field.value='Unsaved change';field.dispatchEvent(new Event('input',{bubbles:true}));true");
    Check((await browser.Evaluate("document.querySelector('#publish-open').disabled && !document.querySelector('#publish-dirty').hidden")).GetBoolean(), "browser unsaved edits block publication");
    await browser.Navigate(url + "/HoaDonDichVu/Details/" + webId);
    await browser.Evaluate("document.querySelector('#publish-open').click();true");
    Check((await browser.Evaluate("document.querySelector('#publish-confirmation').open")).GetBoolean(), "browser publication dialog opens");
    Check((await browser.Evaluate("!document.querySelector('#publish-draft-form').checkValidity()")).GetBoolean(), "browser confirmation checkbox required");
    await browser.Screenshot(Path.Combine(folder, "publish-confirmation-360.png"));
    await browser.Evaluate("document.querySelector('#publish-confirmation [name=XacNhan]').checked=true;document.querySelector('#publish-draft-form').requestSubmit();true");
    await browser.Wait("!document.querySelector('#draft-editor') && document.body.innerText.includes('Thời điểm phát hành') && document.readyState==='complete'");
    Check((await browser.Evaluate("!document.querySelector('#publish-open') && document.body.innerText.includes('Đã phát hành')")).GetBoolean(), "browser published invoice readonly with publication time");
    Check((await browser.Evaluate("document.documentElement.scrollWidth<=innerWidth")).GetBoolean(), "owner invoice fits 360px");
    await browser.Screenshot(Path.Combine(folder, "published-owner-360.png"));
    await browser.ClearCookies(); await browser.Login(url, "tenant@example.test");
    await browser.Navigate(url + "/ThongBao");
    Check((await browser.Evaluate("document.body.innerText.includes('Hóa đơn phòng TEST')")).GetBoolean(), "tenant receives in-app invoice notification");
    await browser.Navigate(url + "/ThongBao/HoaDon/" + webId);
    Check((await browser.Evaluate("!document.querySelector('#draft-editor') && !document.querySelector('#publish-open') && document.body.innerText.includes('Đã phát hành')")).GetBoolean(), "tenant can view only published readonly invoice");
    Check((await browser.Evaluate("document.documentElement.scrollWidth<=innerWidth")).GetBoolean(), "tenant invoice fits 360px");
    await browser.Screenshot(Path.Combine(folder, "published-tenant-360.png"));
    var delivered = false;
    for (var i = 0; i < 60; i++) {
        using var db = Context();
        if (await db.ThongBaoHoaDons.AnyAsync(x => x.HoaDonId == webId && x.TrangThaiEmail == "DA_GUI")) { delivered = true; break; }
        await Task.Delay(100);
    }
    Check(delivered && Directory.EnumerateFiles(Path.Combine(folder, "mail")).Any(), "real worker writes email pickup after publication");
    using var tenant = Client(); await Login(tenant, "tenant@example.test");
    var notices = await tenant.GetStringAsync("/ThongBao");
    int noticeId;
    using (var db = Context()) noticeId = await db.ThongBaoHoaDons.Where(x => x.HoaDonId == webId).Select(x => x.Id).SingleAsync();
    Check((await tenant.PostAsync("/ThongBao/Read", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = noticeId.ToString() }))).StatusCode == HttpStatusCode.BadRequest, "mark-read CSRF enforced");
    await tenant.PostAsync("/ThongBao/Read", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = noticeId.ToString(), ["__RequestVerificationToken"] = Token(notices) }));
    using (var db = Context()) Check((await db.ThongBaoHoaDons.FindAsync(noticeId))!.NgayDoc.HasValue, "tenant marks own notification read");
    using var otherTenant = Client(); await Login(otherTenant, "tenant2@example.test");
    var otherNotices = await otherTenant.GetStringAsync("/ThongBao");
    Check(!otherNotices.Contains("Hóa đơn phòng TEST"), "other tenant cannot list notification");
    Check((await otherTenant.GetAsync("/ThongBao/HoaDon/" + webId)).StatusCode == HttpStatusCode.Forbidden, "other tenant cannot view invoice");
    Check((await otherTenant.PostAsync("/ThongBao/Read", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = noticeId.ToString(), ["__RequestVerificationToken"] = Token(otherNotices) }))).StatusCode == HttpStatusCode.Forbidden, "other tenant cannot mark notification read");
    // Exercise cancellation, preserved history and reissue in the real MVC/browser flow.
    int issuedVersion; long issuedTotal;
    using (var db = Context()) { var bill = await db.HoaDons.FindAsync(webId); issuedVersion=bill!.PhienBan; issuedTotal=bill.TongTien; }
    var cancellationPage = await owner.GetStringAsync("/HoaDonDichVu/Details/"+webId);
    var cancelFields = new Dictionary<string,string> { ["Id"]=webId.ToString(),["PhienBan"]=issuedVersion.ToString(),["LyDo"]="   ",["XacNhan"]="true",["__RequestVerificationToken"]=Token(cancellationPage) };
    await owner.PostAsync("/HoaDonDichVu/CancelInvoice",new FormUrlEncodedContent(cancelFields));
    using (var db=Context()) Check((await db.HoaDons.FindAsync(webId))!.TrangThai=="DA_PHAT_HANH","HTTP blank cancellation reason rejected");
    cancelFields["LyDo"]="Browser correction";cancelFields["PhienBan"]=(issuedVersion-1).ToString();
    await owner.PostAsync("/HoaDonDichVu/CancelInvoice",new FormUrlEncodedContent(cancelFields));
    using (var db=Context()) Check((await db.HoaDons.FindAsync(webId))!.TrangThai=="DA_PHAT_HANH","HTTP stale cancellation rejected");
    cancelFields.Remove("__RequestVerificationToken");
    Check((await owner.PostAsync("/HoaDonDichVu/CancelInvoice",new FormUrlEncodedContent(cancelFields))).StatusCode==HttpStatusCode.BadRequest,"cancellation CSRF enforced");
    Check((await owner.PostAsync("/HoaDonDichVu/CreateReplacement",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=webId.ToString(),["phienBan"]=issuedVersion.ToString()}))).StatusCode==HttpStatusCode.BadRequest,"replacement CSRF enforced");
    foreach(var email in new[]{"other@example.test","manager@example.test","tenant@example.test","admin@example.test"}) {
        using var denied=Client();await Login(denied,email);cancelFields["__RequestVerificationToken"]=Token(await denied.GetStringAsync("/"));
        foreach(var action in new[]{"CancelInvoice","CreateReplacement"})
            Check((await denied.PostAsync("/HoaDonDichVu/"+action,new FormUrlEncodedContent(cancelFields))).StatusCode==HttpStatusCode.Forbidden,"HTTP "+action+" denied for "+email);
    }
    await browser.ClearCookies();await browser.Login(url,"owner@example.test");await browser.Navigate(url+"/HoaDonDichVu/Details/"+webId);
    await browser.Evaluate("document.querySelector('#cancel-open').click();true");
    Check((await browser.Evaluate("document.querySelector('#cancel-confirmation').open && !document.querySelector('#cancel-invoice-form').checkValidity()")).GetBoolean(),"browser cancellation requires reason and confirmation");
    await browser.Screenshot(Path.Combine(folder,"cancel-confirmation-360.png"));
    await browser.Evaluate("document.querySelector('#cancel-reason').value='Ghi nhầm khoản phí';document.querySelector('#cancel-invoice-form [name=XacNhan]').checked=true;document.querySelector('#cancel-invoice-form').requestSubmit();true");
    await browser.Wait("!!document.querySelector('#cancelled-invoice') && !document.querySelector('#cancel-open') && document.readyState==='complete'");
    Check((await browser.Evaluate("document.body.innerText.includes('Ghi nhầm khoản phí') && !document.querySelector('#draft-editor')")).GetBoolean(),"browser cancelled invoice keeps reason and locks content");
    Check((await browser.Evaluate("document.querySelector('#draft-history').innerText")).GetString()==originalHistory.GetString(),"browser original history unchanged after publication and cancellation");
    Check((await browser.Evaluate("document.documentElement.scrollWidth<=innerWidth")).GetBoolean(),"cancelled owner invoice fits 360px");
    await browser.Screenshot(Path.Combine(folder,"cancelled-owner-360.png"));
    Check((await tenant.GetStringAsync("/ThongBao/HoaDon/"+webId)).Contains("Hóa đơn đã hủy"),"tenant retains access to cancelled original");
    Check((await tenant.GetStringAsync("/ThongBao")).Contains("Hóa đơn này đã hủy"),"tenant notification list labels cancelled invoice");
    await browser.Evaluate("document.querySelector('#create-replacement').click();true");
    await browser.Wait("!!document.querySelector('#draft-editor') && !document.querySelector('#cancelled-invoice') && document.readyState==='complete'");
    var replacementWebId=int.Parse((await browser.Evaluate("location.pathname.split('/').pop()")).GetString()!);
    string replacementCode;
    using(var db=Context()) { var bill=await db.HoaDons.FindAsync(replacementWebId);replacementCode=bill!.MaHoaDon;
        Check(bill.ThayTheHoaDonId==webId && bill.TongTien==issuedTotal && !await db.ThongBaoHoaDons.AnyAsync(x=>x.HoaDonId==replacementWebId),"browser creates linked replacement draft without notification"); }
    Check((await tenant.GetAsync("/ThongBao/HoaDon/"+replacementWebId)).StatusCode==HttpStatusCode.Forbidden,"tenant cannot open unpublished replacement");
    Check(!(await tenant.GetStringAsync("/ThongBao/HoaDon/"+webId)).Contains(replacementCode),"cancelled tenant view does not expose draft replacement link");
    await browser.Evaluate("document.querySelector('[name=\"Khoan[0].TenKhoan\"]').value='Điều chỉnh bản thay thế';document.querySelector('[name=\"Khoan[0].SoTien\"]').value='1000';document.querySelector('[name=\"Khoan[0].GhiChu\"]').value='Sửa khoản phí';document.querySelector('#draft-editor').requestSubmit();true");
    await browser.Wait("document.querySelector('#draft-editor [name=PhienBan]')?.value==='1' && document.readyState==='complete'");
    using(var db=Context()) Check((await db.HoaDons.FindAsync(replacementWebId))!.TongTien==issuedTotal+1000 && (await db.HoaDons.FindAsync(webId))!.TongTien==issuedTotal,"browser edit recalculates replacement and preserves cancelled total");
    Check((await browser.Evaluate("!!document.querySelector('#draft-history') && document.querySelector('#draft-history').innerText.includes('Sửa khoản phí') && document.querySelector('#draft-history').innerText.includes('Chưa có')")).GetBoolean(),"browser history renders before after and adjustment notes");
    var replacementHistory = await browser.Evaluate("document.querySelector('#draft-history').innerText");
    await browser.Evaluate("document.querySelector('#publish-open').click();document.querySelector('#publish-draft-form [name=XacNhan]').checked=true;document.querySelector('#publish-draft-form').requestSubmit();true");
    await browser.Wait("!document.querySelector('#draft-editor') && !!document.querySelector('#cancel-open') && document.readyState==='complete'");
    using(var db=Context()) Check((await db.HoaDons.FindAsync(replacementWebId))!.TrangThai=="DA_PHAT_HANH" && await db.ThongBaoHoaDons.CountAsync(x=>x.HoaDonId==replacementWebId)==1,"browser replacement reissued and tenant notified once");
    Check((await browser.Evaluate("document.querySelector('#draft-history').innerText")).GetString()==replacementHistory.GetString(),"browser history unchanged after publication");
    var oldTenantView=await tenant.GetStringAsync("/ThongBao/HoaDon/"+webId);
    Check(oldTenantView.Contains("/ThongBao/HoaDon/"+replacementWebId),"tenant sees published replacement link from cancelled original");
    await browser.ClearCookies();await browser.Login(url,"tenant@example.test");await browser.Navigate(url+"/ThongBao/HoaDon/"+replacementWebId);
    Check((await browser.Evaluate("!document.querySelector('#draft-editor') && !document.querySelector('#cancel-open') && document.body.innerText.includes('Thay thế bản đã hủy')")).GetBoolean(),"tenant replacement is readonly and links back to original");
    Check((await browser.Evaluate("!document.querySelector('#draft-history')")).GetBoolean(),"tenant cannot access private draft edit history");
    Check((await browser.Evaluate("document.documentElement.scrollWidth<=innerWidth")).GetBoolean(),"tenant replacement fits 360px");
    await browser.Screenshot(Path.Combine(folder,"replacement-tenant-360.png"));
    Check((await otherTenant.GetAsync("/ThongBao/HoaDon/"+replacementWebId)).StatusCode==HttpStatusCode.Forbidden,"other tenant cannot access replacement");
    using (var db = Context()) await db.RolePermissions.Where(x => x.RoleCode == "KHACH_THUE" && x.ModuleCode == "TAI_CHINH")
        .ExecuteUpdateAsync(set => set.SetProperty(x => x.AccessLevel, "NONE"));
    var revokedHome = await tenant.GetAsync("/");
    Check(revokedHome.IsSuccessStatusCode && !(await revokedHome.Content.ReadAsStringAsync()).Contains("Thông báo hóa đơn"), "revoked finance permission hides tenant notification menu");
    Check((await tenant.GetAsync("/ThongBao")).StatusCode == HttpStatusCode.Forbidden, "revoked finance permission blocks notifications");
    Check((await tenant.GetAsync("/ThongBao/HoaDon/" + webId)).StatusCode == HttpStatusCode.Forbidden, "revoked finance permission blocks invoice access");
    Console.WriteLine("Browser screenshots: " + folder);
} finally {
    if (!publishServer.HasExited) publishServer.Kill(entireProcessTree: true);
    await publishServer.WaitForExitAsync();
    var publicationLog = await publishOut; var publicationErrors = await publishError;
    if (publicationLog.Contains("fail:") || publicationErrors.Length > 0) { Console.WriteLine(publicationLog); Console.WriteLine(publicationErrors); }
}

// Optional S1-09 invoice module is absent on a valid base-only database.
var baseOnly = Path.Combine(folder, "base-only.sqlite");
LocalDatabaseInitializer.Create(baseOnly, Path.Combine(root, "QL_PhongTro/Data/permissions.seed.json"));
using (var baseDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + baseOnly).Options)) {
    var passwordHash = BCrypt.Net.BCrypt.HashPassword("S308-Test-a1", 4);
    await baseDb.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat) VALUES('Base tenant','base@example.test','0900000099',{passwordHash},'KHACH_THUE',1,1,'2026-01-01','2026-01-01')");
    Check(!await InvoiceNotificationDispatcher.IsInstalledAsync(baseDb), "base database has no optional invoice module");
}
start.ArgumentList[start.ArgumentList.IndexOf("--DatabasePath") + 1] = baseOnly;
using var baseServer = Process.Start(start)!;
var baseOut = baseServer.StandardOutput.ReadToEndAsync(); var baseError = baseServer.StandardError.ReadToEndAsync();
try {
    using var client = Client();
    for (var i = 0; i < 100; i++) {
        try { if ((await client.GetAsync("/Account/Login")).IsSuccessStatusCode) break; } catch (HttpRequestException) { }
        if (baseServer.HasExited) throw new Exception(await baseError);
        await Task.Delay(100);
    }
    await Login(client, "base@example.test");
    var home = await client.GetAsync("/");
    Check(home.IsSuccessStatusCode && !(await home.Content.ReadAsStringAsync()).Contains("Thông báo hóa đơn"), "tenant homepage works without optional invoice tables");
    Check((await client.GetAsync("/ThongBao")).StatusCode == HttpStatusCode.NotFound, "missing invoice module returns 404 instead of 500");
    using var baseDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + baseOnly).Options);
    Check(await new InvoiceNotificationDispatcher(baseDb, new PasswordEmailSender(Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions()),
        null!), new SystemTimeProvider()).DispatchAsync() == 0, "email worker skips missing invoice module");
} finally {
    if (!baseServer.HasExited) baseServer.Kill(entireProcessTree: true);
    await baseServer.WaitForExitAsync();
}
