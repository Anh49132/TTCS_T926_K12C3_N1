-- v7: store an optional unit-price override on each room/service selection.
ALTER TABLE dich_vu_phong
    ADD COLUMN don_gia_rieng INTEGER NULL
    CHECK (don_gia_rieng IS NULL OR don_gia_rieng >= 0);
INSERT INTO app_schema_version(version,applied_at)
VALUES(7,strftime('%Y-%m-%dT%H:%M:%fZ','now'));