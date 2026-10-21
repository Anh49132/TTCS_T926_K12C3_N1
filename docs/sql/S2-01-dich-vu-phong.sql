-- v6: selections have no price; the effective building price remains authoritative.
CREATE TABLE dich_vu_toa_nha (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    toa_nha_id INTEGER NOT NULL REFERENCES toa_nha(id) ON DELETE RESTRICT,
    dich_vu_id INTEGER NOT NULL REFERENCES dich_vu(id) ON DELETE RESTRICT,
    ap_dung_mac_dinh INTEGER NOT NULL DEFAULT 0 CHECK(ap_dung_mac_dinh IN (0,1)),
    UNIQUE(toa_nha_id,dich_vu_id)
);
CREATE TABLE dich_vu_phong (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE CASCADE,
    dich_vu_toa_nha_id INTEGER NOT NULL REFERENCES dich_vu_toa_nha(id) ON DELETE RESTRICT,
    UNIQUE(phong_id,dich_vu_toa_nha_id)
);
CREATE TRIGGER dich_vu_phong_cung_toa_insert BEFORE INSERT ON dich_vu_phong
WHEN (SELECT toa_nha_id FROM phong_tro WHERE id=NEW.phong_id) <>
     (SELECT toa_nha_id FROM dich_vu_toa_nha WHERE id=NEW.dich_vu_toa_nha_id)
BEGIN SELECT RAISE(ABORT,'Room and service must belong to the same building'); END;
CREATE TRIGGER dich_vu_phong_cung_toa_update BEFORE UPDATE ON dich_vu_phong
WHEN (SELECT toa_nha_id FROM phong_tro WHERE id=NEW.phong_id) <>
     (SELECT toa_nha_id FROM dich_vu_toa_nha WHERE id=NEW.dich_vu_toa_nha_id)
BEGIN SELECT RAISE(ABORT,'Room and service must belong to the same building'); END;
INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh)
SELECT DISTINCT c.toa_nha_id,c.dich_vu_id,
    CASE WHEN d.ma_dich_vu IN ('DIEN','NUOC','RAC','GUI_XE','INTERNET') THEN 1 ELSE 0 END
FROM cau_hinh_dich_vu c JOIN dich_vu d ON d.id=c.dich_vu_id WHERE c.phong_id IS NULL;
-- Existing rooms are deliberately not backfilled. No changes to invoice snapshots.
INSERT INTO app_schema_version(version,applied_at) VALUES(6,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
