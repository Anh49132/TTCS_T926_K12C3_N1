using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Xunit;

public class ContractCounterPortTests
{
    [Theory]
    [InlineData(5, "HD-2026-0013")]
    [InlineData(20, "HD-2026-0021")]
    public async Task Portable_upsert_never_moves_counter_backwards(int previousCounter, string expected)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE hop_dong(ma_hop_dong TEXT); CREATE TABLE hop_dong_so_ma(nam INTEGER PRIMARY KEY,so_cuoi INTEGER NOT NULL); INSERT INTO hop_dong VALUES('HD-2026-0012')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong_so_ma VALUES(2026,{previousCounter})");
        await using var transaction = await db.Database.BeginProtectedTransactionAsync();
        Assert.Equal(expected, await new HopDongService(db).SinhMaAsync(2026, default));
        await transaction.RollbackAsync();
    }
}
