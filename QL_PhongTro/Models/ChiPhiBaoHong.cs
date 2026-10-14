using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("chi_phi_bao_hong")]
public sealed class ChiPhiBaoHong
{
    [Column("id")] public int Id { get; set; }
    [Column("bao_hong_id")] public int BaoHongId { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("so_tien")] public long SoTien { get; set; }
    [Column("ben_chiu_phi")] public string BenChiuPhi { get; set; } = "";
    // Eligibility to charge the tenant. Actual allocation is derived by the SQL view.
    [Column("cho_thu_khach")] public bool ChoThuKhach { get; set; }
    [Column("ky_du_kien")] public DateOnly? KyDuKien { get; set; }
    [Column("nguoi_tao_id")] public int NguoiTaoId { get; set; }
    [Column("nguoi_cap_nhat_id")] public int NguoiCapNhatId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("ngay_cap_nhat")] public DateTime NgayCapNhat { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
}
