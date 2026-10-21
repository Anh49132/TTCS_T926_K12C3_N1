using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

internal static class RepairListQuery
{
    // Caller must scope the query to the actor before filtering or loading metadata.
    internal static IQueryable<BaoHongChiTiet> Filter(IQueryable<BaoHongChiTiet> query, BaoHongBoLoc filter, DateTime now, bool state = true)
    {
        if (filter.ToaNhaId.HasValue) query = query.Where(x => x.ToaNhaId == filter.ToaNhaId);
        if (state && filter.TrangThai is not null) query = query.Where(x => x.BaoHong.TrangThai == filter.TrangThai);
        if (filter.MucDo is not null) query = query.Where(x => x.BaoHong.MucDo == filter.MucDo);
        if (filter.TimKiem is not null) { var text = filter.TimKiem.ToLowerInvariant(); query = query.Where(x => x.BaoHong.MaBaoHong.ToLower().Contains(text) || x.BaoHong.LoaiThietBi.ToLower().Contains(text) || x.MaPhong.ToLower().Contains(text) || x.TenNguoiBao.ToLower().Contains(text)); }
        if (filter.CanhBao == "QUA_HAN") { var cutoff = now.AddHours(-24); query = query.Where(x => x.BaoHong.MucDo == "GAP" && x.BaoHong.TrangThai == "MOI" && x.BaoHong.NgayTao < cutoff); }
        return query;
    }
    internal static IOrderedQueryable<BaoHongChiTiet> Sort(IQueryable<BaoHongChiTiet> query, string sort) => sort switch
    {
        "CU_NHAT" => query.OrderBy(x => x.BaoHong.NgayTao).ThenBy(x => x.BaoHong.Id),
        "UU_TIEN" => query.OrderByDescending(x => x.BaoHong.TrangThai == "MOI").ThenByDescending(x => x.BaoHong.MucDo == "GAP").ThenBy(x => x.BaoHong.NgayTao).ThenBy(x => x.BaoHong.Id),
        _ => query.OrderByDescending(x => x.BaoHong.Id)
    };
    internal static async Task EnrichAsync(AppDbContext db, List<BaoHongChiTiet> rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.BaoHong.Id).ToList();
        var replies = await db.BaoHongTraoDois.AsNoTracking().Where(x => ids.Contains(x.BaoHongId)).OrderBy(x => x.ThoiDiem).ThenBy(x => x.Id).ToListAsync(ct);
        var images = await db.AnhBaoHongs.AsNoTracking().Where(x => ids.Contains(x.BaoHongId)).Select(x => new { x.BaoHongId, x.ThuTu }).ToListAsync(ct);
        foreach (var row in rows)
        {
            var history = replies.Where(x => x.BaoHongId == row.BaoHong.Id).ToList();
            row.CapNhatGanNhat = history.LastOrDefault()?.ThoiDiem ?? row.BaoHong.NgayTao;
            row.SoPhanHoi = history.Count(x => x.Loai == "PHAN_HOI"); row.NguoiXuLyGanNhat = history.LastOrDefault()?.TenNguoiGui;
            row.SoAnh = images.Count(x => x.BaoHongId == row.BaoHong.Id);
            row.AnhDaiDienThuTu = images.Where(x => x.BaoHongId == row.BaoHong.Id).OrderBy(x => x.ThuTu).Select(x => (int?)x.ThuTu).FirstOrDefault();
        }
    }
}
