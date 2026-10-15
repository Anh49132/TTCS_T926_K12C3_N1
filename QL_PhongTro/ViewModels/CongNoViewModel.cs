namespace QL_PhongTro.ViewModels;

public sealed record CongNoRowViewModel(
    int PhongId, string MaPhong, string TenToaNha, string NguoiDungTen,
    int SoHoaDonChuaTraHet, long TongConThieu, int SoNgayQuaHanDaiNhat);

public sealed class CongNoIndexViewModel
{
    public int? ToaNhaId { get; init; }
    public int? QuaHanTu { get; init; }
    public int? QuaHanDen { get; init; }
    public long? TienTu { get; init; }
    public long? TienDen { get; init; }
    public IReadOnlyList<BuildingOption> Buildings { get; init; } = [];
    public IReadOnlyList<CongNoRowViewModel> Rows { get; init; } = [];
    public long TotalOutstanding => Rows.Sum(x => x.TongConThieu);
    public int TotalInvoices => Rows.Sum(x => x.SoHoaDonChuaTraHet);
    public sealed record BuildingOption(int Id, string Name);
}
