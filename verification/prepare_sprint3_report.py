"""Enrich the existing 50-room Sprint 3 demo without deleting data or changing schema.
Run on a backup copy first. Requires Pillow and bcrypt already present locally.
"""
import argparse, calendar, hashlib, io, json, secrets, sqlite3, uuid
from datetime import datetime, timezone
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from PIL import Image, ImageOps
import bcrypt

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--database', type=Path, required=True)
parser.add_argument('--artifacts', type=Path, required=True)
parser.add_argument('--room-images', type=Path, required=True)
parser.add_argument('--identity-images', type=Path, required=True)
parser.add_argument('--source', type=Path, required=True)
args = parser.parse_args()
for name in ('database','artifacts','room_images','identity_images'):
    value = getattr(args,name).resolve()
    if not value.is_relative_to(ROOT):
        raise ValueError('All write targets must be within the workspace.')
    setattr(args,name,value)
if not args.database.is_file():
    raise ValueError('Existing database required; refusing to create an empty database.')
if (args.artifacts/'access.json').exists():
    raise ValueError('Use a new artifacts directory; never overwrite an existing credential manifest.')
args.artifacts.mkdir(parents=True,exist_ok=True)
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:6]
now = datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')

def jpeg(path, width):
    if not path.is_file() or not 0<path.stat().st_size<=5*1024*1024:
        raise ValueError('Missing or oversize image: '+str(path))
    with Image.open(path) as source:
        if source.format not in ('JPEG','PNG'): raise ValueError('Invalid image')
        image=ImageOps.exif_transpose(source).convert('RGB')
        image.thumbnail((width,width),Image.Resampling.LANCZOS)
        output=io.BytesIO()
        image.save(output,'JPEG',quality=88)
        return output.getvalue()

room_samples=[(jpeg(args.source/'Ảnh trọ mẫu'/f'Ảnh trọ {i}.{"jpeg" if i==9 else "jpg"}',1600),
               jpeg(args.source/'Ảnh trọ mẫu'/f'Ảnh trọ {i}.{"jpeg" if i==9 else "jpg"}',400)) for i in range(1,11)]
