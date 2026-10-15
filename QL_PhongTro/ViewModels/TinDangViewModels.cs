using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels;

public sealed class TinDangDanhSachViewModel
{
    public long GiaThue { get; init; }
    public int Id { get; init; }
    public string TieuDe { get; init; } = string.Empty;
    public string? AnhDaiDien { get; init; }
}

public sealed class TinDangQuanLyItemViewModel
{
    public bool TuDongSauTraPhong { get; init; }
    public int ToaNhaId { get; init; }
    public long GiaThue { get; init; }
    public decimal DienTich { get; init; }
    public string? AnhDaiDien { get; init; }
    public DateTime? NgayDang { get; init; }
    public int PhongId { get; init; }
    public string MaPhong { get; init; } = string.Empty;
    public string TenToaNha { get; init; } = string.Empty;
    public string TrangThaiPhong { get; init; } = string.Empty;
    public int? TinDangId { get; init; }
    public string? TieuDe { get; init; }
    public string? TrangThaiTin { get; init; }
    public DateTime? NgayHetHan { get; init; }
    public bool DaHetHan { get; init; }
}

public sealed class TinDangQuanLyViewModel
{
    public IReadOnlyList<TinDangQuanLyItemViewModel> DanhSach { get; init; } = [];
}

public sealed record TaoTinDangViewModel
{
    public string? TenChuNha { get; init; }
    public string? DienThoaiChuNha { get; init; }
    public int SoNguoiToiDa { get; init; }
    public IReadOnlyList<PhongDangTinOption> PhongOptions { get; init; } = [];
    public string DiaChi { get; init; } = "";
    public long TienCoc { get; init; }
    public int Tang { get; init; }
    public int? TinDangId { get; set; }
    public string? TrangThaiTin { get; init; }

    [Required(ErrorMessage = "Không xác định được phòng cần đăng tin.")]
    public int PhongId { get; set; }

    public string MaPhong { get; init; } = string.Empty;
    public string TenToaNha { get; init; } = string.Empty;
    public int ToaNhaId { get; init; }
    public decimal DienTich { get; init; }
    public long GiaThue { get; init; }
    public string TrangThaiPhong { get; init; } = string.Empty;
    public DateTime NgayHetHan { get; init; }
    public IReadOnlyList<AnhPhongChiTietViewModel> Anh { get; init; } = [];
    public IReadOnlyList<DichVuTinChiTietViewModel> DichVu { get; init; } = [];

    [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không vượt quá 200 ký tự.")]
    public string TieuDe { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Mô tả không vượt quá 4.000 ký tự.")]
    public string? NoiDung { get; set; }
}

public sealed record PhongDangTinOption(int Id, int ToaNhaId, string TenToaNha, string MaPhong, int Tang, decimal DienTich);

public sealed class AnhPhongQuanLyViewModel
{
    public int Id { get; init; }
    public string DuongDan { get; init; } = string.Empty;
    public string? DuongDanAnhNho { get; init; }
    public int ThuTu { get; init; }
}

public sealed class TinDangChiTietViewModel
{
    public string MaPhong { get; init; } = "";
    public string TenToaNha { get; init; } = "";
    public string? TenChuNha { get; init; }
    public IReadOnlyList<string> TienNghi { get; init; } = [];
    public int Id { get; init; }
    public string TieuDe { get; init; } = string.Empty;
    public string? MoTa { get; init; }
    public long GiaThue { get; init; }
    public decimal DienTich { get; init; }
    public int SoNguoiToiDa { get; init; }
    public long TienCocDuKien { get; init; }
    public string DiaChi { get; init; } = string.Empty;
    public string? PhuongXa { get; init; }
    public string? QuanHuyen { get; init; }
    public string? TinhThanh { get; init; }
    public IReadOnlyList<AnhPhongChiTietViewModel> Anh { get; init; } = [];
    public IReadOnlyList<DichVuTinChiTietViewModel> DichVuTheoSuDung { get; init; } = [];
    public IReadOnlyList<DichVuTinChiTietViewModel> KhoanCoDinh { get; init; } = [];
    public decimal TongChiPhiThangDau
    {
        get
        {
            var total = (decimal)GiaThue;
            foreach (var fee in KhoanCoDinh)
                total += fee.DonGia;
            return total;
        }
    }
}

public sealed class AnhPhongChiTietViewModel
{
    public string DuongDan { get; init; } = string.Empty;
    public string? DuongDanAnhNho { get; init; }
    public string? MoTa { get; init; }
    public int ThuTu { get; init; }
}

public sealed class DichVuTinChiTietViewModel
{
    public string TenDichVu { get; init; } = string.Empty;
    public string CachTinh { get; init; } = string.Empty;
    public string DonViTinh { get; init; } = string.Empty;
    public long DonGia { get; init; }
}
