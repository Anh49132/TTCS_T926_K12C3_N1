using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

// Read-only query shared by the owner dashboard and the complete, paginated list.
public sealed class VacantRoomListingService(AppDbContext db)
{
    public IQueryable<PhongTro> EligibleRooms(int ownerId, int? buildingId, DateTime now) =>
        db.PhongTros.AsNoTracking().Where(room => db.ToaNhas.Any(building => building.Id == room.ToaNhaId
                && building.ChuNhaId == ownerId && building.DangHoatDong
                && (!buildingId.HasValue || building.Id == buildingId.Value)
            )
                && room.TrangThai == "TRONG"
                && !db.TinDangs.Any(post => post.PhongId == room.Id && post.TrangThai == "DANG_HIEN_THI"
                    && (post.NgayHetHan == null || post.NgayHetHan >= now)));

    public IQueryable<HomeVacantRoom> Query(int ownerId, int? buildingId, DateTime now) =>
        from room in EligibleRooms(ownerId, buildingId, now)
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            let vacantSince = db.LichSuTrangThaiPhongs.Where(history => history.PhongId == room.Id
                && history.TrangThai == "TRONG" && history.DenThoiDiem == null && history.HopDongId != null)
                .OrderByDescending(history => history.TuThoiDiem).Select(history => (DateTime?)history.TuThoiDiem).FirstOrDefault()
            orderby vacantSince descending, building.TenToaNha, room.MaPhong, room.Id
            select new HomeVacantRoom(room.Id, room.MaPhong, building.TenToaNha, room.GiaThue, room.DienTich)
            {
                ListingId = db.TinDangs.Where(post => post.PhongId == room.Id)
                    .OrderByDescending(post => post.Id).Select(post => (int?)post.Id).FirstOrDefault(),
                ListingStatus = db.TinDangs.Where(post => post.PhongId == room.Id)
                    .OrderByDescending(post => post.Id).Select(post => post.TrangThai).FirstOrDefault(),
                VacantSince = vacantSince
            };
}
