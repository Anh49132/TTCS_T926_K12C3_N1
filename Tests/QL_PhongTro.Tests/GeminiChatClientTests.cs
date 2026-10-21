using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class GeminiChatClientTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        public string? Key { get; private set; }
        public string? Payload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri=request.RequestUri!.ToString();Key=request.Headers.GetValues("x-goog-api-key").Single();
            Payload=await request.Content!.ReadAsStringAsync(ct);
            return new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
    [Fact]
    public async Task GeminiUsesHeaderForSecretAndPreservesModelContent()
    {
        using var handler=new Handler(HttpStatusCode.OK,"""{"candidates":[{"content":{"role":"model","parts":[{"text":"Xin chào"}]}}]}""");
        using var http=new HttpClient(handler);
        var client=new GeminiChatClient(http,Options.Create(new GeminiOptions{ApiKey="fake-private-key"}));
        var result=await client.GenerateAsync(new{contents=new[]{new{role="user",parts=new[]{new{text="Hello"}}}},generationConfig=new{maxOutputTokens=100}},default);
        Assert.Equal("fake-private-key",handler.Key);Assert.DoesNotContain("fake-private-key",handler.Uri!);
        Assert.DoesNotContain("fake-private-key",handler.Payload!);Assert.Contains("minimal",handler.Payload!);
        Assert.Equal("Xin chào",result.GetProperty("parts")[0].GetProperty("text").GetString());
    }
    [Theory]
    [InlineData(429,429)] [InlineData(403,503)] [InlineData(500,503)]
    public async Task GeminiErrorsAreActionableWithoutExposingProviderBodyOrSecret(int status,int expected)
    {
        using var http=new HttpClient(new Handler((HttpStatusCode)status,"fake-private-key provider internal detail"));
        var client=new GeminiChatClient(http,Options.Create(new GeminiOptions{ApiKey="fake-private-key"}));
        var error=await Assert.ThrowsAsync<ChatUnavailableException>(()=>client.GenerateAsync(new{contents=new object[0]},default));
        Assert.Equal(expected,error.Status);Assert.DoesNotContain("fake-private-key",error.Message);Assert.DoesNotContain("provider internal",error.Message);
    }
    [Fact]
    public async Task GeminiWithoutKeyFailsBeforeSendingNetworkRequest()
    {
        using var http=new HttpClient(new Handler(HttpStatusCode.OK,"{}"));
        var client=new GeminiChatClient(http,Options.Create(new GeminiOptions()));
        await Assert.ThrowsAsync<ChatUnavailableException>(()=>client.GenerateAsync(new{},default));
    }
}
