using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("thanh_toan")]
public sealed class ThanhToanHoaDon
{
    [Column("id")] public int Id { get; set; }
    [Column("hoa_don_id")] public int HoaDonId { get; set; }
    [Column("so_tien")] public long SoTien { get; set; }
    [Column("ngay_thanh_toan")] public DateOnly NgayThanhToan { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "DA_XAC_NHAN";
    [Column("hinh_thuc_bao_cao")] public string? HinhThucBaoCao { get; set; }
    [Column("anh_chung_tu_key")] public string? AnhChungTuKey { get; set; }
    [Column("anh_chung_tu_content_type")] public string? AnhChungTuContentType { get; set; }
    [Column("anh_chung_tu_so_byte")] public long? AnhChungTuSoByte { get; set; }
}
