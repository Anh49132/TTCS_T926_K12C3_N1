using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("nhat_ky_hoat_dong")]
public sealed class NhatKyHoatDong
{
    [Column("id")] public long Id { get; set; }
    [Column("nguoi_thuc_hien_id")] public int? NguoiThucHienId { get; set; }
    [Column("ten_nguoi_thuc_hien")] public string? TenNguoiThucHien { get; set; }
    [Column("vai_tro_luc_thuc_hien")] public string? VaiTroLucThucHien { get; set; }
    [Column("loai_doi_tuong")] public string LoaiDoiTuong { get; set; } = "";
    [Column("doi_tuong_id")] public int DoiTuongId { get; set; }
    [Column("hanh_dong")] public string HanhDong { get; set; } = "";
    [Column("du_lieu_truoc")] public string? DuLieuTruoc { get; set; }
    [Column("du_lieu_sau")] public string? DuLieuSau { get; set; }
    [Column("thoi_diem")] public DateTime ThoiDiem { get; set; }
}
