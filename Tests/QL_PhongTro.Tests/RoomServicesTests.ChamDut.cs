using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;

public sealed partial class RoomServicesTests
{
    [Theory]
    [InlineData(12,true)][InlineData(13,false)][InlineData(14,false)]
    public void ChamDutClassifiesByStrictlyEarlierDay(int day,bool early)
    {
        var result=ChamDutHopDongPolicy.KiemTra(new() {LyDo="THOA_THUAN"},new(2026,10,day),new(2026,10,13),7000000);
        Assert.Equal(early,result.TruocHan);Assert.Equal(0,result.TienPhat);
        if(!early)Assert.Null(result.LyDo);
    }
    [Theory]
    [InlineData(null)][InlineData("")][InlineData("gia_mao")][InlineData("KHAC")]
    public void ChamDutRequiresKnownReasonAndOtherText(string? reason)
        =>Assert.Throws<InvalidOperationException>(()=>ChamDutHopDongPolicy.KiemTra(new() {LyDo=reason},new(2026,10,13),new(2027,10,7),7000000));
    [Theory]
    [InlineData(null)][InlineData("0")][InlineData("-1")][InlineData("1.5")][InlineData("7000001")]
    public void ChamDutRejectsInvalidPenalty(string? amount)
    {
        decimal? money=amount is null?null:decimal.Parse(amount,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<InvalidOperationException>(()=>ChamDutHopDongPolicy.KiemTra(new() {LyDo="THOA_THUAN",ApDungPhat=true,TienPhat=money},new(2026,10,13),new(2027,10,7),7000000));
    }
    [Fact]
    public void ChamDutAllowsCapNormalizesNoPenaltyAndRejectsOnTimePenalty()
    {
        var input=new ChamDutInput {LyDo="KHAC",LyDoKhac="  Chuyển công tác  ",ApDungPhat=true,TienPhat=7000000};
        var result=ChamDutHopDongPolicy.KiemTra(input,new(2026,10,13),new(2027,10,7),7000000);
        Assert.Equal(7000000,result.TienPhat);Assert.Equal("Chuyển công tác",result.LyDoKhac);
        input.ApDungPhat=false;Assert.Equal(0,ChamDutHopDongPolicy.KiemTra(input,new(2026,10,13),new(2027,10,7),7000000).TienPhat);
        input.ApDungPhat=true;
        Assert.Throws<InvalidOperationException>(()=>ChamDutHopDongPolicy.KiemTra(input,new(2027,10,7),new(2027,10,7),7000000));
        Assert.Throws<InvalidOperationException>(()=>ChamDutHopDongPolicy.KiemTra(input,new(2026,10,13),new(2027,10,7),0));
    }
    [Theory]
    [InlineData(7000000,500000,3050000,0)]
    [InlineData(7000000,3500000,50000,0)]
    [InlineData(7000000,4000000,0,450000)]
    [InlineData(3450000,0,0,0)]
    [InlineData(0,3500000,0,6950000)]
    public void ChamDutPenaltyIsSubtractedOnce(long deposit,long fine,long refund,long extra)
    {
        // Base debt 3m + damage450k; penalty is a separate deduction.
        var bang=new BangTatToan {CocConGiu=deposit,NoHoaDonCuoi=2000000,CongNoCu=1000000,TienPhat=fine};
        var input=new TatToanInput {KhauTru=[new() {TenKhoan="Khóa",SoTien=450000}]};
        TatToanService.ApDungKhauTru(bang,input);TatToanService.ApDungKhauTru(bang,input);
        Assert.Equal(refund,bang.TienPhaiHoan);Assert.Equal(extra,bang.TienKhachTraThem);
        Assert.Equal(fine>0?1:0,bang.KhauTru.Count(x=>x.LoaiKhoan=="PHAT_COC"));
        Assert.Equal(450000,bang.TienHuHong);Assert.Equal(450000+fine,bang.TongKhauTru);
    }
    [Fact]
    public async Task ChamDutPersistsExactlyOnePenaltySharesReasonAndToggleRemovesIt()
    {
        using var db=Context();var f=await TatToanFixture(db,7000000);
        await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET tien_coc_thoa_thuan=7000000");db.ChangeTracker.Clear();
        var service=new TatToanService(db,TatToanClock());var input=TatToanFields(await service.NhapAsync(1,f.Contract));
        input.LyDo="KHAC";input.LyDoKhac="Điều chuyển công việc";input.ApDungPhat=true;input.TienPhat=3500000;
        await service.LuuAsync(1,input);db.ChangeTracker.Clear();
        var owner=await service.XemAsync(1,f.Contract);var tenant=await service.XemAsync(3,f.Contract);
        Assert.Equal(50000,owner.Bang.TienPhaiHoan);Assert.Equal(owner.KetQua,tenant.KetQua);
        Assert.Equal("Điều chuyển công việc",tenant.ChamDut!.LyDoKhac);Assert.Equal(3500000,tenant.ChamDut.TienPhat);
        Assert.Single(tenant.Bang.KhauTru,x=>x.LoaiKhoan=="PHAT_COC");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));
        var edit=await service.NhapAsync(1,f.Contract);Assert.Equal(2,edit.Input.KhauTru.Count);
        edit.Input.TienPhat=4000000;await service.LuuAsync(1,edit.Input);db.ChangeTracker.Clear();
        tenant=await service.XemAsync(3,f.Contract);Assert.Equal(450000,tenant.Bang.TienKhachTraThem);
        Assert.Single(tenant.Bang.KhauTru,x=>x.LoaiKhoan=="PHAT_COC");
        edit=await service.NhapAsync(1,f.Contract);edit.Input.ApDungPhat=false;await service.LuuAsync(1,edit.Input);db.ChangeTracker.Clear();
        tenant=await service.XemAsync(3,f.Contract);Assert.Equal(3550000,tenant.Bang.TienPhaiHoan);Assert.Equal(0,tenant.Bang.TienPhat);
        Assert.DoesNotContain(tenant.Bang.KhauTru,x=>x.LoaiKhoan=="PHAT_COC");Assert.False(tenant.ChamDut!.ApDungPhat);
        Assert.Equal("Điều chuyển công việc",tenant.ChamDut.LyDoKhac);
        Assert.DoesNotContain("Điều chuyển công việc",string.Join(" ",await db.NhatKyHoatDongs.Select(x=>x.DuLieuSau).ToListAsync()));
        Assert.Equal(2000000,(await db.HoaDons.SingleAsync(x=>x.Id==f.Final)).TongTien);
        Assert.Single(await db.GiaoDichCocs.ToListAsync()); // Draft does not book offsets or add invoice charges yet.
    }
    [Fact]
    public async Task ChamDutUsesLatestRenewalAndDetectsChangedContractCap()
    {
        using var db=Context();var f=await TatToanFixture(db);
        db.KyHopDongs.Add(new() {HopDongId=f.Contract,SoThuTu=2,NgayBatDau=new(2029,1,1),NgayKetThuc=new(2029,12,31),SoThang=12,GiaThue=1000000,NguoiLapId=1,NgayTao=TatToanClock().UtcNow});
        await db.SaveChangesAsync();
        var terms=await db.KyHopDongs.Where(x=>x.HopDongId==f.Contract).ToListAsync();
        var service=new TatToanService(db,TatToanClock());var initial=await service.NhapAsync(1,f.Contract);
        Assert.Equal(terms.Max(x=>x.NgayKetThuc),initial.ChamDut!.NgayKetThuc);
        var input=TatToanFields(initial);
        await db.Database.ExecuteSqlRawAsync("UPDATE hop_dong SET tien_coc_thoa_thuan=7000000");db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));
        var fresh=await service.NhapAsync(1,f.Contract);fresh.Input.LyDo="THOA_THUAN";fresh.Input.ApDungPhat=true;fresh.Input.TienPhat=7000000;
        await service.LuuAsync(1,fresh.Input);Assert.Equal(7000000,(await service.XemAsync(3,f.Contract)).Bang.TienPhat);
    }
    [Fact]
    public async Task ChamDutOnTimeSettlementNeedsNoReasonAndCannotApplyPenalty()
    {
        using var db=Context();var f=await TatToanFixture(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ky_hop_dong SET ngay_ket_thuc='2026-10-13' WHERE hop_dong_id={f.Contract}");db.ChangeTracker.Clear();
        var service=new TatToanService(db,TatToanClock());var form=await service.NhapAsync(1,f.Contract);
        Assert.False(form.ChamDut!.TruocHan);await service.LuuAsync(1,form.Input);db.ChangeTracker.Clear();
        var saved=await service.XemAsync(3,f.Contract);Assert.False(saved.ChamDut!.TruocHan);Assert.Null(saved.ChamDut.LyDo);Assert.Equal(0,saved.Bang.TienPhat);
        form=await service.NhapAsync(1,f.Contract);form.Input.ApDungPhat=true;form.Input.TienPhat=1;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,form.Input));
    }
    [Fact]
    public async Task ChamDutCheckoutPersistsPenaltyAndAuditFailureRollsBackWholeInvoice()
    {
        using var db=Context();var f=await MonthlyFixture(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE chi_so_dien_nuoc SET tu_ngay='2026-09-01',den_ngay='2026-09-30'; UPDATE hop_dong SET tien_coc_thoa_thuan=7000000");db.ChangeTracker.Clear();
        var service=new TraPhongService(db,TatToanClock());var input=new TraPhongInput {HopDongId=f.A,NgayTra=new(2026,10,13),ChiSoDien=224,ChiSoNuoc=30};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.NhapAsync(1,input,true));
        input.LyDo="VI_PHAM";input.ApDungPhat=true;input.TienPhat=3500000;
        input.DauVanTay=TraPhongService.DauVanTay(await service.NhapAsync(1,input,true));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_termination_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='cham_dut_hop_dong' BEGIN SELECT RAISE(ABORT,'synthetic termination audit failure'); END");
        await Assert.ThrowsAnyAsync<Exception>(()=>service.LuuAsync(1,input));db.ChangeTracker.Clear();
        Assert.False(await db.HoaDons.AnyAsync(x=>x.HopDongId==f.A && x.LoaiHoaDon=="KY_CUOI"));Assert.Empty(await db.ChamDutHopDongs.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_termination_audit");
        // Audit failure poisons that context deliberately; a new request/context can retry.
        using var retry=Context();var handover=new TraPhongService(retry,TatToanClock());await handover.LuuAsync(1,input);
        var info=await retry.ChamDutHopDongs.AsNoTracking().SingleAsync();Assert.Equal(3500000,info.TienPhat);Assert.Equal("VI_PHAM",info.LyDo);
        var draft=await new TatToanService(retry,TatToanClock()).NhapAsync(1,f.A);
        Assert.True(draft.Input.ApDungPhat);Assert.Equal(3500000,draft.Bang.TienPhat);Assert.Single(draft.Bang.KhauTru,x=>x.LoaiKhoan=="PHAT_COC");
    }
    [Fact]
    public async Task ChamDutHttpMissingReasonAndBadPenaltyFailAndTenantCannotPost()
    {
        using var db=Context();var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());
        var input=TatToanFields(await service.NhapAsync(1,f.Contract));input.LyDo=null;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.LuuAsync(1,input));Assert.Empty(await db.BangTatToans.ToListAsync());
        using var factory=BillingWeb(TatToanClock());using var client=factory.CreateClient(new() {AllowAutoRedirect=false});await Login(client,"owner");
        var html=await client.GetStringAsync($"/TatToan/Nhap/{f.Contract}");
        string Field(string name)=>WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(html,"name=\""+System.Text.RegularExpressions.Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var form=new Dictionary<string,string> { ["__RequestVerificationToken"]=Field("__RequestVerificationToken"),["Input.HopDongId"]=f.Contract.ToString(),["Input.DauVanTayNguon"]=Field("Input.DauVanTayNguon") };
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/TatToan/UocTinh",new FormUrlEncodedContent(form))).StatusCode);
        form["Input.LyDo"]="THOA_THUAN";form["Input.ApDungPhat"]="true";form["Input.TienPhat"]="1.5";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/TatToan/UocTinh",new FormUrlEncodedContent(form))).StatusCode);
        form["Input.ApDungPhat"]="false";Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/TatToan/Luu",new FormUrlEncodedContent(form))).StatusCode);
        await Login(client,"tenant");var tenant=await client.GetStringAsync($"/TatToan/ChiTiet/{f.Contract}");Assert.Contains("Hai bên thỏa thuận",WebUtility.HtmlDecode(tenant));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync("/TatToan/Luu",new FormUrlEncodedContent(form))).StatusCode);
    }

    [Fact]
    public async Task ChamDutV32UpgradeCopyPreservesV31SettlementsAndOldRows()
    {
        using(var db=Context())
        {
            var f=await TatToanFixture(db);var service=new TatToanService(db,TatToanClock());
            await service.LuuAsync(1,TatToanFields(await service.NhapAsync(1,f.Contract)));
        }
        using(var source=Open())
        {
            using var command=source.CreateCommand();
            command.CommandText="DROP TABLE cham_dut_hop_dong; DROP INDEX ux_khau_tru_phat_coc; ALTER TABLE thanh_ly_hop_dong DROP COLUMN tien_phat; ALTER TABLE chi_tiet_khau_tru DROP COLUMN loai_khoan; DELETE FROM app_schema_version WHERE version=32";
            command.ExecuteNonQuery();
        }
        var target=Path.Combine(folder,"v32-copy.sqlite");
        using(var source=Open())using(var copy=new SqliteConnection($"Data Source={target};Pooling=False")){copy.Open();source.BackupDatabase(copy);}
        var columns=new Dictionary<string,List<string>>();
        using(var source=Open())
        {
            using var cmd=source.CreateCommand();cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name<>'app_schema_version' ORDER BY name";
            var tables=new List<string>();using(var reader=cmd.ExecuteReader())while(reader.Read())tables.Add(reader.GetString(0));
            foreach(var table in tables){cmd.CommandText=$"SELECT name FROM pragma_table_info('{table}') ORDER BY cid";using var reader=cmd.ExecuteReader();var names=new List<string>();while(reader.Read())names.Add(reader.GetString(0));columns[table]=names;}
        }
        string Snapshot(string file)
        {
            using var c=new SqliteConnection($"Data Source={file};Pooling=False");c.Open();var text=new System.Text.StringBuilder();
            foreach(var table in columns)
            {
                using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT {string.Join(',',table.Value.Select(x=>'"'+x+'"'))} FROM \"{table.Key}\" ORDER BY rowid";
                using var reader=cmd.ExecuteReader();while(reader.Read()){var row=new object[reader.FieldCount];reader.GetValues(row);text.Append(JsonSerializer.Serialize(row));}
            }
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
        }
        var before=Snapshot(path);DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));
        Assert.Equal(before,Snapshot(path));Assert.Equal(before,Snapshot(target));
        DatabaseUpdates.Update(target,Path.Combine(app,"Data","permissions.seed.json"));DatabaseUpdates.Check(target);
        Assert.Single(Directory.GetFiles(folder,"v32-copy.sqlite.before-update-*.bak"));Assert.Equal(before,Snapshot(target));
        using var verify=new SqliteConnection($"Data Source={target};Pooling=False");verify.Open();using var check=verify.CreateCommand();
        check.CommandText="PRAGMA integrity_check";Assert.Equal("ok",check.ExecuteScalar());
        check.CommandText="PRAGMA foreign_key_check";using(var reader=check.ExecuteReader())Assert.False(reader.Read());
        check.CommandText="SELECT COUNT(*) FROM cham_dut_hop_dong";Assert.Equal(0L,check.ExecuteScalar());
        check.CommandText="SELECT tien_phat FROM thanh_ly_hop_dong";Assert.Equal(0L,check.ExecuteScalar());
        check.CommandText="SELECT COUNT(*) FROM chi_tiet_khau_tru WHERE loai_khoan='HU_HONG'";Assert.Equal(2L,check.ExecuteScalar());
    }
}
