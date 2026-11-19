using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LexWatch.Api.LawApi;

public record LawApiOptions(string Oc, string BaseUrl = "https://www.law.go.kr/DRF/");

public abstract record LawApiResult<T>
{
    public sealed record Ok(T Value, string RawBody) : LawApiResult<T>;

    public sealed record Error(string Code, string Message) : LawApiResult<T>;
}

public record LawListItem(
    string LawId, string LawName, DateOnly PromulgationDate, DateOnly? EffectiveDate, string? RawJson = null, string Mst = "");

/// <summary>Number는 "제23조의2" 형식.</summary>
public record LawArticle(string Number, string Title, string Text);

public record LawBody(string LawId, string LawName, IReadOnlyList<LawArticle> Articles);

public record OldNewArticle(string Number, string? OldText, string? NewText);

public record OldNewBody(bool Exists, IReadOnlyList<OldNewArticle> Articles);

/// <summary>
/// 국가법령정보센터 DRF 클라이언트. JSON 형태에 대한 지식은 전부 이 파일에만 둔다
/// (2026-11-19 실응답 녹화로 확인, fixtures/lawapi/recorded). OC 값은 로그·오류 메시지에 싣지 않는다.
/// </summary>
public class LawApiClient
{
    private const int PageSize = 100;
    private const int MaxPages = 20;
    private static readonly Regex ArticleNo = new(@"^\s*(제\d+조(?:의\d+)?)", RegexOptions.Compiled);
    private static readonly Regex ChangeMark = new(@"</?P>", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly LawApiOptions _opt;

    public LawApiClient(HttpClient http, LawApiOptions options)
    {
        _http = http;
        _opt = options;
        _http.BaseAddress ??= new Uri(options.BaseUrl);
    }

    public async Task<LawApiResult<IReadOnlyList<LawListItem>>> ListPromulgatedAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var items = new List<LawListItem>();
        var raw = new List<string>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"lawSearch.do?OC={Uri.EscapeDataString(_opt.Oc)}&target=law&type=JSON" +
                      $"&ancYd={from:yyyyMMdd}~{to:yyyyMMdd}&display={PageSize}&page={page}&sort=ddes";
            var r = await GetJsonAsync(url, ct);
            if (r.Error is not null) return new LawApiResult<IReadOnlyList<LawListItem>>.Error(r.Error.Code, r.Error.Message);
            var (doc, body) = (r.Doc!, r.Body!);
            raw.Add(body);
            if (!TryProp(doc.RootElement, "LawSearch", out var root))
                return new LawApiResult<IReadOnlyList<LawListItem>>.Error(
                    "LAW_API_ERROR_BODY", "예상한 최상위 키(LawSearch)가 없다");
            var total = int.TryParse(Str(root, "totalCnt"), out var t) ? t : 0;
            var got = 0;
            foreach (var e in Elements(root, "law"))
            {
                got++;
                var id = Str(e, "법령ID");
                var pd = ParseDate(Str(e, "공포일자"));
                if (id == "" || pd is null) continue;
                items.Add(new LawListItem(id, Str(e, "법령명한글"), pd.Value, ParseDate(Str(e, "시행일자")), e.GetRawText(), Str(e, "법령일련번호")));
            }
            if (got == 0 || items.Count >= total || got < PageSize) break;
        }
        return new LawApiResult<IReadOnlyList<LawListItem>>.Ok(items, string.Join("\n", raw));
    }

    public async Task<LawApiResult<LawBody>> GetLawAsync(string lawId, string mst = "", CancellationToken ct = default)
    {
        var r = await GetJsonAsync(
            $"lawService.do?OC={Uri.EscapeDataString(_opt.Oc)}&target=law&{Key(lawId, mst)}&type=JSON", ct);
        if (r.Error is not null) return new LawApiResult<LawBody>.Error(r.Error.Code, r.Error.Message);
        if (!TryProp(r.Doc!.RootElement, "법령", out var root))
            return new LawApiResult<LawBody>.Error("LAW_API_ERROR_BODY", "예상한 최상위 키(법령)가 없다");
        var name = TryProp(root, "기본정보", out var info) ? Str(info, "법령명_한글") : "";
        var articles = new List<LawArticle>();
        if (TryProp(root, "조문", out var jo))
            foreach (var e in Elements(jo, "조문단위"))
            {
                if (Str(e, "조문여부") != "조문") continue;
                var no = Str(e, "조문번호");
                if (no == "") continue;
                var branch = Str(e, "조문가지번호");
                var number = $"제{no}조" + (branch is not "" and not "0" ? $"의{branch}" : "");
                articles.Add(new LawArticle(number, Str(e, "조문제목"), FlattenArticle(e)));
            }
        return new LawApiResult<LawBody>.Ok(new LawBody(lawId, name, articles), r.Body!);
    }

    public async Task<LawApiResult<OldNewBody>> GetOldNewAsync(string lawId, string mst = "", CancellationToken ct = default)
    {
        var r = await GetJsonAsync(
            $"lawService.do?OC={Uri.EscapeDataString(_opt.Oc)}&target=oldAndNew&{Key(lawId, mst)}&type=JSON", ct);
        if (r.Error is not null) return new LawApiResult<OldNewBody>.Error(r.Error.Code, r.Error.Message);
        if (!TryProp(r.Doc!.RootElement, "OldAndNewService", out var root))
            return new LawApiResult<OldNewBody>.Error("LAW_API_ERROR_BODY", "예상한 최상위 키(OldAndNewService)가 없다");
        if (Str(root, "신구법존재여부") == "N")
            return new LawApiResult<OldNewBody>.Ok(new OldNewBody(false, []), r.Body!);

        var olds = ArticleTexts(root, "구조문목록");
        var news = ArticleTexts(root, "신조문목록");
        var numbers = olds.Keys.Concat(news.Keys).Distinct().ToList();
        var list = numbers
            .Select(n => new OldNewArticle(n, olds.GetValueOrDefault(n), news.GetValueOrDefault(n)))
            .ToList();
        return new LawApiResult<OldNewBody>.Ok(new OldNewBody(true, list), r.Body!);
    }

    /// <summary>문서 기준 MST(법령일련번호)를 우선하고, 없으면 법령ID로 대체한다.</summary>
    private static string Key(string lawId, string mst) =>
        mst != "" ? $"MST={Uri.EscapeDataString(mst)}" : $"ID={Uri.EscapeDataString(lawId)}";

    /// <summary>
    /// 신구조문 목록 → {조문번호: 본문}. 실응답은 항목이 {"content","no"} 객체이고 한 조문이 항마다 여러 항목으로 온다.
    /// "제N조"로 시작하는 항목이 새 조문을 열고 나머지는 앞 조문에 붙는다. 변경 표시 &lt;P&gt;…&lt;/P&gt;는 걷어낸다.
    /// 문자열 항목(합성 fixture)도 받는다.
    /// </summary>
    private static Dictionary<string, string> ArticleTexts(JsonElement root, string listKey)
    {
        var parts = new Dictionary<string, List<string>>();
        if (!TryProp(root, listKey, out var list)) return [];
        string? current = null;
        foreach (var e in Elements(list, "조문"))
        {
            var raw = e.ValueKind == JsonValueKind.String ? e.GetString() ?? ""
                : Str(e, "content") is { Length: > 0 } c ? c : Str(e, "조문내용");
            var text = ChangeMark.Replace(raw, "").Trim();
            if (text == "") continue;
            var m = ArticleNo.Match(text);
            if (m.Success)
            {
                current = m.Groups[1].Value;
                if (!parts.ContainsKey(current)) parts[current] = [];
            }
            if (current is not null) parts[current].Add(text);
        }
        return parts.ToDictionary(p => p.Key, p => string.Join("\n", p.Value));
    }

    /// <summary>조문내용 뒤에 항 → 호 → 목 본문을 순서대로 붙인다. 실응답의 조문내용은 제목 줄뿐인 경우가 많다.</summary>
    private static string FlattenArticle(JsonElement unit)
    {
        var lines = new List<string>();
        void Add(string s) { s = s.Trim(); if (s != "") lines.Add(s); }
        Add(Str(unit, "조문내용"));
        foreach (var hang in Elements(unit, "항"))
        {
            Add(Str(hang, "항내용"));
            foreach (var ho in Elements(hang, "호"))
            {
                Add(Str(ho, "호내용"));
                foreach (var mok in Elements(ho, "목")) Add(Str(mok, "목내용"));
            }
        }
        return string.Join("\n", lines);
    }

    private sealed record Fetched(JsonDocument? Doc, string? Body, LawApiResult<object>.Error? Error);

    private async Task<Fetched> GetJsonAsync(string url, CancellationToken ct)
    {
        string body;
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return Fail("LAW_API_HTTP_" + (int)resp.StatusCode, $"법령 API가 HTTP {(int)resp.StatusCode}를 돌려줬다");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // 예외 메시지에 URL(OC 포함)이 실릴 수 있어 형식만 남긴다.
            return Fail("LAW_API_NETWORK", $"법령 API 호출 실패({e.GetType().Name})");
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { return Fail("LAW_API_ERROR_BODY", "JSON이 아닌 응답(오류 페이지로 추정)"); }
        // HTTP 200이어도 오류 본문이면 최상위에 result 키만 온다 (spike 노트).
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("result", out _))
            return Fail("LAW_API_ERROR_BODY", "법령 API가 오류 본문을 돌려줬다(사용자 검증 실패 등)");
        return new Fetched(doc, body, null);

        static Fetched Fail(string code, string msg) => new(null, null, new LawApiResult<object>.Error(code, msg));
    }

    private static bool TryProp(JsonElement e, string name, out JsonElement v)
    {
        v = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out v);
    }

    private static string Str(JsonElement e, string name) =>
        TryProp(e, name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.Object when TryProp(v, "content", out var c) && c.ValueKind == JsonValueKind.String => c.GetString() ?? "",
                _ => "",
            }
            : "";

    /// <summary>배열이든 단일 객체든 요소 열거. 없으면 빈 열거.</summary>
    private static IEnumerable<JsonElement> Elements(JsonElement parent, string name)
    {
        if (!TryProp(parent, name, out var v)) yield break;
        if (v.ValueKind == JsonValueKind.Array)
            foreach (var x in v.EnumerateArray()) yield return x;
        else
            yield return v;
    }

    private static DateOnly? ParseDate(string s) =>
        DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
