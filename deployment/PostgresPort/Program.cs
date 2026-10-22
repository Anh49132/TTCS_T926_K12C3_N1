using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QL_PhongTro.Data;

if (args is ["--copy-referenced-files", var fileConfig, var manifest])
{
    try { await FileTransfer.RunAsync(fileConfig,manifest); }
    catch(Exception ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new{failed=true,errorType=ex.GetType().Name}));Environment.ExitCode=1; }
    return;
}
if (args is ["--write-smoke", var writeConfig, var writeSql])
{
    try { await WriteSmoke.RunAsync(writeConfig,writeSql); }
    catch (Npgsql.PostgresException ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, sqlState=ex.SqlState, table=ex.TableName, column=ex.ColumnName, constraint=ex.ConstraintName, syntheticError=ex.MessageText, position=ex.Position, function=ex.Routine })); Environment.ExitCode=1; }
    catch (Exception ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, errorType=ex.GetType().Name, syntheticStack=ex.StackTrace })); Environment.ExitCode=1; }
    return;
}
if (args is ["--storage-smoke", var storageConfig])
{
    try { await StorageSmoke.RunAsync(storageConfig); }
    catch (Exception ex) { Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, errorType = ex.GetType().Name })); Environment.ExitCode = 1; }
    return;
}

if (args.Length == 4 && args[0] is "--rehearse" or "--cutover-empty-main")
{
    try { await Rehearsal.RunAsync(args[1], args[2], args[3], args[0] == "--cutover-empty-main"); }
    catch (Npgsql.PostgresException ex)
    {
        // Detail/MessageText may contain row values, tokens or personal data.
        Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, sqlState = ex.SqlState, table = ex.TableName, column = ex.ColumnName, constraint = ex.ConstraintName }));
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, errorType = ex.GetType().Name }));
        Environment.ExitCode = 1;
    }
    return;
}

// Schema metadata only. Never invokes the web application's startup or initializers.
if (args is ["--export-model", var output])
{
    using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
    var tables = db.Model.GetEntityTypes().Where(e => e.GetTableName() is not null).Select(e => new
    {
        table = e.GetTableName(),
        columns = e.GetProperties().Select(p => new
        {
            column = p.GetColumnName(StoreObjectIdentifier.Table(e.GetTableName()!, e.GetSchema())),
            type = (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).Name,
            nullable = p.IsNullable,
            primary = p.IsPrimaryKey()
        })
    });
    var path = Path.GetFullPath(output);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(tables, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Exported model metadata only; no database opened.");
    return;
}
throw new ArgumentException("Supported: --export-model <ignored output path>");
