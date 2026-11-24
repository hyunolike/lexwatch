using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LexWatch.Api.Radar;

/// <summary>키가 비어 있으면 키 없이 부른다(호출마다 최신 5건, 2026-11-24 확인).</summary>
public record AssemblyApiOptions(string ApiKey, string BaseUrl = "https://open.assembly.go.kr/portal/openapi/")
{
    public const int KeylessPageSize = 5;
    public const int KeyedPageSize = 100;
    public int PageSize => ApiKey == "" ? KeylessPageSize : KeyedPageSize;
}

/// <summary>국회 의원 발의법률안 한 행. 날짜는 없으면 null.</summary>
public record AssemblyBill(
    string BillId, string BillNo, string BillName, DateOnly ProposedOn, string Proposer, string? LeadProposer,
    string? Committee, string? ProcResult, string? CommitteeResult, DateOnly? ProcOn, DateOnly? CommitteeOn, DateOnly? CommitteeProcOn,
    DateOnly? JudiciarySubmitOn, DateOnly? JudiciaryProcOn, string DetailUrl, string RawJson);

public abstract record AssemblyResult<T>
{
    public sealed record Ok(T Value, IReadOnlyList<string> BadRows) : AssemblyResult<T>;
    public sealed record Error(string Code, string Message) : AssemblyResult<T>;
}

/// <summary>
/// 국회 열린국회정보 "국회의원 발의법률안"(nzmimeepazxkubdpn, 22대) 클라이언트. 응답 형태 지식은 이 파일에만 둔다
/// (2026-11-24 실응답 녹화, fixtures/assembly). 인증키는 로그와 오류 메시지에 싣지 않는다.
/// </summary>
public class AssemblyApiClient
{
    public const string Service = "nzmimeepazxkubdpn";
    public const string Age = "22";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;
    private readonly AssemblyApiOptions _opt;

    public AssemblyApiClient(HttpClient http, AssemblyApiOptions options)
    {
        _http = http;
        _opt = options;
        _http.BaseAddress ??= new Uri(options.BaseUrl);
        // User-Agent가 없으면 400 "Bad Request."를 받는다.
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("LexWatch/1.0");
    }

    public Task<AssemblyResult<IReadOnlyList<AssemblyBill>>> ListByLawNameAsync(string lawName, CancellationToken ct = default) =>
        GetAsync($"BILL_NAME={Uri.EscapeDataString(lawName)}", _opt.PageSize, ct);

    public Task<AssemblyResult<IReadOnlyList<AssemblyBill>>> GetByBillNoAsync(string billNo, CancellationToken ct = default) =>
        GetAsync($"BILL_NO={Uri.EscapeDataString(billNo)}", 5, ct);

    private async Task<AssemblyResult<IReadOnlyList<AssemblyBill>>> GetAsync(string filter, int size, CancellationToken ct)
    {
        var key = _opt.ApiKey == "" ? "" : $"KEY={Uri.EscapeDataString(_opt.ApiKey)}&";
        var url = $"{Service}?{key}Type=json&pIndex=1&pSize={size}&AGE={Age}&{filter}";
        string body;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        try
        {
            using var resp = await _http.GetAsync(url, cts.Token);
            body = await resp.Content.ReadAsStringAsync(cts.Token);
            if (!resp.IsSuccessStatusCode)
                return Fail("ASSEMBLY_API_HTTP_" + (int)resp.StatusCode, $"국회 API가 HTTP {(int)resp.StatusCode}를 돌려줬다");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // 예외 메시지에 주소(키 포함)가 실릴 수 있어 형식만 남긴다.
            return Fail("ASSEMBLY_API_NETWORK", $"국회 API 호출 실패({e.GetType().Name})");
        }
        return Parse(body);
    }

    public static AssemblyResult<IReadOnlyList<AssemblyBill>> Parse(string body)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { return Fail("ASSEMBLY_API_ERROR_BODY", "JSON이 아닌 응답"); }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Fail("ASSEMBLY_API_ERROR_BODY", "예상하지 못한 응답 형태");
            // 결과 없음과 오류는 최상위 RESULT만 온다.
            if (root.TryGetProperty("RESULT", out var result))
            {
                var code = Str(result, "CODE");
                return code == "INFO-200"
                    ? new AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok([], [])
                    : Fail(ErrorCode(code), "국회 API가 오류 결과를 돌려줬다");
            }
            if (!root.TryGetProperty(Service, out var parts) || parts.ValueKind != JsonValueKind.Array)
                return Fail("ASSEMBLY_API_ERROR_BODY", $"예상한 최상위 키({Service})가 없다");

            var bills = new List<AssemblyBill>();
            var bad = new List<string>();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("head", out var head) && head.ValueKind == JsonValueKind.Array)
                    foreach (var h in head.EnumerateArray())
                        if (h.TryGetProperty("RESULT", out var r) && Str(r, "CODE") is { Length: > 0 } c && c != "INFO-000")
                            return Fail(ErrorCode(c), "국회 API가 오류 결과를 돌려줬다");
                if (!part.TryGetProperty("row", out var rows) || rows.ValueKind != JsonValueKind.Array) continue;
                foreach (var row in rows.EnumerateArray())
                {
                    if (TryRow(row) is { } b) bills.Add(b);
                    else bad.Add(Str(row, "BILL_NO") is { Length: > 0 } no ? no : "?");
                }
            }
            return new AssemblyResult<IReadOnlyList<AssemblyBill>>.Ok(bills, bad);
        }
    }

    private static AssemblyBill? TryRow(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object) return null;
        var id = Str(row, "BILL_ID");
        var no = Str(row, "BILL_NO");
        var name = Str(row, "BILL_NAME").Trim();
        var proposed = Date(row, "PROPOSE_DT");
        if (id == "" || no == "" || name == "" || proposed is null) return null;
        return new AssemblyBill(
            id, no, name, proposed.Value, Str(row, "PROPOSER"), Opt(row, "RST_PROPOSER"), Opt(row, "COMMITTEE"),
            Opt(row, "PROC_RESULT"), Opt(row, "CMT_PROC_RESULT_CD"), Date(row, "PROC_DT"), Date(row, "COMMITTEE_DT"), Date(row, "CMT_PROC_DT"), Date(row, "LAW_SUBMIT_DT"),
            Date(row, "LAW_PROC_DT"), Https(Str(row, "DETAIL_LINK")), row.GetRawText());
    }

    /// <summary>원 코드의 하이픈을 밑줄로 바꿔 붙인다(ERROR-290 → ASSEMBLY_API_ERROR_290). INFO-000, INFO-200 밖은 모두 실패다.</summary>
    public static string ErrorCode(string code) =>
        "ASSEMBLY_API_" + (code == "" ? "UNKNOWN" : Regex.Replace(code.ToUpperInvariant(), "[^A-Z0-9]", "_"));

    private static readonly Regex HttpScheme = new("^http://", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static string Https(string url) => HttpScheme.Replace(url, "https://");

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            _ => "",
        } : "";

    private static string? Opt(JsonElement e, string name) => Str(e, name).Trim() is { Length: > 0 } s ? s : null;

    private static DateOnly? Date(JsonElement e, string name) =>
        DateOnly.TryParseExact(Str(e, name).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static AssemblyResult<IReadOnlyList<AssemblyBill>> Fail(string code, string msg) =>
        new AssemblyResult<IReadOnlyList<AssemblyBill>>.Error(code, msg);
}
