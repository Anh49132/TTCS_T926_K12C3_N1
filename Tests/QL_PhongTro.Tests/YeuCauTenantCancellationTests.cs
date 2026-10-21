using System.Net;
using System.Text.RegularExpressions;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Theory]
    [InlineData("MOI")]
    [InlineData("DA_HEN_LICH")]
    public async Task TenantCanCancelOpenRequest_AndListShowsCancelledState(string status)
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], status, "YC-TENANT-CANCEL");
        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        var token = AntiForgery(list);

        var missingToken = await tenant.PostAsync($"/YeuCau/Huy/{requestId}", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        Assert.Equal(status, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());

        var response = await PostCancellation(tenant, requestId, token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/YeuCau", response.Headers.Location?.OriginalString);
        Assert.Equal("DA_HUY", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id AND trang_thai_moi='DA_HUY'", ("$id", requestId)));

        var updatedList = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        var row = Regex.Match(updatedList, "(?s)<tr data-request-status=\"DA_HUY\"[^>]*>.*?</tr>").Value;
        Assert.Contains("YC-TENANT-CANCEL", row);
        Assert.Contains("Đã huỷ", row);
        Assert.DoesNotContain("data-open-cancel-confirmation", row);
        Assert.Contains("Đã huỷ yêu cầu.", updatedList);

        var logout = await tenant.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(updatedList)
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var reauthenticatedTenant = await Login("KHACH_THUE");
        var afterLogin = WebUtility.HtmlDecode(await reauthenticatedTenant.GetStringAsync("/YeuCau"));
        Assert.Equal("DA_HUY", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        var rowAfterLogin = Regex.Match(afterLogin, "(?s)<tr data-request-status=\"DA_HUY\"[^>]*>.*?</tr>").Value;
        Assert.Contains("YC-TENANT-CANCEL", rowAfterLogin);
        Assert.Contains("Đã huỷ", rowAfterLogin);
        Assert.DoesNotContain("data-open-cancel-confirmation", rowAfterLogin);
        Assert.DoesNotContain("Khôi phục", rowAfterLogin);
    }

    [Theory]
    [InlineData("TU_CHOI")]
    [InlineData("DA_HUY")]
    [InlineData("DA_DUYET")]
    [InlineData("UNEXPECTED")]
    public async Task TenantCannotCancelRequestInClosedOrUnknownState(string status)
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], status, "YC-TENANT-NO-CANCEL");
        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));

        var response = await PostCancellation(tenant, requestId, AntiForgery(list));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(status, Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id AND trang_thai_moi='DA_HUY'", ("$id", requestId)));
        var updatedList = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        Assert.Contains("Chỉ yêu cầu mới hoặc đã hẹn lịch mới được hủy.", updatedList);
    }

    [Fact]
    public async Task TenantCannotCancelAnotherTenantsRequestByTamperingWithId()
    {
        var requestId = CreateTenantCancellationRequest(accounts["ADMIN"], "MOI", "YC-OTHER-TENANT");
        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));

        var response = await PostCancellation(tenant, requestId, AntiForgery(list));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("MOI", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id", ("$id", requestId)));
        Assert.Contains("Không thể huỷ yêu cầu này.", WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau")));
    }

    [Fact]
    public async Task TenantCannotRestoreCancelledRequestDirectly_AndListHasNoRestoreAction()
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HUY", "YC-TENANT-NO-RESTORE");
        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));
        var row = Regex.Match(list, "(?s)<tr data-request-status=\"DA_HUY\"[^>]*>.*?</tr>").Value;
        Assert.Contains("YC-TENANT-NO-RESTORE", row);
        Assert.Contains("Đã huỷ", row);
        Assert.DoesNotContain("data-open-cancel-confirmation", row);
        Assert.DoesNotContain("Khôi phục", row);

        var restoreAttempt = await tenant.PostAsync($"/YeuCau/KhoiPhuc/{requestId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AntiForgery(list)
        }));

        Assert.Equal(HttpStatusCode.NotFound, restoreAttempt.StatusCode);
        Assert.Equal("DA_HUY", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
    }

    [Fact]
    public async Task TenantCancellationRejectsRequestWhoseStatusChangedAfterListWasLoaded()
    {
        var requestId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "MOI", "YC-TENANT-STALE");
        using var tenant = await Login("KHACH_THUE");
        var list = WebUtility.HtmlDecode(await tenant.GetStringAsync("/YeuCau"));

        Execute("UPDATE yeu_cau_thue SET trang_thai='DA_DUYET', phien_ban=phien_ban+1 WHERE id=$id", ("$id", requestId));
        var response = await PostCancellation(tenant, requestId, AntiForgery(list));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$id AND trang_thai_moi='DA_HUY'", ("$id", requestId)));
    }

    private int CreateTenantCancellationRequest(int tenantAccountId, string status, string code)
    {
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var tenantId = Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$account", ("$account", tenantAccountId));
        if (tenantId is null or DBNull)
        {
            Execute("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES($account,'Khách test','2026-01-01')",
                ("$account", tenantAccountId));
            tenantId = Scalar("SELECT id FROM khach_thue WHERE tai_khoan_id=$account", ("$account", tenantAccountId));
        }

        Execute("""
            INSERT INTO yeu_cau_thue(ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao)
            VALUES($code,$listing,$tenant,'XEM_PHONG','2026-10-20',1,$status,'2026-10-01 12:00:00')
            """, ("$code", code), ("$listing", listingId), ("$tenant", Convert.ToInt32(tenantId)), ("$status", status));
        return Convert.ToInt32(Scalar("SELECT id FROM yeu_cau_thue WHERE ma_yeu_cau=$code", ("$code", code)));
    }

    private static Task<HttpResponseMessage> PostCancellation(HttpClient tenant, int requestId, string token) =>
        tenant.PostAsync($"/YeuCau/Huy/{requestId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
}
