using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Controllers;

public partial class AccountController
{
    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, [FromServices] PasswordResetService passwords)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            if (!await passwords.RequestAsync(model.Email))
            {
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ModelState.AddModelError("", "Email này đã yêu cầu đặt lại mật khẩu 3 lần trong một giờ. Vui lòng thử lại khi yêu cầu cũ nằm ngoài khoảng một giờ.");
                return View(model);
            }
            TempData["ResetRequestMessage"] = PasswordResetService.AcceptedMessage;
            return RedirectToAction(nameof(ForgotPassword));
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            ModelState.AddModelError("", "Chưa thể gửi email đặt lại mật khẩu. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
            return View(model);
        }
    }

    [HttpGet, AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ResetPassword(string? token, [FromServices] PasswordResetService passwords)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!passwords.IsValid(token)) return InvalidResetLink();
        return View(new ResetPasswordViewModel { Token = token! });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ResetPassword(ResetPasswordViewModel model, [FromServices] PasswordResetService passwords)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!passwords.IsValid(model.Token)) return InvalidResetLink();
        if (!ModelState.IsValid) return View(model);
        if (!passwords.Reset(model.Token, model.NewPassword)) return InvalidResetLink();
        TempData["PasswordSuccess"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult InvalidResetLink()
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("InvalidResetLink");
    }
}
