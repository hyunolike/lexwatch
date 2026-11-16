using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LexWatch.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    // 구형 Docker 데몬(API 1.41)은 Docker.DotNet의 기본 클라이언트 버전(1.44)을 거부한다.
    static PostgresFixture() =>
        Environment.SetEnvironmentVariable("DOCKER_API_VERSION",
            Environment.GetEnvironmentVariable("DOCKER_API_VERSION") ?? "1.41");

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    public string ConnectionString => _container.GetConnectionString();

    public LexWatchDb CreateDb() => new(
        new DbContextOptionsBuilder<LexWatchDb>().UseNpgsql(ConnectionString, o => o.UseVector()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    /// <summary>테스트마다 호출. 시드된 PracticeGroup 행은 유지하고 나머지 데이터를 비운다.</summary>
    public async Task ResetAsync()
    {
        await using var db = CreateDb();
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE "LawChanges","WorkflowRuns","GroupReviews","StepRuns","Evidence","AgentToolCalls",
                     "MemoDrafts","CriticFindings","Approvals","Notifications","ChatQuotas" RESTART IDENTITY CASCADE;
            DELETE FROM "PracticeGroups" WHERE "Code" NOT IN ('labor','privacy','finance');
            UPDATE "PracticeGroups" SET "Embedding" = NULL;
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("pg")]
public class PgCollection : ICollectionFixture<PostgresFixture>;
