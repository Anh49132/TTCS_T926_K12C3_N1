using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public interface IInvoiceEmailSender
{
    Task<EmailDeliveryResult> SendInvoiceIssuedAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, DateOnly dueDate, string detailUrl);
    Task<EmailDeliveryResult> SendDueSoonAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, long remainingAmount, DateOnly dueDate, string detailUrl);
    Task<EmailDeliveryResult> SendOverdueAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, long remainingAmount, DateOnly dueDate, string detailUrl);
}

public sealed class InvoiceEmailSender(IOptions<PasswordResetOptions> options, IWebHostEnvironment environment, BrevoEmailTransport? brevo = null) : IInvoiceEmailSender
{
    public Task<EmailDeliveryResult> SendInvoiceIssuedAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, DateOnly dueDate, string detailUrl)
    {
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var subject = $"Hóa đơn mới tháng {month}/{year} - {invoiceCode}";
        var body = $"Kính gửi {name},\n\n" +
            $"Hóa đơn tháng {month}/{year} (mã: {invoiceCode}) đã được phát hành.\n" +
            $"Tổng tiền: {totalAmount.ToString("N0", vn)} đ\n" +
            $"Hạn thanh toán: {dueDate:dd/MM/yyyy}\n\n" +
            $"Xem chi tiết hóa đơn: {detailUrl}\n\n" +
            $"Vui lòng đăng nhập ứng dụng để xem chi tiết và thanh toán đúng hạn.\n\n" +
            $"Trân trọng,\nNhàTốt";

        return DeliverAsync(email, subject, body);
    }

    public Task<EmailDeliveryResult> SendDueSoonAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, long remainingAmount, DateOnly dueDate, string detailUrl)
    {
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var subject = $"Sắp đến hạn thanh toán - Hóa đơn tháng {month}/{year}";
        var body = $"Kính gửi {name},\n\n" +
            $"Hóa đơn tháng {month}/{year} (mã: {invoiceCode}) sẽ đến hạn thanh toán sau 2 ngày.\n" +
            $"Tổng tiền: {totalAmount.ToString("N0", vn)} đ\n" +
            $"Số còn phải trả: {remainingAmount.ToString("N0", vn)} đ\n" +
            $"Hạn thanh toán: {dueDate:dd/MM/yyyy}\n\n" +
            $"Xem chi tiết hóa đơn: {detailUrl}\n\n" +
            $"Vui lòng thanh toán đúng hạn để tránh bị tính quá hạn.\n\n" +
            $"Trân trọng,\nNhàTốt";

        return DeliverAsync(email, subject, body);
    }

    public Task<EmailDeliveryResult> SendOverdueAsync(string email, string name, string invoiceCode, int month, int year, long totalAmount, long remainingAmount, DateOnly dueDate, string detailUrl)
    {
        var vn = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        var subject = $"Quá hạn thanh toán - Hóa đơn tháng {month}/{year}";
        var body = $"Kính gửi {name},\n\n" +
            $"Hóa đơn tháng {month}/{year} (mã: {invoiceCode}) đã quá hạn thanh toán.\n" +
            $"Tổng tiền: {totalAmount.ToString("N0", vn)} đ\n" +
            $"Số còn phải trả: {remainingAmount.ToString("N0", vn)} đ\n" +
            $"Hạn thanh toán: {dueDate:dd/MM/yyyy}\n\n" +
            $"Xem chi tiết hóa đơn: {detailUrl}\n\n" +
            $"Vui lòng thanh toán ngay để tránh ảnh hưởng quyền lợi thuê phòng.\n\n" +
            $"Trân trọng,\nNhàTốt";

        return DeliverAsync(email, subject, body);
    }

    private async Task<EmailDeliveryResult> DeliverAsync(string email, string subject, string body)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.From))
        {
            throw new InvalidOperationException("Missing email sender configuration.");
        }

        using var message = new MailMessage(settings.From, email)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        if (!string.IsNullOrWhiteSpace(settings.PickupDirectory))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Email pickup is development-only.");
            }

            var pickupDirectory = Path.IsPathFullyQualified(settings.PickupDirectory)
                ? settings.PickupDirectory
                : Path.GetFullPath(settings.PickupDirectory, Path.GetTempPath());
            Directory.CreateDirectory(pickupDirectory);
            var filePath = await WritePickupMessageAsync(pickupDirectory, message);
            return new EmailDeliveryResult(EmailDeliveryMode.Pickup, pickupDirectory, filePath);
        }

        if (settings.Provider.Equals("Brevo", StringComparison.OrdinalIgnoreCase))
        {
            if (brevo is null) throw new InvalidOperationException("Thiếu dịch vụ gửi email Brevo.");
            return await brevo.SendAsync(message);
        }
        if (!settings.Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nhà cung cấp email không hợp lệ.");
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException("Missing SMTP host.");
        }

        using var client = new SmtpClient { Timeout = 15000 };
        client.Host = settings.Host;
        client.Port = settings.Port;
        client.EnableSsl = settings.EnableSsl;
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message, timeout.Token);
        return new EmailDeliveryResult(EmailDeliveryMode.Smtp);
    }

    private static async Task<string> WritePickupMessageAsync(string pickupDirectory, MailMessage message)
    {
        var number = NextPickupNumber(pickupDirectory);
        var subjectPart = Slug(message.Subject, "email");
        var recipientPart = Slug(message.To.FirstOrDefault()?.Address ?? "nguoi-nhan", "nguoi-nhan");
        var fileName = $"{number:0000}-{DateTime.Now:yyyyMMdd-HHmmss}-{subjectPart}-{recipientPart}.txt";
        var filePath = Path.Combine(pickupDirectory, fileName);
        var content = string.Join(Environment.NewLine, new[]
        {
            "Chế độ: PICKUP - email chỉ được lưu thành file local, chưa gửi đến hộp thư.",
            $"Thời gian local: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Từ: {message.From}",
            $"Đến: {string.Join(", ", message.To.Select(to => to.Address))}",
            $"Tiêu đề: {message.Subject}",
            new string('-', 72),
            message.Body
        });
        await File.WriteAllTextAsync(filePath, content);
        return filePath;
    }

    private static int NextPickupNumber(string pickupDirectory)
    {
        var max = 0;
        foreach (var file in Directory.EnumerateFiles(pickupDirectory, "*.txt"))
        {
            var name = Path.GetFileName(file);
            if (name.Length >= 4 && int.TryParse(name[..4], out var number) && number > max)
            {
                max = number;
            }
        }
        return max + 1;
    }

    private static string Slug(string value, string fallback)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? fallback : slug[..Math.Min(slug.Length, 60)];
    }
}