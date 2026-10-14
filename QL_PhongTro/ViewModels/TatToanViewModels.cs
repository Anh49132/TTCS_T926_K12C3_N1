using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public sealed class KhauTruInput
{
    public string? TenKhoan { get; set; }
    public decimal? SoTien { get; set; }
    public string? GhiChu { get; set; }
}
public sealed class TatToanInput
{
    public int HopDongId { get; set; }
    public int PhienBan { get; set; }
    public string? DauVanTayNguon { get; set; }
    public string? GhiChu { get; set; }
    public List<KhauTruInput> KhauTru { get; set; } = [];
}
public sealed record CongNoTatToan(int HoaDonId, string MaHoaDon, int Nam, int Thang, long TongTien, long DaThanhToan, long DaCanTru, long ConNo, bool KyCuoi);
public sealed class TatToanViewModel
{
    public TatToanInput Input { get; set; } = new();
    public string MaHopDong { get; set; } = "";
    public string Phong { get; set; } = "";
    public string ToaNha { get; set; } = "";
    public string Khach { get; set; } = "";
    public bool DuocSua { get; set; }
    public BangTatToan Bang { get; set; } = new();
    public List<CongNoTatToan> CongNo { get; set; } = [];
    public long KetQua => checked(Bang.TienPhaiHoan - Bang.TienKhachTraThem);
}
public sealed record TatToanChon(int Id, string MaHopDong, string Phong, string ToaNha, bool DaLuu);
