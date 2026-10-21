using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.ViewModels.Auth;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Nhập địa chỉ email.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [StringLength(254, ErrorMessage = "Email không được quá 254 ký tự.")]
    public string Email { get; set; } = "";
}
public sealed class ResetPasswordViewModel : NewPasswordViewModel
{
    [Required]
    public string Token { get; set; } = "";
}
