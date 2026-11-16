using System.Net;
using System.Text;
using LexWatch.Api.LawApi;

namespace LexWatch.Tests;

public sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(fn(request));
    }
}

public static class LawFixtures
{
    public static string Dir
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "fixtures", "lawapi"))) d = d.Parent;
            return Path.Combine(d!.FullName, "fixtures", "lawapi");
        }
    }

    public static string Read(string name) => File.ReadAllText(Path.Combine(Dir, name));

    public static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static LawApiClient Client(Func<HttpRequestMessage, HttpResponseMessage> fn) =>
        new(new HttpClient(new StubHandler(fn)), new LawApiOptions("OCSECRET"));

    /// <summary>녹화 픽스처를 URL(target·ID)에 따라 돌려주는 대역.</summary>
    public static HttpResponseMessage Recorded(HttpRequestMessage r)
    {
        var q = r.RequestUri!.Query;
        if (r.RequestUri.AbsolutePath.EndsWith("lawSearch.do")) return Json(Read("list_recent.json"));
        // 법령일련번호(MST)로만 응답한다. ID= 호출은 실서버처럼 오류 본문.
        var mst = System.Text.RegularExpressions.Regex.Match(q, @"MST=(\d+)").Groups[1].Value;
        var id = mst switch { "100001" => "001872", "100002" => "011357", "100003" => "001791", _ => "" };
        if (id == "") return Json(Read("error_body.json"));
        return q.Contains("target=oldAndNew") ? Json(Read($"oldnew_{id}.json")) : Json(Read($"law_{id}.json"));
    }
}
