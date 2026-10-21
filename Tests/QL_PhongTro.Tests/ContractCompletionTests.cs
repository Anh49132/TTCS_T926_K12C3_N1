using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Controllers;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class ContractCreationTests
{
    void Execute(string sql) { using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.ExecuteNonQuery(); }
    void Existing(string status="DANG_HIEU_LUC",string start="2026-11-05",string end="2027-11-04",string code="HD-2026-0007") => Execute($"""
        INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES(8,'{code}',1,1,'{status}',1,'2026-01-01');
        INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(8,1,'{start}','{end}',12,3500000,1,'2026-01-01');
        """);

    [Fact] public async Task DepositDefaultsToAgreedRentNotRoomDeposit(){Execute("UPDATE phong_tro SET tien_coc_du_kien=99");using var db=Context();var vm=Assert.IsType<HopDongCreateViewModel>(Assert.IsType<ViewResult>(await Controller(db).Create(1,default)).Model);Assert.Equal(vm.GiaThue,vm.TienCoc);}
    [Theory][InlineData(0,true)][InlineData(1,true)][InlineData(2,true)][InlineData(3,true)][InlineData(-1,false)][InlineData(4,false)]
    public async Task DepositBoundaries(int months,bool accepted){using var db=Context();var vm=Valid();vm.TienCoc=3500000L*months;var result=await Controller(db).Create(vm,default);Assert.Equal(accepted,result is RedirectToActionResult);Assert.Equal(accepted?1:0,await db.HopDongs.CountAsync());Assert.Equal(accepted?"DANG_THUE":"TRONG",(await db.PhongTros.SingleAsync()).TrangThai);if(accepted)Assert.Equal(3500000L*months,(await db.HopDongs.SingleAsync()).TienCoc);}
    [Theory][InlineData(6,false)][InlineData(7,true)]
    public async Task StartDateCannotPrecedeVietnamToday(int day,bool accepted){using var db=Context();var vm=Valid();vm.NgayBatDau=new(2026,10,day);Assert.Equal(accepted,(await Controller(db).Create(vm,default)) is RedirectToActionResult);Assert.Equal(accepted?1:0,await db.HopDongs.CountAsync());Assert.Equal(accepted?"DANG_THUE":"TRONG",(await db.PhongTros.SingleAsync()).TrangThai);}
    [Fact] public async Task OldRequestDateDefaultsToToday(){Execute("UPDATE yeu_cau_thue SET ngay_mong_muon='2026-10-01'");using var db=Context();var vm=Assert.IsType<HopDongCreateViewModel>(Assert.IsType<ViewResult>(await Controller(db).Create(1,default)).Model);Assert.Equal(new DateOnly(2026,10,7),vm.NgayBatDau);Assert.Equal(1,vm.SoThangCoc);}
    [Fact] public async Task PastDateOverlapCheckRejected(){using var db=Context();Assert.IsType<BadRequestResult>(await Controller(db).KiemTra(1,new(2026,10,6),12,default));}
    [Theory][InlineData("DANG_HIEU_LUC")][InlineData("CHO_HIEU_LUC")]
    public async Task OverlapBlockedAndExistingCodeShown(string status){Existing(status);using var db=Context();var result=Assert.IsType<ViewResult>(await Controller(db).Create(Valid(),default));var vm=Assert.IsType<HopDongCreateViewModel>(result.Model);Assert.Equal("HD-2026-0007",Assert.Single(vm.ChongLans).Ma);Assert.Single(await db.HopDongs.ToListAsync());Assert.Empty(await db.HopDongChiSoDauKys.ToListAsync());Assert.Equal("TRONG",(await db.PhongTros.SingleAsync()).TrangThai);}
    [Theory][InlineData("2026-11-05","2026-11-05",false)][InlineData("2026-01-01","2026-11-04",true)][InlineData("2027-11-04","2027-12-01",false)][InlineData("2027-11-05","2028-01-01",true)]
    public async Task OverlapIsInclusiveAndAdjacentPeriodsAllowed(string start,string end,bool accepted){Existing(start:start,end:end);using var db=Context();Assert.Equal(accepted,(await Controller(db).Create(Valid(),default)) is RedirectToActionResult);}
    [Theory][InlineData("DA_HUY")][InlineData("DA_KET_THUC")][InlineData("NHAP")]
    public async Task NonEffectiveContractsDoNotBlock(string status){Existing(status);using var db=Context();Assert.IsType<RedirectToActionResult>(await Controller(db).Create(Valid(),default));}
    [Theory][InlineData(-1,0)][InlineData(0,-1)][InlineData(0.0001,0)]
    public async Task InvalidMetersDoNotChangeRoom(double electric,double water){using var db=Context();var vm=Valid();vm.ChiSoDien=(decimal)electric;vm.ChiSoNuoc=(decimal)water;Assert.IsType<ViewResult>(await Controller(db).Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());Assert.Equal("TRONG",(await db.PhongTros.SingleAsync()).TrangThai);}
    [Fact] public async Task BothMetersRequired(){using var db=Context();var vm=Valid();vm.ChiSoNuoc=null;Assert.IsType<ViewResult>(await Controller(db).Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task ZeroMetersAccepted(){using var db=Context();var vm=Valid();vm.ChiSoDien=vm.ChiSoNuoc=0;Assert.IsType<RedirectToActionResult>(await Controller(db).Create(vm,default));Assert.Equal(0,(await db.HopDongChiSoDauKys.SingleAsync()).ChiSoNuoc);}
    [Fact] public async Task StaleRoomVersionRejected(){Execute("UPDATE phong_tro SET phien_ban=1");using var db=Context();Assert.IsType<ViewResult>(await Controller(db).Create(Valid(),default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task FailureRecordingMetersRollsBackContractRoomAuditAndCode()
    {
        Execute("CREATE TRIGGER fail_initial_meter BEFORE INSERT ON hop_dong_chi_so_dau_ky BEGIN SELECT RAISE(ABORT,'test failure'); END");
        using var db=Context();Assert.IsType<ViewResult>(await Controller(db).Create(Valid(),default));Assert.Empty(await db.HopDongs.ToListAsync());Assert.Empty(await db.KyHopDongs.ToListAsync());Assert.Empty(await db.HopDongChiSoDauKys.ToListAsync());Assert.Empty(await db.NhatKyHoatDongs.ToListAsync());Assert.Equal("TRONG",(await db.PhongTros.SingleAsync()).TrangThai);
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM hop_dong_so_ma";Assert.Equal(0L,cmd.ExecuteScalar());
    }
    [Fact] public async Task ExistingDraftCanBeCompletedWithoutDuplicateContract()
    {
        Existing("NHAP",code:"NHAP-old");Execute("UPDATE hop_dong SET yeu_cau_thue_id=1 WHERE id=8");
        using var db=Context();Assert.IsType<ViewResult>(await Controller(db).Create(1,default));Assert.IsType<RedirectToActionResult>(await Controller(db).Create(Valid(),default));Assert.Equal(8,(await db.HopDongs.SingleAsync()).Id);Assert.Single(await db.KyHopDongs.ToListAsync());Assert.Single(await db.HopDongChiSoDauKys.ToListAsync());
    }
    sealed class ContractClock : ITimeProvider { public DateTime UtcNow=>new(2026,12,31,18,0,0,DateTimeKind.Utc); }
    [Fact] public async Task CodeYearUsesVietnamTimeAndSkipsExistingCode(){Existing("DA_HUY",code:"HD-2027-0007");using var db=Context();var controller=Controller(db);var timed=new HopDongController(db,new ContractClock()){ControllerContext=controller.ControllerContext,TempData=controller.TempData};var vm=Valid();vm.NgayBatDau=new(2027,1,1);Assert.IsType<RedirectToActionResult>(await timed.Create(vm,default));Assert.Equal("HD-2027-0008",(await db.HopDongs.SingleAsync(h=>h.YeuCauThueId==1)).MaHopDong);}
    [Fact] public async Task CodeLimitDoesNotChangeRoom(){Execute("INSERT INTO hop_dong_so_ma VALUES(2027,9999)");using var db=Context();var original=Controller(db);var controller=new HopDongController(db,new ContractClock()){ControllerContext=original.ControllerContext,TempData=original.TempData};var vm=Valid();vm.NgayBatDau=new(2027,1,1);Assert.IsType<ViewResult>(await controller.Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());Assert.Equal("TRONG",(await db.PhongTros.SingleAsync()).TrangThai);}
    [Fact] public async Task VietnamMidnightRejectsPreviousUtcDate(){using var db=Context();var original=Controller(db);var controller=new HopDongController(db,new ContractClock()){ControllerContext=original.ControllerContext,TempData=original.TempData};var vm=Valid();vm.NgayBatDau=new(2026,12,31);Assert.IsType<ViewResult>(await controller.Create(vm,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task SimultaneousOverlappingRequestsOnlyOneSucceeds()
    {
        Execute("INSERT INTO yeu_cau_thue(id,ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao) VALUES(3,'YC-parallel',1,1,'THUE_NGAY','2026-11-05',1,'DA_DUYET','2026-01-01')");
        async Task<IActionResult> Save(int request){using var db=Context();var vm=Valid();vm.YeuCauId=request;return await Controller(db).Create(vm,default);}
        var results=await Task.WhenAll(Task.Run(()=>Save(1)),Task.Run(()=>Save(3)));
        Assert.Single(results.OfType<RedirectToActionResult>());Assert.Single(results.OfType<ViewResult>());using var check=Context();Assert.Single(await check.HopDongs.ToListAsync());Assert.Single(await check.HopDongChiSoDauKys.ToListAsync());
    }
    [Fact] public async Task ForeignOwnerCannotPostOrInspectOverlap(){using var db=Context(2);Assert.IsType<ForbidResult>(await Controller(db,2).Create(Valid(),default));Assert.IsType<ForbidResult>(await Controller(db,2).KiemTra(1,new(2026,11,5),12,default));Assert.Empty(await db.HopDongs.ToListAsync());}
    [Fact] public async Task SimultaneousDifferentRoomsGetDistinctCodes()
    {
        Execute("""
            INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(2,1,'A204',2,25,3500000,3500000,2,'TRONG','2026-01-01');
            INSERT INTO tin_dang(id,phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_tao) VALUES(2,2,1,'Room2','DANG_HIEN_THI','2026-01-01');
            INSERT INTO yeu_cau_thue(id,ma_yeu_cau,tin_dang_id,khach_thue_id,loai_yeu_cau,ngay_mong_muon,so_nguoi_du_kien,trang_thai,ngay_tao) VALUES(3,'YC-second-room',2,1,'THUE_NGAY','2026-11-05',1,'DA_DUYET','2026-01-01');
            """);
        async Task<IActionResult> Save(int request){using var db=Context();var vm=Valid();vm.YeuCauId=request;return await Controller(db).Create(vm,default);}
        var results=await Task.WhenAll(Task.Run(()=>Save(1)),Task.Run(()=>Save(3)));Assert.All(results,r=>Assert.IsType<RedirectToActionResult>(r));
        using var check=Context();var codes=await check.HopDongs.Select(h=>h.MaHopDong).ToListAsync();Assert.Equal(2,codes.Distinct().Count());Assert.All(codes,c=>Assert.Matches(@"^HD-\d{4}-\d{4}$",c));
    }
}
