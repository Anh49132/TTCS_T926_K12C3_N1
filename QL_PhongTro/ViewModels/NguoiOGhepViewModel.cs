using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;
public class NguoiOGhepInput
{
    [Required(ErrorMessage = "Nhập họ tên."), StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    public string HoTen { get; set; } = "";
    [Required(ErrorMessage = "Nhập số điện thoại."), RegularExpression(@"^0[0-9]{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0.")]
    public string SoDienThoai { get; set; } = "";
    [Required(ErrorMessage = "Nhập số căn cước."), RegularExpression(@"^([0-9]{9}|[0-9]{12})$", ErrorMessage = "Căn cước gồm 9 hoặc 12 chữ số.")]
    public string SoGiayTo { get; set; } = "";
    [Required(ErrorMessage = "Chọn ngày bắt đầu ở cùng.")]
    public DateOnly? NgayVao { get; set; }
    public int PhienBanPhong { get; set; }
}
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
