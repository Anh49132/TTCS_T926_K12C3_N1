namespace QL_PhongTro.ViewModels;

public sealed record ToaNhaGhiChiSo(int Id, string Ten);
public sealed record ChiSoThamChieu(decimal? GiaTri, bool LaBanGiao = false);
public sealed record PhongGhiChiSo(int PhongId, int HopDongId, string MaPhong, int Tang,
    ChiSoThamChieu Dien, ChiSoThamChieu Nuoc, bool DaChot)
{
    public List<MeterServiceRow> DichVu { get; set; } = [];
    public int PhienBanPhong { get; set; }
}

public sealed record MeterServiceRow(int Id, string Ma, ChiSoThamChieu Truoc, decimal? Moi, int PhienBan, bool DaKhoa);
public sealed class LuuChiSoInput
{
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(ContractMeterBinder))]
    public decimal? DienDau { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(ContractMeterBinder))]
    public decimal? NuocDau { get; set; }
    public bool XacNhanBatThuong { get; set; }
    public string? MaXacNhan { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public List<MeterUsageWarning> CanhBaos { get; set; } = [];
    public int ToaNhaId { get; set; }
    public int PhongId { get; set; }
    public int HopDongId { get; set; }
    public DateOnly Ky { get; set; }
    public int PhienBanPhong { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(ContractMeterBinder))]
    public decimal? DienMoi { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinder(BinderType=typeof(ContractMeterBinder))]
    public decimal? NuocMoi { get; set; }
    public int DienPhienBan { get; set; } = -1;
    public int NuocPhienBan { get; set; } = -1;
}

public sealed class ChiSoDienNuocViewModel
{
    public List<ToaNhaGhiChiSo> ToaNhas { get; set; } = [];
    public int? ToaNhaId { get; set; }
    public DateOnly DauKy { get; set; }
    public bool KyDaKhoa { get; set; }
    public List<PhongGhiChiSo> Phongs { get; set; } = [];
    public int TongPhongCanChot => Phongs.Count(x => x.DichVu.Count > 0);
    public int SoPhongDaChot => Phongs.Count(x => x.DichVu.Count > 0 && x.DaChot);
    public int SoPhongConLai => TongPhongCanChot - SoPhongDaChot;
    public Dictionary<string,string[]> Loi { get; set; } = [];
    public LuuChiSoInput? Input { get; set; }
}

public sealed record MeterUsageWarning(string Ma, decimal ChiSoTruoc, decimal ChiSoMoi, decimal TieuThu, decimal TrungBinh, decimal Nguong, decimal TieuThuKyTruoc);
