using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("LexWatch");
builder.Services.AddDbContext<LexWatchDb>(o =>
    o.UseNpgsql(connectionString, n => n.UseVector()));

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
