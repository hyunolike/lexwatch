using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LexWatch.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
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
                     "MemoDrafts","CriticFindings","Approvals","Notifications","ChatQuotas","CollectLogs","ArticleChunks","AiUsageLogs" RESTART IDENTITY CASCADE;
            DELETE FROM "PracticeGroups" WHERE "Code" NOT IN ('labor','privacy','finance');
            UPDATE "PracticeGroups" SET "Embedding" = NULL;
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("pg")]
public class PgCollection : ICollectionFixture<PostgresFixture>;
