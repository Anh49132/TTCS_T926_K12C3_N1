"""S1-10 upgrade failure/legacy coverage; all writes stay in disposable copies."""
import hashlib
import os
from pathlib import Path
import sqlite3
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
SOURCE = APP / 'Data/local-dev.sqlite'
DLL = APP / 'bin/Debug/net10.0/QL_PhongTro.dll'


def run(db, command, success=True):
    result = subprocess.run(['dotnet', str(DLL), command], cwd=APP,
        env=dict(os.environ, DatabasePath=str(db)), capture_output=True,
        text=True, encoding='utf-8', errors='replace', timeout=60)
    assert (result.returncode == 0) == success, result.stdout + result.stderr
    return result.stdout + result.stderr


def copy(source, target):
    with sqlite3.connect(source.as_uri()+'?mode=ro',uri=True) as src, sqlite3.connect(target) as dst:
        src.backup(dst)
    src.close(); dst.close()


def query(db, sql):
    with sqlite3.connect(db) as c:
        result=c.execute(sql).fetchall()
    c.close()
    return result


def main():
    original=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    folder=ROOT/'data'/'S1-10-verification'/('schema-'+str(time.time_ns()))
    folder.mkdir(parents=True)
    base=folder/'base.sqlite'; copy(SOURCE,base); run(base,'--update-database')
    # Simulate v2 on copies only, retaining the original user/business rows.
    query(base,'DROP TABLE nhat_ky_hoat_dong')
    query(base,'DELETE FROM app_schema_version WHERE version=3')
    for name,setup in [
        ('bad-columns',"CREATE TABLE nhat_ky_hoat_dong(id INTEGER PRIMARY KEY, thoi_diem TEXT)"),
        ('partial-services',"CREATE TABLE dich_vu(id INTEGER PRIMARY KEY)")]:
        target=folder/(name+'.sqlite');copy(base,target);query(target,setup)
        before=query(target,"SELECT name,sql FROM sqlite_master ORDER BY name")
        run(target,'--update-database',False)
        assert query(target,'SELECT MAX(version) FROM app_schema_version')==[(2,)]
        assert query(target,"SELECT name,sql FROM sqlite_master ORDER BY name")==before
        assert list(folder.glob(name+'.sqlite.before-update-*.bak'))
    legacy=folder/'legacy.sqlite';copy(base,legacy)
    query(legacy,"""CREATE TABLE nhat_ky_hoat_dong(id INTEGER PRIMARY KEY AUTOINCREMENT,
        nguoi_thuc_hien_id INTEGER, vai_tro_luc_thuc_hien TEXT,loai_doi_tuong TEXT NOT NULL,
        doi_tuong_id INTEGER NOT NULL,hanh_dong TEXT NOT NULL,du_lieu_truoc TEXT,
        du_lieu_sau TEXT,ghi_chu TEXT,thoi_diem TEXT NOT NULL)""")
    query(legacy,"""INSERT INTO nhat_ky_hoat_dong(loai_doi_tuong,doi_tuong_id,hanh_dong,thoi_diem)
        VALUES('phong_tro',1,'TAO','2000-01-01 00:00:00')""")
    before=query(legacy,'SELECT * FROM nhat_ky_hoat_dong')
    run(legacy,'--update-database');run(legacy,'--check-database')
    assert query(legacy,'SELECT * FROM nhat_ky_hoat_dong')==[before[0]+(None,)]
    assert query(legacy,'PRAGMA integrity_check')==[('ok',)]
    assert not query(legacy,'PRAGMA foreign_key_check')
    # Same-name weakened trigger must fail validation, never silently mark an upgrade.
    target=folder/'bad-trigger.sqlite';copy(legacy,target)
    query(target,'DELETE FROM app_schema_version WHERE version=3')
    query(target,'DROP TRIGGER audit_no_delete')
    query(target,'CREATE TRIGGER audit_no_delete BEFORE DELETE ON nhat_ky_hoat_dong BEGIN SELECT 1; END')
    run(target,'--update-database',False)
    assert query(target,'SELECT MAX(version) FROM app_schema_version')==[(2,)]
    # Startup/check also fails if protection was removed from a v3 DB.
    query(legacy,'DROP TRIGGER audit_no_update')
    run(legacy,'--check-database',False)
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==original
    print('PASS S1-10 schema: failed updates preserve v2/schema; backups; legacy rows/name NULL; incompatible/missing triggers rejected; source unchanged')

if __name__=='__main__': main()
