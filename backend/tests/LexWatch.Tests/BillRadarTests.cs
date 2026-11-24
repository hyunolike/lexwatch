using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using LexWatch.Api.Data.Entities;
using LexWatch.Api.Radar;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexWatch.Tests;

/// <summary>법률명 검색과 의안번호 검색에 답하는 가짜 국회 API. 행은 실응답과 같은 필드 이름을 쓴다.</summary>
public sealed class FakeAssembly
{
    public readonly Dictionary<string, JsonObject> Bills = [];
    public readonly HashSet<string> FailingLaws = [];
    public readonly HashSet<string> FailingBills = [];
    public bool AllFail;
    public readonly List<string> Calls = [];

    public JsonObject Add(string no, string name, string proposed, string? committeeDt = null, string? procResult = null,
        string? procDt = null, string? id = null)
    {
        var row = new JsonObject
        {
            ["BILL_ID"] = id ?? "PRC_" + no, ["BILL_NO"] = no, ["BILL_NAME"] = name, ["PROPOSE_DT"] = proposed,
            ["PROPOSER"] = "홍길동의원 등 10인", ["RST_PROPOSER"] = "홍길동", ["COMMITTEE"] = committeeDt is null ? null : "기후에너지환경노동위원회",
            ["COMMITTEE_DT"] = committeeDt, ["PROC_RESULT"] = procResult, ["PROC_DT"] = procDt,
            ["DETAIL_LINK"] = $"http://likms.assembly.go.kr/bill/billDetail.do?billId=PRC_{no}",
        };
        Bills[no] = row;
        return row;
    }

    private static string Body(IEnumerable<JsonObject> rows)
    {
        var list = rows.Select(r => (JsonNode)r.DeepClone()).ToArray();
        if (list.Length == 0) return """{"RESULT":{"CODE":"INFO-200","MESSAGE":"해당하는 데이터가 없습니다."}}""";
        return new JsonObject
        {
            ["nzmimeepazxkubdpn"] = new JsonArray(
                new JsonObject { ["head"] = new JsonArray(new JsonObject { ["list_total_count"] = list.Length },
                    new JsonObject { ["RESULT"] = new JsonObject { ["CODE"] = "INFO-000" } }) },
                new JsonObject { ["row"] = new JsonArray(list) }),
        }.ToJsonString();
    }

    public HttpResponseMessage Handle(HttpRequestMessage req)
    {
        var q = System.Web.HttpUtility.ParseQueryString(req.RequestUri!.Query);
        if (AllFail) return LawFixtures.Json("x", HttpStatusCode.ServiceUnavailable);
        if (q["BILL_NAME"] is { } law)
        {
            Calls.Add("law:" + law);
            if (FailingLaws.Contains(law)) return LawFixtures.Json(AssemblyFixtures.Read("recorded_bad_key.json"));
            var size = int.Parse(q["pSize"]!);
            return LawFixtures.Json(Body(Bills.Values.Where(b => ((string)b["BILL_NAME"]!).Contains(law))
                .OrderByDescending(b => (string)b["BILL_NO"]!).Take(size)));
        }
        var no = q["BILL_NO"]!;
        Calls.Add("bill:" + no);
        if (FailingBills.Contains(no)) return LawFixtures.Json("x", HttpStatusCode.InternalServerError);
        return LawFixtures.Json(Body(Bills.TryGetValue(no, out var r) ? [r] : []));
    }
}

