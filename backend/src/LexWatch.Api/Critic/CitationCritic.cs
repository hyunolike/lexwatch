using LexWatch.Api.Ai;

namespace LexWatch.Api.Critic;

public record CriticFindingDto(int SentenceIndex, string RuleId, string Message);

/// <summary>크리틱 1단: 문장마다 인용이 있는지, 인용한 ID가 근거 저장소에 있는지 확인한다.</summary>
public static class CitationCritic
{
    public static IReadOnlyList<CriticFindingDto> Check(IReadOnlyList<MemoSentenceDto> memo, ISet<string> evidenceIds)
    {
        var findings = new List<CriticFindingDto>();
        for (var i = 0; i < memo.Count; i++)
        {
            var cites = memo[i].Citations ?? [];
            if (cites.Count == 0)
            {
                findings.Add(new CriticFindingDto(i, "C1-NOCITE", "문장에 근거 인용이 없다"));
                continue;
            }
            foreach (var c in cites.Where(c => !evidenceIds.Contains(c)))
                findings.Add(new CriticFindingDto(i, "C1-UNKNOWN", $"근거 저장소에 없는 인용 ID: {c}"));
        }
        return findings;
    }
}
