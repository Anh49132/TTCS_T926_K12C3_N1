using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using Xunit;

public class DatabaseTransactionsTests
{
    [Fact]
    public void Sqlite_transaction_still_rolls_back_with_native_parameters()
    {
        using DbConnection connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE ledger(amount INTEGER NOT NULL)"; command.ExecuteNonQuery();
        using (var transaction = connection.BeginPortableTransaction(deferred: false))
        {
            command.Transaction = transaction; command.CommandText = "INSERT INTO ledger(amount) VALUES ($amount)";
            command.Parameters.AddWithValue("$amount", 1234567890123L); command.ExecuteNonQuery();
            transaction.Rollback();
        }
        command.Transaction = null; command.CommandText = "SELECT count(*) FROM ledger";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    [Fact]
    public async Task Ef_Sqlite_transaction_is_owned_and_releases_connection()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE ledger(amount INTEGER NOT NULL)");
        await using (var transaction = await db.Database.BeginProtectedTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("INSERT INTO ledger(amount) VALUES (100)");
            await transaction.RollbackAsync();
        }
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>("SELECT count(*) AS Value FROM ledger").SingleAsync());
    }
}
