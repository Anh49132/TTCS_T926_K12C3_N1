using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels.Auth;

public class NewPasswordViewModel
{
    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [RegularExpression(@"(?s)^(?=.*[A-Za-z])(?=.*\d).{8,}$", ErrorMessage = "Mật khẩu tối thiểu 8 ký tự, ít nhất một chữ cái và một chữ số.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Nhập xác nhận mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class ChangePasswordViewModel : NewPasswordViewModel
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";
}
