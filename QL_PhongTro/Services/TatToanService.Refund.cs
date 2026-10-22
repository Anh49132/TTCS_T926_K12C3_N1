using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class TatToanService
{
    public async Task HoanCocAsync(int actor,HoanCocTatToanInput input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite=db.Database.GetDbConnection().BeginPortableTransaction(deferred:false);
        await using var tx=await db.Database.UseTransactionAsync(sqlite);
        await KiemTraQuyenAsync(actor,input.HopDongId,true);
        if(!input.DaHoan || !Guid.TryParseExact(input.KhoaChongTrung,"N",out _) || input.SoTien is null || input.SoTien<=0 || input.SoTien>long.MaxValue
            || decimal.Truncate(input.SoTien.Value)!=input.SoTien || input.HinhThuc is not ("TIEN_MAT" or "CHUYEN_KHOAN"))
            throw new InvalidOperationException("Xác nhận đã hoàn tiền thực tế, nhập số nguyên đồng dương và chọn hình thức hợp lệ.");
        var bang=await db.BangTatToans.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==input.HopDongId && x.TrangThai=="DA_CHOT")
            ?? throw new InvalidOperationException("Chỉ ghi hoàn cọc sau khi tất toán đã được xác nhận.");
        if(!await db.XacNhanTatToans.AnyAsync(x=>x.ThanhLyId==bang.Id))throw new InvalidOperationException("Thiếu dữ liệu xác nhận tất toán.");
        var key="HOAN_TT:"+input.KhoaChongTrung;
        var replay=await db.GiaoDichCocs.AsNoTracking().SingleOrDefaultAsync(x=>x.KhoaChongTrung==key);
        if(replay is not null)
        {
            if(replay.HopDongId!=input.HopDongId || replay.SoTien!=input.SoTien || replay.HinhThuc!=input.HinhThuc || replay.NguoiThucHienId!=actor)
                throw new InvalidOperationException("Phiên hoàn cọc đã được dùng cho dữ liệu khác.");
            await sqlite.CommitAsync();return;
        }
        var ledger=await db.GiaoDichCocs.AsNoTracking().Where(x=>x.HopDongId==input.HopDongId && x.TrangThai=="DA_XAC_NHAN").ToListAsync();
        var held=ledger.Aggregate(0L,(sum,x)=>checked(sum+(x.Loai=="THU_COC"?x.SoTien:-x.SoTien)));
        var refunded=ledger.Where(x=>x.KhoaChongTrung.StartsWith("HOAN_TT:")).Aggregate(0L,(sum,x)=>checked(sum+x.SoTien));
        if(input.SoTien>Math.Min(held,checked(bang.TienPhaiHoan-refunded)))throw new InvalidOperationException("Số hoàn vượt số tiền còn phải hoàn hoặc số cọc đang giữ.");
        db.GiaoDichCocs.Add(new() {Ma="HC-"+Guid.NewGuid().ToString("N"),KhoaChongTrung=key,HopDongId=input.HopDongId,Loai="HOAN_COC",
            SoTien=checked((long)input.SoTien.Value),HinhThuc=input.HinhThuc,NguoiThucHienId=actor,NgayGiaoDich=clock.UtcNow});
        await db.SaveChangesAsync();await sqlite.CommitAsync();
    }
}
