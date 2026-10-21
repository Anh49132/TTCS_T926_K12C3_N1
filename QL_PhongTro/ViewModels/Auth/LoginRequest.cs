using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Nhập email hoặc số điện thoại.")]
    public string? TaiKhoanDangNhap { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string? MatKhau { get; set; }

    public string? ReturnUrl { get; set; }
    public bool RememberMe { get; set; } = true;
}
