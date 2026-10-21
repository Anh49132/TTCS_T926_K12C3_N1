using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed class PhatHanhThangViewModel
{
    public int ToaNhaId { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }
    public DateOnly NgayPhatHanh { get; set; }
    public DateOnly HanThanhToan { get; set; }
    [ValidateNever] public string ReviewToken { get; set; } = "";
    [ValidateNever] public bool SanSang { get; set; }
    [ValidateNever] public List<SelectListItem> ToaNhas { get; set; } = [];
    [ValidateNever] public int TongPhong { get; set; }
    [ValidateNever] public List<HoaDonThangDuKien> DuKien { get; set; } = [];
    [ValidateNever] public List<HoaDonThangDuKien> DaPhatHanh { get; set; } = [];
    [ValidateNever] public List<PhongHoaDonBoQua> BoQua { get; set; } = [];
    [ValidateNever] public List<KetQuaPhongHoaDon> KetQua { get; set; } = [];
}

public sealed record HoaDonThangDuKien(string MaPhong, HoaDon HoaDon, List<ChiSoDienNuoc> ChiSo)
{
    public int Tang { get; init; }
    public string TenKhach { get; init; } = "";
    public int PhienBanPhong { get; init; }
}
public sealed record PhongHoaDonBoQua(int HopDongId, string MaPhong, string LyDo)
{
    public string TrangThai { get; init; } = "BO_QUA";
    public bool ThieuChiSo { get; init; }
    public int? HoaDonId { get; init; }
}
public sealed record KetQuaPhongHoaDon(int HopDongId, string MaPhong, string TrangThai, string LyDo, int? HoaDonId = null)
{
    public bool ThieuChiSo { get; init; }
    public string MaHoaDon { get; init; } = "";
    public string TenKhach { get; init; } = "";
    public int Tang { get; init; }
    public long TongTien { get; init; }
}
public sealed record KetQuaPhatHanhThang(List<KetQuaPhongHoaDon> Phongs)
{
    public bool LaPhatHanh { get; init; }
    public int SoThatBai => Phongs.Count(x => x.TrangThai == "THAT_BAI");
    public int SoDaPhatHanh => Phongs.Count(x => x.TrangThai == "DA_PHAT_HANH");
    public int SoNhap => Phongs.Count(x => x.TrangThai == "NHAP");
    public int SoDaTao => SoDaPhatHanh + SoNhap;
    public int SoThieuChiSo => Phongs.Count(x => x.ThieuChiSo);
    public int SoDaCoHoaDon => Phongs.Count(x => x.TrangThai == "DA_CO_HOA_DON");
    public int SoBoQuaKhac => Phongs.Count - SoDaTao - SoThieuChiSo - SoDaCoHoaDon;
    public decimal TongTien => Phongs.Where(x => x.TrangThai is "DA_PHAT_HANH" or "NHAP").Sum(x => (decimal)x.TongTien);
    public string MaLanChay { get; init; } = Guid.NewGuid().ToString("N");
    public int NguoiThucHienId { get; init; }
    public string NguoiThucHien { get; init; } = "";
    public int ToaNhaId { get; init; }
    public string TenToaNha { get; init; } = "";
    public int Nam { get; init; }
    public int Thang { get; init; }
    public DateOnly NgayPhatHanh { get; init; }
    public DateOnly HanThanhToan { get; init; }
    public DateTime BatDauUtc { get; init; }
    public DateTime HoanTatUtc { get; init; }
    public double SoGiay { get; init; }
}
