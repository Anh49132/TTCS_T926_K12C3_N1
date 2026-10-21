using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QL_PhongTro.Models;

// Append-only business history of a viewing request, kept apart from the administrator
// activity log. S2-06 owns yeu_cau_thue, so this table declares no foreign key to it:
// a request row is never deleted and the history has to survive any change to that
// table's shape. The link is a plain integer kept consistent by LichHenService.
[Table("yeu_cau_thue_lich_su")]
public class YeuCauThueLichSu
{
    [Column("id")] public int Id { get; set; }

    [Column("yeu_cau_thue_id")] public int? YeuCauThueId { get; set; }

    [Column("trang_thai_cu"), MaxLength(25)] public string? TrangThaiCu { get; set; }

    [Column("trang_thai_moi"), Required, MaxLength(25)] public string TrangThaiMoi { get; set; } = string.Empty;

    [Column("hanh_dong"), Required, MaxLength(40)] public string HanhDong { get; set; } = string.Empty;

    [Column("nguoi_thuc_hien_id")] public int? NguoiThucHienId { get; set; }

    [Column("ten_nguoi_thuc_hien"), MaxLength(100)] public string? TenNguoiThucHien { get; set; }

    [Column("vai_tro_luc_thuc_hien"), MaxLength(20)] public string? VaiTroLucThucHien { get; set; }

    [Column("lich_hen_cu")] public DateTime? LichHenCu { get; set; }

    [Column("lich_hen_moi")] public DateTime? LichHenMoi { get; set; }

    [Column("ly_do_tu_choi"), MaxLength(30)] public string? LyDoTuChoi { get; set; }

    [Column("ghi_chu_tu_choi"), MaxLength(500)] public string? GhiChuTuChoi { get; set; }

    [Column("thoi_diem"), Required] public DateTime ThoiDiem { get; set; }
}

public static class HanhDongYeuCau
{
    public const string XacNhan = "XAC_NHAN";
    public const string DoiLich = "DOI_LICH";
    public const string DuyetThueNgay = "DUYET_THUE_NGAY";
    public const string DuyetSauKhiXem = "DUYET_SAU_KHI_XEM";
    public const string TuChoi = "TU_CHOI";
    public const string Huy = "HUY_YEU_CAU";
    public const string HuyDaDuyet = "HUY_YEU_CAU_DA_DUYET";

    /// <summary>
    /// Dòng mở đầu của dấu vết. S2-06 tạo yêu cầu trong YeuCauThueService.Send và file đó không
    /// thuộc S2-08, nên dòng này được SUY RA lúc hiển thị chứ không ghi vào bảng: thời điểm lấy
    /// từ ngay_tao của yêu cầu, người thực hiện là khách, vai trò là khách thuê. Khi S2-06 ghi
    /// vào bảng thì bỏ hàm nay đi, không còn chỗ nào khác ghi dòng này.
    /// </summary>
    public const string TaoYeuCau = "TAO_YEU_CAU";

    public static string Label(string hanhDong) => hanhDong switch
    {
        XacNhan => "Chủ nhà xác nhận lịch hẹn",
        DoiLich => "Chủ nhà đổi lịch hẹn",
        DuyetThueNgay => "Chủ nhà duyệt thuê ngay",
        DuyetSauKhiXem => "Chủ nhà duyệt thuê sau khi xem phòng",
        TuChoi => "Chủ nhà từ chối yêu cầu",
        Huy => "Hủy yêu cầu",
        HuyDaDuyet => "Chủ nhà hủy giữ chỗ do khách không thuê",
        TaoYeuCau => "Khách gửi yêu cầu",
        _ => hanhDong
    };
}
