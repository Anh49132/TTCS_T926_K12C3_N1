global using System.Data.Common;
global using QL_PhongTro.Data;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace QL_PhongTro.Data;

public static class DatabaseTransactions
{
    // A transaction-scoped, database-wide writer lock preserves the existing
    // SQLite BEGIN IMMEDIATE ordering before balance/availability reads. Every
    // PostgreSQL business transaction must acquire this same key, across hosts.
    public const long WriterLockKey = 0x4E4841544F54;

    public static DbTransaction BeginPortableTransaction(this DbConnection connection, bool deferred = false)
    {
        if (connection is SqliteConnection sqlite) return sqlite.BeginTransaction(deferred);
        if (connection is not NpgsqlConnection) throw new NotSupportedException("Unsupported database provider.");
        var transaction = connection.BeginTransaction(deferred ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted);
        try { Lock(connection, transaction); return transaction; }
        catch { transaction.Dispose(); throw; }
    }

    public static async Task<IDbContextTransaction> BeginProtectedTransactionAsync(this DatabaseFacade database, CancellationToken ct = default)
    {
        if (database.GetDbConnection() is not NpgsqlConnection) return await database.BeginTransactionAsync(ct);
        var transaction = await database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await using var command = LockCommand(database.GetDbConnection(), transaction.GetDbTransaction());
            await command.ExecuteNonQueryAsync(ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    private static DbCommand LockCommand(DbConnection connection, DbTransaction transaction)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandTimeout = 30;
        command.CommandText = "SELECT pg_advisory_xact_lock(@key)";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = WriterLockKey;
        command.Parameters.Add(parameter);
        return command;
    }
    private static void Lock(DbConnection connection, DbTransaction transaction)
    {
        using var command = LockCommand(connection, transaction); command.ExecuteNonQuery();
    }

    public static DbConnection CreateRelatedConnection(this DbConnection original) => original switch
    {
        SqliteConnection => new SqliteConnection(original.ConnectionString),
        NpgsqlConnection postgres => postgres.CloneWith(postgres.ConnectionString),
        _ => throw new NotSupportedException("Unsupported database provider.")
    };

    // ADO services use the common DbConnection API, retaining provider-native
    // parameters and SQLite's existing AddWithValue behavior.
    public static DbParameter AddWithValue(this DbParameterCollection parameters, string name, object value)
    {
        DbParameter parameter = parameters switch
        {
            SqliteParameterCollection => new SqliteParameter(name, value),
            NpgsqlParameterCollection => new NpgsqlParameter(name, value),
            _ => throw new NotSupportedException("Unsupported parameter provider.")
        };
        parameters.Add(parameter); return parameter;
    }
}
