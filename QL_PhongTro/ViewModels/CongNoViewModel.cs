namespace QL_PhongTro.ViewModels;

public sealed record CongNoRowViewModel(
    int PhongId, string MaPhong, string TenToaNha, string NguoiDungTen,
    int SoHoaDonChuaTraHet, long TongConThieu, int SoNgayQuaHanDaiNhat)
{
    public int ToaNhaId { get; init; }
    public IReadOnlyList<CongNoInvoice> Invoices { get; init; } = [];
    public bool QuaHanNhieuKy => Invoices.Where(x => x.SoNgayQuaHan > 0).Select(x => (x.Nam, x.Thang)).Distinct().Count() >= 2;
}

public sealed record CongNoInvoice(int Id, string MaHoaDon, int Thang, int Nam, string NguoiDungTen,
    long TongTien, long DaThu, long CanCoc, long ConThieu, DateOnly HanThanhToan, int SoNgayQuaHan, long ChoXacNhan);

public sealed class CongNoIndexViewModel
{
    public int? ToaNhaId { get; init; }
    public int? QuaHanTu { get; init; }
    public int? QuaHanDen { get; init; }
    public long? TienTu { get; init; }
    public long? TienDen { get; init; }
    public IReadOnlyList<BuildingOption> Buildings { get; init; } = [];
    public IReadOnlyList<CongNoRowViewModel> Rows { get; init; } = [];
    public string TimKiem { get; set; } = "";
    public string SapXep { get; set; } = "PHONG";
    public string TinhTrang { get; set; } = "TAT_CA";
    public bool LocLoi { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => Math.Max(1, (Rows.Count + PageSize - 1) / PageSize);
    public IEnumerable<CongNoRowViewModel> PageRows => Rows.Skip((Page - 1) * PageSize).Take(PageSize);
    public long TotalOutstanding => Rows.Sum(x => x.TongConThieu);
    public int TotalInvoices => Rows.Sum(x => x.SoHoaDonChuaTraHet);
    public sealed record BuildingOption(int Id, string Name);
}
