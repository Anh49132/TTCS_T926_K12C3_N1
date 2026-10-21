using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

public static class CachTinhDichVu
{
    public const string TheoChiSo = "THEO_CHI_SO";
    public const string TheoNguoi = "THEO_NGUOI";
    public const string CoDinh = "CO_DINH";
    public static bool HopLe(string? value) => value is TheoChiSo or TheoNguoi or CoDinh;
    public static string Ten(string value) => value switch
    {
        TheoChiSo => "Theo chỉ số",
        TheoNguoi => "Theo đầu người",
        CoDinh => "Cố định theo phòng",
        _ => value
    };
}

[Table("dich_vu")]
public class DichVu
{
    [Column("id")] public int Id { get; set; }
    [Column("ma_dich_vu"), MaxLength(20)] public string MaDichVu { get; set; } = "";
    [Column("ten_dich_vu"), MaxLength(100)] public string TenDichVu { get; set; } = "";
    [Column("mo_ta")] public string? MoTa { get; set; }
    [Column("dang_hoat_dong")] public bool DangHoatDong { get; set; } = true;
}

[Table("cau_hinh_dich_vu")]
public class CauHinhDichVu
{
    [Column("id")] public int Id { get; set; }
    [Column("toa_nha_id")] public int ToaNhaId { get; set; }
    [Column("phong_id")] public int? PhongId { get; set; }
    [Column("dich_vu_id")] public int DichVuId { get; set; }
    public DichVu DichVu { get; set; } = null!;
    [Column("cach_tinh"), MaxLength(25)] public string CachTinh { get; set; } = "";
    [Column("don_vi_tinh"), MaxLength(30)] public string DonViTinh { get; set; } = "";
    [Column("don_gia")] public long DonGia { get; set; }
    [Column("tu_ngay")] public DateOnly TuNgay { get; set; }
    [Column("den_ngay")] public DateOnly? DenNgay { get; set; }
    [Column("dang_ap_dung")] public bool DangApDung { get; set; } = true;
    [Column("da_chot_gia")] public bool DaChotGia { get; set; } = true;
    [Column("nguoi_tao_id")] public int NguoiTaoId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
}

[Table("khoi_tao_dich_vu")]
public class KhoiTaoDichVu
{
    [Key, Column("toa_nha_id")] public int ToaNhaId { get; set; }
    [Column("ngay_tao")] public DateTime NgayTao { get; set; }
}
