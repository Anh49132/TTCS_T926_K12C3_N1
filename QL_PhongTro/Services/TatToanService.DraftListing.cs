using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed partial class TatToanService
{
    private async Task TaoTinNhapSauTraPhongAsync(int actor,HopDongThamChieu contract,PhongTro room)
    {
        if(db.Database.CurrentTransaction is null || contract.TrangThai!="DA_KET_THUC" || room.TrangThai!="TRONG")
            throw new InvalidOperationException("Chỉ tạo tin Nháp trong giao dịch kết thúc hợp đồng và giải phóng phòng.");
        // Publishing is a separate owner action. Do not expose an old public post when the room becomes vacant.
        if(await db.TinDangs.AnyAsync(x=>x.PhongId==room.Id && x.TrangThai=="DANG_HIEN_THI"))
            throw new InvalidOperationException("Phòng còn tin đang công khai. Hãy tạm ẩn tin cũ trước khi xác nhận trả phòng để tránh hiển thị ngoài ý muốn.");
        var source=await db.TinDangs.AsNoTracking().Where(x=>x.PhongId==room.Id && x.TrangThai!="NHAP")
            .OrderByDescending(x=>x.NgayDang??x.NgayTao).ThenByDescending(x=>x.Id).FirstOrDefaultAsync();
        var draft=new TinDang {PhongId=room.Id,NguoiDangId=actor,TinGocId=source?.Id,
            TieuDe=source?.TieuDe??$"Cho thuê phòng {room.MaPhong}",NoiDung=source is null?room.MoTa:source.NoiDung,
            TrangThai="NHAP",NgayTao=clock.UtcNow,NgayDang=null,NgayHetHan=null};
        // The gallery and current price belong to PhongTro; keep their rows/files untouched.
        // The enclosing immediate transaction + unique contract confirmation serialize retries/concurrent events.
        db.TinDangs.Add(draft);await db.SaveChangesAsync();
        var actorName=await db.TaiKhoans.Where(x=>x.Id==actor).Select(x=>x.HoTen).SingleAsync();
        var metadata=JsonSerializer.Serialize(new {HopDongId=contract.Id,PhongId=room.Id,TinGocId=source?.Id,GiaThue=room.GiaThue});
        var now=clock.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_sau,thoi_diem) VALUES ({actor},{actorName},{"CHU_NHA"},{"tin_dang"},{draft.Id},{"TAO_NHAP_TRA_PHONG"},{metadata},{now})");
    }
}
