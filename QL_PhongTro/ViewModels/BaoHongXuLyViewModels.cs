using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.ViewModels;

public sealed class BaoHongPhanHoiInput
{
    [Required(ErrorMessage = "Vui lòng nhập phản hồi."), StringLength(5000, ErrorMessage = "Phản hồi tối đa 5.000 ký tự.")]
    public string? NoiDung { get; set; }
    public string KhoaChongTrung { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class BaoHongTrangThaiInput
{
    [Required(ErrorMessage = "Vui lòng chọn trạng thái tiếp theo.")]
    public string? TrangThai { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập ghi chú."), StringLength(5000, ErrorMessage = "Ghi chú tối đa 5.000 ký tự.")]
    public string? GhiChu { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Phiên bản không hợp lệ.")]
    public int PhienBan { get; set; }
    public string KhoaChongTrung { get; set; } = Guid.NewGuid().ToString("N");
}
