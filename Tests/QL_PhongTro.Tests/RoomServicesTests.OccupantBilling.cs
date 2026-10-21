using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using System.Net;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
    private async Task<int> AddStay(AppDbContext db, int contract, DateOnly start, DateOnly? end = null)
    {
        var profile = new KhachThue { HoTen = "Synthetic roommate", NgayTao = DateTime.UtcNow };
        db.KhachThues.Add(profile); await db.SaveChangesAsync();
        db.NguoiOGheps.Add(new() { HopDongId = contract, KhachThueId = profile.Id, NgayVao = start, NgayRa = end });
        await db.SaveChangesAsync(); return profile.Id;
    }
    private async Task<(BillingFixture Fixture, int PeopleService)> OccupantFixture(AppDbContext db, bool peopleBased = true)
    {
        var f = await BillingAsync(db);
        var catalog = await AddService(db, "Water per person", 80000, type: peopleBased ? CachTinhDichVu.TheoNguoi : CachTinhDichVu.CoDinh);
        await Rooms(db).DatDichVuAsync(1, f.A, catalog, true);
        await db.Database.ExecuteSqlRawAsync("UPDATE cau_hinh_dich_vu SET tu_ngay='2026-01-01'; UPDATE phong_tro SET so_nguoi_toi_da=10; UPDATE hop_dong SET ngay_chot_hang_thang=5");
        return (f, (await db.DichVuToaNhas.FindAsync(catalog))!.DichVuId);
    }
    private async Task<LapHoaDonDichVuViewModel> OccupantInput(AppDbContext db, BillingFixture f, int people, DateOnly period)
    {
        var result = await BillInput(db, f.A, people, period);
        result.Dong.AddRange((await BillInput(db, f.A, f.Service, period)).Dong);
        return result;
    }
    // Billable residents determine full-month per-person fees; current-month arrivals start next month.
    [Theory]
    [InlineData("2026-09-01", 2, 1260000)]
    [InlineData("2026-09-05", 2, 1260000)]
    [InlineData("2026-09-06", 2, 1260000)]
    [InlineData("2026-09-15", 2, 1260000)]
    [InlineData("2026-09-30", 2, 1260000)]
    [InlineData("2026-10-01", 1, 1180000)]
    [InlineData("2026-10-04", 1, 1180000)]
    [InlineData("2026-10-05", 1, 1180000)]
    [InlineData("2026-10-06", 1, 1180000)]
    [InlineData("2026-10-15", 1, 1180000)]
    public async Task OccupantBillingArrivalBoundariesAndMidMonth(string start, int count, long total)
    {
        using var db=Context();var (f,people)=await OccupantFixture(db, peopleBased: true);await AddStay(db,f.A,DateOnly.Parse(start));
        var id=await Issue(db,await OccupantInput(db,f,people,new(2026,10,20)));
        var invoice=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);
        Assert.Equal(count,invoice.SoNguoiTinhPhi);Assert.Equal(new DateOnly(2026,10,5),invoice.NgayChot);Assert.Equal(total,invoice.TongTien);
        var line=invoice.ChiTiet.Single(x=>x.DichVuId==people);Assert.Equal((decimal)count,line.SoLuong);Assert.Equal(80000,line.DonGia);Assert.Equal(count*80000,line.ThanhTien);
        var fixedLine=invoice.ChiTiet.Single(x=>x.DichVuId==f.Service);Assert.Equal(1m,fixedLine.SoLuong);Assert.Equal(100000,fixedLine.ThanhTien);
    }
    [Fact] public async Task OccupantBillingSignerOnlyIgnoresForgedManualCount()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);var input=await OccupantInput(db,f,people,new(2026,10,1));input.SoNguoi=9999;
        var id=await Issue(db,input);var invoice=await db.HoaDons.FindAsync(id);Assert.Equal(1,invoice!.SoNguoiTinhPhi);Assert.Equal(1180000,invoice.TongTien);
    }
    [Fact] public async Task OccupantBillingPerPersonServiceUsesServerCount()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db);await AddStay(db,f.A,new(2026,9,1));
        var input=await OccupantInput(db,f,people,new(2026,10,1));input.SoNguoi=999;
        var id=await Issue(db,input);
        var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);
        var line=Assert.Single(bill.ChiTiet,x=>x.DichVuId==people);
        Assert.Equal(2,bill.SoNguoiTinhPhi);Assert.Equal(2m,line.SoLuong);Assert.Equal(160000,line.ThanhTien);
        Assert.Equal(1260000,bill.TongTien);Assert.Null(line.ChiSoDau);Assert.Null(line.ChiSoCuoi);
    }
    [Fact] public async Task OccupantBillingMultipleRoommatesAndFollowingMonth()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);await AddStay(db,f.A,new(2026,9,15));await AddStay(db,f.A,new(2026,10,15));
        var october=await Issue(db,await OccupantInput(db,f,people,new(2026,10,1)));
        var november=await Issue(db,await OccupantInput(db,f,people,new(2026,11,1)));
        Assert.Equal(1260000,(await db.HoaDons.FindAsync(october))!.TongTien);Assert.Equal(1340000,(await db.HoaDons.FindAsync(november))!.TongTien);
    }
    [Theory][InlineData("2026-09-30",1)][InlineData("2026-10-01",2)][InlineData("2026-10-04",2)][InlineData("2026-10-05",2)][InlineData("2026-10-06",2)][InlineData("2026-10-31",2)]
    public async Task OccupantBillingDepartureMonthRemainsFullyBilled(string end,int expected)
    {
        using var db=Context();var(f,_)=await OccupantFixture(db);await AddStay(db,f.A,new(2026,9,1),DateOnly.Parse(end));
        var result=await new HoaDonDichVuService(db,new(db)).LaySoNguoiAsync(1,f.A,1,new(2026,10,1));Assert.Equal(expected,result.SoNguoi);
    }
    [Theory][InlineData(2027,2,28)][InlineData(2028,2,29)][InlineData(2026,4,30)]
    public async Task OccupantBillingCutoffClampsToMonthEnd(int year,int month,int day)
    {
        using var db=Context();var(f,_)=await OccupantFixture(db);await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET ngay_chot_hang_thang=31");
        var result=await new HoaDonDichVuService(db,new(db)).LaySoNguoiAsync(1,f.A,1,new(year,month,1));Assert.Equal(new DateOnly(year,month,day),result.NgayChot);
    }
    [Fact] public async Task OccupantBillingIssuedSnapshotSurvivesBackdatedAddition()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);var id=await Issue(db,await OccupantInput(db,f,people,new(2026,10,1)));
        await AddStay(db,f.A,new(2026,9,15));
        var live=await new HoaDonDichVuService(db,new(db)).LaySoNguoiAsync(1,f.A,1,new(2026,10,1));Assert.Equal(2,live.SoNguoi);
        var invoice=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);
        Assert.Equal(1,invoice.SoNguoiTinhPhi);Assert.Equal(1180000,invoice.TongTien);Assert.Equal(80000,invoice.ChiTiet.Single(x=>x.DichVuId==people).ThanhTien);
    }
    [Fact] public async Task OccupantBillingForeignOwnerDenied(){using var db=Context();var(f,_)=await OccupantFixture(db);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new HoaDonDichVuService(db,new(db)).LaySoNguoiAsync(2,f.A,1,new(2026,10,1)));}
    [Fact] public async Task OccupantBillingStaleRoomVersionRejected(){using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);var input=await OccupantInput(db,f,people,new(2026,10,1));input.PhienBanPhong=-1;await Assert.ThrowsAsync<InvalidOperationException>(()=>Issue(db,input));Assert.Empty(await db.HoaDons.ToListAsync());}
    [Fact] public async Task OccupantBillingConcurrentIssueCannotDuplicateSnapshots()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);await AddStay(db,f.A,new(2026,9,15));var input=await OccupantInput(db,f,people,new(2026,10,1));
        async Task<bool> Save(){using var writer=Context();try{await Issue(writer,input);return true;}catch(InvalidOperationException){return false;}}
        var results=await Task.WhenAll(Task.Run(Save),Task.Run(Save));Assert.Single(results,x=>x);
        using var check=Context();Assert.Single(await check.HoaDons.ToListAsync());Assert.Equal(2,(await check.HoaDons.SingleAsync()).SoNguoiTinhPhi);
    }
    [Fact] public async Task OccupantBillingAuditFailureRollsBackInvoiceAndLines()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);var input=await OccupantInput(db,f,people,new(2026,10,1));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_bill_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='hoa_don' BEGIN SELECT RAISE(ABORT,'test failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>Issue(db,input));using var check=Context();Assert.Empty(await check.HoaDons.ToListAsync());Assert.Empty(await check.ChiTietHoaDons.ToListAsync());
    }
    [Fact] public async Task OccupantBillingExplicitContractServicesRestrictCharges()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);
        await new HoaDonDichVuService(db,new(db)).GanVaoHopDongAsync(1,f.A,people,new(2026,10,1));
        var rejected = await OccupantInput(db,f,people,new(2026,10,1));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Issue(db,rejected));
        Assert.Empty(await db.HoaDons.ToListAsync());
        var id=await Issue(db,await BillInput(db,f.A,people,new(2026,10,1)));Assert.Equal(1080000,(await db.HoaDons.FindAsync(id))!.TongTien);
    }
    [Fact] public async Task OccupantBillingHttpPreviewAndSavedDetails()
    {
        using var db=Context();var(f,people)=await OccupantFixture(db, peopleBased: true);await AddStay(db,f.A,new(2026,9,15));
        using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
        var page=WebUtility.HtmlDecode(await client.GetStringAsync($"/HoaDonDichVu?toaNhaId=1&hopDongId={f.A}&ngayApDung=2026-10-20"));
        Assert.Contains("2 người",page);Assert.Contains("05/10/2026",page);Assert.Contains("80.000 đ",page);Assert.DoesNotContain("name=\"SoNguoi\"",page);
        var input=await OccupantInput(db,f,people,new(2026,10,1));var fields=new Dictionary<string,string>{["ToaNhaId"]="1",["HopDongId"]=f.A.ToString(),["NgayApDung"]="2026-10-20",["SoNguoi"]="garbage"};
        for(var i=0;i<input.Dong.Count;i++){var row=input.Dong[i];fields[$"Dong[{i}].Chon"]="true";fields[$"Dong[{i}].DichVuId"]=row.DichVuId.ToString();fields[$"Dong[{i}].CauHinhId"]=row.CauHinhId.ToString();fields[$"Dong[{i}].DonGiaDaXem"]=row.DonGiaDaXem.ToString();}
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/HoaDonDichVu/Issue",new FormUrlEncodedContent(fields))).StatusCode);
        var response=await Post(client,"/HoaDonDichVu/Issue",fields);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var details=WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));        Assert.Contains("2 người",details);Assert.Contains("80.000",details);Assert.Contains("1.260.000",details);
        using var other=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(other,"other");Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync(response.Headers.Location)).StatusCode);
    }
}
