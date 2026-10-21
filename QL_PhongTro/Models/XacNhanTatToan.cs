using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("xac_nhan_tat_toan")]
public sealed class XacNhanTatToan
{
    [Column("id")] public int Id { get; set; }
    [Column("thanh_ly_id")] public int ThanhLyId { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("nguoi_xac_nhan_id")] public int NguoiXacNhanId { get; set; }
    [Column("ten_nguoi_xac_nhan")] public string TenNguoiXacNhan { get; set; }="";
    [Column("thoi_diem")] public DateTime ThoiDiem { get; set; }
    [Column("so_tien_tat_toan")] public long SoTienTatToan { get; set; }
    [Column("tien_can_tru")] public long TienCanTru { get; set; }
}

[Table("lich_su_trang_thai_phong")]
public sealed class LichSuTrangThaiPhong
{
    [Column("id")] public int Id { get; set; }
    [Column("phong_id")] public int PhongId { get; set; }
    [Column("hop_dong_id")] public int? HopDongId { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; }="";
    [Column("tu_thoi_diem")] public DateTime TuThoiDiem { get; set; }
    [Column("den_thoi_diem")] public DateTime? DenThoiDiem { get; set; }
}
