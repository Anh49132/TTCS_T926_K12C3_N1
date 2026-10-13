using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

// Terminal cancellation event; receipt and original recording metadata remain intact.
[Table("huy_lan_thu_hoa_don")]
public sealed class HuyLanThuHoaDon
{
    [Column("id")] public int Id { get; set; }
    [Column("thanh_toan_id")] public int ThanhToanId { get; set; }
    [Column("ly_do_huy")] public string LyDoHuy { get; set; } = "";
    [Column("nguoi_huy_id")] public int NguoiHuyId { get; set; }
    [Column("ten_nguoi_huy")] public string TenNguoiHuy { get; set; } = "";
    [Column("thoi_diem_huy")] public DateTime ThoiDiemHuy { get; set; }
    // Active receipt is version 0; the single terminal transition records version 1.
    [Column("phien_ban")] public int PhienBan { get; set; } = 1;
}
