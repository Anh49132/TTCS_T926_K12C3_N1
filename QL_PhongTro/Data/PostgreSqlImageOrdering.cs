using Npgsql;

namespace QL_PhongTro.Data;

public static class PostgreSqlImageOrdering
{
    public static async Task ApplyAsync(DbConnection connection, DbTransaction transaction, int roomId, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (connection is not NpgsqlConnection || transaction.Connection != connection || ids.Count > 8 || ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("Invalid image reorder transaction.");
        await using var command = connection.CreatePortableCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE anh_phong SET thu_tu=array_position(@ids,id) WHERE phong_id=@room AND id=ANY(@ids)";
        command.Parameters.AddWithValue("ids", ids.ToArray()); command.Parameters.AddWithValue("room", roomId);
        if (await command.ExecuteNonQueryAsync(ct) != ids.Count) throw new InvalidOperationException("Image list changed.");
    }
}
