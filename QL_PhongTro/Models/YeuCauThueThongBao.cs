using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

// Notification about a viewing request. S2-08 owns this channel: the project has no
// shared notification system yet, so the table is named after the feature that writes
// it rather than after a future common inbox. Like the history table it carries no
// foreign key to yeu_cau_thue, which stays under S2-06 control.
[Table("yeu_cau_thue_thong_bao")]
public class YeuCauThueThongBao
{
    [Column("id")] public int Id { get; set; }

    [Column("yeu_cau_thue_id")] public int? YeuCauThueId { get; set; }

    [Column("nguoi_nhan_id")] public int NguoiNhanId { get; set; }

    [Column("loai"), Required, MaxLength(40)] public string Loai { get; set; } = string.Empty;

    [Column("tieu_de"), Required, MaxLength(200)] public string TieuDe { get; set; } = string.Empty;

    [Column("noi_dung"), Required, MaxLength(500)] public string NoiDung { get; set; } = string.Empty;

    [Column("duong_dan"), MaxLength(200)] public string? DuongDan { get; set; }

    [Column("da_doc")] public bool DaDoc { get; set; }

    [Column("ngay_tao"), Required] public DateTime NgayTao { get; set; }
}

public static class LoaiThongBaoYeuCau
{
    public const string XacNhanLich = "XAC_NHAN_LICH";
    public const string YeuCauDoiLich = "YEU_CAU_DOI_LICH";
    public const string YeuCauDuyet = "YEU_CAU_DUYET";
    public const string YeuCauTuChoi = "YEU_CAU_TU_CHOI";
    public const string YeuCauDaHuy = "YEU_CAU_DA_HUY";
}
