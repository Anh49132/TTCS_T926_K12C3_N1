"""Prepare/verify an isolated fake S4-07 demo; never write to the Sprint 2 source.
python verification/S407Demo.py prepare
python verification/S407Demo.py verify <data/s407/demo-...>
Start the dedicated runtime between prepare and verify (see docs/S4-07-tiep-nhan-tien-do.md).
"""
import argparse
import hashlib
import html
import http.cookiejar
import json
import re
import shutil
import sqlite3
import struct
import time
import uuid
import zlib
from datetime import datetime, timedelta, timezone
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def prepare():
    source_folder = Path((ROOT / 'data/sprint2-demo/latest.txt').read_text(encoding='utf-8-sig').strip()).resolve()
    assert source_folder.parent == (ROOT / 'data/sprint2-demo').resolve(), 'Only generated fake Sprint 2 source permitted'
    source = json.loads((source_folder / 'access.json').read_text(encoding='utf-8-sig'))
    folder = ROOT / 'data/s407' / ('demo-' + datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    database = folder / 's407.sqlite'
    fingerprint = digest(source['database'])
    # SQLite backup reads a consistent snapshot without upgrading or writing to source.
    with sqlite3.connect(Path(source['database']).as_uri() + '?mode=ro', uri=True) as origin, sqlite3.connect(database) as db:
        origin.backup(db)
    with sqlite3.connect(database) as db:
        db.execute('PRAGMA foreign_keys=ON')
        assert db.execute('SELECT MAX(version) FROM app_schema_version').fetchone()[0] == 30, 'Schema v30 required; do not migrate automatically'
        ids = dict(db.execute('SELECT email,id FROM tai_khoan'))
        manager = ids['manager.demo@demo.local']; owner = ids['owner.demo@demo.local']
        tenant = ids['tenant1.demo@demo.local']
        profile = db.execute('SELECT id FROM khach_thue WHERE tai_khoan_id=?', (tenant,)).fetchone()[0]
        now = datetime.now(timezone.utc)
        stamp = now.isoformat(); today = now.astimezone(timezone(timedelta(hours=7))).date()
        db.execute('''INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
            SELECT ?,?,?,mat_khau,'QUAN_LY',1,1,?,? FROM tai_khoan WHERE id=?''',
            ('Quản lý ngoài phạm vi S407','manager.other.s407@demo.local','0900070407',stamp,stamp,manager))
        building = db.execute('INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(?,?,?,?) RETURNING id',
            (owner,manager,'Tòa demo S4-07','Địa chỉ giả S4-07')).fetchone()[0]
        room = db.execute('''INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao)
            VALUES(?,'S407-101',1,25,3000000,3,'DANG_THUE',?) RETURNING id''',(building,stamp)).fetchone()[0]
        contract = db.execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai) VALUES('HD-S407-DEMO',?,?,'DANG_HIEU_LUC') RETURNING id",(room,profile)).fetchone()[0]
        db.execute('INSERT INTO ky_hop_dong(hop_dong_id,ngay_bat_dau,ngay_ket_thuc,gia_thue) VALUES(?,?,?,3000000)',
            (contract, str(today-timedelta(days=1)),str(today+timedelta(days=90))))
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    shutil.copytree(source['roomImagesPath'], folder/'room-images')
    access = dict(source, directory=str(folder), database=str(database), url='http://localhost:5271',
        runtime=str(ROOT/'data/s407/runtime/QL_PhongTro.dll'), roomImagesPath=str(folder/'room-images'),
        contract=contract, building=building, room=room, source_database=source['database'], source_sha256=fingerprint)
    (folder/'access.json').write_text(json.dumps(access,ensure_ascii=False,indent=2),encoding='utf-8')
    assert digest(source['database']) == fingerprint, 'Source changed during preparation'
    (ROOT/'data/s407/latest.txt').write_text(str(folder),encoding='utf-8')
    print(str(folder))

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args): return None

class Session:
    def __init__(self, access):
        self.access = access
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
    def request(self,path,data=None,headers=None):
        if isinstance(data,dict): data=urllib.parse.urlencode(data).encode()
        req=urllib.request.Request(self.access['url']+path,data,headers or {})
        try: response=self.opener.open(req,timeout=30)
        except urllib.error.HTTPError as ex: response=ex
        with response: return response.status,response.read(),response.headers
    def page(self,path):
        status,body,headers=self.request(path)
        return status,html.unescape(body.decode()),headers
    def login(self,email):
        status,page,_=self.page('/Account/Login'); assert status==200
        assert self.request('/Account/Login',{'TaiKhoanDangNhap':email,'MatKhau':self.access['password'],
            '__RequestVerificationToken':hidden(page,'__RequestVerificationToken')})[0]==302, email

