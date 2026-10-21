using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    private static async Task<Dictionary<string,string>> PersonalForm(HttpClient client)
    {
        var html=await client.GetStringAsync("/PersonalAccount/Edit");
        string Value(string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+name+"\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return new(){["__RequestVerificationToken"]=Value("__RequestVerificationToken"),["OriginalUpdated"]=Value("OriginalUpdated")};
    }
    [Theory]
    [InlineData("CHU_NHA")][InlineData("QUAN_LY")][InlineData("ADMIN")][InlineData("KHACH_THUE")]
    public async Task PersonalEditAllRolesOwnFieldsOnlyAndStaleFormRejected(string role)
    {
        if(role=="KHACH_THUE")Execute("INSERT INTO khach_thue(tai_khoan_id,ho_ten,so_dien_thoai,ngay_tao) VALUES($id,'Old profile','0901111111','2026-01-01')",("$id",accounts[role]));
        using var client=await Login(role);var form=await PersonalForm(client);
        form["HoTen"]="Updated personal name";form["SoDienThoai"]="0918888888";
        form["id"]=accounts[role=="ADMIN"?"CHU_NHA":"ADMIN"].ToString();
        form["VaiTro"]="ADMIN";form["Email"]="attacker@test.invalid";form["MatKhau"]="bad";
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/PersonalAccount/Edit",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("Updated personal name",Scalar("SELECT ho_ten FROM tai_khoan WHERE id=$id",("$id",accounts[role])));
        Assert.Equal(role,Scalar("SELECT vai_tro FROM tai_khoan WHERE id=$id",("$id",accounts[role])));
        Assert.Equal(role.ToLowerInvariant()+"@s104.test",Scalar("SELECT email FROM tai_khoan WHERE id=$id",("$id",accounts[role])));
        if(role=="KHACH_THUE")Assert.Equal("Updated personal name",Scalar("SELECT ho_ten FROM khach_thue WHERE tai_khoan_id=$id",("$id",accounts[role])));
        Assert.Contains("Updated personal name",await client.GetStringAsync("/PersonalAccount/Edit"));
        form["HoTen"]="Stale overwrite";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/PersonalAccount/Edit",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("Updated personal name",Scalar("SELECT ho_ten FROM tai_khoan WHERE id=$id",("$id",accounts[role])));
    }
    [Fact]
    public async Task PersonalEditPhoneValidationAndCsrf()
    {
        using var client=await Login("CHU_NHA");var form=await PersonalForm(client);
        form["HoTen"]="Valid";form["SoDienThoai"]=(string)Scalar("SELECT so_dien_thoai FROM tai_khoan WHERE id=$id",("$id",accounts["ADMIN"]))!;
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/PersonalAccount/Edit",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("CHU_NHA",Scalar("SELECT ho_ten FROM tai_khoan WHERE id=$id",("$id",accounts["CHU_NHA"])));
        form["SoDienThoai"]="invalid";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/PersonalAccount/Edit",new FormUrlEncodedContent(form))).StatusCode);
        form.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/PersonalAccount/Edit",new FormUrlEncodedContent(form))).StatusCode);
        using var anonymous=Client();Assert.Equal(HttpStatusCode.Redirect,(await anonymous.GetAsync("/PersonalAccount/Edit")).StatusCode);
    }
    [Fact]
    public async Task PersonalAvatarValidatesImageAndProtectsOwnershipAndDelete()
    {
        using var client=await Login("CHU_NHA");var form=await PersonalForm(client);
        using var pixels=new Image<Rgba32>(12,20);using var stream=new MemoryStream();await pixels.SaveAsPngAsync(stream);
        async Task<HttpResponseMessage> Upload(byte[] bytes,string name){
            using var body=new MultipartFormDataContent();body.Add(new StringContent(form["__RequestVerificationToken"]),"__RequestVerificationToken");
            body.Add(new ByteArrayContent(bytes),"image",name);return await client.PostAsync("/PersonalAccount/Avatar",body);
        }
        Assert.Equal(HttpStatusCode.Redirect,(await Upload(stream.ToArray(),"photo.png")).StatusCode);
        var image=await client.GetAsync("/PersonalAccount/Avatar");Assert.Equal("image/png",image.Content.Headers.ContentType!.MediaType);Assert.True(image.Headers.CacheControl!.NoStore);
        using(var decoded=Image.Load(await image.Content.ReadAsByteArrayAsync())){Assert.Equal(512,decoded.Width);Assert.Equal(512,decoded.Height);}
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/PersonalAccount/Avatar?id="+accounts["ADMIN"])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Upload(Encoding.UTF8.GetBytes("<script>alert(1)</script>"),"bad.png")).StatusCode);
        Assert.Equal("image/png",(await client.GetAsync("/PersonalAccount/Avatar")).Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/PersonalAccount/RemoveAvatar",new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal("image/svg+xml",(await client.GetAsync("/PersonalAccount/Avatar")).Content.Headers.ContentType!.MediaType);
    }
}
