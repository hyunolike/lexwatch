using LexWatch.Api.Data;
using LexWatch.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LexWatch.Tests;

[Collection("pg")]
public class SchemaTests(PostgresFixture pg) : IAsyncLifetime
{
    public async Task InitializeAsync() => await pg.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static LawChange NewChange(string key) => new()
    {
        Id = Guid.NewGuid(), LawId = "L1", LawName = "근로기준법",
        PromulgationDate = new DateOnly(2026, 11, 1), RawJson = "{}", RawSha256 = "abc",
        FetchedAt = DateTimeOffset.UtcNow, IdempotencyKey = key,
    };

    [Fact]
    public async Task Migrations_apply_and_unique_idempotency_key_rejects_duplicate()
    {
        await using (var db = pg.CreateDb())
        {
            db.LawChanges.Add(NewChange("k1"));
            await db.SaveChangesAsync();
        }
        await using (var db = pg.CreateDb())
        {
            Assert.Equal(3, await db.PracticeGroups.CountAsync());
            db.LawChanges.Add(NewChange("k1"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Vector_extension_available()
    {
        var vec = new Vector(Enumerable.Range(0, LexWatchDb.EmbedDim).Select(i => i / 1000f).ToArray());
        await using (var db = pg.CreateDb())
        {
            var g = await db.PracticeGroups.SingleAsync(x => x.Code == "labor");
            g.Embedding = vec;
            await db.SaveChangesAsync();
        }
        await using (var db = pg.CreateDb())
        {
            var g = await db.PracticeGroups.SingleAsync(x => x.Code == "labor");
            Assert.NotNull(g.Embedding);
            Assert.Equal(LexWatchDb.EmbedDim, g.Embedding!.ToArray().Length);
            Assert.Equal(vec.ToArray()[5], g.Embedding.ToArray()[5], 3);
        }
    }

    [Fact]
    public async Task Full_graph_roundtrips_with_string_enums_and_version_token()
    {
        var lc = NewChange("k2");
        var run = new WorkflowRun { Id = Guid.NewGuid(), LawChangeId = lc.Id, Status = RunStatus.InReview, TraceId = "t", CreatedAt = DateTimeOffset.UtcNow };
        var gr = new GroupReview { Id = Guid.NewGuid(), RunId = run.Id, GroupCode = "labor", Status = ReviewStatus.NeedsReview };
        await using (var db = pg.CreateDb())
        {
            db.AddRange(lc, run, gr);
            await db.SaveChangesAsync();
        }
        await using var a = pg.CreateDb();
        await using var b = pg.CreateDb();
        var ga = await a.GroupReviews.SingleAsync();
        var gb = await b.GroupReviews.SingleAsync();
        ga.Status = ReviewStatus.Approved; await a.SaveChangesAsync();
        gb.Status = ReviewStatus.Rejected;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
        var raw = await a.Database.SqlQueryRaw<string>("""SELECT "Status" AS "Value" FROM "GroupReviews" """).SingleAsync();
        Assert.Equal("Approved", raw);
    }
}
