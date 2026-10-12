using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Controllers;
public partial class HoaDonDichVuController
{
    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Management(int? toaNhaId, int? nam, int? thang, string? trangThai, string? q, int page = 1)
    {
        var today = DateOnly.FromDateTime(Clock.UtcNow.AddHours(7));
        var model = new QuanLyHoaDonViewModel { ToaNhaId = toaNhaId, Nam = nam, Thang = thang, TrangThai = trangThai, Q = q };
        model.ToaNhas = await db.ToaNhas.AsNoTracking().Where(b => b.ChuNhaId == AccountId)
            .OrderBy(b => b.TenToaNha).Select(b => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(b.TenToaNha,b.Id.ToString())).ToListAsync();
        if (toaNhaId.HasValue && !model.ToaNhas.Any(b => b.Value == toaNhaId.Value.ToString())) return Forbid();
        foreach(var b in model.ToaNhas) b.Selected = b.Value == toaNhaId?.ToString();
        if (!await invoices.SanSangAsync()) return View(model);
        model.SanSang = true;
        var rows = await (from bill in db.HoaDons.AsNoTracking()
            join contract in db.HopDongs on bill.HopDongId equals contract.Id
            join room in db.PhongTros on contract.PhongId equals room.Id
            join building in db.ToaNhas on room.ToaNhaId equals building.Id
            join tenant in db.KhachThues on contract.KhachDungTenId equals tenant.Id
            where building.ChuNhaId == AccountId
            select new DongQuanLyHoaDon { Id=bill.Id, PhienBan=bill.PhienBan, ToaNhaId=building.Id, MaHoaDon=bill.MaHoaDon,
                MaPhong=room.MaPhong, TenKhach=tenant.HoTen, Nam=bill.Nam, Thang=bill.Thang,
                TrangThai=bill.TrangThai, NgayPhatHanh=bill.NgayPhatHanhNghiepVu, HanThanhToan=bill.HanThanhToan,
                TongTien=bill.TongTien,
                DaThu=db.ThanhToans.Where(t=>t.HoaDonId==bill.Id && t.TrangThai=="DA_XAC_NHAN").Sum(t=>(long?)t.SoTien) ?? 0
            }).ToListAsync();
        foreach(var row in rows) row.QuaHan = row.TrangThai=="DA_PHAT_HANH" && row.DaThu<row.TongTien && row.HanThanhToan<today;
        model.SoNhap=rows.Count(r=>r.TrangThai=="NHAP");
        model.SoCho=rows.Count(r=>r.TrangThai=="DA_PHAT_HANH" && r.DaThu<r.TongTien);
        model.SoQuaHan=rows.Count(r=>r.QuaHan);
        model.DaThuThang=await (from t in db.ThanhToans join h in db.HoaDons on t.HoaDonId equals h.Id
            join c in db.HopDongs on h.HopDongId equals c.Id join r in db.PhongTros on c.PhongId equals r.Id
            join b in db.ToaNhas on r.ToaNhaId equals b.Id
            where b.ChuNhaId==AccountId && t.TrangThai=="DA_XAC_NHAN"
                && t.NgayThanhToan>=new DateOnly(today.Year,today.Month,1) && t.NgayThanhToan<=today
            select (long?)t.SoTien).SumAsync() ?? 0;
        var filtered=rows.Where(r=>(!toaNhaId.HasValue || r.ToaNhaId==toaNhaId)
            && (!nam.HasValue || r.Nam==nam) && (!thang.HasValue || r.Thang==thang)
            && (string.IsNullOrEmpty(trangThai) || r.TrangThai==trangThai || trangThai=="QUA_HAN" && r.QuaHan)
            && (string.IsNullOrWhiteSpace(q) || (r.MaHoaDon+" "+r.MaPhong+" "+r.TenKhach).Contains(q.Trim(),StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r=>r.Nam).ThenByDescending(r=>r.Thang).ThenByDescending(r=>r.Id).ToList();
        model.Total=filtered.Count; model.Page=Math.Clamp(page,1,Math.Max(1,(model.Total+9)/10));
        model.Rows=filtered.Skip((model.Page-1)*10).Take(10).ToList();
        return View(model);
    }
    [HttpGet, Authorize(Roles = "CHU_NHA")]
    public async Task<IActionResult> Setup(int? toaNhaId,int? nam,int? thang,DateOnly? ngayPhatHanh,DateOnly? hanThanhToan)
    {
        var result=await Monthly(toaNhaId,nam,thang,ngayPhatHanh,hanThanhToan);
        return result is ViewResult view ? View("Setup",view.Model) : result;
    }
}
