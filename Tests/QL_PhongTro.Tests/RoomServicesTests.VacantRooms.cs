using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task VacantRooms_CompletedSettlementAppearsFirstWithNewDraftAndCurrentPrice()
    {
        using var db=Context();
        var contract=await XacNhanFixture(db);
        var roomId=(await db.HopDongs.SingleAsync(x=>x.Id==contract)).PhongId;
        var unrelated=await AddRoom(db,"000-OLDER-VACANT");
        var query=new VacantRoomListingService(db);
        var now=TatToanClock().UtcNow;
        Assert.DoesNotContain(await query.Query(1,null,now).ToListAsync(),x=>x.Id==roomId);
        var settlement=new TatToanService(db,TatToanClock());
        await settlement.HoanTatAsync(1,DongY(await settlement.KiemTraChotAsync(1,contract)));
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET gia_thue=4500000 WHERE id={roomId}");
        var count=await db.NhatKyHoatDongs.CountAsync();
        var rooms=await query.Query(1,null,now).ToListAsync();
        var returned=rooms.First();
        Assert.Equal(roomId,returned.Id);
        Assert.Equal(4500000,returned.Rent);
        Assert.Equal(now,returned.VacantSince);
        Assert.Equal("NHAP",returned.ListingStatus);
        Assert.Equal("Kiểm tra Nháp",returned.ActionLabel);
        Assert.Contains(rooms,x=>x.Id==unrelated.Id);
        Assert.Equal(count,await db.NhatKyHoatDongs.CountAsync());
    }

    [Fact]
    public async Task VacantRooms_ExpiryBoundaryMatchesPublicVisibilityAndReadDoesNotChangePost()
    {
        using var db=Context();var room=await AddRoom(db,"BOUNDARY");
        var now=new DateTime(2026,10,15,8,0,0,DateTimeKind.Utc);
        var post=new TinDang {PhongId=room.Id,NguoiDangId=1,TieuDe="Boundary post",TrangThai="DANG_HIEN_THI",NgayTao=now,NgayDang=now,NgayHetHan=now};
        db.TinDangs.Add(post);await db.SaveChangesAsync();
        var query=new VacantRoomListingService(db);
        Assert.Empty(await query.Query(1,null,now).ToListAsync());
        Assert.Single(await query.Query(1,null,now.AddTicks(1)).ToListAsync());
        Assert.Equal("DANG_HIEN_THI",(await db.TinDangs.AsNoTracking().SingleAsync()).TrangThai);
        Assert.Equal(now,(await db.TinDangs.AsNoTracking().SingleAsync()).NgayHetHan);
    }
}
