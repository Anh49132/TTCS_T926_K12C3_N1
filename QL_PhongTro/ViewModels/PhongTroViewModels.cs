using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public enum TrangThaiPhong
{
    [Display(Name = "Trống")]
    TRONG,

    [Display(Name = "Đã đặt cọc")]
    DA_DAT_COC,

    [Display(Name = "Đang thuê")]
    DANG_THUE,

    [Display(Name = "Ngừng cho thuê")]
    NGUNG_CHO_THUE
}

public class DanhSachPhongViewModel
{
    public decimal DoanhThuPhongDangThue { get; set; }

    public Dictionary<int, string> KhachTheoPhong { get; set; } = [];

    public int? ToaNhaId { get; set; }
    public int? QuanLyId { get; set; }
    public string? TuKhoa { get; set; }
    public TrangThaiPhong? TrangThaiFilter { get; set; }
    public IReadOnlyList<SelectListItem> ToaNhaOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> QuanLyOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> TrangThaiOptions { get; set; } = [];
    public IReadOnlyDictionary<string, int> SoLuongTheoTrangThai { get; set; } = new Dictionary<string, int>();
    public IReadOnlyList<PhongTro> PhongTros { get; set; } = [];
}

public class TaoPhongViewModel
{
    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? GhiChuNoiBo { get; set; }
    public List<string> TienNghi { get; set; } = [];
    [Required(ErrorMessage = "Vui lòng chọn tòa nhà.")]
    [Display(Name = "Tòa nhà")]
    public int? ToaNhaId { get; set; }