[Collection("pg")]
public class BillRadarTests(PostgresFixture pg) : IAsyncLifetime
{
    public Task InitializeAsync() => pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 2, 0, 0, TimeSpan.Zero);

    private BillRadarService Service(LexWatch.Api.Data.LexWatchDb db, FakeAssembly fake, TimeProvider time, string key = "") =>
        new(db, AssemblyFixtures.Client(fake.Handle, key), new AssemblyApiOptions(key), time, (_, _) => Task.CompletedTask);

    private async Task<BillCollectResult> Collect(FakeAssembly fake, TimeProvider? time = null, string key = "")
    {
        await using var db = pg.CreateDb();
        return await Service(db, fake, time ?? new FakeTime(T0), key).CollectAsync();
    }

    [Fact]
    public async Task First_collect_stores_exact_matches_with_proposed_event_at_kst_midnight()
    {
        var fake = new FakeAssembly();
        fake.Add("2223132", "근로기준법 일부개정법률안", "2026-11-23");
        fake.Add("2222911", "근로기준법 일부개정법률안", "2026-11-18", committeeDt: "2026-11-19");
        fake.Add("2222741", "한국은행법 일부개정법률안", "2026-11-17");
        fake.Add("2222665", "은행법 일부개정법률안", "2026-11-10");

        var r = await Collect(fake);
        Assert.Equal(3, r.NewBills);
        Assert.Equal(0, r.StageChanges);
        Assert.Empty(r.Failures);
        // 지켜볼 법률 15개를 한 번씩 조회했다.
        Assert.Equal(BillWatch.Defaults.Values.SelectMany(v => v).Distinct().Count(), fake.Calls.Count(c => c.StartsWith("law:")));

        await using var db = pg.CreateDb();
        var bills = await db.Bills.OrderBy(b => b.BillNo).ToListAsync();
        Assert.Equal(["2222665", "2222911", "2223132"], bills.Select(b => b.BillNo));
        Assert.DoesNotContain(bills, b => b.BillName.StartsWith("한국은행법"));
        var b = bills.Single(x => x.BillNo == "2222911");
        Assert.Equal(BillStages.Committee, b.Stage);
        Assert.Equal("근로기준법", b.LawName);
        Assert.StartsWith("https://", b.DetailUrl);
        var ev = await db.BillEvents.SingleAsync(e => e.BillId == b.Id);
        Assert.Equal("proposed", ev.Kind);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero), ev.At.ToUniversalTime());
        Assert.Equal(ev.At, b.LastEventAt);
        var log = await db.BillCollectLogs.SingleAsync();
        Assert.True(log.Ok);
        Assert.Equal(3, log.NewBills);
    }

    [Fact]
    public async Task Second_collect_records_stage_change_and_refreshes_bills_not_in_list()
    {
        var fake = new FakeAssembly();
        // 키 없이 5건만 오므로 6번째(가장 오래된) 법안은 재조회로만 갱신된다.
        for (var i = 0; i < 6; i++) fake.Add($"222000{i}", "근로기준법 일부개정법률안", $"2026-11-0{i + 1}");
        await Collect(fake);
        await using (var db = pg.CreateDb()) Assert.Equal(5, await db.Bills.CountAsync());

        fake.Add("2220000", "근로기준법 일부개정법률안", "2026-11-01", committeeDt: "2026-11-20");
        fake.Add("2220005", "근로기준법 일부개정법률안", "2026-11-06", committeeDt: "2026-11-20");
        var t1 = new FakeTime(T0.AddDays(1));
        var r = await Collect(fake, t1);
        Assert.Equal(0, r.NewBills);
        Assert.Equal(1, r.StageChanges);   // 2220005는 목록에서, 2220000은 목록에 없고 저장도 안 됐다
        await using var db2 = pg.CreateDb();
        var b5 = await db2.Bills.SingleAsync(b => b.BillNo == "2220005");
        Assert.Equal(BillStages.Committee, b5.Stage);
        var change = await db2.BillEvents.SingleAsync(e => e.BillId == b5.Id && e.Kind == "stage_changed");
        Assert.Equal((BillStages.Received, BillStages.Committee), (change.FromStage, change.ToStage));
        Assert.Equal(t1.Now, change.At);
        Assert.Equal(t1.Now, b5.LastEventAt);

        // 새 법안 두 건이 들어와 목록(최신 5건)에서 밀려난 2220001, 2220002는 재조회로 갱신한다.
        fake.Add("2220006", "근로기준법 일부개정법률안", "2026-11-07");
        fake.Add("2220007", "근로기준법 일부개정법률안", "2026-11-08");
        fake.Add("2220001", "근로기준법 일부개정법률안", "2026-11-02", committeeDt: "2026-11-21");
        fake.Calls.Clear();
        var r2 = await Collect(fake, new FakeTime(T0.AddDays(2)));
        Assert.Equal(["bill:2220001", "bill:2220002"], fake.Calls.Where(x => x.StartsWith("bill:")));
        Assert.Equal(2, r2.NewBills);
        Assert.Equal(2, r2.Refreshed);
        Assert.Equal(1, r2.StageChanges);
    }

    [Fact]
    public async Task One_law_failure_keeps_others_and_all_failure_is_502_without_saving()
    {
        var fake = new FakeAssembly();
        fake.Add("2223132", "근로기준법 일부개정법률안", "2026-11-23");
        fake.Add("2223100", "최저임금법 일부개정법률안", "2026-11-20");
        fake.FailingLaws.Add("최저임금법");
        var r = await Collect(fake);
        Assert.Equal(1, r.NewBills);
        Assert.Contains(new BillFailure("law:최저임금법", "ASSEMBLY_API_ERROR_290"), r.Failures);

        fake.AllFail = true;
        fake.Add("2223200", "근로기준법 일부개정법률안", "2026-11-24");
        var e = await Assert.ThrowsAsync<BillCollectUnavailableException>(() => Collect(fake, new FakeTime(T0.AddDays(1))));
        Assert.All(e.Failures.Where(f => f.Target.StartsWith("law:")), f => Assert.Equal("ASSEMBLY_API_HTTP_503", f.Code));
        await using var db = pg.CreateDb();
        Assert.Equal(1, await db.Bills.CountAsync());
        var logs = await db.BillCollectLogs.OrderBy(l => l.StartedAt).ToListAsync();
        Assert.Equal([true, false], logs.Select(l => l.Ok));
    }

    [Fact]
    public async Task Refresh_respects_limit_order_and_pushes_back_failures()
    {
        var fake = new FakeAssembly();
        await using (var db = pg.CreateDb())
        {
            for (var i = 0; i < 70; i++)
                db.Bills.Add(new Bill
                {
                    Id = Guid.NewGuid(), Source = "assembly", ExternalId = $"PRC_21{i:D5}", BillNo = $"21{i:D5}",
                    BillName = "근로기준법 일부개정법률안", LawName = "근로기준법", Stage = BillStages.Committee,
                    ProposedOn = new DateOnly(2026, 1, 1), LastCheckedAt = T0.AddDays(-100 + i), LastEventAt = T0.AddDays(-100),
                });
            // 끝난 법안과 지켜보지 않는 법률의 법안은 재조회하지 않는다.
            db.Bills.Add(new Bill
            {
                Id = Guid.NewGuid(), Source = "assembly", ExternalId = "PRC_X1", BillNo = "X1", BillName = "근로기준법 일부개정법률안",
                LawName = "근로기준법", Stage = BillStages.Closed, LastCheckedAt = T0.AddDays(-999),
            });
            db.Bills.Add(new Bill
            {
                Id = Guid.NewGuid(), Source = "assembly", ExternalId = "PRC_X2", BillNo = "X2", BillName = "선박법 일부개정법률안",
                LawName = "선박법", Stage = BillStages.Committee, LastCheckedAt = T0.AddDays(-999),
            });
            await db.SaveChangesAsync();
        }
        fake.FailingBills.Add("2100000");
        for (var i = 1; i < 70; i++) fake.Add($"21{i:D5}", "근로기준법 일부개정법률안", "2026-01-01", committeeDt: "2026-01-02", id: $"PRC_21{i:D5}");
        // 목록 조회에는 나오지 않게 다른 법안 5건을 더 둔다.
        for (var i = 0; i < 5; i++) fake.Add($"29{i:D5}", "근로기준법 일부개정법률안", "2026-11-01");

        var r = await Collect(fake);
        var refreshCalls = fake.Calls.Where(c => c.StartsWith("bill:")).ToList();
        Assert.Equal(BillRadarService.KeylessRefreshLimit, refreshCalls.Count);
        Assert.Equal("bill:2100000", refreshCalls[0]);
        Assert.DoesNotContain("bill:X1", refreshCalls);
        Assert.DoesNotContain("bill:X2", refreshCalls);
        Assert.Contains(new BillFailure("bill:2100000", "ASSEMBLY_API_HTTP_500"), r.Failures);
        Assert.Equal(59, r.Refreshed);

        await using var db2 = pg.CreateDb();
        var failed = await db2.Bills.SingleAsync(b => b.BillNo == "2100000");
        Assert.Equal(T0, failed.LastCheckedAt);   // 실패해도 뒤로 미룬다
    }

    [Fact]
    public async Task Merged_and_passed_are_watched_for_30_days_after_decision()
    {
        var today = new DateOnly(2026, 11, 24);
        Assert.True(BillStages.NeedsRefresh(BillStages.Merged, today.AddDays(-30), today));
        Assert.False(BillStages.NeedsRefresh(BillStages.Passed, today.AddDays(-31), today));
        Assert.True(BillStages.NeedsRefresh(BillStages.Merged, null, today));
        Assert.False(BillStages.NeedsRefresh(BillStages.Closed, null, today));
        Assert.True(BillStages.NeedsRefresh(BillStages.Received, null, today));

        var fake = new FakeAssembly();
        fake.Add("2217813", "근로기준법 일부개정법률안", "2026-03-01", committeeDt: "2026-03-02", procResult: "대안반영폐기", procDt: "2026-05-07");
        await Collect(fake);
        await using var db = pg.CreateDb();
        var b = await db.Bills.SingleAsync();
        Assert.Equal(BillStages.Merged, b.Stage);
        Assert.Equal("대안반영폐기", b.ProcResult);
        Assert.Equal(new DateOnly(2026, 5, 7), b.ProcOn);
    }

    private WebApplicationFactory<Program> Factory(FakeAssembly fake, TimeProvider? time = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", pg.ConnectionString);
            b.UseSetting("JOB_TOKEN", "secret-token");
            b.ConfigureTestServices(s =>
            {
                s.AddScoped(sp => new BillRadarService(sp.GetRequiredService<LexWatch.Api.Data.LexWatchDb>(),
                    AssemblyFixtures.Client(fake.Handle), new AssemblyApiOptions(""), time ?? TimeProvider.System,
                    (_, _) => Task.CompletedTask));
                if (time is not null) s.AddSingleton(time);
            });
        });

    private static HttpRequestMessage CollectReq(string? token = "secret-token")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/jobs/collect-bills");
        if (token is not null) req.Headers.Add("X-Job-Token", token);
        return req;
    }

    [Fact]
    public async Task Job_endpoint_requires_token_and_maps_unavailable_to_502()
    {
        var fake = new FakeAssembly();
        fake.Add("2223132", "근로기준법 일부개정법률안", "2026-11-23");
        await using var f = Factory(fake);
        var c = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(CollectReq(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(CollectReq("wrong"))).StatusCode);

        var ok = await c.SendAsync(CollectReq());
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("newBills").GetInt32());
        Assert.Equal(0, body.GetProperty("failures").GetArrayLength());

        fake.AllFail = true;
        var bad = await c.SendAsync(CollectReq());
        Assert.Equal(HttpStatusCode.BadGateway, bad.StatusCode);
        var err = await bad.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ASSEMBLY_API_UNAVAILABLE", err.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Radar_lists_by_group_with_status_and_promulgation_candidate()
    {
        var fake = new FakeAssembly();
        var now = new FakeTime(T0);
        await using var f = Factory(fake, now);
        var c = f.CreateClient();

        var empty = await c.GetFromJsonAsync<JsonElement>("/api/radar");
        Assert.Equal("never", empty.GetProperty("sources").GetProperty("assembly").GetString());
        Assert.Equal("not_connected", empty.GetProperty("sources").GetProperty("lawmaking").GetString());
        Assert.Equal(3, empty.GetProperty("watch").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/radar?group=tax")).StatusCode);

        fake.Add("2223132", "근로기준법 일부개정법률안", "2026-11-23");
        fake.Add("2223000", "개인정보 보호법 일부개정법률안", "2026-11-20", committeeDt: "2026-11-21");
        fake.Add("2221000", "근로기준법 일부개정법률안", "2026-09-01", committeeDt: "2026-09-02", procResult: "원안가결", procDt: "2026-10-01");
        Assert.Equal(HttpStatusCode.OK, (await c.SendAsync(CollectReq())).StatusCode);

        // 공포 후보: 처리일 뒤 40일 안, 타법개정 제외, 재현 제외
        await using (var db = pg.CreateDb())
        {
            LawChange Law(string date, string kind) => new()
            {
                Id = Guid.NewGuid(), LawId = "001872", LawName = "근로기준법", PromulgationDate = DateOnly.Parse(date),
                RawJson = $$"""{"제개정구분명":"{{kind}}"}""", RawSha256 = "x", FetchedAt = T0, IdempotencyKey = Guid.NewGuid().ToString(),
            };
            var other = Law("2026-10-05", "타법개정");
            var real = Law("2026-10-14", "일부개정");
            var late = Law("2026-12-01", "일부개정");
            db.LawChanges.AddRange(other, real, late);
            var run = new WorkflowRun { Id = Guid.NewGuid(), LawChangeId = real.Id, Status = RunStatus.InReview, TraceId = "t", CreatedAt = T0 };
            db.WorkflowRuns.Add(run);
            db.GroupReviews.Add(new GroupReview { Id = Guid.NewGuid(), RunId = run.Id, GroupCode = "privacy", Status = ReviewStatus.Approved });
            var labor = new GroupReview { Id = Guid.NewGuid(), RunId = run.Id, GroupCode = "labor", Status = ReviewStatus.Approved };
            db.GroupReviews.Add(labor);
            await db.SaveChangesAsync();

            var radar = await c.GetFromJsonAsync<JsonElement>("/api/radar?group=labor");
            Assert.Equal("ok", radar.GetProperty("sources").GetProperty("assembly").GetString());
            var bills = radar.GetProperty("bills").EnumerateArray().ToList();
            Assert.Equal(["2223132", "2221000"], bills.Select(b => b.GetProperty("billNo").GetString()));
            Assert.Equal(["labor"], bills[0].GetProperty("groupCodes").EnumerateArray().Select(x => x.GetString()));
            Assert.Equal("proposed", bills[0].GetProperty("lastEvent").GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, bills[0].GetProperty("promulgated").ValueKind);
            var p = bills[1].GetProperty("promulgated");
            Assert.Equal(real.Id, p.GetProperty("lawChangeId").GetGuid());
            Assert.Equal(labor.Id, p.GetProperty("reviewId").GetGuid());
        }

        var all = await c.GetFromJsonAsync<JsonElement>("/api/radar");
        Assert.Equal(3, all.GetProperty("bills").GetArrayLength());

        // 마지막 성공이 48시간보다 오래되면 failing
        now.Now = T0.AddHours(49);
        var stale = await c.GetFromJsonAsync<JsonElement>("/api/radar");
        Assert.Equal("failing", stale.GetProperty("sources").GetProperty("assembly").GetString());
    }
}
