using System.Globalization;

namespace QL_PhongTro.ViewModels;

public sealed class BaoHongBoLoc
{
    public int? ToaNhaId { get; set; }
    public string? TrangThai { get; set; }
    public string? MucDo { get; set; }
    public string? TimKiem { get; set; }
    public string? CanhBao { get; set; }
    public string SapXep { get; set; } = "MOI_NHAT";
    public int Page { get; set; } = 1;
    public bool DangLoc => ToaNhaId.HasValue || !string.IsNullOrEmpty(TrangThai) || !string.IsNullOrEmpty(MucDo) || TimKiem is not null || CanhBao is not null || SapXep != "MOI_NHAT";

    public void ChuanHoa()
    {
        TrangThai = string.IsNullOrWhiteSpace(TrangThai) ? null : TrangThai.Trim();
        MucDo = string.IsNullOrWhiteSpace(MucDo) ? null : MucDo.Trim();
        TimKiem = string.IsNullOrWhiteSpace(TimKiem) ? null : TimKiem.Trim();
        CanhBao = string.IsNullOrWhiteSpace(CanhBao) ? null : CanhBao.Trim();
        Page = Math.Clamp(Page, 1, 100000);
        if (ToaNhaId is <= 0 || TrangThai is not (null or "MOI" or "DANG_XU_LY" or "HOAN_THANH" or "TU_CHOI")
            || MucDo is not (null or "THUONG" or "GAP") || TimKiem?.Length > 200
            || CanhBao is not (null or "QUA_HAN") || SapXep is not ("MOI_NHAT" or "CU_NHAT" or "UU_TIEN"))
            throw new ArgumentException("Điều kiện lọc báo hỏng không hợp lệ.");
    }

    public Dictionary<string, string> RouteValues(int? page = null, int? id = null)
    {
        var values = new Dictionary<string, string>();
        if (id.HasValue) values["id"] = id.Value.ToString(CultureInfo.InvariantCulture);
        if ((page ?? Page) > 1) values["page"] = (page ?? Page).ToString(CultureInfo.InvariantCulture);
        if (ToaNhaId.HasValue) values["toaNhaId"] = ToaNhaId.Value.ToString(CultureInfo.InvariantCulture);
        if (TrangThai is not null) values["trangThai"] = TrangThai;
        if (MucDo is not null) values["mucDo"] = MucDo;
        if (TimKiem is not null) values["timKiem"] = TimKiem;
        if (CanhBao is not null) values["canhBao"] = CanhBao;
        if (SapXep != "MOI_NHAT") values["sapXep"] = SapXep;
        return values;
    }
}

public sealed record ToaNhaBaoHongLoc(int Id, string TenToaNha);
