using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("hop_dong")]
public class HopDongThamChieu
{
    [Column("yeu_cau_thue_id")] public int? YeuCauThueId { get; set; }
    [Column("khach_dung_ten_id")] public int? KhachDungTenId { get; set; }
    [Column("tien_coc_thoa_thuan")] public long TienCoc { get; set; }
    [Column("ngay_chot_hang_thang")] public int NgayChot { get; set; } = 1;
    [Column("nguoi_lap_id")] public int? NguoiLapId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("id")] public int Id { get; set; }
    [Column("ma_hop_dong")] public string MaHopDong { get; set; } = "";
    [Column("phong_id")] public int PhongId { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "";
    [Column("ngay_tra_phong")] public DateOnly? NgayTraPhong { get; set; }
}
[Table("ky_hop_dong")]
public class KyHopDongThamChieu
{
    [Column("so_thu_tu")] public int SoThuTu { get; set; } = 1;
    [Column("so_thang")] public int SoThang { get; set; }
    [Column("nguoi_lap_id")] public int? NguoiLapId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("ngay_bat_dau")] public DateOnly NgayBatDau { get; set; }
    [Column("ngay_ket_thuc")] public DateOnly NgayKetThuc { get; set; }
    [Column("gia_thue")] public long GiaThue { get; set; }
}
[Table("hop_dong_dich_vu")]
public class HopDongDichVu
{
    [Column("id")] public int Id { get; set; }
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("dich_vu_id")] public int DichVuId { get; set; }
    [Column("cau_hinh_dich_vu_id")] public int CauHinhDichVuId { get; set; }
    [Column("ten_dich_vu")] public string TenDichVu { get; set; } = "";
    [Column("cach_tinh")] public string CachTinh { get; set; } = "";
    [Column("don_vi_tinh")] public string DonViTinh { get; set; } = "";
    [Column("don_gia")] public long DonGia { get; set; }
    [Column("ngay_ghi_nhan")] public DateTime NgayGhiNhan { get; set; }
}
[Table("hoa_don")]
public class HoaDon
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_hoa_don")] public string MaHoaDon { get; set; } = "";
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("thang")] public int Thang { get; set; }
    [Column("nam")] public int Nam { get; set; }
    [Column("tu_ngay")] public DateOnly TuNgay { get; set; }
    [Column("den_ngay")] public DateOnly DenNgay { get; set; }
    [Column("ngay_chot")] public DateOnly NgayChot { get; set; }
    [Column("so_nguoi_tinh_phi")] public int SoNguoiTinhPhi { get; set; }
    [Column("loai_hoa_don")] public string LoaiHoaDon { get; set; } = "DINH_KY";
    [Column("ngay_lap")] public DateTime NgayLap { get; set; }
    [Column("ngay_phat_hanh")] public DateTime? NgayPhatHanh { get; set; }
    [Column("ngay_phat_hanh_nghiep_vu")] public DateOnly? NgayPhatHanhNghiepVu { get; set; }
    [Column("han_thanh_toan")] public DateOnly HanThanhToan { get; set; }
    [Column("tong_tien")] public long TongTien { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "NHAP";
    [Column("nguoi_lap_id")] public int NguoiLapId { get; set; }
    [Column("nguoi_phat_hanh_id")] public int? NguoiPhatHanhId { get; set; }
    [Column("thay_the_hoa_don_id")] public int? ThayTheHoaDonId { get; set; }
    [Column("nguoi_huy_id")] public int? NguoiHuyId { get; set; }
    [Column("ngay_huy")] public DateTime? NgayHuy { get; set; }
    [Column("ly_do_huy")] public string? LyDoHuy { get; set; }
    [Column("ghi_chu")] public string? GhiChu { get; set; }
    [Column("phien_ban")] public int PhienBan { get; set; }
    public List<ChiTietHoaDon> ChiTiet { get; set; } = [];
}
[Table("chi_tiet_hoa_don")]
public class ChiTietHoaDon
{
    [Column("chi_so_id")] public int? ChiSoId { get; set; }
    [Column("bao_hong_id")] public int? BaoHongId { get; set; }
    [Column("so_ngay_tinh_tien")] public int? SoNgayTinhTien { get; set; }
    [Column("so_ngay_trong_thang")] public int? SoNgayTrongThang { get; set; }
    [Column("ghi_chu")] public string? GhiChu { get; set; }
    [Column("id")] public int Id { get; set; }
    [Column("hoa_don_id")] public int HoaDonId { get; set; }
    [Column("so_thu_tu")] public int SoThuTu { get; set; }
    [Column("dich_vu_id")] public int? DichVuId { get; set; }
    [Column("cau_hinh_dich_vu_id")] public int? CauHinhDichVuId { get; set; }
    [Column("ky_hop_dong_id")] public int? KyHopDongId { get; set; }
    [Column("loai_khoan")] public string LoaiKhoan { get; set; } = "DICH_VU";
    [Column("ten_khoan")] public string TenKhoan { get; set; } = "";
    [Column("cach_tinh_ap_dung")] public string? CachTinhApDung { get; set; }
    [Column("don_vi_tinh")] public string? DonViTinh { get; set; }
    [Column("so_luong")] public decimal SoLuong { get; set; }
    [Column("don_gia")] public long DonGia { get; set; }
    [Column("chi_so_dau")] public decimal? ChiSoDau { get; set; }
    [Column("chi_so_cuoi")] public decimal? ChiSoCuoi { get; set; }
    [Column("thanh_tien")] public long ThanhTien { get; set; }
}
