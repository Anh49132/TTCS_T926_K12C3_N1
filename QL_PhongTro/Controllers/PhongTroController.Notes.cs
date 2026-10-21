using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Models;
using System.Security.Claims;
namespace QL_PhongTro.Controllers;
public partial class PhongTroController {
 [HttpPost,ValidateAntiForgeryToken,ModuleAccess("PHONG_TRO",write:true)]
 [RequestSizeLimit(12*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=12*1024*1024)]
 public async Task<IActionResult> LuuGhiChuPhong(int phongId,string? tieuDe,string? noiDung,bool ghim,string? khoaChongTrung,IFormFile? tep,CancellationToken ct) {
  if(CurrentAccountId() is not int accountId)return Forbid();
  var room=await db.PhongTros.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==phongId,ct);if(room==null)return NotFound();
  var building=await db.ToaNhas.AsNoTracking().SingleAsync(x=>x.Id==room.ToaNhaId,ct);
  if(building.ChuNhaId!=accountId&&!IsAdmin)return Forbid();
  tieuDe=tieuDe?.Trim();noiDung=noiDung?.Trim();
  string? error=null;byte[]? bytes=null;string? mime=null;string? name=null;
  if(string.IsNullOrEmpty(tieuDe)||tieuDe.Length>100||string.IsNullOrEmpty(noiDung)||noiDung.Length>1000)error="Nhập tiêu đề tối đa 100 ký tự và nội dung tối đa 1.000 ký tự.";
  if(!Guid.TryParse(khoaChongTrung,out var key))error="Phiên nhập không hợp lệ. Hãy tải lại trang.";
  if(tep!=null&&error==null){
   if(tep.Length<=0||tep.Length>10*1024*1024)error="Tệp đính kèm phải từ 1 byte đến 10 MB.";
   else {
    using var stream=new MemoryStream();await tep.CopyToAsync(stream,ct);bytes=stream.ToArray();
    var extension=Path.GetExtension(tep.FileName).ToLowerInvariant();
    if(extension==".pdf"&&bytes.AsSpan().StartsWith("%PDF-"u8))mime="application/pdf";
    else if(extension==".png"&&bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}))mime="image/png";
    else if((extension==".jpg"||extension==".jpeg")&&bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255)mime="image/jpeg";
    else error="Chỉ nhận PDF, PNG hoặc JPG đúng định dạng.";
    if(mime?.StartsWith("image/")==true){try{var info=SixLabors.ImageSharp.Image.Identify(bytes);if(info.Width>12000||info.Height>12000||((long)info.Width*info.Height)>40000000)error="Ảnh quá lớn (tối đa 40 triệu điểm ảnh).";}catch(Exception ex)when(ex is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException){error="Ảnh không hợp lệ.";}}
    name=Path.GetFileName(tep.FileName.Replace('\\','/'));if(name.Length>150)name=name[..150];
   }
  }
  if(error!=null){TempData["RoomNoteError"]=error;return RedirectToAction(nameof(NhatKyPhong),new{phongId});}
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  var normalizedKey=key.ToString();
  if(await db.GhiChuPhongs.AnyAsync(x=>x.KhoaChongTrung==normalizedKey,ct)){TempData["RoomNoteOk"]="Ghi chú đã được lưu.";return RedirectToAction(nameof(NhatKyPhong),new{phongId});}
  var now=clock?.UtcNow??DateTime.UtcNow;
  var note=new GhiChuPhong{PhongId=phongId,NguoiTaoId=accountId,TieuDe=tieuDe!,NoiDung=noiDung!,Ghim=ghim,NgayTao=now,KhoaChongTrung=normalizedKey,TenTep=name,LoaiTep=mime,NoiDungTep=bytes};
  try {
   db.GhiChuPhongs.Add(note);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  } catch(Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException) {
   await tx.RollbackAsync(CancellationToken.None);
   TempData["RoomNoteError"]="Không thể lưu ghi chú. Hãy tải lại trang và thử lại.";
   return RedirectToAction(nameof(NhatKyPhong),new{phongId});
  }
TempData["RoomNoteOk"]="Đã lưu ghi chú phòng.";
  return RedirectToAction(nameof(NhatKyPhong),new{phongId});
 }
 [HttpGet,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
 public async Task<IActionResult> TepGhiChuPhong(int id,CancellationToken ct){
  if(CurrentAccountId() is not int accountId)return Forbid();
  var reference=await db.GhiChuPhongs.AsNoTracking().Where(x=>x.Id==id).Select(x=>new{x.PhongId}).SingleOrDefaultAsync(ct);if(reference==null)return NotFound();
  var owner=await(from room in db.PhongTros join building in db.ToaNhas on room.ToaNhaId equals building.Id where room.Id==reference.PhongId select building.ChuNhaId).SingleAsync(ct);if(owner!=accountId&&!IsAdmin)return Forbid();
  var note=await db.GhiChuPhongs.AsNoTracking().SingleAsync(x=>x.Id==id,ct);if(note.NoiDungTep==null)return NotFound();Response.Headers.CacheControl="no-store";Response.Headers["X-Content-Type-Options"]="nosniff";
  return File(note.NoiDungTep,note.LoaiTep!,note.TenTep!);
 }
}
