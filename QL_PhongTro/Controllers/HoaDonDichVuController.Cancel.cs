using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

public partial class HoaDonDichVuController
{
    [HttpPost, Authorize(Roles="CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write:true)]
    public async Task<IActionResult> CancelInvoice(HuyHoaDonViewModel model)
    {
        if (!ModelState.IsValid) { TempData["DraftError"]="Nhập lý do hủy, tối đa 1.000 ký tự."; return RedirectToAction(nameof(Details),new {id=model.Id}); }
        try { await invoices.HuyAsync(AccountId,model); TempData["DraftSuccess"]="Đã hủy hóa đơn và giữ nguyên bản cũ để tra cứu."; }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["DraftError"]=ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException) { TempData["DraftError"]="Chưa thể hủy. Dữ liệu đã thay đổi hoặc đang được xử lý; hãy tải lại trang."; }
        return RedirectToAction(nameof(Details),new {id=model.Id});
    }

    [HttpPost, Authorize(Roles="CHU_NHA"), ValidateAntiForgeryToken, ModuleAccess("TAI_CHINH", write:true)]
    public async Task<IActionResult> CreateReplacement(int id,int phienBan)
    {
        if (!ModelState.IsValid) return BadRequest();
        try { return RedirectToAction(nameof(Details),new { id=await invoices.TaoNhapThayTheAsync(AccountId,id,phienBan) }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { TempData["DraftError"]=ex.Message; }
        catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException) { TempData["DraftError"]="Chưa tạo được bản thay thế. Hãy tải lại trang, kiểm tra hóa đơn hiện có."; }
        return RedirectToAction(nameof(Details),new {id});
    }
}
