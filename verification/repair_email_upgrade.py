"""Verify v41 using a fresh backup copy; never updates the source database."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import uuid

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
output = root / "data" / "repair-email-upgrade" / uuid.uuid4().hex
output.mkdir(parents=True)
target = output / "qa.sqlite"

def snapshot(db):
    result = {}
    for (table,) in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('app_schema_version','sqlite_sequence') ORDER BY name"):
        rows = db.execute('SELECT * FROM "' + table.replace('"', '""') + '"').fetchall()
        result[table] = {"count": len(rows), "sha256": hashlib.sha256(json.dumps(sorted(rows, key=repr), ensure_ascii=True, default=str).encode()).hexdigest()}
    return result

with sqlite3.connect(args.source.resolve().as_uri() + "?mode=ro", uri=True) as source, sqlite3.connect(target) as copy:
    source.backup(copy)
    before = snapshot(copy)

env = os.environ.copy()
env["DatabasePath"] = str(target)
command = ["dotnet", str(root / "QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll")]
for i in range(2):
    subprocess.run(command + ["--update-database"], env=env, cwd=root, check=True)
    subprocess.run(command + ["--check-database"], env=env, cwd=root, check=True)
    with sqlite3.connect(target) as db:
        after = snapshot(db)
        assert all(after.get(table) == rows for table, rows in before.items()), "Existing rows changed"
        assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
        assert not db.execute("PRAGMA foreign_key_check").fetchall()
        assert db.execute("SELECT MAX(version) FROM app_schema_version").fetchone()[0] == 41
        if i == 0:
            first = after
        else:
            assert after == first, "Repeated update changed rows"

(output / "report.json").write_text(json.dumps({"preserved_tables": len(before), "preserved_rows": sum(x["count"] for x in before.values()), "version": 41, "integrity": "ok", "foreign_keys": "ok", "idempotent": True}, indent=2))
print("Upgrade verified:", output)
