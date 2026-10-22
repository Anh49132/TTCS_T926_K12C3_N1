using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Npgsql;
using NpgsqlTypes;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;

// An isolated, rollback-only rehearsal. Does not start the application or run
// initializers. SQL errors are redacted by the entry point, never dumped with rows.
internal static class Rehearsal
{
    private sealed record Column(string Name, string StoreType);
    private static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static async Task<string> HashFile(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    internal static async Task RunAsync(string configPath, string inventoryPath, string sqlPath, bool cutover = false)
    {
        var root = Directory.GetCurrentDirectory();
        var dataRoot = Path.GetFullPath(Path.Combine(root, "data", "postgres-port")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(configPath).StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Connection file must be under ignored data/postgres-port.");
        var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var uri = new Uri(config.RootElement.GetProperty("ConnectionUri").GetString()!);
        var hostHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(uri.DnsSafeHost)));
        if (config.RootElement.GetProperty("BranchId").GetString() != (cutover ? "br-bitter-voice-b360ggd5" : "br-spring-smoke-b33dbe1e")
            || config.RootElement.GetProperty("HostSha256").GetString() != hostHash
            || !uri.DnsSafeHost.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the pinned Neon migration-test endpoint is allowed.");
        var credentials = uri.UserInfo.Split(':', 2);
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.VerifyFull, IncludeErrorDetail = false,
            Timeout = 30, CommandTimeout = 180, ApplicationName = "NhaTotMigrationRehearsal"
        };
        // Safety boundary: this tool is never allowed to target a production branch.
        await using var pg = new NpgsqlConnection(cs.ConnectionString);
        await pg.OpenAsync();
        if (!cutover) await VerifyWriterLock(pg, cs.ConnectionString);
        using (var check = new NpgsqlCommand("SELECT current_database(), current_setting('server_version_num')::integer", pg))
        await using (var reader = await check.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            if (reader.GetString(0) != "nhatot" || reader.GetInt32(1) < 170000)
                throw new InvalidOperationException("Unexpected target.");
        }

        var envLines = await File.ReadAllLinesAsync(Path.Combine(root, ".env.local"));
        var configured = envLines.First(line => line.TrimStart().StartsWith("DatabasePath=", StringComparison.Ordinal));
        var source = Path.GetFullPath(configured.Split('=', 2)[1].Trim().Trim('\"', '\''), root);
        if (!File.Exists(source)) throw new FileNotFoundException();
        var before = await HashFile(source);
        var directory = Path.Combine(dataRoot, DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        var snapshot = Path.Combine(directory, "source.sqlite");
        var ro = new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, ForeignKeys = true };
        using (var sourceConnection = new SqliteConnection(ro.ConnectionString))
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = snapshot, Mode = SqliteOpenMode.ReadWriteCreate }.ConnectionString))
        {
            sourceConnection.Open(); destination.Open(); sourceConnection.BackupDatabase(destination);
        }
        if (before != await HashFile(source)) throw new InvalidOperationException("Source changed during backup.");
        ro.DataSource = snapshot;
        using var sqlite = new SqliteConnection(ro.ConnectionString); sqlite.Open();
        using (var integrity = sqlite.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check";
            if ((string?)integrity.ExecuteScalar() != "ok") throw new InvalidOperationException("Integrity failed.");
            integrity.CommandText = "PRAGMA foreign_key_check";
            using var reader = integrity.ExecuteReader();
            if (reader.Read()) throw new InvalidOperationException("Foreign keys failed.");
        }

        var inventory = JsonDocument.Parse(await File.ReadAllTextAsync(inventoryPath));
        var expected = inventory.RootElement.GetProperty("tables").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("rows").GetInt64());
        var sql = await File.ReadAllTextAsync(sqlPath);
        var triggerStart = sql.IndexOf("CREATE FUNCTION \"fn_", StringComparison.Ordinal);
        if (triggerStart < 0 || sql.Contains("DROP ", StringComparison.OrdinalIgnoreCase) || sql.Contains("DELETE FROM ", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected migration script.");
        var tables = Regex.Matches(sql[..triggerStart], "CREATE TABLE \"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        if (tables.Length != expected.Count || tables.Distinct().Count() != expected.Count)
            throw new InvalidOperationException("Inventory mismatch.");

        await using var tx = await pg.BeginTransactionAsync();
        var schema = cutover ? "public" : "rehearsal_" + Guid.NewGuid().ToString("N");
        if (cutover)
        {
            using var check = new NpgsqlCommand("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public'",pg,tx);
            if ((long)(await check.ExecuteScalarAsync())! != 0) throw new InvalidOperationException("Cutover requires a completely empty public schema. Existing data will never be replaced.");
        }
        using (var command = new NpgsqlCommand((cutover ? "" : "CREATE SCHEMA " + Q(schema) + "; ") + "SET LOCAL search_path TO " + Q(schema) + ", pg_catalog; " + sql[..triggerStart], pg, tx))
            await command.ExecuteNonQueryAsync();

        long total = 0;
        foreach (var table in tables)
        {
            var columns = new List<Column>();
            using (var command = new NpgsqlCommand("SELECT column_name,data_type FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table ORDER BY ordinal_position", pg, tx))
            {
                command.Parameters.AddWithValue("schema", schema); command.Parameters.AddWithValue("table", table);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) columns.Add(new(reader.GetString(0), reader.GetString(1)));
            }
            var names = string.Join(",", columns.Select(c => Q(c.Name)));
            using var select = sqlite.CreateCommand();
            select.CommandText = "SELECT " + names + " FROM " + Q(table) + " ORDER BY rowid";
            using var rows = select.ExecuteReader();
            var sourceRows = new List<string>();
            await using (var copy = await pg.BeginBinaryImportAsync("COPY " + Q(schema) + "." + Q(table) + " (" + names + ") FROM STDIN (FORMAT BINARY)"))
            {
                while (rows.Read())
                {
                    await copy.StartRowAsync();
                    var canonical = new object?[columns.Count];
                    for (var i = 0; i < columns.Count; i++)
                    {
                        if (rows.IsDBNull(i)) { await copy.WriteNullAsync(); continue; }
                        var value = ConvertValue(rows.GetValue(i), columns[i].StoreType);
                        canonical[i] = Canonical(value);
                        await copy.WriteAsync(value, PgType(columns[i].StoreType));
                    }
                    sourceRows.Add(Digest(canonical));
                }
                await copy.CompleteAsync();
            }
            if (sourceRows.Count != expected[table]) throw new InvalidOperationException("Source inventory changed: " + table);
            var targetRows = new List<string>();
            using (var command = new NpgsqlCommand("SELECT " + names + " FROM " + Q(schema) + "." + Q(table), pg, tx))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var canonical = new object?[columns.Count];
                    for (var i = 0; i < columns.Count; i++) if (!reader.IsDBNull(i)) canonical[i] = Canonical(reader.GetValue(i));
                    targetRows.Add(Digest(canonical));
                }
            }
            sourceRows.Sort(StringComparer.Ordinal); targetRows.Sort(StringComparer.Ordinal);
            if (!sourceRows.SequenceEqual(targetRows)) throw new InvalidOperationException("Round-trip mismatch: " + table);
            total += sourceRows.Count;
            Console.WriteLine(JsonSerializer.Serialize(new { table, rows = sourceRows.Count, valuesMatched = true }));
        }
        using (var command = new NpgsqlCommand(sql[triggerStart..], pg, tx)) await command.ExecuteNonQueryAsync();
        var verifiedEntities = await VerifyEntityReads(pg, tx, expected, ro.ConnectionString);
        await VerifyReadServices(pg, tx, ro.ConnectionString);
        if (before != await HashFile(source)) throw new InvalidOperationException("Source changed.");
        if (cutover)
        {
            // Preserve deleted-ID high water marks as well as current row IDs.
            foreach (var table in tables)
            {
                using var identity = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name=@table AND is_identity='YES'",pg,tx);
                identity.Parameters.AddWithValue("table",table);
                var column = await identity.ExecuteScalarAsync() as string;
                if (column is null) continue;
                using var sourceSequence=sqlite.CreateCommand(); sourceSequence.CommandText="SELECT seq FROM sqlite_sequence WHERE name=@name";
                sourceSequence.Parameters.AddWithValue("@name",table);
                var sequence=sourceSequence.ExecuteScalar();
                using var adjust=new NpgsqlCommand("SELECT setval(pg_get_serial_sequence(@table,@column),GREATEST(COALESCE((SELECT MAX("+Q(column)+") FROM "+Q(table)+"),0),@sequence,1),true)",pg,tx);
                adjust.Parameters.AddWithValue("table",table);adjust.Parameters.AddWithValue("column",column);adjust.Parameters.AddWithValue("sequence",sequence is null ? 0L : Convert.ToInt64(sequence));
                await adjust.ExecuteScalarAsync();
            }
            await tx.CommitAsync();
        }
        else await tx.RollbackAsync();
        var result = new { tables = tables.Length, rows = total, valuesMatched = true, efEntities = verifiedEntities, scalarValuesMatched = true, readServicesMatched = 3, writerLockVerified = !cutover, rolledBack = !cutover, committed = cutover, sourceUnchanged = true };
        await File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(result));
        Console.WriteLine(JsonSerializer.Serialize(result));
    }

    private static async Task VerifyWriterLock(NpgsqlConnection first, string connectionString)
    {
        await using var second = new NpgsqlConnection(connectionString); await second.OpenAsync();
        await using var observer = new NpgsqlConnection(connectionString); await observer.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(first).Options);
        await using var held = await context.Database.BeginProtectedTransactionAsync();
        var waiting = Task.Run(() => second.BeginPortableTransaction());
        var blocked = false;
        try
        {
            // Neon proxies may expose a cancellation PID different from the
            // backend PID. Identify our database-scoped advisory key instead.
            using var check = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database()) AND classid=CAST(@hi AS oid) AND objid=CAST(@lo AS oid))", observer);
            check.Parameters.AddWithValue("hi", DatabaseTransactions.WriterLockKey >> 32);
            check.Parameters.AddWithValue("lo", DatabaseTransactions.WriterLockKey & 0xffffffffL);
            for (var i = 0; i < 50 && !waiting.IsCompleted; i++)
            {
                if ((bool)(await check.ExecuteScalarAsync())!) { blocked = true; break; }
                await Task.Delay(100);
            }
        }
        finally { await held.RollbackAsync(); }
        await using var acquired = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        await acquired.RollbackAsync();
        if (!blocked) throw new InvalidOperationException("Cross-connection writer serialization was not observed.");
        Console.WriteLine(JsonSerializer.Serialize(new { postgresWriterLock = true, efAndAdoSerialized = true, releasedOnRollback = true }));
    }

    private sealed class FixedClock : ITimeProvider { public DateTime UtcNow => new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc); }
    private static async Task VerifyReadServices(NpgsqlConnection connection, NpgsqlTransaction transaction, string sourceConnection)
    {
        await using var target = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).AddInterceptors(new PostgreSqlCommandInterceptor()).Options);
        await target.Database.UseTransactionAsync(transaction);
        await using var source = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sourceConnection).Options);
        var clock = new FixedClock();
        foreach (var owner in await source.ToaNhas.Select(t => t.ChuNhaId).Distinct().OrderBy(id => id).ToListAsync())
        {
            var buildings = await source.ToaNhas.Where(t => t.ChuNhaId == owner).Select(t => t.Id).OrderBy(id => id).ToArrayAsync();
            var a = await new DashboardFinanceService(source).LoadAsync(owner, "CHU_NHA", buildings, new(2026, 10, 1), new(2026, 10, 22));
            var b = await new DashboardFinanceService(target).LoadAsync(owner, "CHU_NHA", buildings, new(2026, 10, 1), new(2026, 10, 22));
            a.Buildings = a.Buildings.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            b.Buildings = b.Buildings.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
            if (JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b)) throw new InvalidOperationException("Dashboard financial results changed.");
        }
        foreach (var request in await source.YeuCauThues.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync())
        {
            var a = await new LichHenService(source, clock).DocAsync(request, default);
            var b = await new LichHenService(target, clock).DocAsync(request, default);
            if (JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b)) throw new InvalidOperationException("Appointment read results changed.");
        }
        var invoices = await (from h in source.HoaDons join c in source.HopDongs on h.HopDongId equals c.Id
            join p in source.PhongTros on c.PhongId equals p.Id join t in source.ToaNhas on p.ToaNhaId equals t.Id
            orderby h.Id select new { h.Id, Owner = t.ChuNhaId }).Take(20).ToListAsync();
        foreach (var invoice in invoices)
        {
            var a = await new GhiThuHoaDonService(source, clock).ChiTietAsync(invoice.Owner, invoice.Id);
            var b = await new GhiThuHoaDonService(target, clock).ChiTietAsync(invoice.Owner, invoice.Id);
            if (JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b)) throw new InvalidOperationException("Receipt/deposit balances changed.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { readServices = new[] { "DashboardFinance", "LichHen", "GhiThuHoaDon" }, resultsMatched = true }));
    }

    private static async Task<int> VerifyEntityReads(NpgsqlConnection connection, NpgsqlTransaction transaction, Dictionary<string, long> expected, string sourceConnection)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await using var source = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sourceConnection).Options);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        source.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        await db.Database.UseTransactionAsync(transaction);
        var set = typeof(DbContext).GetMethods().Single(m => m.Name == "Set" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        var ignoreFilters = typeof(EntityFrameworkQueryableExtensions).GetMethods().Single(m => m.Name == "IgnoreQueryFilters" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
        var entities = db.Model.GetEntityTypes().OrderBy(e => e.GetTableName()).ToArray();
        foreach (var entity in entities)
        {
            var table = entity.GetTableName()!;
            var properties = entity.GetProperties().OrderBy(p => p.Name).Select(p => p.PropertyInfo ?? throw new InvalidOperationException("Shadow property needs explicit validation.")).ToArray();
            async Task<List<string>> Read(AppDbContext context)
            {
                var query = set.MakeGenericMethod(entity.ClrType).Invoke(context, null)!;
                query = ignoreFilters.MakeGenericMethod(entity.ClrType).Invoke(null, [query])!;
                var hashes = new List<string>();
                await foreach (var row in (IAsyncEnumerable<object>)query)
                    hashes.Add(Digest(properties.Select(p => p.GetValue(row) is { } value ? Canonical(value) : null).ToArray()));
                hashes.Sort(StringComparer.Ordinal);
                return hashes;
            }
            var target = await Read(db); var original = await Read(source);
            if (target.Count != expected[table] || !target.SequenceEqual(original)) throw new InvalidOperationException("Entity value mismatch: " + table);
            Console.WriteLine(JsonSerializer.Serialize(new { efTable = table, rows = target.Count, allScalarValuesMatched = true }));
        }
        return entities.Length;
    }

    private static object ConvertValue(object value, string storeType) => storeType switch
    {
        "integer" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
        "bigint" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        "numeric" => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        "double precision" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        "date" => DateOnly.ParseExact(Convert.ToString(value, CultureInfo.InvariantCulture)!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        "text" => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        "bytea" when value is byte[] bytes => bytes,
        _ => throw new NotSupportedException("Unsupported store type.")
    };
    private static NpgsqlDbType PgType(string type) => type switch
    {
        "integer" => NpgsqlDbType.Integer, "bigint" => NpgsqlDbType.Bigint, "numeric" => NpgsqlDbType.Numeric,
        "double precision" => NpgsqlDbType.Double, "date" => NpgsqlDbType.Date, "text" => NpgsqlDbType.Text, "bytea" => NpgsqlDbType.Bytea,
        _ => throw new NotSupportedException()
    };
    private static object Canonical(object value) => value switch
    {
        int n => (long)n, byte[] bytes => Convert.ToBase64String(bytes),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => time.Ticks.ToString(CultureInfo.InvariantCulture),
        decimal number => number.ToString("G29", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture), _ => value
    };
    private static string Digest(object?[] row) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(row)));
}
