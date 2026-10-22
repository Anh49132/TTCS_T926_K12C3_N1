using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels.Auth;
using QL_PhongTro.ViewModels;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

public partial class AccountController(AppDbContext db, RegistrationSettings settings, SessionVersionStore sessions, ICredentialValidationService credentialValidator, AuthService authService, IRegistrationEmailSender registrationEmail) : Controller
{
    private static bool ValidatePhone(string? phone) =>
        !string.IsNullOrWhiteSpace(phone) && Regex.IsMatch(phone, @"^0\d{9}$");

    private static bool ValidatePassword(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length >= 8 &&
        Regex.IsMatch(password, "[A-Za-z]") && Regex.IsMatch(password, @"\d");

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool timeout = false)
    {
        // Thong diep het phien (tu dev) giu nguyen tren trang dang nhap.
        if (timeout) ViewData["TimeoutMessage"] = "Phiên đăng nhập đã hết hạn hoặc không còn hợp lệ. Vui lòng đăng nhập lại.";
        return View(new LoginRequest { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // S1-02: trinh duyet gui bang fetch de nhan duoc access token + refresh token
        // va luu xuong thiet bi. Van giu duong POST binh thuong de hoat dong khi
        // khong co JavaScript.
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
        {
            var invalid = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage).FirstOrDefault() ?? "Thông tin đăng nhập không hợp lệ.";
            if (isAjax) return Json(new { success = false, message = invalid, locked = false, remainingMinutes = 0 });
            return View(request);
        }

        // Validate credentials using shared service (MVC cookie auth flow)
        var validation = await credentialValidator.ValidateAsync(request);

        if (!validation.Success)
        {
            if (isAjax)
            {
                return Json(new
                {
                    success = false,
                    message = validation.Error ?? "Thông tin đăng nhập không hợp lệ.",
                    locked = validation.IsLocked,
                    remainingMinutes = validation.LockoutRemainingMinutes
                });
            }
            ModelState.AddModelError("", validation.Error ?? "Thông tin đăng nhập không hợp lệ.");
            return View(request);
        }

        var user = validation.User!;
        var sessionVersion = sessions.Capture(user.Id, user.MatKhau);
        if (sessionVersion is null)
        {
            const string changed = "Thông tin đăng nhập vừa thay đổi. Vui lòng đăng nhập lại.";
            if (isAjax) return Json(new { success = false, message = changed, locked = false, remainingMinutes = 0 });
            ModelState.AddModelError("", changed);
            return View(request);
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.HoTen),
            new Claim(ClaimTypes.Role, user.VaiTro ?? ""),
            new Claim(SessionVersionStore.ClaimType, sessionVersion)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = request.RememberMe });

        // S1-03: Check MustChangePassword (from dev)
        if (user.MustChangePassword)
        {
            return isAjax
                ? Json(new { success = true, mustChangePassword = true, returnUrl = Url.Action("ChangePassword", "Account") })
                : (IActionResult)RedirectToAction(nameof(ChangePassword));
        }

        // S1-02: phát access token (30 phut) + refresh token (7 ngay) cho phia may.
        if (isAjax)
        {
            var (issued, tokens, error) = await authService.LoginAsync(request);
            if (!issued)
            {
                return Json(new { success = false, message = error ?? "Đăng nhập thất bại", locked = false, remainingMinutes = 0 });
            }
            return Json(new
            {
                success = true,
                mustChangePassword = false,
                accessToken = tokens!.AccessToken,
                refreshToken = tokens.RefreshToken,
                returnUrl = Url.IsLocalUrl(request.ReturnUrl) ? request.ReturnUrl : Url.Action("Index", "Home")!
            });
        }

