"""Read-only SQLite schema/data-count inventory for the PostgreSQL deployment.

No row values or credentials are exported. Reports stay in ignored data/.
This is an assessment, not an automatic schema converter or migration.
"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import sqlite3
import uuid


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    database = args.database
    if database is None:
        for line in (root / '.env.local').read_text(encoding='utf-8-sig').splitlines():
            if line.strip().startswith('DatabasePath='):
                database = Path(line.split('=', 1)[1].strip().strip('\"\''))
                break
    if database is None:
        parser.error('Cần --database hoặc DatabasePath trong .env.local.')
    if not database.is_absolute():
        database = root / database
    database = database.resolve(strict=True)
    before = digest(database)
    with sqlite3.connect(database.as_uri() + '?mode=ro', uri=True) as connection:
        connection.execute('PRAGMA query_only=ON')
        connection.execute('BEGIN')
        schema = connection.execute("SELECT type,name,tbl_name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name").fetchall()
        tables = []
        for kind, name, _, ddl in schema:
            if kind != 'table':
                continue
            quoted = '"' + name.replace('"', '""') + '"'
            tables.append({
                'name': name,
                'rows': connection.execute('SELECT count(*) FROM ' + quoted).fetchone()[0],
                'columns': connection.execute('PRAGMA table_info(' + quoted + ')').fetchall(),
                'foreign_keys': connection.execute('PRAGMA foreign_key_list(' + quoted + ')').fetchall(),
                'ddl': ddl,
            })
        integrity = connection.execute('PRAGMA integrity_check').fetchall()
        fk_errors = sum(1 for _ in connection.execute('PRAGMA foreign_key_check'))
        versions = connection.execute('SELECT max(version) FROM app_schema_version').fetchone()[0]
    # Keep only source locations, never source lines or configuration contents.
    patterns = {
        'sqlite_api': r'SqliteConnection|SqliteException|UseSqlite',
        'immediate_transaction': r'BeginTransaction\(deferred',
        'database_scoped_files': r'GetDbConnection\(\)\.DataSource',
        'sqlite_sql': r'PRAGMA|sqlite_master|last_insert_rowid|INSERT OR|strftime|julianday|AUTOINCREMENT',
    }
    locations = {key: [] for key in patterns}
    for file in sorted((root / 'QL_PhongTro').rglob('*.cs')):
        if {'bin', 'obj'} & set(file.relative_to(root).parts):
            continue
        for number, line in enumerate(file.read_text(encoding='utf-8-sig').splitlines(), 1):
            for label, pattern in patterns.items():
                if re.search(pattern, line):
                    locations[label].append({'file': file.relative_to(root).as_posix(), 'line': number})
    after = digest(database)
    report = {
        'schema_version': versions,
        'source_main_file_unchanged': before == after,
        'integrity_ok': integrity == [('ok',)],
        'foreign_key_violations': fk_errors,
        'tables': tables,
        'indexes': [{'name': name, 'table': table, 'ddl': sql} for kind, name, table, sql in schema if kind == 'index'],
        'triggers': [{'name': name, 'table': table, 'ddl': sql} for kind, name, table, sql in schema if kind == 'trigger'],
        'views': [{'name': name, 'ddl': sql} for kind, name, _, sql in schema if kind == 'view'],
        'source_locations': locations,
    }
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    output = root / 'data' / 'postgres-port' / (stamp + '-' + uuid.uuid4().hex[:8])
    output.mkdir(parents=True, exist_ok=False)
    (output / 'inventory.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({
        'report': str(output / 'inventory.json'),
        'tables': len(tables),
        'rows': sum(table['rows'] for table in tables),
        'triggers': len(report['triggers']),
        'views': len(report['views']),
        'schema_version': versions,
        'integrity_ok': report['integrity_ok'],
        'foreign_key_violations': fk_errors,
        'source_main_file_unchanged': before == after,
    }, ensure_ascii=False))
    if not report['integrity_ok'] or fk_errors or before != after:
        raise SystemExit('Không đạt kiểm tra nguồn; dừng trước khi chuyển dữ liệu.')


if __name__ == '__main__':
    main()
