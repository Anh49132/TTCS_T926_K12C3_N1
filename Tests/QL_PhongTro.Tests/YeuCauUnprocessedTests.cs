using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio()
    {
        var moi = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "MOI", "YC-UNPROCESSED-NEW");
        var scheduled = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HEN_LICH", "YC-UNPROCESSED-SCHEDULED");
        var closed = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_DUYET", "YC-UNPROCESSED-CLOSED");
        var old = DateTime.UtcNow.AddHours(-25).ToString("O");
        Execute("UPDATE yeu_cau_thue SET ngay_tao=$old WHERE id IN ($new,$scheduled,$closed)",
            ("$old", old), ("$new", moi), ("$scheduled", scheduled), ("$closed", closed));
        using var owner = await Login("CHU_NHA");

        var html = WebUtility.HtmlDecode(await owner.GetStringAsync("/YeuCau"));

        Assert.Contains("aria-label=\"2 yêu cầu chưa xử lý\"", html);
        foreach (var code in new[] { "YC-UNPROCESSED-NEW", "YC-UNPROCESSED-SCHEDULED" })
        {
            var row = Regex.Match(html, $"(?s)<tr[^>]*>.*?{code}.*?</tr>").Value;
            Assert.Contains("Quá 24 giờ, chưa xử lý", row);
        }
        var closedRow = Regex.Match(html, "(?s)<tr[^>]*>.*?YC-UNPROCESSED-CLOSED.*?</tr>").Value;
        Assert.DoesNotContain("Quá 24 giờ, chưa xử lý", closedRow);
        Assert.Contains("Lịch hẹn hôm nay", html);
        Assert.Contains("owner-requests.js", html);
        var detail = WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{moi}"));
        Assert.Contains("Thông tin yêu cầu", detail);
        Assert.Contains("Phòng được yêu cầu", detail);
        Assert.Contains("XacNhanLich", detail);
        Assert.Contains("TuChoi", detail);
        Execute("UPDATE yeu_cau_thue SET loai_yeu_cau='THUE_NGAY' WHERE id=$id", ("$id", moi));
        var instant = WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{moi}"));
        Assert.Contains("DuyetThueNgay", instant);
        Assert.DoesNotContain("action=\"/LichHen/XacNhanLich", instant);
    }
}
