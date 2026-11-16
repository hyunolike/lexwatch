using System.Security.Cryptography;
using System.Text;
using LexWatch.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LexWatch.Api.Reviews;

/// <summary>검토 보조 채팅 호출 상한: IP 해시당 10회/일, 전체 200회/일. 증가는 한 SQL 문이라 동시 요청이 상한을 넘기지 못한다.</summary>
public static class ChatQuotaService
{
    public const int PerIpLimit = 10;
    public const int GlobalLimit = 200;
    public const string GlobalKey = "__global__";

    public static string HashIp(string ip, DateOnly day, string jobToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ip + day.ToString("yyyy-MM-dd") + jobToken))).ToLowerInvariant();

    /// <summary>둘 다 통과하면 둘 다 올리고 true. 하나라도 가득 차 있으면 아무것도 올리지 않고 false.</summary>
    public static async Task<bool> TryAcquireAsync(LexWatchDb db, DateOnly day, string ipHash, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // 항상 전체 → IP 순으로 잠가 교착을 피한다.
        if (await BumpAsync(db, day, GlobalKey, GlobalLimit, ct) == 0 || await BumpAsync(db, day, ipHash, PerIpLimit, ct) == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>AI 호출이 실패해 답을 못 준 요청은 사용자 몫에서 되돌린다.</summary>
    public static async Task RefundAsync(LexWatchDb db, DateOnly day, string ipHash)
    {
        foreach (var key in new[] { GlobalKey, ipHash })
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ChatQuotas" SET "Count" = "Count" - 1 WHERE "Day" = {day} AND "IpHash" = {key} AND "Count" > 0""");
    }

    private static Task<int> BumpAsync(LexWatchDb db, DateOnly day, string key, int limit, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ChatQuotas" ("Day", "IpHash", "Count") VALUES ({day}, {key}, 1)
            ON CONFLICT ("Day", "IpHash") DO UPDATE SET "Count" = "ChatQuotas"."Count" + 1
            WHERE "ChatQuotas"."Count" < {limit}
            """, ct);
}
