using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Services;

namespace QL_PhongTro.ViewModels;

/// <summary>
/// Backs the single detail page of S2-08. There is no list view model on purpose:
/// the landlord request list belongs to S2-07, and S2-09 may add its own tenant list.
/// </summary>
public sealed class LichHenChiTietViewModel
{
    public required LichHenYeuCau YeuCau { get; init; }

    public bool LaChuNha { get; init; }

    /// <summary>Only a landlord sees the confirm form, and only while the request still waits.</summary>
    public bool HienThiFormXacNhan { get; init; }

    /// <summary>
    /// A landlord may also walk away from a slot they already confirmed, so the reject form
    /// stays visible in DA_HEN_LICH and disappears once the request is closed.
    /// </summary>
    public bool HienThiFormTuChoi { get; init; }

    /// <summary>
    /// Rescheduling only makes sense once a slot is booked, so the form appears for the
    /// landlord exactly in DA_HEN_LICH, next to the reject form.
    /// </summary>
    public bool HienThiFormDoiLich { get; init; }

    /// <summary>
    /// Only an instant-rental request can be approved this way; a viewing request still needs
    /// an appointment or a rejection. The room moves to Đã đặt cọc on approval.
    /// </summary>
    public bool HienThiNutDuyetThueNgay { get; init; }
    public bool HienThiNutDuyetSauKhiXem { get; init; }

    /// <summary>
    /// After approval the request is the input of a rental contract, so the landlord gets the
    /// entry point. Contract drafting is S3-01 and has no screen on dev yet: the button still
    /// obeys the real conditions and leads to the placeholder page, which is the only thing
    /// waiting for S3-01 to take over.
    /// </summary>
    public bool HienThiNutLapHopDong { get; init; }

    /// <summary>Chủ nhà và đúng khách của yêu cầu được hủy khi yêu cầu còn mở.</summary>
    public bool HienThiNutHuy { get; init; }
    public bool HienThiFormHuyDaDuyet { get; init; }

    /// <summary>
    /// AC3: khách xem được lịch sử thay đổi của yêu cầu. LichSuService đã chặn sẵn mọi người
    /// không phải chủ nhà phòng hay khách của yêu cầu đó.
    /// </summary>
    public IReadOnlyList<LichHenLichSuMuc> LichSu { get; init; } = [];

    /// <summary>Where the button points. Change this one line when S3-01 publishes its route.</summary>
    public const string DuongDanLapHopDong = "/LichHen/LapHopDong";

    /// <summary>Wall-clock time the landlord typed, kept in Vietnam time for the input control.</summary>
    [DataType(DataType.DateTime)]
    public DateTime? LichHenNhap { get; set; }

    [Required(ErrorMessage = "Hãy chọn lý do từ chối.")]
    [StringLength(30, ErrorMessage = "Lý do từ chối không hợp lệ.")]
    public string? LyDoTuChoi { get; set; }

    [StringLength(LichHenService.GhiChuToiDa, ErrorMessage = "Ghi chú không được dài quá 500 ký tự.")]
    public string? GhiChuTuChoi { get; set; }
}

public sealed class HuyYeuCauDaDuyetInput
{
    [Required] public int? PhienBan { get; set; }
    [Required] public int? PhienBanPhong { get; set; }
    [Required(ErrorMessage = "Nhập lý do khách không thuê.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do phải từ 5 đến 500 ký tự.")]
    public string? LyDo { get; set; }
    public bool XacNhan { get; set; }
}

/// <summary>
/// Placeholder page behind the "Lập hợp đồng" button of AC4. It carries the request code so the
/// landlord can tell which request is waiting, and exists only until S3-01 has the real screen.
/// </summary>
public sealed class LichHenLapHopDongViewModel
{
    public required int YeuCauId { get; init; }
    public required string MaYeuCau { get; init; }
    public string TenPhong { get; init; } = string.Empty;
    public string TenToaNha { get; init; } = string.Empty;
    public string TenKhach { get; init; } = string.Empty;
}
