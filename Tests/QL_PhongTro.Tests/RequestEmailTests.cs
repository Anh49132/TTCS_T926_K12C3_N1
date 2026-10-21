using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestEmailsUseOwnerAndTenantRecipientsAndDoNotUndoSavedChanges(bool mailFails)
    {
        var oldId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HUY", "YC-EMAIL-OLD");
        var listingId = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", oldId)));
        Execute("UPDATE tai_khoan SET email_confirmed=1,must_change_password=0 WHERE id=$id", ("$id", accounts["KHACH_THUE"]));
        var sender = new CapturedRequestEmails { Fail = mailFails };
        using var emailFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAppointmentEmailSender>(sender)));
        using var tenant = emailFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(tenant, "KHACH_THUE");
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1).ToString("yyyy-MM-dd");
        var page = await tenant.GetStringAsync($"/TinDang/ChiTiet/{listingId}");
        var fields = new Dictionary<string,string> { ["__RequestVerificationToken"] = AntiForgery(page), ["Form.LoaiYeuCau"] = "XEM_PHONG", ["Form.NgayMongMuon"] = date, ["Form.SoNguoiDuKien"] = "1" };
        var response = await tenant.PostAsync($"/TinDang/GuiYeuCau/{listingId}", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(sender.Calls);
        Assert.Equal(("new", "chu_nha@s104.test"), sender.Calls[0]);
        var requestId = Convert.ToInt32(Scalar("SELECT MAX(id) FROM yeu_cau_thue"));
        Assert.Equal("MOI", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId)));
        await tenant.PostAsync($"/TinDang/GuiYeuCau/{listingId}", new FormUrlEncodedContent(fields));
        Assert.Single(sender.Calls);
        var denied = await tenant.PostAsync("/LichHen/XacNhanLich", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = AntiForgery(page), ["id"] = requestId.ToString(), ["LichHenNhap"] = date + "T14:30"
        }));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Single(sender.Calls);

        using var owner = emailFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(owner, "CHU_NHA");
        var detail = await owner.GetStringAsync($"/LichHen/ChiTiet/{requestId}");
        var confirm = await owner.PostAsync("/LichHen/XacNhanLich", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = AntiForgery(detail), ["id"] = requestId.ToString(), ["LichHenNhap"] = date + "T14:30"
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal(("confirmed", "khach_thue@s104.test"), sender.Calls[1]);
        Assert.Equal("DA_HEN_LICH", Scalar("SELECT trang_thai FROM yeu_cau_thue WHERE id=$id", ("$id", requestId)));
        detail = await owner.GetStringAsync($"/LichHen/ChiTiet/{requestId}");
        if (mailFails) Assert.Contains("chưa gửi được email", WebUtility.HtmlDecode(detail));
        var change = await owner.PostAsync("/LichHen/DoiLich", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = AntiForgery(detail), ["id"] = requestId.ToString(), ["LichHenNhap"] = date + "T19:30"
        }));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.Equal(("changed", "khach_thue@s104.test"), sender.Calls[2]);
        Assert.Contains("12:30", Scalar("SELECT lich_hen FROM yeu_cau_thue WHERE id=$id", ("$id", requestId))?.ToString());
        if (mailFails) Assert.Contains("chưa gửi được email", WebUtility.HtmlDecode(await owner.GetStringAsync($"/LichHen/ChiTiet/{requestId}")));
    }

    private sealed class CapturedRequestEmails : IAppointmentEmailSender
    {
        public bool Fail { get; init; }
        public List<(string Kind, string Email)> Calls { get; } = [];
        private Task Capture(string kind, string email) { Calls.Add((kind,email)); return Fail ? Task.FromException(new InvalidOperationException("SMTP unavailable")) : Task.CompletedTask; }
        public Task SendConfirmedAsync(string email,string name,string code,DateTime local,int id) => Capture("confirmed",email);
        public Task SendRescheduledAsync(string email,string name,string code,DateTime local,int id) => Capture("changed",email);
        public Task SendNewRequestAsync(string email,string name,string code,string tenant,string room,string type,DateOnly date,int id) => Capture("new",email);
    }
}
