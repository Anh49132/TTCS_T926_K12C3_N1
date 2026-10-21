"""Verify v20 on a NEW copy of a stopped, isolated S3-06 demo; preserve all old rows."""
import hashlib
import json
import sqlite3
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2]).resolve()
assert source.is_relative_to(root / "data/s306-demo") and target.is_relative_to(root / "data/s306-demo")
assert not target.exists(), "Refusing to overwrite a demo copy"
access = json.loads((source / "access.json").read_text(encoding="utf-8-sig"))
source_db = Path(access["database"]).resolve()
assert source_db.is_relative_to(source)
target.mkdir(parents=True)
database = target / "s306-v20.sqlite"
with sqlite3.connect(source_db.as_uri() + "?mode=ro", uri=True) as original, sqlite3.connect(database) as copy:
    assert original.execute("SELECT MAX(version) FROM app_schema_version").fetchone()[0] == 19
    original.backup(copy)

def snapshot(include_versions=False):
    result = {}
    with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True) as db:
        tables = [x[0] for x in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
        for table in tables:
            if table == "app_schema_version" and not include_versions:
                continue
            columns = [x[1] for x in db.execute(f'PRAGMA table_info("{table}")') if x[1] != "ngay_phat_hanh_nghiep_vu"]
            result[table] = list(db.execute('SELECT '+','.join('"'+x+'"' for x in columns)+' FROM "'+table+'" ORDER BY rowid'))
    return hashlib.sha256(json.dumps(result, sort_keys=True, ensure_ascii=True).encode()).hexdigest()

runtime = root / "QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll"
command = ["dotnet", str(runtime), "--contentRoot",str(root / "QL_PhongTro"),"--environment","Development","--DatabasePath",str(database)]
before = snapshot()
subprocess.run(command + ["--update-database"], check=True)
assert before == snapshot(), "Old business rows changed"
with sqlite3.connect(database.as_uri()+"?mode=ro",uri=True) as db:
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert not db.execute("PRAGMA foreign_key_check").fetchall()
    assert db.execute("SELECT MAX(version) FROM app_schema_version").fetchone()[0] == 20
    assert not db.execute("SELECT 1 FROM hoa_don WHERE ngay_phat_hanh_nghiep_vu IS NOT NULL").fetchone()
first = snapshot(True)
subprocess.run(command + ["--update-database"], check=True)
assert first == snapshot(True), "Repeated updater changed rows"
subprocess.run(command + ["--check-database"], check=True)
assert list(target.glob("*.before-update-*.bak")), "Updater backup missing"
access.update(database=str(database),directory=str(target),runtime=str(runtime))
(target / "access.json").write_text(json.dumps(access,ensure_ascii=False,indent=2),encoding="utf-8")
(target / "upgrade-checks.json").write_text(json.dumps({"sourceVersion":19,"targetVersion":20,"oldRowsPreserved":True,"backupCreated":True,"integrity":"ok","foreignKeyErrors":0,"repeatUnchanged":True},indent=2),encoding="utf-8")
print("PASS: v19 -> v20 on a new copy; all old rows preserved, backup/integrity/FK/repeat verified")
