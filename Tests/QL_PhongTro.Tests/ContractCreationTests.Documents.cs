using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed partial class ContractCreationTests
{
    private static IFormFile Upload(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "TaiLieus", name);

    [Fact]
    public async Task ContractTermsAndPrivateDocumentsRoundTripAndRejectOtherOwner()
    {
        using var db = Context();
        var vm = Valid();
        vm.DieuKhoanBoSung = "Không hút thuốc trong phòng.\nThông báo trước khi trả phòng.";
        vm.TaiLieus = [Upload("../hop-dong.pdf", "%PDF-1.7\nDemo"u8.ToArray())];
        var result = Assert.IsType<RedirectToActionResult>(await Controller(db).Create(vm, default));
        Assert.Equal("Details", result.ActionName);
        var id = (await db.HopDongs.SingleAsync()).Id;
        var store = new ContractDocumentStore(path);
        try
        {
            var saved = await store.ReadAsync(id);
            Assert.Equal(vm.DieuKhoanBoSung, saved.Terms);
            var file = Assert.Single(saved.Files);
            Assert.Equal("hop-dong.pdf", file.Name);
            Assert.IsType<PhysicalFileResult>(await Controller(db).TaiLieu(id, file.Id, default));
            Assert.IsType<ForbidResult>(await Controller(db, 2).TaiLieu(id, file.Id, default));
            Assert.IsType<NotFoundResult>(await Controller(db).TaiLieu(id, "../../secret", default));
        }
        finally { store.Delete(id); }
    }

    [Theory]
    [InlineData("fake.pdf")]
    [InlineData("program.exe")]
    public async Task InvalidFilePreventsContractAndRoomChanges(string name)
    {
        using var db = Context();
        var vm = Valid();
        vm.TaiLieus = [Upload(name, "invalid content"u8.ToArray())];
        Assert.IsType<ViewResult>(await Controller(db).Create(vm, default));
        Assert.Empty(await db.HopDongs.ToListAsync());
        Assert.Equal("TRONG", (await db.PhongTros.SingleAsync()).TrangThai);
    }

    [Fact]
    public async Task RejectOversizeAndTooManyAttachments()
    {
        var oversized = new FormFile(new MemoryStream(), 0, 10 * 1024 * 1024 + 1, "TaiLieus", "big.pdf");
        Assert.NotEmpty(await ContractDocumentStore.ValidateAsync([oversized]));
        Assert.NotEmpty(await ContractDocumentStore.ValidateAsync(Enumerable.Range(0, 6)
            .Select(_ => Upload("x.pdf", "%PDF-1.7"u8.ToArray())).ToList()));
    }

    [Fact]
    public async Task StorageFailureRollsBackContractRoomAndAudit()
    {
        var store = new ContractDocumentStore(path);
        await store.WriteAsync(1, "Existing immutable snapshot", []);
        try
        {
            using var db = Context();
            var auditBefore = await db.NhatKyHoatDongs.CountAsync();
            Assert.IsType<ViewResult>(await Controller(db).Create(Valid(), default));
            Assert.Empty(await db.HopDongs.ToListAsync());
            Assert.Equal("TRONG", (await db.PhongTros.SingleAsync()).TrangThai);
            Assert.Equal(auditBefore, await db.NhatKyHoatDongs.CountAsync());
            Assert.Equal("Existing immutable snapshot", (await store.ReadAsync(1)).Terms);
        }
        finally { store.Delete(1); }
    }

    [Fact]
    public async Task FirstInvoiceOptionRequiresFinancePermission()
    {
        using var db = Context();
        var vm = Valid();
        vm.TaoHoaDonKyDau = true;
        var controller = Controller(db);
        Assert.IsType<ViewResult>(await controller.Create(vm, default));
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await db.HopDongs.ToListAsync());
    }
    [Fact]
    public async Task DraftResaveKeepsAttachmentsAndUpdatesTermsWhileActiveContractCannotBeResaved()
    {
        using var db = Context();
        var input = Valid(); input.Intent = "NHAP";
        input.DieuKhoanBoSung = "Điều khoản ban đầu";
        input.TaiLieus = [Upload("contract.pdf", "%PDF-1.7\nDraft"u8.ToArray())];
        Assert.IsType<RedirectToActionResult>(await Controller(db).Create(input, default));
        var id = (await db.HopDongs.SingleAsync()).Id;
        var store = new ContractDocumentStore(path);
        try
        {
            var first = await store.ReadAsync(id);
            var file = Assert.Single(first.Files);
            var bytes = await File.ReadAllBytesAsync(store.FilePath(id, file.Id));
            input.TaiLieus = []; input.DieuKhoanBoSung = "Điều khoản nháp đã sửa";
            Assert.IsType<RedirectToActionResult>(await Controller(db).Create(input, default));
            var next = await store.ReadAsync(id);
            Assert.Equal(input.DieuKhoanBoSung, next.Terms);
            Assert.Equal(file, Assert.Single(next.Files));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(store.FilePath(id, file.Id)));
            input.Intent = "HIEU_LUC";
            Assert.IsType<RedirectToActionResult>(await Controller(db).Create(input, default));
            Assert.IsType<ViewResult>(await Controller(db).Create(input, default));
            Assert.Equal(next, (await store.ReadAsync(id)) with { Files = next.Files });
        }
        finally { store.Delete(id); }
    }

    [Fact]
    public async Task UncommittedDraftDocumentWriteRestoresPreviousManifestAndFileBytes()
    {
        var store = new ContractDocumentStore(path);
        await store.WriteAsync(1, "Old draft", [Upload("old.pdf", "%PDF-1.7\nOld"u8.ToArray())]);
        try
        {
            var before = await store.ReadAsync(1);
            var file = Assert.Single(before.Files);
            using (await store.PrepareWriteAsync(1, "Uncommitted", [], true))
                Assert.Equal("Uncommitted", (await store.ReadAsync(1)).Terms);
            var after = await store.ReadAsync(1);
            Assert.Equal(before.Terms, after.Terms);
            Assert.Equal(file, Assert.Single(after.Files));
            Assert.Equal("%PDF-1.7\nOld"u8.ToArray(), await File.ReadAllBytesAsync(store.FilePath(1, file.Id)));
            await Assert.ThrowsAsync<IOException>(() => store.PrepareWriteAsync(1, "Forbidden", [], false));
            Assert.Equal(before.Terms, (await store.ReadAsync(1)).Terms);
        }
        finally { store.Delete(1); }
    }

}
