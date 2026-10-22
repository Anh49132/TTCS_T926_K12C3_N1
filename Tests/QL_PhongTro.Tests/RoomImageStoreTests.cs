using SkiaSharp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class RoomImageStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "room-images-" + Guid.NewGuid().ToString("N"));
    private readonly QL_PhongTro.Services.RoomImageStore store;

    public RoomImageStoreTests() => store = new QL_PhongTro.Services.RoomImageStore(root);

    [Fact]
    public async Task AcceptsJpegAndPngAndCreates400PixelThumbnails()
    {
        foreach (var (name, bytes, expectedHeight) in new[]
        {
            ("room.jpg", EncodeJpeg(4000, 2000), 200),
            ("room.png", EncodePng(640, 480), 300)
        })
        {
            var prepared = await store.PrepareAsync(File(name, bytes));
            using var thumbnail = SKBitmap.Decode(prepared.ThumbnailBytes);
            Assert.Equal(400, thumbnail.Width);
            Assert.Equal(expectedHeight, thumbnail.Height);
        }
    }

    [Fact]
    public async Task KeepsSmallImageDimensionsAndCorrectsPhoneOrientation()
    {
        var small = await store.PrepareAsync(File("small.jpg", EncodeJpeg(240, 320)));
        using (var image = SKBitmap.Decode(small.ThumbnailBytes))
        {
            Assert.Equal(240, image.Width);
            Assert.Equal(320, image.Height);
        }

        var phone = await store.PrepareAsync(File("phone.jpg", EncodeJpeg(300, 500, 6)));
        using var oriented = SKBitmap.Decode(phone.ThumbnailBytes);
        Assert.Equal(400, oriented.Width);
        Assert.Equal(240, oriented.Height);
    }

    [Fact]
    public async Task AllowsExactlyFiveMegabytesAndRejectsLargerFiles()
    {
        var jpeg = EncodeJpeg(8, 8);
        var exactLimit = new byte[QL_PhongTro.Services.RoomImageStore.MaxBytes];
        jpeg.CopyTo(exactLimit, 0);
        await store.PrepareAsync(File("limit.jpg", exactLimit));

        var tooLarge = new byte[QL_PhongTro.Services.RoomImageStore.MaxBytes + 1];
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.PrepareAsync(File("large.jpg", tooLarge)));
        Assert.Contains("5 MB", error.Message);
    }

    [Theory]
    [InlineData("animation.gif")]
    [InlineData("document.pdf")]
    [InlineData("executable.jpg")]
    [InlineData("png-renamed.jpg")]
    [InlineData("broken.png")]
    public async Task RejectsUnsupportedSpoofedAndCorruptImages(string name)
    {
        var bytes = name == "png-renamed.jpg" ? EncodePng(20, 20) : [1, 2, 3, 4, 5];
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.PrepareAsync(File(name, bytes)));
        Assert.False(Directory.Exists(Path.Combine(root, "uploads", "rooms")));
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public async Task CancelledSaveLeavesNoImageFiles()
    {
        var prepared = await store.PrepareAsync(File("room.jpg", EncodeJpeg(20, 20)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(12, prepared, cancellation.Token));
        var directory = Path.Combine(root, "uploads", "rooms", "12");
        Assert.True(!Directory.Exists(directory) || Directory.GetFiles(directory).Length == 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static IFormFile File(string name, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name);
    }

    private static byte[] EncodeJpeg(int width, int height, ushort? orientation = null)
    {
        var jpeg = TestImages.Encode(width, height, SKEncodedImageFormat.Jpeg);
        return orientation is null ? jpeg : TestImages.WithOrientation(jpeg, orientation.Value);
    }

    private static byte[] EncodePng(int width, int height) =>
        TestImages.Encode(width, height, SKEncodedImageFormat.Png);
}