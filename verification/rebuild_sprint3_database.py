"""Create a fresh 50-room Sprint 3 database, retaining existing accounts/password hashes.
The old file is read-only during preparation. Activation is a separate step after verification.
"""
import argparse,sqlite3,json,uuid,calendar
from datetime import datetime,timezone
from decimal import Decimal,ROUND_HALF_UP
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source',type=Path,required=True)
parser.add_argument('--destination',type=Path,required=True)
args=parser.parse_args()
source=args.source.resolve();destination=args.destination.resolve()
if not source.is_relative_to(ROOT) or not destination.is_relative_to(ROOT) or destination.exists():
    raise ValueError('Use an existing source and a new destination within the workspace.')
destination.parent.mkdir(parents=True,exist_ok=True)
old=sqlite3.connect(source.as_uri()+'?mode=ro',uri=True);old.row_factory=sqlite3.Row
new=sqlite3.connect(destination);new.row_factory=sqlite3.Row
schema=old.execute("SELECT type,name,sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' ORDER BY CASE type WHEN 'table' THEN 0 WHEN 'index' THEN 1 WHEN 'view' THEN 2 ELSE 3 END,name").fetchall()
now=datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
def insert(table,data):
    return new.execute('INSERT INTO "'+table+'"('+','.join('"'+k+'"' for k in data)+') VALUES('+','.join('?' for _ in data)+')',list(data.values())).lastrowid
def copy(table,where='1',parameters=()):
    for row in old.execute('SELECT * FROM "'+table+'" WHERE '+where,parameters).fetchall():insert(table,dict(row))
for item in schema:
    if item['type']!='trigger':new.execute(item['sql'])
