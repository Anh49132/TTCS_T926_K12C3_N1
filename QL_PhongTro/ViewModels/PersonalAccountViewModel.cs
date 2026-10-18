using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace QL_PhongTro.ViewModels;
public sealed class PersonalAccountViewModel
{
    [Required(ErrorMessage="Nhập họ tên."),StringLength(100,ErrorMessage="Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = "";
    [Required(ErrorMessage="Nhập số điện thoại."),RegularExpression("^0[0-9]{9}$",ErrorMessage="Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.")]
    public string SoDienThoai { get; set; } = "";
    [Required] public DateTime? OriginalUpdated { get; set; }
    [BindNever] public string Email { get; set; } = "";
    [BindNever] public string Role { get; set; } = "";
}
