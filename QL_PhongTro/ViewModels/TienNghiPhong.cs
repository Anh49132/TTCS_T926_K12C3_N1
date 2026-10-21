namespace QL_PhongTro.ViewModels;

public static class TienNghiPhong
{
    public static readonly IReadOnlyDictionary<string, string> Options = new Dictionary<string, string>
    {
        ["DIEU_HOA"] = "Điều hòa", ["BINH_NONG_LANH"] = "Bình nóng lạnh",
        ["GIUONG"] = "Giường", ["TU_QUAN_AO"] = "Tủ quần áo",
        ["BEP_RIENG"] = "Khu bếp riêng", ["BAN_CONG"] = "Ban công",
        ["MAY_GIAT"] = "Máy giặt", ["TU_LANH"] = "Tủ lạnh"
    };
}
