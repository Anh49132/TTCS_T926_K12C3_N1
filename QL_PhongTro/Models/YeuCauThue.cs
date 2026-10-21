using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("yeu_cau_thue")]
public class YeuCauThue
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_yeu_cau"), Required] public string MaYeuCau { get; set; } = "";
    [Column("tin_dang_id")] public int TinDangId { get; set; }
    [Column("khach_thue_id")] public int KhachThueId { get; set; }
    [Column("loai_yeu_cau"), Required] public string LoaiYeuCau { get; set; } = "";
    [Column("ngay_mong_muon")] public DateOnly NgayMongMuon { get; set; }
    [Column("so_nguoi_du_kien")] public int SoNguoiDuKien { get; set; }
    [Column("loi_nhan")] public string? LoiNhan { get; set; }
    [Column("lich_hen")] public DateTime? LichHen { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "MOI";
    [Column("ly_do_tu_choi")] public string? LyDoTuChoi { get; set; }
    [Column("nguoi_xu_ly_id")] public int? NguoiXuLyId { get; set; }
    [Column("ngay_xu_ly")] public DateTime? NgayXuLy { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
}
