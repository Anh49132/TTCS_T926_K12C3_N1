namespace QL_PhongTro.ViewModels;

public sealed record ChiTietHoaDonKhachPageViewModel(
    string MaHoaDon,
    string? Ky = null,
    string TrangThai = "TAT_CA",
    int Page = 1);

public static class TrangThaiThanhToanHoaDon
{
    public const string TatCa = "TAT_CA";
    public const string ChuaThanhToan = "CHUA_THANH_TOAN";
    public const string ThanhToanMotPhan = "THANH_TOAN_MOT_PHAN";
    public const string DaThanhToan = "DA_THANH_TOAN";
    public const string QuaHan = "QUA_HAN";
    public const string ChoXacNhan = "CHO_XAC_NHAN";
}

public sealed record HoaDonKhachDanhSachPageViewModel(
    string? Ky,
    string TrangThai,
    int Page,
    string? LoiLoc);

public sealed record HoaDonKhachDanhSachItem(
    string MaHoaDon,
    int Thang,
    int Nam,
    long TongCong,
    long SoConPhaiTra,
    string HanThanhToan,
    string TrangThai,
    string TenTrangThai,
    bool QuaHan,
    int SoNgayTre);

public sealed record HoaDonKhachDanhSachResponse(
    int TongSoHoaDon,
    int SoKetQua,
    int SoTrang,
    int Trang,
    IReadOnlyList<HoaDonKhachDanhSachItem> HoaDons);

public sealed record ChiTietHoaDonKhachViewModel(
    string MaHoaDon,
    int Thang,
    int Nam,
    string MaPhong,
    IReadOnlyList<ChiTietHoaDonKhachLineResponse> ChiTiet,
    long TongCong,
    long SoDaThanhToan,
    long SoConPhaiTra,
    string HanThanhToan,
    bool QuaHan,
    int SoNgayTre,
    int HoaDonId,
    bool ChoPhepBaoCaoThanhToan,
    bool ChoXacNhan,
    long? SoTienBaoCaoChoXacNhan,
    string? NgayBaoCaoChoXacNhan,
    string? HinhThucBaoCaoChoXacNhan,
    string HomNay);

public sealed record ChiTietHoaDonKhachLineViewModel(
    string TenKhoan,
    decimal? ChiSoDau,
    decimal? ChiSoCuoi,
    decimal SoLuong,
    string? DonViTinh,
    string LoaiKhoan,
    long DonGia,
    long ThanhTien);

public sealed record ChiTietHoaDonKhachLineResponse(
    string TenKhoan,
    string? ChiSoDau,
    string? ChiSoCuoi,
    string SoLuongTieuThu,
    string DonViTinh,
    string DonGia,
    string ThanhTien,
    string LoaiKhoan);
