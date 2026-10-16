"""Verify an additive upgrade on a NEW SQLite copy; never update the source."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import subprocess


def quote(name):
    return '"' + name.replace('"', '""') + '"'


def digest_rows(connection, table, columns):
    rows = connection.execute(
        f"SELECT {','.join(map(quote, columns))} FROM {quote(table)}"
    ).fetchall()
    encoded = sorted(json.dumps(row, ensure_ascii=False, default=lambda v: v.hex()) for row in rows)
    return len(rows), hashlib.sha256('\n'.join(encoded).encode()).hexdigest()


def snapshot(connection):
    result = {}
    for (table,) in connection.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"):
        if table == 'app_schema_version':
            continue
        columns = [row[1] for row in connection.execute(f'PRAGMA table_info({quote(table)})')]
        result[table] = (columns, digest_rows(connection, table, columns))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if output.exists() or source == output:
        raise SystemExit('Output must be a new path distinct from the source.')
    repo = Path(__file__).resolve().parents[1]
    app = repo / 'QL_PhongTro'
    runtime = app / 'bin/Debug/net10.0/QL_PhongTro.dll'
    if not runtime.exists():
        raise SystemExit('Build QL_PhongTro in Debug before running this verification.')
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    output.parent.mkdir(parents=True, exist_ok=True)
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as original:
        before = snapshot(original)
        with sqlite3.connect(output) as target:
            original.backup(target)
    env = dict(os.environ, DatabasePath=str(output), ASPNETCORE_ENVIRONMENT='Development')
    def run(flag):
        result = subprocess.run(['dotnet', str(runtime), flag], cwd=app, env=env,
                                capture_output=True, text=True, encoding='utf-8', errors='replace')
        with (output.parent / 'upgrade-cli.log').open('a', encoding='utf-8') as log:
            log.write(result.stdout + result.stderr)
        if result.returncode:
            raise RuntimeError(f'{flag} failed; see upgrade-cli.log')
    run('--update-database')
    run('--check-database')
    with sqlite3.connect(output) as target:
        for table, (columns, expected) in before.items():
            assert digest_rows(target, table, columns) == expected, f'Existing data changed: {table}'
        assert target.execute('PRAGMA integrity_check').fetchone() == ('ok',)
        assert not target.execute('PRAGMA foreign_key_check').fetchall()
        version = target.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0]
        assert version == 37, f'Unexpected schema version: {version}'
        after = snapshot(target)
    file_hash = hashlib.sha256(output.read_bytes()).hexdigest()
    run('--update-database')
    with sqlite3.connect(output) as target:
        assert snapshot(target) == after, 'Rerun changed data'
    assert hashlib.sha256(output.read_bytes()).hexdigest() == file_hash, 'Rerun changed database bytes'
    assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash, 'Source database changed'
    report = dict(schema_version=version, preserved_tables=len(before),
                  preserved_rows=sum(value[1][0] for value in before.values()),
                  integrity_check='ok', foreign_key_violations=0,
                  rerun_unchanged=True, source_unchanged=True)
    (output.parent / 'upgrade-result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
