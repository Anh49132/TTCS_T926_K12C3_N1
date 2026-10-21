using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("chi_so_dien_nuoc")]
public sealed class ChiSoDienNuoc
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("dich_vu_id")] public int DichVuId { get; set; }
    [Column("tu_ngay")] public DateOnly TuNgay { get; set; }
    [Column("den_ngay")] public DateOnly DenNgay { get; set; }
    [Column("chi_so_dau")] public decimal ChiSoDau { get; set; }
    [Column("chi_so_cuoi")] public decimal ChiSoCuoi { get; set; }
    [Column("nguoi_nhap_id")] public int NguoiNhapId { get; set; }
    [Column("ngay_nhap")] public DateTime NgayNhap { get; set; }
    [Column("da_xac_nhan_bat_thuong")] public bool DaXacNhanBatThuong { get; set; }
    [Column("da_khoa")] public bool DaKhoa { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
}
