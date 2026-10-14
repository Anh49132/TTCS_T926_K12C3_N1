using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using QL_PhongTro.Models;

namespace QL_PhongTro.Data;

public partial class AppDbContext
{
    // Server-only intent for the one operation that changes a secret but no public business field.
    internal int? TemporaryPasswordResentFor { get; set; }
    internal TaiKhoan? SelfRegisteringAccount { get; set; }
    private bool auditFaulted;

    // Explicit allowlist: never serialize entities, navigation properties or arbitrary request data.
    private static readonly IReadOnlyDictionary<Type, string[]> AuditFields = new Dictionary<Type, string[]>
    {
        [typeof(BangTatToan)] = ["HopDongId", "HoaDonCuoiId", "NgayTraPhong", "CocConGiu", "NoHoaDonCuoi", "CongNoCu", "TienHuHong", "TienPhaiHoan", "TienKhachTraThem", "TrangThai", "PhienBan"],
        [typeof(KhauTruTatToan)] = ["ThanhLyId", "SoThuTu", "SoTien"],
        [typeof(GhiChuPhong)] = ["PhongId", "NguoiTaoId", "Ghim", "NgayTao"],
        [typeof(ChiSoDienNuoc)] = ["HopDongId", "DichVuId", "TuNgay", "DenNgay", "ChiSoDau", "ChiSoCuoi", "DaXacNhanBatThuong", "DaKhoa", "PhienBan"],
        [typeof(NguoiOGhep)] = ["HopDongId", "KhachThueId", "NgayVao", "NgayRa"],
        [typeof(HopDongChiSoDauKy)] = ["HopDongId", "NgayBanGiao", "ChiSoDien", "ChiSoNuoc"],
        [typeof(HopDongThamChieu)] = ["MaHopDong", "PhongId", "KhachDungTenId", "YeuCauThueId", "TienCoc", "NgayChot", "TrangThai"],
        [typeof(KyHopDongThamChieu)] = ["HopDongId", "NgayBatDau", "NgayKetThuc", "GiaThue", "SoThang"],
        [typeof(YeuCauThue)] = ["MaYeuCau", "TinDangId", "KhachThueId", "LoaiYeuCau", "NgayMongMuon", "SoNguoiDuKien", "TrangThai"],
        [typeof(TaiKhoan)] = ["HoTen", "VaiTro", "DangHoatDong", "MustChangePassword", "IsDeleted"],
        [typeof(ToaNha)] = ["ChuNhaId", "QuanLyId", "TenToaNha", "DiaChi", "PhuongXa", "QuanHuyen", "TinhThanh", "SoTang", "DienTichDat", "ThangMay", "BaiDoXe", "CameraAnNinh", "BaoVe24h", "KhuGiatSay", "SanThuong", "NgayChotHangThang", "DangHoatDong"],
        [typeof(PhongTro)] = ["ToaNhaId", "MaPhong", "Tang", "LoaiPhong", "DienTich", "GiaThue", "TienCocDuKien", "SoNguoiToiDa", "TrangThai", "TienNghi"],
        [typeof(DichVuToaNha)] = ["ToaNhaId", "DichVuId", "ApDungMacDinh"],
        [typeof(NgungDichVuPhong)] = ["DichVuPhongId", "YeuCauLucUtc", "NgungTuKy", "ApDungLaiTuKy", "ApDungLaiLucUtc"],
        [typeof(DichVuPhong)] = ["PhongId", "DichVuToaNhaId", "DonGiaRieng"],
        [typeof(DichVu)] = ["MaDichVu", "TenDichVu", "DangHoatDong"],
        [typeof(CauHinhDichVu)] = ["ToaNhaId", "PhongId", "DichVuId", "CachTinh", "DonViTinh", "DonGia", "TuNgay", "DenNgay", "DangApDung", "DaChotGia"],
        [typeof(HopDongDichVu)] = ["HopDongId", "DichVuId", "CauHinhDichVuId", "TenDichVu", "CachTinh", "DonViTinh", "DonGia"],
        [typeof(HoaDon)] = ["MaHoaDon", "HopDongId", "Thang", "Nam", "TuNgay", "DenNgay", "NgayChot", "SoNguoiTinhPhi", "LoaiHoaDon", "HanThanhToan", "TongTien", "TrangThai", "NgayPhatHanh", "NgayPhatHanhNghiepVu", "ThayTheHoaDonId", "NguoiHuyId", "NgayHuy", "LyDoHuy"],
        [typeof(ChiTietHoaDon)] = ["HoaDonId", "SoThuTu", "DichVuId", "CauHinhDichVuId", "KyHopDongId", "LoaiKhoan", "TenKhoan", "CachTinhApDung", "DonViTinh", "SoLuong", "DonGia", "ChiSoDau", "ChiSoCuoi", "ThanhTien"]
    };

