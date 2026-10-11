using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Data;

public static class RoomDetailsSchema
{
    public static void Upgrade(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        foreach (var (name, definition) in new[] {
            ("ghi_chu_noi_bo", "TEXT CHECK(ghi_chu_noi_bo IS NULL OR length(ghi_chu_noi_bo)<=500)"),
            ("tien_nghi", "TEXT CHECK(tien_nghi IS NULL OR (json_valid(tien_nghi) AND json_type(tien_nghi)='array'))") })
        {
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('phong_tro') WHERE name=$name";
            command.Parameters.Clear();command.Parameters.AddWithValue("$name",name);
            if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                throw new InvalidOperationException("Cột " + name + " đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema.");
            command.Parameters.Clear();command.CommandText = $"ALTER TABLE phong_tro ADD COLUMN {name} {definition}";
            command.ExecuteNonQuery();
        }
        command.CommandText = "INSERT INTO app_schema_version(version,applied_at) VALUES(24,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";
        command.ExecuteNonQuery();transaction.Commit();
    }
}
