using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;
namespace QL_PhongTro.Services;

public sealed partial class TatToanService
{
    public async Task<TatToanViewModel> KiemTraChotAsync(int actor,int contract)
    {
        if(db.Database.CurrentTransaction is not null)return await KiemTraChotCoreAsync(actor,contract);
        await using var read=await db.Database.BeginTransactionAsync();
        var model=await KiemTraChotCoreAsync(actor,contract);await read.CommitAsync();return model;
    }

    private async Task<TatToanViewModel> KiemTraChotCoreAsync(int actor,int contract)
    {
        var model=await ThongTinAsync(actor,contract,true);
        if(model.TrangThaiHopDong!="DANG_HIEU_LUC")throw new InvalidOperationException("Chỉ xác nhận hợp đồng đang hiệu lực và chưa tất toán.");
        var saved=await db.BangTatToans.AsNoTracking().Include(x=>x.KhauTru).SingleOrDefaultAsync(x=>x.HopDongId==contract)
            ?? throw new InvalidOperationException("Cần lưu đầy đủ bảng tất toán trước khi xác nhận.");
        if(saved.TrangThai!="NHAP" || await db.XacNhanTatToans.AnyAsync(x=>x.HopDongId==contract))throw new InvalidOperationException("Hợp đồng đã được xác nhận tất toán.");
        var source=await NguonAsync(contract);
        if(saved.HoaDonCuoiId!=source.Bang.HoaDonCuoiId || saved.NgayTraPhong!=source.Bang.NgayTraPhong || saved.CocConGiu!=source.Bang.CocConGiu
            || saved.NoHoaDonCuoi!=source.Bang.NoHoaDonCuoi || saved.CongNoCu!=source.Bang.CongNoCu || saved.ChiTietChot!=source.Bang.ChiTietChot)
            throw new InvalidOperationException("Tiền cọc hoặc công nợ đã thay đổi. Hãy sửa và lưu lại bảng tất toán để đối chiếu trước khi xác nhận.");
        if(await db.HoaDons.AnyAsync(x=>x.HopDongId==contract && x.Id!=saved.HoaDonCuoiId && x.TrangThai=="NHAP"))
            throw new InvalidOperationException("Còn hóa đơn nháp chưa xử lý. Cần đối chiếu đầy đủ trước khi tất toán.");
        if(await db.ThanhToans.AnyAsync(x=>x.TrangThai=="CHO_XAC_NHAN" && db.HoaDons.Any(h=>h.Id==x.HoaDonId && h.HopDongId==contract)))
            throw new InvalidOperationException("Còn khoản thanh toán chờ xác nhận. Cần xử lý trước khi tất toán.");
        var info=await db.ChamDutHopDongs.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==contract)
            ?? throw new InvalidOperationException("Chưa lưu đầy đủ thông tin trả phòng/chấm dứt hợp đồng.");
        var proof=await ChamDutHopDongService.DoiChieuAsync(db,contract,saved.NgayTraPhong);
        var input=new TatToanInput {KhauTru=saved.KhauTru.Where(x=>x.LoaiKhoan=="HU_HONG").OrderBy(x=>x.SoThuTu).Select(x=>new KhauTruInput {TenKhoan=x.TenKhoan,SoTien=x.SoTien,GhiChu=x.GhiChu}).ToList(),GhiChu=saved.GhiChu};
        ChamDutHopDongPolicy.DienInput(input,info);
        var normalized=ChamDutHopDongPolicy.KiemTra(input,proof.NgayTraPhong,proof.NgayKetThuc,proof.CocThoaThuan);
        if(info.HoaDonCuoiId!=saved.HoaDonCuoiId || info.NgayTraPhong!=proof.NgayTraPhong || info.NgayKetThuc!=proof.NgayKetThuc || info.CocThoaThuan!=proof.CocThoaThuan || normalized.TienPhat!=saved.TienPhat)
            throw new InvalidOperationException("Điều khoản chấm dứt đã thay đổi. Hãy kiểm tra và lưu lại bảng tất toán.");
        var calculated=source.Bang;calculated.TienPhat=normalized.TienPhat;ApDungKhauTru(calculated,input);
        if(calculated.TienHuHong!=saved.TienHuHong || calculated.TienPhaiHoan!=saved.TienPhaiHoan || calculated.TienKhachTraThem!=saved.TienKhachTraThem
            || saved.KhauTru.Count(x=>x.LoaiKhoan=="PHAT_COC")!=(saved.TienPhat>0?1:0)
            || saved.KhauTru.Where(x=>x.LoaiKhoan=="PHAT_COC").Any(x=>x.SoTien!=saved.TienPhat))
            throw new InvalidOperationException("Các khoản khấu trừ hoặc tổng tất toán không khớp. Cần lưu lại bảng nháp.");
        var room=await (from h in db.HopDongs join p in db.PhongTros on h.PhongId equals p.Id where h.Id==contract select p).AsNoTracking().SingleAsync();
        if(room.TrangThai!="DANG_THUE" || await db.HopDongs.AnyAsync(x=>x.PhongId==room.Id && x.Id!=contract && x.TrangThai=="DANG_HIEU_LUC"))
            throw new InvalidOperationException("Trạng thái phòng/hợp đồng không phù hợp để hoàn tất trả phòng.");
        if(await db.TinDangs.AnyAsync(x=>x.PhongId==room.Id && x.TrangThai=="DANG_HIEN_THI"))
            throw new InvalidOperationException("Phòng còn tin đang công khai. Hãy tạm ẩn tin cũ trước khi xác nhận trả phòng để tránh hiển thị ngoài ý muốn.");
        var bill=await db.HoaDons.AsNoTracking().Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==saved.HoaDonCuoiId);
        if(bill.TrangThai!="NHAP" || !bill.ChiTiet.Any(x=>x.LoaiKhoan=="TIEN_PHONG") || bill.ChiTiet.Count==0 || HoaDonDichVuService.TongCong(bill.ChiTiet)!=bill.TongTien)
            throw new InvalidOperationException("Hóa đơn kỳ cuối phải là bản nháp đầy đủ, tổng tiền khớp chi tiết.");
        var readings=await db.ChiSoDienNuocs.AsNoTracking().Where(x=>x.HopDongId==contract).OrderBy(x=>x.Id).ToListAsync();
        var baseline=await db.HopDongChiSoDauKys.AsNoTracking().SingleOrDefaultAsync(x=>x.HopDongId==contract);
        model.Bang=saved;model.CongNo=source.No;model.ChamDut=info;
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            source.Fingerprint,saved.PhienBan,saved.TienPhat,saved.TienHuHong,saved.GhiChu,
            Rows=saved.KhauTru.OrderBy(x=>x.SoThuTu),Lines=bill.ChiTiet.OrderBy(x=>x.SoThuTu),room.Id,RoomVersion=room.PhienBan,room.TrangThai,room.GiaThue,
            readings,baseline
        }))));
        model.Confirm=new() {HopDongId=contract,PhienBan=saved.PhienBan,DauVanTay=hash};return model;
    }

    public async Task HoanTatAsync(int actor,XacNhanTatToanInput input)
    {
        await db.Database.OpenConnectionAsync();
        await using var sqlite=((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred:false);
        await using var tx=await db.Database.UseTransactionAsync(sqlite);
        var model=await KiemTraChotCoreAsync(actor,input.HopDongId);
        if(!input.DaKiemTra || !input.DongY)throw new InvalidOperationException("Cần xác nhận đã kiểm tra và hai bên đã đối chiếu số liệu.");
        if(input.PhienBan!=model.Confirm.PhienBan || input.DauVanTay!=model.Confirm.DauVanTay)
            throw new InvalidOperationException("Dữ liệu đã thay đổi sau khi kiểm tra. Hãy mở lại bước xác nhận.");
        var bang=await db.BangTatToans.SingleAsync(x=>x.Id==model.Bang.Id);
        var bill=await db.HoaDons.Include(x=>x.ChiTiet).SingleAsync(x=>x.Id==bang.HoaDonCuoiId);
        foreach(var fee in model.Bang.KhauTru.OrderBy(x=>x.SoThuTu).Where(x=>x.SoTien>0))
            bill.ChiTiet.Add(new() {SoThuTu=bill.ChiTiet.Max(x=>x.SoThuTu)+1,LoaiKhoan="PHAT_SINH",TenKhoan=fee.TenKhoan,
                DonViTinh="khoản",SoLuong=1,DonGia=fee.SoTien,ThanhTien=fee.SoTien,GhiChu=$"Khấu trừ tất toán {bang.Ma}: {(fee.LoaiKhoan=="PHAT_COC"?"phạt chấm dứt trước hạn":"hư hỏng")}."});
        bill.TongTien=HoaDonDichVuService.TongCong(bill.ChiTiet);bill.PhienBan++;
        await db.SaveChangesAsync();
        await LuuChiSoCuoiAsync(actor,bill);
        var today=DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        await new HoaDonDichVuService(db,new DichVuService(db,time:clock),clock).PhatHanhKyCuoiTrongTatToanAsync(actor,
            new() {Id=bill.Id,PhienBan=bill.PhienBan,XacNhan=true,NgayPhatHanh=today,HanThanhToan=today.AddDays(7)});
        // Damage/penalty now belong to the invoice. These fields become analysis only, never a second debt.
        bang.NoHoaDonCuoi=checked(bang.NoHoaDonCuoi+bang.TongKhauTru);
        var debts=model.CongNo.Select(x=>x.KyCuoi?x with {TongTien=checked(x.TongTien+bang.TongKhauTru),ConNo=checked(x.ConNo+bang.TongKhauTru)}:x).ToList();
        bang.ChiTietChot=JsonSerializer.Serialize(debts);
        var remaining=bang.CocConGiu;long offset=0;
        foreach(var debt in debts.OrderBy(x=>x.Nam).ThenBy(x=>x.Thang).ThenBy(x=>x.HoaDonId))
        {
            var amount=Math.Min(remaining,debt.ConNo);if(amount==0)continue;
            db.GiaoDichCocs.Add(new() {Ma="CT-"+Guid.NewGuid().ToString("N"),KhoaChongTrung=$"TT:{bang.Id}:{debt.HoaDonId}",HopDongId=input.HopDongId,
                HoaDonId=debt.HoaDonId,Loai="CAN_TRU",SoTien=amount,NguoiThucHienId=actor,NgayGiaoDich=clock.UtcNow});
            remaining=checked(remaining-amount);offset=checked(offset+amount);
        }
        var (h,room)=await GiaiPhongPhongAsync(actor,input.HopDongId,bang.NgayTraPhong);
        var history=await db.LichSuTrangThaiPhongs.SingleOrDefaultAsync(x=>x.PhongId==room.Id && x.DenThoiDiem==null);
        if(history is not null){history.DenThoiDiem=clock.UtcNow;await db.SaveChangesAsync();}
        db.LichSuTrangThaiPhongs.Add(new() {PhongId=room.Id,HopDongId=h.Id,TrangThai="TRONG",TuThoiDiem=clock.UtcNow});
        await TaoTinNhapSauTraPhongAsync(actor,h,room);
        bang.TrangThai="DA_CHOT";bang.PhienBan++;bang.CapNhatLuc=clock.UtcNow;
        await db.SaveChangesAsync();
        db.XacNhanTatToans.Add(new() {ThanhLyId=bang.Id,HopDongId=h.Id,NguoiXacNhanId=actor,
            TenNguoiXacNhan=await db.TaiKhoans.Where(x=>x.Id==actor).Select(x=>x.HoTen).SingleAsync(),ThoiDiem=clock.UtcNow,
            SoTienTatToan=checked(bang.TienPhaiHoan-bang.TienKhachTraThem),TienCanTru=offset});
        await db.SaveChangesAsync();
        await new CheckoutRequestService(db,clock).CompleteAsync(actor,input.HopDongId);
        await sqlite.CommitAsync();
    }

    private async Task LuuChiSoCuoiAsync(int actor,HoaDon bill)
    {
        foreach(var line in bill.ChiTiet.Where(x=>x.CachTinhApDung==CachTinhDichVu.TheoChiSo && x.DichVuId.HasValue))
        {
            var previous=await db.ChiSoDienNuocs.Where(x=>x.HopDongId==bill.HopDongId && x.DichVuId==line.DichVuId).OrderByDescending(x=>x.DenNgay).FirstOrDefaultAsync();
            var baseline=await db.HopDongChiSoDauKys.SingleOrDefaultAsync(x=>x.HopDongId==bill.HopDongId);
            var code=await db.DichVus.Where(x=>x.Id==line.DichVuId).Select(x=>x.MaDichVu).SingleAsync();
            var old=previous?.ChiSoCuoi??(code=="DIEN"?baseline?.ChiSoDien:baseline?.ChiSoNuoc);
            var day=previous?.DenNgay??baseline?.NgayBanGiao;
            if(old is null || day is null || day>bill.DenNgay || line.ChiSoDau!=old || line.ChiSoCuoi is null || line.ChiSoCuoi<old)
                throw new InvalidOperationException("Chỉ số bàn giao không còn khớp lần chốt gần nhất. Cần đối chiếu lại trước khi tất toán.");
            // The installed meter table requires whole months. The final partial-period values
            // remain immutable invoice snapshots; do not invent a full month or change its schema.
            if(previous is not null && !previous.DaKhoa){previous.DaKhoa=true;previous.PhienBan++;}
        }
        await db.SaveChangesAsync();
    }
}
