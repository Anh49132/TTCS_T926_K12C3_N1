using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class ContractCreationTests
{
    [Fact]
    public async Task DraftReopensWithItsNegotiatedAmountsAfterListingPriceChanges()
    {
        using var db=Context();var vm=Valid();vm.Intent="NHAP";vm.GiaThue=3200000;vm.TienCoc=1500000;
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(await Controller(db).Create(vm,default));
        await db.Database.ExecuteSqlRawAsync("UPDATE phong_tro SET gia_thue=4000000");
        var restored=Assert.IsType<QL_PhongTro.ViewModels.HopDongCreateViewModel>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(await Controller(db).Create(1,default)).Model);
        Assert.Equal(3200000,restored.GiaThue);Assert.Equal(1500000,restored.TienCoc);
    }
    [Theory]
    [InlineData(3200000,1500000,true)]
    [InlineData(500000,1500000,true)]
    [InlineData(500000,1500001,false)]
    [InlineData(499999,0,false)]
    [InlineData(3200000,-1,false)]
    public async Task NegotiatedAmountsAreValidatedWithoutChangingListing(long rent,long deposit,bool accepted)
    {
        using var db=Context();var vm=Valid();vm.GiaThue=rent;vm.TienCoc=deposit;
        var result=await Controller(db).Create(vm,default);
        Assert.Equal(accepted,result is Microsoft.AspNetCore.Mvc.RedirectToActionResult);
        Assert.Equal(3500000,(await db.PhongTros.SingleAsync()).GiaThue);
        Assert.Equal(accepted?1:0,await db.HopDongs.CountAsync());
        if(accepted){Assert.Equal(rent,(await db.KyHopDongs.SingleAsync()).GiaThue);Assert.Equal(deposit,(await db.HopDongs.SingleAsync()).TienCoc);}
    }
}