    private sealed record PendingAudit(EntityEntry Entry, EntityState State, string[] Fields, string? Before, string Action);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (auditFaulted) throw new InvalidOperationException("Discard this context after a failed audited save.");
        ChangeTracker.DetectChanges();
        var pending = new List<PendingAudit>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is NhatKyHoatDong) throw new InvalidOperationException("Nhật ký chỉ được ghi bởi hệ thống và không thể sửa/xóa.");
            if (!AuditFields.TryGetValue(entry.Metadata.ClrType, out var allowed)) continue;
            var fields = entry.State == EntityState.Modified
                ? allowed.Where(f => entry.Property(f).IsModified && !Equals(entry.Property(f).OriginalValue, entry.Property(f).CurrentValue)).ToArray()
                : allowed;
            var resend = entry.Entity is TaiKhoan account && TemporaryPasswordResentFor == account.Id;
            if (fields.Length == 0 && !resend) continue;
            var action = entry.State switch { EntityState.Added => "TAO", EntityState.Deleted => "XOA", _ => "SUA" };
            if (resend) action = "GUI_LAI_MAT_KHAU_TAM";
            else if (entry.Entity is TaiKhoan && fields.Contains("DangHoatDong") && entry.State == EntityState.Modified)
                action = (bool)entry.Property("DangHoatDong").CurrentValue! ? "MO_KHOA" : "KHOA";
            else if (entry.State == EntityState.Modified && fields.Any(f => f is "TrangThai" or "DangHoatDong" or "DangApDung")) action = "DOI_TRANG_THAI";
            pending.Add(new(entry, entry.State, fields, entry.State == EntityState.Added ? null : Snapshot(entry, fields, true), action));
        }
        if (pending.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        var owned = Database.CurrentTransaction is null;
        await using var ownTransaction = owned ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var transaction = Database.CurrentTransaction!;
        var savepoint = "audit_" + Guid.NewGuid().ToString("N");
        if (!owned) await transaction.CreateSavepointAsync(savepoint, cancellationToken);
        try
        {
            TaiKhoan? actor = null;
            var principal = httpContext?.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
                actor = await TaiKhoans.AsNoTracking().SingleOrDefaultAsync(a => a.Id == actorId, cancellationToken);
            var selfRegistration = actor is null && principal?.Identity?.IsAuthenticated != true &&
                pending.Count == 1 && pending[0].State == EntityState.Added &&
                ReferenceEquals(pending[0].Entry.Entity, SelfRegisteringAccount) && SelfRegisteringAccount?.VaiTro == "KHACH_THUE";
            if (!selfRegistration && (actor is null || !actor.DangHoatDong))
                throw new InvalidOperationException("Không xác minh được tài khoản thực hiện để ghi nhật ký.");
            var count = await base.SaveChangesAsync(false, cancellationToken);
            if (selfRegistration)
            {
                var registeredId = Convert.ToInt32(pending[0].Entry.Property("Id").CurrentValue);
                actor = await TaiKhoans.AsNoTracking().SingleAsync(a => a.Id == registeredId, cancellationToken);
            }
            foreach (var item in pending)
            {
                var table = item.Entry.Metadata.GetTableName()!;
                var id = Convert.ToInt32(item.Entry.Property("Id").CurrentValue);
                var after = item.State == EntityState.Deleted ? null : Snapshot(item.Entry, item.Fields, false);
                var now = DateTime.UtcNow;
                await Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES ({actor!.Id},{actor.HoTen},{actor.VaiTro},{table},{id},{item.Action},{item.Before},{after},{now})", cancellationToken);
            }
            if (owned) await transaction.CommitAsync(cancellationToken);
            else await transaction.ReleaseSavepointAsync(savepoint, cancellationToken);
            if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
            TemporaryPasswordResentFor = null;
            SelfRegisteringAccount = null;
            return count;
        }
        catch
        {
            auditFaulted = true;
            if (owned) await transaction.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
            throw;
        }
    }

    private static string Snapshot(EntityEntry entry, IEnumerable<string> fields, bool original)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var store = StoreObjectIdentifier.Table(entry.Metadata.GetTableName()!, entry.Metadata.GetSchema());
        foreach (var name in fields)
        {
            var property = entry.Property(name);
            values[property.Metadata.GetColumnName(store)!] = original ? property.OriginalValue : property.CurrentValue;
        }
        return JsonSerializer.Serialize(values);
    }
}
