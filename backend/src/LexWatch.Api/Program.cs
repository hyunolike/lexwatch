using LexWatch.Api.Ai;
using LexWatch.Api.Data;
using LexWatch.Api.Jobs;
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

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<WorkflowWorker>();

builder.Services.AddLawApi();
builder.Services.AddAiGateway();
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapJobEndpoints();
app.MapReviewEndpoints();
app.Run();

public partial class Program;
