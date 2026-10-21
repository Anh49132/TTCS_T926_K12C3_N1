using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

public sealed partial class TatToanController
{
    [HttpGet,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> XacNhan(int id)
    {
        try{return View(await Service.KiemTraChotAsync(Actor,id));}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(InvalidOperationException ex){ViewData["Message"]=ex.Message;return View("ThongBao");}
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> XemTruocXacNhan([Bind(Prefix="Confirm")]XacNhanTatToanInput input)
    {
        try
        {
            var model=await Service.KiemTraChotAsync(Actor,input.HopDongId);
            if(!ModelState.IsValid || !input.DaKiemTra){ModelState.AddModelError("","Cần xác nhận đã kiểm tra toàn bộ các khoản tiền.");return View("XacNhan",model);}
            if(input.PhienBan!=model.Confirm.PhienBan || input.DauVanTay!=model.Confirm.DauVanTay)
                throw new InvalidOperationException("Số liệu đã thay đổi. Hãy mở lại bước kiểm tra để đối chiếu.");
            model.Confirm.DaKiemTra=true;ViewData["ShowConfirmation"]=true;return View("XacNhan",model);
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(InvalidOperationException ex){Response.StatusCode=409;ViewData["Message"]=ex.Message;return View("ThongBao");}
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> HoanTat([Bind(Prefix="Confirm")]XacNhanTatToanInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor,input.HopDongId,true);
            if(!ModelState.IsValid)return BadRequest();
            await Service.HoanTatAsync(Actor,input);return RedirectToAction(nameof(KetQua),new {id=input.HopDongId});
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception ex)when(ex is InvalidOperationException or OverflowException or DbUpdateException or SqliteException)
        {
            db.ChangeTracker.Clear();Response.StatusCode=409;
            ViewData["Message"]=ex is InvalidOperationException?ex.Message:"Không thể xác nhận. Mọi thay đổi đã được giữ nguyên; hãy tải lại để kiểm tra.";
            return View("ThongBao");
        }
    }
    [HttpGet,ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> KetQua(int id)
    {
        try{var model=await Service.XemAsync(Actor,id);if(model.XacNhan is null)return RedirectToAction(nameof(ChiTiet),new {id});return View(model);}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(InvalidOperationException ex){ViewData["Message"]=ex.Message;return View("ThongBao");}
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> HoanCoc([Bind(Prefix="Refund")]HoanCocTatToanInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor,input.HopDongId,true);
            var raw=Request.Form["Refund.SoTien"].ToString();
            if(!ModelState.IsValid || raw.Length==0 || raw.Any(c=>c<'0'||c>'9'))throw new InvalidOperationException("Nhập số tiền hoàn bằng số nguyên đồng.");
            await Service.HoanCocAsync(Actor,input);TempData["RefundSuccess"]="Đã ghi nhận tiền cọc thực tế đã hoàn. Bảng tất toán đã chốt được giữ nguyên.";
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception ex)when(ex is InvalidOperationException or OverflowException or DbUpdateException or SqliteException)
        {db.ChangeTracker.Clear();TempData["RefundError"]=ex is InvalidOperationException?ex.Message:"Chưa ghi được hoàn cọc. Vui lòng tải lại và thử cùng phiên để tránh trùng.";}
        return RedirectToAction(nameof(KetQua),new {id=input.HopDongId});
    }
}
