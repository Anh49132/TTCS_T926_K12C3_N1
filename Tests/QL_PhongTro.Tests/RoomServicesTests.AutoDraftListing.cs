using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData(null)][InlineData("")]
    public async Task AutoDraftListingPreservesNullOrEmptyDescriptionAndResetsCreator(string? description)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var old=await OldCheckoutListing(db,contract,text:description);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tin_dang SET nguoi_dang_id=2 WHERE id={old.Id}");db.ChangeTracker.Clear();
        var service=new TatToanService(db,TatToanClock());await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        var draft=await db.TinDangs.OrderByDescending(x=>x.Id).FirstAsync();Assert.Equal(description,draft.NoiDung);Assert.Equal(1,draft.NguoiDangId);
        Assert.Equal(2,(await db.TinDangs.SingleAsync(x=>x.Id==old.Id)).NguoiDangId);
    }

    [Fact]
    public async Task AutoDraftListingPublicOldPostBlocksPreflightWithoutAnyWrites()
    {
        using var db=Context();var contract=await XacNhanFixture(db);await OldCheckoutListing(db,contract,"DANG_HIEN_THI");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,contract));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    private async Task<TinDang> OldCheckoutListing(QL_PhongTro.Data.AppDbContext db,int contract,string state="DA_CHO_THUE",int days=20,string? text="Mô tả cũ")
    {
        var room=(await db.HopDongs.AsNoTracking().SingleAsync(x=>x.Id==contract)).PhongId;
        var post=new TinDang {PhongId=room,NguoiDangId=1,TieuDe="Tin cũ phòng có ban công",NoiDung=text,TrangThai=state,
            NgayTao=TatToanClock().UtcNow.AddDays(-days),NgayDang=state=="NHAP"?null:TatToanClock().UtcNow.AddDays(-days+1),NgayHetHan=TatToanClock().UtcNow.AddDays(-1)};
        db.TinDangs.Add(post);await db.SaveChangesAsync();return post;
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(8)]
    public async Task AutoDraftListingCopiesExactDescriptionPreservesGalleryAndUsesCurrentPrice(int photos)
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var description=string.Concat(Enumerable.Repeat("Nội dung dài\n<script>không thực thi</script>\nGiá cũ ghi trong mô tả: 1.000.000 đ.\n",200));
        var old=await OldCheckoutListing(db,contract,text:description);
        var oldSnapshot=JsonSerializer.Serialize(await db.TinDangs.AsNoTracking().SingleAsync(x=>x.Id==old.Id));
        for(var i=1;i<=photos;i++)db.AnhPhongs.Add(new(){PhongId=old.PhongId,DuongDan=$"/fixture/{i}.jpg",DuongDanAnhNho=$"/fixture/{i}-thumb.jpg",ThuTu=i,MoTa=$"Ảnh {i}",NgayTao=TatToanClock().UtcNow});
        await db.SaveChangesAsync();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET gia_thue=4500000,mo_ta='Mô tả phòng khác' WHERE id={old.PhongId}");
        db.ChangeTracker.Clear();var gallery=JsonSerializer.Serialize(await db.AnhPhongs.AsNoTracking().OrderBy(x=>x.ThuTu).ToListAsync());
        var service=new TatToanService(db,TatToanClock());await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        var draft=await db.TinDangs.AsNoTracking().SingleAsync(x=>x.PhongId==old.PhongId && x.Id!=old.Id);
        Assert.Equal("NHAP",draft.TrangThai);Assert.Equal(old.Id,draft.TinGocId);Assert.Equal(1,draft.NguoiDangId);
        Assert.Equal(old.TieuDe,draft.TieuDe);Assert.Equal(description,draft.NoiDung);Assert.Null(draft.NgayDang);Assert.Null(draft.NgayHetHan);Assert.Equal(TatToanClock().UtcNow,draft.NgayTao);
        Assert.Equal(oldSnapshot,JsonSerializer.Serialize(await db.TinDangs.AsNoTracking().SingleAsync(x=>x.Id==old.Id)));
        Assert.Equal(gallery,JsonSerializer.Serialize(await db.AnhPhongs.AsNoTracking().OrderBy(x=>x.ThuTu).ToListAsync()));
        var mark=await db.NhatKyHoatDongs.SingleAsync(x=>x.DoiTuongId==draft.Id && x.HanhDong=="TAO_NHAP_TRA_PHONG");
        Assert.Equal(4500000,JsonDocument.Parse(mark.DuLieuSau!).RootElement.GetProperty("GiaThue").GetInt64());
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var html=WebUtility.HtmlDecode(await client.GetStringAsync("/TinDang/QuanLy"));Assert.Contains("Nháp tự tạo sau trả phòng",html);Assert.Contains("4.500.000",html);
        var edit=WebUtility.HtmlDecode(await client.GetStringAsync($"/TinDang/Sua/{draft.Id}"));Assert.Contains("4.500.000",edit);
        using var guest=factory.CreateClient(new(){AllowAutoRedirect=false});Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync($"/TinDang/ChiTiet/{draft.Id}")).StatusCode);
        await Login(client,"other");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TinDang/Sua/{draft.Id}")).StatusCode);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task AutoDraftListingNoPublishedSourceUsesOnlyPublicRoomDescription(bool onlyManualDraft)
    {
        using var db=Context();var contract=await XacNhanFixture(db);var room=(await db.HopDongs.SingleAsync(x=>x.Id==contract)).PhongId;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE phong_tro SET mo_ta='Mô tả phòng',ghi_chu_noi_bo='Bí mật nội bộ' WHERE id={room}");
        if(onlyManualDraft)await OldCheckoutListing(db,contract,"NHAP",text:"Nháp riêng chưa đăng");
        db.ChangeTracker.Clear();var before=await db.TinDangs.CountAsync();var service=new TatToanService(db,TatToanClock());
        var input=DongY(await service.KiemTraChotAsync(1,contract));await service.HoanTatAsync(1,input);db.ChangeTracker.Clear();
        var draft=await db.TinDangs.AsNoTracking().OrderByDescending(x=>x.Id).FirstAsync();Assert.Null(draft.TinGocId);Assert.Equal("Mô tả phòng",draft.NoiDung);
        Assert.Equal(before+1,await db.TinDangs.CountAsync());Assert.Equal("NHAP",draft.TrangThai);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.HoanTatAsync(1,input));Assert.Equal(before+1,await db.TinDangs.CountAsync());
    }

    [Fact]
    public async Task AutoDraftListingSelectsLatestPublishedDateThenIdAndNeverOtherRoomOrManualDraft()
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        await OldCheckoutListing(db,contract,days:50);var expected=await OldCheckoutListing(db,contract,"TAM_AN",days:10);
        await OldCheckoutListing(db,contract,"NHAP",days:0);
        var tie=await OldCheckoutListing(db,contract,"DA_CHO_THUE",days:10);
        var another=await db.HopDongs.Where(x=>x.Id!=contract).Select(x=>x.Id).FirstAsync();await OldCheckoutListing(db,another,days:0);
        var service=new TatToanService(db,TatToanClock());await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));db.ChangeTracker.Clear();
        var draft=await db.TinDangs.OrderByDescending(x=>x.Id).FirstAsync();Assert.Equal(tie.Id,draft.TinGocId);Assert.NotEqual(expected.Id,draft.TinGocId);
    }

    [Fact]
    public async Task AutoDraftListingRejectsPublicOldListingWithoutChangingItOrEndingContract()
    {
        using var db=Context();var contract=await XacNhanFixture(db);await OldCheckoutListing(db,contract,"DANG_HIEN_THI");
        db.ChangeTracker.Clear();var before=await ReleaseSnapshot(db);var service=new TatToanService(db,TatToanClock());
        await Assert.ThrowsAsync<InvalidOperationException>(async ()=>await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract))));
        using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Theory]
    [InlineData("insert")][InlineData("marker")]
    public async Task AutoDraftListingFailureRollsBackContractRoomFinancialsAndAudit(string failure)
    {
        using var db=Context();var contract=await XacNhanFixture(db);await OldCheckoutListing(db,contract);db.ChangeTracker.Clear();
        var before=await ReleaseSnapshot(db);var service=new TatToanService(db,TatToanClock());var input=DongY(await service.KiemTraChotAsync(1,contract));
        await db.Database.ExecuteSqlRawAsync(failure=="insert"
            ? "CREATE TRIGGER checkout_draft_failure BEFORE INSERT ON tin_dang WHEN NEW.trang_thai='NHAP' BEGIN SELECT RAISE(ABORT,'Synthetic draft insert failure'); END"
            : "CREATE TRIGGER checkout_marker_failure BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.hanh_dong='TAO_NHAP_TRA_PHONG' BEGIN SELECT RAISE(ABORT,'Synthetic draft marker failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.HoanTatAsync(1,input));using var check=Context();Assert.Equal(before,await ReleaseSnapshot(check));
    }

    [Fact]
    public async Task AutoDraftListingConcurrentCompletionCreatesOneDraftAndOneMarker()
    {
        int contract;QL_PhongTro.ViewModels.XacNhanTatToanInput input;
        using(var db=Context()){contract=await XacNhanFixture(db);await OldCheckoutListing(db,contract);input=DongY(await new TatToanService(db,TatToanClock()).KiemTraChotAsync(1,contract));}
        async Task<bool> Submit(){using var db=Context();try{await new TatToanService(db,TatToanClock()).HoanTatAsync(1,input);return true;}catch(InvalidOperationException){return false;}}
        var results=await Task.WhenAll(Task.Run(Submit),Task.Run(Submit));Assert.Single(results,x=>x);
        using var check=Context();Assert.Single(await check.TinDangs.Where(x=>x.TrangThai=="NHAP").ToListAsync());Assert.Single(await check.NhatKyHoatDongs.Where(x=>x.HanhDong=="TAO_NHAP_TRA_PHONG").ToListAsync());
    }
}
