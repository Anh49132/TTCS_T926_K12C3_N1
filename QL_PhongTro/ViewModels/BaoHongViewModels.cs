using System.ComponentModel.DataAnnotations;
using QL_PhongTro.Models;
namespace QL_PhongTro.ViewModels;

public sealed class BaoHongInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng đang thuê.")]
    public int HopDongId { get; set; }
    public List<IFormFile> Images { get; set; } = [];
    [Required(ErrorMessage = "Vui lòng nhập loại thiết bị."), StringLength(100, ErrorMessage = "Loại thiết bị tối đa 100 ký tự.")]
    public string? LoaiThietBi { get; set; }
    [Required(ErrorMessage = "Vui lòng mô tả sự cố."), StringLength(5000, ErrorMessage = "Mô tả tối đa 5.000 ký tự.")]
    public string? MoTa { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn mức độ.")]
    public string? MucDo { get; set; } = "THUONG";
    public string KhoaChongTrung { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed record PhongBaoHong(int HopDongId, int PhongId, string MaPhong, string TenToaNha, string MaHopDong);
public sealed class TaoBaoHongViewModel
{
    public BaoHongInput Input { get; set; } = new();
    public List<PhongBaoHong> Phongs { get; set; } = [];
}
public sealed class BaoHongChiTiet
{
    public DateTime CapNhatGanNhat { get; set; }
    public int SoPhanHoi { get; set; }
    public int SoAnh { get; set; }
    public int? AnhDaiDienThuTu { get; set; }
    public string? NguoiXuLyGanNhat { get; set; }
    public BaoHongCanhBao? CanhBaoQuaHan { get; set; }
    public ChiPhiBaoHong? ChiPhi { get; set; }
    public BaoHongChiPhiInput ChiPhiInput { get; set; } = new();
    public bool ChiPhiDaKhoa { get; set; }
    public string TrangThaiChiPhi { get; set; } = "";
    public List<LichSuChiPhiBaoHong> LichSuChiPhi { get; set; } = [];
    public List<AnhBaoHong> Images { get; set; } = [];
    public List<BaoHongTraoDoi> TraoDois { get; set; } = [];
    public bool LaQuanLy { get; set; }
    public bool LaChuNha { get; set; }
    public bool ChoPhepXuLy { get; set; }
    public List<string> TrangThaiTiepTheo { get; set; } = [];
    public BaoHongPhanHoiInput PhanHoi { get; set; } = new();
    public BaoHongTrangThaiInput CapNhat { get; set; } = new();
    public BaoHong BaoHong { get; init; } = null!;
    public int ToaNhaId { get; init; }
    public string MaPhong { get; init; } = "";
    public string TenToaNha { get; init; } = "";
    public string MaHopDong { get; init; } = "";
    public string TenNguoiBao { get; init; } = "";
}

public sealed record BaoHongCanhBao(TimeSpan ThoiGianCho)
{
    public string ThoiGianChoHienThi => $"{(long)ThoiGianCho.Days * 24 + ThoiGianCho.Hours} giờ {ThoiGianCho.Minutes} phút";
}
