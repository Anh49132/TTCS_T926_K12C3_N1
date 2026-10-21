using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed class NhatKyPhongViewModel
{
    public int PhongId { get; set; }
    public string MaPhong { get; set; } = string.Empty;
    public string TenToaNha { get; set; } = string.Empty;
    public string TrangThaiPhong { get; set; } = string.Empty;
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (Total + 19) / 20);
    public List<NhatKyHoatDong> Rows { get; set; } = [];
}
