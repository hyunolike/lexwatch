using LexWatch.Api.Data.Entities;
using LexWatch.Api.LawApi;
using LexWatch.Api.Workflow;
using LexWatch.Api.Workflow.Steps;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Tests;

[Collection("pg")]
public class CollectStepTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // 2026-11-16 12:00 UTC = 21:00 KST
    private static FakeTime Time() => new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));

    private CollectService Service(LexWatch.Api.Data.LexWatchDb db, Func<HttpRequestMessage, HttpResponseMessage> fn,
        FakeTime? time = null) =>
        new(db, LawFixtures.Client(fn), time ?? Time(), (_, _) => Task.CompletedTask);

    [Fact]
    public async Task Same_change_collected_twice_creates_one_run()
    {
        await using var db1 = pg.CreateDb();
        var r1 = await Service(db1, LawFixtures.Recorded).CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default);
        Assert.Equal(new CollectResult(3, 0), r1);
        await using var db2 = pg.CreateDb();
        var r2 = await Service(db2, LawFixtures.Recorded).CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default);
        Assert.Equal(new CollectResult(0, 3), r2);

        await using var v = pg.CreateDb();
        Assert.Equal(3, await v.LawChanges.CountAsync());
        Assert.Equal(3, await v.WorkflowRuns.CountAsync());
        var steps = await v.StepRuns.ToListAsync();
        Assert.Equal(3, steps.Count);
        Assert.All(steps, s => Assert.Equal("fetch_articles", s.Step));
        var lc = await v.LawChanges.OrderBy(x => x.LawId).FirstAsync();
        Assert.Equal("001791:20261104:20270505", lc.IdempotencyKey);
        Assert.Equal("100003", lc.Mst);
    }

    [Fact]
    public async Task Concurrent_collects_create_one_run_per_change()
    {
        var tasks = Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var db = pg.CreateDb();
            return await Service(db, LawFixtures.Recorded).CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default);
        });
        var results = await Task.WhenAll(tasks);
        Assert.Equal(3, results.Sum(r => r.Created));
        await using var v = pg.CreateDb();
        Assert.Equal(3, await v.WorkflowRuns.CountAsync());
        Assert.Equal(3, await v.StepRuns.CountAsync());
    }

    [Fact]
    public async Task Retries_api_errors_up_to_three_attempts_then_throws()
    {
        var calls = 0;
        await using var db = pg.CreateDb();
        var svc = Service(db, _ => { calls++; return LawFixtures.Json(LawFixtures.Read("error_body.json")); });
        await Assert.ThrowsAsync<LawApiException>(() => svc.CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default));
        Assert.Equal(3, calls);

        calls = 0;
        await using var db2 = pg.CreateDb();
        var svc2 = Service(db2, r => ++calls < 3 ? LawFixtures.Json(LawFixtures.Read("error_body.json")) : LawFixtures.Recorded(r));
        var res = await svc2.CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default);
        Assert.Equal(3, res.Created);
    }

    [Fact]
    public async Task Three_empty_days_in_a_row_raise_one_admin_alert()
    {
        var time = Time();
        foreach (var day in new[] { 14, 15, 16 })
        {
            time.Now = new DateTimeOffset(2026, 11, day, 12, 0, 0, TimeSpan.Zero);
            await using var db = pg.CreateDb();
            await Service(db, _ => LawFixtures.Json(LawFixtures.Read("empty_list.json")), time)
                .CollectAsync(new(2026, 11, day - 1), new(2026, 11, day), default);
            await using var v = pg.CreateDb();
            Assert.Equal(day == 16 ? 1 : 0, await v.Notifications.CountAsync(n => n.Channel == "admin"));
        }
        // 같은 날 재실행해도 중복 알림 없음
        await using var db3 = pg.CreateDb();
        await Service(db3, _ => LawFixtures.Json(LawFixtures.Read("empty_list.json")), time)
            .CollectAsync(new(2026, 11, 15), new(2026, 11, 16), default);
        await using var v3 = pg.CreateDb();
        Assert.Equal(1, await v3.Notifications.CountAsync(n => n.Channel == "admin"));
    }

    [Fact]
    public async Task A_non_empty_day_resets_the_streak()
    {
        var time = Time();
        var plan = new[] { (14, true), (15, false), (16, true) };
        foreach (var (day, empty) in plan)
        {
            time.Now = new DateTimeOffset(2026, 11, day, 12, 0, 0, TimeSpan.Zero);
            await using var db = pg.CreateDb();
            await Service(db, empty ? _ => LawFixtures.Json(LawFixtures.Read("empty_list.json")) : LawFixtures.Recorded, time)
                .CollectAsync(new(2026, 11, day - 1), new(2026, 11, day), default);
        }
        await using var v = pg.CreateDb();
        Assert.Equal(0, await v.Notifications.CountAsync(n => n.Channel == "admin"));
    }

    private async Task<(WorkflowRun run, StepRun step)> SeedRunAsync()
    {
        await using var db = pg.CreateDb();
        await Service(db, LawFixtures.Recorded).CollectAsync(new(2026, 11, 3), new(2026, 11, 4), default);
        await using var v = pg.CreateDb();
        var lc = await v.LawChanges.SingleAsync(x => x.LawId == "001872");
        var run = await v.WorkflowRuns.SingleAsync(r => r.LawChangeId == lc.Id);
        return (run, await v.StepRuns.SingleAsync(s => s.RunId == run.Id));
    }

    [Fact]
    public async Task Fetch_articles_stores_evidence_with_ids()
    {
        var (run, step) = await SeedRunAsync();
        await using var db = pg.CreateDb();
        var handler = new FetchArticlesStep(LawFixtures.Client(LawFixtures.Recorded), Time());
        var res = await handler.ExecuteAsync(new StepContext(step, run, db), default);
        await db.SaveChangesAsync(); // 워커의 역할
        var outputStatus = System.Text.Json.JsonSerializer.Serialize(((StepResult.Succeeded)res).Output, StepQueue.Json);
        Assert.Contains("\"oldnew\":\"ok\"", outputStatus);
        var ok = Assert.IsType<StepResult.Succeeded>(res);
        Assert.Equal("none", ok.ProviderKind);
        var next = Assert.Single(ok.Next);
        Assert.Equal(new NextStep("classify", null, null), next);

        await using var v = pg.CreateDb();
        var ids = (await v.Evidence.Where(e => e.RunId == run.Id).Select(e => e.EvidenceId).ToListAsync()).Order().ToList();
        Assert.Equal(
            ["law:001872:제50조", "law:001872:제56조", "oldnew:001872:제50조", "oldnew:001872:제56조"], ids);
        var oldnew = await v.Evidence.SingleAsync(e => e.RunId == run.Id && e.EvidenceId == "oldnew:001872:제50조");
        Assert.Contains("구조문 예시", oldnew.Body);
        Assert.Contains("40시간", oldnew.Body);
        Assert.Null(oldnew.SourceUrl);
    }

    [Fact]
    public async Task Fetch_articles_falls_back_to_law_only_when_oldnew_is_absent_or_unavailable()
    {
        foreach (var oldnew in new[] { """{"OldAndNewService":{"신구법존재여부":"N"}}""", LawFixtures.Read("error_body.json") })
        {
            await pg.ResetAsync();
            var (run, step) = await SeedRunAsync();
            await using var db = pg.CreateDb();
            var client = LawFixtures.Client(r => r.RequestUri!.Query.Contains("oldAndNew")
                ? LawFixtures.Json(oldnew) : LawFixtures.Recorded(r));
            var res = await new FetchArticlesStep(client, Time()).ExecuteAsync(new StepContext(step, run, db), default);
            await db.SaveChangesAsync();
            var okRes = Assert.IsType<StepResult.Succeeded>(res);
            var status = System.Text.Json.JsonSerializer.Serialize(okRes.Output, StepQueue.Json);
            Assert.Contains(oldnew.Contains("신구법존재여부") ? "\"oldnew\":\"absent\"" : "\"oldnew\":\"error\",\"code\":\"LAW_API_ERROR_BODY\"", status);
            await using var v = pg.CreateDb();
            Assert.Equal(["law:001872:제50조", "law:001872:제56조"],
                (await v.Evidence.Where(e => e.RunId == run.Id).Select(e => e.EvidenceId).ToListAsync()).Order());
        }
    }

    [Fact]
    public async Task Fetch_articles_retries_when_law_body_unavailable_and_stores_nothing()
    {
        var (run, step) = await SeedRunAsync();
        await using var db = pg.CreateDb();
        var client = LawFixtures.Client(_ => LawFixtures.Json(LawFixtures.Read("error_body.json")));
        var res = await new FetchArticlesStep(client, Time()).ExecuteAsync(new StepContext(step, run, db), default);
        var retry = Assert.IsType<StepResult.Retry>(res);
        Assert.Equal("LAW_API_ERROR_BODY", retry.ErrorCode);
        Assert.Empty(db.ChangeTracker.Entries<Evidence>());
    }

    [Fact]
    public async Task Fetch_articles_runs_through_worker()
    {
        var (run, _) = await SeedRunAsync();
        var worker = Seed.Worker(pg, Time(), new FetchArticlesStep(LawFixtures.Client(LawFixtures.Recorded), Time()));
        Assert.True(await worker.RunOnceAsync());
        await using var v = pg.CreateDb();
        Assert.Equal(4, await v.Evidence.CountAsync(e => e.RunId == run.Id));
        Assert.Contains(await v.StepRuns.Where(s => s.RunId == run.Id).ToListAsync(), s => s.Step == "classify");
    }
}
