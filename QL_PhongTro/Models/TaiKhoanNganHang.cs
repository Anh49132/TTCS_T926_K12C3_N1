using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("tai_khoan_ngan_hang")]
public sealed class TaiKhoanNganHang
{
    [Key, Column("chu_nha_id")] public int ChuNhaId { get; set; }
    [Column("ten_ngan_hang"), MaxLength(120)] public string TenNganHang { get; set; } = "";
    [Column("so_tai_khoan"), MaxLength(50)] public string SoTaiKhoan { get; set; } = "";
    [Column("qr_key")] public string? QrKey { get; set; }
    [Column("qr_content_type")] public string? QrContentType { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("ngay_cap_nhat")] public DateTime NgayCapNhat { get; set; }
    [Column("phien_ban"), ConcurrencyCheck] public string PhienBan { get; set; } = "";
}
