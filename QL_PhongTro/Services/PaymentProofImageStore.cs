using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public sealed class PaymentProofImageStore(string root, DurableFiles? durable = null)
{
    private readonly BaoHongImageStore store = new(root, durable, "proofs");

    public async Task<PreparedBaoHongImage?> PrepareAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return null;
        if (file.Length == 0 && string.IsNullOrWhiteSpace(file.FileName)) return null;
        if (file.Length == 0)
            throw new GhiThuValidationException(new Dictionary<string, string[]> { ["AnhChungTu"] = ["Tệp ảnh rỗng."] });
        try
        {
            return (await store.PrepareAsync([file], ct))[0];
        }
        catch (BaoHongValidationException ex)
        {
            throw new GhiThuValidationException(new Dictionary<string, string[]> { ["AnhChungTu"] = [ex.Message] });
        }
    }

    public string FilePath(string key) => store.FilePath(key);
    public Task SaveAsync(string key, byte[] bytes, CancellationToken ct) => store.SaveAsync(key, bytes, ct);
    public void Delete(string key) => store.Delete(key);
}
