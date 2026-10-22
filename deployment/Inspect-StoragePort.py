"""Read-only deployment storage inventory. Prints counts, never keys or filenames."""
import json
from pathlib import Path
import sqlite3
import uuid
import sys
import hashlib


def main():
    root = Path(__file__).resolve().parent.parent
    app = root / 'QL_PhongTro'
    config = {}
    for name in ('appsettings.json', 'appsettings.Development.json'):
        file = app / name
        if file.is_file():
            config.update({k: v for k, v in json.loads(file.read_text(encoding='utf-8-sig')).items()
                           if k in ('DatabasePath', 'RoomImagesPath', 'IdentityImagePath') and isinstance(v, str)})
    for line in (root / '.env.local').read_text(encoding='utf-8-sig').splitlines():
        key, separator, value = line.partition('=')
        if separator and key.strip() in ('DatabasePath', 'RoomImagesPath', 'IdentityImagePath'):
            config[key.strip()] = value.strip().strip('\"\'')
    database = Path(config['DatabasePath'])
    database = (database if database.is_absolute() else root / database).resolve(strict=True)

    def configured_path(key, default):
        value = Path(config.get(key) or default)
        return (value if value.is_absolute() else app / value).resolve()

    roots = {
        'rooms': configured_path('RoomImagesPath', 'wwwroot/uploads/rooms'),
        'identity': configured_path('IdentityImagePath', 'App_Data/identity-images'),
        'repairs': Path(str(database) + '.repair-images'),
        'proofs': Path(str(database) + '.payment-proofs'),
        'bank_qr': Path(str(database) + '.bank-qr'),
        'avatars': Path(str(database) + '.avatars'),
        'contracts': Path(str(database) + '.contract-documents'),
    }
    references = {key: set() for key in roots}
    invalid = {key: 0 for key in roots}

    def reference(kind, key, prefix=''):
        if not key:
            return
        if prefix:
            if not key.startswith(prefix): invalid[kind] += 1; return
            key = key[len(prefix):]
        target = (roots[kind] / key).resolve()
        if not target.is_relative_to(roots[kind]): invalid[kind] += 1; return
        references[kind].add(target)

    with sqlite3.connect(database.as_uri() + '?mode=ro', uri=True) as connection:
        connection.execute('PRAGMA query_only=ON')
        connection.execute('BEGIN')
        for row in connection.execute('SELECT duong_dan,duong_dan_anh_nho FROM anh_phong'):
            for key in row: reference('rooms', key, '/uploads/rooms/')
        for row in connection.execute('SELECT anh_giay_to_truoc,anh_giay_to_sau FROM khach_thue'):
            for key in row: reference('identity', key)
        for kind, table, column in [('repairs', 'anh_bao_hong', 'khoa_luu'), ('proofs', 'thanh_toan', 'anh_chung_tu_key'), ('bank_qr', 'tai_khoan_ngan_hang', 'qr_key')]:
            for (key,) in connection.execute('SELECT "' + column + '" FROM "' + table + '"'): reference(kind, key)
        for (account,) in connection.execute('SELECT id FROM tai_khoan'):
            key = str(account) + '.png'
            if (roots['avatars'] / key).is_file(): reference('avatars', key)
        for (contract,) in connection.execute('SELECT id FROM hop_dong'):
            manifest = roots['contracts'] / str(contract) / 'manifest.json'
            if manifest.is_file():
                reference('contracts', str(contract) + '/manifest.json')
                content = json.loads(manifest.read_text(encoding='utf-8-sig'))
                for file in content.get('Files', []): reference('contracts', str(contract) + '/' + file['Id'])
    result = {}
    for kind, folder in roots.items():
        files = [file for file in folder.rglob('*') if file.is_file()] if folder.is_dir() else []
        result[kind] = {'files': len(files), 'bytes': sum(file.stat().st_size for file in files),
                        'referenced': len(references[kind]), 'missing': sum(not file.is_file() for file in references[kind]),
                        'invalid_references': invalid[kind]}
    output = root / 'data' / 'postgres-port' / ('storage-' + uuid.uuid4().hex[:8] + '.json')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    if '--manifest' in sys.argv:
        prefixes = {'bank_qr': 'bank-qr'}
        manifest = [{'Source': str(file), 'Key': prefixes.get(kind, kind) + '/' + file.relative_to(roots[kind]).as_posix(),
                     'Sha256': hashlib.sha256(file.read_bytes()).hexdigest().upper(), 'Bytes': file.stat().st_size}
                    for kind in roots for file in sorted(references[kind])]
        (output.parent / 'files-manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
    print(json.dumps({'storage': result, 'total_bytes': sum(group['bytes'] for group in result.values()), 'report': str(output)}, ensure_ascii=False))


if __name__ == '__main__': main()
