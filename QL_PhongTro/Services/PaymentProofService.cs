using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

public sealed class PaymentProofService(
    AppDbContext db,
    PaymentProofImageStore images,
    IDataProtectionProvider protectionProvider,
    ITimeProvider clock) : IPaymentProofService
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);
    private readonly IDataProtector protector = protectionProvider.CreateProtector("InvoicePaymentProof.View.v1");

    public async Task<PaymentProofLink> CreateLinkAsync(int actorId, int paymentId, CancellationToken ct = default)
    {
        var payment = await GetAuthorizedPaymentAsync(actorId, paymentId, ct);
        if (payment is null) throw new KeyNotFoundException();
        var expires = clock.UtcNow.Add(LinkLifetime);
        var token = protector.Protect(JsonSerializer.Serialize(new LinkPayload(paymentId, actorId, expires)));
        return new(token, expires);
    }

    public async Task<PaymentProofFile> OpenAsync(int actorId, int paymentId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException();
        LinkPayload? payload;
        try { payload = JsonSerializer.Deserialize<LinkPayload>(protector.Unprotect(token)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        { throw new UnauthorizedAccessException(); }

        if (payload is null || payload.PaymentId != paymentId || payload.ActorId != actorId)
            throw new UnauthorizedAccessException();
        if (payload.ExpiresUtc <= clock.UtcNow) throw new PaymentProofLinkExpiredException();

        var payment = await GetAuthorizedPaymentAsync(actorId, paymentId, ct);
        if (payment is null) throw new UnauthorizedAccessException();
        var path = images.FilePath(payment.ImageKey);
        if (!System.IO.File.Exists(path)) throw new KeyNotFoundException();
        return new(path, payment.ContentType);
    }

    private Task<AuthorizedPayment?> GetAuthorizedPaymentAsync(int actorId, int paymentId, CancellationToken ct) =>
        (from payment in db.ThanhToans.AsNoTracking()
         join invoice in db.HoaDons.AsNoTracking() on payment.HoaDonId equals invoice.Id
         join contract in db.HopDongs.AsNoTracking() on invoice.HopDongId equals contract.Id
         join room in db.PhongTros.AsNoTracking() on contract.PhongId equals room.Id
         join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
         join account in db.TaiKhoans.AsNoTracking() on actorId equals account.Id
         where payment.Id == paymentId && payment.AnhChungTuKey != null
               && payment.AnhChungTuContentType != null
               && account.DangHoatDong && !account.IsDeleted
               && (account.VaiTro == "ADMIN" || (account.VaiTro == "CHU_NHA" && building.ChuNhaId == actorId))
         select new AuthorizedPayment(payment.AnhChungTuKey!, payment.AnhChungTuContentType!))
        .SingleOrDefaultAsync(ct);

    private sealed record LinkPayload(int PaymentId, int ActorId, DateTime ExpiresUtc);
    private sealed record AuthorizedPayment(string ImageKey, string ContentType);
}
