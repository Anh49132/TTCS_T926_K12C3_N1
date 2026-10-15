using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed partial class TatToanService
{
    // Called only after settlement validation, inside HoanTatAsync's immediate transaction.
    private async Task<(HopDongThamChieu Contract, PhongTro Room)> GiaiPhongPhongAsync(int actor,int contract,DateOnly returned)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Kết thúc hợp đồng và giải phóng phòng phải cùng giao dịch.");
        await KiemTraQuyenAsync(actor,contract,true);
        var h=await db.HopDongs.SingleAsync(x=>x.Id==contract);
        var room=await db.PhongTros.SingleAsync(x=>x.Id==h.PhongId);
        if(h.TrangThai!="DANG_HIEU_LUC" || room.TrangThai!="DANG_THUE"
            || await db.HopDongs.AnyAsync(x=>x.PhongId==room.Id && x.Id!=contract && x.TrangThai=="DANG_HIEU_LUC"))
            throw new InvalidOperationException("Không thể giải phóng phòng: hợp đồng hoặc phòng đã thay đổi trạng thái. Hãy tải lại để kiểm tra.");
        h.TrangThai="DA_KET_THUC";h.NgayTraPhong=returned;
        room.TrangThai="TRONG";room.PhienBan=checked(room.PhienBan+1);
        return(h,room);
    }
}
