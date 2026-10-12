using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task Version23AddsPaymentHistoryWithoutChangingExistingInvoiceData()
    {
        int invoiceId;
        using (var db = Context())
        {
            var fixture = await BillingAsync(db);
            await LinkTenantsAsync(db, fixture.B);
            invoiceId = await AddPublishedTenantInvoiceAsync(db, fixture.A, "HD-MIGRATION-2026", true);
        }

        using (var connection = Open())
        using (var command = connection.CreateCommand())
        {
            SchemaTestFixture.RemoveRoomExtensions(path);
            command.CommandText = "DROP TABLE thanh_toan; DELETE FROM app_schema_version WHERE version>=23";
            command.ExecuteNonQuery();
        }

        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(path);

        using var verify = Context();
        var invoice = await verify.HoaDons.Include(x => x.ChiTiet).SingleAsync(x => x.Id == invoiceId);
        Assert.Equal(1_181_188, invoice.TongTien);
        Assert.Equal(4, invoice.ChiTiet.Count);
        Assert.Empty(await verify.ThanhToans.ToListAsync());
        using var check = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true
        }.ToString());
        await check.OpenAsync();
        using var foreignKeys = check.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check";
        using var reader = await foreignKeys.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }
}
