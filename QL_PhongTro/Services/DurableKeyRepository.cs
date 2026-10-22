using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace QL_PhongTro.Services;

// The online key ring is separate from Windows DPAPI. Encrypt entire XML objects
// before storage, using a stable 256-bit secret kept only in host configuration.
public sealed class DurableKeyRepository(DurableFiles files, IConfiguration configuration) : IXmlRepository
{
    private readonly byte[] key = ReadKey(configuration);
    private static byte[] ReadKey(IConfiguration configuration)
    {
        var key = Convert.FromBase64String(configuration["ObjectStorage:KeyEncryptionKey"] ?? "");
        if (key.Length != 32) throw new InvalidOperationException("A 256-bit key-ring encryption secret is required.");
        return key;
    }
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var result = new List<XElement>();
        foreach (var name in files.ListAsync("keyring/").GetAwaiter().GetResult())
        {
            var bytes = files.ReadAsync(name).GetAwaiter().GetResult() ?? throw new IOException("Key-ring object missing.");
            if (bytes.Length < 29 || bytes[0] != 1) throw new CryptographicException("Invalid key-ring envelope.");
            var plaintext = new byte[bytes.Length - 29];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(1, 12), bytes.AsSpan(29), bytes.AsSpan(13, 16), plaintext, "NhaTotKeyRing-v1"u8);
            using var reader = System.Xml.XmlReader.Create(new MemoryStream(plaintext), new System.Xml.XmlReaderSettings
            { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
            result.Add(XElement.Load(reader));
            CryptographicOperations.ZeroMemory(plaintext);
        }
        return result;
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        var plaintext = Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting));
        var envelope = new byte[plaintext.Length + 29]; envelope[0] = 1;
        RandomNumberGenerator.Fill(envelope.AsSpan(1, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(envelope.AsSpan(1, 12), plaintext, envelope.AsSpan(29), envelope.AsSpan(13, 16), "NhaTotKeyRing-v1"u8);
        CryptographicOperations.ZeroMemory(plaintext);
        files.WriteAsync("keyring/" + Guid.NewGuid().ToString("N"), envelope).GetAwaiter().GetResult();
    }
}
