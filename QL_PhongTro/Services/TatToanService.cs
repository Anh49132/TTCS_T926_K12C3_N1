using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class TatToanService(AppDbContext db, ITimeProvider clock)
{
    public async Task<bool> SanSangAsync() => await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('giao_dich_coc','thanh_ly_hop_dong','chi_tiet_khau_tru','hoa_don','thanh_toan','cham_dut_hop_dong','xac_nhan_tat_toan','lich_su_trang_thai_phong')").SingleAsync() == 8;

    public async Task<bool> KiemTraQuyenAsync(int actor, int contract, bool sua)
    {
        var account = await db.TaiKhoans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor && x.DangHoatDong && !x.IsDeleted)
            ?? throw new UnauthorizedAccessException();
        var rights=await db.RolePermissions.AsNoTracking().Where(x=>x.RoleCode==account.VaiTro && (x.ModuleCode=="HOP_DONG" || x.ModuleCode=="TAI_CHINH")).ToListAsync();
        if (account.VaiTro!="ADMIN" && (rights.Count != 2 || rights.Any(x => sua
            ? x.AccessLevel is not ("WRITE" or "FULL")
            : x.AccessLevel is not ("READ" or "WRITE" or "FULL"))))
            throw new UnauthorizedAccessException();
        var context = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id where h.Id == contract
            select new { Contract = h, b.ChuNhaId }).SingleOrDefaultAsync() ?? throw new UnauthorizedAccessException();
        var owner = account.VaiTro == "CHU_NHA" && context.ChuNhaId == actor;
        if (owner) return true;
        if(!sua && account.VaiTro=="ADMIN")return false;
        if (sua || account.VaiTro != "KHACH_THUE" || context.Contract.TrangThai is "NHAP" or "DA_HUY") throw new UnauthorizedAccessException();
        var profile = await db.KhachThues.AsNoTracking().Where(x => x.TaiKhoanId == actor).Select(x => (int?)x.Id).SingleOrDefaultAsync();
        if (!profile.HasValue || (context.Contract.KhachDungTenId != profile && !await db.NguoiOGheps.AnyAsync(x => x.HopDongId == contract && x.KhachThueId == profile)))
            throw new UnauthorizedAccessException();
        return false;
    }

    private async Task<TatToanViewModel> ThongTinAsync(int actor, int contract, bool sua)
    {
        var owner = await KiemTraQuyenAsync(actor, contract, sua);
        var model = await (from h in db.HopDongs.AsNoTracking() join p in db.PhongTros on h.PhongId equals p.Id
            join b in db.ToaNhas on p.ToaNhaId equals b.Id where h.Id == contract
            select new TatToanViewModel { MaHopDong = h.MaHopDong, Phong = p.MaPhong, PhongId=p.Id, TrangThaiPhong=p.TrangThai, ToaNha = b.TenToaNha,TrangThaiHopDong=h.TrangThai }).SingleAsync();
        var tenant = await db.HopDongs.Where(x => x.Id == contract).Select(x => x.KhachDungTenId).SingleAsync();
        model.Khach = await db.KhachThues.Where(x => x.Id == tenant).Select(x => x.HoTen).SingleOrDefaultAsync() ?? "";
        model.DuocSua = owner && await db.RolePermissions.CountAsync(x=>x.RoleCode=="CHU_NHA" && (x.ModuleCode=="HOP_DONG" || x.ModuleCode=="TAI_CHINH") && (x.AccessLevel=="WRITE" || x.AccessLevel=="FULL"))==2
            && await db.HopDongs.AnyAsync(x=>x.Id==contract && x.TrangThai=="DANG_HIEU_LUC");
        model.Input.HopDongId = contract;
        return model;
    }

    public async Task<TatToanViewModel> XemAsync(int actor, int contract)
    {
        await using var read=await db.Database.BeginProtectedTransactionAsync();
        var model = await ThongTinAsync(actor, contract, false);
        model.Bang = await db.BangTatToans.AsNoTracking().Include(x => x.KhauTru).SingleOrDefaultAsync(x => x.HopDongId == contract)
            ?? throw new InvalidOperationException("Chủ nhà chưa lưu bảng tất toán cho hợp đồng này.");
        model.ChamDut=await db.ChamDutHopDongs.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==contract);
        model.XacNhan=await db.XacNhanTatToans.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==contract);
        model.CongNo = JsonSerializer.Deserialize<List<CongNoTatToan>>(model.Bang.ChiTietChot) ?? [];
        model.DuocSua &= model.Bang.TrangThai == "NHAP";
        if(model.XacNhan is not null)
        {
            var ledger=await db.GiaoDichCocs.AsNoTracking().Where(x=>x.HopDongId==contract && x.TrangThai=="DA_XAC_NHAN").ToListAsync();
            model.DaHoanCoc=ledger.Where(x=>x.Loai=="HOAN_COC" && x.KhoaChongTrung.StartsWith("HOAN_TT:")).Aggregate(0L,(sum,x)=>checked(sum+x.SoTien));
            model.CocDangGiu=ledger.Aggregate(0L,(sum,x)=>checked(sum+(x.Loai=="THU_COC"?x.SoTien:-x.SoTien)));
            var live=await NguonAsync(contract);model.NoHienTai=live.No;model.ConPhaiThu=live.No.Aggregate(0L,(sum,x)=>checked(sum+x.ConNo));
            model.DuocHoan=await db.RolePermissions.CountAsync(x=>x.RoleCode=="CHU_NHA" && (x.ModuleCode=="HOP_DONG" || x.ModuleCode=="TAI_CHINH") && (x.AccessLevel=="WRITE" || x.AccessLevel=="FULL"))==2 && await db.TaiKhoans.AnyAsync(x=>x.Id==actor && x.VaiTro=="CHU_NHA") && await KiemTraQuyenAsync(actor,contract,false);
        }
        await read.CommitAsync();
        return model;
    }

    // Only confirmed transactions are money received. A payment report waiting for confirmation is excluded.
    private async Task<(BangTatToan Bang, List<CongNoTatToan> No, string Fingerprint)> NguonAsync(int contract)
    {
        var finals = await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == contract && x.LoaiHoaDon == "KY_CUOI" && x.TrangThai != "DA_HUY").ToListAsync();
        if (finals.Count != 1) throw new InvalidOperationException("Cần đúng một hóa đơn kỳ cuối chưa hủy trước khi lập bảng tất toán.");
        var final = finals[0];
        var old = await db.HoaDons.AsNoTracking().Where(x => x.HopDongId == contract && x.Id != final.Id && x.TrangThai == "DA_PHAT_HANH")
            .OrderBy(x => x.Nam).ThenBy(x => x.Thang).ToListAsync();
        if (old.Any(x => x.DenNgay >= final.TuNgay)) throw new InvalidOperationException("Có hóa đơn phát hành chồng kỳ hoặc sau kỳ cuối. Cần xử lý trước khi tất toán.");
        var invoices = old.Append(final).ToList(); var ids = invoices.Select(x => x.Id).ToList();
        var payments = await db.ThanhToans.AsNoTracking().Where(x => ids.Contains(x.HoaDonId) && x.TrangThai == "DA_XAC_NHAN").OrderBy(x => x.Id).ToListAsync();
        var deposits = await db.GiaoDichCocs.AsNoTracking().Where(x => x.HopDongId == contract && x.TrangThai == "DA_XAC_NHAN").OrderBy(x => x.Id).ToListAsync();
        if (deposits.Any(x => x.Loai == "CAN_TRU"))
        {
            // Offsets to a cancelled or unissued prior invoice indicate inconsistent financial data.
            var offsets = deposits.Where(x => x.Loai == "CAN_TRU").Select(x => x.HoaDonId).ToList();
            if (await db.HoaDons.AnyAsync(x => offsets.Contains(x.Id) && (x.HopDongId!=contract || x.TrangThai != "DA_PHAT_HANH")))
                throw new InvalidOperationException("Sổ cọc có cấn trừ vào hóa đơn chưa phát hành hoặc đã hủy; cần đối chiếu dữ liệu.");
        }
        long deposit = 0;
        foreach (var entry in deposits) deposit = checked(deposit + (entry.Loai == "THU_COC" ? entry.SoTien : -entry.SoTien));
        if (deposit < 0) throw new InvalidOperationException("Cọc còn giữ đang âm. Cần đối chiếu sổ cọc trước khi tất toán.");
        var rows = new List<CongNoTatToan>();
        foreach (var bill in invoices)
        {
            var paid = payments.Where(x => x.HoaDonId == bill.Id).Aggregate(0L,(sum,x)=>checked(sum+x.SoTien));
            var offset = deposits.Where(x => x.HoaDonId == bill.Id && x.Loai == "CAN_TRU").Aggregate(0L,(sum,x)=>checked(sum+x.SoTien));
            var due = checked(bill.TongTien - paid - offset);
            if (due < 0) throw new InvalidOperationException("Hóa đơn có số đã thu/cấn trừ vượt tổng. Cần đối chiếu trước khi tất toán.");
            rows.Add(new(bill.Id,bill.MaHoaDon,bill.Nam,bill.Thang,bill.TongTien,paid,offset,due,bill.Id==final.Id));
        }
        var bang = new BangTatToan { HopDongId=contract,HoaDonCuoiId=final.Id,NgayTraPhong=final.DenNgay,CocConGiu=deposit,
            NoHoaDonCuoi=rows.Single(x=>x.KyCuoi).ConNo,CongNoCu=rows.Where(x=>!x.KyCuoi).Aggregate(0L,(sum,x)=>checked(sum+x.ConNo)),ChiTietChot=JsonSerializer.Serialize(rows) };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Contract = await db.HopDongs.AsNoTracking().Where(x=>x.Id==contract).Select(x=>new { x.Id,x.TrangThai,x.KhachDungTenId,x.TienCoc }).SingleAsync(),
            Terms=await db.KyHopDongs.AsNoTracking().Where(x=>x.HopDongId==contract).OrderBy(x=>x.Id).Select(x=>new {x.Id,x.NgayBatDau,x.NgayKetThuc}).ToListAsync(),
            Termination=await db.ChamDutHopDongs.AsNoTracking().Where(x=>x.HopDongId==contract).Select(x=>new {x.Id,x.PhienBan}).SingleOrDefaultAsync(),
            Bills=invoices.Select(x=>new { x.Id,x.PhienBan,x.TrangThai,x.TongTien,x.TuNgay,x.DenNgay }),
            Payments=payments.Select(x=>new { x.Id,x.HoaDonId,x.SoTien }),Deposits=deposits.Select(x=>new { x.Id,x.Loai,x.HoaDonId,x.SoTien })
        }))));
        return (bang,rows,fingerprint);
    }

    public async Task<TatToanViewModel> NhapAsync(int actor, int contract, TatToanInput? input = null, bool tinh = false)
    {
        var model = await ThongTinAsync(actor,contract,true);
        if (!await db.HopDongs.AnyAsync(x=>x.Id==contract && x.TrangThai=="DANG_HIEU_LUC")) throw new InvalidOperationException("Chỉ lập/sửa bảng nháp cho hợp đồng đang hiệu lực.");
        var saved = await db.BangTatToans.AsNoTracking().Include(x=>x.KhauTru).SingleOrDefaultAsync(x=>x.HopDongId==contract);
        if (saved is not null && saved.TrangThai != "NHAP") throw new InvalidOperationException("Bảng tất toán đã chốt không được chỉnh sửa.");
        var source = await NguonAsync(contract);
        model.Bang = source.Bang; model.CongNo=source.No;
        model.Input = input ?? new() { HopDongId=contract,PhienBan=saved?.PhienBan??0,DauVanTayNguon=source.Fingerprint,GhiChu=saved?.GhiChu,
            KhauTru=saved?.KhauTru.Where(x=>x.LoaiKhoan=="HU_HONG").OrderBy(x=>x.SoThuTu).Select(x=>new KhauTruInput { TenKhoan=x.TenKhoan,SoTien=x.SoTien,GhiChu=x.GhiChu }).ToList() ?? [] };
        var previous=await db.ChamDutHopDongs.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==contract);
        if(input is null)ChamDutHopDongPolicy.DienInput(model.Input,previous);
        model.ChamDut=await ChamDutHopDongService.DoiChieuAsync(db,contract,source.Bang.NgayTraPhong);
        if (input is not null && tinh && input.DauVanTayNguon != source.Fingerprint)
            throw new InvalidOperationException("Tiền cọc, thanh toán hoặc hóa đơn đã thay đổi. Hãy tải lại trang để đối chiếu số liệu mới.");
        model.Bang.Id=saved?.Id??0; model.Bang.Ma=saved?.Ma??"Chưa lưu";
        model.Bang.PhienBan=saved?.PhienBan??0;
        model.Bang.DauVanTayNguon=source.Fingerprint;
        if (tinh || input is null)
        {
            if(tinh)model.ChamDut=ChamDutHopDongPolicy.KiemTra(model.Input,model.ChamDut.NgayTraPhong,model.ChamDut.NgayKetThuc,model.ChamDut.CocThoaThuan);
            else if(model.ChamDut.TruocHan && previous is not null)
            {
                // Keep the draft editable if a renewed term or agreed deposit changed.
                // Saving always validates against the new proof, including the new cap.
                model.ChamDut.LyDo=previous.LyDo;model.ChamDut.LyDoKhac=previous.LyDoKhac;
                model.ChamDut.ApDungPhat=previous.ApDungPhat;model.ChamDut.TienPhat=previous.TienPhat;
            }
            else if(!model.ChamDut.TruocHan)
            {
                model.Input.ApDungPhat=false;model.Input.TienPhat=0;model.Input.LyDo=null;model.Input.LyDoKhac=null;
            }
            model.Bang.TienPhat=model.ChamDut.TienPhat;
            ApDungKhauTru(model.Bang,model.Input);
        }
        return model;
    }

    public static long LamTron(decimal value)
    {
        if (value < 0 || value > long.MaxValue) throw new InvalidOperationException("Số tiền khấu trừ không được âm hoặc vượt giới hạn hỗ trợ.");
        return checked((long)decimal.Round(value,0,MidpointRounding.AwayFromZero));
    }
    public static void ApDungKhauTru(BangTatToan bang, TatToanInput input)
    {
        if (input.KhauTru.Count>100) throw new InvalidOperationException("Tối đa 100 khoản hư hỏng.");
        if (input.GhiChu?.Trim().Length>1000) throw new InvalidOperationException("Ghi chú tất toán tối đa 1.000 ký tự.");
        var rows=new List<KhauTruTatToan>();
        foreach(var item in input.KhauTru)
        {
            if (string.IsNullOrWhiteSpace(item.TenKhoan) || item.TenKhoan.Trim().Length>200)
                throw new InvalidOperationException("Mỗi khoản hư hỏng phải có tên, tối đa 200 ký tự.");
            if (item.SoTien is null) throw new InvalidOperationException("Nhập số tiền cho mỗi khoản hư hỏng.");
            if (item.GhiChu?.Trim().Length>500) throw new InvalidOperationException("Ghi chú khoản hư hỏng tối đa 500 ký tự.");
            rows.Add(new() { SoThuTu=rows.Count+1,TenKhoan=item.TenKhoan.Trim(),SoTien=LamTron(item.SoTien.Value),GhiChu=item.GhiChu?.Trim() });
        }
        bang.KhauTru=rows; bang.TienHuHong=rows.Aggregate(0L,(sum,x)=>checked(sum+x.SoTien));
        if(bang.TienPhat>0)rows.Add(new() { SoThuTu=rows.Count+1,LoaiKhoan="PHAT_COC",TenKhoan="Phạt chấm dứt trước hạn",SoTien=bang.TienPhat });
        var result=checked(bang.CocConGiu-bang.NoHoaDonCuoi-bang.CongNoCu-bang.TongKhauTru);
        bang.TienPhaiHoan=Math.Max(result,0); bang.TienKhachTraThem=result<0?checked(-result):0; bang.GhiChu=input.GhiChu?.Trim();
    }

    public async Task<int> LuuAsync(int actor,TatToanInput input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite=db.Database.GetDbConnection().BeginPortableTransaction(deferred:false);
        await using var tx=await db.Database.UseTransactionAsync(sqlite);
        var model=await NhapAsync(actor,input.HopDongId,input,true);
        var saved=await db.BangTatToans.Include(x=>x.KhauTru).SingleOrDefaultAsync(x=>x.HopDongId==input.HopDongId);
        if ((saved?.PhienBan??0)!=input.PhienBan) throw new InvalidOperationException("Bảng tất toán đã được người khác lưu. Hãy tải lại trước khi chỉnh sửa.");
        var source=model.Bang;
        if(saved is null)
        {
            source.Ma="TT-"+Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
            source.NguoiLapId=actor;source.NgayTao=clock.UtcNow;source.CapNhatLuc=clock.UtcNow;source.PhienBan=1;
            db.BangTatToans.Add(source);saved=source;
        }
        else
        {
            // Delete only the editable draft's deduction rows, never invoices or financial history.
            // Save deletions first so (settlement, ordinal) stays unique during replacement; outer transaction covers both saves and audit.
            db.KhauTruTatToans.RemoveRange(saved.KhauTru); saved.KhauTru.Clear();
            await db.SaveChangesAsync();
            saved.HoaDonCuoiId=source.HoaDonCuoiId;saved.NgayTraPhong=source.NgayTraPhong;saved.CocConGiu=source.CocConGiu;
            saved.NoHoaDonCuoi=source.NoHoaDonCuoi;saved.CongNoCu=source.CongNoCu;saved.TienHuHong=source.TienHuHong;saved.TienPhat=source.TienPhat;
            saved.TienPhaiHoan=source.TienPhaiHoan;saved.TienKhachTraThem=source.TienKhachTraThem;
            saved.ChiTietChot=source.ChiTietChot;saved.DauVanTayNguon=source.DauVanTayNguon;saved.GhiChu=source.GhiChu;
            saved.KhauTru=source.KhauTru;saved.PhienBan++;saved.CapNhatLuc=clock.UtcNow;
        }
        await ChamDutHopDongService.LuuAsync(db,model.ChamDut!,input.HopDongId,source.HoaDonCuoiId,actor,clock.UtcNow);
        await db.SaveChangesAsync();await sqlite.CommitAsync();return saved.Id;
    }
}
