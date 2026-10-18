using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using QL_PhongTro.Data;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "s104-" + Guid.NewGuid().ToString("N"));
    private readonly string database;
    private readonly string appPath;
    private readonly WebApplicationFactory<Program> factory;
    private readonly Dictionary<string, int> accounts = [];
    private readonly string[] roles = ["KHACH_THUE", "CHU_NHA", "QUAN_LY", "ADMIN"];
    private readonly string[] modules = ["TAI_KHOAN","PHONG_TRO","TIN_DANG","YEU_CAU_THUE","HOP_DONG","DIEN_NUOC","TAI_CHINH","BAO_HONG","BAO_CAO"];
    // Independent expected matrix from the confirmed PO decision, not from seed JSON.
    private readonly string[][] levels = [
        ["READ","READ","NONE","FULL"],
        ["READ","FULL","READ","READ"],
        ["READ","FULL","WRITE","READ"],
        ["WRITE","FULL","READ","READ"],
        ["READ","FULL","READ","READ"],
        ["READ","FULL","WRITE","READ"],
        ["READ","FULL","NONE","READ"],
        ["WRITE","FULL","WRITE","READ"],
        ["NONE","FULL","READ","READ"]
    ];


    public PermissionTests()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        appPath = Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
        Directory.CreateDirectory(temp);
        database = Path.Combine(temp, "test.sqlite");
        LocalDatabaseInitializer.Create(database, Path.Combine(appPath, "Data", "permissions.seed.json"));
        // Run all initializers to ensure schema is complete
        AuthSchemaInitializer.Initialize(database);
        PasswordSchemaInitializer.Initialize(database);
        // Reset only permission tables in the disposable copy to exercise a fresh initialization.
        Execute("DROP TABLE IF EXISTS role_permission; DROP TABLE IF EXISTS app_module; DROP TABLE IF EXISTS app_role;");
        var before = SnapshotBusinessData();
        PermissionSchemaInitializer.Initialize(database, Path.Combine(appPath, "Data", "permissions.seed.json"));
        Assert.Equal(before, SnapshotBusinessData());
        // The app refuses to start below the current schema version; the disposable copy must
        // reach the same version a deployed database has.
        DatabaseUpdates.Update(database, Path.Combine(appPath, "Data", "permissions.seed.json"));
        var hash = BCrypt.Net.BCrypt.HashPassword("DemoPass123!");
        foreach (var role in roles)
        {
            Execute("""
                INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat)
                VALUES ($r,$email,$phone,$hash,$r,1,0,0,'2026-01-01','2026-01-01');
                """, ("$r", role), ("$email", role.ToLowerInvariant() + "@s104.test"), ("$phone", "098" + Array.IndexOf(roles,role).ToString("D7")), ("$hash",hash));
            accounts[role] = Convert.ToInt32(Scalar("SELECT id FROM tai_khoan WHERE email=$email", ("$email", role.ToLowerInvariant()+"@s104.test")));
        }
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(appPath);
            builder.UseWebRoot(Path.Combine(temp, "wwwroot"));
            builder.UseSetting("DatabasePath", database);
            builder.UseSetting("RoomImagesPath", Path.Combine(temp, "wwwroot", "uploads", "rooms"));
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
    }

    private string SnapshotBusinessData()
    {
        using var c = new SqliteConnection("Data Source=" + database); c.Open();
        var result = new List<object?>();
        foreach (var table in new[] {"tai_khoan", "toa_nha", "phong_tro"})
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name=$table";
            cmd.Parameters.AddWithValue("$table", table);
            result.Add(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT * FROM " + table + " ORDER BY id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                result.Add(row);
            }
        }
        return JsonSerializer.Serialize(result);
    }

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private void Execute(string sql, params (string, object)[] values)
    {
        using var c = new SqliteConnection("Data Source=" + database); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        foreach (var (k,v) in values) cmd.Parameters.AddWithValue(k,v);
        cmd.ExecuteNonQuery();
    }
    private object? Scalar(string sql, params (string, object)[] values)
    {
        using var c = new SqliteConnection("Data Source=" + database); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = sql;
        foreach (var (k,v) in values) cmd.Parameters.AddWithValue(k,v);
        return cmd.ExecuteScalar();
    }
    private async Task<HttpClient> Login(string role)
    {
        var client = Client();
        await Login(client, role);
        return client;
    }

    private async Task Login(HttpClient client, string role)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["TaiKhoanDangNhap"] = role.ToLowerInvariant()+"@s104.test", ["MatKhau"] = "DemoPass123!",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public void SeedMatchesAll36CellsAndRerunPreservesRevocations()
    {
        Assert.Equal(4L, Scalar("SELECT COUNT(*) FROM app_role"));
        Assert.Equal(9L, Scalar("SELECT COUNT(*) FROM app_module"));
        Assert.Equal(36L, Scalar("SELECT COUNT(*) FROM role_permission"));
        for (int m=0;m<modules.Length;m++)
        for (int r=0;r<roles.Length;r++)
        {
            Assert.Equal(levels[m][r], Scalar("SELECT AccessLevel FROM role_permission WHERE ModuleCode=$m AND RoleCode=$r", ("$m",modules[m]),("$r",roles[r])));
            var own = r == 0 && new[] {0,3,4,5,6,7}.Contains(m);
            Assert.Equal(own ? 1L : 0L, Scalar("SELECT OwnDataOnly FROM role_permission WHERE ModuleCode=$m AND RoleCode=$r", ("$m",modules[m]),("$r",roles[r])));
        }
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='TAI_CHINH'");
        PermissionSchemaInitializer.Initialize(database,Path.Combine(appPath,"Data","permissions.seed.json"));
        Assert.Equal("NONE",Scalar("SELECT AccessLevel FROM role_permission WHERE RoleCode='CHU_NHA' AND ModuleCode='TAI_CHINH'"));
    }

    [Fact]
    public void UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi()
    {
        SchemaTestFixture.RemoveRoomExtensions(database);
        Execute("DELETE FROM app_schema_version WHERE version>=13");
        Execute("UPDATE role_permission SET AccessLevel='READ' WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'");

        DatabaseUpdates.Update(database, Path.Combine(appPath, "Data", "permissions.seed.json"));

        Assert.Equal(41L, Scalar("SELECT MAX(version) FROM app_schema_version"));
        Assert.Equal(41L, Scalar("SELECT MAX(version) FROM app_schema_version"));
        Assert.Equal("WRITE", Scalar("SELECT AccessLevel FROM role_permission WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'"));

        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'");
        DatabaseUpdates.Update(database, Path.Combine(appPath, "Data", "permissions.seed.json"));
        Assert.Equal("NONE", Scalar("SELECT AccessLevel FROM role_permission WHERE RoleCode='QUAN_LY' AND ModuleCode='TIN_DANG'"));
    }

    [Fact]
    public async Task MatrixShowsEveryRoleAndModuleAndTenantOnlyOwnRole()
    {
        using var admin = await Login("ADMIN");
        var html = await admin.GetStringAsync("/Permissions");
        for(int m=0;m<modules.Length;m++)
        {
            Assert.Contains("data-module=\"" + modules[m] + "\"", html);
            var row = Regex.Match(html, "<tr data-module=\"" + modules[m] + "\"[\\s\\S]*?</tr>").Value;
            for(int r=0;r<roles.Length;r++)
                Assert.Contains("data-role=\"" + roles[r] + "\" data-access=\"" + levels[m][r] + "\"",row);
        }
        using var tenant = await Login("KHACH_THUE");
        var data = JsonDocument.Parse(await tenant.GetStringAsync("/api/permissions"));
        Assert.Single(data.RootElement.GetProperty("roles").EnumerateArray());
        Assert.Equal(9,data.RootElement.GetProperty("modules").GetArrayLength());
    }

    [Fact]
    public async Task MenuAndEveryModuleRouteMatchMatrixForAllRoles()
    {
        for(int r=0;r<roles.Length;r++)
        {
            using var client = await Login(roles[r]);
            var home = await client.GetStringAsync("/");
            for(int m=0;m<modules.Length;m++)
            {
                var marker = "data-menu-module=\"" + modules[m] + "\"";
                var allowed = levels[m][r] != "NONE";
                var showMenu = allowed && roles[r] != "KHACH_THUE";
                Assert.Equal(showMenu,home.Contains(marker));
                var api = await client.GetAsync("/api/modules/" + modules[m]);
                Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden,api.StatusCode);
                var page = await client.GetAsync("/Modules/" + modules[m]);
                if (!allowed)
                {
                    Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
                    Assert.Equal("FORBIDDEN", JsonDocument.Parse(await api.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
                }
                else Assert.True(page.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect);
            }
            if (roles[r] == "KHACH_THUE")
            {
                Assert.Contains("class=\"tenant-header\"", home);
                Assert.DoesNotContain("id=\"app-sidebar\"", home);
            }
            else
            {
                Assert.Contains("id=\"app-sidebar\"", home);
            }
        }
    }

    [Fact]
    public async Task MissingTamperedAndExpiredCookiesReturn401Not403()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/modules/TAI_CHINH")).StatusCode);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier,accounts["ADMIN"].ToString()),
            new Claim(ClaimTypes.Role,"ADMIN")
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        var expired = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow.AddHours(-2), ExpiresUtc = DateTimeOffset.UtcNow.AddHours(-1)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        foreach(var value in new[] {"invalid-cookie",options.TicketDataFormat.Protect(expired)})
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,"/api/permissions");
            request.Headers.Add("Cookie",options.Cookie.Name+"="+value);
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
            Assert.Equal("UNAUTHENTICATED",JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task RevocationRoleChangeMissingPairAndDisabledAccountTakeEffectImmediately()
    {
        using var owner = await Login("CHU_NHA");
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync("/api/modules/TAI_CHINH")).StatusCode);
        Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA'");
        var home = await owner.GetStringAsync("/");
        Assert.DoesNotContain("data-menu-module",home);
        Assert.DoesNotContain("data-menu-group",home);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/api/modules/TAI_CHINH")).StatusCode);
        Execute("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=$id",("$id",accounts["CHU_NHA"]));
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync("/api/modules/DIEN_NUOC")).StatusCode);
        Execute("DELETE FROM role_permission WHERE RoleCode='QUAN_LY' AND ModuleCode='DIEN_NUOC'");
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/api/modules/DIEN_NUOC")).StatusCode);
        Execute("UPDATE tai_khoan SET vai_tro='UNKNOWN' WHERE id=$id",("$id",accounts["CHU_NHA"]));
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync("/api/modules/PHONG_TRO")).StatusCode);
        Execute("UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=$id",("$id",accounts["CHU_NHA"]));
        Assert.Equal(HttpStatusCode.Unauthorized,(await owner.GetAsync("/api/modules/PHONG_TRO")).StatusCode);
    }

    [Fact]
    public async Task ReadOnlyRolesCannotOpenOrPostRoomCreationButAdminHasFullAccess()
    {
        foreach(var role in new[] {"KHACH_THUE","QUAN_LY"})
        {
            using var client = await Login(role);
            if (role == "KHACH_THUE")
            {
                Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/PhongTro")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/PhongTro/ToaNha")).StatusCode);
                continue;
            }
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/PhongTro")).StatusCode);
            Assert.DoesNotContain("href=\"/PhongTro/Create",await client.GetStringAsync("/PhongTro"));
            foreach(var action in new[] {"Create","CreateBulk","TaoToaNha"})
            {
                var getResponse = await client.GetAsync("/PhongTro/"+action);
                Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
                var post = await client.PostAsync("/PhongTro/"+action,new FormUrlEncodedContent(new Dictionary<string,string>()));
                Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
            }
        }
        using var admin = await Login("ADMIN");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/PhongTro")).StatusCode);
        foreach (var action in new[] { "Create", "CreateBulk", "TaoToaNha" })
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/PhongTro/" + action)).StatusCode);
        using var owner = await Login("CHU_NHA");
        Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync("/PhongTro/TaoToaNha")).StatusCode);
    }
    [Fact]
    public void DemoCreatesFourAccountsOnlyInNewCopy()
    {
        var before = SnapshotBusinessData();
        var target = Path.Combine(temp, "demo.sqlite");
        PermissionDemo.Create(database, target, Path.Combine(appPath,"Data","permissions.seed.json"));
        Assert.Equal(before,SnapshotBusinessData());
        using var c = new SqliteConnection("Data Source=" + target); c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(DISTINCT vai_tro) FROM tai_khoan WHERE email LIKE 'demo-%@example.test'";
        Assert.Equal(4L,cmd.ExecuteScalar());
        Assert.Throws<InvalidOperationException>(() => PermissionDemo.Create(database,target,Path.Combine(appPath,"Data","permissions.seed.json")));
    }

    public void Dispose()
    {
        factory.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(temp,true);
    }
}

