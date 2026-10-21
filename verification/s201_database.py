"""Verify v8 on a read-only backup of local SQLite; never write to the source."""
import hashlib
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
app = root / "QL_PhongTro"
source = Path(os.environ.get("DatabasePath", app / "Data/local-dev.sqlite")).resolve()
dll = app / "bin/Debug/net10.0/QL_PhongTro.dll"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(path, flag, success=True):
    result = subprocess.run(
        ["dotnet", str(dll), flag], cwd=app,
        env=dict(os.environ, DatabasePath=str(path)), capture_output=True,
        text=True, encoding="utf-8", errors="replace",
    )
    assert (result.returncode == 0) == success, result.stdout + result.stderr


original = digest(source)
with tempfile.TemporaryDirectory(prefix="s201-upgrade-") as folder:
    copy = Path(folder) / "copy.sqlite"
    with sqlite3.connect(source.as_uri() + "?mode=ro", uri=True) as src:
        with sqlite3.connect(copy) as dst:
            src.backup(dst)
        dst.close()
    src.close()
    with sqlite3.connect(copy) as db:
        tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('sqlite_sequence','app_schema_version')")]
        columns = {table: [r[1] for r in db.execute('PRAGMA table_info("' + table + '")')] for table in tables}
        rows = {table: db.execute('SELECT * FROM "' + table + '"').fetchall() for table in tables}
    db.close()
    run(copy, "--update-database")
    run(copy, "--check-database")
    with sqlite3.connect(copy) as db:
        assert db.execute("SELECT MAX(version) FROM app_schema_version").fetchone() == (8,)
        for table in tables:
            selected_columns = ','.join('"' + column.replace('"', '""') + '"' for column in columns[table])
            assert db.execute('SELECT ' + selected_columns + ' FROM "' + table + '"').fetchall() == rows[table], table
        room_service_columns = {row[1] for row in db.execute('PRAGMA table_info("dich_vu_phong")')}
        assert "don_gia_rieng" in room_service_columns
        assert db.execute("PRAGMA integrity_check").fetchone() == ("ok",)
        assert not db.execute("PRAGMA foreign_key_check").fetchall()
    db.close()
    upgraded = digest(copy)
    run(copy, "--update-database")
    assert digest(copy) == upgraded
    assert list(Path(folder).glob("*.before-update-*.bak"))
    fresh = Path(folder) / "fresh.sqlite"
    run(fresh, "--check-database", False)
    assert not fresh.exists()
    run(fresh, "--initialize-database")
    run(fresh, "--check-database")
    with sqlite3.connect(fresh) as db:
        assert db.execute("SELECT COUNT(*) FROM tai_khoan").fetchone() == (0,)
        assert db.execute("SELECT COUNT(*) FROM app_role").fetchone() == (4,)
    db.close()
    initialized = digest(fresh)
    run(fresh, "--initialize-database", False)
    assert digest(fresh) == initialized
assert digest(source) == original, "Source changed"
print("PASS: v8 backup, data preservation, integrity/FK, repeat update, new initialization, no overwrite, source unchanged")
