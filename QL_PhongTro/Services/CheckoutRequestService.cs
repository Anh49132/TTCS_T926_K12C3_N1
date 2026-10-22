using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
using System.Text.Json;
namespace QL_PhongTro.Services;

public sealed class CheckoutRequestService(AppDbContext db, ITimeProvider clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
    public async Task<bool> ReadyAsync() => await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='yeu_cau_tra_phong'").SingleAsync() == 1;
    private async Task<TaiKhoan> ActorAsync(int actor) => await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.DangHoatDong && !x.IsDeleted && (x.VaiTro == "KHACH_THUE" || x.VaiTro == "CHU_NHA")) ?? throw new UnauthorizedAccessException();
    public async Task<CheckoutRequestViewModel> PageAsync(int actor, int? contract = null)
    {
        var account = await ActorAsync(actor); var owner = account.VaiTro == "CHU_NHA";
        var contracts = from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id join k in db.KhachThues on h.KhachDungTenId equals k.Id
            where owner ? b.ChuNhaId == actor : k.TaiKhoanId == actor
            select new { h.Id, h.MaHopDong, h.TrangThai, Phong = p.MaPhong, ToaNha = b.TenToaNha };
        if (contract.HasValue && !await contracts.AnyAsync(x => x.Id == contract)) throw new UnauthorizedAccessException();
        var available = await contracts
            .Select(x => new CheckoutContract(x.Id, x.MaHopDong, x.Phong, x.ToaNha,
                db.KyHopDongs.Where(k => k.HopDongId == x.Id).Select(k => (DateOnly?)k.NgayKetThuc).Max() ?? Today, x.TrangThai)).ToListAsync();
        var rows = await (from r in db.YeuCauTraPhongs.AsNoTracking() join h in db.HopDongs on r.HopDongId equals h.Id
            join p in db.PhongTros on h.PhongId equals p.Id join b in db.ToaNhas on p.ToaNhaId equals b.Id
            join a in db.TaiKhoans on r.NguoiGuiId equals a.Id
            where (owner ? b.ChuNhaId == actor : r.NguoiGuiId == actor) && (!contract.HasValue || r.HopDongId == contract)
            orderby r.TrangThai == "MOI" descending, r.NgayTao descending, r.Id descending
            select new CheckoutRequestRow(r, h.MaHopDong, p.MaPhong, b.TenToaNha, a.HoTen, h.TrangThai)).ToListAsync();
        return new() { Contracts = available, Rows = rows, HopDongId = contract, HomNay = Today, IsOwner = owner };
    }
    private async Task<(HopDongThamChieu Contract, int Owner, DateOnly Start, DateOnly End)> CheckAsync(int actor, int contract, bool owner)
    {
        var a = await ActorAsync(actor);
        var context = await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id join k in db.KhachThues on h.KhachDungTenId equals k.Id
            where h.Id == contract select new { Contract = h, Owner = b.ChuNhaId, Tenant = k.TaiKhoanId }).SingleOrDefaultAsync();
        if (context is null || (owner ? a.VaiTro != "CHU_NHA" || context.Owner != actor : a.VaiTro != "KHACH_THUE" || context.Tenant != actor)) throw new UnauthorizedAccessException();
        if (context.Contract.TrangThai != "DANG_HIEU_LUC") throw new InvalidOperationException("Hợp đồng không còn đang hiệu lực. Hãy tải lại dữ liệu.");
        var periods = await db.KyHopDongs.Where(x => x.HopDongId == contract).ToListAsync();
        if (periods.Count == 0) throw new InvalidOperationException("Hợp đồng chưa có kỳ thuê.");
        return (context.Contract, context.Owner, periods.Min(x => x.NgayBatDau), periods.Max(x => x.NgayKetThuc));
    }
    public async Task<int> SubmitAsync(int actor, int contract, DateOnly? day, string? reason)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var c = await CheckAsync(actor, contract, false);
        if (day is null || day < Today || day < c.Start || day >= c.End) throw new InvalidOperationException("Ngày đề nghị phải từ hôm nay, nằm trong kỳ thuê và trước ngày hết hợp đồng.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new InvalidOperationException("Nhập lý do trả phòng từ 1 đến 500 ký tự.");
        if (await db.HoaDons.AnyAsync(x => x.HopDongId == contract && x.LoaiHoaDon == "KY_CUOI" && x.TrangThai != "DA_HUY")) throw new InvalidOperationException("Chủ nhà đã bắt đầu lập hóa đơn kỳ cuối. Vui lòng trao đổi trực tiếp để điều chỉnh bàn giao.");
        if (await db.YeuCauTraPhongs.AnyAsync(x => x.HopDongId == contract && (x.TrangThai == "MOI" || x.TrangThai == "DA_TIEP_NHAN"))) throw new InvalidOperationException("Hợp đồng đã có yêu cầu đang xử lý. Xem yêu cầu hiện tại bên dưới.");
        var request = new YeuCauTraPhong { HopDongId = contract, NguoiGuiId = actor, NgayDeNghi = day.Value, LyDo = reason.Trim(), NgayTao = clock.UtcNow, CapNhatLuc = clock.UtcNow };
        db.YeuCauTraPhongs.Add(request); await db.SaveChangesAsync();
        await RecordAsync(actor, request, c.Owner, "MOI", null); await tx!.CommitAsync(); return request.Id;
    }
    public async Task ChangeAsync(int actor, int id, int version, string action, DateOnly? handover, string? response)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
        await using var tx = await db.Database.UseTransactionAsync(sqlite);
        var r = await db.YeuCauTraPhongs.SingleOrDefaultAsync(x => x.Id == id) ?? throw new UnauthorizedAccessException();
        var cancel = action == "DA_HUY"; var c = await CheckAsync(actor, r.HopDongId, !cancel);
        if (cancel && r.NguoiGuiId != actor) throw new UnauthorizedAccessException();
        if (r.PhienBan != version) throw new InvalidOperationException("Yêu cầu đã thay đổi. Hãy tải lại trước khi xử lý.");
        if (action is not ("DA_HUY" or "DA_TIEP_NHAN" or "TU_CHOI")) throw new InvalidOperationException("Thao tác không hợp lệ.");
        if (r.TrangThai != "MOI" && !(r.TrangThai == "DA_TIEP_NHAN" && action == "TU_CHOI")) throw new InvalidOperationException("Yêu cầu đã được xử lý, không thể thực hiện thao tác này.");
        if (response?.Trim().Length > 500 || (action == "TU_CHOI" && string.IsNullOrWhiteSpace(response))) throw new InvalidOperationException("Phản hồi tối đa 500 ký tự; từ chối phải ghi rõ lý do.");
        if (action == "DA_TIEP_NHAN" && (handover is null || handover < Today || handover < c.Start || handover > c.End)) throw new InvalidOperationException("Chọn ngày bàn giao đã thống nhất, từ hôm nay đến ngày hết hợp đồng.");
        var before = r.TrangThai; r.TrangThai = action; r.PhienBan++; r.CapNhatLuc = clock.UtcNow;
        if (!cancel) { r.PhanHoi = response?.Trim(); r.NguoiXuLyId = actor; if (action == "DA_TIEP_NHAN") r.NgayBanGiao = handover; }
        await db.SaveChangesAsync(); await RecordAsync(actor, r, cancel ? c.Owner : r.NguoiGuiId, action, before);
        await tx!.CommitAsync();
    }
    internal async Task CompleteAsync(int actor, int contract)
    {
        // Called inside the existing settlement transaction, after financial/contract changes.
        if (!await ReadyAsync()) return;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Hoàn tất yêu cầu phải nằm trong giao dịch chốt trả phòng.");
        var a = await ActorAsync(actor);
        if (a.VaiTro != "CHU_NHA" || !await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id where h.Id == contract && h.TrangThai == "DA_KET_THUC" && b.ChuNhaId == actor select h.Id).AnyAsync()) throw new UnauthorizedAccessException();
        foreach (var r in await db.YeuCauTraPhongs.Where(x => x.HopDongId == contract && (x.TrangThai == "MOI" || x.TrangThai == "DA_TIEP_NHAN")).ToListAsync())
        {
            var before = r.TrangThai; r.TrangThai = "HOAN_TAT"; r.PhienBan++; r.CapNhatLuc = clock.UtcNow;
            await db.SaveChangesAsync(); await RecordAsync(actor, r, r.NguoiGuiId, "HOAN_TAT", before);
        }
    }
    private async Task RecordAsync(int actor, YeuCauTraPhong r, int recipient, string kind, string? before)
    {
        var a = await ActorAsync(actor);
        var prior = before is null ? null : JsonSerializer.Serialize(new { TrangThai = before });
        var after = JsonSerializer.Serialize(new { r.TrangThai, r.HopDongId, r.NgayDeNghi, r.NgayBanGiao, r.PhienBan });
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem) VALUES({actor},{a.HoTen},{a.VaiTro},{"YEU_CAU_TRA_PHONG"},{r.Id},{kind},{prior},{after},{clock.UtcNow})");
        db.ThongBaoTraPhongs.Add(new() { YeuCauId = r.Id, NguoiNhanId = recipient, Loai = kind, NgayTao = clock.UtcNow,
            TieuDe = kind switch { "MOI" => "Yêu cầu trả phòng trước hạn mới", "DA_HUY" => "Khách đã hủy yêu cầu trả phòng", "DA_TIEP_NHAN" => "Đã tiếp nhận yêu cầu trả phòng", "HOAN_TAT" => "Đã hoàn tất trả phòng và chốt tất toán", _ => "Yêu cầu trả phòng chưa được chấp thuận" },
            NoiDung = kind == "DA_TIEP_NHAN" ? $"Ngày bàn giao đã thống nhất: {r.NgayBanGiao:dd/MM/yyyy}. Hợp đồng vẫn hiệu lực đến khi chốt trả phòng." : $"Yêu cầu TP-{r.Id:D6} · ngày đề nghị {r.NgayDeNghi:dd/MM/yyyy}. Mở để xem chi tiết và phản hồi.",
            DuongDan = $"/YeuCauTraPhong?hopDongId={r.HopDongId}#request-{r.Id}" });
        await db.SaveChangesAsync();
    }
}