        return Url.IsLocalUrl(request.ReturnUrl) ? LocalRedirect(request.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult SessionStatus() => Json(new
    {
        authenticated = User.Identity?.IsAuthenticated == true,
        mustChangePassword = User.FindFirst("must_change_password")?.Value == "true"
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // For proper dual auth logout: need to revoke tokens if user has them
        // Since we only have cookie here, we sign out the cookie
        // The refresh token revocation would require the refresh token which we don't have in cookie-only flow
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.Headers.CacheControl = "no-store";
        return View();
    }

    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel? model)
    {
        if (model is null)
        {
            ModelState.AddModelError("", "Invalid request");
            return View(model);
        }

        if (string.IsNullOrWhiteSpace(model.HoTen))
            ModelState.AddModelError("HoTen", "Họ tên là bắt buộc");
        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError("Email", "Email là bắt buộc");
        if (!ValidatePhone(model.SoDienThoai))
            ModelState.AddModelError("SoDienThoai", "Số điện thoại phải bắt đầu bằng 0 và đúng 10 chữ số");
        if (!ValidatePassword(model.MatKhau))
            ModelState.AddModelError("MatKhau", "Mật khẩu tối thiểu 8 ký tự, ít nhất một chữ cái và một chữ số");

        if (!ModelState.IsValid)
            return View(model);

        var emailNorm = model.Email!.ToLower().Trim();
        var phoneNorm = model.SoDienThoai!;

        // A retry after registration/email delivery must resume confirmation, not
        // create another account or replace the password of the pending account.
        var pendingAccount = await db.TaiKhoans.AsNoTracking().AnyAsync(a =>
            !a.IsDeleted && a.DangHoatDong && !a.EmailConfirmed && a.VaiTro == "KHACH_THUE" &&
            a.Email.Trim().ToLower() == emailNorm && a.SoDienThoai == phoneNorm);
        if (pendingAccount)
        {
            TempData["RegisterEmail"] = emailNorm;
            TempData["RegisterMessage"] = "Tài khoản đã được tạo và đang chờ xác nhận email. Nhập mã đã nhận hoặc chọn Gửi lại mã. Mật khẩu vẫn là mật khẩu của lần đăng ký trước.";
            return RedirectToAction(nameof(ConfirmEmail));
        }

        if (settings.EnableDuplicateCheck)
        {
            if (db.TaiKhoans.Any(user => !user.IsDeleted && user.Email.Trim().ToLower() == emailNorm))
                ModelState.AddModelError("Email", "Email đã được sử dụng");
            if (db.TaiKhoans.Any(user => !user.IsDeleted && user.SoDienThoai == phoneNorm))
                ModelState.AddModelError("SoDienThoai", "Số điện thoại đã được sử dụng");
        }

        if (!ModelState.IsValid)
            return View(model);

        var now = DateTime.UtcNow;
        var user = new TaiKhoan
        {
            HoTen = model.HoTen!,
            Email = emailNorm,
            SoDienThoai = phoneNorm,
            MatKhau = BCrypt.Net.BCrypt.HashPassword(model.MatKhau!),
            VaiTro = "KHACH_THUE",
            DangHoatDong = true,
            IsStaff = false,
            IsSuperuser = false,
            EmailConfirmed = false,
            LastLogin = null,
            NgayTao = now,
            NgayCapNhat = now
        };

        db.TaiKhoans.Add(user);
        db.SelfRegisteringAccount = user;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (DatabaseErrors.IsConstraint(ex.InnerException))
        {
            ModelState.AddModelError("", "Email hoặc số điện thoại đã được sử dụng.");
            return View(model);
        }

        var code = Random.Shared.Next(100000, 1000000).ToString();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code)));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO email_confirmation(account_id,token_hash,expires_at,requested_at) VALUES ({user.Id},{hash},{DateTime.UtcNow.AddMinutes(15).Ticks},{DateTime.UtcNow.Ticks})");
        await SendConfirmationNoticeAsync(user, code);
        TempData["RegisterEmail"] = user.Email;
        return RedirectToAction(nameof(ConfirmEmail));
    }

    [HttpGet]
    public IActionResult ConfirmEmail(string? email) => View(new EmailConfirmationViewModel { Email = email ?? TempData["RegisterEmail"] as string ?? "" });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmEmail(EmailConfirmationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        await using var tx = await db.Database.BeginProtectedTransactionAsync();
        var email = model.Email.Trim().ToLowerInvariant();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(model.Code?.Trim() ?? "")));
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Email.Trim().ToLower() == email && !a.EmailConfirmed && a.DangHoatDong);
        if (account is null) { ModelState.AddModelError("", "Thông tin xác nhận không hợp lệ."); return View(model); }
        var valid = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM email_confirmation WHERE account_id = {0} AND token_hash = {1} AND expires_at > {2}", account.Id, hash, DateTime.UtcNow.Ticks).SingleAsync() == 1;
        if (!valid)
        {
            if (await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM email_confirmation WHERE account_id = {0} AND expires_at <= {1}", account.Id, DateTime.UtcNow.Ticks).SingleAsync() > 0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tai_khoan SET dang_hoat_dong=0, is_deleted=1 WHERE id={account.Id} AND email_confirmed=0 AND is_deleted=0");
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM email_confirmation WHERE account_id={account.Id}");
                await tx.CommitAsync();
                ModelState.AddModelError("", "Mã đã hết hạn. Tài khoản chờ xác nhận đã hủy; vui lòng đăng ký lại.");
            }
            else ModelState.AddModelError("Code", "Mã không đúng hoặc đã hết hạn.");
            return View(model);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tai_khoan SET email_confirmed = 1 WHERE id = {account.Id} AND is_deleted=0 AND dang_hoat_dong=1");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM email_confirmation WHERE account_id={account.Id}");
        await tx.CommitAsync();
        TempData["PasswordSuccess"] = "Xác nhận thành công. Hãy đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(string? email)
    {
        TempData.Remove("RegisterMessage");
        TempData.Remove("RegisterError");
        email = email?.Trim() ?? "";
        TempData["RegisterEmail"] = email;
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            TempData["RegisterError"] = "Vui lòng nhập địa chỉ email hợp lệ để gửi lại mã.";
            return RedirectToAction(nameof(ConfirmEmail));
        }
        var account = await db.TaiKhoans.SingleOrDefaultAsync(a => !a.IsDeleted && a.Email.Trim().ToLower() == email.Trim().ToLowerInvariant() && !a.EmailConfirmed && a.DangHoatDong);
        if (account is not null)
        {
            var code = Random.Shared.Next(100000, 1000000).ToString();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code)));
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM email_confirmation WHERE account_id={account.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO email_confirmation(account_id,token_hash,expires_at,requested_at) VALUES ({account.Id},{hash},{DateTime.UtcNow.AddMinutes(15).Ticks},{DateTime.UtcNow.Ticks})");
            await SendConfirmationNoticeAsync(account, code);
        }
        else TempData["RegisterMessage"] = "Yêu cầu đã được xử lý. Chỉ tài khoản đang chờ xác nhận mới được cấp mã. Nếu đã xác nhận, hãy đăng nhập.";
        return RedirectToAction(nameof(ConfirmEmail));
    }

    private async Task SendConfirmationNoticeAsync(TaiKhoan account, string code)
    {
        TempData.Remove("RegisterMessage");
        TempData.Remove("RegisterError");
        try
        {
            var delivery = await registrationEmail.SendConfirmationAsync(account.Email, account.HoTen, code);
            TempData["RegisterMessage"] = delivery.Mode == EmailDeliveryMode.Pickup
                ? "Chế độ thử nghiệm: mã chỉ được lưu vào file email trên máy chạy ứng dụng, chưa gửi đến hộp thư. Cần cấu hình SMTP để nhận email thật."
                : "Máy chủ gửi thư đã nhận email chứa mã. Kiểm tra Hộp thư đến và Spam; trạng thái này chưa xác nhận thư đã tới hộp thư.";
            if (delivery.Mode == EmailDeliveryMode.Pickup)
            {
                TempData["RegisterPickupDirectory"] = delivery.PickupDirectory;
                TempData["RegisterPickupFile"] = delivery.PickupFilePath;
            }
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            TempData["RegisterError"] = ex switch
            {
                OperationCanceledException => "Gửi mã quá thời gian chờ 15 giây. Kiểm tra kết nối SMTP rồi thử lại.",
                System.Net.Mail.SmtpException => "Không gửi được mã qua SMTP. Kiểm tra máy chủ, tài khoản gửi và mật khẩu ứng dụng Gmail rồi thử lại.",
                _ => "Không gửi được mã: cấu hình gửi email hoặc thư mục lưu thư chưa hợp lệ. Tài khoản vẫn đang chờ xác nhận."
            };
        }
    }
}
