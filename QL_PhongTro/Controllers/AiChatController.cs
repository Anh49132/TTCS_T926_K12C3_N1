using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

public sealed class AiChatInput
{
    [Required, StringLength(2000)] public string Message { get; set; } = "";
    [StringLength(32)] public string? ConversationId { get; set; }
}
[Authorize(Roles = "KHACH_THUE,CHU_NHA,QUAN_LY,ADMIN")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiChatController(AiChatService chat) : Controller
{
    [HttpPost("/api/chatbot"), ValidateAntiForgeryToken, EnableRateLimiting("chatbot")]
    [RequestSizeLimit(12000)]
    public async Task<IActionResult> Ask([FromBody] AiChatInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Câu hỏi cần từ 1 đến 2.000 ký tự. Hãy kiểm tra và thử lại." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) return Forbid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        try { return Json(await chat.AskAsync(actor, input.Message, input.ConversationId, timeout.Token)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest(new { message = "Câu hỏi hoặc tham số tra cứu không hợp lệ." }); }
        catch (ChatUnavailableException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { message = "AI phản hồi quá lâu. Hãy thử lại với câu hỏi ngắn hơn." }); }
        catch (Exception) when (!ct.IsCancellationRequested)
        { return StatusCode(503, new { message = "Chưa thể tra cứu dữ liệu lúc này. Vui lòng thử lại sau." }); }
    }
}
