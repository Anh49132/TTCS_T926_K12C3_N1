using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;
public class ChiTietToaNhaViewModel
{
    public ToaNha ToaNha { get; set; } = null!;
    public string? QuanLy { get; set; }
    public string? Anh { get; set; }
    public int TongPhong { get; set; }
    public int DangThue { get; set; }
    public int Trong { get; set; }
    public int DatCoc { get; set; }
    public decimal? DienTichMin { get; set; }
    public decimal? DienTichMax { get; set; }
    public List<int> Tangs { get; set; } = [];
    public List<PhongChiTietToaNhaRow> Phongs { get; set; } = [];
    public List<string> LoaiPhongs { get; set; } = [];
    public string? LoaiPhong { get; set; }
    public string SapXep { get; set; } = "ma_asc";
    public string? ThanhToan { get; set; }
    public bool XemTaiChinh { get; set; }
    public string KyThanhToan { get; set; } = "";
    public Dictionary<int, ThanhToanPhongSummary> ThanhToanPhongs { get; set; } = [];
    public string? TuKhoa { get; set; }
    public int? Tang { get; set; }
    public string? TrangThai { get; set; }
    public int Trang { get; set; } = 1;
    public int TongKetQua { get; set; }
    public int TongTrang => Math.Max(1, (TongKetQua + 7) / 8);
}
public record PhongChiTietToaNhaRow(PhongTro Phong, string? Khach, int? HopDongId, string? MaHopDong, DateOnly? HanHopDong);

public record ThanhToanPhongSummary(string Ma, string Nhan, long TongTien, long DaTra)
{
    public long ConNo => Math.Max(0, TongTien - DaTra);
    public static ThanhToanPhongSummary Create(long total, long paid, bool overdue, bool pending)
    {
        var code = paid >= total ? "DA_TRA" : overdue ? "QUA_HAN" : pending ? "CHO_XAC_NHAN" : paid > 0 ? "MOT_PHAN" : "CHUA_TRA";
        var label = code switch { "DA_TRA" => "Đã thanh toán", "QUA_HAN" => "Quá hạn", "CHO_XAC_NHAN" => "Chờ xác nhận", "MOT_PHAN" => "Thanh toán một phần", _ => "Chưa thanh toán" };
        return new(code, label, total, paid);
    }
}
