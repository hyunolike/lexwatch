using System.Security.Cryptography;
using System.Text;
using LexWatch.Api.Ai;
using LexWatch.Api.Demo;
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
