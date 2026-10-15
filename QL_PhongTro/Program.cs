using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QL_PhongTro;
using QL_PhongTro.Data;

using QL_PhongTro.Authorization;

using QL_PhongTro.Services;


var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--check-email-config"))
{
    var mail = builder.Configuration.GetSection("PasswordReset").Get<PasswordResetOptions>() ?? new();
    Console.WriteLine("Email mode: " + (string.IsNullOrWhiteSpace(mail.PickupDirectory) ? "SMTP" : "PICKUP (local file only; no inbox delivery)"));
    if (!string.IsNullOrWhiteSpace(mail.PickupDirectory))
    {
        var pickupDirectory = Path.IsPathFullyQualified(mail.PickupDirectory)
            ? mail.PickupDirectory
            : Path.GetFullPath(mail.PickupDirectory, Path.GetTempPath());
        Console.WriteLine("Pickup directory: " + pickupDirectory);
    }
    Console.WriteLine($"SMTP host: {mail.Host}; port: {mail.Port}; TLS: {mail.EnableSsl}");
    Console.WriteLine($"Username configured: {!string.IsNullOrWhiteSpace(mail.Username)}; password configured: {!string.IsNullOrWhiteSpace(mail.Password)}; sender configured: {!string.IsNullOrWhiteSpace(mail.From)}");
    Console.WriteLine("Configuration check only. No email sent; credentials are not displayed.");
    return;
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<QL_PhongTro.Services.GiayToImageStore>();
builder.Services.AddSingleton(sp => new QL_PhongTro.Services.RoomImageStore(
    sp.GetRequiredService<IWebHostEnvironment>(), sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<QL_PhongTro.Services.HoSoAccess>();
builder.Services.AddScoped<QL_PhongTro.Services.LichHenService>();
builder.Services.AddScoped<QL_PhongTro.Services.TienDoChiSoService>();
builder.Services.AddScoped<QL_PhongTro.Services.ICredentialValidationService, QL_PhongTro.Services.CredentialValidationService>();
builder.Services.AddScoped<HopDongPdfService>();

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
var configuredDatabasePath = builder.Configuration["DatabasePath"];
var databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
    ? Path.Combine(dataDir, "local-dev.sqlite")
    : Path.GetFullPath(Path.IsPathRooted(configuredDatabasePath)
        ? configuredDatabasePath
        : Path.Combine(builder.Environment.ContentRootPath, configuredDatabasePath));

if (args.Contains("--create-sprint2-demo"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Sprint 2 demo is available only in Development.");
    var output = builder.Configuration["Sprint2Demo:Directory"]
        ?? throw new ArgumentException("Set Sprint2Demo:Directory to a new output directory.");
    var demoPassword = builder.Configuration["Sprint2Demo:Password"]
        ?? throw new ArgumentException("Set Sprint2Demo:Password. The launcher generates a random local password when omitted.");
    await Sprint2DemoSeeder.CreateAsync(Path.GetFullPath(output), Path.Combine(dataDir, "permissions.seed.json"),
        demoPassword,
        builder.Configuration["Sprint2Demo:Url"] ?? "http://localhost:5268");
    return;
}
if (args.Contains("--initialize-database"))
{
    LocalDatabaseInitializer.Create(databasePath, Path.Combine(dataDir, "permissions.seed.json"));
    return;
}
if (!File.Exists(databasePath))
    throw new FileNotFoundException("Local SQLite database missing. Run --initialize-database explicitly with the same DatabasePath.", databasePath);
if (args.Contains("--initialize-rental-requests"))
{
    RentalRequestSchema.Initialize(databasePath);
    return;
}

if (args.Contains("--update-database"))
{
    DatabaseUpdates.Update(databasePath, Path.Combine(dataDir, "permissions.seed.json"));
    return;
}
if (args.Contains("--check-database"))
{
    DatabaseUpdates.Check(databasePath);
    Console.WriteLine("Database schema is ready.");
    return;
}
if (args.Contains("--create-local-admin"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("--create-local-admin is available only in Development.");
    DatabaseUpdates.Check(databasePath);
    LocalAdminInitializer.Create(databasePath,
        builder.Configuration["LocalAdmin:Email"],
        builder.Configuration["LocalAdmin:Password"],
        builder.Configuration["LocalAdmin:Phone"]);
    return;
}
if (args.Contains("--seed-request-demo"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("--seed-request-demo is available only in Development.");
    RequestDemoSeeder.Seed(databasePath, builder.Configuration["RequestDemo:Password"]);
    return;
}

var demoIndex = Array.IndexOf(args, "--create-permission-demo");
if (demoIndex >= 0)
{
    if (demoIndex + 1 >= args.Length) throw new ArgumentException("Provide a new SQLite path after --create-permission-demo.");
    PermissionDemo.Create(databasePath, args[demoIndex + 1], Path.Combine(dataDir, "permissions.seed.json"));
    return;
}
if (args.Contains("--initialize-permissions"))
{
    PermissionSchemaInitializer.Initialize(databasePath, Path.Combine(dataDir, "permissions.seed.json"));
    return;
}
if (args.Contains("--initialize-password-security"))
{
    PasswordSchemaInitializer.Initialize(databasePath);
    return;
}
if (args.Contains("--initialize-services"))
{
    DichVuSchemaInitializer.Initialize(databasePath);
    return;
}
if (args.Contains("--initialize-service-invoices"))
{
    DichVuSchemaInitializer.InitializeInvoices(databasePath);
    return;
}
var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
{
    DataSource = databasePath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, ForeignKeys = true
}.ToString();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Isolate demo/real databases even when both run on localhost with the same key ring.
var authScope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(databasePath).ToUpperInvariant())))[..24];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("QLPhongTro:" + authScope);
var configuredKeyPath = builder.Configuration["DataProtectionKeysPath"];
if (!string.IsNullOrWhiteSpace(configuredKeyPath))
{
    var keyPath = Path.GetFullPath(Path.IsPathRooted(configuredKeyPath)
        ? configuredKeyPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredKeyPath));
    Directory.CreateDirectory(keyPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".QLPhongTro." + authScope;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.EventsType = typeof(AppCookieEvents);
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddSingleton<RegistrationSettings>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AppCookieEvents>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TokenBlacklistService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.AddScoped<IPasswordEmailSender, PasswordEmailSender>();
builder.Services.AddScoped<ITemporaryPasswordEmailSender, PasswordEmailSender>();
builder.Services.AddScoped<IRegistrationEmailSender, PasswordEmailSender>();
builder.Services.AddScoped<IAppointmentEmailSender, PasswordEmailSender>();
builder.Services.AddScoped<IInvoiceEmailSender, PasswordEmailSender>();
builder.Services.AddScoped<InvoiceNotificationDispatcher>();
builder.Services.AddHostedService<InvoiceNotificationHostedService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<SessionVersionStore>();
builder.Services.AddScoped<DichVuService>();
builder.Services.AddScoped<YeuCauThueService>();
builder.Services.AddScoped<DichVuPhongService>();
builder.Services.AddScoped<HoaDonDichVuService>();
builder.Services.AddScoped<GhiThuHoaDonService>();
builder.Services.AddScoped<IBaoCaoThanhToanService, BaoCaoThanhToanService>();
builder.Services.AddSingleton(new PaymentProofImageStore(databasePath + ".payment-proofs"));
builder.Services.AddScoped<IPaymentProofService, PaymentProofService>();
builder.Services.AddSingleton(new BaoHongImageStore(databasePath + ".repair-images"));
builder.Services.AddScoped<BaoHongService>();
builder.Services.AddScoped<QuanLyBaoHongService>();
builder.Services.AddScoped<BaoHongChiPhiService>();
builder.Services.AddScoped<ChuNhaBaoHongService>();
builder.Services.AddScoped<ChiSoDienNuocService>();
builder.Services.AddScoped<RoomImageDeletionService>();
builder.Services.AddHostedService<RoomImageCleanupHostedService>();
builder.Services.AddScoped<TinDangExpirationService>();
builder.Services.AddHostedService<TinDangExpirationHostedService>();
builder.Services.Configure<DichVuMacDinhOptions>(builder.Configuration.GetSection("DichVuMacDinh"));

builder.Services.AddAuthentication()
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var version = context.Principal?.FindFirst(SessionVersionStore.ClaimType)?.Value;
            if (!int.TryParse(id, out var accountId) || !context.HttpContext.RequestServices.GetRequiredService<SessionVersionStore>().IsValid(accountId, version))
                context.Fail("Phiên đăng nhập đã bị vô hiệu hóa.");
            if (int.TryParse(id, out var currentId))
            {
                var account = await context.HttpContext.RequestServices.GetRequiredService<AppDbContext>().TaiKhoans.AsNoTracking().SingleOrDefaultAsync(a => a.Id == currentId);
                if (account is null || account.IsDeleted || !account.EmailConfirmed || !account.DangHoatDong || account.MustChangePassword)
                    context.Fail("Tài khoản chưa được phép sử dụng phiên này.");
            }
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});


var app = builder.Build();

if (args.Contains("--retry-pending-room-image-deletes"))
{
    DatabaseUpdates.Check(databasePath);
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<RoomImageDeletionService>().RetryPendingDeletesAsync();
    Console.WriteLine($"Pending room image delete retry finished. Deleted: {result.Deleted}; failed: {result.Failed}.");
    return;
}

if (args.Contains("--check-room-image-storage"))
{
    DatabaseUpdates.Check(databasePath);
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<RoomImageDeletionService>().CheckStorageAsync();
    Console.WriteLine($"Room image storage check. Expected files: {result.ExpectedFiles}; missing: {result.MissingFiles}; orphan: {result.OrphanFiles}.");
    foreach (var path in result.Missing.Take(20))
        Console.WriteLine("MISSING " + path);
    foreach (var path in result.Orphans.Take(20))
        Console.WriteLine("ORPHAN " + path);
    if (result.MissingFiles > 0 || result.OrphanFiles > 0)
        Environment.ExitCode = 2;
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
var configuredRoomImagesPath = builder.Configuration["RoomImagesPath"];
if (!string.IsNullOrWhiteSpace(configuredRoomImagesPath))
{
    var roomImagesPath = Path.GetFullPath(Path.IsPathRooted(configuredRoomImagesPath)
        ? configuredRoomImagesPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredRoomImagesPath));
    Directory.CreateDirectory(roomImagesPath);
    var defaultRoomImagesPath = Path.GetFullPath(Path.Combine(
        app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
        "uploads",
        "rooms"));
    if (!string.Equals(roomImagesPath, defaultRoomImagesPath, StringComparison.OrdinalIgnoreCase))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(roomImagesPath),
            RequestPath = "/uploads/rooms"
        });
    }
}
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<RequirePasswordChangeMiddleware>();
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");



DatabaseUpdates.Check(databasePath);
AccountReuseSchema.Ensure(databasePath);

app.MapRazorPages();
await TimTinWarmup.RunAsync(app.Services);
app.Run();

public partial class Program { }
