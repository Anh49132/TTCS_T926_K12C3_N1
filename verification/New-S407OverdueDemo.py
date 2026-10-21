"""Clone the existing fake S4-07 demo and add isolated overdue examples.

Never upgrades, resets, seeds or starts the source database. Credentials stay ignored.
Run after building the application and stopping any app using the source demo.
"""
import argparse
import hashlib
import json
import shutil
import sqlite3
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def prepare(source_folder):
    source_folder = source_folder.resolve()
    assert source_folder.parent == ROOT / 'data/s407' and source_folder.name.startswith('demo-'), 'Only the existing fake S407 demo is supported'
    access = json.loads((source_folder / 'access.json').read_text(encoding='utf-8-sig'))
    source = Path(access['database']).resolve()
    assert source.parent == source_folder and source.is_file()
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    folder = ROOT / 'data/s407-overdue' / ('demo-' + datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    path = folder / 'overdue.sqlite'
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as origin, sqlite3.connect(path) as db:
        origin.backup(db)
    now = datetime.now(timezone.utc)
    month = now.astimezone(timezone(timedelta(hours=7))).strftime('%Y%m')
    cases = [('urgent_26h', 'GAP', 'MOI', 26), ('ordinary_26h', 'THUONG', 'MOI', 26),
             ('urgent_23h', 'GAP', 'MOI', 23), ('completed_26h', 'GAP', 'HOAN_THANH', 26),
             ('in_progress_26h', 'GAP', 'DANG_XU_LY', 26), ('rejected_26h', 'GAP', 'TU_CHOI', 26)]
    ids = {}
    with sqlite3.connect(path) as db:
        db.execute('PRAGMA foreign_keys=ON')
        assert db.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0] == 31, 'Schema31 required; no automatic upgrade'
        template = db.execute('''SELECT b.nguoi_bao_id,b.khach_thue_id,b.phong_id,b.hop_dong_id
            FROM bao_hong b JOIN phong_tro p ON p.id=b.phong_id JOIN toa_nha t ON t.id=p.toa_nha_id
            WHERE t.quan_ly_id IS NOT NULL ORDER BY b.id LIMIT 1''').fetchone()
        assert template, 'Fake source must have an assigned report'
        counter = db.execute('SELECT so_cuoi FROM bao_hong_so_ma WHERE thang=?', (month,)).fetchone()
        last = counter[0] if counter else 0
        for key, level, status, hours in cases:
            last += 1
            assert last <= 9999
            code = f'BH-{month}-{last:04d}'
            ids[key] = db.execute('''INSERT INTO bao_hong(ma_bao_hong,nguoi_bao_id,khach_thue_id,phong_id,hop_dong_id,
                loai_thiet_bi,mo_ta,muc_do,trang_thai,ngay_tao,khoa_chong_trung)
                VALUES(?,?,?,?,?,?,?,?,?,?,?) RETURNING id''',
                (code, *template, 'Vòi nước — demo cảnh báo', f'Dữ liệu giả S4-07:03: {key}', level, status,
                 (now - timedelta(hours=hours)).strftime('%Y-%m-%d %H:%M:%S.%f'), uuid.uuid4().hex)).fetchone()[0]
        db.execute('INSERT INTO bao_hong_so_ma(thang,so_cuoi) VALUES(?,?) ON CONFLICT(thang) DO UPDATE SET so_cuoi=excluded.so_cuoi', (month, last))
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    images = folder / 'room-images'
    if Path(access['roomImagesPath']).is_dir():
        shutil.copytree(access['roomImagesPath'], images)
    # Private repair images live beside the source database; copy only into the fake demo.
    repair_images = Path(str(source) + '.repair-images')
    if repair_images.is_dir():
        shutil.copytree(repair_images, Path(str(path) + '.repair-images'))
    access.update(directory=str(folder), database=str(path), url='http://localhost:5291',
                  runtime=str(ROOT / 'QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll'),
                  roomImagesPath=str(images), overdue_cases=ids, overdue_created_utc=now.isoformat())
    (folder / 'access.json').write_text(json.dumps(access, ensure_ascii=False, indent=2), encoding='utf-8')
    (folder / 'source-check.json').write_text(json.dumps({'source_sha256': before, 'source_unchanged': hashlib.sha256(source.read_bytes()).hexdigest() == before}), encoding='utf-8')
    assert hashlib.sha256(source.read_bytes()).hexdigest() == before
    (folder.parent / 'latest.txt').write_text(str(folder), encoding='utf-8')
    print(folder)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source_folder', type=Path)
    prepare(parser.parse_args().source_folder)
