"""S1-03 HTTP integration checks on a temporary SQLite copy; no real email."""
import email
import copy
from email import policy
import hashlib
from html import unescape
import http.cookiejar
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import tempfile
import time
import urllib.request
import urllib.parse
import urllib.error

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
DLL = APP / 'bin/Debug/net10.0/QL_PhongTro.dll'
SOURCE = APP / 'Data/local-dev.sqlite'
BASE = 'http://localhost:5247'

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None

class Browser:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())

    def request(self, path, data=None, headers=None):
        raw = urllib.parse.urlencode(data).encode() if isinstance(data, dict) else data
        request = urllib.request.Request(BASE + path, raw, headers or {})
        try:
            response = self.opener.open(request, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        return response.code, response.read().decode('utf-8'), response.headers

    def post(self, path, data, form=None):
        code, body, _ = self.request(form or path)
        assert code == 200, (form or path, code)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        assert token, path
        return self.request(path, dict(data, __RequestVerificationToken=token[1]))

    def login(self, address, password):
        return self.post('/Account/Login', {'Identifier': address, 'Password': password})


# All setup mutations below target the disposable copy, never SOURCE.
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify(folder):
    db = folder / 'audit.sqlite'
    with sqlite3.connect(SOURCE.as_uri() + '?mode=ro', uri=True) as src, sqlite3.connect(db) as dst:
        src.backup(dst)
    src.close(); dst.close()
    env = dict(os.environ, DatabasePath=str(db), ASPNETCORE_ENVIRONMENT='Development',
               ASPNETCORE_URLS=BASE, PasswordReset__PickupDirectory=str(folder / 'mail'),
               PasswordReset__PublicBaseUrl=BASE)
    def command(arg, success=True):
        result = subprocess.run(['dotnet', str(DLL), arg], cwd=APP, env=env,
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
        assert (result.returncode == 0) == success, result.stdout + result.stderr
        return result.stdout + result.stderr
    def query(sql, args=()):
        c = sqlite3.connect(db)
        try:
            c.execute('PRAGMA foreign_keys=ON')
            rows = c.execute(sql, args).fetchall(); c.commit(); return rows
        finally: c.close()
    before = query('SELECT * FROM tai_khoan')
    columns = ','.join('"'+r[1]+'"' for r in query('PRAGMA table_info(tai_khoan)'))
    command('--update-database')
    assert query('SELECT '+columns+' FROM tai_khoan') == before
    assert query('SELECT MAX(version) FROM app_schema_version') == [(3,)]
    assert list(folder.glob('audit.sqlite.before-update-*.bak'))
    updated = digest(db)
    assert 'No changes' in command('--update-database')
    assert digest(db) == updated
    command('--initialize-services')
    with sqlite3.connect(db) as c:
        rentals = (ROOT / 'docs/sql/S1-06-quan-he-thue.sql').read_text(encoding='utf-8')
        c.executescript(rentals[rentals.index('CREATE TABLE hop_dong'):].replace('COMMIT;', ''))
    c.close()
    command('--initialize-service-invoices')
    command('--check-database')
    print('PASS: copied DB upgrade, backup, old rows preserved, repeat unchanged, optional S1-09 schema', flush=True)
    log = (folder / 'server.log').open('w', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(DLL)], cwd=APP, env=env, stdout=log, stderr=log)
    def count(): return query('SELECT COUNT(*) FROM nhat_ky_hoat_dong')[0][0]
    def audits(kind, target):
        return query('SELECT nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,hanh_dong,du_lieu_truoc,du_lieu_sau FROM nhat_ky_hoat_dong WHERE loai_doi_tuong=? AND doi_tuong_id=? ORDER BY id', (kind,target))
    def register(browser, name, phone):
        address = name+'@example.test'
        result = browser.post('/Account/Register', {'HoTen': name, 'Email': address,
            'SoDienThoai': phone, 'MatKhau': password, 'XacNhanMatKhau': password})
        assert result[0] == 302, (result[0], result[1][:500])
        return query('SELECT id FROM tai_khoan WHERE email=?',(address,))[0][0]
    try:
        admin = Browser()
        for _ in range(100):
            try:
                assert admin.request('/Account/Login')[0] == 200
                break
            except (OSError, AssertionError):
                if process.poll() is not None: raise AssertionError((folder/'server.log').read_text(encoding='utf-8'))
                time.sleep(.2)
        else: raise AssertionError('Server did not start')
        password = 'Test-'+os.urandom(10).hex()+'!'
        admin_id = register(admin, 'audit-admin', '0981234567')
        query("UPDATE tai_khoan SET vai_tro='ADMIN' WHERE id=?",(admin_id,))
        owner = Browser(); owner_id = register(owner, 'audit-owner', '0981234568')
        query("UPDATE tai_khoan SET vai_tro='CHU_NHA' WHERE id=?",(owner_id,))
        tenant = Browser(); tenant_id = register(tenant, 'audit-tenant', '0981234569')
        assert admin.request('/NhatKy')[0] == 200
        assert owner.request('/NhatKy')[0] == 403
        assert tenant.request('/NhatKy?role=ADMIN')[0] == 403
        assert Browser().request('/NhatKy')[0] == 302
        create = {'HoTen':'Managed audit', 'Email':'managed-audit@example.test','SoDienThoai':'0981234570','VaiTro':'QUAN_LY'}
        n=count(); assert admin.post('/ManagedAccounts/Create',create)[0]==302
        account_id=query('SELECT id FROM tai_khoan WHERE email=?',(create['Email'],))[0][0]
        assert count()==n+1
        row=audits('tai_khoan',account_id)[0]
        assert row[:4]==(admin_id,'audit-admin','ADMIN','TAO')
        assert json.loads(row[5])['vai_tro']=='QUAN_LY'
        n=count(); assert admin.post('/ManagedAccounts/Create',create)[0]==200
        assert admin.post('/ManagedAccounts/Create',dict(create,Email='invalid'))[0]==200
        assert count()==n
        for active,action in [('false','KHOA'),('true','MO_KHOA')]:
            assert admin.post('/ManagedAccounts/SetActive',{'id':account_id,'active':active,'confirmed':'true'},'/ManagedAccounts')[0]==302
            row=audits('tai_khoan',account_id)[-1]
            assert row[:4]==(admin_id,'audit-admin','ADMIN',action)
            assert json.loads(row[4])=={'dang_hoat_dong':active!='true'}
            assert json.loads(row[5])=={'dang_hoat_dong':active=='true'}
        n=count(); assert admin.post('/ManagedAccounts/Resend',{'id':account_id},'/ManagedAccounts')[0]==302
        assert count()==n+1 and audits('tai_khoan',account_id)[-1][3]=='GUI_LAI_MAT_KHAU_TAM'
        print('PASS: ADMIN/owner/tenant/anonymous access; account create, lock/unlock, resend, duplicate/invalid rejection',flush=True)
        building={'TenToaNha':'Audit building','DiaChi':'Test address','SoTang':2}
        assert owner.post('/PhongTro/TaoToaNha',building)[0]==302
        building_id=query('SELECT id FROM toa_nha WHERE ten_toa_nha=?',(building['TenToaNha'],))[0][0]
        assert audits('toa_nha',building_id)[0][:4]==(owner_id,'audit-owner','CHU_NHA','TAO')
        assert owner.post('/PhongTro/SuaToaNha/'+str(building_id),dict(building,TenToaNha='Renamed building'))[0]==302
        assert json.loads(audits('toa_nha',building_id)[-1][4])=={'ten_toa_nha':'Audit building'}
        room={'ToaNhaId':building_id,'MaPhong':'101','Tang':1,'DienTich':25,'GiaThueDisplay':'2000000','SoNguoiToiDa':4,'TrangThai':'TRONG'}
        assert owner.post('/PhongTro/Create',room,'/PhongTro/Create?toaNhaId='+str(building_id))[0]==302
        room_id=query('SELECT id FROM phong_tro WHERE toa_nha_id=?',(building_id,))[0][0]
        assert len(audits('phong_tro',room_id))==1
        n=count(); assert owner.post('/PhongTro/Create',room)[0]==200; assert count()==n
        service={'ToaNhaId':building_id,'TenDichVu':'Audit internet','CachTinh':'CO_DINH','DonViTinh':'thang','DonGia':100000}
        assert owner.post('/DichVu/Create',service,'/DichVu/Create?toaNhaId='+str(building_id))[0]==302
        service_id=query("SELECT id FROM dich_vu WHERE ten_dich_vu='Audit internet'")[0][0]
        price_id=query('SELECT id FROM cau_hinh_dich_vu WHERE dich_vu_id=?',(service_id,))[0][0]
        assert len(audits('dich_vu',service_id))==1 and len(audits('cau_hinh_dich_vu',price_id))==1
        assert json.loads(audits('cau_hinh_dich_vu',price_id)[0][5])['dich_vu_id']==service_id
        from datetime import datetime,timedelta,timezone,date
        today=datetime.now(timezone(timedelta(hours=7))).date()
        effective=today+timedelta(days=1)
        manage=f'/DichVu/Manage?toaNhaId={building_id}&dichVuId={service_id}'
        assert owner.post('/DichVu/SavePrice',{'ToaNhaId':building_id,'DichVuId':service_id,'DonGia':120000,'GiaCu':100000,'PhienBan':price_id,'TuNgay':effective.isoformat()},manage)[0]==302
        assert json.loads(audits('cau_hinh_dich_vu',price_id)[-1][5])['den_ngay']==today.isoformat()
        # Failure after one save in the existing multi-save transaction rolls back its audit too.
        latest=query('SELECT MAX(id) FROM cau_hinh_dich_vu WHERE dich_vu_id=?',(service_id,))[0][0]
        n=count(); versions=query('SELECT * FROM cau_hinh_dich_vu')
        query("CREATE TRIGGER test_fail_price BEFORE INSERT ON cau_hinh_dich_vu BEGIN SELECT RAISE(ABORT,'test failure'); END")
        assert owner.post('/DichVu/SavePrice',{'ToaNhaId':building_id,'DichVuId':service_id,'DonGia':130000,'GiaCu':120000,'PhienBan':latest,'TuNgay':(effective+timedelta(days=1)).isoformat()},manage)[0]==500
        assert count()==n and query('SELECT * FROM cau_hinh_dich_vu')==versions
        query('DROP TRIGGER test_fail_price')
        # Audit failure after business insert: no business row, no log, no email.
        n=count(); accounts=query('SELECT COUNT(*) FROM tai_khoan'); emails=len(list((folder/'mail').glob('*.eml')))
        query("CREATE TRIGGER test_fail_audit BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test audit failure'); END")
        assert admin.post('/ManagedAccounts/Create',dict(create,Email='rollback@example.test',SoDienThoai='0981234571'))[0]==500
        assert count()==n and query('SELECT COUNT(*) FROM tai_khoan')==accounts
        assert len(list((folder/'mail').glob('*.eml')))==emails
        query('DROP TRIGGER test_fail_audit')
        print('PASS: building create/edit, room, service/price, generated foreign IDs, rollback of business/audit and multi-save transaction',flush=True)
        # Existing S1-09 invoice flow; rental fixture only (no contract CRUD in app).
        profile=query("INSERT INTO khach_thue(ho_ten,ngay_tao) VALUES('Fixture','2026-01-01') RETURNING id")[0][0]
        contract=query("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES('AUDIT-HD',?,?,'DANG_HIEU_LUC',?,'2026-01-01') RETURNING id",(room_id,profile,owner_id))[0][0]
        first=today.replace(day=1); last=(first.replace(day=28)+timedelta(days=4)).replace(day=1)-timedelta(days=1)
        query("INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(?,1,?,?,1,2000000,?,'2026-01-01')",(contract,first.isoformat(),last.isoformat(),owner_id))
        invoice_form=f'/HoaDonDichVu?toaNhaId={building_id}&ngayApDung={today.isoformat()}'
        issue={'ToaNhaId':building_id,'HopDongId':contract,'NgayApDung':today.isoformat(),'SoNguoi':1,'Dong[0].Chon':'true','Dong[0].DichVuId':service_id,'Dong[0].CauHinhId':price_id,'Dong[0].DonGiaDaXem':100000}
        response=owner.post('/HoaDonDichVu/Issue',issue,invoice_form)
        assert response[0]==302,(response[0],response[1][-2000:])
        invoice_id=query('SELECT id FROM hoa_don WHERE hop_dong_id=?',(contract,))[0][0]
        assert [r[3] for r in audits('hoa_don',invoice_id)]==['TAO','DOI_TRANG_THAI']
        assert query("SELECT COUNT(*) FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='chi_tiet_hoa_don'")==[(2,)]
        n=count(); assert owner.post('/HoaDonDichVu/Issue',issue,invoice_form)[0]==200; assert count()==n
        # Rename actor directly only in fixture; recorded name and role must stay historical.
        query("UPDATE tai_khoan SET ho_ten='Current admin' WHERE id=?",(admin_id,))
        assert audits('tai_khoan',account_id)[0][1]=='audit-admin'
        for sql in ['UPDATE nhat_ky_hoat_dong SET hanh_dong=hanh_dong','DELETE FROM nhat_ky_hoat_dong']:
            try: query(sql)
            except sqlite3.IntegrityError: pass
            else: raise AssertionError('Audit mutability')
        for path in ['/NhatKy/Edit/1','/NhatKy/Delete/1','/NhatKy/Create']:
            assert admin.request(path,{})[0] in (404,405)
        assert admin.request('/NhatKy',{})[0]==405
        # Boundary fixtures prove Vietnam dates include 17:00 UTC previous day but exclude next 17:00.
        for moment in ['2001-01-01 16:59:59','2001-01-01 17:00:00','2001-01-02 16:59:59','2001-01-02 17:00:00']:
            query("INSERT INTO nhat_ky_hoat_dong(nguoi_thuc_hien_id,ten_nguoi_thuc_hien,vai_tro_luc_thuc_hien,loai_doi_tuong,doi_tuong_id,hanh_dong,thoi_diem) VALUES(?,'Boundary','ADMIN','tai_khoan',?,'TAO',?)",(admin_id,account_id,moment))
        def ids(url):
            code,html,_=admin.request(url); assert code==200,(code,html[:500]); return [int(x) for x in re.findall(r'data-audit-id="(\d+)"',html)]
        assert len(ids('/NhatKy?tuNgay=2001-01-02&denNgay=2001-01-02'))==2
        for params,where,args in [({'nguoiThucHienId':owner_id},'nguoi_thuc_hien_id=?',(owner_id,)),({'loaiDoiTuong':'tai_khoan'},'loai_doi_tuong=?',('tai_khoan',)),({'nguoiThucHienId':admin_id,'loaiDoiTuong':'tai_khoan','tuNgay':today.isoformat(),'denNgay':today.isoformat()},"nguoi_thuc_hien_id=? AND loai_doi_tuong=? AND thoi_diem >= ?",(admin_id,'tai_khoan',(datetime.combine(today,datetime.min.time())-timedelta(hours=7)).isoformat(' ')))]:
            assert ids('/NhatKy?'+urllib.parse.urlencode(params))==[r[0] for r in query('SELECT id FROM nhat_ky_hoat_dong WHERE '+where+' ORDER BY thoi_diem DESC,id DESC LIMIT 50',args)]
        for bad in ['tuNgay=no-date','tuNgay=2026-02-02&denNgay=2026-01-01','denNgay=9999-12-31','tuNgay=9999-01-01','denNgay=0001-01-01','loaiDoiTuong=invalid']:
            assert admin.request('/NhatKy?'+bad)[0]==400
        payload=' '.join(str(x) for row in query('SELECT du_lieu_truoc,du_lieu_sau FROM nhat_ky_hoat_dong') for x in row).lower()
        for forbidden in ['mat_khau','password','token','secret','cookie',password.lower(),'$2a$','$2b$','so_giay_to']:
            # must_change_password is a boolean business flag, never a password value.
            assert forbidden not in payload.replace('must_change_password',''),forbidden
        assert query('PRAGMA integrity_check')==[('ok',)] and not query('PRAGMA foreign_key_check')
        query("UPDATE tai_khoan SET vai_tro='KHACH_THUE' WHERE id=?",(admin_id,))
        assert admin.request('/NhatKy')[0]==403
        print('PASS: invoice publication/duplicate; actor snapshots; HTTP/SQLite immutable; individual/combined/date-boundary filters; no secrets; role revocation; integrity/FK',flush=True)
    except Exception:
        print('Server log retained at:',folder/'server.log')
        raise
    finally:
        process.terminate(); process.wait(timeout=20); log.close()

if __name__=='__main__':
    import socket
    with socket.socket() as probe:
        assert probe.connect_ex(('127.0.0.1',5247)) != 0, 'Port 5247 is occupied; leave the existing process untouched.'
    original=digest(SOURCE)
    folder=ROOT/'data'/'S1-10-verification'/str(time.time_ns())
    folder.mkdir(parents=True)
    try: verify(folder)
    finally: assert digest(SOURCE)==original,'Original database changed'
    print('PASS: localhost:5247 startup; original SQLite unchanged; server stopped')
