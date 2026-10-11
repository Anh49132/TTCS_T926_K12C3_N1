using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using QL_PhongTro.Data;
using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;
public sealed partial class MeterReadingListTests
{
    private void MeterFixture(bool waterMeter=true,bool handover=true)
    {
        Execute($"""
            INSERT INTO dich_vu_toa_nha(id,toa_nha_id,dich_vu_id) VALUES(1,1,1),(2,1,2);
            INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id) VALUES(1,1),(1,2),(2,1),(2,2);
            INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,nguoi_tao_id,ngay_tao)
            VALUES(1,1,'THEO_CHI_SO','kWh',4000,'2026-08-01',1,'2026-08-01'),
                  (1,2,'{(waterMeter?"THEO_CHI_SO":"THEO_NGUOI")}','m3',18000,'2026-08-01',1,'2026-08-01');
            """);
        if(handover){Handover();Handover(2);}
    }
    private LuuChiSoInput SaveInput(decimal? electric=0,decimal? water=12.345m)=>new(){ToaNhaId=1,PhongId=1,HopDongId=1,Ky=new(2026,10,1),DienMoi=electric,NuocMoi=water,PhienBanPhong=0};
    private async Task<Dictionary<string,string[]>> Save(LuuChiSoInput input,int actor=2)
    {
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False").Options,
            new HttpContextAccessor{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,"QUAN_LY")],"test"))}});
        return await new ChiSoDienNuocService(db,clock,meterProtection).LuuAsync(actor,input);
    }
    private long Count(string table){using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT count(*) FROM "+table;return Convert.ToInt64(cmd.ExecuteScalar());}
    [Theory][InlineData(0,12.345)][InlineData(1,13)]
    public async Task SaveEqualAndGreaterOnlySelectedRoom(double electric,double water)
    {
        MeterFixture();Assert.Empty(await Save(SaveInput((decimal)electric,(decimal)water)));
        using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options);
        var rows=await db.ChiSoDienNuocs.ToListAsync();Assert.Equal(2,rows.Count);
        Assert.All(rows,x=>{Assert.Equal(1,x.HopDongId);Assert.Equal(new DateOnly(2026,10,1),x.TuNgay);Assert.Equal(new DateOnly(2026,10,31),x.DenNgay);Assert.Equal(2,x.NguoiNhapId);Assert.Equal(clock.UtcNow,x.NgayNhap);Assert.False(x.DaKhoa);});
        var vm=await List();Assert.True(vm.Phongs.Single(x=>x.PhongId==1).DaChot);Assert.False(vm.Phongs.Single(x=>x.PhongId==2).DaChot);
        Assert.Equal((decimal)electric,vm.Phongs.Single(x=>x.PhongId==1).DichVu.Single(x=>x.Ma=="DIEN").Moi);
    }
    [Theory][InlineData(-1,13,"DienMoi")][InlineData(1,12,"NuocMoi")][InlineData(-1,12,"both")]
    public async Task InvalidFieldsAtomicAndCorrection(double electric,double water,string field)
    {
        MeterFixture();var errors=await Save(SaveInput((decimal)electric,(decimal)water));
        if(field=="both"){Assert.Contains("DienMoi",errors.Keys);Assert.Contains("NuocMoi",errors.Keys);}else Assert.Contains(field,errors.Keys);
        Assert.Equal(0,Count("chi_so_dien_nuoc"));Assert.Equal(0,Count("nhat_ky_hoat_dong"));
        Assert.Empty(await Save(SaveInput(1,13)));
    }
    [Theory][InlineData(null)][InlineData(-1d)][InlineData(100000000000d)]
    public async Task InvalidNumberRejected(double? value)
    {MeterFixture();Assert.Contains("DienMoi",(await Save(SaveInput(value.HasValue?(decimal)value:null))).Keys);Assert.Equal(0,Count("chi_so_dien_nuoc"));}
    [Fact] public async Task ExcessPrecisionRejected(){MeterFixture();Assert.Contains("DienMoi",(await Save(SaveInput(0.0001m))).Keys);}
    [Fact] public async Task MissingReferenceBlockedWithoutFakeZero()
    {MeterFixture(handover:false);var errors=await Save(SaveInput());Assert.Equal(2,errors.Count);Assert.Equal(0,Count("chi_so_dien_nuoc"));Assert.Null((await List()).Phongs.Single(x=>x.PhongId==1).Dien.GiaTri);}
    [Fact] public async Task MissingReferenceCanBeSuppliedAndSurvivesReload()
    {
        MeterFixture(handover:false);
        var input=SaveInput(110,25); input.DienDau=100; input.NuocDau=20;
        Assert.Empty(await Save(input));
        var room=(await List()).Phongs.Single(x=>x.PhongId==1);
        Assert.Equal(100,room.Dien.GiaTri);Assert.Equal(20,room.Nuoc.GiaTri);
        input.DienPhienBan=0;input.NuocPhienBan=0;input.DienDau=0;input.DienMoi=99;
        Assert.Contains("DienMoi",(await Save(input)).Keys);
    }
    [Theory][InlineData(-1)][InlineData(100.0001)]
    public async Task InvalidSuppliedBaselineDoesNotSave(double baseline)
    {
        MeterFixture(handover:false);var input=SaveInput(110,25);input.DienDau=(decimal)baseline;input.NuocDau=20;
        Assert.Contains("DienDau",(await Save(input)).Keys);Assert.Equal(0,Count("chi_so_dien_nuoc"));
    }
    [Fact] public async Task WaterPerPersonCreatesOnlyElectricReading()
    {MeterFixture(waterMeter:false);Assert.Empty(await Save(SaveInput(2,null)));Assert.Equal(1,Count("chi_so_dien_nuoc"));var room=(await List()).Phongs.Single(x=>x.PhongId==1);Assert.Single(room.DichVu);Assert.True(room.DaChot);}
    [Fact] public async Task RoomOverrideAndServiceStopRespected()
    {
        MeterFixture();Execute("INSERT INTO cau_hinh_dich_vu(toa_nha_id,phong_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,nguoi_tao_id,ngay_tao) VALUES(1,1,1,'THEO_NGUOI','person',10000,'2026-08-01',1,'2026-08-01');");
        Assert.Equal("NUOC",Assert.Single((await List()).Phongs.Single(x=>x.PhongId==1).DichVu).Ma);
        Execute("INSERT INTO ngung_dich_vu_phong(dich_vu_phong_id,yeu_cau_luc_utc,ngung_tu_ky) SELECT id,'2026-09-01','2026-10-01' FROM dich_vu_phong WHERE phong_id=1 AND dich_vu_toa_nha_id=2");
        Assert.Empty((await List()).Phongs.Single(x=>x.PhongId==1).DichVu);
    }
    [Fact] public async Task SavedPriorReadingOverridesLegacyAndHandover()
    {
        MeterFixture();Invoice(1,1,9,electricity:10,water:20);clock.UtcNow=new(2026,9,7,0,0,0,DateTimeKind.Utc);
        var input=SaveInput(30,40);input.Ky=new(2026,9,1);Assert.Empty(await Save(input));clock.UtcNow=new(2026,10,7,0,0,0,DateTimeKind.Utc);
        var row=(await List()).Phongs.Single(x=>x.PhongId==1);Assert.Equal(30,row.Dien.GiaTri);Assert.Equal(40,row.Nuoc.GiaTri);
        Assert.Contains("DienMoi",(await Save(SaveInput(29,40))).Keys);
    }
    [Fact] public async Task ForgedScopeAndStalePeriodDenied()
    {
        MeterFixture();var input=SaveInput();input.ToaNhaId=2;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Save(input));
        input=SaveInput();input.HopDongId=6;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Save(input));
        input=SaveInput();input.PhongId=5;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Save(input));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Save(SaveInput(),3));
        input=SaveInput();input.Ky=new(2026,9,1);Assert.Contains("Phong",(await Save(input)).Keys);Assert.Equal(0,Count("chi_so_dien_nuoc"));
    }
    [Fact] public async Task DuplicateConcurrentSaveAndOptimisticUpdate()
    {
        MeterFixture();var results=await Task.WhenAll(Task.Run(()=>Save(SaveInput())),Task.Run(()=>Save(SaveInput())));Assert.Single(results,x=>x.Count==0);Assert.Equal(2,Count("chi_so_dien_nuoc"));
        var update=SaveInput(5,15);update.DienPhienBan=update.NuocPhienBan=0;Assert.Empty(await Save(update));
        Assert.Contains("DienMoi",(await Save(update)).Keys);
        Execute("UPDATE chi_so_dien_nuoc SET da_khoa=1 WHERE dich_vu_id=1");update.DienPhienBan=update.NuocPhienBan=1;
        Assert.Contains("DienMoi",(await Save(update)).Keys);
    }
    [Fact] public async Task StaleRoomAndAuditFailureRollback()
    {
        MeterFixture();Execute("UPDATE phong_tro SET phien_ban=1 WHERE id=1");Assert.Contains("Phong",(await Save(SaveInput())).Keys);
        Execute("UPDATE phong_tro SET phien_ban=0 WHERE id=1; CREATE TRIGGER fail_meter_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test audit failure'); END;");
        await Assert.ThrowsAnyAsync<Exception>(()=>Save(SaveInput()));Assert.Equal(0,Count("chi_so_dien_nuoc"));
    }

    [Fact]
    public async Task PublishedInvoiceLocksBuildingPeriodWithoutChangingExistingReadings()
    {
        MeterFixture();
        Assert.Empty(await Save(SaveInput(10, 20)));
        Invoice(1, 1, 10, issued: true);

        var locked = await List();
        Assert.True(locked.KyDaKhoa);
        var errors = await Save(SaveInput(30, 40));
        Assert.Contains(errors.Values.SelectMany(x => x), message => message.Contains("đã khóa"));

        using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False").Options))
        {
            var saved = await db.ChiSoDienNuocs.OrderBy(x => x.DichVuId).ToListAsync();
            Assert.Equal(2, saved.Count);
            Assert.Equal(new[] { 10m, 20m }, saved.Select(x => x.ChiSoCuoi));
        }

        var anotherRoom = SaveInput(1, 13);
        anotherRoom.PhongId = 2;
        anotherRoom.HopDongId = 2;
        Assert.Contains((await Save(anotherRoom)).Values.SelectMany(x => x),
            message => message.Contains("đã khóa"));

        clock.UtcNow = new(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        Assert.False((await List()).KyDaKhoa);
        var november = SaveInput(11, 21);
        november.Ky = new(2026, 11, 1);
        Assert.Empty(await Save(november));
    }
}
