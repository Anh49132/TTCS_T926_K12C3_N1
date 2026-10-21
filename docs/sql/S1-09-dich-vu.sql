-- Service schema AC1-AC4. Apply explicitly to an inspected, backed-up database.
-- Prerequisites: tai_khoan, toa_nha, phong_tro. Never run at application startup.
-- Existing tables with either of these names must be reviewed before applying.
CREATE TABLE dich_vu (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ma_dich_vu TEXT NOT NULL UNIQUE CHECK(length(trim(ma_dich_vu)) BETWEEN 1 AND 20),
    ten_dich_vu TEXT NOT NULL CHECK(length(trim(ten_dich_vu)) BETWEEN 1 AND 100),
    mo_ta TEXT,
    dang_hoat_dong INTEGER NOT NULL DEFAULT 1 CHECK(dang_hoat_dong IN (0,1))
);
CREATE TABLE cau_hinh_dich_vu (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    toa_nha_id INTEGER NOT NULL REFERENCES toa_nha(id) ON DELETE RESTRICT,
    phong_id INTEGER REFERENCES phong_tro(id) ON DELETE RESTRICT,
    dich_vu_id INTEGER NOT NULL REFERENCES dich_vu(id) ON DELETE RESTRICT,
    cach_tinh TEXT NOT NULL CHECK(cach_tinh IN ('THEO_CHI_SO','THEO_NGUOI','CO_DINH')),
    don_vi_tinh TEXT NOT NULL CHECK(length(trim(don_vi_tinh)) BETWEEN 1 AND 30),
    don_gia INTEGER NOT NULL CHECK(typeof(don_gia)='integer' AND don_gia >= 0),
    tu_ngay TEXT NOT NULL,
    den_ngay TEXT CHECK(den_ngay IS NULL OR den_ngay >= tu_ngay),
    dang_ap_dung INTEGER NOT NULL DEFAULT 1 CHECK(dang_ap_dung IN (0,1)),
    da_chot_gia INTEGER NOT NULL DEFAULT 1 CHECK(da_chot_gia IN (0,1)),
    nguoi_tao_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
    ngay_tao TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_dich_vu_toa_ngay ON cau_hinh_dich_vu(toa_nha_id,dich_vu_id,tu_ngay) WHERE phong_id IS NULL;
CREATE UNIQUE INDEX ux_dich_vu_phong_ngay ON cau_hinh_dich_vu(phong_id,dich_vu_id,tu_ngay) WHERE phong_id IS NOT NULL;
CREATE TABLE khoi_tao_dich_vu (
    toa_nha_id INTEGER NOT NULL PRIMARY KEY REFERENCES toa_nha(id) ON DELETE RESTRICT,
    ngay_tao TEXT NOT NULL
);
CREATE TRIGGER dich_vu_khong_chong_ngay_insert BEFORE INSERT ON cau_hinh_dich_vu
WHEN EXISTS (SELECT 1 FROM cau_hinh_dich_vu c WHERE c.toa_nha_id=NEW.toa_nha_id
    AND c.dich_vu_id=NEW.dich_vu_id AND c.phong_id IS NEW.phong_id
    AND c.tu_ngay<=COALESCE(NEW.den_ngay,'9999-12-31') AND NEW.tu_ngay<=COALESCE(c.den_ngay,'9999-12-31'))
BEGIN SELECT RAISE(ABORT,'Overlapping service price periods'); END;
CREATE TRIGGER dich_vu_khong_chong_ngay_update BEFORE UPDATE OF tu_ngay,den_ngay,toa_nha_id,phong_id,dich_vu_id ON cau_hinh_dich_vu
WHEN EXISTS (SELECT 1 FROM cau_hinh_dich_vu c WHERE c.id<>NEW.id AND c.toa_nha_id=NEW.toa_nha_id
    AND c.dich_vu_id=NEW.dich_vu_id AND c.phong_id IS NEW.phong_id
    AND c.tu_ngay<=COALESCE(NEW.den_ngay,'9999-12-31') AND NEW.tu_ngay<=COALESCE(c.den_ngay,'9999-12-31'))
BEGIN SELECT RAISE(ABORT,'Overlapping service price periods'); END;