new.execute('PRAGMA foreign_keys=ON')
try:
 new.execute('BEGIN')
 for table in ('app_role','app_module','role_permission','app_schema_version','rental_request_schema','dich_vu'):
    copy(table)
 copy('tai_khoan')
 # Retain every account ID, email, phone, role and exact hash. Start fresh session versions.
 for account in new.execute('SELECT id FROM tai_khoan').fetchall():
    old_version=old.execute('SELECT version FROM account_session_version WHERE account_id=?',(account['id'],)).fetchone()
    try: version=str(int(old_version[0])+1) if old_version else '1'
    except ValueError:version=uuid.uuid4().hex
    insert('account_session_version',dict(account_id=account['id'],version=version))
 copy('khach_thue')
 building=old.execute("SELECT * FROM toa_nha WHERE ten_toa_nha='Sprint 3 – Phát hành 50 phòng'").fetchone()
 if not building:raise ValueError('Expected existing 50-room demo fixture.')
 insert('toa_nha',dict(building))
 owner=building['chu_nha_id'];manager=building['quan_ly_id'] or owner
 copy('dich_vu_toa_nha','toa_nha_id=?',(building['id'],))
 copy('cau_hinh_dich_vu','toa_nha_id=?',(building['id'],))
 rooms=old.execute('SELECT * FROM phong_tro WHERE toa_nha_id=? ORDER BY ma_phong',(building['id'],)).fetchall()
 if len(rooms)!=50:raise ValueError('Expected 50 rooms')
 active_contracts=[];examples=[]
 for index,room in enumerate(rooms,1):
    data=dict(room);data['phien_ban']=0;data['ngay_tao']=now
    data['trang_thai']='DANG_THUE' if index<=46 else 'DA_DAT_COC' if index==47 else 'TRONG' if index in (48,49) else 'NGUNG_CHO_THUE'
    insert('phong_tro',data)
    copy('anh_phong','phong_id=? AND dang_cho_xoa=0',(room['id'],))
    copy('dich_vu_phong','phong_id=?',(room['id'],))
    if index>46:
       if index<=49:
          tenant=new.execute("SELECT id FROM khach_thue WHERE tai_khoan_id=(SELECT id FROM tai_khoan WHERE vai_tro='KHACH_THUE' AND id=2)").fetchone()
          if not tenant:tenant=new.execute('SELECT id FROM khach_thue WHERE tai_khoan_id IS NOT NULL ORDER BY id').fetchone()
          listing=insert('tin_dang',dict(phong_id=room['id'],nguoi_dang_id=owner,tieu_de='Phòng '+room['ma_phong']+' – Demo Sprint 3',
              noi_dung='Phòng mẫu dùng cho kiểm thử yêu cầu thuê và lập hợp đồng.',ngay_dang=now,
              ngay_het_han='2026-12-31T16:59:59Z',trang_thai='TAM_AN' if index==47 else 'DANG_HIEN_THI',ngay_tao=now))
          if index in (47,48):
             insert('yeu_cau_thue',dict(ma_yeu_cau=f'YC-202610-S3{index:02}',tin_dang_id=listing,khach_thue_id=tenant['id'],
                 loai_yeu_cau='THUE_NGAY',ngay_mong_muon='2026-10-20',so_nguoi_du_kien=1,
                 trang_thai='DA_DUYET' if index==47 else 'MOI',nguoi_xu_ly_id=owner if index==47 else None,
                 ngay_xu_ly=now if index==47 else None,ngay_tao=now,phien_ban=0))
       continue
    original=old.execute('SELECT * FROM hop_dong WHERE phong_id=?',(room['id'],)).fetchone()
    contract=dict(original);contract.update(ngay_tao=now,trang_thai='DANG_HIEU_LUC',yeu_cau_thue_id=None,ngay_tra_phong=None)
    insert('hop_dong',contract)
    period=dict(old.execute('SELECT * FROM ky_hop_dong WHERE hop_dong_id=? ORDER BY so_thu_tu DESC LIMIT 1',(contract['id'],)).fetchone())
    period.update(ngay_tao=now,ngay_bat_dau='2026-07-01',ngay_ket_thuc='2027-06-30',so_thang=12,so_thu_tu=1)
    if index==45:period.update(ngay_bat_dau='2026-04-28',ngay_ket_thuc='2026-10-27',so_thang=6)
    insert('ky_hop_dong',period)
    if index==44:
       new.execute("UPDATE ky_hop_dong SET ngay_bat_dau='2025-07-01',ngay_ket_thuc='2026-06-30' WHERE id=?",(period['id'],))
       renewal=dict(period);renewal['id']=old.execute('SELECT MAX(id) FROM ky_hop_dong').fetchone()[0]+index;renewal.update(so_thu_tu=2,ngay_bat_dau='2026-07-01',ngay_ket_thuc='2027-06-30',gia_thue=3600000)
       insert('ky_hop_dong',renewal)
    copy('hop_dong_dich_vu','hop_dong_id=?',(contract['id'],))
    insert('hop_dong_chi_so_dau_ky',dict(hop_dong_id=contract['id'],ngay_ban_giao=period['ngay_bat_dau'],
        chi_so_dien='100',chi_so_nuoc='20',nguoi_nhap_id=owner,ngay_nhap=now))
    # Continuous monthly readings; current October has four deliberately incomplete rooms.
    for month in (7,8,9,10,11):
       for sid,start,step in ((1,100,20),(2,20,2)):
          if month==10 and index in (41,42,43,46) and sid==(1 if index%2 else 2):continue
          if month==11 and index==45:continue
          first=Decimal(start+step*(month-7));last=first+step
          end=calendar.monthrange(2026,month)[1]
          insert('chi_so_dien_nuoc',dict(hop_dong_id=contract['id'],dich_vu_id=sid,tu_ngay=f'2026-{month:02}-01',
              den_ngay=f'2026-{month:02}-{end:02}',chi_so_dau=str(first),chi_so_cuoi=str(last),
              nguoi_nhap_id=manager,ngay_nhap=now,da_khoa=0,phien_ban=0,da_xac_nhan_bat_thuong=0))
    active_contracts.append(contract['id'])
    # Three September issued samples, three October drafts, the rest remain available for creation.
    for month in ((9,10) if index<=3 else ()):
       template=old.execute("SELECT * FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=10 AND trang_thai='NHAP'",(contract['id'],)).fetchone()
       if not template:raise ValueError('Draft template missing.')
       bill=dict(template);bill.pop('id');bill.update(ma_hoa_don=f'HD-2026-S3-{month:02}-B{index:03}',thang=month,nam=2026,
           tu_ngay=f'2026-{month:02}-01',den_ngay=f'2026-{month:02}-{calendar.monthrange(2026,month)[1]}',
           ngay_chot=f'2026-{month:02}-12',ngay_lap=now,ngay_phat_hanh=None,ngay_phat_hanh_nghiep_vu=None,
           han_thanh_toan=f'2026-{month:02}-20',trang_thai='NHAP',phien_ban=0,nguoi_phat_hanh_id=None,
           thay_the_hoa_don_id=None,nguoi_huy_id=None,ngay_huy=None,ly_do_huy=None,ghi_chu='Bộ dữ liệu mới Sprint 3')
       iid=insert('hoa_don',bill);total=0
       for original_line in old.execute('SELECT * FROM chi_tiet_hoa_don WHERE hoa_don_id=? ORDER BY so_thu_tu',(template['id'],)).fetchall():
          line=dict(original_line);line.pop('id');line['hoa_don_id']=iid
          if line['cach_tinh_ap_dung']=='THEO_CHI_SO':
             reading=new.execute('SELECT * FROM chi_so_dien_nuoc WHERE hop_dong_id=? AND dich_vu_id=? AND tu_ngay=?',
                 (contract['id'],line['dich_vu_id'],bill['tu_ngay'])).fetchone()
             line.update(chi_so_id=reading['id'],chi_so_dau=reading['chi_so_dau'],chi_so_cuoi=reading['chi_so_cuoi'],
                 so_luong=str(Decimal(reading['chi_so_cuoi'])-Decimal(reading['chi_so_dau'])))
             line['thanh_tien']=int((Decimal(line['so_luong'])*line['don_gia']).quantize(Decimal(1),rounding=ROUND_HALF_UP))
          total+=(-1 if line['loai_khoan']=='GIAM_TRU' else 1)*line['thanh_tien'];insert('chi_tiet_hoa_don',line)
       new.execute('UPDATE hoa_don SET tong_tien=? WHERE id=?',(total,iid))
       if month==9:
          new.execute("UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh='2026-09-13T02:00:00Z',ngay_phat_hanh_nghiep_vu='2026-09-13',nguoi_phat_hanh_id=?,phien_ban=1 WHERE id=?",(owner,iid))
          new.execute("UPDATE chi_so_dien_nuoc SET da_khoa=1,phien_ban=1 WHERE hop_dong_id=? AND tu_ngay='2026-09-01'",(contract['id'],))
          if index<=2:insert('thanh_toan',dict(hoa_don_id=iid,so_tien=total if index==1 else 1500000,ngay_thanh_toan='2026-10-10',trang_thai='DA_XAC_NHAN'))
       examples.append(dict(code=bill['ma_hoa_don'],state='Đã phát hành' if month==9 else 'Nháp'))
 # Use unlinked old demo profiles only as fresh roommates, with nonoverlapping histories.
 free=new.execute('SELECT id FROM khach_thue WHERE tai_khoan_id IS NULL AND id NOT IN (SELECT khach_dung_ten_id FROM hop_dong) ORDER BY id').fetchall()
 if free:
    insert('nguoi_o_ghep',dict(hop_dong_id=active_contracts[3],khach_thue_id=free[0]['id'],ngay_vao='2026-09-01',ngay_ra=None))
 if len(free)>1:
    insert('nguoi_o_ghep',dict(hop_dong_id=active_contracts[4],khach_thue_id=free[1]['id'],ngay_vao='2026-08-01',ngay_ra='2026-09-30'))
 actor=new.execute('SELECT ho_ten FROM tai_khoan WHERE id=?',(owner,)).fetchone()[0]
 insert('nhat_ky_hoat_dong',dict(nguoi_thuc_hien_id=owner,ten_nguoi_thuc_hien=actor,vai_tro_luc_thuc_hien='CHU_NHA',
     loai_doi_tuong='demo_sprint3',doi_tuong_id=building['id'],hanh_dong='TAO_DEMO_MOI',
     du_lieu_sau=json.dumps({'rooms':50,'contracts':46,'accountPasswordsPreserved':True}),thoi_diem=now))
 for item in schema:
    if item['type']=='trigger':new.execute(item['sql'])
 for account in old.execute('SELECT * FROM tai_khoan'):
    assert tuple(new.execute('SELECT * FROM tai_khoan WHERE id=?',(account['id'],)).fetchone())==tuple(account)
 assert not new.execute('PRAGMA foreign_key_check').fetchall()
 assert new.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
 assert new.execute('SELECT COUNT(*) FROM phong_tro').fetchone()[0]==50
 for image in new.execute('SELECT duong_dan,duong_dan_anh_nho FROM anh_phong'):
    for url in image:assert (ROOT/'QL_PhongTro/wwwroot'/url.lstrip('/')).is_file()
 new.commit()
 result=dict(database=str(destination),rooms=50,contracts=46,accountsPreserved=new.execute('SELECT COUNT(*) FROM tai_khoan').fetchone()[0],
     photos=new.execute('SELECT COUNT(*) FROM anh_phong').fetchone()[0],invoices=examples,integrity='ok',foreignKeyErrors=0)
 destination.with_suffix('.summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({k:v for k,v in result.items() if k!='invoices'},ensure_ascii=True))
except:
 new.rollback()
 raise
finally:
 new.close();old.close()
