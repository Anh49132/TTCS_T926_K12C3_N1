using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed class ChiTietHoaDonViewModel
{
    public ThanhToanHoaDonSummary? ThanhToan { get; set; }
    public DateOnly HomNay { get; set; }
    public string TenNguoiGhiThu { get; set; } = "";
    public IReadOnlyList<LichSuHoaDonNhapViewModel> LichSuNhap { get; set; } = [];
    public required HoaDon HoaDon { get; init; }
    public int ToaNhaId { get; init; }
    public string MaPhong { get; init; } = "";
    public string TenToaNha { get; init; } = "";
    public string TenKhach { get; init; } = "";
    public bool XemTruoc { get; init; }
    public bool KhachXem { get; init; }
    public string? LyDoChanHuy { get; set; }
    public int? HoaDonGocId { get; set; }
    public string? MaHoaDonGoc { get; set; }
    public int? HoaDonThayTheId { get; set; }
    public string? MaHoaDonThayThe { get; set; }
    public string? TrangThaiThayThe { get; set; }
    public string ReviewToken { get; init; } = "";
    public long TongCong { get; init; }
    public long SoDaThanhToan { get; init; }
    public long SoConPhaiTra { get; init; }
    public bool QuaHan { get; init; }
    public int SoNgayTre { get; init; }
}
