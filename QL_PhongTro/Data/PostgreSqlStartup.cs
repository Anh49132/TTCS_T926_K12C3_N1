using Npgsql;

namespace QL_PhongTro.Data;

public static class PostgreSqlStartup
{
    public static string ConnectionString(IConfiguration configuration)
    {
        var value = configuration["ConnectionStrings:PostgreSql"] ?? throw new InvalidOperationException("PostgreSQL secret missing.");
        var cs = new NpgsqlConnectionStringBuilder(value) { SslMode = SslMode.VerifyFull, IncludeErrorDetail = false, MaxPoolSize = 10, Timeout = 30 };
        if (cs.Host is null || !cs.Host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected database host.");
        return cs.ConnectionString;
    }
    public static async Task CheckAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand("""
            SELECT (SELECT max(version) FROM app_schema_version),
             (SELECT count(*) FROM information_schema.tables WHERE table_schema=current_schema() AND table_type='BASE TABLE'),
             (SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=current_schema() AND NOT t.tgisinternal)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        if (reader.IsDBNull(0) || reader.GetInt64(0) != 42 || reader.GetInt64(1) != 61 || reader.GetInt64(2) != 60)
            throw new InvalidOperationException("PostgreSQL deployment schema is not ready. No automatic migrations are allowed.");
    }
}
