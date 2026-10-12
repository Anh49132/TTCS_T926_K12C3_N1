using System.Text.Json;

namespace QL_PhongTro.Services;

public sealed record ContractFile(string Id, string Name, string ContentType, long Size);
public sealed record ContractDocuments(string Template, string Terms, List<ContractFile> Files);

// Private storage belongs to the selected database, including isolated demo databases.
public sealed class ContractDocumentStore(string databasePath)
{
    private string Root => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!,
        Path.GetFileName(databasePath) + ".contract-documents");
    private string Folder(int id) => Path.Combine(Root, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public async Task<ContractDocuments> ReadAsync(int id, CancellationToken ct = default)
    {
        var path = Path.Combine(Folder(id), "manifest.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<ContractDocuments>(await File.ReadAllTextAsync(path, ct))!
            : new("Chưa ghi nhận mẫu hợp đồng", "", []);
    }
    public static async Task<string> ValidateAsync(IReadOnlyCollection<IFormFile> files, CancellationToken ct = default)
    {
        if (files.Count > 5) return "Chỉ đính kèm tối đa 5 tệp.";
        foreach (var file in files)
        {
            if (file.Length <= 0 || file.Length > 10 * 1024 * 1024) return "Mỗi tệp phải lớn hơn 0 và không quá 10 MB.";
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var stream = file.OpenReadStream();
            var header = new byte[8];
            var count = await stream.ReadAtLeastAsync(header, 8, throwOnEndOfStream: false, cancellationToken: ct);
            var valid = extension switch
            {
                ".pdf" => count >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                ".jpg" or ".jpeg" => count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
                ".png" => count == 8 && header.AsSpan().SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
                _ => false
            };
            if (!valid) return "Chỉ nhận tệp PDF, JPG, PNG đúng định dạng.";
        }
        return "";
    }
    public async Task WriteAsync(int id, string terms, IReadOnlyCollection<IFormFile> files, CancellationToken ct = default)
    {
        var error = await ValidateAsync(files, ct);
        if (error.Length > 0) throw new InvalidOperationException(error);
        var folder = Folder(id);
        // Contract IDs are unique; never overwrite an existing signed snapshot.
        if (Directory.Exists(folder)) throw new IOException("Tài liệu hợp đồng đã tồn tại.");
        Directory.CreateDirectory(folder);
        try
        {
            var saved = new List<ContractFile>();
            foreach (var file in files)
            {
                var key = Guid.NewGuid().ToString("N");
                await using (var output = new FileStream(Path.Combine(folder, key), FileMode.CreateNew))
                    await file.CopyToAsync(output, ct);
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                saved.Add(new(key, Path.GetFileName(file.FileName.Replace('\\', '/')), ext == ".pdf" ? "application/pdf" : ext == ".png" ? "image/png" : "image/jpeg", file.Length));
            }
            await File.WriteAllTextAsync(Path.Combine(folder, "manifest.json"),
                JsonSerializer.Serialize(new ContractDocuments("Mẫu hợp đồng tiêu chuẩn của hệ thống", terms.Trim(), saved)), ct);
        }
        catch { Delete(id); throw; }
    }
    public void Delete(int id) { if (Directory.Exists(Folder(id))) Directory.Delete(Folder(id), true); }
    public string FilePath(int id, string fileId) => Path.Combine(Folder(id), fileId);
}
