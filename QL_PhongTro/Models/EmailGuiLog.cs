using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("email_gui_log")]
public class EmailGuiLog
{
    [Column("id")] public int Id { get; set; }

    [Column("hoa_don_id")] public int HoaDonId { get; set; }

    [Column("loai_thong_bao"), Required, MaxLength(40)] public string LoaiThongBao { get; set; } = string.Empty;

    [Column("nguoi_nhan_id")] public int NguoiNhanId { get; set; }

    [Column("email_nguoi_nhan"), Required, MaxLength(254)] public string EmailNguoiNhan { get; set; } = string.Empty;

    [Column("trang_thai"), Required, MaxLength(20)] public string TrangThai { get; set; } = string.Empty;

    [Column("so_lan_thu")] public int SoLanThu { get; set; }

    [Column("loi_gan_nhat")] public string? LoiGanNhat { get; set; }

    [Column("ngay_gui"), Required] public DateTime NgayGui { get; set; }

    [Column("ngay_thu_tiep")] public DateTime? NgayThuTiep { get; set; }
}

public static class TrangThaiEmail
{
    public const string ChoGui = "CHO_GUI";
    public const string DangGui = "DANG_GUI";
    public const string DaGui = "DA_GUI";
    public const string ThatBai = "THAT_BAI";
    public const string DaHuy = "DA_HUY";
}
