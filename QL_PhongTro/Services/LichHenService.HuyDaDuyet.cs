using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Models;
using QL_PhongTro.ViewModels;

namespace QL_PhongTro.Services;

public partial class LichHenService
{
    public async Task<bool> HuyDaDuyetAsync(int id, int actor, HuyYeuCauDaDuyetInput input, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await using var efTransaction = await db.Database.UseTransactionAsync(tx, ct);
        var request = await LoadAsync(id, ct, tx) ?? throw new KeyNotFoundException("Yêu cầu không tồn tại.");
        if (!await CoQuyenXuLyAsync(actor, request, ct) || !await db.TaiKhoans.AnyAsync(
            x => x.Id == actor && x.VaiTro == "CHU_NHA" && x.DangHoatDong && !x.IsDeleted, ct))
            throw new UnauthorizedAccessException("Chỉ chủ nhà của phòng mới hủy được giữ chỗ.");
        var reason = input.LyDo?.Trim() ?? "";
        if (!input.XacNhan || reason.Length is < 5 or > 500 || input.PhienBan is null || input.PhienBanPhong is null)
            throw new InvalidOperationException("Nhập lý do từ 5 đến 500 ký tự và xác nhận hủy giữ chỗ.");

        using var command = tx.Connection!.CreateCommand();
        command.Transaction = tx;
        command.Parameters.AddWithValue("$request", id);
        command.Parameters.AddWithValue("$room", request.PhongId);
        if (request.TrangThai == LichHenTrangThai.DaHuy)
        {
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM yeu_cau_thue_lich_su WHERE yeu_cau_thue_id=$request AND hanh_dong='HUY_YEU_CAU_DA_DUYET')";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1) return false;
        }
        if (request.TrangThai != LichHenTrangThai.DaDuyet)
            throw new InvalidOperationException("Chỉ yêu cầu đã duyệt mới được hủy giữ chỗ theo cách này.");
        if (request.PhienBan != input.PhienBan || request.PhienBanPhong != input.PhienBanPhong)
            throw new LichHenConflictException("Yêu cầu hoặc phòng đã thay đổi. Vui lòng tải lại trang trước khi hủy.");
        if (request.TrangThaiPhong != LichHenTrangThai.PhongDaDatCoc)
            throw new InvalidOperationException("Phòng không còn ở trạng thái giữ chỗ. Không thể tự chuyển phòng về trống.");

