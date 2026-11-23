using LexWatch.Api.Ai;
using LexWatch.Api.Data.Entities;
using EvidenceRow = LexWatch.Api.Data.Entities.Evidence;

namespace LexWatch.Api.Critic;

public record RecheckFinding(int SentenceIndex, int Tier, string RuleId, string Message);

/// <summary>
/// 승인 직전 재검사: 승인할 초안(after)에 결정적 크리틱 1·2단을 다시 돌리고, 수정 전 초안(before)의 3단 적발을 옮긴다.
/// 3단은 모델 호출이라 다시 돌리지 않는다. 워크플로의 CriticSteps와 같은 판정 함수를 쓴다.
/// </summary>
public static class DraftRecheck
{
    public const string EditedPrefix = "(수정 전 문장) ";

    public static IReadOnlyList<RecheckFinding> Run(
        IReadOnlyList<MemoSentenceDto> before, IReadOnlyList<MemoSentenceDto> after,
        IEnumerable<CriticFinding> storedTier3, IReadOnlyList<EvidenceRow> evidence, LawChange change)
    {
        var findings = new List<RecheckFinding>();
        findings.AddRange(CitationCritic.Check(after, evidence.Select(e => e.EvidenceId).ToHashSet())
            .Select(f => new RecheckFinding(f.SentenceIndex, 1, f.RuleId, f.Message)));
        findings.AddRange(SourceMatchCritic.Check(after, evidence.ToDictionary(e => e.EvidenceId, e => e.Body), change)
            .Select(f => new RecheckFinding(f.SentenceIndex, 2, f.RuleId, f.Message)));

        var moved = MapSentences(before, after);
        foreach (var f in storedTier3.Where(f => f.Tier == 3))
        {
            if (moved.TryGetValue(f.SentenceIndex, out var j))
                findings.Add(new RecheckFinding(j, 3, f.RuleId, f.Message));
            else if (f.SentenceIndex < after.Count)
                findings.Add(new RecheckFinding(f.SentenceIndex, 3, f.RuleId, EditedPrefix + f.Message));
            // 같은 위치에 문장이 없으면(삭제) 그 주장도 사라졌으므로 버린다.
        }

        return findings.OrderBy(f => f.SentenceIndex).ThenBy(f => f.Tier).ThenBy(f => f.RuleId, StringComparer.Ordinal).ToList();
    }

    /// <summary>before의 각 문장을 (문장, 인용)이 같은 after의 첫 문장에 짝짓는다. 한 번 짝지은 after 문장은 다시 쓰지 않는다.</summary>
    private static Dictionary<int, int> MapSentences(IReadOnlyList<MemoSentenceDto> before, IReadOnlyList<MemoSentenceDto> after)
    {
        var map = new Dictionary<int, int>();
        var used = new HashSet<int>();
        for (var i = 0; i < before.Count; i++)
            for (var j = 0; j < after.Count; j++)
                if (!used.Contains(j) && Same(before[i], after[j]))
                {
                    map[i] = j;
                    used.Add(j);
                    break;
                }
        return map;
    }

    private static bool Same(MemoSentenceDto a, MemoSentenceDto b) =>
        a.Sentence == b.Sentence && (a.Citations ?? []).SequenceEqual(b.Citations ?? []);
}
