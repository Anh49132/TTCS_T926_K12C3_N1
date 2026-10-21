using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;
public sealed partial class BaoHongTests
{
    [Fact]
    public async Task GuestChatFiltersPublicListingsAndDeniesPrivateTools()
    {
        using var f=new Fixture();
        f.Execute("""
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES(10,1,'PUBLIC',1,25,2000000,2,'TRONG','2026-01-01'),
            (11,1,'EXPIRED',1,25,2000000,2,'TRONG','2026-01-01'),
            (12,1,'DRAFT',1,25,2000000,2,'TRONG','2026-01-01');
            INSERT INTO tin_dang(id,phong_id,nguoi_dang_id,tieu_de,ngay_het_han,trang_thai,ngay_tao) VALUES
            (10,10,1,'Public room','2026-11-01','DANG_HIEN_THI','2026-10-01'),
            (11,11,1,'Expired room','2026-10-01','DANG_HIEN_THI','2026-09-01'),
            (12,12,1,'Draft room','2026-11-01','NHAP','2026-10-01');
            """);
        using var db=f.Context();var tools=new ChatDataTools(db,f.Time);
        Assert.Equal(new[]{"public_listings"},(await tools.AccessAsync(0,default)).Tools);
        foreach(var name in new[]{"invoices","contracts","repairs","rooms"})
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>tools.RunAsync(0,name,ChatArgs("{\"actorId\":1}"),default));
        async Task<int> Count(string args) {
            var result=await tools.RunAsync(0,"public_listings",ChatArgs(args),default);
            return JsonSerializer.SerializeToElement(result.Data).GetProperty("count").GetInt32();
        }
        Assert.Equal(1,await Count("{\"maxPrice\":3000000,\"people\":2}"));
        Assert.Equal(0,await Count("{\"maxPrice\":1500000}"));
        Assert.Equal(0,await Count("{\"people\":3}"));
        await Assert.ThrowsAsync<ArgumentException>(()=>Count("{\"minPrice\":3000000,\"maxPrice\":1000000}"));
        await Assert.ThrowsAsync<ArgumentException>(()=>Count("{\"maxPrice\":-1}"));
        f.Execute("UPDATE phong_tro SET trang_thai='DANG_THUE' WHERE id=10");
        Assert.Equal(0,await Count("{}"));
        f.Execute("UPDATE phong_tro SET trang_thai='TRONG' WHERE id=10; UPDATE toa_nha SET dang_hoat_dong=0 WHERE id=1");
        Assert.Equal(0,await Count("{}"));
    }

    [Fact]
    public async Task GuestChatConversationCannotCrossVisitorsOrAccounts()
    {
        using var f=new Fixture();using var db=f.Context();using var memory=new MemoryCache(new MemoryCacheOptions());
        var fake=new ChatFake("""{"role":"model","parts":[{"text":"Ban muon tim phong o dau?"}]}""");
        var chat=new AiChatService(fake,new(db,f.Time),memory,f.Time);
        var id=Guid.NewGuid().ToString("N");
        var reply=await chat.AskGuestAsync(id,"Tim phong",null,default);
        Assert.DoesNotContain("\"name\":\"invoices\"",fake.Requests[0]);
        Assert.Contains("public_listings",fake.Requests[0]);
        Assert.Equal(410,(await Assert.ThrowsAsync<ChatUnavailableException>(()=>chat.AskGuestAsync(Guid.NewGuid().ToString("N"),"Hi",reply.ConversationId,default))).Status);
        Assert.Equal(410,(await Assert.ThrowsAsync<ChatUnavailableException>(()=>chat.AskAsync(2,"Hi",reply.ConversationId,default))).Status);
    }

    [Fact]
    public async Task GuestChatHttpIssuesProtectedCookieAndAcceptsCsrf()
    {
        using var f=new Fixture();
        using var web=FilterWeb(f).WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{
            services.RemoveAll<IGeminiChatClient>();services.AddSingleton<IGeminiChatClient>(new ChatFake(
                """{"role":"model","parts":[{"text":"Xin chao"}]}""",
                """{"role":"model","parts":[{"text":"Tim phong"}]}"""));
        }));
        using var client=web.CreateClient(new(){AllowAutoRedirect=false});
        var page=await client.GetStringAsync("/");Assert.Contains("data-ai-chat",page);
        client.DefaultRequestHeaders.Add("RequestVerificationToken",OverdueToken(page,"__RequestVerificationToken"));
        var response=await client.PostAsJsonAsync("/api/chatbot",new{message="Hi"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),x=>x.Contains(".NhaTot.ChatGuest=")&&x.Contains("httponly",StringComparison.OrdinalIgnoreCase));
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId=json.GetProperty("conversationId").GetString();
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/chatbot",new{message="More",conversationId})).StatusCode);
        using var other=web.CreateClient(new(){AllowAutoRedirect=false});
        var otherPage=await other.GetStringAsync("/");
        other.DefaultRequestHeaders.Add("RequestVerificationToken",OverdueToken(otherPage,"__RequestVerificationToken"));
        Assert.Equal(HttpStatusCode.Gone,(await other.PostAsJsonAsync("/api/chatbot",new{message="More",conversationId})).StatusCode);
    }
}
