using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("yeu_cau_tra_phong")]
public sealed class YeuCauTraPhong
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("nguoi_gui_id")] public int NguoiGuiId { get; set; }
    [Column("ngay_de_nghi")] public DateOnly NgayDeNghi { get; set; }
    [Column("ly_do")] public string LyDo { get; set; } = "";
    [Column("trang_thai")] public string TrangThai { get; set; } = "MOI";
    [Column("ngay_ban_giao")] public DateOnly? NgayBanGiao { get; set; }
    [Column("phan_hoi")] public string? PhanHoi { get; set; }
    [Column("nguoi_xu_ly_id")] public int? NguoiXuLyId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("cap_nhat_luc")] public DateTime CapNhatLuc { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
}
[Table("thong_bao_tra_phong")]
public sealed class ThongBaoTraPhong
{
    [Column("id")] public int Id { get; set; }
    [Column("yeu_cau_id")] public int YeuCauId { get; set; }
    [Column("nguoi_nhan_id")] public int NguoiNhanId { get; set; }
    [Column("loai")] public string Loai { get; set; } = "";
    [Column("tieu_de")] public string TieuDe { get; set; } = "";
    [Column("noi_dung")] public string NoiDung { get; set; } = "";
    [Column("duong_dan")] public string DuongDan { get; set; } = "";
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("ngay_doc")] public DateTime? NgayDoc { get; set; }
}
