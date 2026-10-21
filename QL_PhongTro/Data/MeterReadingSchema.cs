using Microsoft.Data.Sqlite;
using System.Text.RegularExpressions;
namespace QL_PhongTro.Data;

public static class MeterReadingSchema
{
    public static void Upgrade(SqliteConnection connection, bool recordVersion = true)
    {
        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        const string schema = """
            CREATE TABLE chi_so_dien_nuoc (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
                dich_vu_id INTEGER NOT NULL REFERENCES dich_vu(id) ON DELETE RESTRICT,
                tu_ngay TEXT NOT NULL CHECK(date(tu_ngay) IS NOT NULL AND tu_ngay=date(tu_ngay,'start of month')),
                den_ngay TEXT NOT NULL CHECK(date(den_ngay) IS NOT NULL AND den_ngay=date(tu_ngay,'+1 month','-1 day')),
                chi_so_dau TEXT NOT NULL CHECK(length(chi_so_dau)>0 AND chi_so_dau GLOB '[0-9]*' AND chi_so_dau NOT GLOB '*[^0-9.]*'
                    AND length(chi_so_dau)-length(replace(chi_so_dau,'.',''))<=1
                    AND (instr(chi_so_dau,'.')=0 OR length(chi_so_dau)-instr(chi_so_dau,'.') BETWEEN 1 AND 3)
                    AND CAST(chi_so_dau AS NUMERIC) BETWEEN 0 AND 99999999999.999),
                chi_so_cuoi TEXT NOT NULL CHECK(length(chi_so_cuoi)>0 AND chi_so_cuoi GLOB '[0-9]*' AND chi_so_cuoi NOT GLOB '*[^0-9.]*'
                    AND length(chi_so_cuoi)-length(replace(chi_so_cuoi,'.',''))<=1
                    AND (instr(chi_so_cuoi,'.')=0 OR length(chi_so_cuoi)-instr(chi_so_cuoi,'.') BETWEEN 1 AND 3)
                    AND CAST(chi_so_cuoi AS NUMERIC) BETWEEN CAST(chi_so_dau AS NUMERIC) AND 99999999999.999),
                nguoi_nhap_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                ngay_nhap TEXT NOT NULL,
                da_khoa INTEGER NOT NULL DEFAULT 0 CHECK(da_khoa IN (0,1)),
                phien_ban INTEGER NOT NULL DEFAULT 0 CHECK(phien_ban>=0),
                UNIQUE(hop_dong_id,dich_vu_id,tu_ngay,den_ngay));
            CREATE TRIGGER meter_service_insert BEFORE INSERT ON chi_so_dien_nuoc
            WHEN NOT EXISTS(SELECT 1 FROM dich_vu WHERE id=NEW.dich_vu_id AND ma_dich_vu IN ('DIEN','NUOC'))
            BEGIN SELECT RAISE(ABORT,'meter service required'); END;
            CREATE TRIGGER meter_service_update BEFORE UPDATE ON chi_so_dien_nuoc
            WHEN NOT EXISTS(SELECT 1 FROM dich_vu WHERE id=NEW.dich_vu_id AND ma_dich_vu IN ('DIEN','NUOC'))
            BEGIN SELECT RAISE(ABORT,'meter service required'); END;
            CREATE TRIGGER meter_locked_update BEFORE UPDATE ON chi_so_dien_nuoc WHEN OLD.da_khoa=1
            BEGIN SELECT RAISE(ABORT,'locked meter reading'); END;
            CREATE TRIGGER meter_locked_delete BEFORE DELETE ON chi_so_dien_nuoc WHEN OLD.da_khoa=1
            BEGIN SELECT RAISE(ABORT,'locked meter reading'); END;
            """;
        cmd.CommandText="SELECT sql FROM sqlite_master WHERE type='table' AND name='chi_so_dien_nuoc'";
        var existing=cmd.ExecuteScalar()?.ToString();
        static string Normalize(string sql)=>Regex.Replace(sql.Trim().TrimEnd(';'),@"\s+"," ");
        if(existing==null){cmd.CommandText=schema;cmd.ExecuteNonQuery();}
        else
        {
            // Reruns may encounter a completed additive step whose version marker
            // was not retained. Adopt only the exact known table and protections.
            // A later additive v18 step may remain when an earlier version marker
            // is missing. Strip only its exact known column; still verify v17
            // table constraints and every protection below before adoption.
            var original = Regex.Replace(existing,
                @",\s*da_xac_nhan_bat_thuong INTEGER NOT NULL DEFAULT 0 CHECK\(da_xac_nhan_bat_thuong IN \(0,1\)\)", "");
            if(Normalize(original)!=Normalize(schema[..schema.IndexOf(';')]))
                throw new InvalidOperationException("Schema chi_so_dien_nuoc không khớp v17; không thay đổi bảng hiện có.");
            foreach(Match match in Regex.Matches(schema,@"CREATE TRIGGER\s+(\w+).*?END;",RegexOptions.Singleline))
            {
                cmd.CommandText="SELECT sql FROM sqlite_master WHERE type='trigger' AND name=$name";
                cmd.Parameters.Clear();cmd.Parameters.AddWithValue("$name",match.Groups[1].Value);
                var actual=cmd.ExecuteScalar()?.ToString();
                if(actual==null || Normalize(actual)!=Normalize(match.Value))
                    throw new InvalidOperationException("Trigger chỉ số không khớp v17; cần kiểm tra trước khi nâng cấp.");
            }
            cmd.Parameters.Clear();
        }
        cmd.CommandText="PRAGMA integrity_check";
        if (cmd.ExecuteScalar()?.ToString() != "ok") throw new InvalidOperationException("Meter schema integrity check failed.");
        cmd.CommandText="PRAGMA foreign_key_check";
        using(var reader=cmd.ExecuteReader()) if(reader.Read()) throw new InvalidOperationException("Meter schema foreign key check failed.");
        if (recordVersion) cmd.CommandText="INSERT INTO app_schema_version(version,applied_at) VALUES(17,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        if (recordVersion) cmd.ExecuteNonQuery(); tx.Commit();
    }
}
