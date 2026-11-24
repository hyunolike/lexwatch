using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Demo;
using LexWatch.Api.Governance;
using LexWatch.Api.Jobs;
using LexWatch.Api.Observability;
using LexWatch.Api.Runs;
using LexWatch.Api.LawApi;
using LexWatch.Api.Radar;
using LexWatch.Api.Ops;
using LexWatch.Api.Reviews;
using LexWatch.Api.Retrieval;
using LexWatch.Api.Workflow.Steps;
using LexWatch.Api.Workflow;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddLexWatchObservability();

var connectionString = ConnectionStringNormalizer.Normalize(Environment.GetEnvironmentVariable("DATABASE_URL"))
    ?? builder.Configuration.GetConnectionString("LexWatch");
builder.Services.AddDbContext<LexWatchDb>(o =>
    o.UseNpgsql(connectionString, n => n.UseVector()));

var webOrigins = (builder.Configuration["Web:Origins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(webOrigins).WithHeaders("X-Demo-Role", "Content-Type").WithMethods("GET", "POST")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(GovernanceOptions.From(builder.Configuration));
builder.Services.AddHostedService<WorkflowWorker>();

builder.Services.AddLawApi();
builder.Services.AddAssemblyApi();
builder.Services.AddScoped<BillRadarService>();
builder.Services.AddAiGateway();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<IStepHandler, FetchArticlesStep>();
builder.Services.AddScoped<IStepHandler, ClassifyStep>();
builder.Services.AddScoped<IStepHandler, IndexArticlesStep>();
builder.Services.AddScoped<IStepHandler>(sp => new AnalyzeStep(
    sp.GetRequiredService<AiGateway>(), sp.GetRequiredService<TimeProvider>(),
    Enum.TryParse<SearchMode>(builder.Configuration["Retrieval:Mode"], true, out var m) ? m : SearchMode.Hybrid));
builder.Services.AddScoped<IStepHandler, CriticCitationStep>();
builder.Services.AddScoped<IStepHandler, CriticSourceStep>();
builder.Services.AddScoped<IStepHandler, CritiqueStep>();
builder.Services.AddScoped<IStepHandler, NotifyStep>();
builder.Services.AddScoped(sp => new CollectService(
    sp.GetRequiredService<LexWatchDb>(), sp.GetRequiredService<LawApiClient>(), sp.GetRequiredService<TimeProvider>()));

var app = builder.Build();
// 스키마는 앱이 시작할 때 적용한다(MIGRATE_ON_STARTUP=true). compose와 Render가 켠다. 동시 시작은 EF의 advisory lock이 직렬화한다.
if (app.Configuration.GetValue<bool>("MIGRATE_ON_STARTUP"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LexWatchDb>().Database.MigrateAsync();
}
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
// commit: Render가 넣어 주는 RENDER_GIT_COMMIT. 배포 워크플로가 새 코드가 떴는지 이 값으로 확인한다.
var deployedCommit = app.Configuration["RENDER_GIT_COMMIT"] is { Length: > 0 } c ? c : null;
app.MapGet("/health", () => Results.Ok(new { status = "ok", commit = deployedCommit }));
app.MapJobEndpoints();
app.MapReviewEndpoints();
app.MapRunEndpoints();
app.MapGovernanceEndpoints();
app.MapRadarEndpoints();
app.MapOpsEndpoints();
app.Run();

public partial class Program;
