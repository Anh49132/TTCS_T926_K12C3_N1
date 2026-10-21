using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed record YeuCauThueItemViewModel(
    int Id,
    string MaYeuCau,
    int TinDangId,
    string MaPhong,
    DateTime NgayTao,
    string TrangThai,
    string TrangThaiHienThi,
    DateTime? LichHen,
    string? LyDoTuChoi)
{
    public string LoaiYeuCau { get; init; } = "XEM_PHONG";
    public string TenToaNha { get; init; } = "";
    public bool ChoPhepHuy => TrangThai is LichHenTrangThai.Moi or LichHenTrangThai.DaHenLich;
}

public sealed record ChiTietYeuCauKhachViewModel(YeuCauThue YeuCau, TinDangChiTietViewModel? TinDang, IReadOnlyList<QL_PhongTro.Services.LichHenLichSuMuc> LichSu);
