using System.Net;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task ViewingApprovalRequiresConsentAndCsrfThenOpensContractForm()
    {
        var id = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HEN_LICH", "YC-VIEWING-CONTRACT");
        using var owner = await Login("CHU_NHA");
        var detail = await owner.GetStringAsync($"/LichHen/ChiTiet/{id}");
        Assert.Contains("DuyetSauKhiXem", detail);
        var deniedCsrf = await owner.PostAsync("/LichHen/DuyetSauKhiXem", new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = id.ToString(), ["daDongYThue"] = "true" }));
        Assert.Equal(HttpStatusCode.BadRequest, deniedCsrf.StatusCode);
        var fields = new Dictionary<string,string> { ["id"] = id.ToString(), ["__RequestVerificationToken"] = AntiForgery(detail) };
        await owner.PostAsync("/LichHen/DuyetSauKhiXem", new FormUrlEncodedContent(fields));
        Assert.Equal("DA_HEN_LICH", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        fields["daDongYThue"] = "true";
        var approved = await owner.PostAsync("/LichHen/DuyetSauKhiXem", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        Assert.Equal("DA_DUYET", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", id)));
        detail = WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{id}"));
        Assert.Contains("Lập hợp đồng", detail);
        var contract = await owner.GetAsync($"/LichHen/LapHopDong?id={id}");
        Assert.Equal(HttpStatusCode.Redirect, contract.StatusCode);
        Assert.Contains("/HopDong/Create", contract.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(contract.Headers.Location)).StatusCode);
    }
}
