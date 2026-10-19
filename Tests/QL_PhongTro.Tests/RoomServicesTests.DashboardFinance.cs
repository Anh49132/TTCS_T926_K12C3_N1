using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
    [Fact]
    public async Task DashboardFinanceMatchesInvoicePeriodAndExcludesPendingCancelledPayments()
    {
        using var db=Context();var f=await CollectionFixtureAsync(db);
        var id=await AddPublishedTenantInvoiceAsync(db,f.A,"DASH-FINANCE",false);
        await new GhiThuHoaDonService(db,CollectionClock()).GhiAsync(1,Receipt(id,200000));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO thanh_toan(hoa_don_id,so_tien,ngay_thanh_toan,trang_thai) VALUES({id},400000,'2026-10-13','CHO_XAC_NHAN'),({id},100000,'2026-10-13','DA_HUY')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO giao_dich_coc(ma_giao_dich_coc,khoa_chong_trung,hop_dong_id,loai_giao_dich,so_tien,hinh_thuc,trang_thai,nguoi_thuc_hien_id,ngay_giao_dich) VALUES('DASH-COC','dash-deposit',{f.A},'THU_COC',100000,'TIEN_MAT','DA_XAC_NHAN',1,'2026-10-13')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO giao_dich_coc(ma_giao_dich_coc,khoa_chong_trung,hop_dong_id,hoa_don_id,loai_giao_dich,so_tien,trang_thai,nguoi_thuc_hien_id,ngay_giao_dich) VALUES('DASH-CANTRU','dash-offset',{f.A},{id},'CAN_TRU',100000,'DA_XAC_NHAN',1,'2026-10-13')");
        var service=new DashboardFinanceService(db);
        var month=await service.LoadAsync(1,"CHU_NHA",[1],new(2026,9,1),new(2026,10,19));
        Assert.True(month.Available);Assert.Equal(1000000,month.Issued);Assert.Equal(200000,month.Paid);Assert.Equal(100000,month.Deposit);Assert.Equal(700000,month.Remaining);Assert.Equal(700000,month.TotalDebt);Assert.Equal(1,month.OverdueCount);
        Assert.Equal(200000,month.Buildings[1].Paid);Assert.Equal(12,month.Months.Count);
        var october=await service.LoadAsync(1,"CHU_NHA",[1],new(2026,10,1),new(2026,10,19));
        Assert.Equal(0,october.InvoiceCount);Assert.Equal(700000,october.TotalDebt);
        Assert.Equal(200000,october.Months.Single(x=>x.Label=="09/2026").Paid);
        Assert.Equal(0,october.Months.Single(x=>x.Label=="10/2026").Paid);
    }
    [Fact]
    public async Task DashboardFinanceDeniesOtherOwnerManagerAndRevokedFinancePermission()
    {
        using var db=Context();await CollectionFixtureAsync(db);var service=new DashboardFinanceService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.LoadAsync(1,"CHU_NHA",[2],new(2026,10,1),new(2026,10,19)));
        Assert.False((await service.LoadAsync(1,"QUAN_LY",[1],new(2026,10,1),new(2026,10,19))).Available);
        await db.Database.ExecuteSqlRawAsync("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='CHU_NHA' AND ModuleCode='TAI_CHINH'");
        Assert.False((await service.LoadAsync(1,"CHU_NHA",[1],new(2026,10,1),new(2026,10,19))).Available);
    }
}
