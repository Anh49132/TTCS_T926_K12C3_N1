using System.Security.Claims;
using System.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

[Authorize(Roles="CHU_NHA,QUAN_LY,ADMIN,KHACH_THUE")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PersonalAccountController(AppDbContext db,PersonalAvatarStore avatars,ITimeProvider clock):Controller
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private Task<QL_PhongTro.Models.TaiKhoan?> Current()=>db.TaiKhoans.SingleOrDefaultAsync(x=>x.Id==Actor && x.DangHoatDong && !x.IsDeleted);
    private void Display(PersonalAccountViewModel model,QL_PhongTro.Models.TaiKhoan account)
    { model.Email=account.Email;model.Role=ManagedAccountsViewModel.RoleName(account.VaiTro); }
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var account=await Current();if(account is null)return Forbid();
        var model=new PersonalAccountViewModel{HoTen=account.HoTen,SoDienThoai=account.SoDienThoai,OriginalUpdated=account.NgayCapNhat};
        Display(model,account);return View(model);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PersonalAccountViewModel model)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        var account=await Current();if(account is null)return Forbid();Display(model,account);
        model.HoTen=(model.HoTen??"").Trim();model.SoDienThoai=(model.SoDienThoai??"").Trim();
        if(string.IsNullOrWhiteSpace(model.HoTen))ModelState.AddModelError(nameof(model.HoTen),"Nhập họ tên.");
        if(model.OriginalUpdated!=account.NgayCapNhat)ModelState.AddModelError("","Thông tin đã thay đổi. Hãy tải lại trang trước khi lưu.");
        if(await db.TaiKhoans.AnyAsync(x=>!x.IsDeleted && x.Id!=Actor && x.SoDienThoai==model.SoDienThoai))
            ModelState.AddModelError(nameof(model.SoDienThoai),"Số điện thoại đã được sử dụng.");
        if(!ModelState.IsValid)return View(model);
        account.HoTen=model.HoTen;account.SoDienThoai=model.SoDienThoai;account.NgayCapNhat=clock.UtcNow;
        var profile=await db.KhachThues.SingleOrDefaultAsync(x=>x.TaiKhoanId==Actor);
        if(profile is not null){profile.HoTen=model.HoTen;profile.SoDienThoai=model.SoDienThoai;}
        try {await db.SaveChangesAsync();await tx.CommitAsync();}
        catch(Exception ex) when(ex is DbUpdateException or SqliteException)
        { ModelState.AddModelError("","Chưa lưu được thông tin. Số điện thoại có thể đã được dùng hoặc dữ liệu đã đổi. Hãy tải lại trang.");return View(model); }
        TempData["PersonalSuccess"]="Đã cập nhật thông tin tài khoản.";
        return RedirectToAction(nameof(Edit));
    }
    [HttpGet]
    public async Task<IActionResult> Avatar(int? id)
    {
        var account=await Current();if(account is null || id.HasValue && id!=Actor)return Forbid();
        Response.Headers["X-Content-Type-Options"]="nosniff";
        try {return File(new FileStream(avatars.FilePath(Actor),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete),"image/png");}
        catch(Exception ex) when(ex is FileNotFoundException or DirectoryNotFoundException)
        {
            var initials=SecurityElement.Escape(string.Concat(account.HoTen.Split(' ',StringSplitOptions.RemoveEmptyEntries).TakeLast(2).Select(x=>System.Globalization.StringInfo.GetNextTextElement(x))).ToUpperInvariant());
            return Content($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"128\" height=\"128\" viewBox=\"0 0 128 128\"><rect width=\"128\" height=\"128\" rx=\"64\" fill=\"#E9EAEC\"/><text x=\"64\" y=\"76\" font-family=\"Arial,sans-serif\" font-size=\"40\" text-anchor=\"middle\" fill=\"#30343B\">{initials}</text></svg>","image/svg+xml");
        }
    }
    [HttpPost,ValidateAntiForgeryToken,RequestSizeLimit(6*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
    public async Task<IActionResult> Avatar(IFormFile? image)
    {
        if(await Current() is null)return Forbid();
        try {if(image is null)throw new InvalidDataException();await avatars.SaveAsync(Actor,image);TempData["PersonalSuccess"]="Đã cập nhật ảnh đại diện.";}
        catch(InvalidDataException){TempData["PersonalError"]="Chọn ảnh JPG/PNG hợp lệ, tối đa 5MB và 50 megapixel.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){TempData["PersonalError"]="Chưa lưu được ảnh. Vui lòng thử lại.";}
        return RedirectToAction(nameof(Edit));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        if(await Current() is null)return Forbid();
        try {avatars.Delete(Actor);TempData["PersonalSuccess"]="Đã xóa ảnh đại diện.";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){TempData["PersonalError"]="Chưa xóa được ảnh. Vui lòng thử lại.";}
        return RedirectToAction(nameof(Edit));
    }
}