def hidden(page,name):
    return re.search(r'name="'+re.escape(name)+r'"[^>]*value="([^"]*)"',page).group(1)

def verify(folder):
    folder=folder.resolve()
    assert folder.parent==(ROOT/'data/s407').resolve() and folder.name.startswith('demo-'), 'Dedicated fake S407 only'
    access=json.loads((folder/'access.json').read_text(encoding='utf-8-sig'))
    assert Path(access['database']).resolve().parent==folder
    assert urllib.parse.urlparse(access['url']).hostname in ('localhost','127.0.0.1')
    for attempt in range(30):
        try:
            if Session(access).request('/Account/Login')[0] == 200: break
        except urllib.error.URLError:
            if attempt == 29: raise
            time.sleep(.5)
    sessions={}
    for role,email in [('manager','manager.demo@demo.local'),('owner','owner.demo@demo.local'),('tenant','tenant1.demo@demo.local'),
            ('other_owner','owner.b.demo@demo.local'),('other_manager','manager.other.s407@demo.local'),('other_tenant','tenant3.demo@demo.local')]:
        sessions[role]=Session(access); sessions[role].login(email)
    manager,owner,tenant=(sessions[r] for r in ('manager','owner','tenant'))
    def chunk(kind,data): return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data))
    png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(b'\x00\x78\x96\xb4'))+chunk(b'IEND',b'')
    def create(description):
        status,page,_=tenant.page('/BaoHong/Create'); assert status==200
        fields={'__RequestVerificationToken':hidden(page,'__RequestVerificationToken'),'Input.KhoaChongTrung':hidden(page,'Input.KhoaChongTrung'),
            'Input.HopDongId':access['contract'],'Input.LoaiThietBi':'Vòi nước','Input.MoTa':description,'Input.MucDo':'GAP'}
        boundary='s407-'+uuid.uuid4().hex
        parts=[f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k,v in fields.items()]
        parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="Input.Images"; filename="repair.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+png+b'\r\n',f'--{boundary}--\r\n'.encode()]
        status,_,headers=tenant.request('/BaoHong/Create',b''.join(parts),{'Content-Type':f'multipart/form-data; boundary={boundary}'})
        assert status==302,(status,headers)
        return int(headers['Location'].rsplit('/',1)[-1])
    id=create('Nước rò dưới bồn rửa — kiểm thử S4-07 <script>alert(1)</script>')
    demo_id=create('Báo hỏng Mới để nghiệm thu: vòi nước bị rò.')
    reject_id=create('Báo hỏng Mới để nghiệm thu nhánh Từ chối.')
    def form(session=manager,target='DANG_XU_LY',note='Đã liên hệ thợ sửa chữa',issue=id):
        status,page,_=session.page(f'/QuanLyBaoHong/Details/{issue}'); assert status==200
        return {'__RequestVerificationToken':hidden(page,'__RequestVerificationToken'),
            'CapNhat.PhienBan':hidden(page,'CapNhat.PhienBan'),'CapNhat.KhoaChongTrung':hidden(page,'CapNhat.KhoaChongTrung'),
            'CapNhat.TrangThai':target,'CapNhat.GhiChu':note}
    data=form(); endpoint=f'/QuanLyBaoHong/CapNhat/{id}'
    assert manager.request(endpoint,{k:v for k,v in data.items() if k!='__RequestVerificationToken'})[0]==400
    for note in ('',' \r\n\t '): assert manager.request(endpoint,dict(data,**{'CapNhat.GhiChu':note}))[0]==200
    assert manager.request(endpoint,dict(data,**{'CapNhat.TrangThai':'HOAN_THANH'}))[0]==200
    assert manager.request(endpoint,data)[0]==302
    assert manager.request(endpoint,data)[0]==302
    assert manager.request(endpoint,dict(data,**{'CapNhat.KhoaChongTrung':uuid.uuid4().hex}))[0]==409
    for role in ('owner','tenant'):
        session=sessions[role]; controller='ChuNhaBaoHong' if role=='owner' else 'BaoHong'
        status,page,headers=session.page(f'/{controller}/Details/{id}')
        assert status==200 and 'Đã liên hệ thợ sửa chữa' in page and 'Người gửi' in page
        assert 'CapNhat.TrangThai' not in page and 'PhanHoi.NoiDung' not in page
        assert '<script>alert(1)</script>' not in session.request(f'/{controller}/Details/{id}')[1].decode()
        assert 'no-store' in headers['Cache-Control']
        status,body,headers=session.request(f'/{controller}/Image/{id}?order=1')
        assert status==200 and body==png and headers['X-Content-Type-Options']=='nosniff'
    assert owner.page('/Modules/BAO_HONG')[2]['Location']=='/ChuNhaBaoHong'
    assert 'Nước rò dưới bồn rửa' in manager.page('/QuanLyBaoHong')[1]
    for role,controller in [('other_owner','ChuNhaBaoHong'),('other_manager','QuanLyBaoHong'),('other_tenant','BaoHong')]:
        s=sessions[role]
        assert f'/{controller}/Details/{id}' not in s.page('/'+controller)[1]
        assert s.request(f'/{controller}/Details/{id}')[0]==403
        assert s.request(f'/{controller}/Image/{id}?order=1')[0]==403
    for role,controller in [('owner','ChuNhaBaoHong'),('tenant','BaoHong'),('other_manager','QuanLyBaoHong')]:
        s=sessions[role]; _,page,_=s.page('/'+controller)
        forged=dict(data,**{'__RequestVerificationToken':hidden(page,'__RequestVerificationToken')})
        assert s.request(endpoint,forged)[0]==403
        assert s.request(f'/QuanLyBaoHong/PhanHoi/{id}',{'__RequestVerificationToken':forged['__RequestVerificationToken'],
            'PhanHoi.KhoaChongTrung':uuid.uuid4().hex,'PhanHoi.NoiDung':'Không được gửi'})[0]==403
    final=form(target='HOAN_THANH',note='Đã sửa xong vòi nước')
    assert manager.request(endpoint,final)[0]==302
    assert manager.request(endpoint,dict(final,**{'CapNhat.PhienBan':'2','CapNhat.TrangThai':'DANG_XU_LY','CapNhat.KhoaChongTrung':uuid.uuid4().hex}))[0]==200
    assert manager.request(f'/QuanLyBaoHong/CapNhat/{reject_id}',form(target='TU_CHOI',note='Thiết bị không thuộc yêu cầu bảo trì',issue=reject_id))[0]==302
    with sqlite3.connect(access['database']) as db:
        history=db.execute('SELECT trang_thai_truoc,trang_thai_sau,noi_dung,nguoi_gui_id,thoi_diem FROM bao_hong_trao_doi WHERE bao_hong_id=? ORDER BY thoi_diem,id',(id,)).fetchall()
        assert len(history)==2 and history[0][:3]==('MOI','DANG_XU_LY','Đã liên hệ thợ sửa chữa')
        assert history[1][:3]==('DANG_XU_LY','HOAN_THANH','Đã sửa xong vòi nước')
        manager_id=db.execute("SELECT id FROM tai_khoan WHERE email='manager.demo@demo.local'").fetchone()[0]
        assert all(x[3]==manager_id and x[4] for x in history)
        db.execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=?",(access['contract'],)); db.commit()
        for session,controller in [(owner,'ChuNhaBaoHong'),(tenant,'BaoHong')]:
            assert session.page(f'/{controller}/Details/{id}')[0]==200
            assert session.request(f'/{controller}/Image/{id}?order=1')[0]==200
        # Restore the fake contract so the untouched Mới reports can be demoed.
        db.execute("UPDATE hop_dong SET trang_thai='DANG_HIEU_LUC' WHERE id=?",(access['contract'],)); db.commit()
        assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    assert digest(access['source_database'])==access['source_sha256'], 'Sprint 2 source changed'
    access.update(demo_issue=demo_id,reject_issue=reject_id,verified_issue=id)
    (folder/'access.json').write_text(json.dumps(access,ensure_ascii=False,indent=2),encoding='utf-8')
    report={'passed':True,'demo_issue':demo_id,'verified_issue':id,'sprint2_unchanged':True,
        'checks':['seven routes/role scope','owner photos and read-only','sender/list description','CSRF400','blank notes','invalid transition',
            'retry','stale409','manager identity/time/history','owner/tenant reload','ended contract','terminal states','Razor escaping','integrity/FK']}
    (folder/'http-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=True))

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('mode',choices=['prepare','verify']); parser.add_argument('folder',nargs='?',type=Path)
    args=parser.parse_args()
    if args.mode=='prepare': prepare()
    else:
        assert args.folder, 'Demo folder required'
        verify(args.folder)
