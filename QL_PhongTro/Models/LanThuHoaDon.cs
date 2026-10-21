using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

// One-to-one metadata for thanh_toan; money is stored only in thanh_toan.
[Table("lan_thu_hoa_don")]
public sealed class LanThuHoaDon
{
    [Column("id")] public int Id { get; set; }
    [Column("thanh_toan_id")] public int ThanhToanId { get; set; }
    [Column("hinh_thuc")] public string HinhThuc { get; set; } = "";
    [Column("ghi_chu")] public string? GhiChu { get; set; }
    [Column("nguoi_xac_nhan_id")] public int NguoiXacNhanId { get; set; }
    [Column("ten_nguoi_xac_nhan")] public string TenNguoiXacNhan { get; set; } = "";
    [Column("thoi_diem_ghi")] public DateTime ThoiDiemGhi { get; set; }
    [Column("khoa_chong_trung")] public string KhoaChongTrung { get; set; } = "";
}
