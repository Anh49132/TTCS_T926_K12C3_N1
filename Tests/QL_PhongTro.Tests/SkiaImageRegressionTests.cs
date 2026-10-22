using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using QL_PhongTro.Services;
using SkiaSharp;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class SkiaImageRegressionTests
{
    private static readonly SKColor[] Corners = [SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow];
    private readonly RoomImageStore rooms = new(Path.GetTempPath());

    [Theory]
    [InlineData(1, 0, 1, 2, 3)]
    [InlineData(2, 1, 0, 3, 2)]
    [InlineData(3, 3, 2, 1, 0)]
    [InlineData(4, 2, 3, 0, 1)]
    [InlineData(5, 0, 2, 1, 3)]
    [InlineData(6, 2, 0, 3, 1)]
    [InlineData(7, 3, 1, 2, 0)]
    [InlineData(8, 1, 3, 0, 2)]
    public async Task ExifRotationsAndReflectionsPreserveCornerPositionsAndStripMetadata(
        int orientation, int tl, int tr, int bl, int br)
    {
        using var bitmap = new SKBitmap(80, 120);
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, Corners[(y < 60 ? 0 : 2) + (x < 40 ? 0 : 1)]);
        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95);
        var original = TestImages.WithOrientation(jpeg.ToArray(), (ushort)orientation);
        var prepared = await rooms.PrepareAsync(Form(original, "phone.jpg"));
        Assert.Equal(original, prepared.OriginalBytes);
        using var result = SKBitmap.Decode(prepared.ThumbnailBytes);
        Assert.Equal(orientation < 5 ? 80 : 120, result.Width);
        Assert.Equal(orientation < 5 ? 120 : 80, result.Height);
        AssertColor(Corners[tl], result.GetPixel(5, 5));
        AssertColor(Corners[tr], result.GetPixel(result.Width - 6, 5));
        AssertColor(Corners[bl], result.GetPixel(5, result.Height - 6));
        AssertColor(Corners[br], result.GetPixel(result.Width - 6, result.Height - 6));
        using var codec = SKCodec.Create(new MemoryStream(prepared.ThumbnailBytes));
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
        Assert.DoesNotContain("Exif", System.Text.Encoding.ASCII.GetString(prepared.ThumbnailBytes));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg")]
    [InlineData(SKEncodedImageFormat.Png, "photo.png")]
    public async Task TruncatedPixelDataIsRejectedByBothPublicAndPrivateStores(SKEncodedImageFormat format, string name)
    {
        var bytes = TestImages.Encode(100, 100, format);
        var truncated = bytes[..(bytes.Length / 2)];
        await Assert.ThrowsAsync<InvalidDataException>(() => rooms.PrepareAsync(Form(truncated, name)));
        var repairs = new BaoHongImageStore(Path.GetTempPath());
        await Assert.ThrowsAsync<BaoHongValidationException>(() => repairs.PrepareAsync([Form(truncated, name)], default));
    }

    [Fact]
    public async Task IdentityResizeAndPngThumbnailPreserveTransparency()
    {
        var environment = new TestEnvironment();
        var identities = new GiayToImageStore(environment, new ConfigurationBuilder().Build());
        var prepared = await identities.PrepareAsync(Form(TestImages.Encode(2000, 1000,
            SKEncodedImageFormat.Png, new SKColor(200, 80, 40, 128)), "identity.png"));
        using var result = SKBitmap.Decode(prepared!.Bytes);
        Assert.Equal(1600, result.Width);
        Assert.Equal(800, result.Height);
        Assert.InRange(result.GetPixel(100, 100).Alpha, 126, 130);
        var thumbnail = await rooms.PrepareAsync(Form(prepared.Bytes, "room.png"));
        using var thumb = SKBitmap.Decode(thumbnail.ThumbnailBytes);
        Assert.Equal(400, thumb.Width);
        Assert.Equal(200, thumb.Height);
        Assert.InRange(thumb.GetPixel(100, 100).Alpha, 126, 130);
    }

    [Fact]
    public async Task HugeDeclaredDimensionsAreRejectedBeforePixelAllocation()
    {
        var bytes = TestImages.Encode(2, 2, SKEncodedImageFormat.Png);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), 10000);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), 5001);
        uint crc = 0xffffffff;
        foreach (var value in bytes.AsSpan(12, 17))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320);
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), ~crc);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => rooms.PrepareAsync(Form(bytes, "huge.png")));
        Assert.Contains("50 megapixel", error.Message);
    }

    private static IFormFile Form(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    private static void AssertColor(SKColor expected, SKColor actual)
    {
        Assert.InRange(Math.Abs(expected.Red - actual.Red), 0, 30);
        Assert.InRange(Math.Abs(expected.Green - actual.Green), 0, 30);
        Assert.InRange(Math.Abs(expected.Blue - actual.Blue), 0, 30);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ImageTest";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
