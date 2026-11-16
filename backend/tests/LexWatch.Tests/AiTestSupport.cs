using System.Text.Json;
using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace LexWatch.Tests;

/// <summary>Python AI 서비스 대역.</summary>
public sealed class AiStub : IDisposable
{
    public WireMockServer Server { get; } = WireMockServer.Start();

    public void Json(string path, int status, object body, int delayMs = 0, object? partialMatch = null)
    {
        var req = Request.Create().WithPath(path).UsingPost();
        if (partialMatch is not null) req = req.WithBody(new JsonPartialMatcher(partialMatch));
        var resp = Response.Create().WithStatusCode(status).WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(body));
        if (delayMs > 0) resp = resp.WithDelay(delayMs);
        Server.Given(req).RespondWith(resp);
    }

    public IAiClient Client(string? url = null) =>
        new AiClient(new HttpClient { BaseAddress = new Uri((url ?? Server.Url!) + "/") });

    public AiGateway Gateway(AiGatewayOptions? options = null) => new(Client(), options ?? AiGatewayOptions.Default);

    public IReadOnlyList<JsonDocument> Requests(string path) => Server.LogEntries
        .Where(e => e.RequestMessage?.Path == path)
        .Select(e => JsonDocument.Parse(e.RequestMessage!.Body!)).ToList();

    public void Dispose() => Server.Dispose();

    public static object Provider(string kind = "nvidia", string name = "nim") => new { name, kind };

    public static float[] Unit(int i, int dim = 2048)
    {
        var v = new float[dim];
        v[i] = 1;
        return v;
    }
}

public static class AiSeed
{
    /// <summary>실행 + 법령 변경 + 신구조문 근거 하나.</summary>
    public static async Task<(WorkflowRun Run, LawChange Change)> RunWithEvidenceAsync(PostgresFixture pg)
    {
        await using var db = pg.CreateDb();
        var lc = new LawChange
        {
            Id = Guid.NewGuid(), LawId = "L1", LawName = "근로기준법", Mst = "100003",
            PromulgationDate = new DateOnly(2026, 11, 1), EffectiveDate = new DateOnly(2027, 5, 1),
            RawJson = "{}", RawSha256 = "x", FetchedAt = DateTimeOffset.UtcNow, IdempotencyKey = Guid.NewGuid().ToString(),
        };
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(), LawChangeId = lc.Id, Status = RunStatus.Running, TraceId = "t", CreatedAt = DateTimeOffset.UtcNow,
        };
        db.AddRange(lc, run);
        db.Evidence.Add(new LexWatch.Api.Data.Entities.Evidence
        {
            RunId = run.Id, EvidenceId = "oldnew:L1:제23조", Kind = "oldnew", Title = "근로기준법 제23조 신구조문 대비",
            Body = "[구조문]\n...\n\n[신조문]\n" + new string('가', 3000), FetchedAt = DateTimeOffset.UtcNow,
        });
        db.Evidence.Add(new LexWatch.Api.Data.Entities.Evidence
        {
            RunId = run.Id, EvidenceId = "law:L1:제23조", Kind = "law", Title = "근로기준법 제23조",
            Body = "사용자는 근로자에게 정당한 이유 없이 해고를 하지 못한다.", FetchedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (run, lc);
    }

    public static async Task<StepRun> StepAsync(PostgresFixture pg, Guid runId, string step, Guid? groupId = null, int attempt = 1)
    {
        await using var db = pg.CreateDb();
        var s = new StepRun
        {
            Id = Guid.NewGuid(), RunId = runId, GroupReviewId = groupId, Step = step, Attempt = attempt,
            Status = StepStatus.Running, NotBefore = DateTimeOffset.UtcNow, ProviderKind = "none",
        };
        db.StepRuns.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    public static async Task<GroupReview> GroupAsync(PostgresFixture pg, Guid runId, string code = "labor",
        ReviewStatus status = ReviewStatus.Analyzing)
    {
        await using var db = pg.CreateDb();
        var g = new GroupReview { Id = Guid.NewGuid(), RunId = runId, GroupCode = code, Status = status, Confidence = 0.9 };
        db.GroupReviews.Add(g);
        await db.SaveChangesAsync();
        return g;
    }

    /// <summary>워커처럼: 핸들러 실행 뒤 SaveChanges.</summary>
    public static async Task<StepResult> ExecuteAsync(PostgresFixture pg, IStepHandler handler, Guid stepId)
    {
        await using var db = pg.CreateDb();
        var step = await db.StepRuns.SingleAsync(s => s.Id == stepId);
        var run = await db.WorkflowRuns.SingleAsync(r => r.Id == step.RunId);
        var result = await handler.ExecuteAsync(new StepContext(step, run, db), default);
        await db.SaveChangesAsync();
        return result;
    }
}
