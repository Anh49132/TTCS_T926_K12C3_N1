using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class DichVuThamChieu(AppDbContext db)
{
    // Called within the same write transaction as delete/initial-price edits.
    // Legacy contract snapshots are opaque; unknown formats prevent destructive operations.
    public async Task<string?> LyDoKhongDuocXoaAsync(int buildingId, int serviceId)
    {
        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync();
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            var tables = new HashSet<string>();
            using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            async Task<bool> Exists(string sql)
            {
                command.CommandText = sql; command.Parameters.Clear();
                foreach (var (name, value) in new[] { ("$service", serviceId), ("$building", buildingId) })
                { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
                return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
            }
            if (tables.Contains("chi_tiet_hoa_don") && await Exists("""
                SELECT COUNT(*) FROM chi_tiet_hoa_don d JOIN hoa_don hd ON hd.id=d.hoa_don_id
                JOIN hop_dong h ON h.id=hd.hop_dong_id JOIN phong_tro p ON p.id=h.phong_id
                WHERE p.toa_nha_id=$building AND (d.dich_vu_id=$service
                OR d.cau_hinh_dich_vu_id IN (SELECT id FROM cau_hinh_dich_vu WHERE dich_vu_id=$service AND toa_nha_id=$building))
                """)) return "Dịch vụ đã được sử dụng trong hóa đơn. Bạn có thể ngừng áp dụng thay vì xóa hoặc ghi đè giá.";
            if (tables.Contains("hop_dong_dich_vu") && await Exists("SELECT COUNT(*) FROM hop_dong_dich_vu d JOIN hop_dong h ON h.id=d.hop_dong_id JOIN phong_tro p ON p.id=h.phong_id WHERE d.dich_vu_id=$service AND p.toa_nha_id=$building"))
                return "Dịch vụ đã được sử dụng trong hợp đồng. Bạn có thể ngừng áp dụng thay vì xóa hoặc ghi đè giá.";
            if (tables.Contains("hop_dong"))
            {
                if (!tables.Contains("ky_hop_dong"))
                    return "Chưa đủ dữ liệu để kiểm tra dịch vụ trong hợp đồng cũ; chưa thể xóa hoặc ghi đè giá.";
                command.CommandText = """
                    SELECT k.thong_tin_chot FROM ky_hop_dong k JOIN hop_dong h ON h.id=k.hop_dong_id
                    JOIN phong_tro p ON p.id=h.phong_id WHERE p.toa_nha_id=$building
                    AND k.thong_tin_chot IS NOT NULL AND trim(k.thong_tin_chot)<>''
                    """;
                command.Parameters.Clear();
                var buildingParameter = command.CreateParameter(); buildingParameter.ParameterName = "$building";
                buildingParameter.Value = buildingId; command.Parameters.Add(buildingParameter);
                var snapshots = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) snapshots.Add(reader.GetString(0));
                foreach (var snapshot in snapshots)
                {
                    try
                    {
                        using var json = JsonDocument.Parse(snapshot);
                        // Recognise an explicit service-ID list; do not guess references from free-form text.
                        if (json.RootElement.ValueKind == JsonValueKind.Object &&
                            json.RootElement.TryGetProperty("dich_vu", out var services) && services.ValueKind == JsonValueKind.Array)
                        {
                            var understood = true;
                            foreach (var item in services.EnumerateArray())
                            {
                                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("dich_vu_id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var value) || value <= 0)
                                { understood = false; break; }
                                if (value == serviceId) return "Dịch vụ có trong thông tin hợp đồng đã chốt; không thể xóa hoặc ghi đè giá.";
                            }
                            if (understood) continue;
                        }
                        return "Hợp đồng cũ có thông tin dịch vụ đã chốt. Cần đối chiếu trước khi xóa hoặc ghi đè giá; vẫn có thể ngừng áp dụng.";
                    }
                    catch (JsonException) { return "Thông tin hợp đồng cũ chưa đọc được; không thể xác nhận dịch vụ chưa được tham chiếu."; }
                }
            }
            return null;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return "Schema hợp đồng/hóa đơn chưa tương thích; chưa thể xác minh an toàn để xóa hoặc ghi đè giá.";
        }
        finally { if (close) await connection.CloseAsync(); }
    }
}
