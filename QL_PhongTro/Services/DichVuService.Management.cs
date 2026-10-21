using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed partial class DichVuService
{
    public static DateOnly KySau() => new DateOnly(HomNay().Year, HomNay().Month, 1).AddMonths(1);

    public async Task<bool> DaKhoiTaoAsync(int accountId, int buildingId)
    {
        if (!await SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        return await db.KhoiTaoDichVus.AnyAsync(x => x.ToaNhaId == buildingId);
    }

    public async Task KhoiTaoMacDinhAsync(int accountId, int buildingId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        if (await db.KhoiTaoDichVus.AnyAsync(x => x.ToaNhaId == buildingId)) return;
        var configured = defaults?.Value.DanhSach;
        var definitions = configured is { Count: > 0 } ? configured : DichVuMacDinhOptions.DeXuat();
        if (definitions.Count != 5 || !definitions.Select(x => x.Ma).Order().SequenceEqual(DichVuMacDinhOptions.Codes.Order()) ||
            definitions.Any(x => string.IsNullOrWhiteSpace(x.Ten) || x.Ten.Length > 100 || string.IsNullOrWhiteSpace(x.DonVi) || x.DonVi.Length > 30 ||
                !CachTinhDichVu.HopLe(x.CachTinh) || x.DonGia < 0 || (x.Ma is "DIEN" or "NUOC" && x.DonGia == 0)))
            throw new InvalidOperationException("Cấu hình năm dịch vụ mặc định chưa hợp lệ. Vui lòng liên hệ người quản trị.");
        foreach (var definition in definitions)
        {
            var catalog = await db.DichVus.SingleOrDefaultAsync(x => x.MaDichVu == definition.Ma);
            if (catalog is null)
            {
                catalog = new DichVu { MaDichVu = definition.Ma, TenDichVu = definition.Ten };
                db.DichVus.Add(catalog);
            }
            if (catalog.Id != 0 && await db.CauHinhDichVus.AnyAsync(x => x.ToaNhaId == buildingId && x.DichVuId == catalog.Id && x.PhongId == null)) continue;
            db.DichVuToaNhas.Add(new DichVuToaNha { ToaNhaId = buildingId, DichVu = catalog, ApDungMacDinh = true });
            db.CauHinhDichVus.Add(new CauHinhDichVu
            {
                ToaNhaId = buildingId, DichVu = catalog, CachTinh = definition.CachTinh, DonViTinh = definition.DonVi,
                DonGia = definition.DonGia ?? 0, DaChotGia = definition.DonGia.HasValue,
                TuNgay = HomNay(), NguoiTaoId = accountId, NgayTao = DateTime.UtcNow
            });
        }
        db.KhoiTaoDichVus.Add(new KhoiTaoDichVu { ToaNhaId = buildingId, NgayTao = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<CauHinhDichVu>> LichSuAsync(int accountId, int buildingId, int serviceId)
    {
        if (!await SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        return await db.CauHinhDichVus.AsNoTracking().Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == buildingId && x.DichVuId == serviceId && x.PhongId == null)
            .OrderBy(x => x.TuNgay).ToListAsync();
    }

    private async Task<List<CauHinhDichVu>> EditableAsync(int accountId, int buildingId, int serviceId)
    {
        if (!await SoHuuToaNhaAsync(accountId, buildingId)) throw new UnauthorizedAccessException();
        var versions = await db.CauHinhDichVus.Include(x => x.DichVu)
            .Where(x => x.ToaNhaId == buildingId && x.DichVuId == serviceId && x.PhongId == null).OrderBy(x => x.TuNgay).ToListAsync();
        if (versions.Count == 0) throw new InvalidOperationException("Không tìm thấy dịch vụ trong tòa nhà này.");
        return versions;
    }

    private static void ValidatePrice(CauHinhDichVu source, long price)
    {
        if (price < 0 || (source.DichVu.MaDichVu is "DIEN" or "NUOC" && price == 0))
            throw new InvalidOperationException("Đơn giá không được âm; điện và nước mặc định phải có đơn giá lớn hơn 0.");
    }

    public async Task SuaGiaBanDauAsync(int accountId, int buildingId, int serviceId, long price, long expectedPrice)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var versions = await EditableAsync(accountId, buildingId, serviceId);
        var initial = versions[0];
        if (initial.DichVu.MaDichVu is "DIEN" or "NUOC")
            throw new InvalidOperationException("Hãy dùng màn hình Cấu hình điện nước để lưu cho kỳ kế tiếp. Thiết lập giá lần đầu cần chốt nghiệp vụ riêng.");
        if (!DichVuMacDinhOptions.LaMacDinh(initial.DichVu.MaDichVu) || versions.Count != 1)
            throw new InvalidOperationException("Chỉ sửa trực tiếp giá ban đầu của dịch vụ mặc định chưa có lịch sử. Hãy tạo phiên bản giá mới.");
        if (initial.DonGia != expectedPrice) throw new InvalidOperationException("Đơn giá vừa thay đổi. Hãy tải lại trang trước khi lưu.");
        ValidatePrice(initial, price);
        var reason = await new DichVuThamChieu(db).LyDoKhongDuocXoaAsync(buildingId, serviceId);
        if (reason is not null) throw new InvalidOperationException(reason);
        await BaoToanCauHinhRiengAsync(initial, HomNay(), accountId);
        initial.DonGia = price; initial.DaChotGia = true;
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task DoiGiaAsync(int accountId, int buildingId, int serviceId, long price, DateOnly effectiveDate, int expectedVersion)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var versions = await EditableAsync(accountId, buildingId, serviceId);
        if (versions[0].DichVu.MaDichVu is "DIEN" or "NUOC")
            throw new InvalidOperationException("Hãy dùng màn hình Cấu hình điện nước để tạo hoặc sửa cấu hình cho kỳ kế tiếp.");
        if (versions.Any(x => x.TuNgay == effectiveDate)) throw new InvalidOperationException("Ngày hiệu lực đã tồn tại. Hãy chọn ngày khác.");
        var previous = versions.Last();
        if (previous.Id != expectedVersion) throw new InvalidOperationException("Dịch vụ vừa thay đổi. Hãy tải lại trang.");
        if (effectiveDate.Year > 9998 || effectiveDate < HomNay() || effectiveDate <= previous.TuNgay)
            throw new InvalidOperationException("Ngày hiệu lực phải từ hôm nay và sau phiên bản mới nhất.");
        if (!previous.DaChotGia) throw new InvalidOperationException("Hãy thiết lập đơn giá ban đầu trước.");
        ValidatePrice(previous, price);
        await BaoToanCauHinhRiengAsync(previous, effectiveDate, accountId);
        await AppendVersionAsync(previous, price, previous.DangApDung, effectiveDate, accountId);
        await tx.CommitAsync();
    }

    public async Task DoiTrangThaiAsync(int accountId, int buildingId, int serviceId, bool active, DateOnly effectiveDate, int expectedVersion)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var versions = await EditableAsync(accountId, buildingId, serviceId);
        var previous = versions.Last();
        if (previous.Id != expectedVersion) throw new InvalidOperationException("Dịch vụ vừa thay đổi. Hãy tải lại trang.");
        if (effectiveDate.Year > 9998 || effectiveDate.Day != 1 || effectiveDate < KySau() || effectiveDate <= previous.TuNgay)
            throw new InvalidOperationException("Chọn ngày đầu kỳ từ tháng sau, sau ngày hiệu lực mới nhất; không dùng trùng ngày.");
        if (previous.DangApDung == active) throw new InvalidOperationException("Trạng thái đã được thiết lập như yêu cầu.");
        if (!previous.DaChotGia) throw new InvalidOperationException("Hãy thiết lập đơn giá ban đầu trước khi thay đổi trạng thái.");
        await AppendVersionAsync(previous, previous.DonGia, active, effectiveDate, accountId);
        await tx.CommitAsync();
    }

    private async Task AppendVersionAsync(CauHinhDichVu previous, long price, bool active, DateOnly date, int accountId)
    {
        previous.DenNgay = date.AddDays(-1);
        // Close first so the database overlap trigger also protects concurrent writers.
        await db.SaveChangesAsync();
        db.CauHinhDichVus.Add(new CauHinhDichVu
        {
            ToaNhaId = previous.ToaNhaId, DichVuId = previous.DichVuId, CachTinh = previous.CachTinh,
            DonViTinh = previous.DonViTinh, DonGia = price, TuNgay = date, DangApDung = active,
            DaChotGia = previous.DaChotGia, NguoiTaoId = accountId, NgayTao = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task BaoToanCauHinhRiengAsync(CauHinhDichVu common, DateOnly effectiveDate, int accountId)
    {
        var catalogId = await db.DichVuToaNhas.Where(x => x.ToaNhaId == common.ToaNhaId && x.DichVuId == common.DichVuId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (catalogId is null) return;
        var overrides = await db.DichVuPhongs.AsNoTracking()
            .Where(x => x.DichVuToaNhaId == catalogId && x.DonGiaRieng != null)
            .Select(x => new { x.PhongId, DonGia = x.DonGiaRieng!.Value }).ToListAsync();
        foreach (var item in overrides)
        {
            var alreadyProtected = await db.CauHinhDichVus.AnyAsync(x => x.PhongId == item.PhongId && x.DichVuId == common.DichVuId
                && x.TuNgay <= effectiveDate && (x.DenNgay == null || x.DenNgay >= effectiveDate));
            if (alreadyProtected) continue;
            db.CauHinhDichVus.Add(new CauHinhDichVu
            {
                ToaNhaId = common.ToaNhaId, PhongId = item.PhongId, DichVuId = common.DichVuId,
                CachTinh = common.CachTinh, DonViTinh = common.DonViTinh, DonGia = item.DonGia,
                TuNgay = effectiveDate, DangApDung = common.DangApDung, DaChotGia = true,
                NguoiTaoId = accountId, NgayTao = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task XoaAsync(int accountId, int buildingId, int serviceId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var versions = await EditableAsync(accountId, buildingId, serviceId);
        var reason = await new DichVuThamChieu(db).LyDoKhongDuocXoaAsync(buildingId, serviceId);
        if (reason is not null) throw new InvalidOperationException(reason);
        if (await db.CauHinhDichVus.AnyAsync(x => x.ToaNhaId == buildingId && x.DichVuId == serviceId && x.PhongId != null))
            throw new InvalidOperationException("Dịch vụ đang có cấu hình riêng của phòng; không thể xóa.");
        var buildingService = await db.DichVuToaNhas.SingleOrDefaultAsync(x => x.ToaNhaId == buildingId && x.DichVuId == serviceId);
        if (buildingService is not null)
        {
            if (await db.DichVuPhongs.AnyAsync(x => x.DichVuToaNhaId == buildingService.Id))
                throw new InvalidOperationException("Dịch vụ đang được phòng sử dụng; không thể xóa.");
            db.DichVuToaNhas.Remove(buildingService);
        }
        var catalog = versions[0].DichVu;
        db.CauHinhDichVus.RemoveRange(versions);
        await db.SaveChangesAsync();
        if (!await db.CauHinhDichVus.AnyAsync(x => x.DichVuId == serviceId) && !DichVuMacDinhOptions.LaMacDinh(catalog.MaDichVu))
            db.DichVus.Remove(catalog);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        // The building initialization marker remains: a deliberately removed default never reappears on reload.
    }
}
