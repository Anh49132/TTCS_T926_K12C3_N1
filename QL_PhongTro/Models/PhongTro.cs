using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.Models;

public class PhongTro
{
    [Key]
    public int Id { get; set; }

    public int ToaNhaId { get; set; }

    [Required, MaxLength(20)]
    public string MaPhong { get; set; } = string.Empty;

    public int Tang { get; set; }

    [MaxLength(50)]
    public string? LoaiPhong { get; set; }

    public decimal DienTich { get; set; }
    public long GiaThue { get; set; }
    public long TienCocDuKien { get; set; }
    public int SoNguoiToiDa { get; set; }

    [Required, MaxLength(25)]
    public string TrangThai { get; set; } = "TRONG";

    public string? MoTa { get; set; }
    public string? GhiChuNoiBo { get; set; }
    public string? TienNghi { get; set; }
    public DateTime NgayTao { get; set; }
    public int PhienBan { get; set; }
}
