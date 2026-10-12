using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QL_PhongTro.Authorization;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace QL_PhongTro.Controllers;

[Authorize(Roles = "CHU_NHA,QUAN_LY"), ModuleAccess("DIEN_NUOC")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChiSoDienNuocController(ChiSoDienNuocService readings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? toaNhaId, CancellationToken ct, string? ky = null)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return Forbid();
        DateOnly? selected = null;
        if (!string.IsNullOrWhiteSpace(ky))
        {
            if (!DateOnly.TryParseExact(ky + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date) || date.Year < 2000 || date.Year > 2100) return BadRequest();
            selected = date;
        }
        try { return View(await readings.DanhSachAsync(accountId, toaNhaId, ct, selected)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost,ValidateAntiForgeryToken,ModuleAccess("DIEN_NUOC",write:true)]
    public async Task<IActionResult> Save(LuuChiSoInput input,CancellationToken ct)
    {
        if(!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)) return Forbid();
        try
        {
            var errors=ModelState.Where(x=>x.Value!.Errors.Count>0).ToDictionary(x=>x.Key,x=>new[]{"Giá trị nhập không hợp lệ."});
            if(errors.Count==0) errors=await readings.LuuAsync(actor,input,ct);
            if(errors.Count==0) {TempData["MeterSuccess"]="Đã lưu chỉ số phòng vừa chọn.";return RedirectToAction(nameof(Index),null,new{toaNhaId=input.ToaNhaId},$"meter-room-{input.PhongId}");}
            var model=await readings.DanhSachAsync(actor,input.ToaNhaId,ct);
            if(!model.Phongs.Any(x=>x.PhongId==input.PhongId && x.HopDongId==input.HopDongId)) return Forbid();
            model.Loi=errors;model.Input=input;return View("Index",model);
        }
        catch(UnauthorizedAccessException){return Forbid();}
        catch(Exception ex) when(ex is DbUpdateException or SqliteException)
        {
            ModelState.Clear();var model=await readings.DanhSachAsync(actor,input.ToaNhaId,ct);
            model.Input=input;model.Loi=new(){["Phong"]=["Không thể lưu vì dữ liệu đã thay đổi hoặc lỗi ghi dữ liệu. Hãy tải lại trang."]};
            return View("Index",model);
        }
    }
}
