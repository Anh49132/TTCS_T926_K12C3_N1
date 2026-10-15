using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("giao_dich_coc")]
public sealed class GiaoDichCoc
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_giao_dich_coc")] public string Ma { get; set; } = "";
    [Column("khoa_chong_trung")] public string KhoaChongTrung { get; set; } = "";
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("hoa_don_id")] public int? HoaDonId { get; set; }
    [Column("loai_giao_dich")] public string Loai { get; set; } = "THU_COC";
    [Column("so_tien")] public long SoTien { get; set; }
    [Column("hinh_thuc")] public string? HinhThuc { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "DA_XAC_NHAN";
    [Column("nguoi_thuc_hien_id")] public int NguoiThucHienId { get; set; }
    [Column("ngay_giao_dich")] public DateTime NgayGiaoDich { get; set; }
}

[Table("thanh_ly_hop_dong")]
public sealed class BangTatToan
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_tat_toan")] public string Ma { get; set; } = "";
    [Column("hop_dong_id")] public int HopDongId { get; set; }
    [Column("hoa_don_cuoi_id")] public int HoaDonCuoiId { get; set; }
    [Column("ngay_tra_phong")] public DateOnly NgayTraPhong { get; set; }
    [Column("coc_con_giu_truoc_chot")] public long CocConGiu { get; set; }
    [Column("no_hoa_don_cuoi")] public long NoHoaDonCuoi { get; set; }
    [Column("cong_no_cu")] public long CongNoCu { get; set; }
    [Column("tien_phat")] public long TienPhat { get; set; }
    [NotMapped] public long TongKhauTru => checked(TienHuHong + TienPhat);
    [Column("tien_hu_hong")] public long TienHuHong { get; set; }
    [Column("tien_phai_hoan")] public long TienPhaiHoan { get; set; }
    [Column("tien_khach_tra_them")] public long TienKhachTraThem { get; set; }
    [Column("chi_tiet_chot")] public string ChiTietChot { get; set; } = "[]";
    [Column("dau_van_tay_nguon")] public string DauVanTayNguon { get; set; } = "";
    [Column("ghi_chu")] public string? GhiChu { get; set; }
    [Column("trang_thai")] public string TrangThai { get; set; } = "NHAP";
    [Column("phien_ban")] public int PhienBan { get; set; }
    [Column("nguoi_lap_id")] public int NguoiLapId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
    [Column("cap_nhat_luc")] public DateTime CapNhatLuc { get; set; }
    public List<KhauTruTatToan> KhauTru { get; set; } = [];
}

[Table("chi_tiet_khau_tru")]
public sealed class KhauTruTatToan
{
    [Column("id")] public int Id { get; set; }
    [Column("thanh_ly_id")] public int ThanhLyId { get; set; }
    [Column("so_thu_tu")] public int SoThuTu { get; set; }
    [Column("loai_khoan")] public string LoaiKhoan { get; set; } = "HU_HONG";
    [Column("ten_khoan")] public string TenKhoan { get; set; } = "";
    [Column("so_tien")] public long SoTien { get; set; }
    [Column("ghi_chu")] public string? GhiChu { get; set; }
}
