using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var appDir = Path.Combine(root, "QL_PhongTro");
var source = Path.Combine(appDir, "Data", "local-dev.sqlite");
var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
var runDir = Path.Combine(root, "data", "S1-09-verification", DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
Directory.CreateDirectory(runDir);
var database = Path.Combine(runDir, "test.sqlite");
using (var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly }.ToString()))
using (var copy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ToString()))
{ original.Open(); copy.Open(); original.BackupDatabase(copy); }
int assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    assertions++;
    Console.WriteLine("PASS: " + message);
}
Process? server = null;
try
{
    // The existing repository DB is incomplete. Initialize prerequisites on the disposable copy only.
    AuthSchemaInitializer.Initialize(database);
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(new SqliteConnectionStringBuilder
        { DataSource = database, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true }.ToString()).Options;
    await using var db = new AppDbContext(options);
    RoomSchemaInitializer.EnsureSchema(db);
    PermissionSchemaInitializer.Initialize(database, Path.Combine(appDir, "Data", "permissions.seed.json"));
    PasswordSchemaInitializer.Initialize(database);
    var service = new DichVuService(db);
    Check(!await service.SanSangAsync(), "missing service schema detected without creating tables");
    var oldAccounts = await db.TaiKhoans.CountAsync();
    DichVuSchemaInitializer.Initialize(database);
    Check(await service.SanSangAsync(), "explicit initializer installs service tables");
    Check(await db.TaiKhoans.CountAsync() == oldAccounts, "initializer preserves existing accounts");
    Check(Directory.GetFiles(runDir, "*.before-s109-*.bak").Length == 1, "initializer creates backup");
    try { DichVuSchemaInitializer.Initialize(database); throw new Exception("Initializer overwrote schema"); }
    catch (InvalidOperationException) { Check(true, "initializer refuses existing service tables"); }

    var password = "S109-test-" + Guid.NewGuid().ToString("N");
    var hash = BCrypt.Net.BCrypt.HashPassword(password);
    var accounts = new List<TaiKhoan>();
    foreach (var role in new[] { "CHU_NHA", "CHU_NHA", "KHACH_THUE", "QUAN_LY", "ADMIN" })
    {
        var account = new TaiKhoan { HoTen = "S109 " + role, Email = Guid.NewGuid().ToString("N") + "@example.invalid",
            SoDienThoai = "0" + Random.Shared.NextInt64(100000000, 999999999), MatKhau = hash,
            VaiTro = role, DangHoatDong = true, NgayTao = DateTime.UtcNow, NgayCapNhat = DateTime.UtcNow };
        accounts.Add(account); db.TaiKhoans.Add(account);
    }
    await db.SaveChangesAsync();
    var building = new ToaNha { ChuNhaId = accounts[0].Id, TenToaNha = "Tòa kiểm thử AC1", DiaChi = "Dữ liệu kiểm thử" };
    db.ToaNhas.Add(building);
    await db.SaveChangesAsync();
    if (args.Contains("--prepare-demo"))
    {
        await DemoPreparation.PrepareAsync(db, root, database, runDir, accounts[0], building, password);
        return;
    }
    var cases = new[] { ("Điện kiểm thử", CachTinhDichVu.TheoChiSo, "kWh", 3500L), ("Nước khoán", CachTinhDichVu.TheoNguoi, "người/tháng", 80000L), ("Internet", CachTinhDichVu.CoDinh, "phòng/tháng", 120000L) };
    foreach (var (name, method, unit, price) in cases)
    {
        var id = await service.ThemAsync(accounts[0].Id, new TaoDichVuViewModel
            { ToaNhaId = building.Id, TenDichVu = " " + name + " ", CachTinh = method, DonViTinh = unit, DonGia = price });
        var row = await db.CauHinhDichVus.AsNoTracking().SingleAsync(x => x.Id == id);
        var found = await service.LayDonGiaAsync(accounts[0].Id, building.Id, row.DichVuId, DichVuService.HomNay());
        Check(found is not null && found.DonGia == price && found.CachTinh == method && found.DonViTinh == unit && found.TenDichVu == name, "create and retrieve " + method);
        Check(await service.LayDonGiaAsync(accounts[0].Id, building.Id, row.DichVuId, DichVuService.HomNay().AddDays(-1)) is null, "no price before creation date " + method);
    }
    Check((await service.DanhSachAsync(accounts[0].Id, building.Id)).Count == 3, "list contains all three saved services");
    try { await service.DanhSachAsync(accounts[1].Id, building.Id); throw new Exception("Cross-owner access allowed"); }
    catch (UnauthorizedAccessException) { Check(true, "service denies another owner's building"); }
    foreach (var field in new[] { "name", "unit", "method", "price", "negative", "building" })
    {
        var model = new TaoDichVuViewModel { ToaNhaId = building.Id, TenDichVu = "Valid", CachTinh = "THEO_CHI_SO", DonViTinh = "kWh", DonGia = 1000 };
        switch (field) { case "name": model.TenDichVu = "  "; break; case "unit": model.DonViTinh = "  "; break;
            case "method": model.CachTinh = "INVALID"; break; case "price": model.DonGia = null; break;
            case "negative": model.DonGia = -1; break; case "building": model.ToaNhaId = null; break; }
        try { await service.ThemAsync(accounts[0].Id, model); throw new Exception("Invalid model saved"); }
        catch (ValidationException) { Check(true, "validation rejects " + field); }
    }
    Check(await db.CauHinhDichVus.CountAsync() == 3 && await db.DichVus.CountAsync() == 3, "invalid submissions leave no orphan catalog rows");
    try { await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET don_gia=-1"); throw new Exception("Negative SQL price accepted"); }
    catch (SqliteException e) when (e.SqliteErrorCode == 19) { Check(true, "database rejects negative prices"); }

    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var url = "http://127.0.0.1:" + port;
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = appDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(Path.Combine(appDir, "bin", "Debug", "net10.0", "QL_PhongTro.dll"));
    start.ArgumentList.Add("--DatabasePath=" + database); start.ArgumentList.Add("--urls=" + url);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["LOCALAPPDATA"] = runDir;
    var log = new System.Text.StringBuilder();
    server = Process.Start(start) ?? throw new Exception("Could not start test application");
    server.OutputDataReceived += (_, e) => { lock (log) log.AppendLine(e.Data); };
    server.ErrorDataReceived += (_, e) => { lock (log) log.AppendLine(e.Data); };
    server.BeginOutputReadLine(); server.BeginErrorReadLine();
    HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(url) };
    using var anonymous = Client();
    var started = false;
    for (var attempt = 0; attempt < 60; attempt++)
    {
        if (server.HasExited) throw new Exception("Test server exited: " + log);
        try { using var response = await anonymous.GetAsync("/Account/Login"); if (response.StatusCode == HttpStatusCode.OK) { started = true; break; } }
        catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    Check(started, "isolated HTTP application starts");
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    async Task<HttpClient> Login(TaiKhoan account)
    {
        var client = Client();
        var html = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["Identifier"] = account.Email, ["Password"] = password, ["__RequestVerificationToken"] = Token(html) }));
        Check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString() == "/", "cookie login " + account.VaiTro);
        return client;
    }
    using var owner = await Login(accounts[0]);
    using var unauthenticated = await anonymous.GetAsync("/DichVu");
    Check(unauthenticated.StatusCode == HttpStatusCode.Redirect && unauthenticated.Headers.Location!.ToString().Contains("/Account/Login"), "anonymous user redirected to login");
    var list = WebUtility.HtmlDecode(await owner.GetStringAsync("/DichVu?toaNhaId=" + building.Id));
    Check(cases.All(x => list.Contains(x.Item1)) && list.Contains("3.500") && list.Contains("80.000") && list.Contains("120.000"), "Razor list renders names and Vietnamese prices");
    foreach (var account in accounts.Skip(1))
    {
        using var other = await Login(account);
        using var denied = await other.GetAsync("/DichVu?toaNhaId=" + building.Id);
        Check(denied.StatusCode == HttpStatusCode.Redirect && denied.Headers.Location!.ToString().Contains("AccessDenied"), "HTTP denies non-owner " + account.VaiTro);
    }
    var form = await owner.GetStringAsync("/DichVu/Create?toaNhaId=" + building.Id);
    Dictionary<string,string> Fields(string price = "2500") => new()
    {
        ["ToaNhaId"] = building.Id.ToString(), ["TenDichVu"] = "Dịch vụ HTTP", ["CachTinh"] = "THEO_CHI_SO",
        ["DonViTinh"] = "m³", ["DonGia"] = price, ["__RequestVerificationToken"] = Token(form)
    };
    var noCsrf = Fields(); noCsrf.Remove("__RequestVerificationToken");
    using var csrf = await owner.PostAsync("/DichVu/Create", new FormUrlEncodedContent(noCsrf));
    Check(csrf.StatusCode == HttpStatusCode.BadRequest, "POST requires anti-forgery token");
    foreach (var badPrice in new[] { "-1", "", "1.5", "9223372036854775808", "abc" })
    {
        using var invalid = await owner.PostAsync("/DichVu/Create", new FormUrlEncodedContent(Fields(badPrice)));
        Check(invalid.StatusCode == HttpStatusCode.OK && (await invalid.Content.ReadAsStringAsync()).Contains("validation-summary-errors"), "HTTP rejects price '" + badPrice + "'");
    }
    foreach (var name in new[] { "TenDichVu", "DonViTinh", "CachTinh", "ToaNhaId" })
    {
        var fields = Fields(); fields[name] = "";
        using var invalid = await owner.PostAsync("/DichVu/Create", new FormUrlEncodedContent(fields));
        Check(invalid.StatusCode == HttpStatusCode.OK && (await invalid.Content.ReadAsStringAsync()).Contains("validation-summary-errors"), "HTTP requires " + name);
    }
    foreach (var method in new[] { "THEO_CHI_SO", "THEO_NGUOI", "CO_DINH" })
    {
        var fields = Fields("0"); fields["CachTinh"] = method; fields["TenDichVu"] = "HTTP " + method;
        fields["DonViTinh"] = method == "THEO_CHI_SO" ? "kWh" : method == "THEO_NGUOI" ? "người/tháng" : "phòng/tháng";
        using var saved = await owner.PostAsync("/DichVu/Create", new FormUrlEncodedContent(fields));
        Check(saved.StatusCode == HttpStatusCode.Redirect && saved.Headers.Location!.ToString().StartsWith("/DichVu"), "HTTP saves " + method + " with zero price");
    }
    Check(await db.CauHinhDichVus.CountAsync() == 6, "only valid HTTP submissions persisted");
    await ManagementChecks.RunAsync(db, options, root, database, accounts[0].Id, accounts[1].Id, building.Id, Check);
    var managedList = WebUtility.HtmlDecode(await owner.GetStringAsync("/DichVu?toaNhaId=" + building.Id));
    Check(managedList.Contains("Mặc định") && managedList.Contains("Có thay đổi đã lên lịch"), "Razor list identifies defaults and scheduled changes");
    var defaultElectricity = await db.DichVus.SingleAsync(x => x.MaDichVu == "DIEN");
    var historyPage = await owner.GetStringAsync($"/DichVu/Manage?toaNhaId={building.Id}&dichVuId={defaultElectricity.Id}");
    Check(WebUtility.HtmlDecode(historyPage).Contains("Lịch sử đơn giá và trạng thái"), "Razor management history renders");
    using (var missingCsrf = await owner.PostAsync("/DichVu/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["toaNhaId"] = building.Id.ToString(), ["dichVuId"] = defaultElectricity.Id.ToString() })))
        Check(missingCsrf.StatusCode == HttpStatusCode.BadRequest, "deletion endpoint requires anti-forgery");
    using (var referenced = await owner.PostAsync("/DichVu/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["toaNhaId"] = building.Id.ToString(), ["dichVuId"] = defaultElectricity.Id.ToString(), ["__RequestVerificationToken"] = Token(historyPage) })))
        Check(referenced.StatusCode == HttpStatusCode.Redirect, "referenced delete returns explanatory list redirect");
    Check(WebUtility.HtmlDecode(await owner.GetStringAsync("/DichVu?toaNhaId=" + building.Id)).Contains("đã được sử dụng trong hóa đơn"), "referenced delete displays useful message");
    var invoiceList = WebUtility.HtmlDecode(await owner.GetStringAsync("/HoaDonDichVu?toaNhaId=" + building.Id));
    Check(invoiceList.Contains("Lập hóa đơn tiền phòng và dịch vụ") && invoiceList.Contains("Hóa đơn gần đây"), "invoice form and saved list render");
    using var integrityConnection = new SqliteConnection(db.Database.GetConnectionString()); integrityConnection.Open();
    using var integrity = integrityConnection.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check";
    Check((string?)integrity.ExecuteScalar() == "ok", "test database integrity_check=ok");
    integrity.CommandText = "PRAGMA foreign_key_check";
    using (var reader = integrity.ExecuteReader()) Check(!reader.Read(), "test database foreign_key_check clean");
    Console.WriteLine($"SUCCESS: {assertions} assertions. Test artifacts: {runDir}");
}
finally
{
    if (server is { HasExited: false }) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    server?.Dispose();
    var after = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
    if (after != sourceHash) throw new Exception("Original database changed during verification");
    Console.WriteLine("Original SQLite SHA-256 unchanged: " + after);
}
