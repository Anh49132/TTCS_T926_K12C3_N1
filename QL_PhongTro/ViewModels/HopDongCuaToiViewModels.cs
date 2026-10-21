namespace QL_PhongTro.ViewModels;

public class HopDongCuaToiViewModel
{
    public bool SchemaReady { get; set; } = true;
    public string? Message { get; set; }
    public int ActiveCount => Contracts.Count(x => x.Category == "active");
    public int EndedCount => Contracts.Count(x => x.Category == "ended");
    public List<HopDongCardViewModel> Contracts { get; set; } = [];
}

public class HopDongCardViewModel
{
    public int Id { get; set; }
    public int? KhachDungTenId { get; set; }
    public string MaHopDong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenToaNha { get; set; } = "";
    public string DiaChiToaNha { get; set; } = "";
    public long GiaThue { get; set; }
    public long TienCoc { get; set; }
    public DateOnly? NgayBatDau { get; set; }
    public DateOnly? NgayKetThuc { get; set; }
    public DateOnly? NgayTraPhong { get; set; }
    public string TrangThai { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public string Category { get; set; } = "other";
    public int? SoNgayConLai { get; set; }
    public bool SapHetHan => SoNgayConLai is >= 0 and < 30;
}

public class HopDongChiTietViewModel
{
    public int NgayChot { get; set; }
    public int SoNguoiToiDa { get; set; }
    public int ThoiHanThang { get; set; }
    public string? AnhPhong { get; set; }
    public int? TinDangId { get; set; }
    public string TrangThaiPhong { get; set; } = "";
    public DateOnly? NgayChotTiepTheo { get; set; }
    public string MaHopDong { get; set; } = "";
    public string MaPhong { get; set; } = "";
    public string TenToaNha { get; set; } = "";
    public string DiaChiToaNha { get; set; } = "";
    public long TienCoc { get; set; }
    public string TrangThai { get; set; } = "";
    public DateOnly? NgayTraPhong { get; set; }
    public List<KyHopDongViewModel> CacKy { get; set; } = [];
    public List<NguoiOViewModel> NguoiO { get; set; } = [];
    public List<DichVuHopDongViewModel> DichVus { get; set; } = [];
    public long GiaThueHienTai { get; set; }
    public int? SoNgayConLai { get; set; }
    public bool SapHetHan => SoNgayConLai is >= 0 and < 30;
    public DateOnly? NgayBatDau => CacKy.FirstOrDefault()?.NgayBatDau;
    public DateOnly? NgayKetThuc => CacKy.LastOrDefault()?.NgayKetThuc;
}

public class DichVuHopDongViewModel
{
    public string TenDichVu { get; set; } = "";
    public string CachTinh { get; set; } = "";
    public string DonViTinh { get; set; } = "";
    public long DonGia { get; set; }
}

public class KyHopDongViewModel
{
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public long GiaThue { get; set; }
}

public class NguoiOViewModel
{
    public string HoTen { get; set; } = "";
    public string VaiTro { get; set; } = "";
    public DateOnly NgayVao { get; set; }
    public DateOnly? NgayChuyenDi { get; set; }
    public bool DangO { get; set; }
}
