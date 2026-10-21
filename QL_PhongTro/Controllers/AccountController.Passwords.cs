using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Controllers;

public partial class AccountController
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Challenge();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(a => !a.IsDeleted && a.Id == id && a.DangHoatDong);
        if (account is null) return Forbid();
        var valid = false;
        try { valid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, account.MatKhau); }
        catch (BCrypt.Net.SaltParseException) { }
        if (!valid)
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
        else if (BCrypt.Net.BCrypt.Verify(model.NewPassword, account.MatKhau))
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
        if (!ModelState.IsValid) return View(model);

        var hash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        var updated = await db.TaiKhoans.Where(a => !a.IsDeleted && a.Id == id && a.MatKhau == account.MatKhau && a.DangHoatDong)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.MatKhau, hash).SetProperty(a => a.MustChangePassword, false)
                .SetProperty(a => a.RefreshTokenHash, (string?)null).SetProperty(a => a.RefreshTokenExpiry, (DateTime?)null)
                .SetProperty(a => a.NgayCapNhat, DateTime.UtcNow));
        if (updated != 1)
        {
            ModelState.AddModelError("", "Tài khoản vừa thay đổi. Vui lòng thử lại.");
            return View(model);
        }
        var version = Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO account_session_version(account_id,version) VALUES ({id},{version}) ON CONFLICT(account_id) DO UPDATE SET version=excluded.version");
        await transaction.CommitAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["PasswordSuccess"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
}
