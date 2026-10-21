using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed class CreateManagedAccountViewModel
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(100)]
    public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Nhập email."), EmailAddress(ErrorMessage = "Email không đúng định dạng."), StringLength(254)]
    public string Email { get; set; } = "";
    [Required, RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.")]
    public string SoDienThoai { get; set; } = "";
    [Required, RegularExpression("^(CHU_NHA|QUAN_LY|ADMIN)$", ErrorMessage = "Chỉ được tạo Chủ nhà, Quản lý toà nhà hoặc Admin.")]
    public string VaiTro { get; set; } = "CHU_NHA";
}

public sealed class ManagedAccountsViewModel
{
    public List<TaiKhoan> Accounts { get; set; } = [];
    public string? Role { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / 20d));
    public static string RoleName(string? role) => role switch
    {
        "CHU_NHA" => "Chủ nhà", "QUAN_LY" => "Quản lý toà nhà",
        "ADMIN" => "Quản trị viên", "KHACH_THUE" => "Khách thuê", _ => role ?? ""
    };
}

public sealed class ChangeAccountRoleViewModel
{
    public int Id { get; set; }
    public string HoTen { get; set; } = "";
    [Required]
    public string OriginalRole { get; set; } = "";
    [Required, RegularExpression("^(KHACH_THUE|CHU_NHA|QUAN_LY|ADMIN)$", ErrorMessage = "Vai trò không hợp lệ.")]
    public string VaiTro { get; set; } = "";
}
