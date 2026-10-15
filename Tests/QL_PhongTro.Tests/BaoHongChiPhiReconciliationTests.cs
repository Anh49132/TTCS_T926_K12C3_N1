using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using System.Text.Json;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class BaoHongChiPhiSchemaTests
{
    [Theory]
    [InlineData("v30")]
    [InlineData("cost31")]
    [InlineData("settlement31")]
    [InlineData("settlement32")]
    [InlineData("settlement33")]
    public void ReconciliationV34PreservesBothPublishedHistoriesAndEveryExistingRow(string source)
    {
        using var f = new Fixture(false); // Synthetic v30 with invoices and reports.
        if (source == "cost31")
        {
            BaoHongChiPhiSchema.Upgrade(f.Db);
            f.History(f.Cost());
        }
        if (source.StartsWith("settlement"))
        {
            TatToanSchema.Upgrade(f.Db);
            f.Execute($"INSERT INTO giao_dich_coc(ma_giao_dich_coc,khoa_chong_trung,hop_dong_id,loai_giao_dich,so_tien,hinh_thuc,trang_thai,nguoi_thuc_hien_id,ngay_giao_dich) VALUES('COC-RECONCILE','reconcile',{f.Contract},'THU_COC',3000000,'TIEN_MAT','DA_XAC_NHAN',{f.Owner},'2026-10-14')");
            if (source != "settlement31") ChamDutHopDongSchema.Upgrade(f.Db);
            if (source == "settlement33") XacNhanTatToanSchema.Upgrade(f.Db);
        }
        var tables = new Dictionary<string,string[]>();
        using (var cmd = f.Db.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0), []);
        }
        foreach (var table in tables.Keys.ToArray())
        {
            using var cmd = f.Db.CreateCommand(); cmd.CommandText = $"SELECT name FROM pragma_table_info('{table}')";
            using var reader = cmd.ExecuteReader(); var columns = new List<string>();
            while (reader.Read()) columns.Add(reader.GetString(0));
            tables[table] = columns.ToArray();
        }
        string Snapshot()
        {
            var rows = new List<string>();
            foreach (var (table, columns) in tables)
            {
                using var cmd = f.Db.CreateCommand();
                cmd.CommandText = "SELECT " + string.Join(",", columns.Select(x => "\"" + x + "\"")) + " FROM \"" + table + "\"" + (table == "app_schema_version" ? " WHERE version<=" + (source=="v30"?30:source=="cost31"?31:int.Parse(source[^2..])) : "") + " ORDER BY rowid";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) { var row = new object[reader.FieldCount]; reader.GetValues(row); rows.Add(table + JsonSerializer.Serialize(row)); }
            }
            return JsonSerializer.Serialize(rows);
        }
        var before = Snapshot();
        DatabaseUpdates.Update(f.Path, f.Seed);
        Assert.Equal(before, Snapshot());
        Assert.Equal(34, f.Scalar("SELECT MAX(version) FROM app_schema_version"));
        DatabaseUpdates.Check(f.Path);
        BaoHongChiPhiSchema.Validate(f.Db); TatToanSchema.Validate(f.Db);
        ChamDutHopDongSchema.Validate(f.Db); XacNhanTatToanSchema.Validate(f.Db);
        // Both modules are usable after either published history.
        if (source != "cost31") f.History(f.Cost());
        Assert.Equal(350000, f.Scalar("SELECT so_tien FROM chi_phi_bao_hong WHERE bao_hong_id=1"));
        using (var cmd = f.Db.CreateCommand())
        {
            cmd.CommandText = "PRAGMA integrity_check"; Assert.Equal("ok", cmd.ExecuteScalar());
            cmd.CommandText = "PRAGMA foreign_key_check"; using var reader = cmd.ExecuteReader(); Assert.False(reader.Read());
        }
        var after = Snapshot();
        var backups = Directory.GetFiles(System.IO.Path.GetDirectoryName(f.Path)!, "test.sqlite.before-update-*.bak").Length;
        DatabaseUpdates.Update(f.Path, f.Seed);
        Assert.Equal(after, Snapshot());
        Assert.Equal(backups, Directory.GetFiles(System.IO.Path.GetDirectoryName(f.Path)!, "test.sqlite.before-update-*.bak").Length);
    }

    [Fact]
    public void ReconciliationRejectsPartialSchemaBeforeInstallingAnotherModule()
    {
        using var f = new Fixture(false);
        TatToanSchema.Upgrade(f.Db);
        f.Execute("CREATE TABLE chi_phi_bao_hong(id INTEGER PRIMARY KEY)");
        Assert.Throws<InvalidOperationException>(() => BranchSchemaReconciliation.Prepare(f.Db));
        Assert.Equal(0, f.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='lich_su_chi_phi_bao_hong'"));
        Assert.Equal(31, f.Scalar("SELECT MAX(version) FROM app_schema_version"));
    }
}
