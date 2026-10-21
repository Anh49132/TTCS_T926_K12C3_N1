using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QL_PhongTro.ViewModels;

public class HopDongCreateViewModel
{
    [StringLength(5000, ErrorMessage = "Điều khoản bổ sung tối đa 5.000 ký tự.")]
    public string? DieuKhoanBoSung { get; set; }
    public List<IFormFile> TaiLieus { get; set; } = [];
    public bool TaoHoaDonKyDau { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn yêu cầu đã duyệt.")]
    public int? YeuCauId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập giá thuê chốt."), Range(500000, long.MaxValue, ErrorMessage = "Giá thuê chốt tối thiểu 500.000 đ.")]
    public long? GiaThue { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tiền cọc."), Range(0, long.MaxValue, ErrorMessage = "Tiền cọc không được âm.")]
    public long? TienCoc { get; set; }
    [BindNever]
    public int? SoThangCoc { get; set; }
    [BindNever] public DateOnly HomNay { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")] public DateOnly? NgayBatDau { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập kỳ hạn."), Range(1, 120, ErrorMessage = "Kỳ hạn phải từ 1 đến 120 tháng.")] public int? SoThang { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ngày chốt hóa đơn."), Range(1, 31, ErrorMessage = "Ngày chốt phải từ 1 đến 31.")] public int? NgayChot { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập chỉ số điện đầu kỳ."), Range(typeof(decimal), "0", "99999999999.999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Chỉ số điện phải không âm, tối đa 99999999999,999.")]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(ContractMeterBinder))]
    public decimal? ChiSoDien { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập chỉ số nước đầu kỳ."), Range(typeof(decimal), "0", "99999999999.999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Chỉ số nước phải không âm, tối đa 99999999999,999.")]
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType = typeof(ContractMeterBinder))]
    public decimal? ChiSoNuoc { get; set; }
    [Required(ErrorMessage = "Thông tin phiên bản phòng bị thiếu. Vui lòng tải lại trang.")] public int? PhienBanPhong { get; set; }
    [BindNever] public List<HopDongChongLan> ChongLans { get; set; } = [];
    public DateOnly? NgayKetThuc => NgayBatDau.HasValue && SoThang is >= 1 and <= 120
        && NgayBatDau.Value.Year <= 9988 ? TinhNgayKetThuc(NgayBatDau.Value, SoThang.Value) : null;
    public static DateOnly TinhNgayKetThuc(DateOnly start, int months) => start.AddMonths(months).AddDays(-1);
    [BindNever] public List<YeuCauHopDong> YeuCaus { get; set; } = [];
    [BindNever] public YeuCauHopDong? DaChon { get; set; }
    [BindNever] public List<QL_PhongTro.Services.DonGiaDichVu> PhiDichVus { get; set; } = [];
    public string? Intent { get; set; }
}

public class YeuCauHopDong
{
    public int SoNguoiDuKien { get; set; }
    public string? SoGiayTo { get; set; }
    public string? Email { get; set; }
    public string? QueQuan { get; set; }
    public bool DuocXemHoSo { get; set; }
    public int ToaNhaId { get; set; }
    public string DiaChi { get; set; } = "";
    public int Tang { get; set; }
    public decimal DienTich { get; set; }
    public string? LoaiPhong { get; set; }
    public string TrangThaiPhong { get; set; } = "";
    public int Id { get; set; }
    public int PhongId { get; set; }
    public int KhachId { get; set; }
    public string Ma { get; set; } = "";
    public string Khach { get; set; } = "";
    public string? DienThoai { get; set; }
    public string Phong { get; set; } = "";
    public string ToaNha { get; set; } = "";
    public DateOnly NgayMongMuon { get; set; }
    public long GiaThue { get; set; }
    public long TienCoc { get; set; }
    public int NgayChot { get; set; }
    public int PhienBanPhong { get; set; }
}

public record HopDongChongLan(int Id, string Ma, DateOnly NgayBatDau, DateOnly NgayKetThuc);

public record HopDongDanhSach(QL_PhongTro.Models.HopDongThamChieu HopDong, string Phong, string ToaNha, string? Khach)
{
    public QL_PhongTro.Models.KyHopDongThamChieu? Ky { get; init; }
}
