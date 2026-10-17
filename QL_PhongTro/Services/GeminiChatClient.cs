using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace QL_PhongTro.Services;

public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-3.1-flash-lite";
}
public sealed class ChatUnavailableException(string message, int status = 503) : Exception(message)
{ public int Status { get; } = status; }
public interface IGeminiChatClient
{
    Task<JsonElement> GenerateAsync(object payload, CancellationToken ct);
}
public sealed class GeminiChatClient(HttpClient http, IOptions<GeminiOptions> options) : IGeminiChatClient
{
    public async Task<JsonElement> GenerateAsync(object payload, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new ChatUnavailableException("Trợ lý AI chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
        if (!Regex.IsMatch(settings.Model, "^gemini-[a-zA-Z0-9.-]{1,80}$"))
            throw new ChatUnavailableException("Cấu hình model AI không hợp lệ.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{settings.Model}:generateContent");
        request.Headers.Add("x-goog-api-key", settings.ApiKey.Trim());
        var body = JsonSerializer.SerializeToNode(payload)!.AsObject();
        if (settings.Model.StartsWith("gemini-3", StringComparison.Ordinal) && body["generationConfig"] is JsonObject generation)
            generation["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "minimal" };
        request.Content = JsonContent.Create(body);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ChatUnavailableException("Gemini đang hết hạn mức hoặc quá tải. Vui lòng thử lại sau.", 429);
            if (!response.IsSuccessStatusCode)
                throw new ChatUnavailableException(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Gemini chưa chấp nhận cấu hình hiện tại. Vui lòng kiểm tra khóa API và model."
                    : "Chưa kết nối được Gemini. Vui lòng thử lại sau.");
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!json.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0
                || !candidates[0].TryGetProperty("content", out var content))
                throw new ChatUnavailableException("AI chưa tạo được câu trả lời. Hãy diễn đạt lại câu hỏi.");
            return content.Clone();
        }
        catch (HttpRequestException) { throw new ChatUnavailableException("Không kết nối được dịch vụ AI. Vui lòng thử lại."); }
        catch (JsonException) { throw new ChatUnavailableException("Dịch vụ AI trả về dữ liệu không hợp lệ. Vui lòng thử lại."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ChatUnavailableException("AI phản hồi quá lâu. Vui lòng thử lại."); }
    }
}
