using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("bao_cao_thanh_toan")]
public sealed class BaoCaoThanhToan
{
    [Key, Column("thanh_toan_id")] public int ThanhToanId { get; set; }
    [Column("nguoi_gui_id")] public int? NguoiGuiId { get; set; }
    [Column("gui_luc")] public DateTime? GuiLuc { get; set; }
    [Column("ghi_chu")] public string? GhiChu { get; set; }
    [Column("ten_tep")] public string? TenTep { get; set; }
    [Column("ly_do_tu_choi")] public string? LyDoTuChoi { get; set; }
    [Column("nguoi_xu_ly_id")] public int? NguoiXuLyId { get; set; }
    [Column("xu_ly_luc")] public DateTime? XuLyLuc { get; set; }
}

[Table("thong_bao_thanh_toan")]
public sealed class ThongBaoThanhToan
{
    [Column("id")] public int Id { get; set; }
    [Column("thanh_toan_id")] public int ThanhToanId { get; set; }
    [Column("nguoi_nhan_id")] public int NguoiNhanId { get; set; }
    [Column("loai")] public string Loai { get; set; } = "";
    [Column("tieu_de")] public string TieuDe { get; set; } = "";
    [Column("noi_dung")] public string NoiDung { get; set; } = "";
    [Column("duong_dan")] public string DuongDan { get; set; } = "";
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("ngay_doc")] public DateTime? NgayDoc { get; set; }
}
