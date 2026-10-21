using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task InvoiceDateSchemaUpgradePreservesIssuedInvoicesAndRepeatsWithoutChanges()
    {
        using var db=Context();var f=await BillingAsync(db);
        await Issue(db,await BillInput(db,f.A,f.Service,new(2026,10,1)));
        var before=JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).ToListAsync());
        SchemaTestFixture.RemoveRoomExtensions(path);
        // Synthetic test fixture only: emulate the exact pre-v20 optional invoice schema.
        await db.Database.ExecuteSqlRawAsync("""
            DROP TRIGGER khoa_hoa_don_update;
            DROP TRIGGER hoa_don_thay_the_insert;
            DROP TRIGGER hoa_don_thay_the_update;
            CREATE TRIGGER khoa_hoa_don_update BEFORE UPDATE ON hoa_don WHEN OLD.trang_thai<>'NHAP'
            BEGIN SELECT RAISE(ABORT,'Issued invoice is immutable'); END;
            ALTER TABLE hoa_don DROP COLUMN ngay_phat_hanh_nghiep_vu;
            DELETE FROM app_schema_version WHERE version>=20;
            """);
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        DatabaseUpdates.Check(path);
        Assert.Equal(before,JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).ToListAsync()));
        Assert.All(await db.HoaDons.AsNoTracking().ToListAsync(),x=>Assert.Null(x.NgayPhatHanhNghiepVu));
        Assert.NotEmpty(Directory.GetFiles(folder,"test.sqlite.before-update-*.bak"));
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,JsonSerializer.Serialize(await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).ToListAsync()));
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());
        cmd.CommandText="PRAGMA foreign_key_check";using(var reader=cmd.ExecuteReader()) Assert.False(reader.Read());
        cmd.CommandText="SELECT COUNT(*) FROM app_schema_version WHERE version=20";Assert.Equal(1L,cmd.ExecuteScalar());
    }
}
