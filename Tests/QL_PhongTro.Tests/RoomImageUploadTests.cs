using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class PermissionTests
{
    [Fact]
    public async Task OwnerUploadsJpegAndPngWithOriginalAndSmallOrderedThumbnail()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);

        using var jpeg = await Upload(owner, roomId, token, "room.jpg", EncodeJpeg(1200, 800));
        Assert.Equal(HttpStatusCode.OK, jpeg.StatusCode);
        using var jpegJson = JsonDocument.Parse(await jpeg.Content.ReadAsStringAsync());
        Assert.Equal(1, jpegJson.RootElement.GetProperty("order").GetInt32());
        Assert.Equal(1, jpegJson.RootElement.GetProperty("count").GetInt32());

        using var png = await Upload(owner, roomId, token, "room.png", EncodePng(640, 480));
        Assert.Equal(HttpStatusCode.OK, png.StatusCode);
        using var pngJson = JsonDocument.Parse(await png.Content.ReadAsStringAsync());
        Assert.Equal(2, pngJson.RootElement.GetProperty("order").GetInt32());
        Assert.Equal(2, pngJson.RootElement.GetProperty("count").GetInt32());

        Assert.Equal(2L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        var originalPath = jpegJson.RootElement.GetProperty("originalPath").GetString()!;
        var thumbnailPath = jpegJson.RootElement.GetProperty("thumbnailPath").GetString()!;
        using var guest = Client();
        var original = await guest.GetByteArrayAsync(originalPath);
        var thumbnail = await guest.GetByteArrayAsync(thumbnailPath);
        Assert.Equal(EncodeJpeg(1200, 800).Length, original.Length);
        Assert.True(thumbnail.Length < original.Length);
        using var thumbImage = await Image.LoadAsync(new MemoryStream(thumbnail));
        Assert.Equal(400, thumbImage.Width);
        Assert.Equal(267, thumbImage.Height);

        var edit = await owner.GetStringAsync($"/PhongTro/Edit/{roomId}");
        Assert.Contains("2/8 ảnh", edit);
        Assert.Contains("data-image-id", edit);
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task ConfiguredRoomImagePathOutsideWebRootIsServedAtUploadsRooms()
    {
        var externalStorage = Path.Combine(temp, "external-room-images");
        using var externalFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(appPath);
            builder.UseWebRoot(Path.Combine(temp, "external-wwwroot"));
            builder.UseSetting("DatabasePath", database);
            builder.UseSetting("RoomImagesPath", externalStorage);
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
        using var owner = externalFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(owner, "CHU_NHA");
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        var token = await RoomImageToken(owner, roomId);

        using var upload = await Upload(owner, roomId, token, "external.png", EncodePng(640, 480));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var json = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var originalPath = json.RootElement.GetProperty("originalPath").GetString()!;
        var thumbnailPath = json.RootElement.GetProperty("thumbnailPath").GetString()!;
        Assert.True(File.Exists(Path.Combine(externalStorage, roomId.ToString(), Path.GetFileName(originalPath))));
        Assert.True(File.Exists(Path.Combine(externalStorage, roomId.ToString(), Path.GetFileName(thumbnailPath))));

        using var guest = externalFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var original = await guest.GetAsync(originalPath);
        using var thumbnail = await guest.GetAsync(thumbnailPath);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.True((await thumbnail.Content.ReadAsByteArrayAsync()).Length > 0);
        CleanupExternalRoomImages(externalStorage, roomId);
    }

    [Fact]
    public async Task OwnerCanUploadEightImagesButNinthIsRejected()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        var bytes = EncodeJpeg(64, 48);

        for (var index = 0; index < 8; index++)
        {
            using var response = await Upload(owner, roomId, token, $"room-{index}.jpg", bytes);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var ninth = await Upload(owner, roomId, token, "ninth.jpg", bytes);
        Assert.Equal(HttpStatusCode.Conflict, ninth.StatusCode);
        Assert.Contains("đủ 8 ảnh", await ninth.Content.ReadAsStringAsync());
        Assert.Equal(8L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task SingleImageIsPrimaryAndItsThumbnailIsUsedOnListing()
    {
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        using var upload = await Upload(owner, roomId, token, "only.jpg", EncodeJpeg(800, 600));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var edit = WebUtility.HtmlDecode(await owner.GetStringAsync($"/PhongTro/Edit/{roomId}"));
        Assert.Contains("Ảnh đại diện", edit);
        Assert.Contains("data-primary-label", edit);
        Assert.Contains("1/8 ảnh", edit);

        using var guest = Client();
        var index = WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang"));
        using var saved = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var thumbnail = saved.RootElement.GetProperty("thumbnailPath").GetString();
        Assert.Contains($"src=\"{thumbnail}\"", index);
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task InvalidUploadFormatsAndOversizedFilesAreRejectedWithoutRowsOrFiles()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        var png = EncodePng(20, 20);
        var corruptPng = png[..Math.Min(24, png.Length)];
        var executable = new byte[] { 0x4d, 0x5a, 0x90, 0x00, 0x03, 0x00 };
        var pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n");
        var gif = Convert.FromBase64String("R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=");
        var tooLarge = new byte[QL_PhongTro.Services.RoomImageStore.MaxBytes + 1];
        var rejected = new (string Name, byte[] Bytes, string Message)[]
        {
            ("animation.gif", gif, "JPG hoặc PNG"),
            ("document.pdf", pdf, "JPG hoặc PNG"),
            ("payload.jpg", executable, "hỏng"),
            ("renamed.jpg", png, "không khớp"),
            ("broken.png", corruptPng, "hỏng"),
            ("large.jpg", tooLarge, "5 MB")
        };

        foreach (var (name, bytes, message) in rejected)
        {
            using var response = await Upload(owner, roomId, token, name, bytes);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(message, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        var directory = RoomImageDirectory(roomId);
        Assert.True(!Directory.Exists(directory) || Directory.GetFiles(directory).Length == 0);
    }

    [Fact]
    public async Task AnonymousOrAnotherAccountCannotUploadToRoom()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        var bytes = EncodeJpeg(24, 24);

        using (var anonymous = Client())
        using (var response = await Upload(anonymous, roomId, string.Empty, "room.jpg", bytes))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var otherOwner = await Login("CHU_NHA");
        var token = await RoomImageToken(otherOwner, roomId);
        Execute("UPDATE toa_nha SET chu_nha_id=$owner WHERE id=$building",
            ("$owner", accounts["ADMIN"]), ("$building", buildingId));
        using var forbidden = await Upload(otherOwner, roomId, token, "room.jpg", bytes);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        Assert.True(!Directory.Exists(RoomImageDirectory(roomId)) || Directory.GetFiles(RoomImageDirectory(roomId)).Length == 0);
    }

    [Fact]
    public async Task ReorderPersistsExactOrderAcrossTabsAndNewUploadAppends()
    {
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        var imageIds = new List<int>();
        for (var index = 0; index < 5; index++)
        {
            using var upload = await Upload(owner, roomId, token, $"sort-{index}.jpg", EncodeJpeg(80, 60));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var result = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            imageIds.Add(result.RootElement.GetProperty("id").GetInt32());
        }

        var firstTabOrder = new[] { imageIds[2], imageIds[0], imageIds[1], imageIds[3], imageIds[4] };
        using var firstSave = await Reorder(owner, roomId, token, firstTabOrder);
        Assert.Equal(HttpStatusCode.OK, firstSave.StatusCode);
        Assert.Equal(firstTabOrder, ReadImageIds(roomId));

        var secondTabOrder = new[] { imageIds[1], imageIds[2], imageIds[0], imageIds[4], imageIds[3] };
        using var secondSave = await Reorder(owner, roomId, token, secondTabOrder);
        Assert.Equal(HttpStatusCode.OK, secondSave.StatusCode);
        Assert.Equal(secondTabOrder, ReadImageIds(roomId));
        Assert.Equal(imageIds.Order().ToArray(), ReadImageIds(roomId).Order().ToArray());

        using var guest = Client();
        var list = WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang"));
        var leadingThumbnail = Scalar("SELECT duong_dan_anh_nho FROM anh_phong WHERE id=$id", ("$id", secondTabOrder[0]))?.ToString();
        Assert.Contains($"src=\"{leadingThumbnail}\"", list);
        using var detailResponse = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        var detailPaths = detail.RootElement.GetProperty("anh").EnumerateArray()
            .Select(image => image.GetProperty("duongDan").GetString()).ToArray();
        var expectedPaths = secondTabOrder.Select(imageId => Scalar("SELECT duong_dan FROM anh_phong WHERE id=$id", ("$id", imageId))?.ToString()).ToArray();
        Assert.Equal(expectedPaths, detailPaths);

        using var added = await Upload(owner, roomId, token, "appended.jpg", EncodeJpeg(80, 60));
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var addedJson = JsonDocument.Parse(await added.Content.ReadAsStringAsync());
        Assert.Equal(6, addedJson.RootElement.GetProperty("order").GetInt32());
        Assert.Equal(6, ReadImageIds(roomId).Length);
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task ReorderRejectsMissingExtraAndDuplicateIdsWithoutChangingOrder()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        var imageIds = new List<int>();
        for (var index = 0; index < 3; index++)
        {
            using var upload = await Upload(owner, roomId, token, $"invalid-order-{index}.jpg", EncodeJpeg(80, 60));
            using var result = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            imageIds.Add(result.RootElement.GetProperty("id").GetInt32());
        }
        var before = ReadImageIds(roomId);
        var invalidLists = new[]
        {
            new[] { imageIds[0], imageIds[2] },
            new[] { imageIds[0], imageIds[1], imageIds[2], 999999 },
            new[] { imageIds[0], imageIds[0], imageIds[2] }
        };

        foreach (var invalid in invalidLists)
        {
            using var response = await Reorder(owner, roomId, token, invalid);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Danh sách ảnh đã thay đổi", await response.Content.ReadAsStringAsync());
            Assert.Equal(before, ReadImageIds(roomId));
        }
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task OwnerCannotReorderAnotherOwnersRoom()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        Execute("INSERT INTO anh_phong(phong_id,duong_dan,duong_dan_anh_nho,thu_tu,ngay_tao) VALUES($room,'/fake/one.jpg','/fake/one-thumb.jpg',1,$created)",
            ("$room", roomId), ("$created", DateTime.UtcNow.ToString("O")));
        using var ownerA = await Login("CHU_NHA");
        var token = await RoomImageToken(ownerA, roomId);
        var ownerBEmail = "second-owner@s104.test";
        var ownerBHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
        var now = DateTime.UtcNow.ToString("O");
        Execute("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat) VALUES('Owner B',$email,'0970000001',$hash,'CHU_NHA',1,0,0,$now,$now)",
            ("$email", ownerBEmail), ("$hash", ownerBHash), ("$now", now));
        var ownerBId = Convert.ToInt32(Scalar("SELECT id FROM tai_khoan WHERE email=$email", ("$email", ownerBEmail)));
        Execute("UPDATE toa_nha SET chu_nha_id=$owner WHERE id=$building", ("$owner", ownerBId), ("$building", buildingId));

        using var response = await Reorder(ownerA, roomId, token, [1]);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room AND thu_tu=1", ("$room", roomId)));
        Assert.Equal((long)ownerBId, Convert.ToInt64(Scalar("SELECT chu_nha_id FROM toa_nha WHERE id=$building", ("$building", buildingId))));
    }

    [Fact]
    public async Task OwnerDeletesImageFilesReordersAndPublicListingUsesNextPrimary()
    {
        RentalRequestSchema.Initialize(database);
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, _) = GetListingRoomAndBuilding(listingId);
        using var owner = await Login("CHU_NHA");
        var token = await RoomImageToken(owner, roomId);
        var uploads = new List<(int Id, string Original, string Thumbnail)>();
        for (var index = 0; index < 3; index++)
        {
            using var upload = await Upload(owner, roomId, token, $"delete-{index}.jpg", EncodeJpeg(96, 72));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var json = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            uploads.Add((
                json.RootElement.GetProperty("id").GetInt32(),
                json.RootElement.GetProperty("originalPath").GetString()!,
                json.RootElement.GetProperty("thumbnailPath").GetString()!));
        }

        using var deletePrimary = await DeleteImage(owner, roomId, uploads[0].Id, token);
        Assert.Equal(HttpStatusCode.OK, deletePrimary.StatusCode);
        using var deleteJson = JsonDocument.Parse(await deletePrimary.Content.ReadAsStringAsync());
        Assert.Equal(2, deleteJson.RootElement.GetProperty("count").GetInt32());
        Assert.False(File.Exists(StorageFile(uploads[0].Original)));
        Assert.False(File.Exists(StorageFile(uploads[0].Thumbnail)));
        Assert.Equal(new[] { uploads[1].Id, uploads[2].Id }, ReadImageIds(roomId));
        Assert.Equal(1L, Scalar("SELECT thu_tu FROM anh_phong WHERE id=$id", ("$id", uploads[1].Id)));
        Assert.Equal(2L, Scalar("SELECT thu_tu FROM anh_phong WHERE id=$id", ("$id", uploads[2].Id)));

        using var guest = Client();
        var listingIndex = WebUtility.HtmlDecode(await guest.GetStringAsync("/TinDang"));
        Assert.DoesNotContain(uploads[0].Thumbnail, listingIndex);
        Assert.Contains($"src=\"{uploads[1].Thumbnail}\"", listingIndex);
        using var detailResponse = await guest.GetAsync($"/api/tin-dang/{listingId}");
        using var detail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
        Assert.Equal(uploads[1].Original, detail.RootElement.GetProperty("anh")[0].GetProperty("duongDan").GetString());

        using var deleteAgain = await DeleteImage(owner, roomId, uploads[0].Id, token);
        Assert.Equal(HttpStatusCode.OK, deleteAgain.StatusCode);
        Assert.Equal(new[] { uploads[1].Id, uploads[2].Id }, ReadImageIds(roomId));

        using var deleteMiddle = await DeleteImage(owner, roomId, uploads[1].Id, token);
        Assert.Equal(HttpStatusCode.OK, deleteMiddle.StatusCode);
        Assert.Equal(new[] { uploads[2].Id }, ReadImageIds(roomId));
        Assert.Equal(1L, Scalar("SELECT thu_tu FROM anh_phong WHERE id=$id", ("$id", uploads[2].Id)));

        using var deleteLast = await DeleteImage(owner, roomId, uploads[2].Id, token);
        Assert.Equal(HttpStatusCode.OK, deleteLast.StatusCode);
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        var emptyIndex = await guest.GetStringAsync("/TinDang");
        Assert.Contains("room-placeholder.svg", emptyIndex);

        for (var indexUpload = 0; indexUpload < 8; indexUpload++)
        {
            using var upload = await Upload(owner, roomId, token, $"after-delete-{indexUpload}.jpg", EncodeJpeg(64, 48));
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }
        Assert.Equal(8L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        using (var scope = factory.Services.CreateScope())
        {
            var check = await scope.ServiceProvider.GetRequiredService<RoomImageDeletionService>().CheckStorageAsync();
            Assert.Equal(0, check.OrphanFiles);
            Assert.Equal(0, check.MissingFiles);
        }
        CleanupRoomImages(roomId);
    }

    [Fact]
    public async Task OwnerCannotDeleteAnotherOwnersImageAndStorageCheckFindsNoOrphans()
    {
        var listingId = CreatePublicListing("TRONG", "DANG_HIEN_THI", includePhoto: false);
        var (roomId, buildingId) = GetListingRoomAndBuilding(listingId);
        using var ownerA = await Login("CHU_NHA");
        var tokenA = await RoomImageToken(ownerA, roomId);
        using var upload = await Upload(ownerA, roomId, tokenA, "owned.jpg", EncodeJpeg(64, 48));
        using var uploadJson = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var imageId = uploadJson.RootElement.GetProperty("id").GetInt32();

        var ownerBEmail = "delete-owner-b@s202.test";
        var ownerBHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!");
        var now = DateTime.UtcNow.ToString("O");
        Execute("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat) VALUES('Owner B',$email,'0970000002',$hash,'CHU_NHA',1,0,0,$now,$now)",
            ("$email", ownerBEmail), ("$hash", ownerBHash), ("$now", now));
        var ownerBId = Convert.ToInt32(Scalar("SELECT id FROM tai_khoan WHERE email=$email", ("$email", ownerBEmail)));
        Execute("UPDATE toa_nha SET chu_nha_id=$owner WHERE id=$building", ("$owner", ownerBId), ("$building", buildingId));

        using var denied = await DeleteImage(ownerA, roomId, imageId, tokenA);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(1L, Scalar("SELECT COUNT(*) FROM anh_phong WHERE id=$id", ("$id", imageId)));

        using var scope = factory.Services.CreateScope();
        var check = await scope.ServiceProvider.GetRequiredService<RoomImageDeletionService>().CheckStorageAsync();
        Assert.Equal(0, check.OrphanFiles);
        Assert.Equal(0, check.MissingFiles);
        CleanupRoomImages(roomId);
    }

    private static async Task<string> RoomImageToken(HttpClient client, int roomId)
    {
        var html = await client.GetStringAsync($"/PhongTro/Edit/{roomId}");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, int roomId, string token, string name, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", name);
        if (token.Length > 0) content.Add(new StringContent(token), "__RequestVerificationToken");
        return await client.PostAsync($"/PhongTro/UploadImage/{roomId}", content);
    }

    private static async Task<HttpResponseMessage> Reorder(HttpClient client, int roomId, string token, IReadOnlyList<int> imageIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/PhongTro/ReorderImages/{roomId}")
        {
            Content = JsonContent.Create(imageIds)
        };
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> DeleteImage(HttpClient client, int roomId, int imageId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/PhongTro/DeleteImage/{roomId}?imageId={imageId}");
        request.Headers.Add("RequestVerificationToken", token);
        return await client.SendAsync(request);
    }

    private int[] ReadImageIds(int roomId)
    {
        var count = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM anh_phong WHERE phong_id=$room", ("$room", roomId)));
        return Enumerable.Range(1, count)
            .Select(order => Convert.ToInt32(Scalar("SELECT id FROM anh_phong WHERE phong_id=$room AND thu_tu=$order", ("$room", roomId), ("$order", order))))
            .ToArray();
    }

    private string RoomImageDirectory(int roomId) => Path.Combine(temp, "wwwroot", "uploads", "rooms", roomId.ToString());

    private string StorageFile(string publicPath) => Path.Combine(temp, "wwwroot",
        publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private void CleanupRoomImages(int roomId)
    {
        var directory = RoomImageDirectory(roomId);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static void CleanupExternalRoomImages(string root, int roomId)
    {
        var directory = Path.Combine(root, roomId.ToString());
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(Configuration.Default, width, height, new Rgba32(210, 160, 90));
        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder { Quality = 90 });
        return output.ToArray();
    }

    private static byte[] EncodePng(int width, int height)
    {
        using var image = new Image<Rgba32>(Configuration.Default, width, height, new Rgba32(70, 130, 120));
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }
}
