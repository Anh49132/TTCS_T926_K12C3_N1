using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

[Table("dich_vu_toa_nha")]
public class DichVuToaNha
{
    [Column("id")] public int Id { get; set; }
    [Column("toa_nha_id")] public int ToaNhaId { get; set; }
    [Column("dich_vu_id")] public int DichVuId { get; set; }
    public DichVu DichVu { get; set; } = null!;
    [Column("ap_dung_mac_dinh")] public bool ApDungMacDinh { get; set; }
}

[Table("dich_vu_phong")]
public class DichVuPhong
{
    [Column("id")] public int Id { get; set; }
    [Column("phong_id")] public int PhongId { get; set; }
    public PhongTro Phong { get; set; } = null!;
    [Column("dich_vu_toa_nha_id")] public int DichVuToaNhaId { get; set; }
    public DichVuToaNha DichVuToaNha { get; set; } = null!;
    [Column("don_gia_rieng")] public long? DonGiaRieng { get; set; }
}
