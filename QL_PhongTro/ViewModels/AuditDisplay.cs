using System.Globalization;
using System.Text.Json;
using QL_PhongTro.Models;

namespace QL_PhongTro.ViewModels;

public sealed record AuditChange(string Label, string Before, string After);
public sealed record AuditDetails(string Subject, string Summary, IReadOnlyList<AuditChange> Changes, bool Unreadable);

public static class AuditDisplay
{
    public static string Sentence(NhatKyHoatDong row)
    {
        var verb = row.HanhDong switch
        {
            "TAO" => "đã tạo", "SUA" => "đã cập nhật", "XOA" => "đã xóa",
            "KHOA" => "đã khóa", "MO_KHOA" => "đã mở khóa",
            "DOI_TRANG_THAI" => "đã đổi trạng thái",
            "GUI_LAI_MAT_KHAU_TAM" => "đã cấp lại mật khẩu tạm cho", _ => "đã thao tác với"
        };
        return $"{row.TenNguoiThucHien ?? "Người dùng chưa ghi nhận tên"} {verb} {Describe(row).Subject} lúc {row.ThoiDiem.AddHours(7):dd/MM/yyyy HH:mm}.";
    }
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["ho_ten"] = "Họ tên", ["vai_tro"] = "Vai trò", ["dang_hoat_dong"] = "Tình trạng hoạt động",
        ["must_change_password"] = "Yêu cầu đổi mật khẩu", ["is_deleted"] = "Đã xóa tài khoản",
        ["chu_nha_id"] = "Chủ nhà", ["quan_ly_id"] = "Người quản lý", ["ten_toa_nha"] = "Tên tòa nhà",
        ["dia_chi"] = "Địa chỉ", ["phuong_xa"] = "Phường/xã", ["quan_huyen"] = "Quận/huyện",
        ["tinh_thanh"] = "Tỉnh/thành phố", ["so_tang"] = "Số tầng", ["ngay_chot_hang_thang"] = "Ngày chốt hằng tháng",
        ["toa_nha_id"] = "Tòa nhà", ["ma_phong"] = "Mã phòng", ["tang"] = "Tầng", ["loai_phong"] = "Loại phòng",
        ["dien_tich"] = "Diện tích", ["gia_thue"] = "Giá thuê mỗi tháng", ["tien_coc_du_kien"] = "Tiền cọc dự kiến",
        ["so_nguoi_toi_da"] = "Số người tối đa", ["trang_thai"] = "Trạng thái",
        ["ma_dich_vu"] = "Mã dịch vụ", ["ten_dich_vu"] = "Tên dịch vụ", ["phong_id"] = "Phòng",
        ["dich_vu_id"] = "Dịch vụ", ["cach_tinh"] = "Cách tính phí", ["don_vi_tinh"] = "Đơn vị tính",
        ["don_gia"] = "Đơn giá", ["tu_ngay"] = "Từ ngày", ["den_ngay"] = "Đến ngày",
        ["dang_ap_dung"] = "Tình trạng áp dụng", ["da_chot_gia"] = "Đã chốt giá", ["hop_dong_id"] = "Hợp đồng",
        ["cau_hinh_dich_vu_id"] = "Bảng giá dịch vụ", ["ma_hop_dong"] = "Mã hợp đồng", ["ma_hoa_don"] = "Mã hóa đơn", ["thang"] = "Tháng",
        ["nam"] = "Năm", ["ngay_chot"] = "Ngày chốt", ["so_nguoi_tinh_phi"] = "Số người tính phí",
        ["loai_hoa_don"] = "Loại hóa đơn", ["han_thanh_toan"] = "Hạn thanh toán", ["tong_tien"] = "Tổng tiền",
        ["ngay_phat_hanh"] = "Ngày phát hành", ["hoa_don_id"] = "Hóa đơn", ["so_thu_tu"] = "Số thứ tự",
        ["ky_hop_dong_id"] = "Kỳ hợp đồng", ["loai_khoan"] = "Loại khoản thu", ["ten_khoan"] = "Tên khoản thu",
        ["cach_tinh_ap_dung"] = "Cách tính phí", ["so_luong"] = "Số lượng", ["chi_so_dau"] = "Chỉ số đầu",
        ["chi_so_cuoi"] = "Chỉ số cuối", ["thanh_tien"] = "Thành tiền"
    };

    public static string Role(string? role) => role switch
    {
        "CHU_NHA" => "Chủ nhà", "QUAN_LY" => "Quản lý", "KHACH_THUE" => "Khách thuê",
        "ADMIN" => "Quản trị viên", _ => "Chưa ghi nhận vai trò"
    };

    public static AuditDetails Describe(NhatKyHoatDong row)
    {
        var before = Read(row.DuLieuTruoc, out var invalidBefore);
        var after = Read(row.DuLieuSau, out var invalidAfter);
        var changes = before.Keys.Union(after.Keys).Select(key => new AuditChange(
            Labels.GetValueOrDefault(key, "Thông tin bổ sung"),
            Format(key, before.GetValueOrDefault(key), row.LoaiDoiTuong),
            Format(key, after.GetValueOrDefault(key), row.LoaiDoiTuong))).ToList();
        var subject = NhatKyViewModel.Types.GetValueOrDefault(row.LoaiDoiTuong, "Dữ liệu");
        var titleField = row.LoaiDoiTuong switch
        {
            "phong_tro" => "ma_phong", "toa_nha" => "ten_toa_nha", "tai_khoan" => "ho_ten",
            "dich_vu" => "ten_dich_vu", "hoa_don" => "ma_hoa_don", "hop_dong" => "ma_hop_dong", _ => ""
        };
        var title = after.GetValueOrDefault(titleField);
        if (title.ValueKind != JsonValueKind.String) title = before.GetValueOrDefault(titleField);
        subject += title.ValueKind == JsonValueKind.String ? " “" + title.GetString() + "”" : " (mã " + row.DoiTuongId + ")";
        var summary = row.HanhDong switch
        {
            "TAO" => "Đã thêm mới", "XOA" => "Đã xóa", "KHOA" => "Đã khóa tài khoản",
            "MO_KHOA" => "Đã mở khóa tài khoản", "GUI_LAI_MAT_KHAU_TAM" => "Đã cấp lại mật khẩu tạm",
            _ when changes.Count > 0 => string.Join("; ", changes.Take(2).Select(x => $"{x.Label}: {x.Before} → {x.After}"))
                + (changes.Count > 2 ? $"; và {changes.Count - 2} thông tin khác" : ""),
            _ => "Đã cập nhật thông tin"
        };
        return new(subject, summary, changes, invalidBefore || invalidAfter);
    }

    private static Dictionary<string, JsonElement> Read(string? json, out bool invalid)
    {
        invalid = false;
        if (json is null) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) { invalid = true; return []; }
            return doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone());
        }
        catch (JsonException) { invalid = true; return []; }
        catch (ArgumentException) { invalid = true; return []; }
    }

    private static string Format(string key, JsonElement value, string type)
    {
        if (value.ValueKind == JsonValueKind.Undefined) return "Không có thông tin";
        if (value.ValueKind == JsonValueKind.Null) return key == "quan_ly_id" ? "Chưa phân công" : "Chưa có";
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var yes = value.GetBoolean();
            return key switch
            {
                "dang_hoat_dong" when type == "tai_khoan" => yes ? "Đang hoạt động" : "Đã khóa",
                "dang_hoat_dong" => yes ? "Đang hoạt động" : "Ngừng hoạt động",
                "dang_ap_dung" => yes ? "Đang áp dụng" : "Ngừng áp dụng",
                _ => yes ? "Có" : "Không"
            };
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            if (key.EndsWith("_id")) return "Mã " + number.ToString("0", CultureInfo.InvariantCulture);
            if (key is "gia_thue" or "tien_coc_du_kien" or "don_gia" or "tong_tien" or "thanh_tien") return number.ToString("N0", Vietnamese) + " đ";
            if (key == "nam") return number.ToString("0", CultureInfo.InvariantCulture);
            var text = number.ToString("#,0.############################", Vietnamese);
            return key switch { "dien_tich" => text + " m²", "so_nguoi_toi_da" or "so_nguoi_tinh_phi" => text + " người", _ => text };
        }
        if (value.ValueKind != JsonValueKind.String) return "Thông tin chi tiết chưa hỗ trợ hiển thị";
        var raw = value.GetString()!;
        if (string.IsNullOrWhiteSpace(raw)) return "Chưa có";
        if (key == "vai_tro") return Role(raw);
        if (key is "tu_ngay" or "den_ngay" or "ngay_chot" or "han_thanh_toan" &&
            DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date.ToString("dd/MM/yyyy");
        if (key == "ngay_phat_hanh" && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            return timestamp.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
        if (key is "trang_thai" or "cach_tinh" or "cach_tinh_ap_dung" or "loai_hoa_don" or "loai_khoan")
            return raw switch
            {
                "TRONG" => "Trống", "DA_DAT_COC" => "Đã đặt cọc", "DANG_THUE" => "Đang thuê", "NGUNG_CHO_THUE" => "Ngừng cho thuê",
                "THEO_CHI_SO" => "Theo chỉ số sử dụng", "THEO_NGUOI" => "Theo số người", "CO_DINH" => "Mức phí cố định",
                "NHAP" => "Bản nháp", "DA_PHAT_HANH" => "Đã phát hành", "DA_HUY" => "Đã hủy", "DA_THANH_TOAN" => "Đã thanh toán",
                "DINH_KY" => "Định kỳ", "DICH_VU" => "Dịch vụ", "TIEN_PHONG" => "Tiền phòng", "DANG_HIEU_LUC" => "Đang hiệu lực",
                _ => "Chưa có tên hiển thị (" + raw + ")"
            };
        return raw;
    }
}
