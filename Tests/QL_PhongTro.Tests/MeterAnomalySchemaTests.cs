using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    [Fact] public async Task UpgradeV17PreservesMeterRowsDefaultFalseAndReruns()
    {
        MeterFixture();Assert.Empty(await Save(SaveInput(1,13)));
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("ALTER TABLE chi_so_dien_nuoc DROP COLUMN da_xac_nhan_bat_thuong;DELETE FROM app_schema_version WHERE version>=18;");
        var before=DatabaseSnapshot();
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(new[]{false,false},await ConfirmationFlags());
        using(var c=Open())using(var cmd=c.CreateCommand())
        {
            cmd.CommandText="SELECT max(version) FROM app_schema_version";Assert.Equal(40L,cmd.ExecuteScalar());
            cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());
            cmd.CommandText="PRAGMA foreign_key_check";using(var r=cmd.ExecuteReader())Assert.False(r.Read());
        }
        var after=DatabaseSnapshot();DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));Assert.Equal(after,DatabaseSnapshot());
        Assert.Throws<SqliteException>(()=>Execute("UPDATE chi_so_dien_nuoc SET da_xac_nhan_bat_thuong=2"));
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("ALTER TABLE chi_so_dien_nuoc DROP COLUMN da_xac_nhan_bat_thuong;DELETE FROM app_schema_version WHERE version>=18;");
        Assert.Equal(before,DatabaseSnapshot());
    }
    [Fact] public void UpgradeRecoversEarlierMarkerWithExactV18Protections()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DELETE FROM app_schema_version WHERE version>=17;");
        var before=DatabaseSnapshot(true);
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,DatabaseSnapshot(true));DatabaseUpdates.Check(path);
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DELETE FROM app_schema_version WHERE version>=17;DROP TRIGGER meter_locked_update;");
        Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json")));
    }
    [Fact] public void UpgradeV18RecoversMissingMarkerButRejectsIncompatibleColumn()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DELETE FROM app_schema_version WHERE version>=18;");DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        DatabaseUpdates.Check(path);
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DELETE FROM app_schema_version WHERE version>=18;ALTER TABLE chi_so_dien_nuoc DROP COLUMN da_xac_nhan_bat_thuong;ALTER TABLE chi_so_dien_nuoc ADD COLUMN da_xac_nhan_bat_thuong TEXT;");
        var before=DatabaseSnapshot();Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json")));Assert.Equal(before,DatabaseSnapshot());
    }
}
