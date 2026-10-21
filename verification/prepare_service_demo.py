"""Create a new disposable service/invoice demo and leave it running on localhost:5250.
Build first: dotnet build QL_PhongTro -c Debug -o data/service-demo/runtime
Run from repository root: python verification/prepare_service_demo.py
Never overwrites an existing demo. Credentials/logs stay in ignored data/service-demo.
"""
import datetime as dt
import hashlib
import html
import http.cookiejar
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
SOURCE = APP / 'Data/local-dev.sqlite'
DLL = ROOT / 'data/service-demo/runtime/QL_PhongTro.dll'
BASE = 'http://localhost:5250'


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Browser:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, route, data=None):
        payload = urllib.parse.urlencode(data).encode() if data is not None else None
        try:
            response = self.opener.open(BASE + route, payload, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, response.read().decode('utf-8'), response.headers

    def post(self, route, data, form=None):
        code, body, _ = self.request(form or route)
        assert code == 200, (form or route, code)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)
        assert token, 'Missing CSRF token'
        return self.request(route, dict(data, __RequestVerificationToken=html.unescape(token[1])))


def main():
    assert DLL.exists(), 'Build demo runtime first'
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 5250))
    source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    with sqlite3.connect(SOURCE.as_uri() + '?mode=ro', uri=True) as original:
        tables = {r[0] for r in original.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        assert 'tai_khoan' in tables
        assert not tables.intersection({'dich_vu', 'hop_dong', 'hoa_don'}), 'Source already has optional schema; review fixture before proceeding'
        print('Source schema inspected read-only; optional service/rental/invoice tables absent.', flush=True)
    original.close()
    folder = ROOT / 'data/service-demo' / (dt.datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    database = folder / 'demo.sqlite'
    env = dict(os.environ, DatabasePath=str(SOURCE), ASPNETCORE_ENVIRONMENT='Development',
               ASPNETCORE_URLS=BASE, PasswordReset__PublicBaseUrl=BASE,
               PasswordReset__PickupDirectory=str(folder / 'mail'), IdentityImagePath=str(folder / 'images'))

    def command(*args):
        result = subprocess.run(['dotnet', str(DLL), *args], cwd=APP, env=env,
                                capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=90)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result.stdout

    def query(sql, params=()):
        with sqlite3.connect(database) as connection:
            connection.execute('PRAGMA foreign_keys=ON')
            result = connection.execute(sql, params).fetchall()
        connection.close()
        return result

    output = command('--create-permission-demo', str(database))
    password = re.search(r'Demo password \(all four accounts\): (.+)', output)[1].strip()
    accounts = {}
    for email in re.findall(r'Login: (\S+)', output):
        account_id, role = query('SELECT id,vai_tro FROM tai_khoan WHERE email=?', (email,))[0]
        accounts[role] = (account_id, email)
    env['DatabasePath'] = str(database)
    command('--update-database')
    command('--initialize-services')
    # Rental CRUD does not yet exist; install its reference fixture only on the new copy.
    rentals = (ROOT / 'docs/sql/S1-06-quan-he-thue.sql').read_text(encoding='utf-8')
    with sqlite3.connect(database) as connection:
        connection.executescript('PRAGMA foreign_keys=ON; BEGIN IMMEDIATE;\n' + rentals[rentals.index('CREATE TABLE hop_dong'):])
    connection.close()
    command('--initialize-service-invoices')
    command('--check-database')
    log = (folder / 'server.log').open('w', encoding='utf-8')
    flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    process = subprocess.Popen(['dotnet', str(DLL)], cwd=APP, env=env, stdout=log, stderr=log, creationflags=flags)
    try:
        owner = Browser()
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError((folder / 'server.log').read_text(encoding='utf-8'))
            try:
                if owner.request('/Account/Login')[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.2)
        else:
            raise RuntimeError('Demo startup timed out')
        for role, (_, email) in accounts.items():
            browser = owner if role == 'CHU_NHA' else Browser()
            assert browser.post('/Account/Login', {'Identifier': email, 'Password': password})[0] == 302, role
            if role == 'ADMIN':
                assert browser.request('/NhatKy')[0] == 200
            elif role == 'QUAN_LY':
                assert browser.request('/DichVu')[0] == 403
        owner_id = accounts['CHU_NHA'][0]
        assert owner.post('/PhongTro/TaoToaNha', {'TenToaNha':'Demo Dich vu Hoa don', 'DiaChi':'Dia chi gia lap', 'SoTang':2})[0] == 302
        building = query('SELECT id FROM toa_nha WHERE chu_nha_id=? ORDER BY id DESC', (owner_id,))[0][0]
        rooms = []
        for code, status in [('DEMO-101','DANG_THUE'), ('DEMO-102','DANG_THUE'), ('DEMO-103','TRONG')]:
            response = owner.post('/PhongTro/Create', {'ToaNhaId':building, 'MaPhong':code, 'Tang':1, 'DienTich':25,
                'GiaThueDisplay':'2000000', 'SoNguoiToiDa':4, 'TrangThai':status}, f'/PhongTro/Create?toaNhaId={building}')
            assert response[0] == 302, response[:2]
            rooms.append(query('SELECT id FROM phong_tro WHERE toa_nha_id=? AND ma_phong=?', (building,code))[0][0])
        today = dt.datetime.now(dt.timezone(dt.timedelta(hours=7))).date()
        first = today.replace(day=1)
        end = first.replace(year=first.year+2) - dt.timedelta(days=1)
        profile = query('INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES(?,?,?) RETURNING id',
                        (accounts['KHACH_THUE'][0], 'Khach demo', today.isoformat()))[0][0]
        contracts = []
        for i, room in enumerate(rooms[:2], 1):
            contract = query("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,tien_coc_thoa_thuan,nguoi_lap_id,ngay_tao) VALUES(?,?,?,'DANG_HIEU_LUC',2000000,?,?) RETURNING id",
                             (f'HD-DEMO-{i}',room,profile,owner_id,today.isoformat()))[0][0]
            query('INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(?,1,?,?,24,2000000,?,?)',
                  (contract,first.isoformat(),end.isoformat(),owner_id,today.isoformat()))
            contracts.append(contract)
        assert owner.post('/DichVu/Initialize', {'toaNhaId':building}, f'/DichVu?toaNhaId={building}')[0] == 302
        prices = {'DIEN':3000,'NUOC':15000,'RAC':20000,'GUI_XE':50000,'INTERNET':100000}
        for code, price in prices.items():
            service_id = query('SELECT id FROM dich_vu WHERE ma_dich_vu=?',(code,))[0][0]
            config, old_price = query('SELECT id,don_gia FROM cau_hinh_dich_vu WHERE toa_nha_id=? AND dich_vu_id=?',(building,service_id))[0]
            response = owner.post('/DichVu/SavePrice', {'ToaNhaId':building,'DichVuId':service_id,'DonGia':price,
                'GiaCu':old_price,'PhienBan':config,'initial':'true'}, f'/DichVu/Manage?toaNhaId={building}&dichVuId={service_id}')
            assert response[0] == 302, response[:2]
        electricity, config = query("SELECT d.id,c.id FROM dich_vu d JOIN cau_hinh_dich_vu c ON c.dich_vu_id=d.id WHERE d.ma_dich_vu='DIEN' AND c.toa_nha_id=?",(building,))[0]
        form = f'/HoaDonDichVu?toaNhaId={building}&ngayApDung={today.isoformat()}'
        issue = {'ToaNhaId':building,'HopDongId':contracts[0],'NgayApDung':today.isoformat(),'SoNguoi':2,
                 'Dong[0].Chon':'true','Dong[0].DichVuId':electricity,'Dong[0].CauHinhId':config,
                 'Dong[0].DonGiaDaXem':3000,'Dong[0].ChiSoDau':100,'Dong[0].ChiSoCuoi':110}
        response = owner.post('/HoaDonDichVu/Issue', issue, form)
        assert response[0] == 302, response[:2]
        invoice, total = query('SELECT id,tong_tien FROM hoa_don WHERE hop_dong_id=?',(contracts[0],))[0]
        assert total == 2030000
        assert owner.request(f'/HoaDonDichVu/Details/{invoice}')[0] == 200
        assert owner.post('/HoaDonDichVu/Issue', issue, form)[0] == 200
        assert query('SELECT COUNT(*) FROM hoa_don WHERE hop_dong_id=?',(contracts[0],))[0][0] == 1
        assert query('PRAGMA integrity_check') == [('ok',)]
        assert not query('PRAGMA foreign_key_check')
        command('--check-database')
        assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash, 'Source file changed during setup'
        manifest = {'url':BASE, 'database':str(database), 'pid':process.pid, 'password':password,
                    'accounts':accounts, 'building':building, 'contracts':contracts, 'invoice':invoice,
                    'month':today.strftime('%m/%Y'), 'source_sha256':source_hash}
        (folder / 'access.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        (ROOT / 'data/service-demo/latest.txt').write_text(str(folder),encoding='utf-8')
        print(json.dumps(manifest,ensure_ascii=True,indent=2),flush=True)
        print('PASS: four logins, owner service/invoice pages, ADMIN audit, manager denied, invoice total/duplicate, schema/integrity/FK, source hash unchanged. Demo remains running.',flush=True)
    except BaseException:
        process.terminate()
        process.wait(timeout=15)
        raise
    finally:
        log.close()


if __name__ == '__main__':
    main()
