using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Controllers;

[AllowAnonymous]
public class TimTinController(AppDbContext db, TinDangExpirationService expiration) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [Bind("TuKhoa,PhuongXa,QuanHuyen,GiaToiThieu,GiaToiDa,DienTichToiThieu,DienTichToiDa,SoNguoiToiDa,SapXep,Trang")] TimTinViewModel model,
        CancellationToken cancellationToken)
    {
        await expiration.ExpireAsync(cancellationToken);
        model.TuKhoa = model.TuKhoa?.Trim();
        model.PhuongXa = model.PhuongXa?.Trim();
        model.QuanHuyen = model.QuanHuyen?.Trim();
        if (string.IsNullOrEmpty(model.SapXep)) model.SapXep = "moi-nhat";
        if (model.SapXep is not ("moi-nhat" or "gia-tang" or "gia-giam"))
            ModelState.AddModelError(nameof(model.SapXep), "Cách sắp xếp không hợp lệ.");
        if (model.GiaToiThieu > model.GiaToiDa)
            ModelState.AddModelError(nameof(model.GiaToiDa), "Giá thuê tối thiểu không được lớn hơn giá thuê tối đa.");
        if (model.DienTichToiThieu > model.DienTichToiDa)
            ModelState.AddModelError(nameof(model.DienTichToiDa), "Diện tích tối thiểu không được lớn hơn diện tích tối đa.");
        // Districts come from the existing building data; no separate catalogue is needed.
        model.QuanHuyens = await db.ToaNhas.AsNoTracking()
            .Where(t => t.QuanHuyen != null && t.QuanHuyen.Trim() != "")
            .Select(t => t.QuanHuyen!.Trim()).Distinct().OrderBy(q => q)
            .ToListAsync(cancellationToken);
        model.PhuongXas = await db.ToaNhas.AsNoTracking()
            .Where(t => t.PhuongXa != null && t.PhuongXa.Trim() != ""
                && (string.IsNullOrEmpty(model.QuanHuyen)
                    || (t.QuanHuyen != null && t.QuanHuyen.Trim() == model.QuanHuyen)))
            .Select(t => t.PhuongXa!.Trim()).Distinct().OrderBy(q => q)
            .ToListAsync(cancellationToken);
        model.SoNguoiOptions = await db.PhongTros.AsNoTracking()
            .Where(p => p.SoNguoiToiDa > 0)
            .Select(p => p.SoNguoiToiDa).Distinct().OrderBy(n => n)
            .ToListAsync(cancellationToken);

        // Do not create or upgrade a local database from a search request.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            using var command = db.Database.GetDbConnection().CreatePortableCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'tin_dang'";
            model.SchemaReady = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        if (!string.IsNullOrEmpty(model.QuanHuyen) && !model.QuanHuyens.Contains(model.QuanHuyen))
        {
            ModelState.AddModelError(nameof(model.QuanHuyen), "Quận/huyện không thuộc danh sách hiện có.");
        }
        if (!string.IsNullOrEmpty(model.PhuongXa) && !model.PhuongXas.Contains(model.PhuongXa))
            ModelState.AddModelError(nameof(model.PhuongXa), "Phường/xã không thuộc khu vực đã chọn.");
        if (model.SoNguoiToiDa.HasValue && !model.SoNguoiOptions.Contains(model.SoNguoiToiDa.Value))
            ModelState.AddModelError(nameof(model.SoNguoiToiDa), "Số người ở tối đa không thuộc danh sách hiện có.");
        if (!model.SchemaReady || !ModelState.IsValid) return View(model);

        // Expiration is a UTC timestamp; equality is still within the validity period.
        var now = DateTime.UtcNow;
        var query = from tin in db.Set<TinDang>().AsNoTracking()
                    join phong in db.PhongTros.AsNoTracking() on tin.PhongId equals phong.Id
                    join toa in db.ToaNhas.AsNoTracking() on phong.ToaNhaId equals toa.Id
                    where tin.TrangThai == "DANG_HIEN_THI"
                        && tin.NgayHetHan != null && tin.NgayHetHan >= now
                        && phong.TrangThai == "TRONG" && toa.DangHoatDong
                    select new TinTimKiem
                    {
                        Id = tin.Id, NgayDang = tin.NgayDang,
                        TenChuNha = db.TaiKhoans.Where(a=>a.Id==toa.ChuNhaId).Select(a=>a.HoTen).FirstOrDefault(),
                        DienThoaiChuNha = db.TaiKhoans.Where(a=>a.Id==toa.ChuNhaId).Select(a=>a.SoDienThoai).FirstOrDefault(),
                        TieuDe = tin.TieuDe, NoiDung = tin.NoiDung ?? phong.MoTa, MaPhong = phong.MaPhong, DiaChi = toa.DiaChi,
                        PhuongXa = toa.PhuongXa == null ? null : toa.PhuongXa.Trim(),
                        QuanHuyen = toa.QuanHuyen == null ? null : toa.QuanHuyen.Trim(),
                        GiaThue = phong.GiaThue, DienTich = phong.DienTich,
                        SoNguoiToiDa = phong.SoNguoiToiDa,
                        AnhDaiDien = db.AnhPhongs.Where(image => image.PhongId == phong.Id)
                            .OrderBy(image => image.ThuTu).Select(image => image.DuongDanAnhNho).FirstOrDefault()
                    };
        if (!string.IsNullOrEmpty(model.TuKhoa))
        {
            var keyword = model.TuKhoa;
            query = query.Where(t => t.TieuDe.Contains(keyword) || (t.NoiDung != null && t.NoiDung.Contains(keyword))
                || t.MaPhong.Contains(keyword) || t.DiaChi.Contains(keyword));
        }
        if (!string.IsNullOrEmpty(model.PhuongXa))
            query = query.Where(t => t.PhuongXa == model.PhuongXa);
        if (!string.IsNullOrEmpty(model.QuanHuyen))
            query = query.Where(t => t.QuanHuyen == model.QuanHuyen);
        if (model.GiaToiThieu.HasValue)
            query = query.Where(t => t.GiaThue >= model.GiaToiThieu.Value);
        if (model.GiaToiDa.HasValue)
            query = query.Where(t => t.GiaThue <= model.GiaToiDa.Value);
        if (model.DienTichToiThieu.HasValue)
            query = query.Where(t => t.DienTich >= model.DienTichToiThieu.Value);
        if (model.DienTichToiDa.HasValue)
            query = query.Where(t => t.DienTich <= model.DienTichToiDa.Value);
        if (model.SoNguoiToiDa.HasValue)
            query = query.Where(t => t.SoNguoiToiDa == model.SoNguoiToiDa.Value);
        model.TongKetQua = await query.CountAsync(cancellationToken);
        if (model.TongKetQua == 0)
        {
            // Widen each supplied boundary by 500,000 VND; keep open boundaries open.
            const long step = 500_000;
            model.GiaGoiYToiThieu = model.GiaToiThieu.HasValue
                ? Math.Max(0, model.GiaToiThieu.Value - step) : null;
            model.GiaGoiYToiDa = model.GiaToiDa.HasValue
                ? Math.Min(long.MaxValue - step, model.GiaToiDa.Value) + step : null;
            model.CoGoiYGia = model.GiaGoiYToiThieu != model.GiaToiThieu
                || model.GiaGoiYToiDa != model.GiaToiDa;
        }
        model.Trang = Math.Clamp(model.Trang, 1, Math.Max(1, model.TongTrang));
        var ordered = model.SapXep switch
        {
            "gia-tang" => query.OrderBy(t => t.GiaThue).ThenByDescending(t => t.Id),
            "gia-giam" => query.OrderByDescending(t => t.GiaThue).ThenByDescending(t => t.Id),
            _ => query.OrderByDescending(t => t.NgayDang).ThenByDescending(t => t.Id)
        };
        model.TinDangs = await ordered.Skip((model.Trang - 1) * TimTinViewModel.KichThuocTrang)
            .Take(TimTinViewModel.KichThuocTrang).ToListAsync(cancellationToken);
        return View(model);
    }
}
