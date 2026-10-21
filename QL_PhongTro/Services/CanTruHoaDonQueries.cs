using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
namespace QL_PhongTro.Services;

public static class CanTruHoaDonQueries
{
    public static async Task<Dictionary<int,long>> LayAsync(AppDbContext db,IEnumerable<int> invoices)
    {
        if(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'").SingleAsync()==0)return [];
        var ids=invoices.ToList();
        return await db.GiaoDichCocs.AsNoTracking().Where(x=>x.HoaDonId.HasValue && ids.Contains(x.HoaDonId.Value) && x.Loai=="CAN_TRU" && x.TrangThai=="DA_XAC_NHAN")
            .GroupBy(x=>x.HoaDonId!.Value).Select(x=>new {Id=x.Key,Amount=x.Sum(v=>v.SoTien)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount);
    }
}
