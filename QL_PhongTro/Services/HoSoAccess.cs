using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public class HoSoAccess(AppDbContext db)
{
    public static string Mask(string? value) => string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= 4 ? new string('*', value.Length)
        : new string('*', value.Length - 4) + value[^4..];

    public async Task<bool> CanReadFullAsync(TaiKhoan viewer, int profileId)
    {
        if (!viewer.DangHoatDong || viewer.IsDeleted) return false;
        if (viewer.VaiTro == "ADMIN") return true;
        return viewer.VaiTro == "CHU_NHA" && (await RelatedProfileIdsAsync(viewer.Id)).Contains(profileId);
    }

    public async Task<List<int>> RelatedProfileIdsAsync(int ownerId)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            if (openedHere) await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='nguoi_o_ghep'";
            var hasRoommates = Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
            // Signed contracts remain a relationship after the tenancy ends.
            // Draft/cancelled contracts and rental requests do not grant access.
            command.CommandText = """
                SELECT DISTINCT h.khach_dung_ten_id FROM hop_dong h
                JOIN phong_tro p ON p.id = h.phong_id
                JOIN toa_nha t ON t.id = p.toa_nha_id
                WHERE t.chu_nha_id = @viewer
                  AND h.trang_thai IN ('CHO_HIEU_LUC','DANG_HIEU_LUC','DA_KET_THUC')
                """;
            if (hasRoommates) command.CommandText += """

                UNION
                SELECT DISTINCT g.khach_thue_id FROM nguoi_o_ghep g
                JOIN hop_dong h ON h.id = g.hop_dong_id
                JOIN phong_tro p ON p.id = h.phong_id
                JOIN toa_nha t ON t.id = p.toa_nha_id
                WHERE t.chu_nha_id = @viewer
                  AND h.trang_thai IN ('CHO_HIEU_LUC','DANG_HIEU_LUC','DA_KET_THUC')
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@viewer";
            parameter.Value = ownerId;
            command.Parameters.Add(parameter);
            var ids = new List<int>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
            return ids;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { return []; }
        finally { if (openedHere) await connection.CloseAsync(); }
    }
}
