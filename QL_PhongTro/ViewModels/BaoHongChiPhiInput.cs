using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.ViewModels;

public sealed class BaoHongChiPhiInput
{
    [Required(ErrorMessage = "Nhập số tiền sửa chữa.")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "Số tiền phải là số nguyên VND lớn hơn 0.")]
    public string? SoTien { get; set; }
    [Required(ErrorMessage = "Chọn bên chịu chi phí.")]
    public string? BenChiuPhi { get; set; }
    public int PhienBan { get; set; }
    public string KhoaChongTrung { get; set; } = Guid.NewGuid().ToString("N");
}
