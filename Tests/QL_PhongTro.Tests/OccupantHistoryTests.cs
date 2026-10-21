using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class ContractCreationTests
{
    private void HistoryFixture()
    {
        Existing(status:"DA_KET_THUC",start:"2026-01-01",end:"2026-03-31");
        Execute("""
            INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(2,'History roommate','2026-01-01');
            INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ngay_ra) VALUES(8,2,'2026-01-10','2026-01-31'),(8,2,'2026-02-10','2026-02-28');
            INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES(9,'HD-current-history',1,1,'DANG_HIEU_LUC',1,'2026-04-01');
            INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(9,1,'2026-04-01','2026-06-30',3,3500000,1,'2026-04-01'),(9,2,'2026-07-01','2026-12-31',6,3500000,1,'2026-07-01');
            INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(9,2,'2026-04-01');
            """);
    }
    private async Task<LichSuNguoiOViewModel> History(DateOnly from,DateOnly to)
    {
        using var db=Context();return Assert.IsType<LichSuNguoiOViewModel>(Assert.IsType<ViewResult>(await Controller(db).LichSuNguoiO(1,from,to,default)).Model);
    }
    [Theory]
    [InlineData("2025-01-01","2025-12-31",0)]
    [InlineData("2027-01-01","2027-12-31",0)]
    [InlineData("2026-01-09","2026-01-10",1)]
    [InlineData("2026-01-31","2026-01-31",1)]
    [InlineData("2026-02-01","2026-02-09",0)]
    [InlineData("2026-01-15","2026-01-20",1)]
    [InlineData("2026-01-01","2026-03-31",2)]
    public async Task HistoryInclusiveOverlapBoundaries(string from,string to,int roommateCount)
    {
        HistoryFixture();var vm=await History(DateOnly.Parse(from),DateOnly.Parse(to));Assert.True(vm.DaLoc);
        Assert.Equal(roommateCount,vm.Nguois.Count(x=>x.VaiTro=="Người ở ghép"));
        if(roommateCount>0){var row=vm.Nguois.First(x=>x.VaiTro=="Người ở ghép");Assert.Equal("History roommate",row.HoTen);Assert.Equal(8,row.HopDongId);}
        if(vm.Nguois.Count>0)Assert.Contains(vm.Nguois,x=>x.VaiTro=="Người đứng tên");
    }
    [Fact] public async Task HistoryMultipleContractsRepeatedPersonAndRenewals()
    {
        HistoryFixture();var vm=await History(new(2026,1,1),new(2026,12,31));Assert.Equal(5,vm.Nguois.Count);
        Assert.Equal(3,vm.Nguois.Count(x=>x.HoTen=="History roommate"));Assert.Equal(2,vm.Nguois.Count(x=>x.VaiTro=="Người đứng tên"));
        var active=vm.Nguois.Where(x=>x.HopDongId==9).ToList();Assert.Equal(2,active.Count);Assert.All(active,x=>Assert.True(x.DangO));
        Assert.All(active,x=>Assert.Equal(new DateOnly(2026,4,1),x.NgayBatDau));Assert.All(active,x=>Assert.Equal(new DateOnly(2026,12,31),x.NgayKetThuc));
        Assert.All(vm.Nguois.Where(x=>x.HopDongId==8),x=>Assert.False(x.DangO));
    }
    [Fact] public async Task HistoryFilterDoesNotTruncateRecordedDates()
    {
        HistoryFixture();var vm=await History(new(2026,1,15),new(2026,1,20));var roommate=Assert.Single(vm.Nguois,x=>x.VaiTro=="Người ở ghép");Assert.Equal(new DateOnly(2026,1,10),roommate.NgayBatDau);Assert.Equal(new DateOnly(2026,1,31),roommate.NgayKetThuc);
    }
    [Fact] public async Task HistoryActualReturnClipsContractAndUnclosedRoommate()
    {
        HistoryFixture();Execute("UPDATE hop_dong SET ngay_tra_phong='2026-05-10',trang_thai='DA_KET_THUC' WHERE id=9");
        var vm=await History(new(2026,5,10),new(2026,5,10));Assert.Equal(2,vm.Nguois.Count);Assert.All(vm.Nguois,x=>Assert.Equal(new DateOnly(2026,5,10),x.NgayKetThuc));Assert.All(vm.Nguois,x=>Assert.False(x.KetThucTheoHanHopDong));
        Assert.Empty((await History(new(2026,5,11),new(2026,6,1))).Nguois);
    }
    [Fact] public async Task HistoryRealGapsNotInvented()
    {
        HistoryFixture();Execute("UPDATE ky_hop_dong SET ngay_bat_dau='2026-07-10' WHERE hop_dong_id=9 AND so_thu_tu=2");Assert.Empty((await History(new(2026,7,1),new(2026,7,9))).Nguois);
        Assert.Equal(4,(await History(new(2026,4,1),new(2026,12,31))).Nguois.Count);
    }
    [Theory][InlineData("NHAP")][InlineData("DA_HUY")]
    public async Task HistoryDraftAndCancelledExcluded(string status){HistoryFixture();Execute($"UPDATE hop_dong SET trang_thai='{status}' WHERE id=9");Assert.Empty((await History(new(2026,4,1),new(2026,12,31))).Nguois);}
    [Fact] public async Task HistoryInvalidRangeNoResults(){HistoryFixture();var vm=await History(new(2026,10,7),new(2026,10,6));Assert.False(vm.DaLoc);Assert.Empty(vm.Nguois);}
    [Fact] public async Task HistoryForeignOwnerAndManagerForbidden()
    {
        HistoryFixture();using var db=Context(2);Assert.IsType<ForbidResult>(await Controller(db,2).LichSuNguoiO(1,new(2026,1,1),new(2026,12,31),default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new LichSuNguoiOService(db).LayAsync(2,1,new(2026,1,1),new(2026,12,31),new(2026,10,7),default));
        Execute("UPDATE tai_khoan SET vai_tro='QUAN_LY' WHERE id=2; UPDATE toa_nha SET quan_ly_id=2");Assert.IsType<ForbidResult>(await Controller(db,2).LichSuNguoiO(1,null,null,default));
    }
    [Fact] public async Task HistoryMissingRoom404AndMissingSignerWarning()
    {
        HistoryFixture();using var db=Context();Assert.IsType<NotFoundResult>(await Controller(db).LichSuNguoiO(99,null,null,default));
        Execute("DROP TRIGGER contract_signer_immutable; UPDATE hop_dong SET trang_thai='DA_KET_THUC',khach_dung_ten_id=NULL WHERE id=8");
        var vm=await History(new(2026,1,1),new(2026,3,31));Assert.Equal(1,vm.HopDongThieuDuLieu);Assert.Equal(2,vm.Nguois.Count);
    }
}
