namespace QL_PhongTro.ViewModels;
public record LichSuNguoiORow(string HoTen, string VaiTro, int HopDongId, string MaHopDong,
    DateOnly NgayBatDau, DateOnly NgayKetThuc, bool DangO, bool KetThucTheoHanHopDong);
public class LichSuNguoiOViewModel
{
    public int PhongId { get; set; }
    public string Phong { get; set; } = "";
    public string ToaNha { get; set; } = "";
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public List<LichSuNguoiORow> Nguois { get; set; } = [];
    public bool DaLoc { get; set; }
    public int HopDongThieuDuLieu { get; set; }
}
