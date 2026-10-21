using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;
public sealed record CheckoutContract(int Id, string MaHopDong, string Phong, string ToaNha, DateOnly NgayKetThuc, string TrangThai);
public sealed record CheckoutRequestRow(YeuCauTraPhong Request, string MaHopDong, string Phong, string ToaNha, string Khach, string TrangThaiHopDong);
public sealed class CheckoutRequestViewModel
{
    public List<CheckoutContract> Contracts { get; set; } = [];
    public List<CheckoutRequestRow> Rows { get; set; } = [];
    public int? HopDongId { get; set; }
    public DateOnly HomNay { get; set; }
    public bool IsOwner { get; set; }
    public int Page { get; set; } = 1;
}
