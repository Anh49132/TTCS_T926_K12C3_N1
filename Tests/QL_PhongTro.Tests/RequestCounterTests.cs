using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Theory]
    [InlineData(null, 21)]
    [InlineData(1, 21)]
    [InlineData(75, 76)]
    public async Task RequestCodeContinuesLegacyRowsAndNeverLowersCounter(int? counter, int expected)
    {
        var oldId = CreateTenantCancellationRequest(accounts["KHACH_THUE"], "DA_HUY", "YC-LEGACY");
        var listing = Convert.ToInt32(Scalar("SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$id", ("$id", oldId)));
        var month = DateTime.UtcNow.AddHours(7).ToString("yyyyMM");
        var prefix = $"YC-{month}-";
        Execute("UPDATE yeu_cau_thue SET ma_yeu_cau=$code WHERE id=$id", ("$code", prefix+"0020"), ("$id", oldId));
        Execute("DELETE FROM rental_request_counter WHERE thang=$month", ("$month", month));
        if (counter.HasValue) Execute("INSERT INTO rental_request_counter(thang,so_cuoi) VALUES($month,$number)", ("$month", month), ("$number", counter.Value));
        Execute("UPDATE tai_khoan SET email_confirmed=1,must_change_password=0 WHERE id=$id", ("$id", accounts["KHACH_THUE"]));
        var sender = new CapturedRequestEmails();
        using var emailFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IAppointmentEmailSender>(sender)));
        using var tenant = emailFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect=false });
        await Login(tenant,"KHACH_THUE");
        var page = await tenant.GetStringAsync($"/TinDang/ChiTiet/{listing}");
        var response = await tenant.PostAsync($"/TinDang/GuiYeuCau/{listing}",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"]=AntiForgery(page), ["Form.LoaiYeuCau"]="XEM_PHONG",
            ["Form.NgayMongMuon"]=DateTime.UtcNow.AddHours(7).AddDays(1).ToString("yyyy-MM-dd"), ["Form.SoNguoiDuKien"]="1"
        }));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        Assert.Equal(prefix+expected.ToString("D4"),Scalar("SELECT ma_yeu_cau FROM yeu_cau_thue ORDER BY id DESC LIMIT 1"));
        Assert.Equal(prefix+"0020",Scalar("SELECT ma_yeu_cau FROM yeu_cau_thue WHERE id=$id",("$id",oldId)));
        Assert.Equal(expected,Convert.ToInt32(Scalar("SELECT so_cuoi FROM rental_request_counter WHERE thang=$month",("$month",month))));
        Assert.Single(sender.Calls);
    }
}
