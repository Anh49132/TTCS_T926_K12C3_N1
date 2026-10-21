-- v8. Preserve selections/prices and issued invoice snapshots; no inferred history.
CREATE TABLE ngung_dich_vu_phong (
    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    dich_vu_phong_id INTEGER NOT NULL REFERENCES dich_vu_phong(id) ON DELETE RESTRICT,
    yeu_cau_luc_utc TEXT NOT NULL,
    ngung_tu_ky TEXT NOT NULL CHECK(substr(ngung_tu_ky,9,2)='01'),
    ap_dung_lai_tu_ky TEXT CHECK(ap_dung_lai_tu_ky IS NULL OR
        (substr(ap_dung_lai_tu_ky,9,2)='01' AND ap_dung_lai_tu_ky >= ngung_tu_ky)),
    ap_dung_lai_luc_utc TEXT,
    CHECK((ap_dung_lai_tu_ky IS NULL) = (ap_dung_lai_luc_utc IS NULL))
);
CREATE UNIQUE INDEX ux_ngung_dich_vu_phong_open ON ngung_dich_vu_phong(dich_vu_phong_id)
    WHERE ap_dung_lai_tu_ky IS NULL;
CREATE INDEX ix_ngung_dich_vu_phong_ky ON ngung_dich_vu_phong(dich_vu_phong_id,ngung_tu_ky);
INSERT INTO app_schema_version(version,applied_at) VALUES(8,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
