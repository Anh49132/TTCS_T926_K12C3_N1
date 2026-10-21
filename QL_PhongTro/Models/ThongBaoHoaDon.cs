using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

public static class LoaiThongBaoHoaDon
{
    public const string HoaDonMoi = "HOA_DON_MOI";
    public const string SapDenHan = "TRUOC_HAN_2_NGAY";
    public const string QuaHan = "QUA_HAN";
}

[Table("thong_bao")]
public sealed class ThongBaoHoaDon
{
    [Column("id")] public int Id { get; set; }
    [Column("nguoi_nhan_id")] public int NguoiNhanId { get; set; }
    [Column("hoa_don_id")] public int HoaDonId { get; set; }
    [Column("loai_thong_bao")] public string LoaiThongBao { get; set; } = "HOA_DON_MOI";
    [Column("tieu_de")] public string TieuDe { get; set; } = "";
    [Column("noi_dung")] public string NoiDung { get; set; } = "";
    [Column("duong_dan")] public string DuongDan { get; set; } = "";
    [Column("email_nhan")] public string EmailNhan { get; set; } = "";
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("ngay_doc")] public DateTime? NgayDoc { get; set; }
    [Column("trang_thai_email")] public string TrangThaiEmail { get; set; } = "CHO_GUI";
    [Column("so_lan_gui")] public int SoLanGui { get; set; }
    [Column("lan_gui_tiep_theo")] public DateTime? LanGuiTiepTheo { get; set; }
    [Column("khoa_xu_ly_den")] public DateTime? KhoaXuLyDen { get; set; }
    [Column("ngay_gui_thanh_cong")] public DateTime? NgayGuiThanhCong { get; set; }
    [Column("loi_gan_nhat")] public string? LoiGanNhat { get; set; }
}
