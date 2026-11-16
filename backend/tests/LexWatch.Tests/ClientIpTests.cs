using System.Net;
using LexWatch.Api.Reviews;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace LexWatch.Tests;

public class ClientIpTests
{
    private static string Resolve(Dictionary<string, string?> cfg, params (string Name, string Value)[] headers)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");
        foreach (var (n, v) in headers) http.Request.Headers[n] = v;
        var config = new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
        return ReviewEndpoints.ClientIp(http, config);
    }

    [Fact]
    public void 기본은_원격_IP다_헤더를_믿지_않는다()
    {
        Assert.Equal("10.0.0.9", Resolve([], ("X-Forwarded-For", "1.1.1.1"), ("True-Client-IP", "2.2.2.2")));
    }

    [Fact]
    public void TRUST_FORWARDED_FOR면_XFF의_마지막_항목()
    {
        Assert.Equal("3.3.3.3", Resolve(new() { ["TRUST_FORWARDED_FOR"] = "true" }, ("X-Forwarded-For", "1.1.1.1, 3.3.3.3")));
    }

    [Fact]
    public void CLIENT_IP_HEADER가_있으면_그_헤더_하나만_읽는다()
    {
        var cfg = new Dictionary<string, string?> { ["CLIENT_IP_HEADER"] = "True-Client-IP", ["TRUST_FORWARDED_FOR"] = "true" };
        Assert.Equal("203.0.113.7", Resolve(cfg, ("True-Client-IP", " 203.0.113.7 "), ("X-Forwarded-For", "1.1.1.1")));
        Assert.Equal("2001:db8::1", Resolve(cfg, ("True-Client-IP", "2001:db8::1")));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("1.1.1.1, 2.2.2.2")]
    [InlineData("")]
    public void CLIENT_IP_HEADER_값이_IP가_아니면_원격_IP로_내려간다(string value)
    {
        var cfg = new Dictionary<string, string?> { ["CLIENT_IP_HEADER"] = "CF-Connecting-IP", ["TRUST_FORWARDED_FOR"] = "true" };
        Assert.Equal("10.0.0.9", Resolve(cfg, ("CF-Connecting-IP", value), ("X-Forwarded-For", "1.1.1.1")));
        Assert.Equal("10.0.0.9", Resolve(cfg)); // 헤더가 아예 없을 때
    }
}
