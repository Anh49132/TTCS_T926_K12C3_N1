using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;

internal static class ManagementChecks
{
    public static async Task RunAsync(AppDbContext db, DbContextOptions<AppDbContext> options, string root, string database,
        int owner, int otherOwner, int building, Action<bool, string> check)
    {
        var service = new DichVuService(db);
        async Task Reject(Func<Task> operation, string message)
        {
            var rejected = false;
            try { await operation(); }
            catch (InvalidOperationException) { rejected = true; }
            catch (UnauthorizedAccessException) { rejected = true; }
            check(rejected, message);
            db.ChangeTracker.Clear();
        }
        await service.KhoiTaoMacDinhAsync(owner, building);
        await service.KhoiTaoMacDinhAsync(owner, building);
        var defaults = (await service.DanhSachAsync(owner, building)).Where(x => DichVuMacDinhOptions.LaMacDinh(x.DichVu.MaDichVu)).ToList();
        check(defaults.Count == 5 && defaults.Select(x => x.DichVu.MaDichVu).Distinct().Count() == 5, "AC2 five defaults; repeated initialization does not duplicate");
        foreach (var definition in DichVuMacDinhOptions.DeXuat())
        {
            var row = defaults.Single(x => x.DichVu.MaDichVu == definition.Ma);
            check(row.DichVu.TenDichVu == definition.Ten && row.CachTinh == definition.CachTinh && row.DonViTinh == definition.DonVi && !row.DaChotGia,
                "AC2 metadata and unconfigured price: " + definition.Ma);
            check(await service.LayDonGiaAsync(owner, building, row.DichVuId, DichVuService.HomNay()) is null, "unconfigured default cannot be invoiced: " + definition.Ma);
        }
        var electricity = defaults.Single(x => x.DichVu.MaDichVu == "DIEN");
        var water = defaults.Single(x => x.DichVu.MaDichVu == "NUOC");
        await Reject(() => service.SuaGiaBanDauAsync(owner, building, electricity.DichVuId, 0, 0), "default electricity price must be positive");
        await service.SuaGiaBanDauAsync(owner, building, electricity.DichVuId, 3000, 0);
        await service.SuaGiaBanDauAsync(owner, building, water.DichVuId, 15000, 0);
        var headCountService = defaults.Single(x => x.DichVu.MaDichVu == "RAC");
        var fixedService = defaults.Single(x => x.DichVu.MaDichVu == "INTERNET");
        await service.SuaGiaBanDauAsync(owner, building, headCountService.DichVuId, 20000, 0);
        await service.SuaGiaBanDauAsync(owner, building, fixedService.DichVuId, 100000, 0);
        check((await service.LayDonGiaAsync(owner, building, electricity.DichVuId, DichVuService.HomNay()))!.DonGia == 3000, "AC2 edited initial price used by lookup");

        // Existing S1-06 schema is prepared only on the explicitly disposable test copy.
        await db.Database.ExecuteSqlRawAsync(File.ReadAllText(Path.Combine(root, "docs/sql/S1-06-khach-thue.sql")));
        var rentals = File.ReadAllText(Path.Combine(root, "docs/sql/S1-06-quan-he-thue.sql"));
        rentals = rentals[rentals.IndexOf("CREATE TABLE hop_dong", StringComparison.Ordinal)..].Replace("COMMIT;", "");
        await db.Database.ExecuteSqlRawAsync(rentals);
        DichVuSchemaInitializer.InitializeInvoices(database);
        var profile = new KhachThue { HoTen = "Khách kiểm thử dịch vụ", NgayTao = DateTime.UtcNow };
        db.KhachThues.Add(profile);
        var room = new PhongTro { ToaNhaId = building, MaPhong = "S109", Tang = 1, DienTich = 25, GiaThue = 2000000, SoNguoiToiDa = 4, TrangThai = "DANG_THUE", NgayTao = DateTime.UtcNow };
        db.PhongTros.Add(room); await db.SaveChangesAsync();
        var contractCode = "S109-" + Guid.NewGuid().ToString("N")[..8];
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES({contractCode},{room.Id},{profile.Id},{"DANG_HIEU_LUC"},{owner},{DateTime.UtcNow})");
        var contract = await db.HopDongs.SingleAsync(x => x.MaHopDong == contractCode);
        var today = DichVuService.HomNay(); var start = new DateOnly(today.Year, today.Month, 1); var end = start.AddYears(2).AddDays(-1);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES({contract.Id},1,{start},{end},24,2000000,{owner},{DateTime.UtcNow})");
        var invoices = new HoaDonDichVuService(db, service);
        async Task<LapHoaDonDichVuViewModel> Invoice(DateOnly date)
        {
            var price = await service.LayDonGiaAsync(owner, building, electricity.DichVuId, date);
            return new LapHoaDonDichVuViewModel { ToaNhaId = building, HopDongId = contract.Id, NgayApDung = date, SoNguoi = 2,
                Dong = [new DongDichVuInput { DichVuId = electricity.DichVuId, CauHinhId = price?.CauHinhId ?? 0, DonGiaDaXem = price?.DonGia ?? 0, Chon = true, ChiSoDau = 100, ChiSoCuoi = 110 }] };
        }
        var oldInvoiceId = await invoices.PhatHanhAsync(owner, await Invoice(today));
        var oldInvoice = await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).SingleAsync(x => x.Id == oldInvoiceId);
        check(oldInvoice.TongTien == 2030000 && oldInvoice.ChiTiet.Single(x => x.DichVuId.HasValue).DonGia == 3000, "AC3 invoice stores price 3000 and amount snapshot");
        await Reject(() => service.SuaGiaBanDauAsync(owner, building, electricity.DichVuId, 3100, 3000), "initial price cannot overwrite an invoiced price");
        var next = start.AddMonths(1);
        var version = (await service.LichSuAsync(owner, building, electricity.DichVuId)).Last();
        await service.DoiGiaAsync(owner, building, electricity.DichVuId, 3500, next, version.Id);
        var history = await service.LichSuAsync(owner, building, electricity.DichVuId);
        check(history.Count == 2 && history[0].DenNgay == next.AddDays(-1), "AC3 history closes old version without overlap");
        check((await service.LayDonGiaAsync(owner, building, electricity.DichVuId, next.AddDays(-1)))!.DonGia == 3000 &&
              (await service.LayDonGiaAsync(owner, building, electricity.DichVuId, next))!.DonGia == 3500, "AC3 inclusive date boundaries select correct prices");
        await Reject(() => service.DoiGiaAsync(owner, building, electricity.DichVuId, 3600, next, history.Last().Id), "AC3 duplicate effective date rejected");
        var staleInput = await Invoice(next); staleInput.Dong[0].DonGiaDaXem = 3000;
        await Reject(async () => { await invoices.PhatHanhAsync(owner, staleInput); }, "invoice refuses stale displayed price");
        var newInvoiceId = await invoices.PhatHanhAsync(owner, await Invoice(next));
        check((await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == newInvoiceId)).TongTien == 2035000, "AC3 new invoice uses price 3500");
        check((await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == oldInvoiceId)).TongTien == 2030000 &&
            (await db.ChiTietHoaDons.AsNoTracking().SingleAsync(x => x.HoaDonId == oldInvoiceId && x.DichVuId != null)).DonGia == 3000, "AC3 old invoice remains unchanged");
        await Reject(() => service.XoaAsync(owner, building, electricity.DichVuId), "AC4 invoice reference prevents deletion");
        await invoices.GanVaoHopDongAsync(owner, contract.Id, water.DichVuId, today);
        var agreement = await db.HopDongDichVus.AsNoTracking().SingleAsync(x => x.HopDongId == contract.Id && x.DichVuId == water.DichVuId);
        await Reject(() => service.XoaAsync(owner, building, water.DichVuId), "AC4 contract reference prevents deletion");
        var stop = next.AddMonths(1);
        await service.DoiTrangThaiAsync(owner, building, electricity.DichVuId, false, stop, (await service.LichSuAsync(owner, building, electricity.DichVuId)).Last().Id);
        check(await service.LayDonGiaAsync(owner, building, electricity.DichVuId, stop) is null, "AC4 stopped service absent from available prices");
        await Reject(async () => { await invoices.PhatHanhAsync(owner, await Invoice(stop)); }, "AC4 crafted invoice cannot select stopped service");
        var resume = stop.AddMonths(1);
        await service.DoiTrangThaiAsync(owner, building, electricity.DichVuId, true, resume, (await service.LichSuAsync(owner, building, electricity.DichVuId)).Last().Id);
        check((await service.LayDonGiaAsync(owner, building, electricity.DichVuId, resume))!.DonGia == 3500, "AC4 reactivation restores availability in selected future period");
        var resumedInput = await Invoice(resume);
        foreach (var serviceId in new[] { headCountService.DichVuId, fixedService.DichVuId })
        {
            var price = (await service.LayDonGiaAsync(owner, building, serviceId, resume))!;
            resumedInput.Dong.Add(new DongDichVuInput { Chon = true, DichVuId = serviceId, CauHinhId = price.CauHinhId, DonGiaDaXem = price.DonGia });
        }
        var resumedInvoice = await invoices.PhatHanhAsync(owner, resumedInput);
        check((await db.HoaDons.AsNoTracking().SingleAsync(x => x.Id == resumedInvoice)).TongTien == 2175000, "reactivated meter service plus per-person and fixed services calculate correctly");
        var versionAfterResume = (await service.LichSuAsync(owner, building, electricity.DichVuId)).Last();
        await service.DoiGiaAsync(owner, building, electricity.DichVuId, 4000, resume.AddMonths(1), versionAfterResume.Id);
        check((await service.LichSuAsync(owner, building, electricity.DichVuId)).Count == 5, "AC3 multiple price and status changes retain history");
        await service.DoiGiaAsync(owner, building, water.DichVuId, 16000, next, (await service.LichSuAsync(owner, building, water.DichVuId)).Last().Id);
        await service.DoiTrangThaiAsync(owner, building, water.DichVuId, false, stop, (await service.LichSuAsync(owner, building, water.DichVuId)).Last().Id);
        var contractSnapshot = await db.HopDongDichVus.AsNoTracking().SingleAsync(x => x.Id == agreement.Id);
        check(contractSnapshot.DonGia == agreement.DonGia && contractSnapshot.TenDichVu == agreement.TenDichVu, "AC4 contract service snapshot retained");
        var garbage = defaults.Single(x => x.DichVu.MaDichVu == "GUI_XE");
        await service.XoaAsync(owner, building, garbage.DichVuId); await service.KhoiTaoMacDinhAsync(owner, building);
        check(!(await service.DanhSachAsync(owner, building)).Any(x => x.DichVuId == garbage.DichVuId), "AC4 unused default deletes and does not reseed");
        var customId = await service.ThemAsync(owner, new TaoDichVuViewModel { ToaNhaId = building, TenDichVu = "Chưa sử dụng", CachTinh = "CO_DINH", DonViTinh = "phòng/tháng", DonGia = 1000 });
        var customService = (await db.CauHinhDichVus.AsNoTracking().SingleAsync(x => x.Id == customId)).DichVuId;
        await service.XoaAsync(owner, building, customService);
        check(!await db.DichVus.AnyAsync(x => x.Id == customService), "unused custom service and its configurations can be deleted");
        await Reject(() => service.XoaAsync(otherOwner, building, water.DichVuId), "cross-owner deletion denied");
        var otherBuilding = new ToaNha { ChuNhaId = otherOwner, TenToaNha = "Tòa kiểm thử độc lập", DiaChi = "Test" };
        db.ToaNhas.Add(otherBuilding); await db.SaveChangesAsync();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        { await using var concurrentDb = new AppDbContext(options); await new DichVuService(concurrentDb).KhoiTaoMacDinhAsync(otherOwner, otherBuilding.Id); })));
        check((await service.DanhSachAsync(otherOwner, otherBuilding.Id)).Count == 5, "AC2 concurrent initialization creates only five configurations");
        await service.SuaGiaBanDauAsync(otherOwner, otherBuilding.Id, electricity.DichVuId, 5000, 0);
        check((await service.LayDonGiaAsync(otherOwner, otherBuilding.Id, electricity.DichVuId, today))!.DonGia == 5000 &&
            (await service.LayDonGiaAsync(owner, building, electricity.DichVuId, today))!.DonGia == 3000, "building prices remain independent despite shared default catalog");
        var blocked = false;
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE chi_tiet_hoa_don SET don_gia=1 WHERE hoa_don_id={oldInvoiceId}"); }
        catch (SqliteException) { blocked = true; }
        check(blocked, "database trigger prevents changing issued invoice line");
        check(HoaDonDichVuService.ThanhTien(0.125m, 3000) == 375 && HoaDonDichVuService.ThanhTien(0.001m, 500) == 1, "decimal calculation rounds each line to VND without floating-point error");
    }
}
