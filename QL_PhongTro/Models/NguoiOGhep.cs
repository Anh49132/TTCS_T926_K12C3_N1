using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;
[Table("nguoi_o_ghep")]
public class NguoiOGhep
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("khach_thue_id")] public int KhachThueId { get; set; }
    [Column("ngay_vao")] public DateOnly NgayVao { get; set; }
    [Column("ngay_ra")] public DateOnly? NgayRa { get; set; }
}