    [Required(ErrorMessage = "Mã phòng là bắt buộc.")]
    [StringLength(20, ErrorMessage = "Mã phòng không vượt quá 20 ký tự.")]
    [Display(Name = "Mã phòng")]
    public string MaPhong { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tầng là bắt buộc.")]
    [Display(Name = "Tầng")]
    public int? Tang { get; set; }

    [Required(ErrorMessage = "Diện tích là bắt buộc.")]
    [Range(0.01, 999999.99, ErrorMessage = "Diện tích phải lớn hơn 0 và tối đa 999999,99 m².")]
    [Display(Name = "Diện tích (m²)")]
    public decimal? DienTich { get; set; }

    [Required(ErrorMessage = "Giá thuê là bắt buộc.")]
    [Range(500000, long.MaxValue, ErrorMessage = "Giá thuê tối thiểu là 500.000 VND.")]
    [Display(Name = "Giá thuê mỗi tháng (VND)")]
    public long? GiaThue { get; set; }

    [Display(Name = "Giá thuê mỗi tháng (VND)")]
    public string? GiaThueDisplay { get; set; }

    [Required(ErrorMessage = "Số người ở tối đa là bắt buộc.")]
    [Range(1, int.MaxValue, ErrorMessage = "Số người ở tối đa phải lớn hơn 0.")]
    [Display(Name = "Số người ở tối đa")]
    public int? SoNguoiToiDa { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [EnumDataType(typeof(TrangThaiPhong), ErrorMessage = "Trạng thái phòng không hợp lệ.")]
    [Display(Name = "Trạng thái")]
    public TrangThaiPhong? TrangThai { get; set; }

    public IReadOnlyList<SelectListItem> ToaNhaOptions { get; set; } = [];
    public IReadOnlyList<AnhPhongQuanLyViewModel> Anh { get; set; } = [];
}

public class TaoPhongHangLoatViewModel
{
    public string? TienCocDisplay { get; set; }

    [StringLength(500, ErrorMessage = "Tiện ích tối đa 500 ký tự.")]
    public string? TienIch { get; set; }

    [StringLength(500, ErrorMessage = "Nội thất tối đa 500 ký tự.")]
    public string? NoiThat { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1.000 ký tự.")]
    public string? GhiChuPhong { get; set; }

    public bool TaoTiep { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tòa nhà.")]
    [Display(Name = "Tòa nhà")]
    public int? ToaNhaId { get; set; }

    [Required(ErrorMessage = "Số tầng là bắt buộc.")]
    [Range(1, 99, ErrorMessage = "Số tầng phải từ 1 đến 99.")]
    [Display(Name = "Số tầng")]
    public int? SoTang { get; set; }

    [Required(ErrorMessage = "Số phòng mỗi tầng là bắt buộc.")]
    [Range(1, 99, ErrorMessage = "Số phòng mỗi tầng phải từ 1 đến 99.")]
    [Display(Name = "Số phòng mỗi tầng")]
    public int? SoPhongMoiTang { get; set; }

    [Required(ErrorMessage = "Diện tích là bắt buộc.")]
    [Range(0.01, 999999.99, ErrorMessage = "Diện tích phải lớn hơn 0 và tối đa 999999,99 m².")]
    [Display(Name = "Diện tích (m²)")]
    public decimal? DienTich { get; set; }

    [Required(ErrorMessage = "Giá thuê là bắt buộc.")]
    [Range(500000, long.MaxValue, ErrorMessage = "Giá thuê tối thiểu là 500.000 VND.")]
    [Display(Name = "Giá thuê mỗi tháng (VND)")]
    public long? GiaThue { get; set; }

    [Display(Name = "Giá thuê mỗi tháng (VND)")]
    public string? GiaThueDisplay { get; set; }


    [Range(1, int.MaxValue, ErrorMessage = "Số người ở tối đa phải lớn hơn 0.")]
    [Display(Name = "Số người ở tối đa")]
    public int? SoNguoiToiDa { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [EnumDataType(typeof(TrangThaiPhong), ErrorMessage = "Trạng thái phòng không hợp lệ.")]
    [Display(Name = "Trạng thái ban đầu")]
    public TrangThaiPhong? TrangThai { get; set; }

    public IReadOnlyList<SelectListItem> ToaNhaOptions { get; set; } = [];
}

public class TaoToaNhaViewModel
{
    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Diện tích đất phải lớn hơn 0.")]
    public decimal? DienTichDat { get; set; }
    public bool ThangMay { get; set; }
    public bool BaiDoXe { get; set; }
    public bool CameraAnNinh { get; set; }
    public bool BaoVe24h { get; set; }
    public bool KhuGiatSay { get; set; }
    public bool SanThuong { get; set; }

    public bool DangHoatDongHienTai { get; set; }
    public int TongPhongHienTai { get; set; }
    public int DangThueHienTai { get; set; }
    public decimal TongDienTichHienTai { get; set; }
    public string? AnhHienTai { get; set; }
    public int? PhongAnhHienTaiId { get; set; }
    public string? DienThoaiQuanLyHienTai { get; set; }

    [Required(ErrorMessage = "Tên tòa nhà là bắt buộc.")]
    [StringLength(150)]
    [Display(Name = "Tên tòa nhà")]
    public string TenToaNha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ là bắt buộc.")]
    [Display(Name = "Địa chỉ")]
    public string DiaChi { get; set; } = string.Empty;

    [StringLength(100)]
    [Required(ErrorMessage = "Phường/xã là bắt buộc.")]
    [Display(Name = "Phường/xã")]
    public string? PhuongXa { get; set; }

    [StringLength(100)]
    [Required(ErrorMessage = "Quận/huyện là bắt buộc.")]
    [Display(Name = "Quận/huyện")]
    public string? QuanHuyen { get; set; }

    [StringLength(100)]
    [Required(ErrorMessage = "Tỉnh/thành phố là bắt buộc.")]
    [Display(Name = "Tỉnh/thành phố")]
    public string? TinhThanh { get; set; }

    [Required(ErrorMessage = "Số tầng là bắt buộc.")]
    [Range(1, 99, ErrorMessage = "Số tầng phải từ 1 đến 99.")]
    [Display(Name = "Số tầng")]
    public int? SoTang { get; set; }

    [Display(Name = "Người quản lý")]
    public int? QuanLyId { get; set; }

    [Display(Name = "Ghi chú")]
    public string? GhiChu { get; set; }

    public IReadOnlyList<SelectListItem> QuanLyOptions { get; set; } = [];
}

public class DanhSachToaNhaViewModel
{
    public string? TuKhoa { get; set; }
    public IReadOnlyList<ToaNhaTongHopViewModel> ToaNhas { get; set; } = [];
}

public class ToaNhaTongHopViewModel
{
    public string? KhuVuc { get; set; }
    public int Id { get; set; }
    public string TenToaNha { get; set; } = string.Empty;
    public string DiaChi { get; set; } = string.Empty;
    public string? QuanLy { get; set; }
    public int SoPhong { get; set; }
    public int SoPhongTrong { get; set; }
    public int SoPhongDangThue { get; set; }
    public bool DangHoatDong { get; set; }
}

public class ChiTietPhongViewModel
{
    public PhongTro Phong { get; set; } = null!;
    public ToaNha ToaNha { get; set; } = null!;
    public IReadOnlyList<AnhPhongQuanLyViewModel> Anh { get; set; } = [];
}
