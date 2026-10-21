"""Import user-confirmed local demo assets; no schema or existing-image changes."""
import io
import argparse
import json
import random
import sqlite3
import uuid
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--all-empty-rooms', action='store_true', help='Assign one random sample per empty room; do not modify identity profiles')
args = parser.parse_args()
SOURCE = Path('C:/Users/cter4/OneDrive/Pictures/Ảnh Mẫu')
config = dict(line.split('=', 1) for line in (ROOT / '.env.local').read_text(encoding='utf-8-sig').splitlines()
              if line.startswith(('DatabasePath=', 'RoomImagesPath=', 'IdentityImagePath=')))
database = Path(config['DatabasePath']).resolve()
rooms_root = Path(config['RoomImagesPath']).resolve()
identity_root = Path(config.get('IdentityImagePath', str(ROOT / 'QL_PhongTro/App_Data/identity-images'))).resolve()
for target in (database, rooms_root, identity_root):
    if not target.is_relative_to(ROOT):
        raise RuntimeError('Import target must stay inside workspace')

def prepare(path, width):
    raw = path.read_bytes()
    if not 0 < len(raw) <= 5 * 1024 * 1024:
        raise ValueError('Invalid image size')
    with Image.open(io.BytesIO(raw)) as original:
        if original.format != 'JPEG':
            raise ValueError('Expected JPEG')
        image = ImageOps.exif_transpose(original).convert('RGB')
        if image.width > width:
            image = image.resize((width, round(image.height * width / image.width)), Image.Resampling.LANCZOS)
        output = io.BytesIO()
        image.save(output, 'JPEG', quality=85)
    return raw, output.getvalue()

samples = [prepare(SOURCE / 'Ảnh trọ mẫu' / f'Ảnh trọ {i}.{ "jpeg" if i == 9 else "jpg"}', 400) for i in range(1, 11)]
identity = [] if args.all_empty_rooms else [prepare(SOURCE / 'Ảnh CCCD' / name, 1600)[1] for name in ('Front.jpg', 'Back.jpg')]
conn = sqlite3.connect(database, timeout=10)
conn.execute('PRAGMA foreign_keys=ON')
stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]
backup = database.with_name(database.name + '.before-sample-images-' + stamp + '.bak')
with sqlite3.connect(backup) as destination:
    conn.backup(destination)
created = []
def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as file:
        file.write(data)
    created.append(path)

try:
    conn.execute('BEGIN IMMEDIATE')
    rooms = conn.execute("SELECT p.id,p.ma_phong FROM phong_tro p WHERE NOT EXISTS(SELECT 1 FROM anh_phong a WHERE a.phong_id=p.id) ORDER BY p.id" + ('' if args.all_empty_rooms else ' LIMIT 10')).fetchall()
    clients = [] if args.all_empty_rooms else conn.execute("SELECT k.id,k.ho_ten FROM khach_thue k JOIN tai_khoan t ON t.id=k.tai_khoan_id WHERE t.email IN ('tenant2.demo@demo.local','tenant3.demo@demo.local') AND k.anh_giay_to_truoc IS NULL AND k.anh_giay_to_sau IS NULL ORDER BY k.id").fetchall()
    if not args.all_empty_rooms and (len(rooms) != 10 or len(clients) != 2):
        raise RuntimeError('Expected ten empty rooms and two empty confirmed demo profiles; refusing import')
    now = datetime.now(timezone.utc).isoformat()
    assignments = [random.SystemRandom().choice(samples) for _ in rooms] if args.all_empty_rooms else samples
    for (room_id, _), (original, thumbnail) in zip(rooms, assignments):
        name = uuid.uuid4().hex
        save(rooms_root / str(room_id) / (name + '.jpg'), original)
        save(rooms_root / str(room_id) / (name + '-thumb.jpg'), thumbnail)
        conn.execute('INSERT INTO anh_phong(phong_id,duong_dan,duong_dan_anh_nho,thu_tu,mo_ta,ngay_tao) VALUES(?,?,?,1,?,?)',
                     (room_id, f'/uploads/rooms/{room_id}/{name}.jpg', f'/uploads/rooms/{room_id}/{name}-thumb.jpg', 'Ảnh phòng mẫu do người dùng bổ sung', now))
    for client_id, _ in clients:
        names = []
        for data in identity:
            name = uuid.uuid4().hex + '.jpg'
            save(identity_root / name, data)
            names.append(name)
        conn.execute('UPDATE khach_thue SET anh_giay_to_truoc=?,anh_giay_to_sau=? WHERE id=?', (*names, client_id))
    assert conn.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert not conn.execute('PRAGMA foreign_key_check').fetchall()
    conn.commit()
except Exception:
    conn.rollback()
    for path in created:
        path.unlink(missing_ok=True)
    raise
finally:
    conn.close()
print(json.dumps({'backup': str(backup), 'rooms': rooms, 'profiles': clients, 'created_files': len(created)}, ensure_ascii=True))
