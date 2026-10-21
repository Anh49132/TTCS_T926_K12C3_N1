using System.Net;
using System.Text.RegularExpressions;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task TenantRequestList_ShowsCancelOnlyForOpenStatuses_AndConfirmationIsClientOnly()
    {
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        Execute("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES($account,'Khách test','2026-01-01')",
            ("$account", accounts["KHACH_THUE"]));
        var tenantId = Convert.ToInt32(Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$account",
            ("$account", accounts["KHACH_THUE"])));
        var statuses = new[] { "MOI", "DA_HEN_LICH", "TU_CHOI", "DA_HUY", "DA_DUYET", "UNEXPECTED" };
        for (var index = 0; index < statuses.Length; index++)
        {
            Execute("""
                INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao)
                VALUES($code,$listing,$tenant,'XEM_PHONG','2026-10-20',1,$status,$created)
                """,
                ("$code", "YC-CANCEL-" + index), ("$listing", listingId), ("$tenant", tenantId),
                ("$status", statuses[index]), ("$created", $"2026-10-{index + 1:D2}"));
        }

        var before = Scalar("SELECT group_concat(ma_yeu_cau || ':' || trang_thai, ',') FROM (SELECT ma_yeu_cau,trang_thai FROM yeu_cau_thue WHERE khach_thue_id=$tenant ORDER BY ma_yeu_cau)",
            ("$tenant", tenantId))?.ToString();
        using var tenant = await Login("KHACH_THUE");
        var html = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));

        foreach (var status in statuses)
        {
            var row = Regex.Match(html, $"(?s)<tr data-request-status=\\\"{Regex.Escape(status)}\\\".*?</tr>").Value;
            Assert.NotEmpty(row);
            var mayCancel = status is "MOI" or "DA_HEN_LICH";
            Assert.Equal(mayCancel, row.Contains("data-open-cancel-confirmation", StringComparison.Ordinal));
        }

        Assert.Contains("Bạn có chắc chắn muốn huỷ yêu cầu này không?", html);
        var dialog = Regex.Match(html, "(?s)<dialog id=\"tenant-request-cancel-confirmation\".*?</dialog>").Value;
        Assert.NotEmpty(dialog);
        Assert.Contains("<form method=\"dialog\">", dialog);
        Assert.Contains("value=\"cancel\"", dialog);
        Assert.Contains("Quay lại", dialog);
        Assert.Contains("value=\"confirm\"", dialog);
        Assert.Contains("Huỷ yêu cầu", dialog);
        Assert.DoesNotContain("method=\"post\"", dialog);

        var script = File.ReadAllText(Path.Combine(appPath, "wwwroot", "js", "tenant-request-cancel-confirmation.js"));
        Assert.Contains("window.location.assign(url)", script);
        Assert.Contains("dialog.showModal()", script);
        Assert.Contains("dialog.returnValue === 'confirm'", script);
        Assert.Contains("form.requestSubmit()", script);
        Assert.Contains("event.target.closest('a, button, form, input, select, textarea')", script);

        var after = Scalar("SELECT group_concat(ma_yeu_cau || ':' || trang_thai, ',') FROM (SELECT ma_yeu_cau,trang_thai FROM yeu_cau_thue WHERE khach_thue_id=$tenant ORDER BY ma_yeu_cau)",
            ("$tenant", tenantId))?.ToString();
        Assert.Equal(before, after);
    }
}
