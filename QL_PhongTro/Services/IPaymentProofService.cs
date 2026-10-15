namespace QL_PhongTro.Services;

public sealed record PaymentProofLink(string Token, DateTime ExpiresUtc);
public sealed record PaymentProofFile(string Path, string ContentType);

public interface IPaymentProofService
{
    Task<PaymentProofLink> CreateLinkAsync(int actorId, int paymentId, CancellationToken ct = default);
    Task<PaymentProofFile> OpenAsync(int actorId, int paymentId, string token, CancellationToken ct = default);
}

public sealed class PaymentProofLinkExpiredException : Exception { }
