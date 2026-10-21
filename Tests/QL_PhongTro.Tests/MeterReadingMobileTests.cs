using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    [Fact]
    public async Task MobileUsesOneRoomFormUniqueIdsAndAdjacentAccessibleReference()
    {
        MeterFixture(); using var factory = Factory(); using var manager = await Login(factory, "manager@meter.test");
        var html = WebUtility.HtmlDecode(await manager.GetStringAsync("/ChiSoDienNuoc?toaNhaId=1"));
        Assert.Contains("meter-readings.css", html);
        var ids = Regex.Matches(html, "\\bid=\"([^\"]+)\"").Select(x => x.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        var forms = Regex.Matches(html, "(?s)<form[^>]*data-meter-form.*?</form>").Select(x => x.Value).ToArray();
        Assert.Equal(2, forms.Length);
        Assert.Equal(2, forms.Select(x => Regex.Match(x, "name=\"PhongId\" value=\"(\\d+)\"").Groups[1].Value).Distinct().Count());
        foreach (var form in forms)
        {
            Assert.Single(Regex.Matches(form, "name=\"__RequestVerificationToken\""));
            foreach (var input in Regex.Matches(form, "<input[^>]*data-meter-input[^>]*>").Select(x => x.Value))
            {
                var id = Regex.Match(input, "id=\"([^\"]+)\"").Groups[1].Value;
                Assert.Contains("type=\"number\"", input); Assert.Contains("inputmode=\"decimal\"", input);
                Assert.Contains("step=\"0.001\"", input); Assert.Contains("min=\"0\"", input);
                Assert.Contains("max=\"99999999999.999\"", input); Assert.Contains("required", input);
                Assert.Contains($"aria-describedby=\"{id}-previous {id}-error\"", input);
                Assert.Contains($"id=\"{id}-previous\"", form); Assert.Contains($"id=\"{id}-error\"", form);
                Assert.True(form.IndexOf($"id=\"{id}-previous\"", StringComparison.Ordinal) < form.IndexOf(input, StringComparison.Ordinal));
            }
        }
        Assert.Contains("Đã chốt: 0/2 · Còn lại: 2 phòng", html);
    }

    [Fact]
    public async Task MobileErrorKeepsRoomAndValuesThenSaveReturnsToSameRoom()
    {
        MeterFixture(); using var factory = Factory(); using var manager = await Login(factory, "manager@meter.test");
        var fields = await MeterForm(manager); fields["DienMoi"] = "1"; fields["NuocMoi"] = "12";
        var response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"meter-room-1\" data-meter-room tabindex=\"-1\" data-meter-active=\"true\"", html);
        var error = Regex.Match(html, "(?s)<span id=\"meter-1-NUOC-error\".*?</span>").Value;
        Assert.Contains("nhỏ hơn", error); Assert.Equal(0, Count("chi_so_dien_nuoc"));
        Assert.Contains("value=\"1\" class=\"form-control", html);
        fields["NuocMoi"] = "13";
        response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/ChiSoDienNuoc?toaNhaId=1#meter-room-1", response.Headers.Location!.OriginalString);
        html = WebUtility.HtmlDecode(await manager.GetStringAsync(response.Headers.Location));
        Assert.Contains("Đã chốt: 1/2 · Còn lại: 1 phòng", html); Assert.Equal(2, Count("chi_so_dien_nuoc"));
    }

    [Fact]
    public async Task MobileWarningRequiresExplicitConfirmationAndReturnsToRoom()
    {
        UsageFixture(); using var factory = Factory(); using var manager = await Login(factory, "manager@meter.test");
        var fields = await MeterForm(manager); fields["DienMoi"] = "51"; fields["NuocMoi"] = "40";
        var before = Count("chi_so_dien_nuoc");
        var response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("role=\"alert\" tabindex=\"-1\" data-usage-warning", html);
        Assert.Contains("meter-confirm", html); Assert.Contains("Chưa lưu chỉ số", html);
        fields["MaXacNhan"] = Regex.Match(html, "name=\"MaXacNhan\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(fields["MaXacNhan"]);
        response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(before, Count("chi_so_dien_nuoc"));
        html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        fields["MaXacNhan"] = Regex.Match(html, "name=\"MaXacNhan\" value=\"([^\"]+)\"").Groups[1].Value;
        fields["XacNhanBatThuong"] = "true";
        response = await manager.PostAsync("/ChiSoDienNuoc/Save", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("#meter-room-1", response.Headers.Location!.OriginalString);
        Assert.Equal(new[] { true, false }, await ConfirmationFlags());
    }
}
