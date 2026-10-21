using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.Models;
public static class TrangThaiYeuCau
{
    public const string Moi = "MOI", DaHenLich = "DA_HEN_LICH", DaDuyet = "DA_DUYET", TuChoi = "TU_CHOI", DaHuy = "DA_HUY";
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    { [Moi] = "Mới", [DaHenLich] = "Đã hẹn lịch", [DaDuyet] = "Đã duyệt", [TuChoi] = "Từ chối", [DaHuy] = "Đã huỷ" };

    // PO S2-07: both a new request and a scheduled request remain open until they
    // are approved, rejected or cancelled.
    public static bool ChuaXuLy(string trangThai) => trangThai is Moi or DaHenLich;

    public static string BadgeClass(string trangThai) => trangThai switch
    {
        Moi => "text-bg-success",
        DaHenLich => "text-bg-primary",
        DaDuyet => "text-bg-info",
        TuChoi or DaHuy => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string LoaiLabel(string loaiYeuCau) => loaiYeuCau switch
    {
        LichHenTrangThai.LoaiXemPhong => "Xem phòng",
        LichHenTrangThai.LoaiThueNgay => "Thuê ngay",
        _ => loaiYeuCau
    };
}
public class YeuCau
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(30)] public string MaYeuCau { get; set; } = string.Empty;
    public int KhachThueId { get; set; }
    public int PhongId { get; set; }
    public int ToaNhaId { get; set; }
    [Required, MaxLength(50)] public string LoaiYeuCau { get; set; } = string.Empty;
    public DateOnly? NgayMongMuon { get; set; }
    [Required, MaxLength(25)] public string TrangThai { get; set; } = TrangThaiYeuCau.Moi;
    public DateTime NgayTao { get; set; }
}
