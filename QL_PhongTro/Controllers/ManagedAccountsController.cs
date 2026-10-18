using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Authorization;

namespace QL_PhongTro.Controllers;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "ADMIN")]
[AutoValidateAntiforgeryToken]
[ModuleAccess("TAI_KHOAN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ManagedAccountsController(AppDbContext db, ITemporaryPasswordEmailSender email,
    IOptions<PasswordResetOptions> options) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? role, string? status, int page = 1)
    {
        if (role is not (null or "" or "ADMIN" or "CHU_NHA" or "QUAN_LY" or "KHACH_THUE") ||
            status is not (null or "" or "active" or "locked")) return BadRequest("Bộ lọc không hợp lệ.");
        var query = db.TaiKhoans.AsNoTracking().Where(a => !a.IsDeleted);
        if (!string.IsNullOrEmpty(role)) query = query.Where(a => a.VaiTro == role);
        if (!string.IsNullOrEmpty(status)) query = query.Where(a => a.DangHoatDong == (status == "active"));
        var total = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 20d)));
        return View(new ManagedAccountsViewModel
        {
            Role = role, Status = status, Total = total, Page = page,
            Accounts = await query.OrderByDescending(a => a.Id).Skip((page - 1) * 20).Take(20).ToListAsync()
        });
    }

    [HttpGet, ModuleAccess("TAI_KHOAN", write: true)]
    public IActionResult Create() => View(new CreateManagedAccountViewModel());

    [HttpPost, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> Create(CreateManagedAccountViewModel model)
    {
        model.Email = (model.Email ?? "").Trim().ToLowerInvariant();
        model.HoTen = (model.HoTen ?? "").Trim();
        if (string.IsNullOrWhiteSpace(model.HoTen)) ModelState.AddModelError(nameof(model.HoTen), "Nhập họ tên.");
        if (!ModelState.IsValid) return View(model);
        if (await db.TaiKhoans.AnyAsync(a => !a.IsDeleted && a.Email.Trim().ToLower() == model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng.");
        if (await db.TaiKhoans.AnyAsync(a => !a.IsDeleted && a.SoDienThoai == model.SoDienThoai))
            ModelState.AddModelError(nameof(model.SoDienThoai), "Số điện thoại đã được sử dụng.");
        if (!ModelState.IsValid) return View(model);
        var password = TemporaryPassword();
        var now = DateTime.UtcNow;
        var account = new TaiKhoan
        {
            HoTen = model.HoTen, Email = model.Email, SoDienThoai = model.SoDienThoai,
            VaiTro = model.VaiTro, MatKhau = BCrypt.Net.BCrypt.HashPassword(password),
            DangHoatDong = true, MustChangePassword = true, NgayTao = now, NgayCapNhat = now
        };
        db.TaiKhoans.Add(account);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            ModelState.AddModelError("", "Email hoặc số điện thoại đã được sử dụng. Vui lòng kiểm tra lại.");
            return View(model);
        }
        await SendAsync(account, password);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> ChangeRole(int id)
    {
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == id);
        if (account is null) return NotFound();
        return View(new ChangeAccountRoleViewModel
        {
            Id = id, HoTen = account.HoTen, VaiTro = account.VaiTro ?? "", OriginalRole = account.VaiTro ?? ""
        });
    }

    [HttpPost, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> ChangeRole(ChangeAccountRoleViewModel model)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == model.Id);
        if (account is null) return NotFound();
        model.HoTen = account.HoTen;
        if (!ModelState.IsValid) return View(model);
        if (account.VaiTro != model.OriginalRole)
            ModelState.AddModelError("", "Vai trò đã được thay đổi. Vui lòng tải lại trang.");
        if (account.VaiTro != model.VaiTro)
        {
            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == model.Id.ToString())
                ModelState.AddModelError("", "Không được tự đổi vai trò của mình.");
            if (await db.ToaNhas.AnyAsync(b => b.ChuNhaId == model.Id) && model.VaiTro != "CHU_NHA")
                ModelState.AddModelError("", "Tài khoản đang sở hữu tòa nhà. Cần chuyển chủ nhà trước khi đổi vai trò.");
            if (await db.ToaNhas.AnyAsync(b => b.QuanLyId == model.Id) && model.VaiTro != "QUAN_LY")
                ModelState.AddModelError("", "Tài khoản đang được phân công quản lý tòa nhà. Cần bỏ phân công trước khi đổi vai trò.");
        }
        if (!ModelState.IsValid) return View(model);
        if (account.VaiTro == model.VaiTro) return RedirectToAction(nameof(Index));
        account.VaiTro = model.VaiTro;
        account.NgayCapNhat = DateTime.UtcNow;
        account.RefreshTokenHash = null;
        account.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
        await RevokeAsync(account.Id);
        await tx.CommitAsync();
        TempData["AccountMessage"] = "Đã đổi vai trò và thu hồi mọi phiên đăng nhập. Người dùng cần đăng nhập lại.";
        return RedirectToAction(nameof(Index));
    }

    // Retry after delivery failure without storing a plaintext temporary password.
    [HttpPost, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> Resend(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == id && a.MustChangePassword && a.DangHoatDong);
        if (account is null || account.VaiTro is not ("CHU_NHA" or "QUAN_LY" or "ADMIN")) return NotFound();
        var password = TemporaryPassword();
        db.TemporaryPasswordResentFor = account.Id;
        account.MatKhau = BCrypt.Net.BCrypt.HashPassword(password);
        account.NgayCapNhat = DateTime.UtcNow;
        account.RefreshTokenHash = null; account.RefreshTokenExpiry = null;
        await db.SaveChangesAsync();
        await RevokeAsync(id);
        await tx.CommitAsync();
        await SendAsync(account, password);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> SetActive(int id, bool active, bool confirmed = false)
    {
        if (!active && !confirmed) return BadRequest("Cần xác nhận khoá tài khoản.");
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString()) return BadRequest("Không được tự khoá tài khoản.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == id);
        if (account is null) return NotFound();
        if (account.VaiTro is not ("CHU_NHA" or "QUAN_LY" or "KHACH_THUE" or "ADMIN")) return Forbid();
        account.DangHoatDong = active;
        account.RefreshTokenHash = null; account.RefreshTokenExpiry = null;
        account.FailedLoginCount = 0; account.LockedUntil = null;
        account.NgayCapNhat = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await RevokeAsync(id);
        await tx.CommitAsync();
        TempData["AccountMessage"] = active ? "Đã mở khoá. Người dùng cần đăng nhập lại." : "Đã khoá và thu hồi mọi phiên đăng nhập.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ModuleAccess("TAI_KHOAN", write: true)]
    public async Task<IActionResult> Delete(int id, bool confirmed = false)
    {
        if (!confirmed) return BadRequest("Cần xác nhận xóa tài khoản.");
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString()) return BadRequest("Không được tự xóa tài khoản.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == id && a.VaiTro != "ADMIN");
        if (account is null) return NotFound();
        account.DangHoatDong = false; account.IsDeleted = true; account.RefreshTokenHash = null; account.RefreshTokenExpiry = null;
        account.NgayCapNhat = DateTime.UtcNow;
        if (account.VaiTro == "QUAN_LY")
        {
            var buildings = await db.ToaNhas.Where(x => x.QuanLyId == id).ToListAsync();
            foreach (var building in buildings) building.QuanLyId = null;
        }
        await db.SaveChangesAsync();
        await RevokeAsync(id);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE password_reset_token SET used_at=COALESCE(used_at,0) WHERE account_id={id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM email_confirmation WHERE account_id={id}");
        await tx.CommitAsync();
        TempData["AccountMessage"] = "Đã xóa tài khoản; email và số điện thoại có thể đăng ký lại. Dữ liệu lịch sử được giữ lại; các tòa của quản lý đã chuyển về Chưa phân công.";
        return RedirectToAction(nameof(Index));
    }

    private Task<int> RevokeAsync(int id)
    {
        var version = Guid.NewGuid().ToString("N");
        return db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO account_session_version(account_id,version) VALUES ({id},{version}) ON CONFLICT(account_id) DO UPDATE SET version=excluded.version");
    }

    private async Task SendAsync(TaiKhoan account, string password)
    {
        try
        {
            await email.SendTemporaryAsync(account.Email, account.HoTen, ManagedAccountsViewModel.RoleName(account.VaiTro), password);
            TempData["AccountMessage"] = string.IsNullOrWhiteSpace(options.Value.PickupDirectory)
                ? "Tài khoản sẵn sàng. Email mật khẩu tạm đã được chuyển đến máy chủ gửi thư."
                : "Tài khoản sẵn sàng. Email thử nghiệm đã lưu thành file .eml, chưa gửi đến hộp thư thật.";
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            TempData["AccountError"] = "Tài khoản đã lưu nhưng chưa gửi được email. Kiểm tra cấu hình email, rồi chọn Gửi lại mật khẩu tạm. Mật khẩu tạm cũ sẽ bị thay thế.";
        }
    }

    private static string TemporaryPassword()
    {
        string[] groups = ["ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghijkmnopqrstuvwxyz", "23456789", "!@#$%&*?"];
        var all = string.Concat(groups);
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
        {
            var source = i < groups.Length ? groups[i] : all;
            chars[i] = source[RandomNumberGenerator.GetInt32(source.Length)];
        }
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
