using System.Text.RegularExpressions;

namespace LexWatch.Api.Radar;

public static class BillStages
{
    public const string Received = "Received";
    public const string Committee = "Committee";
    public const string Judiciary = "Judiciary";
    public const string Plenary = "Plenary";
    public const string Passed = "Passed";
    public const string Merged = "Merged";
    public const string Closed = "Closed";

    public const string JudiciaryCommittee = "법제사법위원회";

    /// <summary>의결(또는 대안 의결) 뒤에도 재의요구 같은 변화를 보려고 다시 조회하는 기간(BR-04).</summary>
    public const int WatchAfterDecisionDays = 30;

    private static readonly string[] PassedResults = ["원안가결", "수정가결"];
    private static readonly string[] MergedResults = ["대안반영폐기", "수정안반영폐기"];

    /// <summary>
    /// bill-radar BR-03. 위에서부터 처음 맞는 것. 위원장 대안은 이 API에 나오지 않고(2026-11-24 확인),
    /// 원래 의원안이 대안반영폐기로 끝나므로 Closed와 나눈다.
    /// </summary>
    public static string From(AssemblyBill b)
    {
        if (b.ProcResult is { } r)
            return PassedResults.Contains(r) ? Passed : MergedResults.Contains(r) ? Merged : Closed;
        if (b.CommitteeResult is { } c && MergedResults.Contains(c)) return Merged;
        if (b.JudiciaryProcOn is not null) return Plenary;
        // 법사위 소관 법안은 체계자구 심사(LAW_*)를 거치지 않는다(녹화 2221357).
        if (b.Committee == JudiciaryCommittee && b.CommitteeProcOn is not null) return Plenary;
        if (b.JudiciarySubmitOn is not null) return Judiciary;
        if (b.CommitteeOn is not null) return Committee;
        return Received;
    }

    /// <summary>재조회 대상인가. Closed는 끝났고, 의결과 대안 의결은 처리일 뒤 30일까지 본다.</summary>
    public static bool NeedsRefresh(string stage, DateOnly? procOn, DateOnly today) => stage switch
    {
        Closed => false,
        Passed or Merged => procOn is null || today <= procOn.Value.AddDays(WatchAfterDecisionDays),
        _ => true,
    };
}

public static class BillNames
{
    /// <summary>
    /// 법안명이 정확히 그 법률의 개정안인가(BR-02). 국회 API의 법안명 검색은 부분 일치라
    /// "은행법"이 "한국은행법 일부개정법률안"도 돌려준다. 법률명과 뒤 말 사이는 반각 공백 0~1칸.
    /// </summary>
    public static bool Matches(string billName, string lawName)
    {
        var law = lawName.Trim();
        if (law == "") return false;
        return Regex.IsMatch(billName.Trim(),
            "^" + Regex.Escape(law) + @" ?(일부개정|전부개정|폐지)법률안(\(대안\))?$");
    }
}
