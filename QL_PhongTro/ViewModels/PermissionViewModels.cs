using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public class PermissionMatrixViewModel
{
    public List<AppRole> Roles { get; set; } = [];
    public List<AppModule> Modules { get; set; } = [];
    public List<RolePermission> Permissions { get; set; } = [];
}
public class LoginViewModel
{
    [Required(ErrorMessage = "Nhập email hoặc số điện thoại.")]
    public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

