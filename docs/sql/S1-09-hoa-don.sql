-- Explicit installation only; requires existing hop_dong, ky_hop_dong and service schema.
-- No modifications to existing contracts. Contract service snapshots are an additive relation.
CREATE TABLE hop_dong_dich_vu (
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
 dich_vu_id INTEGER NOT NULL REFERENCES dich_vu(id) ON DELETE RESTRICT,
 cau_hinh_dich_vu_id INTEGER NOT NULL REFERENCES cau_hinh_dich_vu(id) ON DELETE RESTRICT,
 ten_dich_vu TEXT NOT NULL, cach_tinh TEXT NOT NULL, don_vi_tinh TEXT NOT NULL,
 don_gia INTEGER NOT NULL CHECK(don_gia>=0), ngay_ghi_nhan TEXT NOT NULL,
 UNIQUE(hop_dong_id,dich_vu_id)
);
CREATE TABLE hoa_don (
 id INTEGER PRIMARY KEY AUTOINCREMENT, ma_hoa_don TEXT NOT NULL UNIQUE,
 hop_dong_id INTEGER NOT NULL REFERENCES hop_dong(id) ON DELETE RESTRICT,
 thang INTEGER NOT NULL CHECK(thang BETWEEN 1 AND 12), nam INTEGER NOT NULL,
 tu_ngay TEXT NOT NULL, den_ngay TEXT NOT NULL CHECK(den_ngay>=tu_ngay), ngay_chot TEXT NOT NULL,
 so_nguoi_tinh_phi INTEGER NOT NULL CHECK(so_nguoi_tinh_phi>0), loai_hoa_don TEXT NOT NULL DEFAULT 'DINH_KY',
 ngay_lap TEXT NOT NULL, ngay_phat_hanh TEXT, han_thanh_toan TEXT NOT NULL,
 tong_tien INTEGER NOT NULL DEFAULT 0 CHECK(tong_tien>=0), trang_thai TEXT NOT NULL DEFAULT 'NHAP',
 thay_the_hoa_don_id INTEGER UNIQUE REFERENCES hoa_don(id),
 nguoi_lap_id INTEGER NOT NULL REFERENCES tai_khoan(id), nguoi_phat_hanh_id INTEGER REFERENCES tai_khoan(id),
 nguoi_huy_id INTEGER REFERENCES tai_khoan(id), ngay_huy TEXT, ly_do_huy TEXT, phien_ban INTEGER NOT NULL DEFAULT 0, ghi_chu TEXT
);
CREATE UNIQUE INDEX ux_hoa_don_hop_dong_thang ON hoa_don(hop_dong_id,nam,thang) WHERE trang_thai<>'DA_HUY';
CREATE TABLE chi_tiet_hoa_don (
 id INTEGER PRIMARY KEY AUTOINCREMENT, hoa_don_id INTEGER NOT NULL REFERENCES hoa_don(id) ON DELETE RESTRICT,
 so_thu_tu INTEGER NOT NULL, dich_vu_id INTEGER REFERENCES dich_vu(id) ON DELETE RESTRICT,
 cau_hinh_dich_vu_id INTEGER REFERENCES cau_hinh_dich_vu(id) ON DELETE RESTRICT,
 ky_hop_dong_id INTEGER REFERENCES ky_hop_dong(id) ON DELETE RESTRICT,
 -- Optional integration IDs are reserved; their modules/tables do not exist in this branch.
 chi_so_id INTEGER, bao_hong_id INTEGER,
 loai_khoan TEXT NOT NULL, ten_khoan TEXT NOT NULL, cach_tinh_ap_dung TEXT, don_vi_tinh TEXT,
 so_luong TEXT NOT NULL, don_gia INTEGER NOT NULL CHECK(don_gia>=0), chi_so_dau TEXT, chi_so_cuoi TEXT,
 so_ngay_tinh_tien INTEGER, so_ngay_trong_thang INTEGER,
 thanh_tien INTEGER NOT NULL CHECK(thanh_tien>=0), ghi_chu TEXT,
 UNIQUE(hoa_don_id,so_thu_tu)
);
CREATE TRIGGER khoa_dong_hoa_don_insert BEFORE INSERT ON chi_tiet_hoa_don
WHEN (SELECT trang_thai FROM hoa_don WHERE id=NEW.hoa_don_id)<>'NHAP'
BEGIN SELECT RAISE(ABORT,'Issued invoice lines are immutable'); END;
CREATE TRIGGER khoa_dong_hoa_don_update BEFORE UPDATE ON chi_tiet_hoa_don
WHEN (SELECT trang_thai FROM hoa_don WHERE id=OLD.hoa_don_id)<>'NHAP'
 OR (SELECT trang_thai FROM hoa_don WHERE id=NEW.hoa_don_id)<>'NHAP'
BEGIN SELECT RAISE(ABORT,'Issued invoice lines are immutable'); END;
CREATE TRIGGER khoa_dong_hoa_don_delete BEFORE DELETE ON chi_tiet_hoa_don
WHEN (SELECT trang_thai FROM hoa_don WHERE id=OLD.hoa_don_id)<>'NHAP'
BEGIN SELECT RAISE(ABORT,'Issued invoice lines are immutable'); END;
CREATE TRIGGER khoa_hoa_don_update BEFORE UPDATE ON hoa_don WHEN OLD.trang_thai<>'NHAP'
BEGIN SELECT RAISE(ABORT,'Issued invoice is immutable'); END;
CREATE TRIGGER khoa_hoa_don_delete BEFORE DELETE ON hoa_don WHEN OLD.trang_thai<>'NHAP'
BEGIN SELECT RAISE(ABORT,'Issued invoice is immutable'); END;
