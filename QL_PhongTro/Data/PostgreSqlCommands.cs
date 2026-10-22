using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace QL_PhongTro.Data;

public static partial class PostgreSqlCommands
{
    // Quoted values, identifiers and comments are complete tokens, so a string
    // containing '$id' or 'AS Value' is never rewritten as SQL syntax.
    [GeneratedRegex("--[^\\r\\n]*|/\\*.*?\\*/|'(?:''|[^'])*'|\"(?:\"\"|[^\"])*\"|(?<dollar>\\$(?:[A-Za-z_][A-Za-z_0-9]*)?\\$).*?\\k<dollar>|\\$[A-Za-z_][A-Za-z_0-9]*|[A-Za-z_][A-Za-z_0-9]*|[^\\s]", RegexOptions.Singleline)]
    private static partial Regex Tokens();

    public static string Normalize(string sql)
    {
        var result = new StringBuilder(); var end = 0; var previous = "";
        foreach (Match match in Tokens().Matches(sql))
        {
            result.Append(sql.AsSpan(end, match.Index - end));
            var token = match.Value;
            if (Regex.IsMatch(token, @"^\$[A-Za-z_][A-Za-z_0-9]*$")) token = "@" + token[1..];
            else if (previous.Equals("AS", StringComparison.OrdinalIgnoreCase) && token.Equals("Value", StringComparison.OrdinalIgnoreCase)) token = "\"Value\"";
            result.Append(token); end = match.Index + match.Length;
            if (!token.StartsWith("--") && !token.StartsWith("/*")) previous = token;
        }
        return result.Append(sql.AsSpan(end)).ToString();
    }

    public static void Prepare(DbCommand command, bool rawAdo = false)
    {
        if (command is not NpgsqlCommand) return;
        command.CommandText = Normalize(command.CommandText);
        foreach (NpgsqlParameter parameter in command.Parameters)
        {
            if (parameter.ParameterName.StartsWith('$')) parameter.ParameterName = "@" + parameter.ParameterName[1..];
            if (parameter.Value is DateTime time)
            {
                parameter.NpgsqlDbType = NpgsqlDbType.Text;
                parameter.Value = PostgreSqlTimestamp.Write(time);
            }
            else if (rawAdo && parameter.Value is bool flag)
            {
                parameter.NpgsqlDbType = NpgsqlDbType.Integer; parameter.Value = flag ? 1 : 0;
            }
        }
    }

    public static DbCommand CreatePortableCommand(this DbConnection connection)
    {
        var command = connection.CreateCommand();
        return command is NpgsqlCommand ? new PortableCommand(command) : command;
    }

    private sealed class PortableCommand(DbCommand inner) : DbCommand
    {
        [AllowNull] public override string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
        public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
        public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
        public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
        public override UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
        protected override DbConnection? DbConnection { get => inner.Connection; set => inner.Connection = value; }
        protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = value; }
        protected override DbParameterCollection DbParameterCollection => inner.Parameters;
        protected override DbParameter CreateDbParameter() => inner.CreateParameter();
        public override void Cancel() => inner.Cancel();
        public override void Prepare() { PostgreSqlCommands.Prepare(inner, rawAdo: true); inner.Prepare(); }
        public override int ExecuteNonQuery() { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteNonQuery(); }
        public override object? ExecuteScalar() { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteScalar(); }
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteReader(behavior); }
        public override Task<int> ExecuteNonQueryAsync(CancellationToken ct) { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteNonQueryAsync(ct); }
        public override Task<object?> ExecuteScalarAsync(CancellationToken ct) { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteScalarAsync(ct); }
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken ct) { PostgreSqlCommands.Prepare(inner, rawAdo: true); return inner.ExecuteReaderAsync(behavior, ct); }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}

// EF raw SQL and ordinary ADO commands share exactly the same limited adapter.
// No DDL, PRAGMA skipping, or unparameterized values are introduced here.
public sealed class PostgreSqlCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { PostgreSqlCommands.Prepare(command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { PostgreSqlCommands.Prepare(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { PostgreSqlCommands.Prepare(command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default) { PostgreSqlCommands.Prepare(command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { PostgreSqlCommands.Prepare(command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default) { PostgreSqlCommands.Prepare(command); return ValueTask.FromResult(result); }
}
