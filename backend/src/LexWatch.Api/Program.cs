using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Demo;
using LexWatch.Api.Jobs;
using LexWatch.Api.Runs;
using LexWatch.Api.LawApi;
using LexWatch.Api.Reviews;
using LexWatch.Api.Workflow.Steps;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("LexWatch");
builder.Services.AddDbContext<LexWatchDb>(o =>
    o.UseNpgsql(connectionString, n => n.UseVector()));

var webOrigins = (builder.Configuration["Web:Origins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(webOrigins).WithHeaders("X-Demo-Role", "Content-Type").WithMethods("GET", "POST")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<WorkflowWorker>();

builder.Services.AddLawApi();
builder.Services.AddAiGateway();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<IStepHandler, FetchArticlesStep>();
builder.Services.AddScoped<IStepHandler, ClassifyStep>();
builder.Services.AddScoped<IStepHandler, AnalyzeStep>();
builder.Services.AddScoped<IStepHandler, CriticCitationStep>();
builder.Services.AddScoped<IStepHandler, CriticSourceStep>();
builder.Services.AddScoped<IStepHandler, CritiqueStep>();
builder.Services.AddScoped<IStepHandler, NotifyStep>();
builder.Services.AddScoped(sp => new CollectService(
    sp.GetRequiredService<LexWatchDb>(), sp.GetRequiredService<LawApiClient>(), sp.GetRequiredService<TimeProvider>()));

var app = builder.Build();
// 새 배포 직후 데모 화면이 비지 않도록 재현 실행을 한 번 넣는다. 켜는 쪽은 환경변수(DEMO_SEED_ON_STARTUP=true)다.
if (app.Configuration.GetValue<bool>("DEMO_SEED_ON_STARTUP"))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedIfEmptyAsync();
    }
    catch (Exception e)
    {
        app.Logger.LogWarning(e, "데모 시드 실패 (서비스는 계속 시작한다)");
    }
}
app.UseCors();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapJobEndpoints();
app.MapReviewEndpoints();
app.MapRunEndpoints();
app.Run();

public partial class Program;
