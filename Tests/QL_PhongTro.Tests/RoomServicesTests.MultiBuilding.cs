using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Models;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class RoomServicesTests
{
 [Fact]
 public async Task MultipleBuildingsShareServiceAndRejectForeignBuildingAtomically()
 {
  using var connection=Open();
  using var command=connection.CreateCommand();
  command.CommandText="INSERT INTO toa_nha(id,chu_nha_id,ten_toa_nha,dia_chi) VALUES(4,1,'Building C','Test')";
  command.ExecuteNonQuery();
  using var db=Context();
  var service=new DichVuService(db);
  var input=new TaoDichVuViewModel{ToaNhaIds=[1,4,4],TenDichVu="Multi",CachTinh=CachTinhDichVu.CoDinh,DonGia=20000,DonViTinh="phòng / tháng",ApDungMacDinh=true};
  await service.ThemAsync(1,input);
  var prices=await db.CauHinhDichVus.Where(x=>x.DichVu.TenDichVu=="Multi").ToListAsync();
  Assert.Equal(2,prices.Count);
  Assert.Single(prices.Select(x=>x.DichVuId).Distinct());
  Assert.All(prices,x=>Assert.Equal(20000,x.DonGia));
  input.ToaNhaIds=[1,2];input.TenDichVu="Denied";
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ThemAsync(1,input));
  Assert.False(await db.CauHinhDichVus.AnyAsync(x=>x.DichVu.TenDichVu=="Denied"));
 }
 [Fact]
 public async Task SpecificRoomsReceiveServiceAndOtherBuildingsAreRejected()
 {
  using var db=Context();
  var room=await AddRoom(db,"SPECIFIC");
  var other=await AddRoom(db,"UNCHOSEN");
  var input=new TaoDichVuViewModel{ToaNhaIds=[1],ChonPhongCuThe=true,PhongIds=[room.Id,room.Id],TenDichVu="Selected rooms",CachTinh=CachTinhDichVu.CoDinh,DonGia=10000,DonViTinh="phòng / tháng",ApDungMacDinh=true};
  var service=new DichVuService(db);await service.ThemAsync(1,input);
  Assert.True(await db.DichVuPhongs.AnyAsync(x=>x.PhongId==room.Id && x.DichVuToaNha.DichVu.TenDichVu==input.TenDichVu));
  Assert.False(await db.DichVuPhongs.AnyAsync(x=>x.PhongId==other.Id && x.DichVuToaNha.DichVu.TenDichVu==input.TenDichVu));
  Assert.False((await db.DichVuToaNhas.SingleAsync(x=>x.DichVu.TenDichVu==input.TenDichVu)).ApDungMacDinh);
  input.ToaNhaIds=[2];input.TenDichVu="Denied room";
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ThemAsync(2,input));
  Assert.False(await db.CauHinhDichVus.AnyAsync(x=>x.DichVu.TenDichVu=="Denied room"));
 }
 [Fact]
 public async Task ServiceCreationPersistsStatusAndNotesAndRejectsInvalidStatus()
 {
  using var db=Context();var service=new DichVuService(db);
  var input=new TaoDichVuViewModel{ToaNhaIds=[1],TenDichVu="Paused",CachTinh=CachTinhDichVu.CoDinh,DonGia=12000,DonViTinh="phòng / tháng",TrangThai="TAM_NGUNG",GhiChu="  Dọn phòng theo lịch  "};
  await service.ThemAsync(1,input);
  var saved=await db.CauHinhDichVus.Include(x=>x.DichVu).SingleAsync(x=>x.DichVu.TenDichVu=="Paused");
  Assert.False(saved.DangApDung);Assert.Equal("Dọn phòng theo lịch",saved.DichVu.MoTa);
  input.TrangThai="INVALID";
  await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(()=>service.ThemAsync(1,input));
 }
}