identity_samples=[jpeg(args.source/'Ảnh CCCD'/side,1600) for side in ('Front.jpg','Back.jpg')]
connection=sqlite3.connect(args.database,timeout=20)
connection.row_factory=sqlite3.Row
connection.execute('PRAGMA foreign_keys=ON')
backup=args.artifacts/('before-'+stamp+'.sqlite')
with sqlite3.connect(backup) as target: connection.backup(target)
# Capture every pre-existing row, including credentials, for preservation verification.
before={r[0]:list(connection.execute('SELECT * FROM "'+r[0]+'" ORDER BY rowid'))
        for r in connection.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")}
schema_before=list(connection.execute("SELECT type,name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name"))
created=[]
def save(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    with path.open('xb') as file:file.write(data)
    created.append(path)
def insert(table,data):
    keys=list(data)
    return connection.execute('INSERT INTO '+table+'('+','.join(keys)+') VALUES('+','.join('?' for _ in keys)+')',
                              [data[key] for key in keys]).lastrowid
def fingerprint(rows):
    return hashlib.sha256(json.dumps([list(r) for r in rows],ensure_ascii=False,default=str).encode()).hexdigest()

try:
    connection.execute('BEGIN IMMEDIATE')
    building=connection.execute("SELECT * FROM toa_nha WHERE ten_toa_nha='Sprint 3 – Phát hành 50 phòng'").fetchone()
    if not building:raise ValueError('Expected existing Sprint 3 building.')
    rooms=list(connection.execute('SELECT * FROM phong_tro WHERE toa_nha_id=? ORDER BY ma_phong',(building['id'],)))
    if len(rooms)!=50:raise ValueError('Expected exactly 50 rooms in the demo building.')
    password=secrets.token_urlsafe(16)
    password_hash=bcrypt.hashpw(password.encode(),bcrypt.gensalt(rounds=12)).decode()
    access=[]
    changed_profiles=set()
    for index,room in enumerate(rooms,1):
        contract=connection.execute("SELECT * FROM hop_dong WHERE phong_id=? AND trang_thai='DANG_HIEU_LUC'",(room['id'],)).fetchone()
        if not contract:raise ValueError('Expected a committed demo contract per room.')
        tenant=connection.execute('SELECT * FROM khach_thue WHERE id=?',(contract['khach_dung_ten_id'],)).fetchone()
        if tenant['tai_khoan_id'] is None:
            email=f'sprint3.b{index:03}@demo.invalid'
            phone=f'089730{index:04}'
            if connection.execute('SELECT 1 FROM tai_khoan WHERE email=? OR so_dien_thoai=?',(email,phone)).fetchone():
                raise ValueError('Generated account collides with an existing account.')
            account=insert('tai_khoan',dict(ho_ten=tenant['ho_ten'],email=email,so_dien_thoai=phone,mat_khau=password_hash,
                vai_tro='KHACH_THUE',dang_hoat_dong=1,is_staff=0,is_superuser=0,ngay_tao=now,ngay_cap_nhat=now,email_confirmed=1))
            document=f'099900{index:06}'
            if connection.execute('SELECT 1 FROM khach_thue WHERE so_giay_to=?',(document,)).fetchone():
                raise ValueError('Synthetic identity collision.')
            connection.execute("""UPDATE khach_thue SET tai_khoan_id=?,so_dien_thoai=COALESCE(so_dien_thoai,?),
              email=COALESCE(email,?),so_giay_to=COALESCE(so_giay_to,?),giay_to_bon_so_cuoi=COALESCE(giay_to_bon_so_cuoi,?),
              ngay_sinh=COALESCE(ngay_sinh,?),dia_chi_thuong_tru=COALESCE(dia_chi_thuong_tru,?),
              nghe_nghiep=COALESCE(nghe_nghiep,?) WHERE id=?""",
              (account,phone,email,document,document[-4:],f'{1990+index%12}-05-15','Địa chỉ giả lập kiểm thử Sprint 3','Nhân viên (dữ liệu demo)',tenant['id']))
            changed_profiles.add(tenant['id'])
            access.append(dict(room=room['ma_phong'],accountId=account,email=email,password=password))
        else:
            access.append(dict(room=room['ma_phong'],accountId=tenant['tai_khoan_id'],note='Existing account; password unchanged.'))

    # Attach samples to the 50 rooms and the four contract-flow examples; never replace existing images.
    image_rooms=list(connection.execute('SELECT * FROM phong_tro WHERE toa_nha_id IN (?,?) ORDER BY id',(building['id'],5)))
    room_count=0
    for index,room in enumerate(image_rooms):
        existing=connection.execute('SELECT count(*) FROM anh_phong WHERE phong_id=?',(room['id'],)).fetchone()[0]
        if existing:continue
        for order in range(1,3):
            name=uuid.uuid4().hex
            full,thumb=room_samples[(index+order-1)%10]
            save(args.room_images/str(room['id'])/(name+'.jpg'),full)
            save(args.room_images/str(room['id'])/(name+'-thumb.jpg'),thumb)
            insert('anh_phong',dict(phong_id=room['id'],duong_dan=f'/uploads/rooms/{room["id"]}/{name}.jpg',
                duong_dan_anh_nho=f'/uploads/rooms/{room["id"]}/{name}-thumb.jpg',thu_tu=order,
                mo_ta='Ảnh mẫu do người dùng cung cấp; chỉ phục vụ demo Sprint 3',ngay_tao=now))
        room_count+=1
    profile_count=0
    profiles=connection.execute("""SELECT DISTINCT k.* FROM khach_thue k JOIN hop_dong h ON h.khach_dung_ten_id=k.id
             JOIN phong_tro p ON p.id=h.phong_id WHERE p.toa_nha_id IN (?,?)""",(building['id'],5)).fetchall()
    for profile in profiles:
        updates={}
        for column,data in zip(('anh_giay_to_truoc','anh_giay_to_sau'),identity_samples):
            if profile[column]:continue
            name=uuid.uuid4().hex+'.jpg';save(args.identity_images/name,data);updates[column]=name
        if updates:
            connection.execute('UPDATE khach_thue SET '+','.join(k+'=?' for k in updates)+' WHERE id=?',
                               [*updates.values(),profile['id']])
            changed_profiles.add(profile['id']);profile_count+=1

    # November: 46 complete, four missing, three existing drafts -> 43 eligible for creation.
    missing=[]
    for index,room in enumerate(rooms,1):
        contract=connection.execute('SELECT * FROM hop_dong WHERE phong_id=?',(room['id'],)).fetchone()
        services=connection.execute("""SELECT DISTINCT c.dich_vu_id FROM cau_hinh_dich_vu c
            JOIN dich_vu d ON d.id=c.dich_vu_id WHERE c.toa_nha_id=? AND c.cach_tinh='THEO_CHI_SO' AND c.den_ngay IS NULL ORDER BY c.dich_vu_id""",(building['id'],)).fetchall()
        for service in services:
            sid=service['dich_vu_id']
            if index>=47 and sid==(services[0]['dich_vu_id'] if index%2 else services[-1]['dich_vu_id']):
                missing.append(dict(room=room['ma_phong'],serviceId=sid));continue
            if connection.execute("SELECT 1 FROM chi_so_dien_nuoc WHERE hop_dong_id=? AND dich_vu_id=? AND tu_ngay='2026-11-01'",(contract['id'],sid)).fetchone():continue
            previous=connection.execute("SELECT chi_so_cuoi FROM chi_so_dien_nuoc WHERE hop_dong_id=? AND dich_vu_id=? AND den_ngay='2026-10-31'",(contract['id'],sid)).fetchone()
            if not previous:raise ValueError('October baseline missing.')
            first=Decimal(previous[0]);last=first+(Decimal(20) if sid==services[0]['dich_vu_id'] else Decimal(2))
            insert('chi_so_dien_nuoc',dict(hop_dong_id=contract['id'],dich_vu_id=sid,tu_ngay='2026-11-01',den_ngay='2026-11-30',
                chi_so_dau=str(first),chi_so_cuoi=str(last),nguoi_nhap_id=building['quan_ly_id'] or building['chu_nha_id'],
                ngay_nhap=now,da_khoa=0,phien_ban=0,da_xac_nhan_bat_thuong=0))
    added_invoices=[]
    for index,room in enumerate(rooms[:3],1):
        contract=connection.execute('SELECT * FROM hop_dong WHERE phong_id=?',(room['id'],)).fetchone()
        template=connection.execute("SELECT * FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=10 AND trang_thai='NHAP'",(contract['id'],)).fetchone()
        if not template:raise ValueError('October draft template missing.')
        for month,published in ((9,True),(11,False)):
            if connection.execute("SELECT 1 FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=? AND trang_thai<>'DA_HUY'",(contract['id'],month)).fetchone():continue
            data=dict(template);data.pop('id')
            data.update(ma_hoa_don=f'HD-S3-REPORT-2026{month:02}-B{index:03}',thang=month,
                tu_ngay=f'2026-{month:02}-01',den_ngay=f'2026-{month:02}-{calendar.monthrange(2026,month)[1]}',
                ngay_chot=f'2026-{month:02}-12',ngay_lap=now,ngay_phat_hanh=None,ngay_phat_hanh_nghiep_vu=None,
                nguoi_phat_hanh_id=None,han_thanh_toan=f'2026-{month:02}-20',trang_thai='NHAP',phien_ban=0,
                ghi_chu='Dữ liệu giả lập phục vụ báo cáo Sprint 3')
            invoice=insert('hoa_don',data)
            total=0
            for source_line in connection.execute('SELECT * FROM chi_tiet_hoa_don WHERE hoa_don_id=? ORDER BY so_thu_tu',(template['id'],)).fetchall():
                line=dict(source_line);line.pop('id');line['hoa_don_id']=invoice
                if line['cach_tinh_ap_dung']=='THEO_CHI_SO':
                    meter=connection.execute('SELECT * FROM chi_so_dien_nuoc WHERE hop_dong_id=? AND dich_vu_id=? AND tu_ngay=?',
                        (contract['id'],line['dich_vu_id'],data['tu_ngay'])).fetchone()
                    if not meter:raise ValueError('Invoice source reading missing.')
                    line.update(chi_so_id=meter['id'],chi_so_dau=meter['chi_so_dau'],chi_so_cuoi=meter['chi_so_cuoi'],
                        so_luong=str(Decimal(meter['chi_so_cuoi'])-Decimal(meter['chi_so_dau'])))
                    line['thanh_tien']=int((Decimal(line['so_luong'])*line['don_gia']).quantize(Decimal(1),rounding=ROUND_HALF_UP))
                total+=(-1 if line['loai_khoan']=='GIAM_TRU' else 1)*line['thanh_tien']
                insert('chi_tiet_hoa_don',line)
            connection.execute('UPDATE hoa_don SET tong_tien=? WHERE id=?',(total,invoice))
            if published:
                connection.execute("""UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh='2026-09-13T02:00:00Z',
                    ngay_phat_hanh_nghiep_vu='2026-09-13',nguoi_phat_hanh_id=?,phien_ban=phien_ban+1 WHERE id=?""",(building['chu_nha_id'],invoice))
                connection.execute("""UPDATE chi_so_dien_nuoc SET da_khoa=1,phien_ban=phien_ban+1
                    WHERE hop_dong_id=? AND tu_ngay='2026-09-01' AND da_khoa=0""",(contract['id'],))
                if index<=2:
                    insert('thanh_toan',dict(hoa_don_id=invoice,so_tien=total if index==1 else 1500000,
                        ngay_thanh_toan='2026-10-10',trang_thai='DA_XAC_NHAN'))
            added_invoices.append(dict(id=invoice,code=data['ma_hoa_don'],total=total,state='DA_PHAT_HANH' if published else 'NHAP'))

    insert('nhat_ky_hoat_dong',dict(nguoi_thuc_hien_id=building['chu_nha_id'],
        ten_nguoi_thuc_hien=connection.execute('SELECT ho_ten FROM tai_khoan WHERE id=?',(building['chu_nha_id'],)).fetchone()[0],
        vai_tro_luc_thuc_hien='CHU_NHA',loai_doi_tuong='demo_sprint3',doi_tuong_id=building['id'],
        hanh_dong='IMPORT_DEMO',du_lieu_truoc=json.dumps({'rooms':len(rooms)}),
        du_lieu_sau=json.dumps({'newAccounts':sum('password' in a for a in access),'roomsWithNewImages':room_count,
            'profilesWithNewImages':profile_count,'newInvoices':len(added_invoices)}),thoi_diem=now))
    # Verify all existing rows except explicitly enriched profile fields and September source locks.
    allowed_profile={'tai_khoan_id','so_dien_thoai','email','so_giay_to','giay_to_bon_so_cuoi','ngay_sinh','dia_chi_thuong_tru','nghe_nghiep','anh_giay_to_truoc','anh_giay_to_sau'}
    preserved={}
    for table,rows in before.items():
        columns=[r[1] for r in connection.execute('PRAGMA table_info("'+table+'")')]
        permitted=allowed_profile if table=='khach_thue' else {'da_khoa','phien_ban'} if table=='chi_so_dien_nuoc' else set()
        id_index=columns.index('id') if 'id' in columns else None
        if id_index is None:
            if [list(r) for r in connection.execute('SELECT * FROM "'+table+'" ORDER BY rowid')]!=[list(r) for r in rows]:
                raise ValueError('Unexpected infrastructure change: '+table)
        else:
            for old in rows:
                current=connection.execute('SELECT * FROM "'+table+'" WHERE id=?',(old[id_index],)).fetchone()
                if current is None:raise ValueError('Old record removed: '+table)
                for i,column in enumerate(columns):
                    if old[i]!=current[i] and column not in permitted:raise ValueError('Old record unexpectedly changed: '+table+'.'+column)
                if table=='khach_thue' and old['id'] not in changed_profiles and list(current)!=list(old):raise ValueError('Unexpected profile edit')
        preserved[table]=len(rows)
    if [tuple(r) for r in schema_before]!=[tuple(r) for r in connection.execute("SELECT type,name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name")]:
        raise ValueError('Schema changed.')
    if connection.execute('PRAGMA integrity_check').fetchone()[0]!='ok' or connection.execute('PRAGMA foreign_key_check').fetchall():
        raise ValueError('Database integrity/FK failed.')
    connection.commit()
    summary=dict(database=str(args.database),backup=str(backup),buildingId=building['id'],demoRooms=50,
        roomsWithNewImages=room_count,newRoomPhotos=room_count*2,profilesWithNewImages=profile_count,
        createdAccounts=sum('password' in a for a in access),addedInvoices=added_invoices,
        missingNovember=missing,preservedRecords=preserved,integrity='ok',foreignKeyErrors=0,
        unchangedAccountsFingerprint=fingerprint(before['tai_khoan']))
    (args.artifacts/'access.json').write_text(json.dumps(access,ensure_ascii=False,indent=2),encoding='utf-8')
    (args.artifacts/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in summary.items() if k not in ('preservedRecords','unchangedAccountsFingerprint')},ensure_ascii=True))
except:
    connection.rollback()
    for file in created:file.unlink(missing_ok=True)
    raise
finally:
    connection.close()
