namespace QL_PhongTro.ViewModels;

public sealed class XacNhanTatToanInput
{
    public int HopDongId { get; set; }
    public int PhienBan { get; set; }
    public string? DauVanTay { get; set; }
    public bool DaKiemTra { get; set; }
    public bool DongY { get; set; }
}

public sealed class HoanCocTatToanInput
{
    public int HopDongId { get; set; }
    public decimal? SoTien { get; set; }
    public string? HinhThuc { get; set; }
    public string? KhoaChongTrung { get; set; }
    public bool DaHoan { get; set; }
}
