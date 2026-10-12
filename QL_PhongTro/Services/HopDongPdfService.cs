using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class HopDongPdfService
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    static HopDongPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(HopDongChiTietViewModel contract)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor("#292d34"));
                page.Header().Column(header =>
                {
                    header.Item().Text("HỢP ĐỒNG THUÊ PHÒNG").FontSize(20).Bold().FontColor("#245f56");
                    header.Item().PaddingTop(4).Text($"Mã hợp đồng: {contract.MaHopDong}").FontSize(12).SemiBold();
                });
                page.Content().PaddingVertical(18).Column(content =>
                {
                    content.Spacing(16);
                    content.Item().Text("THÔNG TIN HỢP ĐỒNG").FontSize(12).Bold();
                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        AddDetail(table, "Phòng", contract.MaPhong);
                        AddDetail(table, "Giá thuê", FormatMoney(contract.GiaThueHienTai) + " đ/tháng");
                        AddDetail(table, "Tiền cọc", FormatMoney(contract.TienCoc) + " đ");
                        AddDetail(table, "Ngày bắt đầu", FormatDate(contract.NgayBatDau));
                        AddDetail(table, "Ngày kết thúc", FormatDate(contract.NgayKetThuc));
                    });

                    content.Item().Text("DANH SÁCH NGƯỜI Ở").FontSize(12).Bold();
                    if (contract.NguoiO.Count == 0)
                    {
                        content.Item().Text("Chưa có thông tin người ở trong hợp đồng này.");
                    }
                    else
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            AddHeader(table, "Họ tên", "Vai trò", "Trạng thái");
                            foreach (var person in contract.NguoiO)
                                AddRow(table, person.HoTen, person.VaiTro,
                                    person.DangO ? "Đang ở" : person.NgayChuyenDi.HasValue ? "Đã chuyển đi" : "Chưa ở");
                        });
                    }

                    content.Item().Text("DỊCH VỤ VÀ ĐƠN GIÁ ÁP DỤNG").FontSize(12).Bold();
                    if (contract.DichVus.Count == 0)
                    {
                        content.Item().Text("Chưa có dịch vụ được ghi nhận cho hợp đồng này.");
                    }
                    else
                    {
                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });
                            AddHeader(table, "Dịch vụ", "Cách tính", "Đơn vị", "Đơn giá");
                            foreach (var service in contract.DichVus)
                                AddRow(table, service.TenDichVu, CachTinhDichVu.Ten(service.CachTinh),
                                    service.DonViTinh, FormatMoney(service.DonGia) + " đ");
                        });
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void AddDetail(TableDescriptor table, string label, string value)
    {
        table.Cell().Element(DetailCell).Text(label).SemiBold();
        table.Cell().Element(DetailCell).Text(value);
    }

    private static void AddHeader(TableDescriptor table, params string[] values)
    {
        foreach (var value in values)
            table.Cell().Element(HeaderCell).Text(value).SemiBold();
    }

    private static void AddRow(TableDescriptor table, params string[] values)
    {
        foreach (var value in values)
            table.Cell().Element(BodyCell).Text(value);
    }

    private static IContainer DetailCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).PaddingHorizontal(6);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background("#e8f1ee").BorderBottom(1).BorderColor("#b7d0c8").Padding(7);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(7);

    private static string FormatMoney(long value) => value.ToString("N0", VietnameseCulture);

    private static string FormatDate(DateOnly? value) => value?.ToString("dd/MM/yyyy", VietnameseCulture) ?? "Chưa cập nhật";
}
