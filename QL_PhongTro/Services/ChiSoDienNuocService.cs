using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;
using QL_PhongTro.ViewModels;
using QL_PhongTro.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;

namespace QL_PhongTro.Services;

public sealed partial class ChiSoDienNuocService(AppDbContext db, ITimeProvider clock, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider protection)
{
    private sealed record Reading(int HopDongId, int Id, DateOnly TuNgay, DateOnly DenNgay,
        string MaDichVu, decimal? ChiSoCuoi, int LineId);

    public async Task<ChiSoDienNuocViewModel> DanhSachAsync(int accountId, int? toaNhaId, CancellationToken ct = default)
    {
        var role = await db.TaiKhoans.AsNoTracking()
            .Where(x => x.Id == accountId && x.DangHoatDong && !x.IsDeleted)
            .Select(x => x.VaiTro).SingleOrDefaultAsync(ct);
        if (role is not ("CHU_NHA" or "QUAN_LY")) throw new UnauthorizedAccessException();
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var model = new ChiSoDienNuocViewModel { DauKy = new(today.Year, today.Month, 1) };
        model.ToaNhas = await db.ToaNhas.AsNoTracking()
            .Where(x => x.DangHoatDong && (role == "CHU_NHA" ? x.ChuNhaId == accountId : x.QuanLyId == accountId)).OrderBy(x => x.TenToaNha).ThenBy(x => x.Id)
            .Select(x => new ToaNhaGhiChiSo(x.Id, x.TenToaNha)).ToListAsync(ct);
        if (toaNhaId.HasValue && !model.ToaNhas.Any(x => x.Id == toaNhaId)) throw new UnauthorizedAccessException();
        model.ToaNhaId = toaNhaId ?? model.ToaNhas.FirstOrDefault()?.Id;
        if (model.ToaNhaId is null) return model;
        model.KyDaKhoa = (await KyChiSoLockService.LayToaNhaDaKhoaAsync(
            db, [model.ToaNhaId.Value], today.Year, today.Month, ct)).Contains(model.ToaNhaId.Value);
        model.Phongs = await LayPhongAsync(model.ToaNhaId.Value, model.DauKy, ct);
        return model;
    }

