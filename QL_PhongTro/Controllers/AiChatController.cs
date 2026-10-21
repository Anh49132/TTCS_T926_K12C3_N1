using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using QL_PhongTro.Services;

namespace QL_PhongTro.Controllers;

public sealed class AiChatInput
{
    [Required, StringLength(2000)] public string Message { get; set; } = "";
    [StringLength(32)] public string? ConversationId { get; set; }
}
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiChatController(AiChatService chat, IDataProtectionProvider protection) : Controller
{
    private string GuestId()
    {
        const string cookieName = ".NhaTot.ChatGuest";
        var protector = protection.CreateProtector("NhaTot.ChatGuest.v1").ToTimeLimitedDataProtector();
        if (Request.Cookies.TryGetValue(cookieName, out var value) && value.Length < 2048)
        {
            try { var id = protector.Unprotect(value); if (Guid.TryParseExact(id,"N",out _)) return id; }
            catch (Exception ex) when (ex is CryptographicException or FormatException) { }
        }
        var guest = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(cookieName, protector.Protect(guest, TimeSpan.FromHours(1)), new CookieOptions {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1), IsEssential = true, Path = "/"
        });
        return guest;
    }
    [HttpPost("/api/chatbot"), ValidateAntiForgeryToken, EnableRateLimiting("chatbot")]
    [RequestSizeLimit(12000)]
    public async Task<IActionResult> Ask([FromBody] AiChatInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Câu hỏi cần từ 1 đến 2.000 ký tự. Hãy kiểm tra và thử lại." });
        var loggedIn = User.Identity?.IsAuthenticated == true;
        var actor = 0;
        if (loggedIn && (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actor)
            || !User.IsInRole("KHACH_THUE") && !User.IsInRole("CHU_NHA") && !User.IsInRole("QUAN_LY") && !User.IsInRole("ADMIN"))) return Forbid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        try { return Json(loggedIn
            ? await chat.AskAsync(actor, input.Message, input.ConversationId, timeout.Token)
            : await chat.AskGuestAsync(GuestId(), input.Message, input.ConversationId, timeout.Token)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return BadRequest(new { message = "Câu hỏi hoặc tham số tra cứu không hợp lệ." }); }
        catch (ChatUnavailableException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(504, new { message = "AI phản hồi quá lâu. Hãy thử lại với câu hỏi ngắn hơn." }); }
        catch (Exception) when (!ct.IsCancellationRequested)
        { return StatusCode(503, new { message = "Chưa thể tra cứu dữ liệu lúc này. Vui lòng thử lại sau." }); }
    }
}
