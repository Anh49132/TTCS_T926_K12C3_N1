namespace QL_PhongTro.ViewModels;
public sealed class HoSoChuNhaDanhSachViewModel
{
    public string? TimKiem { get; set; }
    public int? ToaNhaId { get; set; }
    public string? TrangThai { get; set; }
    public int Trang { get; set; }
    public int TongTrang { get; set; }
    public int TongKetQua { get; set; }
    public int TongKhach { get; set; }
    public int DangThue { get; set; }
    public int DaKetThuc { get; set; }
    public int ThieuHoSo { get; set; }
    public List<HoSoChuNhaRow> Khachs { get; set; } = [];
    public List<(int Id, string Ten)> ToaNhas { get; set; } = [];
}
public sealed record HoSoChuNhaRow(int Id, string HoTen, string DienThoai, string CanCuoc, DateOnly? NgaySinh,
    string Phong, string ToaNha, string VaiTro, string TinhTrang, bool DayDu);
public sealed record HoSoHopDongRow(int Id, string Ma, int ToaNhaId, string ToaNha, string Phong, string VaiTro,
    string TrangThai, DateOnly? BatDau, DateOnly? KetThuc, bool DangO, int SucChua, int SoNguoi);
public sealed record HoSoCaNhanViewModel(int Id, string HoTen, string VaiTro, string DienThoai, string Email,
    bool EmailDaXacThuc, DateTime NgayTao, DateTime? DangNhapGanNhat);