        // Deposits belong to contracts. A confirmed transaction on this request's
        // contract must be handled by the financial/contract flow, never refunded here.
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='giao_dich_coc'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1)
        {
            command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('giao_dich_coc') WHERE name IN ('hop_dong_id','trang_thai')";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 2)
                throw new InvalidOperationException("Chưa kiểm tra được giao dịch cọc trên CSDL này. Không hủy giữ chỗ; hãy kiểm tra hợp đồng và cọc trước.");
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM giao_dich_coc c JOIN hop_dong h ON h.id=c.hop_dong_id WHERE h.yeu_cau_thue_id=$request AND c.trang_thai='DA_XAC_NHAN')";
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1)
                throw new InvalidOperationException("Đã có giao dịch cọc xác nhận. Hãy xử lý hợp đồng và tiền cọc trước khi giải phóng phòng.");
        }
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM hop_dong WHERE yeu_cau_thue_id=$request OR (phong_id=$room AND trang_thai IN ('NHAP','CHO_HIEU_LUC','DANG_HIEU_LUC')))";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1)
            throw new InvalidOperationException("Yêu cầu hoặc phòng đã có hợp đồng. Hãy xử lý hợp đồng trước; thao tác này chỉ hủy giữ chỗ chưa lập hợp đồng.");
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM yeu_cau_thue r JOIN tin_dang t ON t.id=r.tin_dang_id WHERE t.phong_id=$room AND r.id<>$request AND r.trang_thai='DA_DUYET')";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1)
            throw new InvalidOperationException("Phòng có yêu cầu đã duyệt khác. Không thể tự giải phóng giữ chỗ của khách khác.");

        command.CommandText = "SELECT EXISTS(SELECT 1 FROM phong_tro p JOIN toa_nha b ON b.id=p.toa_nha_id WHERE p.id=$room AND b.dang_hoat_dong=1)";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
            throw new InvalidOperationException("Tòa nhà đã ngừng hoạt động. Không thể tự đăng lại tin.");
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM tin_dang WHERE phong_id=$room AND trang_thai='DANG_HIEN_THI' AND id<>(SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$request))";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1)
            throw new InvalidOperationException("Phòng đã có tin khác đang hiển thị. Hãy kiểm tra tin đăng trước khi hủy giữ chỗ.");

        var now = UtcHienTai();
        command.Parameters.AddWithValue("$version", input.PhienBan.Value);
        command.Parameters.AddWithValue("$roomVersion", input.PhienBanPhong.Value);
        command.Parameters.AddWithValue("$actor", actor);
        command.Parameters.AddWithValue("$now", SqlUtc(now));
        command.CommandText = """
            UPDATE yeu_cau_thue SET trang_thai='DA_HUY',nguoi_xu_ly_id=$actor,ngay_xu_ly=$now,phien_ban=phien_ban+1
            WHERE id=$request AND trang_thai='DA_DUYET' AND phien_ban=$version;
            UPDATE phong_tro SET trang_thai='TRONG',phien_ban=phien_ban+1
            WHERE id=$room AND trang_thai='DA_DAT_COC' AND phien_ban=$roomVersion;
            """;
        if (await command.ExecuteNonQueryAsync(ct) != 2)
            throw new LichHenConflictException("Yêu cầu hoặc phòng đã thay đổi. Vui lòng tải lại trang.");

        // Republish only the source listing, with the standard 30-day lifetime.
        command.CommandText = "SELECT id FROM tin_dang WHERE id=(SELECT tin_dang_id FROM yeu_cau_thue WHERE id=$request)";
        var listing = await command.ExecuteScalarAsync(ct);
        var (name, role) = await DocNguoiThucHienAsync(tx, actor, ct);
        if (listing is not null)
        {
            command.Parameters.AddWithValue("$listing", listing);
            command.CommandText = "SELECT trang_thai FROM tin_dang WHERE id=$listing";
            var previousStatus = (string)(await command.ExecuteScalarAsync(ct))!;
            command.Parameters.AddWithValue("$before", System.Text.Json.JsonSerializer.Serialize(new { trang_thai = previousStatus }));
            command.Parameters.AddWithValue("$expires", SqlUtc(now.AddDays(30)));
            command.CommandText = "UPDATE tin_dang SET trang_thai='DANG_HIEN_THI',ngay_dang=$now,ngay_het_han=$expires WHERE id=$listing";
            await command.ExecuteNonQueryAsync(ct);
            command.Parameters.AddWithValue("$name", (object?)name ?? DBNull.Value);
            command.Parameters.AddWithValue("$role", (object?)role ?? DBNull.Value);
            command.CommandText = """
                INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,du_lieu_truoc,du_lieu_sau,thoi_diem)
                VALUES($actor,$name,$role,'tin_dang',$listing,'DOI_TRANG_THAI',$before,'{"trang_thai":"DANG_HIEN_THI"}',$now);
                """;
            await command.ExecuteNonQueryAsync(ct);
        }
        await GhiNhatKyPhongAsync(tx, request.PhongId, LichHenTrangThai.PhongDaDatCoc, LichHenTrangThai.PhongTrong,
            actor, name, role, now, ct);
        await GhiLichSuAsync(tx, id, request.TrangThai, LichHenTrangThai.DaHuy, HanhDongYeuCau.HuyDaDuyet,
            actor, name, role, request.LichHen, request.LichHen, null, reason, now, ct);
        await GhiThongBaoAsync(tx, request, LoaiThongBaoYeuCau.YeuCauDaHuy, "Yêu cầu thuê đã được hủy",
            $"Chủ nhà đã ghi nhận khách không thuê và hủy giữ chỗ cho yêu cầu {request.MaYeuCau}.", now, ct);
        tx.Commit();
        return true;
    }
}
