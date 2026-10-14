using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;

[Authorize(Roles="CHU_NHA,KHACH_THUE,ADMIN"),ModuleAccess("HOP_DONG")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed partial class TatToanController(AppDbContext db,ITimeProvider clock):Controller
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private TatToanService Service=>new(db,clock);
    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        if(!await Service.SanSangAsync()) { Response.StatusCode=503;context.Result=View("ChuaSanSang");return; }
        await next();
    }
    [HttpGet,ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> Index()
    {
        var owner=User.IsInRole("CHU_NHA");
        var admin=User.IsInRole("ADMIN");
        var profiles=db.KhachThues.Where(x=>x.TaiKhoanId==Actor).Select(x=>x.Id);
        var rows=await(from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id
            where (admin || (owner? b.ChuNhaId==Actor : h.TrangThai!="NHAP" && h.TrangThai!="DA_HUY" &&
                ((h.KhachDungTenId!=null && profiles.Contains(h.KhachDungTenId.Value)) || db.NguoiOGheps.Any(g=>g.HopDongId==h.Id && profiles.Contains(g.KhachThueId)))))
                && (owner? db.HoaDons.Any(i=>i.HopDongId==h.Id && i.LoaiHoaDon=="KY_CUOI" && i.TrangThai!="DA_HUY") || db.BangTatToans.Any(t=>t.HopDongId==h.Id):db.BangTatToans.Any(t=>t.HopDongId==h.Id))
            orderby b.TenToaNha,p.MaPhong
            select new TatToanChon(h.Id,h.MaHopDong,p.MaPhong,b.TenToaNha,db.BangTatToans.Any(t=>t.HopDongId==h.Id))).ToListAsync();
        return View(rows);
    }
    [HttpGet,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Nhap(int id)
    {
        try { return View(await Service.NhapAsync(Actor,id)); }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(InvalidOperationException ex) { ViewData["Message"]=ex.Message;return View("ThongBao"); }
    }
    [HttpGet,ModuleAccess("TAI_CHINH")]
    public async Task<IActionResult> ChiTiet(int id)
    {
        try { var model=await Service.XemAsync(Actor,id);return model.XacNhan is not null?View("KetQua",model):View(model); }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(InvalidOperationException ex) { ViewData["Message"]=ex.Message;return View("ThongBao"); }
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Luu([Bind(Prefix="Input")]TatToanInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor,input.HopDongId,true);
            if(!ModelState.IsValid)return await LoiAsync(input);
            await Service.LuuAsync(Actor,input);
            TempData["TatToanSuccess"]="Đã lưu bảng tất toán nháp. Khách thuê có thể xem cùng số liệu.";
            return RedirectToAction(nameof(ChiTiet),new { id=input.HopDongId });
        }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(Exception ex)when(ex is InvalidOperationException or DbUpdateException or SqliteException or OverflowException)
        {
            db.ChangeTracker.Clear();
            ModelState.AddModelError("",ex is InvalidOperationException?ex.Message:"Không thể lưu do dữ liệu đang thay đổi hoặc số tiền vượt giới hạn. Không có thay đổi nào được lưu.");
            return await LoiAsync(input);
        }
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> DuTinh([Bind(Prefix="Input")]TatToanInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor,input.HopDongId,true);
            if(!ModelState.IsValid)return await LoiAsync(input);
            return View("Nhap",await Service.NhapAsync(Actor,input.HopDongId,input,true));
        }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(Exception ex)when(ex is InvalidOperationException or OverflowException)
        { ModelState.AddModelError("",ex is InvalidOperationException?ex.Message:"Số tiền vượt giới hạn hỗ trợ.");return await LoiAsync(input); }
    }
    [HttpPost,ValidateAntiForgeryToken,Authorize(Roles="CHU_NHA"),ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> UocTinh([Bind(Prefix="Input")]TatToanInput input)
    {
        try
        {
            await Service.KiemTraQuyenAsync(Actor,input.HopDongId,true);
            if(!ModelState.IsValid)return BadRequest(new { error="Nhập tên và số tiền hợp lệ cho các khoản hư hỏng." });
            var model=await Service.NhapAsync(Actor,input.HopDongId,input,true);
            return Json(new { huHong=model.Bang.TienHuHong.ToString(),phat=model.Bang.TienPhat.ToString(),tongKhauTru=model.Bang.TongKhauTru.ToString(),hoan=model.Bang.TienPhaiHoan.ToString(),traThem=model.Bang.TienKhachTraThem.ToString() });
        }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(Exception ex)when(ex is InvalidOperationException or OverflowException)
        { return BadRequest(new { error=ex is InvalidOperationException?ex.Message:"Số tiền vượt giới hạn hỗ trợ." }); }
    }
    private async Task<IActionResult> LoiAsync(TatToanInput input)
    {
        try { var model=await Service.NhapAsync(Actor,input.HopDongId);model.Input=input;return View("Nhap",model); }
        catch(UnauthorizedAccessException) { return Forbid(); }
        catch(InvalidOperationException ex) { ViewData["Message"]=ex.Message;return View("ThongBao"); }
    }
}
