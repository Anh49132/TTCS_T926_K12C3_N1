using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public enum RoomImageDeleteStatus
{
    Deleted,
    NotFound,
    StorageFailed
}

public sealed record RoomImageDto(int Id, int Order, string OriginalPath, string? ThumbnailPath);
public sealed record RoomImageDeleteOutcome(RoomImageDeleteStatus Status, string? Message, IReadOnlyList<RoomImageDto> Images);
public sealed record RoomImageRetryResult(int Deleted, int Failed);
public sealed record RoomImageStorageCheckResult(int ExpectedFiles, int MissingFiles, int OrphanFiles, IReadOnlyList<string> Missing, IReadOnlyList<string> Orphans);

public sealed class RoomImageDeletionService(AppDbContext db, RoomImageStore imageStore, ILogger<RoomImageDeletionService> logger)
{
    public async Task<RoomImageDeleteOutcome> DeleteOwnedImageAsync(int roomId, int imageId, int ownerId, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction);

        var roomIsOwned = await db.PhongTros.AsNoTracking().AnyAsync(room => room.Id == roomId
            && db.ToaNhas.Any(building => building.Id == room.ToaNhaId
                && building.ChuNhaId == ownerId && building.DangHoatDong), cancellationToken);
        if (!roomIsOwned)
            return new(RoomImageDeleteStatus.NotFound, null, []);

        var images = await db.AnhPhongs.AsNoTracking()
            .Where(image => image.PhongId == roomId)
            .OrderBy(image => image.ThuTu)
            .ToListAsync(cancellationToken);
        var image = images.FirstOrDefault(item => item.Id == imageId);
        if (image is null)
        {
            if (await db.AnhPhongs.AsNoTracking().AnyAsync(item => item.Id == imageId, cancellationToken))
                return new(RoomImageDeleteStatus.NotFound, null, []);

            return new(RoomImageDeleteStatus.Deleted, null, ToDtos(images));
        }

        await MarkPendingAsync(connection, sqliteTransaction, image.Id, null, cancellationToken);
        var deletion = imageStore.DeletePermanent(ToStored(image));
        if (!deletion.Success)
        {
            await MarkPendingAsync(connection, sqliteTransaction, image.Id, deletion.ErrorMessage, cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
            logger.LogWarning("Room image {ImageId} is pending delete because storage deletion failed: {Error}", image.Id, deletion.ErrorMessage);
            return new(RoomImageDeleteStatus.StorageFailed,
                "Không xoá được tệp ảnh trên kho lưu trữ. Ảnh đã được giữ lại để thử xoá tự động sau.",
                ToDtos(images));
        }

        var remainingIds = images.Where(item => item.Id != image.Id).Select(item => item.Id).ToList();
        await DeleteRowAndReorderAsync(connection, sqliteTransaction, roomId, image.Id, remainingIds, cancellationToken);
        await transaction!.CommitAsync(cancellationToken);

        var updated = images.Where(item => item.Id != image.Id)
            .Select((item, index) => new RoomImageDto(item.Id, index + 1, item.DuongDan, item.DuongDanAnhNho))
            .ToList();
        return new(RoomImageDeleteStatus.Deleted, null, updated);
    }

    public async Task<RoomImageRetryResult> RetryPendingDeletesAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var pending = await db.AnhPhongs.AsNoTracking()
            .Where(image => image.DangChoXoa)
            .OrderBy(image => image.LanThuXoaGanNhat ?? DateTime.MinValue)
            .ThenBy(image => image.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var deleted = 0;
        var failed = 0;
        foreach (var image in pending)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            var connection = (SqliteConnection)db.Database.GetDbConnection();
            using var sqliteTransaction = connection.BeginTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqliteTransaction);
            var current = await db.AnhPhongs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == image.Id, cancellationToken);
            if (current is null)
                continue;

            var deletion = imageStore.DeletePermanent(ToStored(current));
            if (!deletion.Success)
            {
                await MarkPendingAsync(connection, sqliteTransaction, current.Id, deletion.ErrorMessage, cancellationToken);
                await transaction!.CommitAsync(cancellationToken);
                failed++;
                logger.LogWarning("Retry for room image {ImageId} failed: {Error}", current.Id, deletion.ErrorMessage);
                continue;
            }

            var remainingIds = await db.AnhPhongs.AsNoTracking()
                .Where(item => item.PhongId == current.PhongId && item.Id != current.Id)
                .OrderBy(item => item.ThuTu)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            await DeleteRowAndReorderAsync(connection, sqliteTransaction, current.PhongId, current.Id, remainingIds, cancellationToken);
            await transaction!.CommitAsync(cancellationToken);
            deleted++;
        }

        return new(deleted, failed);
    }

    public async Task<RoomImageStorageCheckResult> CheckStorageAsync(CancellationToken cancellationToken = default)
    {
        var images = await db.AnhPhongs.AsNoTracking().ToListAsync(cancellationToken);
        var expected = images.SelectMany(image => new[] { image.DuongDan, image.DuongDanAnhNho })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => imageStore.TryGetFullPath(path, out var fullPath) ? fullPath : null)
            .Where(path => path is not null)
            .Select(path => path!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actual = imageStore.EnumerateStoredFiles().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expected.Where(path => !File.Exists(path)).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var orphans = actual.Where(path => !expected.Contains(path)).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return new(expected.Count, missing.Count, orphans.Count, missing, orphans);
    }

    private static StoredRoomImage ToStored(AnhPhong image) => new(image.DuongDan, image.DuongDanAnhNho ?? string.Empty);

    private static IReadOnlyList<RoomImageDto> ToDtos(IEnumerable<AnhPhong> images) =>
        images.OrderBy(image => image.ThuTu)
            .Select((image, index) => new RoomImageDto(image.Id, index + 1, image.DuongDan, image.DuongDanAnhNho))
            .ToList();

    private static async Task MarkPendingAsync(SqliteConnection connection, SqliteTransaction transaction, int imageId, string? error, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE anh_phong
            SET dang_cho_xoa=1, loi_xoa_gan_nhat=$error, lan_thu_xoa_gan_nhat=$triedAt
            WHERE id=$imageId
            """;
        command.Parameters.AddWithValue("$imageId", imageId);
        command.Parameters.AddWithValue("$error", (object?)Truncate(error, 1000) ?? DBNull.Value);
        command.Parameters.AddWithValue("$triedAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteRowAndReorderAsync(SqliteConnection connection, SqliteTransaction transaction,
        int roomId, int imageId, IReadOnlyList<int> remainingIds, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM anh_phong WHERE id=$imageId AND phong_id=$roomId";
        command.Parameters.AddWithValue("$imageId", imageId);
        command.Parameters.AddWithValue("$roomId", roomId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.Parameters.Clear();
        try
        {
            command.CommandText = "PRAGMA ignore_check_constraints=ON";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "UPDATE anh_phong SET thu_tu=thu_tu+100 WHERE phong_id=$roomId";
            command.Parameters.AddWithValue("$roomId", roomId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.Parameters.Clear();
            command.CommandText = "UPDATE anh_phong SET thu_tu=$order WHERE id=$imageId AND phong_id=$roomId";
            for (var index = 0; index < remainingIds.Count; index++)
            {
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$order", index + 1);
                command.Parameters.AddWithValue("$imageId", remainingIds[index]);
                command.Parameters.AddWithValue("$roomId", roomId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = "PRAGMA ignore_check_constraints=OFF";
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
