using System.Net;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Services;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    private async Task<int> CheckoutRequestFixture()
    {
        using var db = Context(); var f = await BillingAsync(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE khach_thue SET tai_khoan_id=3 WHERE id=1"); return f.A;
    }
    [Fact]
    public async Task CheckoutRequestSubmitAcceptRejectResubmitNeverChangesFinancialOrContractState()
    {
        var contract = await CheckoutRequestFixture(); using var db = Context(); var service = new CheckoutRequestService(db, CollectionClock());
        const string privateReason = "Lý do trao đổi riêng tư";
        var id = await service.SubmitAsync(3, contract, new(2026,10,20), privateReason);
        Assert.Equal("DANG_HIEU_LUC", (await db.HopDongs.AsNoTracking().SingleAsync(x=>x.Id==contract)).TrangThai);
        Assert.Empty(await db.HoaDons.ToListAsync()); Assert.Empty(await db.GiaoDichCocs.ToListAsync());
        Assert.Equal(1, (await db.ThongBaoTraPhongs.SingleAsync()).NguoiNhanId);
        var page = await service.PageAsync(3, contract); Assert.Single(page.Rows); Assert.NotEmpty(page.Contracts);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SubmitAsync(3,contract,new(2026,10,21),"duplicate"));
        await service.ChangeAsync(1,id,0,"DA_TIEP_NHAN",new(2026,10,22),"Đã trao đổi với khách");
        Assert.Equal(new DateOnly(2026,10,22),(await db.YeuCauTraPhongs.AsNoTracking().SingleAsync()).NgayBanGiao);
        Assert.Equal("DANG_HIEU_LUC",(await db.HopDongs.AsNoTracking().SingleAsync(x=>x.Id==contract)).TrangThai);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ChangeAsync(3,id,1,"DA_HUY",null,null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ChangeAsync(1,id,0,"TU_CHOI",null,"stale"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ChangeAsync(1,id,1,"TU_CHOI",null," "));
        await service.ChangeAsync(1,id,1,"TU_CHOI",null,"Hai bên cần thống nhất lại lịch");
        var second = await service.SubmitAsync(3,contract,new(2026,10,23),"Gửi ngày mới"); Assert.NotEqual(id,second);
        await service.ChangeAsync(3,second,0,"DA_HUY",null,null);
        Assert.Equal(2,await db.YeuCauTraPhongs.CountAsync()); Assert.Empty(await db.HoaDons.ToListAsync()); Assert.Empty(await db.GiaoDichCocs.ToListAsync());
        var audit=await db.NhatKyHoatDongs.Where(x=>x.LoaiDoiTuong=="YEU_CAU_TRA_PHONG").ToListAsync();
        Assert.Equal(5,audit.Count); Assert.All(audit,x=>Assert.DoesNotContain(privateReason,x.DuLieuSau??""));
    }
    [Fact]
    public async Task CheckoutRequestRejectsWrongActorInvalidDatesDuplicateAndStaleState()
    {
        var contract=await CheckoutRequestFixture(); using var db=Context(); var s=new CheckoutRequestService(db,CollectionClock());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.SubmitAsync(1,contract,new(2026,10,20),"wrong role"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.PageAsync(2,contract));
        foreach(var day in new DateOnly?[]{null,new(2026,10,12),new(2028,12,31),new(2029,1,1)})
            await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SubmitAsync(3,contract,day,"reason"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SubmitAsync(3,contract,new(2026,10,20)," "));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SubmitAsync(3,contract,new(2026,10,20),new string('x',501)));
        var id=await s.SubmitAsync(3,contract,new(2026,10,20),"valid");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>s.ChangeAsync(2,id,0,"DA_TIEP_NHAN",new(2026,10,20),null));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.ChangeAsync(1,id,0,"DA_TIEP_NHAN",new(2026,10,12),null));
        await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET trang_thai='DA_KET_THUC'"); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.ChangeAsync(1,id,0,"DA_TIEP_NHAN",new(2026,10,20),null));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CheckoutRequestNoticeFailureRollsBackRequestStateAndAudit(bool decision)
    {
        var contract=await CheckoutRequestFixture(); using var db=Context();var s=new CheckoutRequestService(db,CollectionClock());
        var id=decision?await s.SubmitAsync(3,contract,new(2026,10,20),"valid"):0;
        var audit=await db.NhatKyHoatDongs.CountAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_checkout_notice BEFORE INSERT ON thong_bao_tra_phong BEGIN SELECT RAISE(ABORT,'test rollback'); END;");
        await Assert.ThrowsAnyAsync<Exception>(async()=>{if(decision)await s.ChangeAsync(1,id,0,"DA_TIEP_NHAN",new(2026,10,20),null);else await s.SubmitAsync(3,contract,new(2026,10,20),"valid");});
        db.ChangeTracker.Clear(); Assert.Equal(decision?1:0,await db.YeuCauTraPhongs.CountAsync());Assert.Equal(audit,await db.NhatKyHoatDongs.CountAsync());
        if(decision){var r=await db.YeuCauTraPhongs.SingleAsync();Assert.Equal("MOI",r.TrangThai);Assert.Equal(0,r.PhienBan);Assert.Null(r.NgayBanGiao);}
    }
    [Fact]
    public async Task CheckoutRequestHttpChecksRolesCsrfOwnershipAndRecipientNotices()
    {
        var contract=await CheckoutRequestFixture();using var web=BillingWeb(CollectionClock());
        using var tenant=web.CreateClient(new(){AllowAutoRedirect=false}); await Login(tenant,"tenant");
        Assert.Equal(HttpStatusCode.OK,(await tenant.GetAsync("/YeuCauTraPhong?hopDongId="+contract)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await tenant.PostAsync("/YeuCauTraPhong/Gui",new FormUrlEncodedContent(new Dictionary<string,string>{{"hopDongId",contract.ToString()}}))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(tenant,"/YeuCauTraPhong/Gui",new(){["hopDongId"]=contract.ToString(),["ngayDeNghi"]="2026-10-20",["lyDo"]="Xin chuyển đi"})).StatusCode);
        using var db=Context();var r=await db.YeuCauTraPhongs.SingleAsync();var notice=await db.ThongBaoTraPhongs.SingleAsync();
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(tenant,"/YeuCauTraPhong/XuLy",new(){["id"]=r.Id.ToString(),["phienBan"]="0",["thaoTac"]="DA_TIEP_NHAN"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(tenant,$"/api/thong-bao/tra-phong/{notice.Id}/da-doc",new())).StatusCode);
        using var owner=web.CreateClient(new(){AllowAutoRedirect=false});await Login(owner,"owner");
        Assert.Contains("CHECKOUT",await owner.GetStringAsync("/api/thong-bao/danh-sach"));
        Assert.Equal(HttpStatusCode.OK,(await Post(owner,$"/api/thong-bao/tra-phong/{notice.Id}/da-doc",new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(owner,"/YeuCauTraPhong/XuLy",new(){["id"]=r.Id.ToString(),["phienBan"]="0",["thaoTac"]="DA_TIEP_NHAN",["ngayBanGiao"]="2026-10-22"})).StatusCode);
        Assert.Contains("CHECKOUT",await tenant.GetStringAsync("/api/thong-bao/danh-sach"));
        using var other=web.CreateClient(new(){AllowAutoRedirect=false});await Login(other,"other");
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/YeuCauTraPhong?hopDongId="+contract)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(other,"/YeuCauTraPhong/XuLy",new(){["id"]=r.Id.ToString(),["phienBan"]="1",["thaoTac"]="TU_CHOI",["phanHoi"]="wrong owner"})).StatusCode);
    }

    [Fact]
    public async Task CheckoutRequestConcurrentSubmissionCreatesExactlyOneOpenRequest()
    {
        var contract=await CheckoutRequestFixture();
        async Task<bool> Send()
        {
            using var db=Context();
            try {await new CheckoutRequestService(db,CollectionClock()).SubmitAsync(3,contract,new(2026,10,20),"Concurrent request");return true;}
            catch(Exception ex) when(ex is InvalidOperationException or DbUpdateException or Microsoft.Data.Sqlite.SqliteException){return false;}
        }
        var results=await Task.WhenAll(Task.Run(Send),Task.Run(Send));Assert.Single(results,x=>x);
        using var verify=Context();Assert.Single(await verify.YeuCauTraPhongs.ToListAsync());Assert.Single(await verify.ThongBaoTraPhongs.ToListAsync());
    }

    [Fact]
    public async Task CheckoutRequestAuditFailureRollsBackAndSubmittedContentIsImmutable()
    {
        var contract=await CheckoutRequestFixture();using var db=Context();var s=new CheckoutRequestService(db,CollectionClock());
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_checkout_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='YEU_CAU_TRA_PHONG' BEGIN SELECT RAISE(ABORT,'test audit'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>s.SubmitAsync(3,contract,new(2026,10,20),"valid"));db.ChangeTracker.Clear();
        Assert.Empty(await db.YeuCauTraPhongs.ToListAsync());Assert.Empty(await db.ThongBaoTraPhongs.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_checkout_audit");
        var id=await s.SubmitAsync(3,contract,new(2026,10,20),"valid");
        await Assert.ThrowsAnyAsync<Exception>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE yeu_cau_tra_phong SET ly_do='changed' WHERE id={id}"));
        await Assert.ThrowsAnyAsync<Exception>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM yeu_cau_tra_phong WHERE id={id}"));
    }
}
