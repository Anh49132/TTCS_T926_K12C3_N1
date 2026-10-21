using Microsoft.Data.Sqlite;
using System.Text.Json;
using QL_PhongTro.Data;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class MeterReadingListTests
{
    private string DatabaseSnapshot(bool excludeNew=false)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        var tables=new List<string>();using(var reader=cmd.ExecuteReader())while(reader.Read())tables.Add(reader.GetString(0));
        var result=new List<string>();
        foreach(var table in tables.Where(x=>!excludeNew || x!="chi_so_dien_nuoc"))
        {
            cmd.CommandText="SELECT * FROM \""+table+"\" ORDER BY rowid";using var reader=cmd.ExecuteReader();
            while(reader.Read()) {var values=new object[reader.FieldCount];reader.GetValues(values);
                if(excludeNew && table=="app_schema_version" && Convert.ToInt32(values[0])>=17)continue;
                if(excludeNew && table=="phong_tro") values=values.Where((_, i)=>reader.GetName(i) is not ("ghi_chu_noi_bo" or "tien_nghi")).ToArray();
                result.Add(table+JsonSerializer.Serialize(values));}
        }
        return string.Join("\n",result);
    }
    [Fact] public void UpgradeV16PreservesRowsAndIsIdempotent()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DROP TABLE chi_so_dien_nuoc;DELETE FROM app_schema_version WHERE version>=17;");
        var before=DatabaseSnapshot(true);
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,DatabaseSnapshot(true));Assert.Equal(0,Count("chi_so_dien_nuoc"));
        var after=DatabaseSnapshot();DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));Assert.Equal(after,DatabaseSnapshot());
        DatabaseUpdates.Check(path);using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());
        cmd.CommandText="PRAGMA foreign_key_check";using(var reader=cmd.ExecuteReader())Assert.False(reader.Read());
        cmd.CommandText="PRAGMA foreign_key_list(chi_so_dien_nuoc)";using(var reader=cmd.ExecuteReader()){var count=0;while(reader.Read()){Assert.Equal("RESTRICT",reader.GetString(6));count++;}Assert.Equal(3,count);}
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!,Path.GetFileName(path)+".before-update-*.bak"));
    }
    [Fact] public void UpgradeRefusesUnexpectedTableWithoutChangingData()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("DROP TABLE chi_so_dien_nuoc;DELETE FROM app_schema_version WHERE version>=17;CREATE TABLE chi_so_dien_nuoc(id INTEGER);");
        var before=DatabaseSnapshot();Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json")));Assert.Equal(before,DatabaseSnapshot());
    }
    [Fact] public void UpgradeRecognizesExactCompletedStepButRejectsMissingProtections()
    {
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("ALTER TABLE chi_so_dien_nuoc DROP COLUMN da_xac_nhan_bat_thuong;DELETE FROM app_schema_version WHERE version>=17;");var before=DatabaseSnapshot(true);
        DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json"));Assert.Equal(before,DatabaseSnapshot(true));
        SchemaTestFixture.RemoveRoomExtensions(path);
        Execute("ALTER TABLE chi_so_dien_nuoc DROP COLUMN da_xac_nhan_bat_thuong;DELETE FROM app_schema_version WHERE version>=17;DROP TRIGGER meter_locked_update;");
        Assert.Throws<InvalidOperationException>(()=>DatabaseUpdates.Update(path,Path.Combine(app,"Data","permissions.seed.json")));
    }
    [Theory][InlineData("abc")][InlineData("1.2345")][InlineData("-1")][InlineData("100000000000")]
    public void SchemaRejectsInvalidDecimal(string value)
    {Assert.Throws<SqliteException>(()=>Execute($"INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,1,'2026-10-01','2026-10-31','0','{value}',2,'2026-10-07')"));}
    [Fact] public void SchemaUniqueFkMonthAndLockedRestrictions()
    {
        const string insert="INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(1,1,'2026-10-01','2026-10-31','0','1',2,'2026-10-07')";
        Execute(insert);Assert.Throws<SqliteException>(()=>Execute(insert));
        Assert.Throws<SqliteException>(()=>Execute("UPDATE chi_so_dien_nuoc SET hop_dong_id=999"));
        Assert.Throws<SqliteException>(()=>Execute("UPDATE chi_so_dien_nuoc SET den_ngay='2026-10-30'"));
        Assert.Throws<SqliteException>(()=>Execute("DELETE FROM hop_dong WHERE id=1"));
        Execute("UPDATE chi_so_dien_nuoc SET da_khoa=1");Assert.Throws<SqliteException>(()=>Execute("UPDATE chi_so_dien_nuoc SET chi_so_cuoi='2'"));Assert.Throws<SqliteException>(()=>Execute("DELETE FROM chi_so_dien_nuoc"));
    }
}
