using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("bao_hong")]
public sealed class BaoHong
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_bao_hong")] public string MaBaoHong { get; set; } = "";
    [Column("nguoi_bao_id")] public int NguoiBaoId { get; set; }
    [Column("khach_thue_id")] public int KhachThueId { get; set; }
    [Column("phong_id")] public int PhongId { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("loai_thiet_bi")] public string LoaiThietBi { get; set; } = "";
    [Column("mo_ta")] public string MoTa { get; set; } = "";
    [Column("muc_do")] public string MucDo { get; set; } = "THUONG";
    [Column("trang_thai")] public string TrangThai { get; set; } = "MOI";
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("khoa_chong_trung")] public string KhoaChongTrung { get; set; } = "";
}
