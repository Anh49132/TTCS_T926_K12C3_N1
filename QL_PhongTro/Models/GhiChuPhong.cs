using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;
[Table("ghi_chu_phong")]
public sealed class GhiChuPhong {
 [Column("id")] public int Id {get;set;}
 [Column("phong_id")] public int PhongId {get;set;}
 [Column("nguoi_tao_id")] public int NguoiTaoId {get;set;}
 [Column("tieu_de")] public string TieuDe {get;set;}="";
 [Column("noi_dung")] public string NoiDung {get;set;}="";
 [Column("ghim")] public bool Ghim {get;set;}
 [Column("ngay_tao")] public DateTime NgayTao {get;set;}
 [Column("khoa_chong_trung")] public string KhoaChongTrung {get;set;}="";
 [Column("ten_tep")] public string? TenTep {get;set;}
 [Column("loai_tep")] public string? LoaiTep {get;set;}
 [Column("noi_dung_tep")] public byte[]? NoiDungTep {get;set;}
}
