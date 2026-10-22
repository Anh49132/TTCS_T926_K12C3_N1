using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed class BaoHongValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class BaoHongService(AppDbContext db, ITimeProvider clock, BaoHongImageStore? imageStore = null)
{
    private readonly BaoHongImageStore images = imageStore ?? new BaoHongImageStore(db.FileStoragePath + ".repair-images");
    public static string TenTrangThai(string status) => status switch
    { "MOI" => "Mới", "DANG_XU_LY" => "Đang xử lý", "HOAN_THANH" => "Hoàn thành", "TU_CHOI" => "Từ chối", _ => status };
    private async Task<TaiKhoan> ActorAsync(int actorId, bool write, CancellationToken ct)
    {
        var actor = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId &&
            x.VaiTro == "KHACH_THUE" && x.DangHoatDong && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword, ct);
        var level = await db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == "KHACH_THUE" && x.ModuleCode == "BAO_HONG")
            .Select(x => x.AccessLevel).SingleOrDefaultAsync(ct);
        if (actor is null || (write ? level is not ("WRITE" or "FULL") : level is not ("READ" or "WRITE" or "FULL")))
            throw new UnauthorizedAccessException();
        return actor;
    }

    private IQueryable<HopDongThamChieu> Eligible(int actorId, DateOnly today) => db.HopDongs.AsNoTracking()
        .Where(h => h.TrangThai == "DANG_HIEU_LUC" && (h.NgayTraPhong == null || h.NgayTraPhong >= today)
            && db.KyHopDongs.Any(k => k.HopDongId == h.Id && k.NgayBatDau <= today && k.NgayKetThuc >= today)
            && db.KhachThues.Any(p => p.TaiKhoanId == actorId && (p.Id == h.KhachDungTenId ||
                db.NguoiOGheps.Any(g => g.HopDongId == h.Id && g.KhachThueId == p.Id && g.NgayVao <= today &&
                    (g.NgayRa == null || g.NgayRa >= today)))));

    public async Task<List<PhongBaoHong>> PhongsAsync(int actorId, CancellationToken ct = default)
    {
        await ActorAsync(actorId, true, ct);
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        return await (from h in Eligible(actorId, today)
            join p in db.PhongTros on h.PhongId equals p.Id
            join t in db.ToaNhas on p.ToaNhaId equals t.Id
            orderby t.TenToaNha, p.MaPhong, h.Id
            select new PhongBaoHong(h.Id, p.Id, p.MaPhong, t.TenToaNha, h.MaHopDong)).ToListAsync(ct);
    }

    private IQueryable<BaoHongChiTiet> Rows(int actorId) =>
        from b in db.BaoHongs.AsNoTracking() where b.NguoiBaoId == actorId
        join p in db.PhongTros on b.PhongId equals p.Id
        join t in db.ToaNhas on p.ToaNhaId equals t.Id
        join h in db.HopDongs on b.HopDongId equals h.Id
        join a in db.TaiKhoans on b.NguoiBaoId equals a.Id
        select new BaoHongChiTiet { BaoHong = b, ToaNhaId = t.Id, MaPhong = p.MaPhong, TenToaNha = t.TenToaNha, MaHopDong = h.MaHopDong, TenNguoiBao = a.HoTen };

    public async Task<(List<BaoHongChiTiet> Rows, BaoHongTongQuan Summary)> DanhSachLocAsync(int actorId, BaoHongBoLoc filter, CancellationToken ct = default)
    {
        await ActorAsync(actorId, false, ct); filter.ChuanHoa();
        if (filter.ToaNhaId.HasValue || filter.CanhBao is not null) throw new ArgumentException("Điều kiện lọc không hỗ trợ trên danh sách khách thuê.");
        var scoped = Rows(actorId); var query = RepairListQuery.Filter(scoped, filter, clock.UtcNow);
        var counts = await scoped.GroupBy(x => x.BaoHong.TrangThai).Select(g => new { State = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.State, x => x.Count, ct);
        var total = await query.CountAsync(ct); filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (total + 19) / 20));
        var rows = await RepairListQuery.Sort(query, filter.SapXep).Skip((filter.Page - 1) * 20).Take(20).ToListAsync(ct);
        await RepairListQuery.EnrichAsync(db, rows, ct);
        return (rows, new BaoHongTongQuan { Tong = counts.Values.Sum(), Moi = counts.GetValueOrDefault("MOI"), DangXuLy = counts.GetValueOrDefault("DANG_XU_LY"), HoanThanh = counts.GetValueOrDefault("HOAN_THANH"), TheoTrangThai = counts, KetQua = total });
    }

    public async Task<List<BaoHongChiTiet>> DanhSachAsync(int actorId, int page, CancellationToken ct = default)
    {
        await ActorAsync(actorId, false, ct);
        return await Rows(actorId).OrderByDescending(x => x.BaoHong.Id).Skip((Math.Clamp(page, 1, 100000) - 1) * 20).Take(21).ToListAsync(ct);
    }

    public async Task<BaoHongChiTiet?> ChiTietAsync(int actorId, int id, CancellationToken ct = default)
    {
        await ActorAsync(actorId, false, ct);
        var result = await Rows(actorId).SingleOrDefaultAsync(x => x.BaoHong.Id == id, ct);
        if (result is null && await db.BaoHongs.AnyAsync(x => x.Id == id, ct)) throw new UnauthorizedAccessException();
        if (result is not null) await LoadExtrasAsync(result, ct);
        return result;
    }

    internal async Task LoadExtrasAsync(BaoHongChiTiet result, CancellationToken ct)
    {
        var id = result.BaoHong.Id;
        result.ChiPhi = await db.ChiPhiBaoHongs.AsNoTracking().SingleOrDefaultAsync(x => x.BaoHongId == id, ct);
        if (result.ChiPhi is not null)
        {
            result.TrangThaiChiPhi = await db.Database.SqlQuery<string>($"SELECT trang_thai_phan_bo AS Value FROM chi_phi_bao_hong_trang_thai WHERE bao_hong_id={id}").SingleAsync(ct);
            result.ChiPhiDaKhoa = result.TrangThaiChiPhi is "TRONG_HOA_DON_NHAP" or "DA_DUA_VAO_HOA_DON" or "HOA_DON_DA_HUY";
            result.ChiPhiInput.SoTien = result.ChiPhi.SoTien.ToString(CultureInfo.InvariantCulture);
            result.ChiPhiInput.BenChiuPhi = result.ChiPhi.BenChiuPhi;
            result.ChiPhiInput.PhienBan = result.ChiPhi.PhienBan;
            result.LichSuChiPhi = await db.LichSuChiPhiBaoHongs.AsNoTracking().Where(x => x.ChiPhiId == result.ChiPhi.Id).OrderBy(x => x.PhienBanSau).ToListAsync(ct);
        }
        result.Images = await db.AnhBaoHongs.AsNoTracking().Where(x => x.BaoHongId == id).OrderBy(x => x.ThuTu).ToListAsync(ct);
        result.TraoDois = await db.BaoHongTraoDois.AsNoTracking().Where(x => x.BaoHongId == id).OrderBy(x => x.ThoiDiem).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<BaoHong> GuiAsync(int actorId, BaoHongInput input, CancellationToken ct = default)
    {
        var device = input.LoaiThietBi?.Trim(); var description = input.MoTa?.Trim();
        if (string.IsNullOrWhiteSpace(device) || device.Length > 100) throw new BaoHongValidationException("LoaiThietBi", "Nhập loại thiết bị, tối đa 100 ký tự.");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 5000) throw new BaoHongValidationException("MoTa", "Nhập mô tả sự cố, tối đa 5.000 ký tự.");
        if (input.MucDo is not ("THUONG" or "GAP")) throw new BaoHongValidationException("MucDo", "Chọn mức độ Thường hoặc Gấp.");
        if (!Guid.TryParseExact(input.KhoaChongTrung, "N", out _)) throw new BaoHongValidationException("", "Phiên gửi không hợp lệ. Hãy mở lại biểu mẫu.");
        await ActorAsync(actorId, true, ct);
        var prepared = await images.PrepareAsync(input.Images, ct);
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite, ct);
        var actor = await ActorAsync(actorId, true, ct);
        var existing = await db.BaoHongs.AsNoTracking().SingleOrDefaultAsync(x => x.KhoaChongTrung == input.KhoaChongTrung, ct);
        if (existing is not null)
        {
            if (existing.NguoiBaoId != actorId) throw new UnauthorizedAccessException();
            if (existing.HopDongId != input.HopDongId || existing.LoaiThietBi != device || existing.MoTa != description || existing.MucDo != input.MucDo)
                throw new BaoHongValidationException("", "Phiên gửi đã dùng cho nội dung khác. Hãy mở lại biểu mẫu.");
            var stored = await db.AnhBaoHongs.AsNoTracking().Where(x => x.BaoHongId == existing.Id).OrderBy(x => x.ThuTu).ToListAsync(ct);
            if (!stored.Select(x => x.Sha256).SequenceEqual(prepared.Select(x => x.Hash)))
                throw new BaoHongValidationException("Images", "Phiên gửi đã dùng cho bộ ảnh khác. Hãy kiểm tra yêu cầu đã gửi hoặc mở lại biểu mẫu.");
            await tx!.CommitAsync(ct); return existing;
        }
        // Read the clock once after reserving the writer: midnight and expiry use the same instant.
        var now = clock.UtcNow; var today = DateOnly.FromDateTime(now.AddHours(7));
        var contract = await Eligible(actorId, today).SingleOrDefaultAsync(x => x.Id == input.HopDongId, ct);
        if (contract is null) throw new BaoHongValidationException("HopDongId", "Phòng không thuộc hợp đồng còn hiệu lực của bạn tại thời điểm gửi. Hãy chọn lại phòng.");
        var profileId = await db.KhachThues.Where(x => x.TaiKhoanId == actorId).Select(x => x.Id).SingleAsync(ct);
        var month = today.ToString("yyyyMM", CultureInfo.InvariantCulture);
        using var counter = sqlite.Connection!.CreatePortableCommand(); counter.Transaction = sqlite;
        counter.CommandText = """
            INSERT INTO bao_hong_so_ma(thang,so_cuoi) VALUES($month,1)
            ON CONFLICT(thang) DO UPDATE SET so_cuoi=so_cuoi+1 WHERE so_cuoi<9999
            RETURNING so_cuoi
            """;
        counter.Parameters.AddWithValue("$month", month);
        var number = await counter.ExecuteScalarAsync(ct);
        if (number is null) throw new BaoHongValidationException("", "Tháng này đã hết dải 9999 mã báo hỏng. Vui lòng liên hệ chủ nhà để báo sự cố.");
        var issue = new BaoHong { MaBaoHong = $"BH-{month}-{Convert.ToInt32(number):D4}", NguoiBaoId = actorId,
            KhachThueId = profileId, PhongId = contract.PhongId, HopDongId = contract.Id, LoaiThietBi = device,
            MoTa = description, MucDo = input.MucDo, TrangThai = "MOI", NgayTao = now, KhoaChongTrung = input.KhoaChongTrung };
        db.BaoHongs.Add(issue); await db.SaveChangesAsync(ct);
        var written = new List<string>();
        try
        {
            for (var i = 0; i < prepared.Count; i++)
            {
                var photo = prepared[i]; var key = Guid.NewGuid().ToString("N");
                written.Add(key);
                try { await images.SaveAsync(key, photo.Bytes, ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { throw new IOException("Không lưu được ảnh báo hỏng.", ex); }
                db.AnhBaoHongs.Add(new AnhBaoHong { BaoHongId = issue.Id, ThuTu = i + 1, KhoaLuu = key, ContentType = photo.ContentType, SoByte = photo.Bytes.Length, Sha256 = photo.Hash });
            }
            await db.SaveChangesAsync(ct);
            var audit = JsonSerializer.Serialize(new { issue.MaBaoHong, issue.PhongId, issue.HopDongId, issue.MucDo, issue.TrangThai });
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_sau,thoi_diem) VALUES({actor.Id},{actor.HoTen},{actor.VaiTro},{"bao_hong"},{issue.Id},{"TAO"},{audit},{now})", ct);
            await RepairEmailQueue.EnqueueAsync(db,issue,"MOI:"+issue.Id,actorId,null,now,ct);
            await tx!.CommitAsync(ct); return issue;
        }
        catch
        {
            await tx!.RollbackAsync(CancellationToken.None);
            foreach (var key in written) images.Delete(key);
            throw;
        }
    }
}
