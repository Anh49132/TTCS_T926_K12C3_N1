"""Verify additive S3-01 v14 -> v15 upgrade on a NEW SQLite backup, never the source."""
import argparse
import sqlite3
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--source', required=True)
parser.add_argument('--copy', required=True)
parser.add_argument('--runtime', required=True)
parser.add_argument('--app', required=True)
args = parser.parse_args()
source, target = Path(args.source).resolve(), Path(args.copy).resolve()
if target.exists() or source == target:
    raise SystemExit('Copy target must be new and distinct from source.')
target.parent.mkdir(parents=True, exist_ok=True)
with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as original:
    if original.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0] != 14:
        raise SystemExit('This preservation check expects a v14 source.')
    with sqlite3.connect(target) as copy:
        original.backup(copy)

def snapshot():
    with sqlite3.connect(target) as db:
        names = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> 'app_schema_version'")]
        return {name: db.execute('SELECT * FROM "' + name.replace('"', '""') + '"').fetchall() for name in names}

before = snapshot()
command = ['dotnet', str(Path(args.runtime).resolve()), '--DatabasePath=' + str(target), '--update-database']
for _ in range(2):
    subprocess.run(command, cwd=Path(args.app).resolve(), check=True)
    after = snapshot()
    assert all(after[name] == rows for name, rows in before.items()), 'Existing business rows changed.'
with sqlite3.connect(target) as db:
    assert db.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0] == 15
    assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
print('PASS: v14 -> v15; all existing tables preserved; repeat update; integrity/FK. Source unchanged.')
