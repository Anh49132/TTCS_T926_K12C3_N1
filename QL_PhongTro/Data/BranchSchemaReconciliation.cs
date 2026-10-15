using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

// v31 was published independently by repair costs and settlement. Never rewrite
// its marker: recognize the actual objects and converge both histories at v34.
public static class BranchSchemaReconciliation
{
    private static int Count(SqliteConnection c, string names)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN (" + names + ")";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public static void Prepare(SqliteConnection c)
    {
        var cost = Count(c, "'chi_phi_bao_hong','lich_su_chi_phi_bao_hong','chi_phi_bao_hong_trang_thai'");
        var settlement = Count(c, "'giao_dich_coc','thanh_ly_hop_dong','chi_tiet_khau_tru'");
        if (cost is not (0 or 3) || settlement is not (0 or 3) || cost + settlement == 0)
            throw new InvalidOperationException("Schema v31 không đầy đủ hoặc không xác định; dừng hòa giải, không ghi đè dữ liệu.");
        // Validate all existing objects before installing anything.
        if (cost == 3) BaoHongChiPhiSchema.Validate(c);
        if (settlement == 3) TatToanSchema.Validate(c);
        if (settlement == 0) TatToanSchema.InstallForReconciliation(c);
    }

    public static void Upgrade(SqliteConnection c)
    {
        Prepare(c);
        if (Count(c, "'chi_phi_bao_hong'") == 0) BaoHongChiPhiSchema.InstallForReconciliation(c);
        BaoHongChiPhiSchema.Validate(c);
        TatToanSchema.Validate(c);
        ChamDutHopDongSchema.Validate(c);
        XacNhanTatToanSchema.Validate(c);
        using var tx = c.BeginTransaction(deferred: false);
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Integrity check thất bại khi hòa giải v34.");
        cmd.CommandText = "PRAGMA foreign_key_check";
        using (var reader = cmd.ExecuteReader())
            if (reader.Read()) throw new InvalidOperationException("Foreign key check thất bại khi hòa giải v34.");
        cmd.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(34,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        cmd.ExecuteNonQuery(); tx.Commit();
    }
}
