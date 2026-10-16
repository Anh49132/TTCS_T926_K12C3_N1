using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public sealed class EmailGuiLogViewModel
{
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
