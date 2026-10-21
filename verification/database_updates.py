"""Run after dotnet build. Uses temporary copies only; never modifies the source DB."""
import hashlib
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
app = root / 'QL_PhongTro'
source = app / 'Data/local-dev.sqlite'
dll = app / 'bin/Debug/net10.0/QL_PhongTro.dll'

def run(db, command, success=True):
    env = dict(os.environ, DatabasePath=str(db))
    result = subprocess.run(['dotnet', str(dll), command], cwd=app, env=env,
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    assert (result.returncode == 0) == success, result.stdout + result.stderr
    return result.stdout + result.stderr

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

original = digest(source)
with tempfile.TemporaryDirectory(prefix='ql-schema-') as folder:
    db = Path(folder) / 'test.sqlite'
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as src, sqlite3.connect(db) as dst:
        src.backup(dst)
    src.close()
    dst.close()
    with sqlite3.connect(db) as c:
        # Simulate the old checkout while retaining the user's business rows.
        c.execute('DROP TABLE IF EXISTS app_schema_version')
        c.execute('DROP TABLE IF EXISTS role_permission')
        c.execute('DROP TABLE IF EXISTS app_module')
        c.execute('DROP TABLE IF EXISTS app_role')
        tables = [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table'")]
        before = {t: c.execute('SELECT * FROM "' + t + '"').fetchall() for t in tables}
        columns = {t: ','.join('"'+r[1]+'"' for r in c.execute('PRAGMA table_info("'+t+'")')) for t in tables}
    c.close()
    old_hash = digest(db)
    assert '--update-database' in run(db, '--check-database', False)
    assert digest(db) == old_hash, 'Read-only check changed database'
    run(db, '--update-database')
    run(db, '--check-database')
    with sqlite3.connect(db) as c:
        for table, rows in before.items():
            if table != 'sqlite_sequence':
                assert c.execute('SELECT '+columns[table]+' FROM "' + table + '"').fetchall() == rows, table
        assert c.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not c.execute('PRAGMA foreign_key_check').fetchall()
        # A revoked permission must remain revoked on repeat updates.
        c.execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='ADMIN'")
    c.close()
    updated_hash = digest(db)
    assert 'No changes' in run(db, '--update-database')
    assert digest(db) == updated_hash, 'Repeated update changed database'
    assert list(Path(folder).glob('test.sqlite.before-update-*.bak'))
    with sqlite3.connect(db) as c:
        c.execute('INSERT INTO app_schema_version VALUES (999, CURRENT_TIMESTAMP)')
    c.close()
    future_hash = digest(db)
    assert 'newer' in run(db, '--update-database', False)
    assert digest(db) == future_hash
    missing = Path(folder) / 'missing.sqlite'
    run(missing, '--update-database', False)
    assert not missing.exists()
assert digest(source) == original, 'Source database changed'
print('PASS: missing-schema check, backup, upgrade, row preservation, integrity, repeat update, revoked permissions, future version, missing file, source unchanged')
