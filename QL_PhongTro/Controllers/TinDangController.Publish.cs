using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Authorization;
using QL_PhongTro.Models;

namespace QL_PhongTro.Controllers;

public partial class TinDangController
{
    [Authorize(Roles = "CHU_NHA,QUAN_LY"), ModuleAccess("TIN_DANG", write: true), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dang(int id)
    {
        try
        {
            await db.Database.OpenConnectionAsync();
            await using var sqlite = db.Database.GetDbConnection().BeginPortableTransaction(deferred: false);
            await using var transaction = await db.Database.UseTransactionAsync(sqlite);
            var listing = await db.TinDangs.SingleOrDefaultAsync(x => x.Id == id);
            if (listing is null) return NotFound();
            var room = await db.PhongTros.AsNoTracking().SingleAsync(x => x.Id == listing.PhongId);
            var building = await db.ToaNhas.AsNoTracking().SingleAsync(x => x.Id == room.ToaNhaId);
            if (building.ChuNhaId != AccountId && !(User.IsInRole("QUAN_LY") && building.QuanLyId == AccountId)) return Forbid();
            // A repeated request must not renew the publication date, expiry or audit.
            if (listing.TrangThai == "DANG_HIEN_THI")
            {
                TempData["TinDangOk"] = "Tin đã được đăng. Không tạo thêm lượt đăng.";
                return RedirectToAction(nameof(QuanLy));
            }
            if (listing.TrangThai != "NHAP")
            {
                TempData["TinDangError"] = "Tin không còn ở trạng thái Nháp. Hãy tải lại danh sách.";
                return RedirectToAction(nameof(QuanLy));
            }
            var errors = await KiemTraBatNhapAsync(listing, room, building, listing.TieuDe, listing.NoiDung);
            if (errors.Count > 0)
            {
                TempData["TinDangError"] = "Chưa thể đăng tin: " + string.Join(" ", errors);
                return RedirectToAction(nameof(QuanLy));
            }
            var now = DateTime.UtcNow;
            listing.TrangThai = "DANG_HIEN_THI";
            listing.NgayDang = now;
            listing.NgayHetHan = now.AddDays(30);
            listing.NguoiDangId = AccountId;
            await db.SaveChangesAsync(); // Includes the existing transactional audit.
            await transaction!.CommitAsync();
            TempData["TinDangOk"] = "Đã đăng tin. Tin đang hiển thị với khách tìm phòng.";
        }
        catch (DbUpdateException)
        {
            TempData["TinDangError"] = "Không thể đăng tin. Dữ liệu chưa được thay đổi; hãy tải lại và thử lại.";
        }
        catch (DbException error) when (DatabaseErrors.IsBusy(error))
        {
            TempData["TinDangError"] = "Dữ liệu đang được cập nhật. Hãy tải lại và thử lại.";
        }
        catch (DbException error) when (DatabaseErrors.IsConstraint(error))
        {
            TempData["TinDangError"] = "Không thể lưu tin hoặc nhật ký. Dữ liệu chưa được thay đổi; hãy tải lại và thử lại.";
        }
        return RedirectToAction(nameof(QuanLy));
    }

    private async Task<List<string>> KiemTraBatNhapAsync(TinDang listing, PhongTro room, ToaNha building, string? title, string? description)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(title)) errors.Add("Tiêu đề là bắt buộc.");
        else if (title.Length > 200) errors.Add("Tiêu đề không vượt quá 200 ký tự.");
        if (description?.Length > 4000) errors.Add("Mô tả không vượt quá 4.000 ký tự.");
        if (room.GiaThue <= 0) errors.Add("Giá thuê phải lớn hơn 0.");
        if (room.DienTich <= 0) errors.Add("Diện tích phải lớn hơn 0.");
        if (room.SoNguoiToiDa <= 0) errors.Add("Sức chứa phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(building.DiaChi)) errors.Add("Địa chỉ cơ sở là bắt buộc.");
        if (room.TrangThai != "TRONG") errors.Add("Chỉ đăng tin khi phòng đang trống.");
        if (!building.DangHoatDong) errors.Add("Cơ sở phải đang hoạt động.");
        if (await db.TinDangs.AnyAsync(x => x.PhongId == room.Id && x.Id != listing.Id && x.TrangThai == "DANG_HIEN_THI"))
            errors.Add("Phòng đã có tin công khai khác.");
        return errors;
    }
}
