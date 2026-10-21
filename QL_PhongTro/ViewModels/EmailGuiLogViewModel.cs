using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public sealed class EmailGuiLogViewModel
{
    public string? Nhom {get;set;}
    public static string KindName(string value)=>value.StartsWith(QL_PhongTro.Services.PaymentEmailQueue.Prefix,StringComparison.Ordinal)
        ? value.Split(':').Last() switch {"BAO_MOI"=>"Báo thanh toán mới","DA_XAC_NHAN"=>"Xác nhận thanh toán","TU_CHOI"=>"Từ chối báo thanh toán","DA_HUY"=>"Hủy báo thanh toán",_=>"Thanh toán"}
        : value switch {"HOA_DON_MOI"=>"Hóa đơn mới","TRUOC_HAN_2_NGAY"=>"Sắp đến hạn","QUA_HAN"=>"Quá hạn",_=>value};
    public string? TrangThai { get; set; }
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (Total + 49) / 50);
    public List<EmailGuiLogRow> Rows { get; set; } = [];
    public static IReadOnlyDictionary<string, string> TrangThais => new Dictionary<string, string>
    {
        [TrangThaiEmail.ChoGui] = "Chờ gửi",
        [TrangThaiEmail.DangGui] = "Đang gửi",
        [TrangThaiEmail.DaGui] = "Đã gửi",
        [TrangThaiEmail.ThatBai] = "Thất bại",
        [TrangThaiEmail.DaHuy] = "Đã hủy"
    };
}
public sealed record EmailGuiLogRow(int Id, int HoaDonId, string LoaiThongBao, int NguoiNhanId, string EmailNguoiNhan, string TrangThai, int SoLanThu, string? LoiGanNhat, DateTime NgayGui, DateTime? NgayThuTiep);
