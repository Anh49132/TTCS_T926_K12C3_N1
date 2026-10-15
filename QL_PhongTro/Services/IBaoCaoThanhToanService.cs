using QL_PhongTro.Models;

namespace QL_PhongTro.Services;

public sealed record HuyBaoCaoThanhToanResult(string MaHoaDon, bool DaHuy);

public interface IBaoCaoThanhToanService
{
    Task<ThanhToanHoaDon?> LayBaoCaoChoXacNhanAsync(int actorId, int invoiceId, CancellationToken cancellationToken = default);
    Task<HuyBaoCaoThanhToanResult> HuyAsync(int actorId, int invoiceId, CancellationToken cancellationToken = default);
    Task GuiAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        CancellationToken cancellationToken = default);
    Task GuiAsync(int actorId, int invoiceId, decimal? amount, DateOnly? paymentDate, string? paymentMethod,
        IFormFile? proofImage, CancellationToken cancellationToken = default);
}
