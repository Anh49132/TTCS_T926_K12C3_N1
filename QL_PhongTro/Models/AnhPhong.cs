using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.Models;

public class AnhPhong
{
    [Key]
    public int Id { get; set; }

    public int PhongId { get; set; }

    [Required, MaxLength(500)]
    public string DuongDan { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DuongDanAnhNho { get; set; }

    public int ThuTu { get; set; }

    [MaxLength(255)]
    public string? MoTa { get; set; }

    public DateTime NgayTao { get; set; }

    public bool DangChoXoa { get; set; }

    [MaxLength(1000)]
    public string? LoiXoaGanNhat { get; set; }

    public DateTime? LanThuXoaGanNhat { get; set; }
}
