using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels;

public sealed class TienDoChiSoViewModel
{
    [Range(1900, 9998, ErrorMessage = "Năm theo dõi phải từ 1900 đến 9998.")]
    public int Nam { get; set; }

    [Range(1, 12, ErrorMessage = "Tháng theo dõi không hợp lệ.")]
    public int Thang { get; set; }

    public DateTime CapNhatLuc { get; set; }
    public List<HomeMeterAlert> CanhBaos { get; set; } = [];
    public List<TienDoChiSoToaNha> ToaNhas { get; set; } = [];
}

public sealed record TienDoChiSoToaNha(
    int ToaNhaId,
    string TenToaNha,
    int TongPhongDangThue,
    int SoPhongDaChot,
    int SoPhongConThieu,
    bool KyDaKhoa)
{
    public string? TenQuanLy { get; init; }
}

public sealed record PhongConThieuViewModel(
    int ToaNhaId,
    string TenToaNha,
    int Nam,
    int Thang,
    List<PhongConThieuItem> Phongs);

public sealed record PhongConThieuItem(
    int PhongId,
    string MaPhong,
    int Tang,
    string? TenQuanLy);