public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task ConcurrentBatchPublishCreatesOneNotificationPerInvoice()
    {
        Dictionary<int,int> versions;
        using(var seed=Context()){
            await MonthlyFixture(seed);await seed.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");
            await new HoaDonDichVuService(seed,new(seed)).PhatHanhDanhSachAsync(1,1,2026,10,taoNhap:true);
            versions=await seed.HoaDons.ToDictionaryAsync(x=>x.Id,x=>x.PhienBan);
        }
        async Task<int> Publish(){using var db=Context();return await new HoaDonDichVuService(db,new(db)).PhatHanhNhieuNhapAsync(1,1,2026,10,versions,new(2026,11,1),new(2026,11,8),true);}
        var results=await Task.WhenAll(Task.Run(Publish),Task.Run(Publish));Assert.Equal(2,results.Sum());
        using var check=Context();Assert.Equal(2,await check.ThongBaoHoaDons.CountAsync());Assert.Equal(2,await check.HoaDons.CountAsync(x=>x.TrangThai=="DA_PHAT_HANH"));
    }

    [Theory][InlineData(0,true)][InlineData(51,true)][InlineData(1,false)]
    public async Task BatchRequiresConfirmationAndAtMostFiftyDrafts(int count,bool confirmation)
    {
        using var db=Context();
        await MonthlyFixture(db);
        var versions=Enumerable.Range(1,count).ToDictionary(id=>id,id=>0);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new HoaDonDichVuService(db,new(db)).PhatHanhNhieuNhapAsync(1,1,2026,10,versions,new(2026,11,1),new(2026,11,8),confirmation));
        Assert.Empty(await db.HoaDons.ToListAsync());Assert.Empty(await db.ThongBaoHoaDons.ToListAsync());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ProgressRequiresOnlyMeteredUtilities(bool bothFlat)
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE ky_hop_dong SET ngay_bat_dau='2026-10-01',ngay_ket_thuc='2026-10-31',so_thang=1");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cau_hinh_dich_vu SET cach_tinh='THEO_NGUOI' WHERE dich_vu_id={f.Nuoc}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE dich_vu_id={f.Nuoc}");
        if(bothFlat){await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE cau_hinh_dich_vu SET cach_tinh='THEO_NGUOI' WHERE dich_vu_id={f.Dien}");await db.Database.ExecuteSqlRawAsync("DELETE FROM chi_so_dien_nuoc");}
        var service=new TienDoChiSoService(db);
        var progress=Assert.Single((await service.XemAsync(1,2026,10,default)).ToaNhas);
        Assert.Equal(2,progress.SoPhongDaChot);Assert.Equal(0,progress.SoPhongConThieu);
        Assert.Empty((await service.PhongConThieuAsync(1,1,2026,10,default)).Phongs);
        Assert.Empty(await service.LayCanhBaoAsync(1,new(2026,10,31)));
        if(!bothFlat){await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM chi_so_dien_nuoc WHERE hop_dong_id={f.B}");Assert.Equal(1,Assert.Single((await service.XemAsync(1,2026,10,default)).ToaNhas).SoPhongConThieu);}
    }

    [Fact]
    public async Task PublishFiftyDraftsThroughHttpIsAtomicAndIdempotent()
    {
        using(var db=Context()){
            await MonthlyFiftyFixture(db);
            await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");
            await new HoaDonDichVuService(db,new(db)).PhatHanhDanhSachAsync(1,1,2026,10,taoNhap:true);
        }
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var page=await client.GetStringAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10");
        Assert.Contains("publish-monthly-drafts",page);
        var fields=new List<KeyValuePair<string,string>>{new("toaNhaId","1"),new("nam","2026"),new("thang","10"),new("ngayPhatHanh","2026-11-01"),new("hanThanhToan","2026-11-08"),new("xacNhan","true"),new("__RequestVerificationToken",FormValue(page,"__RequestVerificationToken"))};
        using(var db=Context())foreach(var bill in await db.HoaDons.ToListAsync()){fields.Add(new("selectedInvoiceIds",bill.Id.ToString()));fields.Add(new($"versions[{bill.Id}]",bill.PhienBan.ToString()));}
        var timer=Stopwatch.StartNew();
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields))).StatusCode);
        Assert.True(timer.Elapsed.TotalSeconds<30);
        using(var db=Context()){Assert.Equal(50,await db.HoaDons.CountAsync(x=>x.TrangThai=="DA_PHAT_HANH"));Assert.Equal(50,await db.ThongBaoHoaDons.CountAsync());Assert.Equal(100,await db.ChiSoDienNuocs.CountAsync(x=>x.DaKhoa));}
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields))).StatusCode);
        using(var db=Context())Assert.Equal(50,await db.ThongBaoHoaDons.CountAsync());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent([]))).StatusCode);
        fields.RemoveAll(x=>x.Key=="toaNhaId");fields.Add(new("toaNhaId","2"));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields))).StatusCode);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task BatchFailureRollsBackEarlierPublishedDraftAndLocks(bool auditFailure)
    {
        Dictionary<int,int> versions;
        using(var db=Context()){
            await MonthlyFixture(db);await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");
            await new HoaDonDichVuService(db,new(db)).PhatHanhDanhSachAsync(1,1,2026,10,taoNhap:true);
            versions=await db.HoaDons.ToDictionaryAsync(x=>x.Id,x=>x.PhienBan);
            if(auditFailure)await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_batch_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN (SELECT COUNT(*) FROM thong_bao)>0 BEGIN SELECT RAISE(ABORT,'test failure'); END");
            else versions[versions.Keys.Max()]++;
            await Assert.ThrowsAnyAsync<Exception>(()=>new HoaDonDichVuService(db,new(db)).PhatHanhNhieuNhapAsync(1,1,2026,10,versions,new(2026,11,1),new(2026,11,8),true));
        }
        using var check=Context();Assert.Equal(2,await check.HoaDons.CountAsync(x=>x.TrangThai=="NHAP"));Assert.Empty(await check.ThongBaoHoaDons.ToListAsync());Assert.False(await check.ChiSoDienNuocs.AnyAsync(x=>x.DaKhoa));
    }
}
