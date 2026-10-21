using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
using System.Net;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests {
 [Fact]
 public async Task InvoiceFlowEditsAndRemovesAdjustmentsWithoutDuplicatingAndAuditsDueDate() {
  using var db=Context();var f=await MonthlyFixture(db);
  var service=new HoaDonDichVuService(db,new DichVuService(db));
  var id=await service.TaoNhapThangAsync(1,1,f.A,2026,10);
  var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==id);
  SuaHoaDonNhapViewModel Input()=>new(){Id=id,PhienBan=bill.PhienBan,
   ChiSo=bill.ChiTiet.Where(x=>x.ChiSoDau.HasValue).Select(x=>new ChiSoNhapInput{Id=x.Id,ChiSoDau=x.ChiSoDau!.Value,ChiSoCuoi=x.ChiSoCuoi!.Value}).ToList()};
  var input=Input();input.Khoan.Add(new(){LoaiKhoan="PHAT_SINH",TenKhoan="Phí",SoTien=200000,GhiChu="Sửa khóa"});
  await service.LuuNhapAsync(1,input);
  var extra=bill.ChiTiet.Single(x=>x.LoaiKhoan=="PHAT_SINH");
  input=Input();input.Khoan.Add(new(){Id=extra.Id,LoaiKhoan="PHAT_SINH",TenKhoan="Phí",SoTien=100000,GhiChu="Điều chỉnh"});
  await service.LuuNhapAsync(1,input);
  Assert.Single(bill.ChiTiet,x=>x.LoaiKhoan=="PHAT_SINH");Assert.Equal(1380000,bill.TongTien);
  input=Input();input.XoaKhoanIds.Add(extra.Id);await service.LuuNhapAsync(1,input);
  Assert.Equal(1280000,bill.TongTien);Assert.DoesNotContain(bill.ChiTiet,x=>x.Id==extra.Id);
  input=Input();input.HanThanhToan=new(2026,12,25);var version=bill.PhienBan;await service.LuuNhapAsync(1,input);
  Assert.Equal(version+1,bill.PhienBan);Assert.Equal(new DateOnly(2026,12,25),bill.HanThanhToan);
  var history=await service.LichSuNhapAsync(1,id);Assert.Equal(new DateOnly(2026,12,25),history[0].Sau!.HanThanhToan);
  input=Input();input.XoaKhoanIds.Add(bill.ChiTiet.First().Id);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuNhapAsync(1,input));
 }
 [Fact]
 public async Task InvoiceFlowManagementSetupAndBatchResultUseRealDataAndEnforceScope() {
  int id,version;
  using(var db=Context()){var f=await MonthlyFixture(db);id=await new HoaDonDichVuService(db,new DichVuService(db)).TaoNhapThangAsync(1,1,f.A,2026,10);version=(await db.HoaDons.FindAsync(id))!.PhienBan;}
  using var factory=BillingWeb();using var client=factory.CreateClient(new(){AllowAutoRedirect=false});await Login(client,"owner");
  Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/HoaDonDichVu/Management")).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/HoaDonDichVu/Management?toaNhaId=99999")).StatusCode);
  Assert.Contains("Thông tin kỳ hóa đơn",WebUtility.HtmlDecode(await client.GetStringAsync("/HoaDonDichVu/Setup?toaNhaId=1&nam=2026&thang=10")));
  var html=await client.GetStringAsync("/HoaDonDichVu/Monthly?toaNhaId=1&nam=2026&thang=10");
  var fields=new Dictionary<string,string>{{"toaNhaId","1"},{"nam","2026"},{"thang","10"},{"selectedInvoiceIds",id.ToString()},
   {$"versions[{id}]",version.ToString()},{"ngayPhatHanh","2026-11-01"},{"hanThanhToan","2026-11-08"},{"xacNhan","true"},
   {"__RequestVerificationToken",FormValue(html,"__RequestVerificationToken")}};
  var response=await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields));
  Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("Results",response.Headers.Location!.ToString());
  var failed=WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
  Assert.Contains("chưa có tài khoản khách thuê hoạt động",failed);
  using(var db=Context()){Assert.Empty(await db.ThongBaoHoaDons.ToListAsync());Assert.Equal("NHAP",(await db.HoaDons.FindAsync(id))!.TrangThai);await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");}
  response=await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields));
  Assert.Contains("Đã phát hành 1 hóa đơn",WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
  var retry=await client.PostAsync("/HoaDonDichVu/PublishMonthlyDrafts",new FormUrlEncodedContent(fields));
  Assert.Equal(HttpStatusCode.Redirect,retry.StatusCode);
  using var verify=Context();Assert.Single(await verify.ThongBaoHoaDons.ToListAsync());
 }
}
