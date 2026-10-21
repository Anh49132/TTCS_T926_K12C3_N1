using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace QL_PhongTro.Services;

public sealed class PasswordResetOptions
{
    public string PublicBaseUrl { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    // Explicit local email preview only; never enabled automatically or in production.
    public string PickupDirectory { get; set; } = "";
}

public interface IPasswordEmailSender
{
    Task SendAsync(string email, string resetUrl);
}

public interface ITemporaryPasswordEmailSender
{
    Task SendTemporaryAsync(string email, string name, string role, string password);
}

public interface IRegistrationEmailSender
{
    Task<EmailDeliveryResult> SendConfirmationAsync(string email, string name, string code);
}

public interface IAppointmentEmailSender
{
    Task SendConfirmedAsync(string email, string name, string requestCode, DateTime appointmentLocal, int requestId);
    Task SendRescheduledAsync(string email, string name, string requestCode, DateTime appointmentLocal, int requestId);
    Task SendNewRequestAsync(string email, string ownerName, string requestCode, string tenantName, string roomCode, string requestType, DateOnly desiredDate, int requestId);
}

public interface IQueuedInvoiceEmailSender
{
    Task SendIssuedAsync(string email, string subject, string body, string path);
}

public interface IRepairEmailSender
{
    Task SendRepairAsync(string email,string subject,string body,string path);
}

public interface IPaymentEmailSender
{
    Task SendPaymentAsync(string email,string subject,string body,string path);
}

public enum EmailDeliveryMode { Pickup, Smtp }

public sealed record EmailDeliveryResult(EmailDeliveryMode Mode, string? PickupDirectory = null, string? PickupFilePath = null);

public sealed class PasswordEmailSender(IOptions<PasswordResetOptions> options, IWebHostEnvironment environment) : IPasswordEmailSender, ITemporaryPasswordEmailSender, IRegistrationEmailSender, IAppointmentEmailSender, IQueuedInvoiceEmailSender, IRepairEmailSender, IPaymentEmailSender
{
    public Task SendPaymentAsync(string email,string subject,string body,string path)
    {
        if(!Uri.TryCreate(options.Value.PublicBaseUrl,UriKind.Absolute,out var origin)
            || (origin.Scheme!="https" && !(environment.IsDevelopment() && origin.Scheme=="http" && origin.IsLoopback))
            || !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)
            || !System.Text.RegularExpressions.Regex.IsMatch(path,@"^/(HoaDonDichVu/Details|ThongBao/HoaDon)/[1-9][0-9]*(#payment-report-[1-9][0-9]*)?$"))
            throw new InvalidOperationException("Liên kết thanh toán không hợp lệ.");
        return DeliverAsync(email,subject,body+"\n\nXem thanh toán: "+options.Value.PublicBaseUrl.TrimEnd('/')+path);
    }
    public Task SendRepairAsync(string email,string subject,string body,string path)
    {
        if(!Uri.TryCreate(options.Value.PublicBaseUrl,UriKind.Absolute,out var origin)
            || (origin.Scheme!="https" && !(environment.IsDevelopment() && origin.Scheme=="http" && origin.IsLoopback))
            || !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)
            || !System.Text.RegularExpressions.Regex.IsMatch(path,@"^/(BaoHong|QuanLyBaoHong|ChuNhaBaoHong)/Details/[1-9][0-9]*$"))
            throw new InvalidOperationException("Liên kết báo hỏng không hợp lệ.");
        return DeliverAsync(email,subject,body+"\n\nXem báo hỏng: "+options.Value.PublicBaseUrl.TrimEnd('/')+path);
    }
    public Task SendIssuedAsync(string email, string subject, string body, string path)
    {
        if (!Uri.TryCreate(options.Value.PublicBaseUrl, UriKind.Absolute, out var origin)
            || (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback))
            || !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)
            || !path.StartsWith("/ThongBao/HoaDon/", StringComparison.Ordinal))
            throw new InvalidOperationException("Configure a trusted PublicBaseUrl.");
        return DeliverAsync(email, subject, body + "\n\nXem hóa đơn: " + options.Value.PublicBaseUrl.TrimEnd('/') + path);
    }
    public Task SendAsync(string email, string resetUrl) => DeliverAsync(email, "Đặt lại mật khẩu Nhà Trọ",
        "Bạn đã yêu cầu đặt lại mật khẩu Nhà Trọ. Mở liên kết sau trong vòng 30 phút; liên kết chỉ dùng được một lần:\n\n" + resetUrl + "\n\nNếu bạn không yêu cầu, hãy bỏ qua email này.");

    public Task SendTemporaryAsync(string email, string name, string role, string password)
    {
        var url = options.Value.PublicBaseUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback)) ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new InvalidOperationException("Configure a trusted PublicBaseUrl.");
        }

        return DeliverAsync(email, "Tài khoản Nhà Trọ - yêu cầu đổi mật khẩu lần đầu",
            $"Xin chào {name},\n\nQuản trị viên đã cấp tài khoản Nhà Trọ cho bạn.\nVai trò: {role}\nEmail đăng nhập: {email}\nMật khẩu tạm: {password}\nĐăng nhập: {url.TrimEnd('/')}/Account/Login\n\nBạn bắt buộc đặt mật khẩu mới ngay lần đăng nhập đầu tiên trước khi sử dụng hệ thống. Không chia sẻ mật khẩu này. Nếu bạn không yêu cầu tài khoản, hãy liên hệ quản trị viên.");
    }

    public Task<EmailDeliveryResult> SendConfirmationAsync(string email, string name, string code)
    {
        return DeliverAsync(email, "Xác nhận tài khoản Nhà Trọ",
            $"Xin chào {name},\n\nMã xác nhận tài khoản của bạn là: {code}\n\nMã có hiệu lực trong 15 phút. Nếu bạn không thực hiện đăng ký, hãy bỏ qua email này.");
    }

    public Task SendConfirmedAsync(string email, string name, string requestCode, DateTime appointmentLocal, int requestId)
    {
        var baseUrl = options.Value.PublicBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback)) ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new InvalidOperationException("Configure a trusted PublicBaseUrl.");

        var detailUrl = $"{baseUrl.TrimEnd('/')}/LichHen/ChiTiet/{requestId}";
        return DeliverAsync(email, $"Lịch hẹn xem phòng đã được xác nhận - {requestCode}",
            $"Xin chào {name},\n\nLịch hẹn của yêu cầu {requestCode} đã được chủ nhà xác nhận.\n" +
            $"Thời gian: {appointmentLocal:dd/MM/yyyy HH:mm}\n" +
            $"Xem chi tiết yêu cầu: {detailUrl}\n\nNếu cần thay đổi, vui lòng liên hệ chủ nhà qua thông tin trong yêu cầu.");
    }

    private string RequestUrl(int requestId)
    {
        var baseUrl = options.Value.PublicBaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback)) ||
            !string.IsNullOrEmpty(origin.UserInfo) || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            throw new InvalidOperationException("Configure a trusted PublicBaseUrl.");
        return $"{baseUrl.TrimEnd('/')}/LichHen/ChiTiet/{requestId}";
    }

    public Task SendRescheduledAsync(string email, string name, string requestCode, DateTime appointmentLocal, int requestId) =>
        DeliverAsync(email, $"Lịch hẹn xem phòng đã thay đổi - {requestCode}",
            $"Xin chào {name},\n\nChủ nhà đã đổi lịch hẹn của yêu cầu {requestCode}.\n" +
            $"Thời gian mới: {appointmentLocal:dd/MM/yyyy HH:mm}\n" +
            $"Xem chi tiết yêu cầu: {RequestUrl(requestId)}\n\nVui lòng kiểm tra lịch mới trước khi đến xem phòng.");

    public Task SendNewRequestAsync(string email, string ownerName, string requestCode, string tenantName, string roomCode, string requestType, DateOnly desiredDate, int requestId) =>
        DeliverAsync(email, $"Yêu cầu thuê phòng mới - {requestCode}",
            $"Xin chào {ownerName},\n\nBạn có yêu cầu mới từ khách {tenantName}.\n" +
            $"Mã yêu cầu: {requestCode}\nPhòng: {roomCode}\nLoại yêu cầu: {QL_PhongTro.Models.TrangThaiYeuCau.LoaiLabel(requestType)}\n" +
            $"Ngày mong muốn: {desiredDate:dd/MM/yyyy}\n" +
            $"Xử lý yêu cầu: {RequestUrl(requestId)}\n\nBạn nên phản hồi yêu cầu trong vòng 24 giờ.");

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

            // Relative pickup paths use this machine's temp directory, outside the repository.
            var pickupDirectory = Path.IsPathFullyQualified(settings.PickupDirectory)
                ? settings.PickupDirectory
                : Path.GetFullPath(settings.PickupDirectory, Path.GetTempPath());
            Directory.CreateDirectory(pickupDirectory);
            var filePath = await WritePickupMessageAsync(pickupDirectory, message);
            return new EmailDeliveryResult(EmailDeliveryMode.Pickup, pickupDirectory, filePath);
        }

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
