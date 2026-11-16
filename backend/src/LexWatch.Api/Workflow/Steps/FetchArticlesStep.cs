using LexWatch.Api.Data.Entities;
using LexWatch.Api.LawApi;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Workflow.Steps;

/// <summary>본문·신구조문을 받아 조문 단위 Evidence로 저장한다. SaveChanges는 워커가 한다.</summary>
public class FetchArticlesStep(LawApiClient api, TimeProvider time) : IStepHandler
{
    public string Step => "fetch_articles";

    public async Task<StepResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var change = await db.LawChanges.AsNoTracking().SingleAsync(l => l.Id == ctx.Run.LawChangeId, ct);

        // 둘 다 받은 뒤에만 Evidence를 추가한다 (Retry 시 반쯤 쓴 상태를 남기지 않기 위해).
        var lawResult = await api.GetLawAsync(change.LawId, ct);
        if (lawResult is not LawApiResult<LawBody>.Ok law)
        {
            var e = (LawApiResult<LawBody>.Error)lawResult;
            return new StepResult.Retry(e.Code, e.Message, null);
        }
        if (law.Value.Articles.Count == 0)
            return new StepResult.Failed("NO_ARTICLES", "법령 본문에서 조문을 찾지 못했다");

        // 신구조문이 없거나(신구법존재여부 N) 못 받으면 본문 조문만으로 계속한다.
        var oldNew = await api.GetOldNewAsync(change.LawId, ct) is LawApiResult<OldNewBody>.Ok { Value.Exists: true } on
            ? on.Value : null;

        var now = time.GetUtcNow();
        var existing = (await db.Evidence.Where(x => x.RunId == ctx.Run.Id).Select(x => x.EvidenceId).ToListAsync(ct)).ToHashSet();
        var added = 0;
        void Add(string id, string kind, string title, string body)
        {
            if (!existing.Add(id)) return;
            db.Evidence.Add(new Evidence
            {
                RunId = ctx.Run.Id, EvidenceId = id, Kind = kind, Title = title, Body = body, FetchedAt = now,
            });
            added++;
        }

        var name = string.IsNullOrEmpty(law.Value.LawName) ? change.LawName : law.Value.LawName;
        foreach (var a in law.Value.Articles)
            Add($"law:{change.LawId}:{a.Number}", "law", Title(name, a.Number, a.Title), a.Text);
        var titles = law.Value.Articles.ToDictionary(a => a.Number, a => a.Title);
        if (oldNew is not null)
            foreach (var a in oldNew.Articles)
                Add($"oldnew:{change.LawId}:{a.Number}", "oldnew",
                    Title(name, a.Number, titles.GetValueOrDefault(a.Number, "")) + " 신구조문 대비",
                    $"[구조문]\n{a.OldText ?? "(없음)"}\n\n[신조문]\n{a.NewText ?? "(없음)"}");

        return new StepResult.Succeeded(
            new { evidence = added, oldNew = oldNew is not null },
            [new NextStep("classify", null, null)]);
    }

    private static string Title(string law, string number, string title) =>
        title == "" ? $"{law} {number}" : $"{law} {number}({title})";
}