    private async Task<List<PhongGhiChiSo>> LayPhongAsync(int buildingId, DateOnly first, CancellationToken ct)
    {
        var last = first.AddMonths(1).AddDays(-1);
        var candidates = await (from p in db.PhongTros.AsNoTracking()
            join h in db.HopDongs on p.Id equals h.PhongId
            join k in db.KyHopDongs on h.Id equals k.HopDongId
            where p.ToaNhaId == buildingId && p.TrangThai == "DANG_THUE" && h.TrangThai == "DANG_HIEU_LUC"
                && k.NgayBatDau <= last && k.NgayKetThuc >= first
                && (h.NgayTraPhong == null || (h.NgayTraPhong >= first && h.NgayTraPhong >= k.NgayBatDau))
            select new { p.Id, p.MaPhong, p.Tang, p.PhienBan, HopDongId = h.Id, h.NgayChot, k.NgayBatDau }).ToListAsync(ct);
        // Multiple terms must not duplicate a room. For successive contracts in one month,
        // use the most recently starting intersecting contract, never combine their readings.
        var rooms = candidates.GroupBy(x => x.Id).Select(g => g.OrderByDescending(x => x.NgayBatDau)
            .ThenByDescending(x => x.HopDongId).First()).OrderBy(x => x.Tang).ThenBy(x => x.MaPhong, StringComparer.Ordinal).ToList();
        var ids = rooms.Select(x => x.HopDongId).Distinct().ToArray();
        var saved = await db.ChiSoDienNuocs.AsNoTracking().Where(x=>ids.Contains(x.HopDongId) && x.DenNgay<=last).ToListAsync(ct);
        var serviceCodes = await db.DichVus.AsNoTracking().Where(x=>x.MaDichVu=="DIEN" || x.MaDichVu=="NUOC").ToDictionaryAsync(x=>x.Id,x=>x.MaDichVu,ct);
        var readings = new List<Reading>();
        // Invoices are an optional module in existing databases. Missing history is
        // not a reason to manufacture readings or install that module on a GET.
        var connection = db.Database.GetDbConnection();
        var close = connection.State != System.Data.ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        bool hasInvoices;
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('hoa_don','chi_tiet_hoa_don')";
            hasInvoices = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 2;
        }
        finally { if (close) await connection.CloseAsync(); }
        if (hasInvoices) readings = await (from invoice in db.HoaDons.AsNoTracking()
            join line in db.ChiTietHoaDons on invoice.Id equals line.HoaDonId
            join service in db.DichVus on line.DichVuId equals service.Id
            where ids.Contains(invoice.HopDongId) && invoice.TrangThai == "DA_PHAT_HANH"
                && line.LoaiKhoan == "DICH_VU" && line.CachTinhApDung == "THEO_CHI_SO" && line.ChiSoCuoi != null
                && (service.MaDichVu == "DIEN" || service.MaDichVu == "NUOC") && invoice.DenNgay <= last
            select new Reading(invoice.HopDongId, invoice.Id, invoice.TuNgay, invoice.DenNgay, service.MaDichVu, line.ChiSoCuoi, line.Id)).ToListAsync(ct);
        var handovers = await db.HopDongChiSoDauKys.AsNoTracking()
            .Where(x => ids.Contains(x.HopDongId) && x.NgayBanGiao <= last).ToDictionaryAsync(x => x.HopDongId, ct);
        var result = rooms.Select(room =>
        {
            ChiSoThamChieu Previous(string code)
            {
                var prior = saved.Where(x=>x.HopDongId==room.HopDongId && x.DenNgay<first && serviceCodes.GetValueOrDefault(x.DichVuId)==code)
                    .OrderByDescending(x=>x.DenNgay).FirstOrDefault();
                if(prior != null) return new(prior.ChiSoCuoi);
                var previous = readings.Where(x => x.HopDongId == room.HopDongId && x.MaDichVu == code && x.DenNgay < first)
                    .OrderByDescending(x => x.DenNgay).ThenByDescending(x => x.Id).ThenByDescending(x => x.LineId).FirstOrDefault();
                if (previous != null) return new(previous.ChiSoCuoi);
                if (handovers.TryGetValue(room.HopDongId, out var handover))
                    return new(code == "DIEN" ? handover.ChiSoDien : handover.ChiSoNuoc, true);
                var current = saved.FirstOrDefault(x => x.HopDongId == room.HopDongId
                    && x.TuNgay == first && x.DenNgay == last && serviceCodes.GetValueOrDefault(x.DichVuId) == code);
                return new(current?.ChiSoDau);
            }
            bool Current(string code) => readings.Any(x => x.HopDongId == room.HopDongId && x.MaDichVu == code
                && x.TuNgay >= first && x.DenNgay <= last);
            return new PhongGhiChiSo(room.Id, room.HopDongId, room.MaPhong, room.Tang,
                Previous("DIEN"), Previous("NUOC"), Current("DIEN") && Current("NUOC")) { PhienBanPhong=room.PhienBan };
        }).ToList();
        var roomIds=rooms.Select(x=>x.Id).ToArray();
        var assignments=await db.DichVuPhongs.AsNoTracking().Where(x=>roomIds.Contains(x.PhongId))
            .Select(x=>new {x.Id,x.PhongId,x.DichVuToaNha.DichVuId}).ToListAsync(ct);
        var assignmentIds=assignments.Select(x=>x.Id).ToArray();
        var removals=await db.NgungDichVuPhongs.AsNoTracking().Where(x=>assignmentIds.Contains(x.DichVuPhongId)
            && x.NgungTuKy<=first && (x.ApDungLaiTuKy==null || x.ApDungLaiTuKy>first)).Select(x=>x.DichVuPhongId).ToListAsync(ct);
        var prices=await db.CauHinhDichVus.AsNoTracking().Where(x=>x.ToaNhaId==buildingId
            && (x.PhongId==null || roomIds.Contains(x.PhongId.Value)) && x.TuNgay<=last && (x.DenNgay==null || x.DenNgay>=first)).ToListAsync(ct);
        var activeServices=await db.DichVus.AsNoTracking().Where(x=>x.DangHoatDong).Select(x=>x.Id).ToListAsync(ct);
        var contractServices=hasInvoices ? await db.HopDongDichVus.AsNoTracking().Where(x=>ids.Contains(x.HopDongId)).Select(x=>new{x.HopDongId,x.DichVuId}).ToListAsync(ct) : [];
        for(var rowIndex=0;rowIndex<result.Count;rowIndex++)
        {
            var row=result[rowIndex];
            var context=rooms.Single(x=>x.Id==row.PhongId);
            var cutoff=new DateOnly(first.Year,first.Month,Math.Min(context.NgayChot,DateTime.DaysInMonth(first.Year,first.Month)));
            var assigned=assignments.Where(x=>x.PhongId==row.PhongId);
            foreach(var assignment in assigned.Where(x=>serviceCodes.ContainsKey(x.DichVuId)))
            {
                if(removals.Contains(assignment.Id)) continue;
                var agreed=contractServices.Where(x=>x.HopDongId==row.HopDongId).ToList();
                if(agreed.Count>0 && !agreed.Any(x=>x.DichVuId==assignment.DichVuId)) continue;
                var configs=prices.Where(x=>x.DichVuId==assignment.DichVuId
                    && (x.PhongId==row.PhongId || x.PhongId==null) && x.TuNgay<=cutoff && (x.DenNgay==null || x.DenNgay>=cutoff)).ToList();
                var own=configs.Where(x=>x.PhongId==row.PhongId).ToList();
                var effective=own.Count>0?own:configs.Where(x=>x.PhongId==null).ToList();
                if(effective.Count!=1) continue;
                var config=effective[0];
                if(!config.DangApDung || !config.DaChotGia || config.DonGia<=0 || config.CachTinh!="THEO_CHI_SO"
                    || !activeServices.Contains(assignment.DichVuId)) continue;
                var code=serviceCodes[assignment.DichVuId];
                var current=saved.SingleOrDefault(x=>x.HopDongId==row.HopDongId && x.DichVuId==assignment.DichVuId && x.TuNgay==first && x.DenNgay==last);
                row.DichVu.Add(new(assignment.DichVuId,code,code=="DIEN"?row.Dien:row.Nuoc,current?.ChiSoCuoi,current?.PhienBan??-1,current?.DaKhoa??false));
            }
            // No applicable meters means there is no meter-closing action for this room.
            if(row.DichVu.Count>0)
                result[rowIndex]=row with {DaChot=row.DichVu.All(x=>x.Moi.HasValue)};
        }
        return result;
    }

    public async Task<Dictionary<string,string[]>> LuuAsync(int actor,LuuChiSoInput input,CancellationToken ct=default)
    {
        input.CanhBaos.Clear();
        await db.Database.OpenConnectionAsync(ct);
        await using var sqlite=((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred:false);
        await using var tx=await db.Database.UseTransactionAsync(sqlite,ct);
        var model=await DanhSachAsync(actor,input.ToaNhaId,ct);
        var room=model.Phongs.SingleOrDefault(x=>x.PhongId==input.PhongId);
        if(room==null || room.HopDongId!=input.HopDongId) throw new UnauthorizedAccessException();
        var errors=new Dictionary<string,string[]>();
        void Error(string key,string message)=>errors[key]=[message];
        if(model.KyDaKhoa)
            Error("Phong","Kỳ chốt chỉ số của tòa nhà đã khóa sau khi phát hành hóa đơn. Không thể thêm hoặc sửa chỉ số.");
        else if(input.Ky!=model.DauKy || input.PhienBanPhong!=room.PhienBanPhong)
            Error("Phong","Kỳ hoặc thông tin phòng đã thay đổi. Vui lòng tải lại trang.");
        if(room.DichVu.Count==0) Error("Phong","Phòng không có dịch vụ điện/nước theo chỉ số đang áp dụng.");
        // A supplied baseline is accepted only when server-side history is absent.
        room.DichVu = room.DichVu.Select(service => service.Truoc.GiaTri.HasValue ? service :
            service with { Truoc = new(service.Ma == "DIEN" ? input.DienDau : input.NuocDau) }).ToList();
        foreach(var service in room.DichVu)
        {
            var field=service.Ma=="DIEN"?"DienMoi":"NuocMoi";
            var value=service.Ma=="DIEN"?input.DienMoi:input.NuocMoi;
            var version=service.Ma=="DIEN"?input.DienPhienBan:input.NuocPhienBan;
            if(service.DaKhoa || version!=service.PhienBan) Error(field,"Chỉ số đã thay đổi hoặc bị khóa. Vui lòng tải lại trang.");
            else if(!service.Truoc.GiaTri.HasValue || service.Truoc.GiaTri < 0
                || service.Truoc.GiaTri > 99999999999.999m || decimal.Round(service.Truoc.GiaTri.Value,3) != service.Truoc.GiaTri)
                Error(service.Ma == "DIEN" ? "DienDau" : "NuocDau", "Nhập chỉ số đầu kỳ không âm, tối đa 3 chữ số thập phân và không quá 99.999.999.999,999.");
            else if(!value.HasValue || value<0 || value>99999999999.999m || decimal.Round(value.Value,3)!=value)
                Error(field,"Nhập chỉ số không âm, tối đa 3 chữ số thập phân và không quá 99.999.999.999,999.");
            else if(value<service.Truoc.GiaTri) Error(field,"Chỉ số mới không được nhỏ hơn chỉ số kỳ trước.");
        }
        if(errors.Count>0) return errors;
        var abnormal = await CheckUsageAsync(actor, input, room, model.DauKy, ct);
        if (input.CanhBaos.Count > 0) { Error("Anomaly", "Cần xác nhận mức tiêu thụ bất thường trước khi lưu."); return errors; }
        foreach(var service in room.DichVu)
        {
            var record=await db.ChiSoDienNuocs.SingleOrDefaultAsync(x=>x.HopDongId==room.HopDongId && x.DichVuId==service.Id && x.TuNgay==model.DauKy,ct);
            if(record==null) {record=new(){HopDongId=room.HopDongId,DichVuId=service.Id,TuNgay=model.DauKy,DenNgay=model.DauKy.AddMonths(1).AddDays(-1)};db.ChiSoDienNuocs.Add(record);}
            else record.PhienBan++;
            record.ChiSoDau=service.Truoc.GiaTri!.Value;
            record.ChiSoCuoi=(service.Ma=="DIEN"?input.DienMoi:input.NuocMoi)!.Value;
            record.DaXacNhanBatThuong = abnormal.Contains(service.Ma);
            record.NguoiNhapId=actor;record.NgayNhap=clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);await sqlite.CommitAsync(ct);return errors;
    }
}
