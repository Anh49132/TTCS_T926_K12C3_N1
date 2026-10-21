using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("hop_dong_chi_so_dau_ky")]
public class HopDongChiSoDauKy
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("ngay_ban_giao")] public DateOnly NgayBanGiao { get; set; }
    [Column("chi_so_dien")] public decimal ChiSoDien { get; set; }
    [Column("chi_so_nuoc")] public decimal ChiSoNuoc { get; set; }
    [Column("nguoi_nhap_id")] public int NguoiNhapId { get; set; }
    [Column("ngay_nhap")] public DateTime NgayNhap { get; set; }
}
