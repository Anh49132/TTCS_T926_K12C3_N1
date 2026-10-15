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
[Authorize(Roles="CHU_NHA"),ModuleAccess("HOP_DONG",write:true)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class TraPhongController(AppDbContext db,ITimeProvider clock):Controller
{
    private int Actor=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:0;
    private TraPhongService Service=>new(db,clock);
    public override async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var count=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don','chi_so_dien_nuoc','hop_dong_chi_so_dau_ky','hop_dong_dich_vu','cham_dut_hop_dong')").SingleAsync();
        if(count!=6){Response.StatusCode=503;context.Result=View("ChuaSanSang");return;}await next();
    }
    [HttpGet,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Index()=>View(await(from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id join b in db.ToaNhas on p.ToaNhaId equals b.Id join k in db.KhachThues on h.KhachDungTenId equals k.Id where b.ChuNhaId==Actor && h.TrangThai=="DANG_HIEU_LUC" orderby b.TenToaNha,p.MaPhong select new HopDongTraPhongChon(h.Id,h.MaHopDong,p.MaPhong,b.TenToaNha,k.HoTen)).ToListAsync());
    [HttpGet,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Nhap(int id)
    {
        try{var model=await Service.NhapAsync(Actor,new() { HopDongId=id });return model.HoaDonDaCo.HasValue?RedirectToAction(nameof(ChiTiet),new {id=model.HoaDonDaCo}):View(model);}
        catch(UnauthorizedAccessException){return Forbid();}catch(InvalidOperationException ex){ViewData["Message"]=ex.Message;return View("ThongBao");}
    }
    [HttpPost,ValidateAntiForgeryToken,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Tinh([Bind(Prefix="Input")]TraPhongInput input)
    {
        try{await Service.KiemTraQuyenAsync(Actor,input.HopDongId);if(!ModelState.IsValid)return await Loi(input);var model=await Service.NhapAsync(Actor,input,true);input.DauVanTay=TraPhongService.DauVanTay(model);ModelState.Remove("Input.DauVanTay");return View("ChiTiet",model);}
        catch(UnauthorizedAccessException){return Forbid();}catch(Exception ex)when(ex is InvalidOperationException or OverflowException){ModelState.AddModelError("",ex is InvalidOperationException?ex.Message:"Số tiền vượt giới hạn.");return await Loi(input);}
    }
    [HttpPost,ValidateAntiForgeryToken,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Sua([Bind(Prefix="Input")]TraPhongInput input)
    {try{await Service.KiemTraQuyenAsync(Actor,input.HopDongId);return await Loi(input);}catch(UnauthorizedAccessException){return Forbid();}}
    [HttpPost,ValidateAntiForgeryToken,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> Luu([Bind(Prefix="Input")]TraPhongInput input)
    {
        try{await Service.KiemTraQuyenAsync(Actor,input.HopDongId);if(!ModelState.IsValid)return await Loi(input);var id=await Service.LuuAsync(Actor,input);return RedirectToAction(nameof(ChiTiet),new {id});}
        catch(UnauthorizedAccessException){return Forbid();}catch(Exception ex)when(ex is InvalidOperationException or OverflowException or DbUpdateException or SqliteException){db.ChangeTracker.Clear();ModelState.AddModelError("",ex is InvalidOperationException?ex.Message:"Không thể lưu. Vui lòng kiểm tra dữ liệu và thử lại.");return await Loi(input);}
    }
    [HttpPost,ValidateAntiForgeryToken,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> UocTinh([Bind(Prefix="Input")]TraPhongInput input)
    {
        try{await Service.KiemTraQuyenAsync(Actor,input.HopDongId);if(!ModelState.IsValid)return BadRequest(new {error="Nhập đầy đủ ngày và hai chỉ số hợp lệ."});var model=await Service.NhapAsync(Actor,input,true);return Json(new {total=model.HoaDon!.TongTien.ToString(),lines=model.HoaDon.ChiTiet.Select(x=>new {name=x.TenKhoan,amount=x.ThanhTien.ToString()})});}
        catch(UnauthorizedAccessException){return Forbid();}catch(Exception ex)when(ex is InvalidOperationException or OverflowException){return BadRequest(new {error=ex is InvalidOperationException?ex.Message:"Số tiền vượt giới hạn."});}
    }
    private async Task<IActionResult> Loi(TraPhongInput input)
    {try{var model=await Service.NhapAsync(Actor,new() {HopDongId=input.HopDongId});model.Input=input;return View("Nhap",model);}catch(UnauthorizedAccessException){return Forbid();}catch(InvalidOperationException ex){ViewData["Message"]=ex.Message;return View("ThongBao");}}
    [HttpGet,ModuleAccess("TAI_CHINH",write:true)]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var bill=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleOrDefaultAsync(x=>x.Id==id && x.LoaiHoaDon=="KY_CUOI");if(bill is null)return NotFound();
        try{await Service.KiemTraQuyenAsync(Actor,bill.HopDongId);}catch(UnauthorizedAccessException){return Forbid();}
        var h=await db.HopDongs.AsNoTracking().SingleAsync(x=>x.Id==bill.HopDongId);var p=await db.PhongTros.AsNoTracking().SingleAsync(x=>x.Id==h.PhongId);
        return View(new TraPhongViewModel {ChamDut=await db.ChamDutHopDongs.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==h.Id),HoaDon=bill,MaHopDong=h.MaHopDong,Phong=p.MaPhong,ToaNha=await db.ToaNhas.Where(x=>x.Id==p.ToaNhaId).Select(x=>x.TenToaNha).SingleAsync(),Khach=await db.KhachThues.Where(x=>x.Id==h.KhachDungTenId).Select(x=>x.HoTen).SingleOrDefaultAsync()??"",Input=new() {HopDongId=h.Id,NgayTra=bill.DenNgay}});
    }
}
