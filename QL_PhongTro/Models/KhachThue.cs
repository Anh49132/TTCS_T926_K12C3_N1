namespace QL_PhongTro.Models;

public class KhachThue
{
    public int Id { get; set; }
    public int? TaiKhoanId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateOnly? NgaySinh { get; set; }
    public string? SoGiayTo { get; set; }
    public string? AnhGiayToTruoc { get; set; }
    public string? AnhGiayToSau { get; set; }
    public string? QueQuan { get; set; }
    public string? NgheNghiep { get; set; }
    public string? SoDienThoai { get; set; }
    public DateTime NgayTao { get; set; }
}
