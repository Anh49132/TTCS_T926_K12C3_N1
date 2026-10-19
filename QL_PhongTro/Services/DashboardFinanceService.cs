using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

// Invoice-period totals match the revenue report. Pending/cancelled payments never count.
public sealed class DashboardFinanceService(AppDbContext db)
{
    public async Task<DashboardFinance> LoadAsync(int actor, string role, int[] buildingIds, DateOnly period, DateOnly today, CancellationToken ct = default)
    {
        var result = new DashboardFinance();
        if (role is not ("CHU_NHA" or "ADMIN") || !await db.RolePermissions.AsNoTracking().AnyAsync(x => x.RoleCode == role && x.ModuleCode == "TAI_CHINH" && (x.AccessLevel == "READ" || x.AccessLevel == "WRITE" || x.AccessLevel == "FULL"),ct)) return result;
        var visible = db.ToaNhas.AsNoTracking().Where(x => buildingIds.Contains(x.Id));
        if (role == "CHU_NHA") visible = visible.Where(x => x.ChuNhaId == actor);
        var ids = await visible.Select(x => x.Id).ToArrayAsync(ct);
        if (ids.Length != buildingIds.Distinct().Count()) throw new UnauthorizedAccessException();
        var tables = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','thanh_toan')").SingleAsync(ct);
        if(tables != 2) return result;
        result.Available = true;
        var invoices = await (from h in db.HoaDons.AsNoTracking() join c in db.HopDongs on h.HopDongId equals c.Id join p in db.PhongTros on c.PhongId equals p.Id
            where ids.Contains(p.ToaNhaId) && h.TrangThai == "DA_PHAT_HANH"
            select new {h.Id,h.Nam,h.Thang,h.TongTien,h.HanThanhToan,p.ToaNhaId}).ToListAsync(ct);
        var invoiceIds = invoices.Select(x => x.Id).ToArray();
        var paid = await db.ThanhToans.AsNoTracking().Where(x => invoiceIds.Contains(x.HoaDonId) && x.TrangThai == "DA_XAC_NHAN")
            .GroupBy(x => x.HoaDonId).Select(g => new {Id=g.Key,Amount=g.Sum(x => x.SoTien)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var deposits = new Dictionary<int,long>();
        if(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'").SingleAsync(ct)==1)
            deposits = await db.GiaoDichCocs.AsNoTracking().Where(x=>x.HoaDonId.HasValue && invoiceIds.Contains(x.HoaDonId.Value) && x.Loai=="CAN_TRU" && x.TrangThai=="DA_XAC_NHAN")
                .GroupBy(x=>x.HoaDonId!.Value).Select(g=>new {Id=g.Key,Amount=g.Sum(x=>x.SoTien)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        foreach(var h in invoices)
        {
            var remaining = Math.Max(0,h.TongTien-paid.GetValueOrDefault(h.Id)-deposits.GetValueOrDefault(h.Id));
            result.TotalDebt += remaining;
            if(remaining>0){result.UnpaidCount++;if(h.HanThanhToan<today)result.OverdueCount++;}
            if(h.Nam==period.Year && h.Thang==period.Month){result.Issued+=h.TongTien;result.Paid+=paid.GetValueOrDefault(h.Id);result.Deposit+=deposits.GetValueOrDefault(h.Id);result.Remaining+=remaining;result.InvoiceCount++;}
        }
        for(var i=11;i>=0;i--)
        {
            var month=period.AddMonths(-i);var rows=invoices.Where(x=>x.Nam==month.Year && x.Thang==month.Month).ToList();
            result.Months.Add(new(month.ToString("MM/yyyy"),rows.Sum(x=>x.TongTien),rows.Sum(x=>paid.GetValueOrDefault(x.Id))));
        }
        foreach(var id in ids){var rows=invoices.Where(x=>x.ToaNhaId==id && x.Nam==period.Year && x.Thang==period.Month).ToList();result.Buildings[id]=new(id.ToString(),rows.Sum(x=>x.TongTien),rows.Sum(x=>paid.GetValueOrDefault(x.Id)));}
        return result;
    }
}
