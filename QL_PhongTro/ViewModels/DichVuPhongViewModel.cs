using QL_PhongTro.Models;
using QL_PhongTro.Services;

namespace QL_PhongTro.ViewModels;

public record DongDichVuPhong(int Id, string Ten, bool DaChon, DonGiaDichVu? Gia, long? DonGiaRieng, DateOnly? NgungTuKy = null)
{
    public long? DonGiaHieuLuc => DonGiaRieng ?? Gia?.DonGia;
    public bool KhacGiaChung => DonGiaRieng is not null && Gia is not null && DonGiaRieng != Gia.DonGia;
    public decimal? TongThang => Gia?.CachTinh == CachTinhDichVu.CoDinh ? DonGiaHieuLuc : null;
}

public class DichVuPhongViewModel
{
    public int PhongId { get; set; }
    public int ToaNhaId { get; set; }
    public string MaPhong { get; set; } = "";
    public List<DongDichVuPhong> DichVus { get; set; } = [];
    public List<NgungDichVuPhong> LichSuNgung { get; set; } = [];
    public decimal TongCoDinh => DichVus.Where(x => x.DaChon).Sum(x => x.TongThang ?? 0m);
}
