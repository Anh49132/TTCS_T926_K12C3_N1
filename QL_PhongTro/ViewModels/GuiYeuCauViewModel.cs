using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public class GuiYeuCauViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn loại yêu cầu.")]
    [RegularExpression("^(XEM_PHONG|THUE_NGAY)$", ErrorMessage = "Loại yêu cầu không hợp lệ.")]
    public string? LoaiYeuCau { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập ngày mong muốn.")]
    public DateOnly? NgayMongMuon { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập số người dự kiến ở.")]
    [Range(1, int.MaxValue, ErrorMessage = "Số người dự kiến ở phải là số nguyên dương.")]
    public int? SoNguoiDuKien { get; set; }
    [StringLength(2000, ErrorMessage = "Lời nhắn tối đa 2.000 ký tự.")]
    public string? LoiNhan { get; set; }
}

public record ChiTietTinDangViewModel(TinDang Tin, PhongTro Phong, GuiYeuCauViewModel Form, DateOnly Today)
{
    public TinDangChiTietViewModel PublicDetail { get; init; } = null!;
    public bool RequestModuleInstalled { get; init; }
    public int? OpenRequestId { get; init; }
    public bool TinConCongKhai { get; init; } = true;
}
