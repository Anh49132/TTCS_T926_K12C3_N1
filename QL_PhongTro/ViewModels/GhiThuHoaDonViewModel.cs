namespace QL_PhongTro.ViewModels;

public sealed class GhiThuHoaDonInput
{
    public int HoaDonId { get; set; }
    public decimal? SoTien { get; set; }
    public DateOnly? NgayThu { get; set; }
    public string? HinhThuc { get; set; }
    public string? GhiChu { get; set; }
    public string? KhoaChongTrung { get; set; }
}

public sealed record LanThuHoaDonItem(int Id, long SoTien, DateOnly NgayThu, string HinhThuc,
    string? GhiChu, string NguoiXacNhan, DateTime? ThoiDiemGhi, bool DaHuy = false,
    string? LyDoHuy = null, string? NguoiHuy = null, DateTime? ThoiDiemHuy = null, int PhienBan = 0,
    bool ChoPhepHuy = false);

public sealed class HuyLanThuHoaDonInput
{
    public int ThanhToanId { get; set; }
    public int? PhienBan { get; set; }
    public string? LyDo { get; set; }
}

public sealed record ThanhToanHoaDonSummary(long TongHoaDon, long TongDaThu, long ConPhaiTra,
    string TrangThai, string TenTrangThai, bool ChoPhepGhiThu, bool SanSang,
    IReadOnlyList<LanThuHoaDonItem> LanThus, bool HuySanSang = false);

public sealed class GhiThuValidationException(Dictionary<string, string[]> errors) : Exception("Thông tin ghi thu chưa hợp lệ.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
