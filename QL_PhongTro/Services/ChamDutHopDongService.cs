using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

internal static class ChamDutHopDongService
{
    internal static async Task<ChamDutHopDong> DoiChieuAsync(AppDbContext db,int contract,DateOnly day)
    {
        var end=await db.KyHopDongs.Where(x=>x.HopDongId==contract).Select(x=>(DateOnly?)x.NgayKetThuc).MaxAsync()
            ?? throw new InvalidOperationException("Hợp đồng chưa có kỳ thuê.");
        var cap=await db.HopDongs.Where(x=>x.Id==contract).Select(x=>x.TienCoc).SingleAsync();
        return new() { HopDongId=contract,NgayTraPhong=day,NgayKetThuc=end,CocThoaThuan=cap,TruocHan=day<end };
    }
    // Caller owns the immediate transaction; audit and settlement changes roll back together.
    internal static async Task LuuAsync(AppDbContext db,ChamDutHopDong info,int contract,int invoice,int actor,DateTime now)
    {
        var saved=await db.ChamDutHopDongs.SingleOrDefaultAsync(x=>x.HopDongId==contract);
        if(saved is null)
        {
            info.HopDongId=contract;info.HoaDonCuoiId=invoice;info.PhienBan=1;
            info.NguoiCapNhatId=actor;info.CapNhatLuc=now;db.ChamDutHopDongs.Add(info);
        }
        else
        {
            if(saved.HoaDonCuoiId!=invoice)throw new InvalidOperationException("Thông tin chấm dứt đang gắn với hóa đơn kỳ cuối khác. Cần đối chiếu trước khi sửa.");
            saved.NgayTraPhong=info.NgayTraPhong;saved.NgayKetThuc=info.NgayKetThuc;saved.CocThoaThuan=info.CocThoaThuan;
            saved.TruocHan=info.TruocHan;saved.LyDo=info.LyDo;saved.LyDoKhac=info.LyDoKhac;
            saved.ApDungPhat=info.ApDungPhat;saved.TienPhat=info.TienPhat;
            saved.PhienBan++;saved.NguoiCapNhatId=actor;saved.CapNhatLuc=now;
        }
    }
}
