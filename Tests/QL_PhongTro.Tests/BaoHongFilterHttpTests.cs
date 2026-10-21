using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class BaoHongTests
{
    private static WebApplicationFactory<Program> FilterWeb(Fixture f)
    {
        var root=Directory.GetCurrentDirectory();
        while(!Directory.Exists(Path.Combine(root,"QL_PhongTro","Data"))) root=Directory.GetParent(root)!.FullName;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder=> {
            builder.UseContentRoot(Path.Combine(root,"QL_PhongTro"));builder.UseSetting("DatabasePath",f.Path);
            builder.UseSetting("RoomImagesPath",f.Path+".room-images");builder.UseEnvironment("Development");
            builder.ConfigureLogging(x=>x.ClearProviders());
            builder.ConfigureServices(x=>{x.AddDataProtection().UseEphemeralDataProtectionProvider();x.RemoveAll<ITimeProvider>();x.AddSingleton<ITimeProvider>(f.Time);});
        });
    }
    private static async Task<HttpClient> FilterLogin(WebApplicationFactory<Program> web,string password,string email="manager@test.local")
    {
        var client=web.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        var login=await client.GetStringAsync("/Account/Login");
        var response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["TaiKhoanDangNhap"]=email,["MatKhau"]=password,["__RequestVerificationToken"]=OverdueToken(login,"__RequestVerificationToken")}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);return client;
    }
    private static string FilterHref(string page,string label)
    {
        var link = Regex.Matches(WebUtility.HtmlDecode(page), "<a\\b[^>]*href=\"([^\"]*)\"[^>]*>([\\s\\S]*?)</a>")
            .Cast<Match>().FirstOrDefault(x => Regex.Replace(x.Groups[2].Value,"<[^>]*>","").Contains(label,StringComparison.Ordinal));
        return link?.Groups[1].Value ?? "";
    }

    [Fact]
    public async Task FilterHttpOptionsIntersectionEmptyResetAndRoundTripKeepScopeAndWarning()
    {
        using var f=new Fixture();var ids=await FilterReports(f);
        var password=Guid.NewGuid().ToString("N")+"Aa1!";
        f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password,4)}'");
        using var web=FilterWeb(f);using var client=await FilterLogin(web,password);
        const string query="?toaNhaId=1&trangThai=MOI&mucDo=GAP";
        var audits=f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong");
        var page=await client.GetStringAsync("/QuanLyBaoHong"+query);
        var decoded=WebUtility.HtmlDecode(page);
        Assert.Contains("Tòa B",decoded);Assert.Contains("Tòa trống",decoded);Assert.DoesNotContain("Other building",decoded);
        Assert.Contains("Đã xong",decoded);Assert.Contains("Gấp — Quá 24 giờ",decoded);
        Assert.Single(Regex.Matches(page,"<tr data-repair-id=\""));
        var detailUrl=FilterHref(page,$"BH-202610-{ids["a-new-urgent"]:0000}");Assert.NotEmpty(detailUrl);
        Assert.Contains("toaNhaId=1",detailUrl);Assert.Contains("trangThai=MOI",detailUrl);Assert.Contains("mucDo=GAP",detailUrl);
        var detail=await client.GetStringAsync(detailUrl);
        var back=FilterHref(detail,"Danh sách báo hỏng");Assert.NotEmpty(back);
        Assert.Contains("toaNhaId=1",back);Assert.Contains("mucDo=GAP",back);Assert.Contains("trangThai=MOI",back);
        Assert.Single(Regex.Matches(await client.GetStringAsync(back),"<tr data-repair-id=\""));
        var empty=WebUtility.HtmlDecode(await client.GetStringAsync("/QuanLyBaoHong?toaNhaId=4&trangThai=MOI&mucDo=GAP"));
        Assert.Contains("Không có báo hỏng phù hợp với bộ lọc đã chọn.",empty);
        var reset=FilterHref(empty,"Xóa bộ lọc");Assert.Equal("/QuanLyBaoHong",reset);
        Assert.Equal(6,Regex.Matches(await client.GetStringAsync(reset),"<tr data-repair-id=\"").Count);
        foreach(var invalid in new[]{"?trangThai=BAD","?mucDo=BAD","?toaNhaId=abc","?page=abc","?toaNhaId=-1"})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/QuanLyBaoHong"+invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/QuanLyBaoHong?toaNhaId=2")).StatusCode);
        using var outsider=await FilterLogin(web,password,"manager2@test.local");
        Assert.Equal(HttpStatusCode.Forbidden,(await outsider.GetAsync("/QuanLyBaoHong"+query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await outsider.GetAsync(detailUrl)).StatusCode);
        using var tenant=await FilterLogin(web,password,"tenant@test.local");
        Assert.Equal(HttpStatusCode.Forbidden,(await tenant.GetAsync("/QuanLyBaoHong"+query)).StatusCode);
        Assert.Equal(audits,f.Scalar("SELECT count(*) FROM nhat_ky_hoat_dong"));
        // Invalid and successful POST round trips preserve the same server-validated query context.
        var postUrl=WebUtility.HtmlDecode(Regex.Match(detail,"<form[^>]*action=\"([^\"]*PhanHoi[^\"]*)\"").Groups[1].Value);
        Assert.NotEmpty(postUrl);Assert.Contains("mucDo=GAP",postUrl);
        var fields=new Dictionary<string,string>{["__RequestVerificationToken"]=OverdueToken(detail,"__RequestVerificationToken"),
            ["PhanHoi.KhoaChongTrung"]=OverdueToken(detail,"PhanHoi.KhoaChongTrung"),["PhanHoi.NoiDung"]=""};
        var invalidPost=await client.PostAsync(postUrl,new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK,invalidPost.StatusCode);
        Assert.Contains("trangThai=MOI",FilterHref(await invalidPost.Content.ReadAsStringAsync(),"Danh sách báo hỏng"));
        fields["PhanHoi.NoiDung"]="Phản hồi kiểm thử bộ lọc";
        var saved=await client.PostAsync(postUrl,new FormUrlEncodedContent(fields));Assert.Equal(HttpStatusCode.Redirect,saved.StatusCode);
        Assert.Contains("toaNhaId=1",saved.Headers.Location!.ToString());Assert.Contains("mucDo=GAP",saved.Headers.Location.ToString());
        var after=await client.GetStringAsync(saved.Headers.Location);Assert.Contains("trangThai=MOI",FilterHref(after,"Danh sách báo hỏng"));
        var stateUrl=WebUtility.HtmlDecode(Regex.Match(after,"<form[^>]*action=\"([^\"]*CapNhat[^\"]*)\"").Groups[1].Value);
        Assert.Contains($"/CapNhat/{ids["a-new-urgent"]}",stateUrl);
        var stateFields=new Dictionary<string,string>{["__RequestVerificationToken"]=OverdueToken(after,"__RequestVerificationToken"),
            ["CapNhat.PhienBan"]=OverdueToken(after,"CapNhat.PhienBan"),["CapNhat.KhoaChongTrung"]=OverdueToken(after,"CapNhat.KhoaChongTrung"),
            ["CapNhat.TrangThai"]="DANG_XU_LY",["CapNhat.GhiChu"]="Nhận xử lý từ danh sách đã lọc"};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(stateUrl.Split('?')[0]+"?toaNhaId=abc",new FormUrlEncodedContent(stateFields))).StatusCode);
        var changed=await client.PostAsync(stateUrl,new FormUrlEncodedContent(stateFields));Assert.Equal(HttpStatusCode.Redirect,changed.StatusCode);
        Assert.Contains("trangThai=MOI",changed.Headers.Location!.ToString());
        var changedDetail=await client.GetStringAsync(changed.Headers.Location);
        var changedList=WebUtility.HtmlDecode(await client.GetStringAsync(FilterHref(changedDetail,"Danh sách báo hỏng")));
        Assert.Contains("Không có báo hỏng phù hợp",changedList);
        // Completed report cost form must retain both its source ID and the list context.
        var costDetail=await client.GetStringAsync($"/QuanLyBaoHong/Details/{ids["a-done"]}?toaNhaId=1&trangThai=HOAN_THANH&mucDo=THUONG");
        var costUrl=WebUtility.HtmlDecode(Regex.Match(costDetail,"<form[^>]*action=\"([^\"]*ChiPhi[^\"]*)\"").Groups[1].Value);
        Assert.Contains($"/ChiPhi/{ids["a-done"]}",costUrl);
        var costFields=new Dictionary<string,string>{["__RequestVerificationToken"]=OverdueToken(costDetail,"__RequestVerificationToken"),
            ["ChiPhiInput.PhienBan"]=OverdueToken(costDetail,"ChiPhiInput.PhienBan"),["ChiPhiInput.KhoaChongTrung"]=OverdueToken(costDetail,"ChiPhiInput.KhoaChongTrung"),
            ["ChiPhiInput.SoTien"]="250000",["ChiPhiInput.BenChiuPhi"]="CHU_NHA"};
        var costSaved=await client.PostAsync(costUrl,new FormUrlEncodedContent(costFields));Assert.Equal(HttpStatusCode.Redirect,costSaved.StatusCode);
        Assert.Contains("trangThai=HOAN_THANH",costSaved.Headers.Location!.ToString());Assert.Contains("mucDo=THUONG",costSaved.Headers.Location.ToString());
    }

    [Fact]
    public async Task FilterHttpPaginationKeepsAllCriteriaAndPageOnReturn()
    {
        using var f=new Fixture();FilterBuildings(f);
        using(var db=f.Context()) {
            var sender=new BaoHongService(db,f.Time);
            for(var i=0;i<22;i++) await sender.GuiAsync(2,Input(level:"GAP"));
            for(var i=0;i<21;i++) await sender.GuiAsync(2,Input(2));
        }
        var password=Guid.NewGuid().ToString("N")+"Aa1!";f.Execute($"UPDATE tai_khoan SET mat_khau='{BCrypt.Net.BCrypt.HashPassword(password,4)}'");
        using var web=FilterWeb(f);using var client=await FilterLogin(web,password);
        var page1=await client.GetStringAsync("/QuanLyBaoHong?toaNhaId=1&trangThai=MOI&mucDo=GAP");
        Assert.Equal(20,Regex.Matches(page1,"<tr data-repair-id=\"").Count);
        var next=FilterHref(page1,"Trang sau →");Assert.Contains("page=2",next);Assert.Contains("toaNhaId=1",next);Assert.Contains("trangThai=MOI",next);Assert.Contains("mucDo=GAP",next);
        var page2=await client.GetStringAsync(next);Assert.Equal(2,Regex.Matches(page2,"<tr data-repair-id=\"").Count);
        var detail=FilterHref(page2,"BH-202610-0002");Assert.Contains("page=2",detail);
        var back=FilterHref(await client.GetStringAsync(detail),"Danh sách báo hỏng");
        Assert.Contains("page=2",back);Assert.Contains("toaNhaId=1",back);Assert.Equal(2,Regex.Matches(await client.GetStringAsync(back),"<tr data-repair-id=\"").Count);
    }
}
