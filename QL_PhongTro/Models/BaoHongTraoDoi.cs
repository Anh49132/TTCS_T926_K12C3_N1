using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("bao_hong_trao_doi")]
public sealed class BaoHongTraoDoi
{
    [Column("id")] public int Id { get; set; }
    [Column("bao_hong_id")] public int BaoHongId { get; set; }
    [Column("nguoi_gui_id")] public int NguoiGuiId { get; set; }
    [Column("ten_nguoi_gui")] public string TenNguoiGui { get; set; } = "";
    [Column("loai")] public string Loai { get; set; } = "PHAN_HOI";
    [Column("noi_dung")] public string NoiDung { get; set; } = "";
    [Column("trang_thai_truoc")] public string? TrangThaiTruoc { get; set; }
    [Column("trang_thai_sau")] public string? TrangThaiSau { get; set; }
    [Column("thoi_diem")] public DateTime ThoiDiem { get; set; }
    [Column("khoa_chong_trung")] public string KhoaChongTrung { get; set; } = "";
}
