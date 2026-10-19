using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels.Auth;

namespace QL_PhongTro.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService, JwtSettings jwtSettings) : ControllerBase
{
    private readonly AuthService _authService = authService;
    private readonly JwtSettings _jwtSettings = jwtSettings;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.TaiKhoanDangNhap))
        {
            return BadRequest(new AuthErrorResponse { Error = "VALIDATION_ERROR", Message = "Phải nhập email hoặc số điện thoại", Code = 400 });
        }

        if (string.IsNullOrEmpty(request?.MatKhau))
        {
            return BadRequest(new AuthErrorResponse { Error = "VALIDATION_ERROR", Message = "Nhập mật khẩu.", Code = 400 });
        }

        var (success, response, error) = await _authService.LoginAsync(request);

        if (!success)
        {
            return Unauthorized(new AuthErrorResponse { Error = "LOGIN_FAILED", Message = error, Code = 401 });
        }

        return Ok(response!);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return BadRequest(new AuthErrorResponse { Error = "VALIDATION_ERROR", Message = "Refresh token là bắt buộc", Code = 400 });
        }

        var (success, response, error) = await _authService.RefreshTokenAsync(request.RefreshToken);

        if (!success)
        {
            return Unauthorized(new AuthErrorResponse { Error = "TOKEN_EXPIRED", Message = error, Code = 401 });
        }

        return Ok(response!);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var refreshToken = request?.RefreshToken ?? Request.Headers["X-Refresh-Token"].ToString();
        var accessToken = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new AuthErrorResponse { Error = "VALIDATION_ERROR", Message = "Refresh token là bắt buộc", Code = 400 });
        }

        await _authService.LogoutAsync(refreshToken, accessToken);

        return Ok(new LogoutResponse { Success = true, Message = "Đăng xuất thành công" });
    }

    [HttpPost("me")]
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { user_id = userId, role });
    }
}

public class LogoutRequest
{
    public string? RefreshToken { get; set; }
}
