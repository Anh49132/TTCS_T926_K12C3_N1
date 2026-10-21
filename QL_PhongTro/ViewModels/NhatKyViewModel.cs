using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public sealed class NhatKyViewModel
{
    public DateOnly? TuNgay { get; set; }
    public DateOnly? DenNgay { get; set; }
    public int? NguoiThucHienId { get; set; }
    public string? LoaiDoiTuong { get; set; }
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (Total + 49) / 50);
    public List<NhatKyHoatDong> Rows { get; set; } = [];
    public List<AuditActorOption> Actors { get; set; } = [];
    public static IReadOnlyDictionary<string, string> Types { get; } = new Dictionary<string, string>
    {
        ["tai_khoan"] = "Tài khoản", ["toa_nha"] = "Tòa nhà", ["phong_tro"] = "Phòng",
        ["dich_vu"] = "Dịch vụ", ["cau_hinh_dich_vu"] = "Đơn giá dịch vụ",
        ["hoa_don"] = "Hóa đơn", ["chi_tiet_hoa_don"] = "Dòng hóa đơn / chỉ số",
        ["hop_dong_dich_vu"] = "Dịch vụ hợp đồng", ["hop_dong"] = "Hợp đồng",
        ["chi_so_dien_nuoc"] = "Chỉ số điện nước", ["thanh_toan"] = "Thanh toán"
    };
    public static string ActionName(string action) => action switch
    {
        "TAO" => "Tạo", "SUA" => "Sửa", "XOA" => "Xóa", "KHOA" => "Khóa",
        "MO_KHOA" => "Mở khóa", "DOI_TRANG_THAI" => "Đổi trạng thái",
        "GUI_LAI_MAT_KHAU_TAM" => "Gửi lại mật khẩu tạm", _ => action
    };
}
public sealed record AuditActorOption(int Id, string? Name);
