using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace QL_PhongTro.Services;

public sealed class BrevoOptions
{
    public string ApiKey { get; set; } = "";
    public string SenderName { get; set; } = "NhàTốt";
}

public sealed class BrevoEmailTransport(HttpClient client, IOptions<BrevoOptions> options)
{
    public async Task<EmailDeliveryResult> SendAsync(MailMessage message)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new InvalidOperationException("Thiếu cấu hình Brevo__ApiKey.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", options.Value.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = message.From!.Address, name = options.Value.SenderName },
            to = message.To.Select(to => new { email = to.Address }).ToArray(),
            subject = message.Subject,
            textContent = message.Body
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, timeout.Token); }
        catch (HttpRequestException) { throw new HttpRequestException("Không kết nối được dịch vụ email Brevo."); }
        using (response)
        {
            // No response bodies or mail content in errors; queues own retries.
            if (response.StatusCode != HttpStatusCode.Created)
                throw new HttpRequestException("Brevo chưa chấp nhận email. Kiểm tra cấu hình, quyền và hạn mức.", null, response.StatusCode);
            try
            {
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                if (!document.RootElement.TryGetProperty("messageId", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
                    throw new InvalidOperationException("Brevo không trả mã xác nhận tiếp nhận email.");
            }
            catch (JsonException) { throw new InvalidOperationException("Phản hồi dịch vụ email không hợp lệ."); }
        }
        return new(EmailDeliveryMode.Brevo);
    }
}
