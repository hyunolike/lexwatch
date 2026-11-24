using System.Security.Cryptography;
using System.Text;
using LexWatch.Api.Ai;
using LexWatch.Api.Demo;
using LexWatch.Api.Radar;
using LexWatch.Api.Workflow.Steps;

namespace LexWatch.Api.Jobs;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/jobs/collect", async (HttpContext http, IConfiguration cfg, CollectService svc,
            TimeProvider time, CancellationToken ct) =>
        {
            if (!TokenOk(cfg["JOB_TOKEN"], http.Request.Headers["X-Job-Token"].ToString()))
                return Results.Unauthorized();
            var (from, to) = CollectService.KstWindow(time);
            try
            {
                var r = await svc.CollectAsync(from, to, ct);
                return Results.Ok(new { created = r.Created, skipped = r.Skipped, runIds = r.RunIds });
            }
            catch (LawApiException e)
            {
                return Results.Json(new { code = e.Code, message = e.Message }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        // AI 서비스(무료 인스턴스)를 깨운다. 한 번만 부르고 반복은 호출하는 쪽(collect 워크플로)이 맡는다.
        app.MapPost("/jobs/wake-ai", async (HttpContext http, IConfiguration cfg, IAiClient ai, CancellationToken ct) =>
        {
            if (!TokenOk(cfg["JOB_TOKEN"], http.Request.Headers["X-Job-Token"].ToString()))
                return Results.Unauthorized();
            var started = System.Diagnostics.Stopwatch.StartNew();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(WakeTimeout);
            try
            {
                await ai.HealthAsync(cts.Token);
                return Results.Ok(new { ai = "ok", ms = started.ElapsedMilliseconds });
            }
            catch (Exception e) when ((e is AiApiException or HttpRequestException or OperationCanceledException) && !ct.IsCancellationRequested)
            {
                return Results.Json(new { code = "AI_ASLEEP", message = "AI 서비스가 아직 응답하지 않는다" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        // 공포 전 법안 수집(bill-radar BR-02~05). 서버가 120초 상한을 지키고, 요청이 끊겨도 받은 만큼 저장한다.
        app.MapPost("/jobs/collect-bills", async (HttpContext http, IConfiguration cfg, BillRadarService svc) =>
        {
            if (!TokenOk(cfg["JOB_TOKEN"], http.Request.Headers["X-Job-Token"].ToString()))
                return Results.Unauthorized();
            try
            {
                var r = await svc.CollectAsync();
                return Results.Ok(new
                {
                    newBills = r.NewBills, stageChanges = r.StageChanges, refreshed = r.Refreshed,
                    failures = r.Failures.Select(f => new { target = f.Target, code = f.Code }),
                });
            }
            catch (BillCollectUnavailableException e)
            {
                return Results.Json(new
                {
                    code = "ASSEMBLY_API_UNAVAILABLE", message = "국회 API 조회가 모두 실패했다",
                    failures = e.Failures.Select(f => new { target = f.Target, code = f.Code }),
                }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        app.MapPost("/jobs/reset-demo", async (HttpContext http, IConfiguration cfg, DemoSeeder seeder, CancellationToken ct) =>
        {
            if (!TokenOk(cfg["JOB_TOKEN"], http.Request.Headers["X-Job-Token"].ToString()))
                return Results.Unauthorized();
            await seeder.ResetAsync(ct);
            return Results.Ok(new { reset = true });
        });
    }

    public static readonly TimeSpan WakeTimeout = TimeSpan.FromSeconds(10);

    private static bool TokenOk(string? expected, string given)
    {
        if (string.IsNullOrEmpty(expected) || given.Length == 0) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }
}
