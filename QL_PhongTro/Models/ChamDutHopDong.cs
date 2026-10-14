using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("cham_dut_hop_dong")]
public sealed class ChamDutHopDong
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("hoa_don_cuoi_id")] public int HoaDonCuoiId { get; set; }
    [Column("ngay_tra_phong")] public DateOnly NgayTraPhong { get; set; }
    [Column("ngay_ket_thuc_doi_chieu")] public DateOnly NgayKetThuc { get; set; }
    [Column("coc_thoa_thuan_doi_chieu")] public long CocThoaThuan { get; set; }
    [Column("truoc_han")] public bool TruocHan { get; set; }
    [Column("ly_do")] public string? LyDo { get; set; }
    [Column("ly_do_khac")] public string? LyDoKhac { get; set; }
    [Column("ap_dung_phat")] public bool ApDungPhat { get; set; }
    [Column("tien_phat")] public long TienPhat { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
    [Column("nguoi_cap_nhat_id")] public int NguoiCapNhatId { get; set; }
    [Column("cap_nhat_luc")] public DateTime CapNhatLuc { get; set; }
}
