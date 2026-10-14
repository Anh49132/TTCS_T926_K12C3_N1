using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed class BaoHongConflictException : Exception
{
    public BaoHongConflictException() : base("Báo hỏng đã được cập nhật ở phiên khác. Hãy tải lại trang và chọn trạng thái phù hợp.") { }
}

public sealed class QuanLyBaoHongService(AppDbContext db, ITimeProvider clock, BaoHongService tenantService)
{
    public static List<string> TrangThaiTiepTheo(string current) => current switch
    {
        "MOI" => ["DANG_XU_LY", "TU_CHOI"],
        "DANG_XU_LY" => ["HOAN_THANH"],
        _ => []
    };

    private async Task<(TaiKhoan Actor, bool CanWrite)> ActorAsync(int id, bool write, CancellationToken ct)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VaiTro == "QUAN_LY"
            && x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword, ct);
        var level = await db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == "QUAN_LY" && x.ModuleCode == "BAO_HONG")
            .Select(x => x.AccessLevel).SingleOrDefaultAsync(ct);
        if (actor is null || (write ? level is not ("WRITE" or "FULL") : level is not ("READ" or "WRITE" or "FULL")))
            throw new UnauthorizedAccessException();
        return (actor, level is "WRITE" or "FULL");
    }

    private IQueryable<BaoHongChiTiet> Rows(int managerId) =>
        from b in db.BaoHongs.AsNoTracking()
        join p in db.PhongTros on b.PhongId equals p.Id
        join t in db.ToaNhas on p.ToaNhaId equals t.Id where t.QuanLyId == managerId
        join h in db.HopDongs on b.HopDongId equals h.Id
        join a in db.TaiKhoans on b.NguoiBaoId equals a.Id
        select new BaoHongChiTiet { BaoHong = b, MaPhong = p.MaPhong, TenToaNha = t.TenToaNha, MaHopDong = h.MaHopDong, TenNguoiBao = a.HoTen };

    private async Task<BaoHongChiTiet?> ScopedAsync(int managerId, int id, CancellationToken ct)
    {
        var result = await Rows(managerId).SingleOrDefaultAsync(x => x.BaoHong.Id == id, ct);
        if (result is null && await db.BaoHongs.AnyAsync(x => x.Id == id, ct)) throw new UnauthorizedAccessException();
        return result;
    }

    public async Task<List<BaoHongChiTiet>> DanhSachAsync(int managerId, int page, CancellationToken ct = default)
    {
        await ActorAsync(managerId, false, ct);
        var rows = await Rows(managerId).OrderByDescending(x => x.BaoHong.Id)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 20).Take(21).ToListAsync(ct);
        var now = clock.UtcNow;
        foreach (var row in rows) row.CanhBaoQuaHan = BaoHongCanhBaoService.DanhGia(row.BaoHong, now);
        return rows;
    }

    public async Task<BaoHongChiTiet?> ChiTietAsync(int managerId, int id, CancellationToken ct = default)
    {
        var actor = await ActorAsync(managerId, false, ct);
        var result = await ScopedAsync(managerId, id, ct);
        if (result is null) return null;
        await tenantService.LoadExtrasAsync(result, ct);
        result.CanhBaoQuaHan = BaoHongCanhBaoService.DanhGia(result.BaoHong, clock.UtcNow);
        result.LaQuanLy = true; result.ChoPhepXuLy = actor.CanWrite;
        result.TrangThaiTiepTheo = TrangThaiTiepTheo(result.BaoHong.TrangThai);
        result.CapNhat.PhienBan = result.BaoHong.PhienBan;
        return result;
    }

    public Task<BaoHongTraoDoi> PhanHoiAsync(int managerId, int id, BaoHongPhanHoiInput input, CancellationToken ct = default) =>
        WriteAsync(managerId, id, "PHAN_HOI", input.NoiDung, null, null, input.KhoaChongTrung, ct);

    public Task<BaoHongTraoDoi> CapNhatAsync(int managerId, int id, BaoHongTrangThaiInput input, CancellationToken ct = default) =>
        WriteAsync(managerId, id, "TRANG_THAI", input.GhiChu, input.TrangThai, input.PhienBan, input.KhoaChongTrung, ct);

    private async Task<BaoHongTraoDoi> WriteAsync(int managerId, int id, string kind, string? text,
        string? target, int? version, string key, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        // Recheck live role, module permission and current assignment inside the writer transaction.
        var actor = (await ActorAsync(managerId, true, ct)).Actor;
        var report = (await ScopedAsync(managerId, id, ct))?.BaoHong ?? throw new KeyNotFoundException();
        var content = text?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 5000)
            throw new BaoHongValidationException(kind == "PHAN_HOI" ? "NoiDung" : "GhiChu", "Nhập nội dung có ký tự, tối đa 5.000 ký tự.");
        if (!Guid.TryParseExact(key, "N", out _)) throw new BaoHongValidationException("", "Phiên gửi không hợp lệ. Hãy mở lại trang.");
        var existing = await db.BaoHongTraoDois.AsNoTracking().SingleOrDefaultAsync(x => x.KhoaChongTrung == key, ct);
        if (existing is not null)
        {
            if (existing.NguoiGuiId != managerId || existing.BaoHongId != id) throw new UnauthorizedAccessException();
            if (existing.Loai != kind || existing.NoiDung != content || existing.TrangThaiSau != target)
                throw new BaoHongValidationException("", "Phiên gửi đã dùng cho nội dung khác. Hãy kiểm tra lịch sử hoặc mở lại trang.");
            await tx!.CommitAsync(ct); return existing;
        }
        if (kind == "TRANG_THAI")
        {
            if (version is null || version < 0) throw new BaoHongValidationException("PhienBan", "Phiên bản không hợp lệ.");
            if (version != report.PhienBan) throw new BaoHongConflictException();
            if (target is null || !TrangThaiTiepTheo(report.TrangThai).Contains(target))
                throw new BaoHongValidationException("TrangThai", "Không được chuyển sang trạng thái này từ trạng thái hiện tại.");
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bao_hong SET trang_thai={target},phien_ban=phien_ban+1 WHERE id={id} AND phien_ban={version}", ct);
            if (changed != 1) throw new BaoHongConflictException();
        }
        var now = clock.UtcNow;
        var item = new BaoHongTraoDoi { BaoHongId = id, NguoiGuiId = actor.Id, TenNguoiGui = actor.HoTen,
            Loai = kind, NoiDung = content, TrangThaiTruoc = kind == "TRANG_THAI" ? report.TrangThai : null,
            TrangThaiSau = target, ThoiDiem = now, KhoaChongTrung = key };
        db.BaoHongTraoDois.Add(item); await db.SaveChangesAsync(ct);
        // Free text belongs in the private history only, never in the global audit payload.
        var action = kind == "TRANG_THAI" ? "DOI_TRANG_THAI" : "PHAN_HOI";
        var metadata = JsonSerializer.Serialize(new { TraoDoiId = item.Id, item.Loai, item.TrangThaiTruoc, item.TrangThaiSau });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_sau,thoi_diem) VALUES({actor.Id},{actor.HoTen},{actor.VaiTro},{"bao_hong"},{id},{action},{metadata},{now})", ct);
        await tx!.CommitAsync(ct); return item;
    }
}
