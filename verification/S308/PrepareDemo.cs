using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;

internal static class PrepareDemo
{
    public static async Task Run(string root, string database)
    {
        root = Path.GetFullPath(root); database = Path.GetFullPath(database);
        var allowed = Path.Combine(root, "data", "s308-demo") + Path.DirectorySeparatorChar;
        if (!database.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !File.Exists(database))
            throw new InvalidOperationException("Only an existing copy under data/s308-demo is allowed.");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=" + database).Options;
        using var read = new AppDbContext(options);
        var actor = await read.TaiKhoans.Where(x => x.Email == "owner.demo@demo.local").Select(x => x.Id).SingleAsync();
        using var db = new AppDbContext(options, new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()),
                new Claim(ClaimTypes.Role, "CHU_NHA")], "demo")) } });
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var first = new DateOnly(today.Year, today.Month, 1); var last = first.AddMonths(1).AddDays(-1);
        var contracts = await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id
            where b.ChuNhaId == actor && h.TrangThai == "DANG_HIEU_LUC" select new { ContractId = h.Id, RoomId = p.Id, p.ToaNhaId }).ToListAsync();
        var meters = await db.DichVus.Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC").ToListAsync();
        // Sprint 2 water is billed per person. This disposable copy needs a new meter-price period for the monthly preview.
        foreach (var meter in meters) {
            var configs = await db.CauHinhDichVus.Where(x => x.DichVuId == meter.Id && x.TuNgay < first
                && (x.DenNgay == null || x.DenNgay >= first) && x.CachTinh != "THEO_CHI_SO").ToListAsync();
            foreach (var old in configs) {
                var previousEnd = old.DenNgay; old.DenNgay = first.AddDays(-1);
                db.CauHinhDichVus.Add(new() { ToaNhaId = old.ToaNhaId, PhongId = old.PhongId, DichVuId = meter.Id,
                    CachTinh = "THEO_CHI_SO", DonViTinh = meter.MaDichVu == "DIEN" ? "kWh" : "m³", DonGia = old.DonGia,
                    TuNgay = first, DenNgay = previousEnd, NguoiTaoId = actor, NgayTao = DateTime.UtcNow });
            }
        }
        await db.SaveChangesAsync();
        foreach (var contract in contracts)
            foreach (var meter in meters)
                if (!await db.ChiSoDienNuocs.AnyAsync(x => x.HopDongId == contract.ContractId && x.DichVuId == meter.Id && x.TuNgay == first && x.DenNgay == last))
                    db.ChiSoDienNuocs.Add(new() { HopDongId = contract.ContractId, DichVuId = meter.Id, TuNgay = first, DenNgay = last,
                        ChiSoDau = meter.MaDichVu == "DIEN" ? 100 : 10, ChiSoCuoi = meter.MaDichVu == "DIEN" ? 120 : 12,
                        NguoiNhapId = actor, NgayNhap = DateTime.UtcNow, DaXacNhanBatThuong = true });
        await db.SaveChangesAsync();
        await PrepareTenantInvoiceAsync(db, actor, today);
        var water = meters.Single(x => x.MaDichVu == "NUOC");
        foreach (var contract in contracts)
        {
            var catalogId = await db.DichVuToaNhas.Where(x => x.ToaNhaId == contract.ToaNhaId && x.DichVuId == water.Id)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync();
            if (catalogId.HasValue && !await db.DichVuPhongs.AnyAsync(x => x.PhongId == contract.RoomId && x.DichVuToaNhaId == catalogId.Value))
                db.DichVuPhongs.Add(new() { PhongId = contract.RoomId, DichVuToaNhaId = catalogId.Value });
        }
        await db.SaveChangesAsync();
        var existingDraftPath = Path.Combine(Path.GetDirectoryName(database)!, "draft-id.txt");
        if (File.Exists(existingDraftPath)
            && int.TryParse((await File.ReadAllTextAsync(existingDraftPath)).Trim(), out var existingDraftId)
            && await db.HoaDons.AnyAsync(x => x.Id == existingDraftId && x.TrangThai == "NHAP"))
        {
            Console.WriteLine($"Prepared draft already exists: {existingDraftId}");
            return;
        }
        var svc = new HoaDonDichVuService(db, new DichVuService(db));
        foreach (var building in contracts.Select(x => x.ToaNhaId).Distinct()) {
            var preview = await svc.XemThangAsync(actor, building, today.Year, today.Month);
            foreach (var skipped in preview.BoQua) Console.WriteLine(skipped.MaPhong + ": " + skipped.LyDo);
            if (preview.DuKien.Count == 0) continue;
            var id = await svc.TaoNhapThangAsync(actor, building, preview.DuKien[0].HoaDon.HopDongId, today.Year, today.Month);
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(database)!, "draft-id.txt"), id.ToString());
            Console.WriteLine("Prepared draft ID " + id + " on copy only: " + database);
            return;
        }
        throw new InvalidOperationException("No eligible invoice in the copied demo.");
    }

    private static async Task PrepareTenantInvoiceAsync(AppDbContext db, int ownerId, DateOnly today)
    {
        var directory = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)
            ?? throw new InvalidOperationException("Demo database has no parent directory.");
        var codePath = Path.Combine(directory, "tenant-invoice-code.txt");
        if (File.Exists(codePath))
        {
            var existingCode = (await File.ReadAllTextAsync(codePath)).Trim();
            if (existingCode.Length > 0 && await db.HoaDons.AnyAsync(x => x.MaHoaDon == existingCode && x.TrangThai == "DA_PHAT_HANH"))
            {
                await PreparePaymentSamplesAsync(db, ownerId, today);
                Console.WriteLine("Tenant invoice detail demo already prepared: " + existingCode);
                return;
            }
        }

        var contracts = await (from contract in db.HopDongs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id
            join account in db.TaiKhoans.AsNoTracking() on tenant.TaiKhoanId equals account.Id
            where building.ChuNhaId == ownerId && contract.TrangThai == "DANG_HIEU_LUC"
                && account.VaiTro == "KHACH_THUE" && account.DangHoatDong && !account.IsDeleted
            orderby room.MaPhong
            select new { contract.Id, room.MaPhong, room.ToaNhaId })
            .ToListAsync();
        var selected = default((int Id, string MaPhong, int ToaNhaId, KyHopDongThamChieu Term, DateOnly First, DateOnly Last)?);
        var previousMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        foreach (var contract in contracts)
        {
            var terms = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == contract.Id).OrderBy(x => x.NgayBatDau).ToListAsync();
            for (var offset = 0; offset < 24 && selected is null; offset++)
            {
                var first = previousMonth.AddMonths(-offset);
                var last = first.AddMonths(1).AddDays(-1);
                var term = terms.SingleOrDefault(x => x.NgayBatDau <= first && x.NgayKetThuc >= last);
                if (term is null || await db.HoaDons.AnyAsync(x => x.HopDongId == contract.Id && x.Nam == first.Year && x.Thang == first.Month))
                    continue;
                selected = (contract.Id, contract.MaPhong, contract.ToaNhaId, term, first, last);
            }
            if (selected is not null) break;
        }
        if (selected is not { } choice)
            throw new InvalidOperationException("No tenant contract has a free full-month period for the invoice demo.");

        var meterServices = await db.DichVus.AsNoTracking()
            .Where(x => x.MaDichVu == "DIEN" || x.MaDichVu == "NUOC")
            .ToDictionaryAsync(x => x.MaDichVu);
        if (!meterServices.TryGetValue("DIEN", out var electricity) || !meterServices.TryGetValue("NUOC", out var water))
            throw new InvalidOperationException("The demo copy must contain the electricity and water service catalog.");
        var monthCode = choice.First.ToString("yyyyMM");
        var lines = new List<ChiTietHoaDon>
        {
            new()
            {
                SoThuTu = 1, LoaiKhoan = "TIEN_PHONG", TenKhoan = "Tiền phòng", DonViTinh = "tháng",
                KyHopDongId = choice.Term.Id, SoLuong = 1, DonGia = choice.Term.GiaThue, ThanhTien = choice.Term.GiaThue
            },
            new()
            {
                SoThuTu = 2, LoaiKhoan = "DICH_VU", DichVuId = electricity.Id, TenKhoan = "Điện",
                CachTinhApDung = CachTinhDichVu.TheoChiSo, DonViTinh = "kWh", ChiSoDau = 100.125m,
                ChiSoCuoi = 120.25m, SoLuong = 20.125m, DonGia = 3500, ThanhTien = 70438
            },
            new()
            {
                SoThuTu = 3, LoaiKhoan = "DICH_VU", DichVuId = water.Id, TenKhoan = "Nước",
                CachTinhApDung = CachTinhDichVu.TheoChiSo, DonViTinh = "m³", ChiSoDau = 10.1m,
                ChiSoCuoi = 12.25m, SoLuong = 2.15m, DonGia = 5000, ThanhTien = 10750
            },
            new()
            {
                SoThuTu = 4, LoaiKhoan = "DICH_VU", TenKhoan = "Internet",
                CachTinhApDung = CachTinhDichVu.CoDinh, DonViTinh = "phòng", SoLuong = 1,
                DonGia = 100000, ThanhTien = 100000
            },
            new()
            {
                SoThuTu = 5, LoaiKhoan = "DICH_VU", TenKhoan = "Phí dịch vụ",
                CachTinhApDung = CachTinhDichVu.CoDinh, DonViTinh = "phòng", SoLuong = 1,
                DonGia = 50000, ThanhTien = 50000
            }
        };
        var issueDate = today;
        var dueDate = issueDate.AddDays(7);
        var bill = new HoaDon
        {
            MaHoaDon = $"HD-DEMO-{monthCode}-{Guid.NewGuid().ToString("N")[..8]}",
            HopDongId = choice.Id, Thang = choice.First.Month, Nam = choice.First.Year,
            TuNgay = choice.First, DenNgay = choice.Last, NgayChot = choice.Last,
            SoNguoiTinhPhi = 1, LoaiHoaDon = "DINH_KY", NgayLap = DateTime.UtcNow,
            HanThanhToan = dueDate, TongTien = lines.Sum(x => x.ThanhTien),
            TrangThai = "NHAP", NguoiLapId = ownerId, ChiTiet = lines
        };
        db.HoaDons.Add(bill);
        await db.SaveChangesAsync();
        await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhNhapAsync(ownerId, new()
        {
            Id = bill.Id, PhienBan = bill.PhienBan, NgayPhatHanh = issueDate,
            HanThanhToan = dueDate, XacNhan = true
        });
        await File.WriteAllTextAsync(codePath, bill.MaHoaDon);
        Console.WriteLine($"Tenant invoice demo ready for room {choice.MaPhong}, period {choice.First:MM/yyyy}: {bill.MaHoaDon}");
        Console.WriteLine($"Open /ThongBao/ChiTiet?maHoaDon={Uri.EscapeDataString(bill.MaHoaDon)} as the tenant account.");
        Console.WriteLine("Demo-only prices: electricity 3,500 đ/kWh, water 5,000 đ/m³, internet 100,000 đ/room, service fee 50,000 đ/room.");
        await PreparePaymentSamplesAsync(db, ownerId, today);
    }

    private static async Task PreparePaymentSamplesAsync(AppDbContext db, int ownerId, DateOnly today)
    {
        var directory = Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)
            ?? throw new InvalidOperationException("Demo database has no parent directory.");
        var samplesFile = Path.Combine(directory, "tenant-invoice-samples.txt");
        var samples = new[]
        {
            ("UNPAID", "Chưa thanh toán", 7),
            ("PARTIAL", "Trả một phần", 7),
            ("PAID", "Đã thanh toán đủ", -4),
            ("OVERDUE-MANY", "Quá hạn nhiều ngày", -10),
            ("OVERDUE-ONE", "Quá hạn một ngày", -1),
            ("DUE-TODAY", "Đúng ngày hạn", 0),
            ("NOT-DUE", "Chưa đến hạn", 1),
            ("OVERPAID", "Trả dư", -8)
        };
        var codes = samples.Select(x => $"HD-DEMO-S307-{x.Item1}").ToArray();
        var present = await db.HoaDons.AsNoTracking().Where(x => codes.Contains(x.MaHoaDon))
            .Include(x => x.ChiTiet).ToListAsync();
        if (present.Count == samples.Length)
        {
            foreach (var invoice in present)
            {
                if (invoice.TrangThai != "DA_PHAT_HANH")
                    throw new InvalidOperationException("Some invoice balance samples exist but are not published; inspect the demo copy.");
                var key = invoice.MaHoaDon["HD-DEMO-S307-".Length..];
                var paymentRows = await db.ThanhToans.AsNoTracking()
                    .Where(x => x.HoaDonId == invoice.Id && x.TrangThai == "DA_XAC_NHAN")
                    .Select(x => x.SoTien).ToListAsync();
                var expected = key switch
                {
                    "PARTIAL" => new long[] { invoice.TongTien / 3, invoice.TongTien / 3 },
                    "PAID" => new long[] { invoice.TongTien },
                    "OVERPAID" => new long[] { checked(invoice.TongTien + Math.Max(1, invoice.TongTien / 10)) },
                    _ => Array.Empty<long>()
                };
                if (!paymentRows.Order().SequenceEqual(expected.Order()))
                    throw new InvalidOperationException($"Payment records for {invoice.MaHoaDon} are incomplete; inspect the demo copy.");
            }
            var otherTenantCode = await PrepareOtherTenantInvoiceAsync(db, ownerId, today);
            await File.WriteAllLinesAsync(samplesFile,
                samples.Select(x => $"{x.Item2} | HD-DEMO-S307-{x.Item1}")
                    .Append($"Hóa đơn khách thuê khác (không được thấy) | {otherTenantCode}"));
            Console.WriteLine("Tenant payment samples already prepared: " + samplesFile);
            return;
        }
        if (present.Count != 0)
            throw new InvalidOperationException("Only some invoice balance samples exist; inspect the demo copy before retrying.");

        var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        var contracts = await (from contract in db.HopDongs.AsNoTracking()
            join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
            join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
            join tenant in db.KhachThues.AsNoTracking() on contract.KhachDungTenId equals tenant.Id
            join account in db.TaiKhoans.AsNoTracking() on tenant.TaiKhoanId equals account.Id
            where building.ChuNhaId == ownerId && contract.TrangThai == "DANG_HIEU_LUC"
                && account.VaiTro == "KHACH_THUE" && account.DangHoatDong && !account.IsDeleted
            orderby room.MaPhong
            select new { contract.Id, room.MaPhong }).ToListAsync();
        (int ContractId, string Room, List<(DateOnly First, DateOnly Last, KyHopDongThamChieu Term)> Periods)? selected = null;
        foreach (var contract in contracts)
        {
            var terms = await db.KyHopDongs.AsNoTracking().Where(x => x.HopDongId == contract.Id)
                .OrderBy(x => x.NgayBatDau).ToListAsync();
            for (var offset = 0; offset < 12 && selected is null; offset++)
            {
                var periods = new List<(DateOnly First, DateOnly Last, KyHopDongThamChieu Term)>();
                for (var index = 0; index < samples.Length; index++)
                {
                    var first = nextMonth.AddMonths(offset + index);
                    var last = first.AddMonths(1).AddDays(-1);
                    var term = terms.SingleOrDefault(x => x.NgayBatDau <= first && x.NgayKetThuc >= last);
                    if (term is null || await db.HoaDons.AnyAsync(x => x.HopDongId == contract.Id
                        && x.Nam == first.Year && x.Thang == first.Month && x.TrangThai != "DA_HUY"))
                        break;
                    periods.Add((first, last, term));
                }
                if (periods.Count == samples.Length) selected = (contract.Id, contract.MaPhong, periods);
            }
        }
        if (selected is not { } selection)
            throw new InvalidOperationException("No tenant contract has eight consecutive free invoice periods for payment samples.");

        var bills = new List<HoaDon>();
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            var period = selection.Periods[index];
            var dueDate = today.AddDays(sample.Item3);
            var bill = new HoaDon
            {
                MaHoaDon = $"HD-DEMO-S307-{sample.Item1}",
                HopDongId = selection.ContractId,
                Thang = period.First.Month,
                Nam = period.First.Year,
                TuNgay = period.First,
                DenNgay = period.Last,
                NgayChot = period.Last,
                SoNguoiTinhPhi = 1,
                LoaiHoaDon = "DINH_KY",
                NgayLap = DateTime.UtcNow,
                NgayPhatHanh = null,
                NgayPhatHanhNghiepVu = null,
                HanThanhToan = dueDate,
                TongTien = period.Term.GiaThue,
                TrangThai = "NHAP",
                NguoiLapId = ownerId,
                ChiTiet =
                [
                    new ChiTietHoaDon
                    {
                        SoThuTu = 1,
                        KyHopDongId = period.Term.Id,
                        LoaiKhoan = "TIEN_PHONG",
                        TenKhoan = "Tiền phòng",
                        DonViTinh = "tháng",
                        SoLuong = 1,
                        DonGia = period.Term.GiaThue,
                        ThanhTien = period.Term.GiaThue
                    }
                ]
            };
            db.HoaDons.Add(bill);
            bills.Add(bill);
        }
        await db.SaveChangesAsync();

        var issuedAt = DateTime.UtcNow;
        foreach (var bill in bills)
        {
            var sampleKey = bill.MaHoaDon["HD-DEMO-S307-".Length..];
            var sample = samples.Single(x => x.Item1 == sampleKey);
            var dueDate = today.AddDays(sample.Item3);
            bill.NgayPhatHanh = issuedAt;
            bill.NgayPhatHanhNghiepVu = dueDate.AddDays(-7);
            bill.HanThanhToan = dueDate;
            bill.NguoiPhatHanhId = ownerId;
            bill.TrangThai = "DA_PHAT_HANH";
        }
        await db.SaveChangesAsync();

        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            var rent = selection.Periods[index].Term.GiaThue;
            long[] amounts = sample.Item1 switch
            {
                "PARTIAL" => new long[] { rent / 3, rent / 3 },
                "PAID" => new long[] { rent },
                "OVERPAID" => new long[] { checked(rent + Math.Max(1, rent / 10)) },
                _ => Array.Empty<long>()
            };
            if (amounts.Length == 0) continue;
            var code = $"HD-DEMO-S307-{sample.Item1}";
            var invoiceId = await db.HoaDons.Where(x => x.MaHoaDon == code).Select(x => x.Id).SingleAsync();
            foreach (var amount in amounts)
                db.ThanhToans.Add(new ThanhToanHoaDon
                {
                    HoaDonId = invoiceId,
                    SoTien = amount,
                    NgayThanhToan = today.AddDays(-1),
                    TrangThai = "DA_XAC_NHAN"
                });
        }
        await db.SaveChangesAsync();
        var otherTenantInvoice = await PrepareOtherTenantInvoiceAsync(db, ownerId, today);
        await File.WriteAllLinesAsync(samplesFile, samples.Select(x => $"{x.Item2} | HD-DEMO-S307-{x.Item1}")
            .Append($"Hóa đơn khách thuê khác (không được thấy) | {otherTenantInvoice}"));
        Console.WriteLine($"Tenant payment samples ready for {selection.Room}: {samplesFile}");
    }

    private static async Task<string> PrepareOtherTenantInvoiceAsync(AppDbContext db, int ownerId, DateOnly today)
    {
        const string code = "HD-DEMO-S307-OTHER-TENANT";
        var existing = await (from existingInvoice in db.HoaDons.AsNoTracking()
            join existingContract in db.HopDongs.AsNoTracking() on existingInvoice.HopDongId equals existingContract.Id
            join existingTenant in db.KhachThues.AsNoTracking() on existingContract.KhachDungTenId equals existingTenant.Id
            join existingAccount in db.TaiKhoans.AsNoTracking() on existingTenant.TaiKhoanId equals existingAccount.Id
            join existingRoom in db.PhongTros.AsNoTracking() on existingContract.PhongId equals existingRoom.Id
            join existingBuilding in db.ToaNhas.AsNoTracking() on existingRoom.ToaNhaId equals existingBuilding.Id
            where existingInvoice.MaHoaDon == code
            select new { existingInvoice.TrangThai, existingAccount.Email, existingBuilding.ChuNhaId }).SingleOrDefaultAsync();
        if (existing is not null)
        {
            if (existing.TrangThai != "DA_PHAT_HANH"
                || existing.Email != RequestDemoSeeder.Tenant2Email
                || existing.ChuNhaId != ownerId)
                throw new InvalidOperationException("The other-tenant invoice demo is inconsistent; inspect the copied database.");
            return code;
        }

        var tenantId = await (from tenant in db.KhachThues
            join account in db.TaiKhoans on tenant.TaiKhoanId equals account.Id
            where account.Email == RequestDemoSeeder.Tenant2Email
                && account.VaiTro == "KHACH_THUE" && account.DangHoatDong && !account.IsDeleted
            select tenant.Id).SingleAsync();
        var buildingId = await (from baseContract in db.HopDongs.AsNoTracking()
            join baseRoom in db.PhongTros.AsNoTracking() on baseContract.PhongId equals baseRoom.Id
            join baseBuilding in db.ToaNhas.AsNoTracking() on baseRoom.ToaNhaId equals baseBuilding.Id
            where baseContract.TrangThai == "DANG_HIEU_LUC" && baseBuilding.ChuNhaId == ownerId
            orderby baseContract.Id
            select baseBuilding.Id).FirstAsync();
        if (await db.PhongTros.AnyAsync(x => x.ToaNhaId == buildingId && x.MaPhong == "S307-FOREIGN"))
            throw new InvalidOperationException("The other-tenant invoice demo room already exists without its invoice.");

        var now = DateTime.UtcNow;
        var room = new PhongTro
        {
            ToaNhaId = buildingId,
            MaPhong = "S307-FOREIGN",
            Tang = 1,
            DienTich = 20,
            GiaThue = 1_250_000,
            TienCocDuKien = 1_250_000,
            SoNguoiToiDa = 2,
            TrangThai = "DANG_THUE",
            MoTa = "Phòng mẫu riêng cho kiểm thử phân quyền hóa đơn.",
            NgayTao = now
        };
        db.PhongTros.Add(room);
        await db.SaveChangesAsync();

        var contract = new HopDongThamChieu
        {
            MaHopDong = "HD-S307-FOREIGN-DEMO",
            PhongId = room.Id,
            KhachDungTenId = tenantId,
            TrangThai = "DANG_HIEU_LUC",
            NguoiLapId = ownerId,
            NgayTao = now
        };
        db.HopDongs.Add(contract);
        await db.SaveChangesAsync();
        var first = new DateOnly(today.Year, today.Month, 1);
        var term = new KyHopDongThamChieu
        {
            HopDongId = contract.Id,
            SoThuTu = 1,
            NgayBatDau = first.AddYears(-1),
            NgayKetThuc = first.AddYears(2).AddDays(-1),
            SoThang = 36,
            GiaThue = room.GiaThue,
            NguoiLapId = ownerId,
            NgayTao = now
        };
        db.KyHopDongs.Add(term);
        await db.SaveChangesAsync();
        var bill = new HoaDon
        {
            MaHoaDon = code,
            HopDongId = contract.Id,
            Thang = first.Month,
            Nam = first.Year,
            TuNgay = first,
            DenNgay = first.AddMonths(1).AddDays(-1),
            NgayChot = first.AddMonths(1).AddDays(-1),
            SoNguoiTinhPhi = 1,
            NgayLap = now,
            HanThanhToan = today.AddDays(7),
            TongTien = room.GiaThue,
            TrangThai = "NHAP",
            NguoiLapId = ownerId,
            ChiTiet =
            [
                new ChiTietHoaDon
                {
                    SoThuTu = 1,
                    KyHopDongId = term.Id,
                    LoaiKhoan = "TIEN_PHONG",
                    TenKhoan = "Tiền phòng",
                    DonViTinh = "tháng",
                    SoLuong = 1,
                    DonGia = room.GiaThue,
                    ThanhTien = room.GiaThue
                }
            ]
        };
        db.HoaDons.Add(bill);
        await db.SaveChangesAsync();
        await new HoaDonDichVuService(db, new DichVuService(db)).PhatHanhNhapAsync(ownerId, new()
        {
            Id = bill.Id,
            PhienBan = bill.PhienBan,
            NgayPhatHanh = today,
            HanThanhToan = today.AddDays(7),
            XacNhan = true
        });
        return code;
    }
}
