using Npgsql;

namespace LexWatch.Api.Data;

/// <summary>
/// Supabase·Render가 주는 postgres:// URI를 Npgsql 연결 문자열로 바꾼다. 이미 키=값 형식이면 그대로 둔다.
/// </summary>
public static class ConnectionStringNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) return raw;

        var u = new Uri(raw);
        var info = u.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = u.Host,
            Port = u.Port > 0 ? u.Port : 5432,
            Database = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(info[0]),
            Password = info.Length > 1 ? Uri.UnescapeDataString(info[1]) : null,
        };
        foreach (var kv in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = kv.Split('=', 2);
            if (p.Length != 2 || !p[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)) continue;
            if (p[1].Equals("require", StringComparison.OrdinalIgnoreCase))
            {
                b.SslMode = SslMode.Require; // Npgsql 10의 Require는 암호화만 요구하고 인증서는 검증하지 않는다.
            }
            else if (p[1].Equals("disable", StringComparison.OrdinalIgnoreCase)) b.SslMode = SslMode.Disable;
        }
        return b.ConnectionString;
    }
}
