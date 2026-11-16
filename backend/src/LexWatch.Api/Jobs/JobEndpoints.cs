using System.Security.Cryptography;
using System.Text;
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
                return Results.Ok(new { created = r.Created, skipped = r.Skipped });
            }
            catch (LawApiException e)
            {
                return Results.Json(new { code = e.Code, message = e.Message }, statusCode: StatusCodes.Status502BadGateway);
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

    private static bool TokenOk(string? expected, string given)
    {
        if (string.IsNullOrEmpty(expected) || given.Length == 0) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }
}
