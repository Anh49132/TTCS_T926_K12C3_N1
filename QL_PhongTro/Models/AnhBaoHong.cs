using System.ComponentModel.DataAnnotations.Schema;
namespace QL_PhongTro.Models;

[Table("anh_bao_hong")]
public sealed class AnhBaoHong
{
    [Column("id")] public int Id { get; set; }
    [Column("bao_hong_id")] public int BaoHongId { get; set; }
    [Column("thu_tu")] public int ThuTu { get; set; }
    [Column("khoa_luu")] public string KhoaLuu { get; set; } = "";
    [Column("content_type")] public string ContentType { get; set; } = "";
    [Column("so_byte")] public long SoByte { get; set; }
    [Column("sha256")] public string Sha256 { get; set; } = "";
}
