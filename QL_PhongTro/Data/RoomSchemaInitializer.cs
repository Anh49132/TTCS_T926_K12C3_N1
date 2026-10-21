using Microsoft.EntityFrameworkCore;

namespace QL_PhongTro.Data;

public static class RoomSchemaInitializer
{
    public static void EnsureSchema(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS toa_nha (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                chu_nha_id INTEGER NOT NULL,
                quan_ly_id INTEGER NULL,
                ten_toa_nha TEXT NOT NULL,
                dia_chi TEXT NOT NULL,
                phuong_xa TEXT NULL,
                quan_huyen TEXT NULL,
                tinh_thanh TEXT NULL,
                so_tang INTEGER NULL,
                ngay_chot_hang_thang INTEGER NOT NULL DEFAULT 1 CHECK (ngay_chot_hang_thang BETWEEN 1 AND 31),
                dang_hoat_dong INTEGER NOT NULL DEFAULT 1,
                ghi_chu TEXT NULL,
                FOREIGN KEY (chu_nha_id) REFERENCES tai_khoan(id) ON DELETE RESTRICT,
                FOREIGN KEY (quan_ly_id) REFERENCES tai_khoan(id) ON DELETE RESTRICT
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS phong_tro (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                toa_nha_id INTEGER NOT NULL,
                ma_phong TEXT NOT NULL CHECK (length(ma_phong) <= 20),
                tang INTEGER NOT NULL,
                loai_phong TEXT NULL,
                dien_tich DECIMAL(8,2) NOT NULL CHECK (dien_tich > 0),
                gia_thue INTEGER NOT NULL CHECK (gia_thue > 0),
                tien_coc_du_kien INTEGER NOT NULL DEFAULT 0 CHECK (tien_coc_du_kien >= 0),
                so_nguoi_toi_da INTEGER NOT NULL CHECK (so_nguoi_toi_da > 0),
                trang_thai TEXT NOT NULL DEFAULT 'TRONG'
                    CHECK (trang_thai IN ('TRONG', 'DA_DAT_COC', 'DANG_THUE', 'NGUNG_CHO_THUE')),
                mo_ta TEXT NULL,
                ngay_tao TEXT NOT NULL,
                phien_ban INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (toa_nha_id) REFERENCES toa_nha(id) ON DELETE RESTRICT
            );
            """);

        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_toa_nha_chu_nha_id ON toa_nha(chu_nha_id)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_phong_tro_toa_nha_trang_thai ON phong_tro(toa_nha_id, trang_thai)");

        var hasDuplicateCodes = db.PhongTros.AsNoTracking()
            .GroupBy(room => new { room.ToaNhaId, room.MaPhong })
            .Any(group => group.Count() > 1);
        if (hasDuplicateCodes)
            throw new InvalidOperationException("The existing database contains duplicate room codes within a building. Resolve them before enabling room-code uniqueness.");

        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS ux_phong_tro_toa_nha_ma_phong ON phong_tro(toa_nha_id, ma_phong)");
    }
}