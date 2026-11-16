using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LexWatch.Api.Observability;

/// <summary>추적과 구조화 로그 설정. 내보내기는 콘솔뿐이다(Render·Vercel 로그가 수집한다).</summary>
public static partial class Telemetry
{
    public const string WorkflowSourceName = "LexWatch.Workflow";
    public static readonly ActivitySource Workflow = new(WorkflowSourceName);

    public static void AddLexWatchObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((_, cfg) => cfg
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<TraceEnricher>()
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("lexwatch-api"))
            .WithTracing(t => t
                .AddSource(WorkflowSourceName)
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => ShouldTraceRequest(ctx.Request.Path))
                .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = r => ShouldTraceOutbound(r.RequestUri))
                .AddProcessor(new SensitiveTagScrubber())
                .AddConsoleExporter());
    }

    /// <summary>주기적 /health 호출은 추적하지 않는다(UptimeRobot이 10분마다 부른다).</summary>
    public static bool ShouldTraceRequest(PathString path) => !path.StartsWithSegments("/health");

    /// <summary>법제처 호출의 쿼리에는 OC(인증값)가 들어 있다. 그 요청은 추적하지 않는다.</summary>
    public static bool ShouldTraceOutbound(Uri? uri) =>
        uri is null || !OcQuery().IsMatch(uri.Query);

    [GeneratedRegex(@"[?&]OC=", RegexOptions.IgnoreCase)]
    private static partial Regex OcQuery();

    /// <summary>실행의 TraceId에서 이어지는 단계 Activity를 시작한다. 이어 나가는 HttpClient 호출이 같은 trace로 AI 서비스에 전파된다.</summary>
    public static Activity? StartStep(string traceId, string step, int attempt)
    {
        ActivityContext parent = default;
        if (traceId.Length == 32 && TraceIdRx().IsMatch(traceId) && traceId != new string('0', 32))
        {
            parent = new ActivityContext(ActivityTraceId.CreateFromString(traceId),
                ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        }
        var a = Workflow.StartActivity($"step {step}", ActivityKind.Internal, parent);
        a?.SetTag("step", step);
        a?.SetTag("attempt", attempt);
        return a;
    }

    /// <summary>새 실행의 TraceId(W3C 32자리 16진수). 실행마다 따로 만들고, 이후 모든 단계가 이 trace로 이어진다.</summary>
    public static string NewTraceId() => ActivityTraceId.CreateRandom().ToString();

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdRx();
}

/// <summary>로그에 trace_id/span_id를 붙인다.</summary>
public sealed class TraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory pf)
    {
        if (Activity.Current is not { } a) return;
        logEvent.AddPropertyIfAbsent(pf.CreateProperty("trace_id", a.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(pf.CreateProperty("span_id", a.SpanId.ToString()));
    }
}

/// <summary>콘솔로 나가기 전에 IP·쿼리·UA 태그를 지운다(IP와 쿼리는 로그에 남기지 않는다).</summary>
public sealed class SensitiveTagScrubber : BaseProcessor<Activity>
{
    public static readonly string[] Keys =
    [
        "client.address", "client.port", "network.peer.address", "network.peer.port",
        "http.client_ip", "url.query", "url.full", "http.url", "user_agent.original",
    ];

    public override void OnEnd(Activity data)
    {
        foreach (var k in Keys) data.SetTag(k, null);
    }
}
