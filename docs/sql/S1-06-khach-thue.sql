-- Apply explicitly to an existing database only after approval and backup.
-- Intentionally fails if the table already exists: inspect that schema first.
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;
CREATE TABLE khach_thue (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    tai_khoan_id INTEGER UNIQUE REFERENCES tai_khoan(id) ON DELETE RESTRICT,
    ho_ten TEXT NOT NULL,
    ngay_sinh TEXT,
    so_giay_to TEXT,
    giay_to_bon_so_cuoi TEXT,
    anh_giay_to_truoc TEXT,
    anh_giay_to_sau TEXT,
    dia_chi_thuong_tru TEXT,
    nghe_nghiep TEXT,
    so_dien_thoai TEXT,
    email TEXT,
    lien_he_khan_cap TEXT,
    ngay_tao TEXT NOT NULL
);
COMMIT;
