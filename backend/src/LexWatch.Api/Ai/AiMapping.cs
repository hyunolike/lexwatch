using System.Text.Json;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;

namespace LexWatch.Api.Ai;

/// <summary>MemoDraft.SentencesJson의 문장 한 줄.</summary>
public record MemoSentenceDto(string Sentence, IReadOnlyList<string> Citations);

public static class AiMapping
{
    public static readonly JsonSerializerOptions Json = StepQueue.Json;

    public static string ProviderKind(ProviderInfo p) => p.Kind switch
    {
        ProviderInfoKind.Nvidia => "nvidia",
        ProviderInfoKind.Cloudflare => "cloudflare",
        ProviderInfoKind.Fallback => "fallback",
        _ => "fallback",
    };

    public static LawSummary Summary(LawChange c, string summary) => new()
    {
        Law_id = c.LawId,
        Name = c.LawName,
        Promulgation_date = new DateTimeOffset(c.PromulgationDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        Effective_date = c.EffectiveDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
        Summary = summary,
        Mst = string.IsNullOrEmpty(c.Mst) ? null : c.Mst,
    };

    public static List<Api.Ai.Evidence> Evidence(IEnumerable<Data.Entities.Evidence> rows) =>
        EvidenceInOrder(rows.OrderBy(e => e.EvidenceId, StringComparer.Ordinal));

    /// <summary>주어진 순서를 그대로 둔다(변경 조문 → 검색 순위).</summary>
    public static List<Api.Ai.Evidence> EvidenceInOrder(IEnumerable<Data.Entities.Evidence> rows) =>
        rows.Select(e => new Api.Ai.Evidence { Id = e.EvidenceId, Kind = e.Kind, Title = e.Title, Body = e.Body }).ToList();
}
