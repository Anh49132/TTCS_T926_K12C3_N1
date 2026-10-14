using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("lich_su_chi_phi_bao_hong")]
public sealed class LichSuChiPhiBaoHong
{
    [Column("id")] public int Id { get; set; }
    [Column("chi_phi_id")] public int ChiPhiId { get; set; }
    [Column("nguoi_cap_nhat_id")] public int NguoiCapNhatId { get; set; }
    [Column("thoi_diem")] public DateTime ThoiDiem { get; set; }
    [Column("loai")] public string Loai { get; set; } = "";
    [Column("so_tien_truoc")] public long? SoTienTruoc { get; set; }
    [Column("ben_chiu_phi_truoc")] public string? BenChiuPhiTruoc { get; set; }
    [Column("ky_du_kien_truoc")] public DateOnly? KyDuKienTruoc { get; set; }
    [Column("so_tien_sau")] public long SoTienSau { get; set; }
    [Column("ben_chiu_phi_sau")] public string BenChiuPhiSau { get; set; } = "";
    [Column("ky_du_kien_sau")] public DateOnly? KyDuKienSau { get; set; }
    [Column("phien_ban_truoc")] public int PhienBanTruoc { get; set; }
    [Column("phien_ban_sau")] public int PhienBanSau { get; set; }
    [Column("khoa_chong_trung")] public string KhoaChongTrung { get; set; } = "";
}
