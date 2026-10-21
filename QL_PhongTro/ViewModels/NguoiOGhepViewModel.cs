using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;
public class NguoiOGhepInput : IValidatableObject
{
    public string CachThem { get; set; } = "CHUA_CO_TAI_KHOAN";
    public int? TaiKhoanId { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string HoTen { get; set; } = "";
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string SoDienThoai { get; set; } = "";
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string SoGiayTo { get; set; } = "";
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public DateOnly? NgayVao { get; set; }
    public int PhienBanPhong { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (CachThem == "CO_TAI_KHOAN")
        {
            if (TaiKhoanId is null or <= 0) yield return new("Nhập chính xác ID người dùng.", [nameof(TaiKhoanId)]);
            yield break;
        }
        if (CachThem != "CHUA_CO_TAI_KHOAN") { yield return new("Cách thêm người ở ghép không hợp lệ.", [nameof(CachThem)]); yield break; }
        if (string.IsNullOrWhiteSpace(HoTen) || HoTen.Trim().Length is < 2 or > 100)
            yield return new("Họ tên từ 2 đến 100 ký tự.", [nameof(HoTen)]);
        if (!System.Text.RegularExpressions.Regex.IsMatch(SoDienThoai ?? "", "^0[0-9]{9}$"))
            yield return new("Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.", [nameof(SoDienThoai)]);
        if (!System.Text.RegularExpressions.Regex.IsMatch(SoGiayTo ?? "", "^([0-9]{9}|[0-9]{12})$"))
            yield return new("Căn cước gồm 9 hoặc 12 chữ số.", [nameof(SoGiayTo)]);
    }
}
public sealed record TaiKhoanOGhepXacNhan(int Id, string HoTen, string? DienThoaiDaChe, string? CanCuocDaChe, bool EmailDaXacThuc, bool DuThongTin);
public record NguoiOGhepRow(string HoTen, string? SoDienThoai, string? SoGiayTo, DateOnly NgayVao)
{
    public int Id { get; init; }
    public DateOnly? NgayRa { get; init; }
}
public class ChuyenDiInput
{
    [Required(ErrorMessage = "Chọn ngày chuyển đi.")]
    public DateOnly? NgayRa { get; set; }
    public int PhienBanPhong { get; set; }
}
public record ChuyenDiRowViewModel(HopDongDetailsViewModel Contract, NguoiOGhepRow Person);
public class HopDongDetailsViewModel
{
    public string DiaChiPhong { get; set; } = "";
    public string? LoaiPhong { get; set; }
    public int Tang { get; set; }
    public decimal DienTich { get; set; }
    public string ChuNha { get; set; } = "";
    public string? ChuNhaDienThoai { get; set; }
    public string? ChuNhaEmail { get; set; }
    public string? NguoiThueEmail { get; set; }
    public List<QL_PhongTro.Models.HopDongDichVu> DichVus { get; set; } = [];
    public List<QL_PhongTro.Models.HoaDon> HoaDons { get; set; } = [];
    public bool XemTaiChinh { get; set; }
    public string ToaNha { get; set; } = "";
    public KyHopDongThamChieu? Ky { get; set; }
    public HopDongChiSoDauKy? ChiSo { get; set; }
    public HopDongThamChieu HopDong { get; set; } = null!;
    public KhachThue? DungTen { get; set; }
    public string Phong { get; set; } = "";
    public int GioiHan { get; set; }
    public DateOnly HomNay { get; set; }
    public List<NguoiOGhepRow> Nguois { get; set; } = [];
    public List<NguoiOGhepRow> DaChuyenDi { get; set; } = [];
    public List<NguoiOGhepRow> SapVao { get; set; } = [];
    public List<HopDongGiaHanHistoryRow> LichSuGiaHan { get; set; } = [];
    public ChuyenDiInput ChuyenDi { get; set; } = new();
    public bool ChoGiaHan { get; set; }
    public int? ChuyenDiNguoiId { get; set; }
    public NguoiOGhepInput Input { get; set; } = new();
    public TaiKhoanOGhepXacNhan? TaiKhoanXacNhan { get; set; }
    public bool ChoThem { get; set; }
    public string TrangThaiPhong { get; set; } = "";
    public string TrangThaiTin { get; set; } = "";
    public bool ChoKichHoat { get; set; }
}

public sealed record HopDongGiaHanHistoryRow(
    int SoThuTu,
    DateOnly NgayBatDau,
    DateOnly NgayKetThuc,
    long GiaThue,
    DateTime ThoiDiemVietNam,
    string? NguoiThucHien);
