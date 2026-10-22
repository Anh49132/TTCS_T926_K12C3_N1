using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QL_PhongTro.Services;

public sealed partial class TatToanService
{
    // Only immutable settlement and issued invoice snapshots contribute to PDF amounts.
    // Payments/refunds recorded after confirmation must never change this statement.
    public async Task<(string FileName, byte[] Content)> PdfAsync(int actor, int contract)
    {
        await using var read = await db.Database.BeginProtectedTransactionAsync();
        await KiemTraQuyenAsync(actor, contract, false);
        var info = await (from h in db.HopDongs.AsNoTracking()
            join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id
            join owner in db.TaiKhoans on b.ChuNhaId equals owner.Id
            where h.Id == contract
            select new { h.MaHopDong, h.TrangThai, p.MaPhong, b.TenToaNha, ChuNha = owner.HoTen, h.KhachDungTenId }).SingleAsync();
        var bang = await db.BangTatToans.AsNoTracking().Include(x => x.KhauTru).SingleOrDefaultAsync(x => x.HopDongId == contract);
        var confirmed = await db.XacNhanTatToans.AsNoTracking().SingleOrDefaultAsync(x => x.HopDongId == contract);
        if (bang is null || confirmed is null || bang.TrangThai != "DA_CHOT" || info.TrangThai != "DA_KET_THUC" || confirmed.ThanhLyId != bang.Id)
            throw new InvalidOperationException("Chỉ tải PDF sau khi bảng tất toán đã được xác nhận và hợp đồng đã kết thúc.");
        var bill = await db.HoaDons.AsNoTracking().Include(x => x.ChiTiet).SingleOrDefaultAsync(x => x.Id == bang.HoaDonCuoiId && x.HopDongId == contract && x.LoaiHoaDon == "KY_CUOI" && x.TrangThai == "DA_PHAT_HANH")
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn kỳ cuối đã phát hành của bảng tất toán.");
        var tenant = await db.KhachThues.Where(x => x.Id == info.KhachDungTenId).Select(x => x.HoTen).SingleOrDefaultAsync() ?? "Chưa ghi nhận";
        var early = await db.ChamDutHopDongs.AsNoTracking().SingleOrDefaultAsync(x => x.HopDongId == contract);
        var debts = JsonSerializer.Deserialize<List<CongNoTatToan>>(bang.ChiTietChot) ?? [];
        var finalDebt = debts.SingleOrDefault(x => x.KyCuoi && x.HoaDonId == bill.Id);
        if (checked(bang.CocConGiu - bang.NoHoaDonCuoi - bang.CongNoCu) != confirmed.SoTienTatToan ||
            checked(bang.TienPhaiHoan - bang.TienKhachTraThem) != confirmed.SoTienTatToan ||
            finalDebt is null || finalDebt.ConNo != bang.NoHoaDonCuoi ||
            debts.Where(x => !x.KyCuoi).Aggregate(0L,(sum,x)=>checked(sum+x.ConNo)) != bang.CongNoCu ||
            bill.ChiTiet.Aggregate(0L,(sum,x)=>checked(sum+(x.LoaiKhoan=="GIAM_TRU"?-x.ThanhTien:x.ThanhTien))) != bill.TongTien)
            throw new InvalidOperationException("Số liệu tất toán không khớp dữ liệu đã xác nhận.");
        await read.CommitAsync();

        QuestPDF.Settings.License = LicenseType.Community;
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string Money(long amount) => amount.ToString("N0", vi) + " đ";
        string Number(decimal amount) => amount.ToString("0.###", vi);
        var bytes = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(32);
            page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(10).FontColor("#25282D"));
            page.Header().Column(c =>
            {
                c.Item().Text("NhàTốt · SỐNG ĐÚNG NƠI").Bold().FontColor("#92794B");
                c.Item().PaddingTop(8).Text("BẢNG TẤT TOÁN HỢP ĐỒNG").FontSize(19).Bold();
                c.Item().Text($"{bang.Ma} · {info.MaHopDong} · Đã chốt");
            });
            page.Content().PaddingVertical(15).Column(c =>
            {
                c.Spacing(10);
                c.Item().Text("Thông tin hợp đồng").Bold().FontSize(13);
                c.Item().Text($"Hợp đồng: {info.MaHopDong}\nPhòng: {info.MaPhong} · {info.TenToaNha}\nChủ nhà: {info.ChuNha}\nKhách thuê: {tenant}\nNgày trả thực tế: {bang.NgayTraPhong:dd/MM/yyyy}\nTrạng thái hợp đồng: Đã kết thúc");
                c.Item().Text($"Hóa đơn kỳ cuối: {bill.MaHoaDon}").Bold().FontSize(13);
                c.Item().Text($"Kỳ tính: {bill.TuNgay:dd/MM/yyyy} — {bill.DenNgay:dd/MM/yyyy}");
                c.Item().Table(t =>
                {
                    t.ColumnsDefinition(d => { d.RelativeColumn(3); d.RelativeColumn(2); d.RelativeColumn(2); d.RelativeColumn(2); });
                    t.Header(h => { foreach (var text in new[] { "Khoản thu", "Số lượng / chỉ số", "Đơn giá", "Thành tiền" }) h.Cell().Background("#F4F0E7").Padding(6).Text(text).Bold(); });
                    foreach (var line in bill.ChiTiet.OrderBy(x => x.SoThuTu))
                    {
                        var quantity = line.SoNgayTinhTien.HasValue ? $"{line.SoNgayTinhTien}/{line.SoNgayTrongThang} ngày" : Number(line.SoLuong) + " " + line.DonViTinh;
                        if (line.ChiSoDau.HasValue && line.ChiSoCuoi.HasValue) quantity += $"\n{Number(line.ChiSoCuoi.Value)} − {Number(line.ChiSoDau.Value)}";
                        foreach (var text in new[] { line.TenKhoan, quantity, Money(line.DonGia), (line.LoaiKhoan == "GIAM_TRU" ? "− " : "") + Money(line.ThanhTien) })
                            t.Cell().BorderBottom(0.5f).BorderColor("#E9EAEC").Padding(6).Text(text);
                    }
                });
                c.Item().Text("Tổng hóa đơn kỳ cuối: " + Money(bill.TongTien)).Bold();
                c.Item().Text($"Đã thanh toán trước chốt: {Money(finalDebt.DaThanhToan)} · Đã cấn trước chốt: {Money(finalDebt.DaCanTru)} · Dư nợ kỳ cuối: {Money(finalDebt.ConNo)}");
                c.Item().Text("Các khoản khấu trừ hư hỏng / phạt").Bold().FontSize(13);
                if (bang.KhauTru.Count == 0) c.Item().Text("Không có khoản khấu trừ.");
                foreach (var row in bang.KhauTru.OrderBy(x => x.SoThuTu))
                    c.Item().Text($"{row.TenKhoan}: {Money(row.SoTien)}" + (string.IsNullOrWhiteSpace(row.GhiChu) ? "" : $" · {row.GhiChu}"));
                c.Item().Text($"Tổng hư hỏng: {Money(bang.TienHuHong)} · Phạt cọc: {Money(bang.TienPhat)}");
                c.Item().Text("Các khoản trên đã gồm trong hóa đơn kỳ cuối, không trừ thêm lần nữa.").FontSize(9);
                if (early?.TruocHan == true)
                {
                    var reason = early.LyDo switch { "KHACH_CHUYEN_DI" => "Khách chủ động chuyển đi", "THOA_THUAN" => "Hai bên thỏa thuận", "VI_PHAM" => "Khách vi phạm hợp đồng", _ => "Lý do khác: " + early.LyDoKhac };
                    c.Item().Text($"Chấm dứt trước hạn: {reason}\nÁp dụng phạt cọc: {(early.ApDungPhat ? "Có" : "Không")} · {Money(early.TienPhat)}");
                }
                else c.Item().Text("Không chấm dứt trước hạn · Không áp dụng phạt cọc.");
                c.Item().Text("Công nợ kỳ trước tại thời điểm chốt").Bold().FontSize(13);
                foreach (var debt in debts.Where(x => !x.KyCuoi))
                    c.Item().Text($"{debt.MaHoaDon} ({debt.Thang:00}/{debt.Nam}) · Tổng {Money(debt.TongTien)} · Đã thanh toán {Money(debt.DaThanhToan)} · Đã cấn {Money(debt.DaCanTru)} · Còn nợ {Money(debt.ConNo)}");
                if (!debts.Any(x => !x.KyCuoi)) c.Item().Text("Không có công nợ kỳ trước.");
                c.Item().Background("#F4F0E7").Padding(12).Column(result =>
                {
                    result.Spacing(5);
                    result.Item().Text("Kết quả tất toán đã xác nhận").Bold().FontSize(13);
                    result.Item().Text("Tiền cọc còn giữ trước chốt: " + Money(bang.CocConGiu));
                    result.Item().Text("− Dư nợ hóa đơn kỳ cuối (đã gồm hư hỏng/phạt): " + Money(bang.NoHoaDonCuoi));
                    result.Item().Text("− Công nợ kỳ trước: " + Money(bang.CongNoCu));
                    result.Item().Text(bang.TienKhachTraThem > 0 ? "Khách phải trả thêm: " + Money(bang.TienKhachTraThem) : bang.TienPhaiHoan > 0 ? "Hoàn cho khách: " + Money(bang.TienPhaiHoan) : "Không phải hoàn hoặc thu thêm: 0 đ").Bold().FontSize(14);
                    result.Item().Text("Cọc đã cấn khi xác nhận: " + Money(confirmed.TienCanTru));
                });
                if (!string.IsNullOrWhiteSpace(bang.GhiChu)) c.Item().Text("Ghi chú: " + bang.GhiChu);
                c.Item().Text($"Xác nhận bởi: {confirmed.TenNguoiXacNhan}\nThời điểm xác nhận: {confirmed.ThoiDiem.AddHours(7):dd/MM/yyyy HH:mm} (giờ Việt Nam)");
                c.Item().Text("Đây là số liệu tại thời điểm chốt nghĩa vụ. Các khoản thu hoặc hoàn thực tế sau chốt được theo dõi riêng.").FontSize(9);
            });
            page.Footer().AlignCenter().Text(t => { t.Span("NhàTốt · Trang "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
        })).GeneratePdf();
        var safeCode = string.Concat(info.MaHopDong.Where(x => char.IsAsciiLetterOrDigit(x) || x == '-'));
        return ($"Bang-tat-toan-{safeCode}.pdf", bytes);
    }
}
