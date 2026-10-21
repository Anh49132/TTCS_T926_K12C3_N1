using Microsoft.AspNetCore.Mvc.Rendering;
namespace QL_PhongTro.ViewModels;
public class YeuCauListItemViewModel
{
    public int Id { get; set; }
    public string MaYeuCau { get; set; } = ""; public string TenKhach { get; set; } = ""; public string SoDienThoai { get; set; } = "";
    public string MaPhong { get; set; } = ""; public string LoaiYeuCau { get; set; } = ""; public DateOnly? NgayMongMuon { get; set; }
    public string TrangThai { get; set; } = ""; public DateTime NgayTao { get; set; }
    public string? Email { get; set; }
    public string? NgheNghiep { get; set; }
    public string? AnhPhong { get; set; }
    public decimal DienTich { get; set; }
    public long GiaThue { get; set; }
    public int SoNguoiDuKien { get; set; }
    public string? LoiNhan { get; set; }
    public bool QuaHanChuaXuLy { get; set; }
    public string TenToaNha { get; set; } = "";
    public DateTime? LichHen { get; set; }
    public bool DaLapHopDong { get; set; }
}
public class DanhSachYeuCauViewModel
{
    public IReadOnlyList<YeuCauListItemViewModel> YeuCaus { get; set; } = [];
    public IReadOnlyList<YeuCauThueItemViewModel> YeuCausCuaKhach { get; set; } = [];
    public bool LaKhachThue { get; set; }
    public string? TrangThai { get; set; }
    public int? ToaNhaId { get; set; }
    public IReadOnlyList<SelectListItem> ToaNhaOptions { get; set; } = [];
    public IReadOnlyList<YeuCauListItemViewModel> TongHop { get; set; } = [];
    public DateTime NowUtc { get; set; }
}
