using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
using UglyToad.PdfPig;

namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TatToanPdfConfirmedBothRolesDownloadAndForeignDenied(bool fine)
    {
        using var db=Context();var contract=await XacNhanFixture(db,5000000,fine);
        var service=new TatToanService(db,TatToanClock());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PdfAsync(1,contract));
        await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));
        var owner=await service.PdfAsync(1,contract);var tenant=await service.PdfAsync(3,contract);
        Assert.StartsWith("Bang-tat-toan-",owner.FileName);Assert.EndsWith(".pdf",owner.FileName);
        Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(owner.Content,0,5));
        Assert.True(owner.Content.Length>10000);Assert.True(tenant.Content.Length>10000);
        using var document=PdfDocument.Open(owner.Content);
        string Text(byte[] bytes) {using var pdf=PdfDocument.Open(bytes);return string.Concat(pdf.GetPages().Select(x=>x.Text));}
        var text=Text(owner.Content);Assert.Equal(text,Text(tenant.Content));
        Assert.Contains("BẢNG TẤT TOÁN HỢP ĐỒNG",text);Assert.Contains("Đã kết thúc",text);
        Assert.Contains("Thay khóa",text);Assert.Contains("450.000",text);
        Assert.Contains(fine?"1.950.000":"1.550.000",text);
        Assert.Contains("Khách chủ động chuyển đi",text);
        if(fine)Assert.Contains("3.500.000",text);else Assert.Contains("Không · 0 đ",text);
        if(fine)
        {
            var saved=await service.XemAsync(1,contract);
            await new GhiThuHoaDonService(db,TatToanClock()).GhiAsync(1,new() {HoaDonId=saved.Bang.HoaDonCuoiId,SoTien=1950000,NgayThu=new(2026,10,14),HinhThuc="TIEN_MAT",KhoaChongTrung=Guid.NewGuid().ToString("N")});
            Assert.Equal(text,Text((await service.PdfAsync(3,contract)).Content));
        }
        else
        {
            await service.HoanCocAsync(1,new() {HopDongId=contract,SoTien=500000,HinhThuc="TIEN_MAT",DaHoan=true,KhoaChongTrung=Guid.NewGuid().ToString("N")});
            Assert.Equal(text,Text((await service.PdfAsync(3,contract)).Content));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.PdfAsync(2,contract));
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new() {AllowAutoRedirect=false});
        foreach(var role in new[]{"owner","tenant"})
        {
            await Login(client,role);
            var page=await client.GetStringAsync($"/TatToan/ChiTiet/{contract}");Assert.Contains($"/TatToan/TaiPdf/{contract}",page);
            var response=await client.GetAsync($"/TatToan/TaiPdf/{contract}");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            Assert.Equal("application/pdf",response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("attachment",response.Content.Headers.ContentDisposition!.DispositionType);
            Assert.True(response.Headers.CacheControl!.NoStore);Assert.Equal("nosniff",response.Headers.GetValues("X-Content-Type-Options").Single());
        }
        await Login(client,"other");Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/TatToan/TaiPdf/{contract}")).StatusCode);
    }

    [Fact]
    public async Task TatToanPdfIncludesActualDayRentElectricityAndWaterSnapshots()
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE chi_so_dien_nuoc SET tu_ngay='2026-09-01',den_ngay='2026-09-30'; UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1");db.ChangeTracker.Clear();
        var handover=new TraPhongService(db,TatToanClock());
        var input=new QL_PhongTro.ViewModels.TraPhongInput {HopDongId=f.A,NgayTra=new(2026,10,13),ChiSoDien=224,ChiSoNuoc=30,LyDo="THOA_THUAN"};
        input.DauVanTay=TraPhongService.DauVanTay(await handover.NhapAsync(1,input,true));await handover.LuuAsync(1,input);
        var service=new TatToanService(db,TatToanClock());await service.LuuAsync(1,(await service.NhapAsync(1,f.A)).Input);
        await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,f.A)));
        var pdf=await service.PdfAsync(3,f.A);using var document=PdfDocument.Open(pdf.Content);
        var text=string.Concat(document.GetPages().Select(x=>x.Text));
        foreach(var value in new[]{"Tiền phòng","Điện","Nước","13/31 ngày","13/10/2026","795.355","Đã kết thúc"})Assert.Contains(value,text);
    }

    [Fact]
    public async Task TatToanPdfCanRenderManyVietnameseDamageRowsAcrossPages()
    {
        using var db=Context();var contract=await XacNhanFixture(db);
        var service=new TatToanService(db,TatToanClock());var model=await service.NhapAsync(1,contract);
        model.Input.KhauTru=Enumerable.Range(1,40).Select(i=>new QL_PhongTro.ViewModels.KhauTruInput {TenKhoan=$"Sửa chữa cửa sổ phòng ngủ số {i}",SoTien=1000,GhiChu="Đối chiếu hư hỏng bàn giao: tiếng Việt đầy đủ dấu."}).ToList();
        await service.LuuAsync(1,model.Input);await service.HoanTatAsync(1,DongY(await service.KiemTraChotAsync(1,contract)));
        var pdf=await service.PdfAsync(3,contract);Assert.True(pdf.Content.Length>10000);
        using var document=PdfDocument.Open(pdf.Content);Assert.True(document.NumberOfPages>1);
        var text=string.Concat(document.GetPages().Select(x=>x.Text));
        Assert.Contains("Sửa chữa cửa sổ phòng ngủ số 40",text);Assert.Contains("tiếng Việt đầy đủ dấu",text);
    }
}
