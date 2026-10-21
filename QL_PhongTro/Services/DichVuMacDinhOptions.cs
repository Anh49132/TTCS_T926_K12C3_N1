namespace QL_PhongTro.Services;

// Prices deliberately remain unconfigured until the PO/operator supplies them.
public sealed class DichVuMacDinhOptions
{
    public List<DichVuMacDinhDefinition> DanhSach { get; set; } = [];
    public static List<DichVuMacDinhDefinition> DeXuat() =>
    [
        new("DIEN", "Điện", "THEO_CHI_SO", "kWh"),
        new("NUOC", "Nước", "THEO_CHI_SO", "m³"),
        new("RAC", "Rác", "THEO_NGUOI", "người/tháng"),
        new("GUI_XE", "Gửi xe", "CO_DINH", "phòng/tháng"),
        new("INTERNET", "Internet", "CO_DINH", "phòng/tháng")
    ];
    public static readonly string[] Codes = ["DIEN", "NUOC", "RAC", "GUI_XE", "INTERNET"];
    public static bool LaMacDinh(string code) => Codes.Contains(code);
}

public sealed class DichVuMacDinhDefinition(string ma, string ten, string cachTinh, string donVi)
{
    public DichVuMacDinhDefinition() : this("", "", "", "") { }
    public string Ma { get; set; } = ma;
    public string Ten { get; set; } = ten;
    public string CachTinh { get; set; } = cachTinh;
    public string DonVi { get; set; } = donVi;
    public long? DonGia { get; set; }
}
