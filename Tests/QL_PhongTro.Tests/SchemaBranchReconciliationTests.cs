using QL_PhongTro.Data;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class MeterReadingListTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReconcileUi17AndDev18PreservesBusinessRows(bool uiDatabase)
    {
        if (uiDatabase)
        {
            SchemaTestFixture.RemoveRoomExtensions(path);
            Execute("DROP TABLE chi_so_dien_nuoc; DELETE FROM app_schema_version WHERE version >= 17; INSERT INTO app_schema_version(version,applied_at) VALUES(17,'2026-10-08');");
        }
        else
        {
            foreach (var column in new[] { "dien_tich_dat", "thang_may", "bai_do_xe", "camera_an_ninh", "bao_ve_24h", "khu_giat_say", "san_thuong" })
                Execute($"ALTER TABLE toa_nha DROP COLUMN {column}");
            SchemaTestFixture.RemoveRoomExtensions(path);
            Execute("DELETE FROM app_schema_version WHERE version >= 18; INSERT INTO app_schema_version(version,applied_at) VALUES(18,'2026-10-08');");
        }
        var roomCount = Count("phong_tro");
        var contractCount = Count("hop_dong");
        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        DatabaseUpdates.Check(path);
        Assert.Equal(roomCount, Count("phong_tro"));
        Assert.Equal(contractCount, Count("hop_dong"));
        using (var c = Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT MAX(version) FROM app_schema_version";
            Assert.Equal(42L, cmd.ExecuteScalar());
            cmd.CommandText = "PRAGMA integrity_check";
            Assert.Equal("ok", cmd.ExecuteScalar());
            cmd.CommandText = "PRAGMA foreign_key_check";
            using var reader = cmd.ExecuteReader();
            Assert.False(reader.Read());
        }
        var snapshot = DatabaseSnapshot();
        DatabaseUpdates.Update(path, Path.Combine(app, "Data", "permissions.seed.json"));
        Assert.Equal(snapshot, DatabaseSnapshot());
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".before-update-*.bak"));
    }
}
