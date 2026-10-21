using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("ngung_dich_vu_phong")]
public class NgungDichVuPhong
{
    [Column("id")] public int Id { get; set; }
    [Column("dich_vu_phong_id")] public int DichVuPhongId { get; set; }
    public DichVuPhong DichVuPhong { get; set; } = null!;
    [Column("yeu_cau_luc_utc")] public DateTime YeuCauLucUtc { get; set; }
    [Column("ngung_tu_ky")] public DateOnly NgungTuKy { get; set; }
    [Column("ap_dung_lai_tu_ky")] public DateOnly? ApDungLaiTuKy { get; set; }
    [Column("ap_dung_lai_luc_utc")] public DateTime? ApDungLaiLucUtc { get; set; }
}
