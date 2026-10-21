using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.Models;

public class TinDang
{
    [Key]
    public int Id { get; set; }

    public int PhongId { get; set; }
    public int NguoiDangId { get; set; }
    public int? TinGocId { get; set; }

    [Required, MaxLength(200)]
    public string TieuDe { get; set; } = string.Empty;

    public string? NoiDung { get; set; }
    public DateTime? NgayDang { get; set; }
    public DateTime? NgayHetHan { get; set; }

    [Required, MaxLength(25)]
    public string TrangThai { get; set; } = "NHAP";

    public DateTime NgayTao { get; set; }
}