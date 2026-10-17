using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed record ChatLink(string Label, string Url);
public sealed record ChatToolResult(object Data, IReadOnlyList<ChatLink> Links);

// All queries are fixed, read-only and scoped here. Model arguments never supply actor/role/SQL.
public sealed class ChatDataTools(AppDbContext db, ITimeProvider clock)
{
    public async Task<(string Role, HashSet<string> Tools, string Scope)> AccessAsync(int actor, CancellationToken ct)
    {
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.DangHoatDong
            && !x.IsDeleted && x.EmailConfirmed && !x.MustChangePassword, ct);
        if (account?.VaiTro is not ("KHACH_THUE" or "CHU_NHA" or "QUAN_LY" or "ADMIN")) throw new UnauthorizedAccessException();
        var role = account.VaiTro;
        var permissions = await db.RolePermissions.AsNoTracking().Where(x => x.RoleCode == role
            && (x.AccessLevel == "READ" || x.AccessLevel == "WRITE" || x.AccessLevel == "FULL"))
            .Select(x => x.ModuleCode).ToListAsync(ct);
        bool Allowed(string module) => role == "ADMIN" || permissions.Contains(module);
        HashSet<string> tools = [];
        if (role != "QUAN_LY" && Allowed("TAI_CHINH")) tools.Add("invoices");
        if (Allowed("BAO_HONG")) tools.Add("repairs");
        if (role != "KHACH_THUE" && Allowed("PHONG_TRO")) tools.Add("rooms");
        if (Allowed("HOP_DONG")) tools.Add("contracts");
        var buildings = await db.ToaNhas.AsNoTracking().Where(x => role == "ADMIN"
            || role == "CHU_NHA" && x.ChuNhaId == actor || role == "QUAN_LY" && x.QuanLyId == actor)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
        var profiles = await db.KhachThues.AsNoTracking().Where(x => x.TaiKhoanId == actor).Select(x => x.Id).ToListAsync(ct);
        return (role, tools, role + ":" + string.Join(',', tools.Order()) + ":" + string.Join(',', buildings) + ":" + string.Join(',', profiles));
    }

    public async Task<ChatToolResult> RunAsync(int actor, string tool, JsonElement args, CancellationToken ct)
    {
        var access = await AccessAsync(actor, ct);
        if (!access.Tools.Contains(tool)) throw new UnauthorizedAccessException();
        var role = access.Role;
        var buildingIds = db.ToaNhas.Where(x => role == "ADMIN" || role == "CHU_NHA" && x.ChuNhaId == actor
            || role == "QUAN_LY" && x.QuanLyId == actor).Select(x => x.Id);
        var rooms = db.PhongTros.AsNoTracking().Where(x => buildingIds.Contains(x.ToaNhaId));
        var profiles = db.KhachThues.Where(x => x.TaiKhoanId == actor).Select(x => x.Id);
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        string? Search()
        {
            if (!args.TryGetProperty("search", out var value) || value.ValueKind == JsonValueKind.Null) return null;
            var text = value.GetString()?.Trim();
            if (text?.Length > 100) throw new ArgumentException("Từ khóa tra cứu tối đa 100 ký tự.");
            return string.IsNullOrEmpty(text) ? null : text;
        }
        var search = Search();
        if (tool == "invoices")
        {
            int? year = null;
            if (args.TryGetProperty("year", out var y) && y.ValueKind != JsonValueKind.Null
                )
            { if (!y.TryGetInt32(out var v) || v < 1900 || v > 9998) throw new ArgumentException("Năm không hợp lệ."); year = v; }
            int? month = null;
            if (args.TryGetProperty("month", out var m) && m.ValueKind != JsonValueKind.Null)
            { if (!m.TryGetInt32(out var v) || v < 1 || v > 12) throw new ArgumentException("Tháng không hợp lệ."); month = v; }
            if (month.HasValue && !year.HasValue) year = today.Year;
            var query = from h in db.HoaDons.AsNoTracking()
                        join c in db.HopDongs on h.HopDongId equals c.Id
                        join p in db.PhongTros on c.PhongId equals p.Id
                        where h.TrangThai == "DA_PHAT_HANH" && (!year.HasValue || h.Nam == year) && (!month.HasValue || h.Thang == month)
                        && (role == "KHACH_THUE" ? c.KhachDungTenId.HasValue && profiles.Contains(c.KhachDungTenId.Value) : buildingIds.Contains(p.ToaNhaId))
                        && (search == null || h.MaHoaDon.Contains(search) || p.MaPhong.Contains(search))
                        select new { h.Id, h.MaHoaDon, p.MaPhong, h.Nam, h.Thang, h.TongTien, h.HanThanhToan,
                            Paid = db.ThanhToans.Where(t => t.HoaDonId == h.Id && t.TrangThai == "DA_XAC_NHAN").Sum(t => (long?)t.SoTien) ?? 0,
                            Pending = db.ThanhToans.Where(t => t.HoaDonId == h.Id && t.TrangThai == "CHO_XAC_NHAN").Sum(t => (long?)t.SoTien) ?? 0 };
            var values = await query.OrderByDescending(x => x.Nam).ThenByDescending(x => x.Thang).ThenByDescending(x => x.Id).ToListAsync(ct);
            var offsets = await CanTruHoaDonQueries.LayAsync(db, values.Select(x => x.Id));
            var result = values.Select(x => new { code = x.MaHoaDon, room = x.MaPhong, year = x.Nam, month = x.Thang,
                total = x.TongTien, confirmed = x.Paid, depositOffset = offsets.GetValueOrDefault(x.Id), pending = x.Pending,
                remaining = Math.Max(0L, x.TongTien - x.Paid - offsets.GetValueOrDefault(x.Id)), due = x.HanThanhToan.ToString("dd/MM/yyyy"),
                overdueDays = x.TongTien > x.Paid + offsets.GetValueOrDefault(x.Id) ? Math.Max(0, today.DayNumber - x.HanThanhToan.DayNumber) : 0 }).ToList();
            return new(new { year, month, count = result.Count, total = result.Sum(x => x.total), confirmed = result.Sum(x => x.confirmed),
                remaining = result.Sum(x => x.remaining), pending = result.Sum(x => x.pending), items = result.OrderByDescending(x => x.remaining > 0).Take(20),
                note = "Tổng toàn bộ hóa đơn đã phát hành trong phạm vi năm/tháng nếu có lọc, không lọc thì mọi kỳ. Danh sách tối đa 20 hóa đơn ưu tiên còn nợ. Chờ xác nhận không giảm nợ." },
                [new(role == "KHACH_THUE" ? "Hóa đơn của tôi" : "Quản lý hóa đơn", role == "KHACH_THUE" ? "/ThongBao/DanhSach" : "/HoaDonDichVu/Management")]);
        }
        if (tool == "repairs")
        {
            var query = db.BaoHongs.AsNoTracking().Where(x => role == "KHACH_THUE" ? x.NguoiBaoId == actor : rooms.Select(p => p.Id).Contains(x.PhongId));
            if (search is not null) query = query.Where(x => x.MaBaoHong.Contains(search) || x.LoaiThietBi.Contains(search));
            var counts = await query.GroupBy(x => x.TrangThai).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
            var rows = await query.OrderByDescending(x => x.Id).Take(20).Select(x => new { code = x.MaBaoHong, device = x.LoaiThietBi,
                status = x.TrangThai, urgency = x.MucDo, sentUtc = x.NgayTao }).ToListAsync(ct);
            var path = role == "KHACH_THUE" ? "/BaoHong" : role == "QUAN_LY" ? "/QuanLyBaoHong" : "/ChuNhaBaoHong";
            // ADMIN has read permission but no owner/manager repair route.
            return new(new { counts, items = rows, note = "Danh sách tối đa 20 báo hỏng mới nhất; không gồm mô tả/ảnh riêng tư." },
                role == "ADMIN" ? [] : [new("Xem báo hỏng", path)]);
        }
        if (tool == "rooms")
        {
            if (search is not null) rooms = rooms.Where(x => x.MaPhong.Contains(search));
            var counts = await rooms.GroupBy(x => x.TrangThai).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
            var perBuilding = await (from p in rooms join b in db.ToaNhas on p.ToaNhaId equals b.Id
                group p by b.TenToaNha into g select new { building = g.Key, total = g.Count(), vacant = g.Count(x => x.TrangThai == "TRONG"),
                    rented = g.Count(x => x.TrangThai == "DANG_THUE"), stopped = g.Count(x => x.TrangThai == "NGUNG_CHO_THUE") }).ToListAsync(ct);
            var rows = await rooms.OrderBy(x => x.GiaThue).ThenBy(x => x.Id).Take(20).Select(x => new { room = x.MaPhong,
                price = x.GiaThue, area = x.DienTich, capacity = x.SoNguoiToiDa, status = x.TrangThai }).ToListAsync(ct);
            return new(new { counts, perBuilding, items = rows, note = "Trạng thái hiện tại, không phải báo cáo lấp đầy lịch sử. Danh sách tối đa 20 phòng giá tăng dần." }, [new("Danh sách phòng", "/PhongTro")]);
        }
        var contracts = db.HopDongs.AsNoTracking().Where(c => c.TrangThai != "NHAP" && c.TrangThai != "DA_HUY"
            && (role == "KHACH_THUE" ? c.KhachDungTenId.HasValue && profiles.Contains(c.KhachDungTenId.Value)
                || db.NguoiOGheps.Any(g => g.HopDongId == c.Id && profiles.Contains(g.KhachThueId)) : rooms.Select(p => p.Id).Contains(c.PhongId)));
        if (search is not null) contracts = contracts.Where(x => x.MaHopDong.Contains(search));
        var periods = await (from c in contracts join k in db.KyHopDongs on c.Id equals k.HopDongId
            orderby c.Id descending, k.NgayBatDau descending
            select new { code = c.MaHopDong, status = c.TrangThai, start = k.NgayBatDau, end = k.NgayKetThuc, rent = k.GiaThue, agreedDeposit = c.TienCoc })
            .Take(20).ToListAsync(ct);
        return new(new { items = periods, note = "Tối đa 20 kỳ hợp đồng, mới nhất trước. Cọc thỏa thuận không phải tiền cọc thực nhận." },
            [new("Xem hợp đồng", role == "KHACH_THUE" ? "/HopDongCuaToi" : "/HopDong")]);
    }
}
