using Microsoft.Data.Sqlite;
using Npgsql;

namespace QL_PhongTro.Data;

public static class DatabaseErrors
{
    private static Exception? Root(Exception? error) => error?.GetBaseException();
    public static bool IsConstraint(Exception? error) => Root(error) switch
    {
        SqliteException sqlite => sqlite.SqliteErrorCode == 19,
        PostgresException postgres => postgres.SqlState.StartsWith("23", StringComparison.Ordinal),
        _ => false
    };
    public static bool IsUnique(Exception? error) => Root(error) switch
    {
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode is 2067 or 1555,
        PostgresException postgres => postgres.SqlState == "23505",
        _ => false
    };
    public static bool IsForeignKey(Exception? error) => Root(error) switch
    {
        SqliteException sqlite => sqlite.SqliteExtendedErrorCode == 787,
        PostgresException postgres => postgres.SqlState == "23503",
        _ => false
    };
    public static bool IsBusy(Exception? error) => Root(error) switch
    {
        SqliteException sqlite => sqlite.SqliteErrorCode is 5 or 6,
        PostgresException postgres => postgres.SqlState is "55P03" or "57014" or "40001" or "40P01",
        _ => false
    };
    public static bool IsMissingSchema(Exception? error) => Root(error) switch
    {
        SqliteException sqlite => sqlite.SqliteErrorCode == 1,
        PostgresException postgres => postgres.SqlState is "42P01" or "42703" or "42883",
        _ => false
    };
}
