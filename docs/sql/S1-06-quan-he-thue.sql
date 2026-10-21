-- Run only after approval, schema inspection and backup. No demo data.
-- Minimal rental context for S1-06; not a complete contract-management module.
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;
CREATE TABLE toa_nha (
 id INTEGER PRIMARY KEY AUTOINCREMENT, chu_nha_id INTEGER NOT NULL REFERENCES tai_khoan(id),
 quan_ly_id INTEGER REFERENCES tai_khoan(id), ten_toa_nha TEXT NOT NULL, dia_chi TEXT NOT NULL,
 phuong_xa TEXT, quan_huyen TEXT, tinh_thanh TEXT, so_tang INTEGER,
 ngay_chot_hang_thang INTEGER NOT NULL DEFAULT 1, dang_hoat_dong INTEGER NOT NULL DEFAULT 1, ghi_chu TEXT
);
CREATE TABLE phong_tro (
 id INTEGER PRIMARY KEY AUTOINCREMENT, toa_nha_id INTEGER NOT NULL REFERENCES toa_nha(id),
 ma_phong TEXT NOT NULL, tang INTEGER NOT NULL, loai_phong TEXT, dien_tich DECIMAL(8,2) NOT NULL CHECK(dien_tich>0),
 gia_thue INTEGER NOT NULL CHECK(gia_thue>0), tien_coc_du_kien INTEGER NOT NULL DEFAULT 0,
 so_nguoi_toi_da INTEGER NOT NULL CHECK(so_nguoi_toi_da>0), trang_thai TEXT NOT NULL DEFAULT 'TRONG',
 mo_ta TEXT, ngay_tao TEXT NOT NULL, phien_ban INTEGER NOT NULL DEFAULT 0,
 UNIQUE(toa_nha_id, ma_phong)
);
CREATE TABLE hop_dong (
 id INTEGER PRIMARY KEY AUTOINCREMENT, ma_hop_dong TEXT NOT NULL UNIQUE,
 phong_id INTEGER NOT NULL REFERENCES phong_tro(id), khach_dung_ten_id INTEGER NOT NULL REFERENCES khach_thue(id),
 tien_coc_thoa_thuan INTEGER NOT NULL DEFAULT 0, dieu_khoan TEXT,
 trang_thai TEXT NOT NULL DEFAULT 'NHAP', ngay_kich_hoat TEXT, ngay_tra_phong TEXT, ly_do_ket_thuc TEXT,
 nguoi_lap_id INTEGER NOT NULL REFERENCES tai_khoan(id), ngay_tao TEXT NOT NULL, phien_ban INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE ky_hop_dong (
 id INTEGER PRIMARY KEY AUTOINCREMENT, hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id),
 so_thu_tu INTEGER NOT NULL CHECK(so_thu_tu>0), ngay_bat_dau TEXT NOT NULL, ngay_ket_thuc TEXT NOT NULL,
 so_thang INTEGER NOT NULL CHECK(so_thang>0), gia_thue INTEGER NOT NULL CHECK(gia_thue>0),
 thong_tin_chot TEXT, tep_hop_dong TEXT, nguoi_lap_id INTEGER NOT NULL REFERENCES tai_khoan(id),
 ngay_tao TEXT NOT NULL, ghi_chu TEXT, UNIQUE(hop_dong_id,so_thu_tu), UNIQUE(hop_dong_id,ngay_bat_dau),
 CHECK(ngay_ket_thuc>=ngay_bat_dau)
);
CREATE TABLE nguoi_o_ghep (
 id INTEGER PRIMARY KEY AUTOINCREMENT, hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id),
 khach_thue_id INTEGER NOT NULL REFERENCES khach_thue(id), ngay_vao TEXT NOT NULL, ngay_chuyen_di TEXT,
 ky_bat_dau_tinh_phi TEXT NOT NULL, ky_ngung_tinh_phi TEXT, ghi_chu TEXT,
 UNIQUE(hop_dong_id,khach_thue_id,ngay_vao)
);
CREATE INDEX ix_hop_dong_khach_trang_thai ON hop_dong(khach_dung_ten_id,trang_thai);
CREATE INDEX ix_nguoi_o_ghep_khach ON nguoi_o_ghep(khach_thue_id);
COMMIT;
