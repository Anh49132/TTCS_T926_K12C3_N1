"""Create an isolated fake v31 demo for S4-07:04; source is read-only."""
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
    assert source_folder.parent == ROOT / 'data/s407' and source_folder.name.startswith('demo-')
    access = json.loads((source_folder / 'access.json').read_text(encoding='utf-8-sig'))
    source = Path(access['database']).resolve()
    assert source.parent == source_folder
    before = hashlib.sha256(source.read_bytes()).hexdigest()
    folder = ROOT / 'data/s407-filters' / ('demo-' + datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    path = folder / 'filters.sqlite'
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as origin, sqlite3.connect(path) as db:
        origin.backup(db)
    now = datetime.now(timezone.utc)
    month = now.astimezone(timezone(timedelta(hours=7))).strftime('%Y%m')
    ids = {}
    with sqlite3.connect(path) as db:
        db.execute('PRAGMA foreign_keys=ON')
        assert db.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0] == 31
        owner, manager = db.execute('SELECT chu_nha_id,quan_ly_id FROM toa_nha WHERE id=?', (access['building'],)).fetchone()
        reporter, profile = db.execute('SELECT nguoi_bao_id,khach_thue_id FROM bao_hong WHERE hop_dong_id=? LIMIT 1', (access['contract'],)).fetchone()
        foreign_manager = db.execute("SELECT id FROM tai_khoan WHERE email='manager.other.s407@demo.local'").fetchone()[0]
        buildings = {}
        for label, assigned in [('A', manager), ('B', manager), ('Trống', manager), ('Ngoài phạm vi', foreign_manager)]:
            buildings[label] = db.execute('INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(?,?,?,?) RETURNING id',
                                         (owner, assigned, 'Tòa ' + label + ' — demo lọc', 'Địa chỉ giả')).fetchone()[0]
        contracts = {}
        rooms = {}
        for label in ['A', 'B', 'Ngoài phạm vi']:
            rooms[label] = db.execute('''INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
                VALUES(?,'101',1,25,3000000,3,'DANG_THUE',?) RETURNING id''', (buildings[label], now.isoformat())).fetchone()[0]
            contracts[label] = db.execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES(?,?,?,'DANG_HIEU_LUC') RETURNING id",
                                         ('HD-FILTER-' + uuid.uuid4().hex[:12], rooms[label], profile)).fetchone()[0]
            today = now.astimezone(timezone(timedelta(hours=7))).date()
            db.execute('INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(?,?,?,3000000)',
                       (contracts[label], str(today - timedelta(days=2)), str(today + timedelta(days=90))))
        counter = db.execute('SELECT so_cuoi FROM bao_hong_so_ma WHERE thang=?', (month,)).fetchone()
        last = counter[0] if counter else 0
        cases = [('a_urgent', 'A', 'MOI', 'GAP'), ('a_normal', 'A', 'MOI', 'THUONG'),
                 ('a_progress', 'A', 'DANG_XU_LY', 'GAP'), ('a_done', 'A', 'HOAN_THANH', 'THUONG'),
                 ('a_rejected', 'A', 'TU_CHOI', 'GAP'), ('b_urgent', 'B', 'MOI', 'GAP'),
                 ('foreign', 'Ngoài phạm vi', 'MOI', 'GAP')]
        for key, label, state, priority in cases:
            last += 1
            assert last <= 9999
            ids[key] = db.execute('''INSERT INTO bao_hong(ma_bao_hong,nguoi_bao_id,khach_thue_id,phong_id,hop_dong_id,
                loai_thiet_bi,mo_ta,muc_do,trang_thai,ngay_tao,khoa_chong_trung) VALUES(?,?,?,?,?,?,?,?,?,?,?) RETURNING id''',
                (f'BH-{month}-{last:04d}', reporter, profile, rooms[label], contracts[label], 'Vòi nước — demo bộ lọc',
                 f'Dữ liệu giả S4-07:04: {key}', priority, state, (now - timedelta(hours=26)).strftime('%Y-%m-%d %H:%M:%S.%f'), uuid.uuid4().hex)).fetchone()[0]
        db.execute('INSERT INTO bao_hong_so_ma(thang,so_cuoi) VALUES(?,?) ON CONFLICT(thang) DO UPDATE SET so_cuoi=excluded.so_cuoi', (month, last))
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    images = folder / 'room-images'
    if Path(access['roomImagesPath']).is_dir():
        shutil.copytree(access['roomImagesPath'], images)
    if Path(str(source) + '.repair-images').is_dir():
        shutil.copytree(Path(str(source) + '.repair-images'), Path(str(path) + '.repair-images'))
    access.update(directory=str(folder), database=str(path), url='http://localhost:5292',
                  runtime=str(ROOT / 'QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll'), roomImagesPath=str(images),
                  filter_buildings=buildings, filter_cases=ids)
    (folder / 'access.json').write_text(json.dumps(access, ensure_ascii=False, indent=2), encoding='utf-8')
    assert hashlib.sha256(source.read_bytes()).hexdigest() == before
    (folder / 'source-check.json').write_text(json.dumps({'source_sha256': before, 'source_unchanged': True}), encoding='utf-8')
    (folder.parent / 'latest.txt').write_text(str(folder), encoding='utf-8')
    print(folder)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source_folder', type=Path)
    prepare(parser.parse_args().source_folder)
