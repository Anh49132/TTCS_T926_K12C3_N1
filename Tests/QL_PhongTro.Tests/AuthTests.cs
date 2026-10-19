using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace QL_PhongTro.Tests;

public class MockTimeProvider : ITimeProvider
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}

public class AuthTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScope _scope;
    private readonly HttpClient _client;
    private readonly MockTimeProvider _mockTimeProvider;
    private readonly string _testDatabasePath;
    private readonly string _appPath;

    public AuthTests()
    {
        _mockTimeProvider = new MockTimeProvider();
        
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "QL_PhongTro", "Data")))
            directory = directory.Parent;
        _appPath = Path.Combine(directory?.FullName ?? throw new Exception("Repository not found"), "QL_PhongTro");
        
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.sqlite");

        LocalDatabaseInitializer.Create(_testDatabasePath, Path.Combine(_appPath, "Data", "permissions.seed.json"));
        
        // Now create the factory with the same database path
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(_appPath);
                builder.UseSetting("DatabasePath", _testDatabasePath);
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITimeProvider));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton<ITimeProvider>(_mockTimeProvider);
                });
            });
        
        _client = _factory.CreateClient();

        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose()
    {
        // Stop hosted workers and close their SQLite connections before removing
        // the isolated fixture. The app must not keep using a deleted database.
        _client.Dispose();
        _factory.Dispose();
        _scope.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_testDatabasePath))
        {
            File.Delete(_testDatabasePath);
        }
    }

    private async Task<(int statusCode, JsonElement body)> PostJsonAsync(string url, object body, string? accessToken = null)
    {
        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }
        var response = await _client.SendAsync(request);
        var statusCode = (int)response.StatusCode;
        var responseBody = await response.Content.ReadAsStringAsync();
        try
        {
            var doc = JsonDocument.Parse(responseBody);
            return (statusCode, doc.RootElement);
        }
        catch
        {
            return (statusCode, JsonDocument.Parse("{}").RootElement);
        }
    }

    private TaiKhoan CreateUser(string email = "test@example.com", string phone = "0901234567", string password = "Test123456")
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        int id;
        // Seed with plain SQL: an audited entity write needs a verified actor, and a test
        // fixture has no HTTP caller that could be the actor for a brand new account.
        using (var c = new SqliteConnection("Data Source=" + _testDatabasePath))
        {
            c.Open();
            using var insert = c.CreateCommand();
            insert.CommandText = """
                INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,failed_login_count,must_change_password,ngay_tao,ngay_cap_nhat)
                VALUES ('Test User',$email,$phone,$hash,'KHACH_THUE',1,0,0,0,0,$now,$now);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$email", email);
            insert.Parameters.AddWithValue("$phone", phone);
            insert.Parameters.AddWithValue("$hash", hash);
            insert.Parameters.AddWithValue("$now", now);
            id = Convert.ToInt32(insert.ExecuteScalar());
        }
        return new TaiKhoan
        {
            Id = id,
            HoTen = "Test User",
            Email = email,
            SoDienThoai = phone,
            MatKhau = hash,
            VaiTro = "KHACH_THUE",
            DangHoatDong = true,
            IsStaff = false,
            IsSuperuser = false,
            FailedLoginCount = 0,
            NgayTao = DateTime.UtcNow,
            NgayCapNhat = DateTime.UtcNow
        };
    }

    private async Task<TaiKhoan?> GetUserAsync(int id)
    {
        return await _dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    // ========== LÁT 1: ĐĂNG NHẬP & DUY TRÌ PHIÊN ==========

    [Fact]
    public async Task L1_1_Login_ValidCredentials_ReturnsAccessAndRefreshTokens()
    {
        CreateUser();
        var (statusCode, body) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("accessToken", out var accessToken));
        Assert.True(body.TryGetProperty("refreshToken", out var refreshToken));
        Assert.True(body.TryGetProperty("userId", out _));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));
        Assert.False(string.IsNullOrEmpty(refreshToken.GetString()));
    }

    [Fact]
    public async Task L1_2_Refresh_ValidRefreshToken_ReturnsNewAccessToken()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        var refreshToken = loginBody.GetProperty("refreshToken").GetString();

        var (statusCode, body) = await PostJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = refreshToken
        });

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("accessToken", out var newAccessToken));
        Assert.False(string.IsNullOrEmpty(newAccessToken.GetString()));
    }

    [Fact]
    public async Task L1_3_Refresh_ExpiredRefreshToken_Returns401()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        var refreshToken = loginBody.GetProperty("refreshToken").GetString();

        // Tiến thời gian quá hạn refresh token (8 ngày > 7 ngày)
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddDays(8);

        var (statusCode, body) = await PostJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = refreshToken
        });

        Assert.Equal(401, statusCode);
        Assert.Equal("TOKEN_EXPIRED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task L1_4_WrongEmailAndWrongPassword_ReturnSameError()
    {
        CreateUser();

        // Sai email
        var (status1, body1) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "wrong@example.com",
            MatKhau = "Test123456"
        });

        // Sai mật khẩu
        var (status2, body2) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "WrongPass123"
        });

        Assert.Equal(status1, status2);
        Assert.Equal(body1.GetProperty("error").GetString(), body2.GetProperty("error").GetString());
        Assert.Equal(body1.GetProperty("message").GetString(), body2.GetProperty("message").GetString());
    }

    // ========== LÁT 2: KHOÁ TÀI KHOẢN ==========

    [Fact]
    public async Task L2_5_FailedLogin5TimesIn15Min_LocksAccount()
    {
        CreateUser();

        for (int i = 0; i < 5; i++)
        {
            await PostJsonAsync("/api/auth/login", new
            {
                TaiKhoanDangNhap = "test@example.com",
                MatKhau = "WrongPass" + i
            });
        }

        var updatedUser = await _dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(updatedUser?.LockedUntil);
        Assert.True(updatedUser?.LockedUntil > _mockTimeProvider.UtcNow);
    }

    [Fact]
    public async Task L2_6_LockedAccount_RejectsCorrectPassword()
    {
        var user = CreateUser();
        user.FailedLoginCount = 5;
        user.LockedUntil = _mockTimeProvider.UtcNow.AddMinutes(15);
        _dbContext.TaiKhoans.Update(user);
        _dbContext.SaveChanges();

        var (statusCode, body) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        Assert.Equal(401, statusCode);
        Assert.Contains("khoá", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task L2_7_LockExpired_LoginWithCorrectPassword_Succeeds()
    {
        var user = CreateUser();
        user.FailedLoginCount = 5;
        user.LockedUntil = _mockTimeProvider.UtcNow.AddMinutes(15);
        _dbContext.TaiKhoans.Update(user);
        _dbContext.SaveChanges();

        // Tiến thời gian qua 15 phút khoá
        _mockTimeProvider.UtcNow = _mockTimeProvider.UtcNow.AddMinutes(16);

        var (statusCode, body) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task L2_8_SuccessfulLogin_ResetsFailedCounter()
    {
        var user = CreateUser();
        user.FailedLoginCount = 3;
        _dbContext.TaiKhoans.Update(user);
        _dbContext.SaveChanges();

        await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        var updatedUser = await _dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.Equal(0, updatedUser?.FailedLoginCount);
        Assert.Null(updatedUser?.LockedUntil);
    }

    [Fact]
    public async Task L2_9_FailedLogin4Times_NotLocked_CanLoginOn5thAttempt()
    {
        CreateUser();

        // 4 lần sai
        for (int i = 0; i < 4; i++)
        {
            await PostJsonAsync("/api/auth/login", new
            {
                TaiKhoanDangNhap = "test@example.com",
                MatKhau = "WrongPass" + i
            });
        }

        // Lần 5 đúng mật khẩu -> phải thành công
        var (statusCode, body) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("accessToken", out _));
    }

    // ========== LÁT 3: ĐĂNG XUẤT ==========

    [Fact]
    public async Task L3_10_Logout_InvalidatesRefreshToken()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });
        var refreshToken = loginBody.GetProperty("refreshToken").GetString();
        var accessToken = loginBody.GetProperty("accessToken").GetString();

        var (statusCode, body) = await PostJsonAsync("/api/auth/logout", new
        {
            RefreshToken = refreshToken
        }, accessToken);

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("success", out _) && body.GetProperty("success").GetBoolean());

        var updatedUser = await _dbContext.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.Null(updatedUser?.RefreshTokenHash);
    }

    [Fact]
    public async Task L3_11_AccessTokenAfterLogout_Returns401()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });
        var accessToken = loginBody.GetProperty("accessToken").GetString();
        var refreshToken = loginBody.GetProperty("refreshToken").GetString();

        // Logout with access token to blacklist it
        await PostJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken }, accessToken);

        // Gọi API /api/auth/me với access token cũ
        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task L3_12_RefreshTokenAfterLogout_Returns401()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });
        var refreshToken = loginBody.GetProperty("refreshToken").GetString();
        var accessToken = loginBody.GetProperty("accessToken").GetString();

        await PostJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken }, accessToken);

        var (statusCode, body) = await PostJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken });

        Assert.Equal(401, statusCode);
        Assert.Equal("TOKEN_EXPIRED", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task L3_13_LogoutThenLoginAgain_Works()
    {
        CreateUser();
        var (_, loginBody) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });
        var refreshToken = loginBody.GetProperty("refreshToken").GetString();
        var accessToken = loginBody.GetProperty("accessToken").GetString();

        await PostJsonAsync("/api/auth/logout", new { RefreshToken = refreshToken }, accessToken);

        // Đăng nhập lại
        var (statusCode, body) = await PostJsonAsync("/api/auth/login", new
        {
            TaiKhoanDangNhap = "test@example.com",
            MatKhau = "Test123456"
        });

        Assert.Equal(200, statusCode);
        Assert.True(body.TryGetProperty("accessToken", out _));
        Assert.True(body.TryGetProperty("refreshToken", out _));
    }
}
