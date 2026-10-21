"""S2-06: new isolated database, read-only source backup, real HTTP and CSRF.

Artifacts and the fake demo fixture remain in ignored data/S2-06 (server stops).
No credentials, database or personal data should be committed.
"""
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import sqlite3
import subprocess
import sys
import time
from html import unescape
from datetime import datetime, timedelta, timezone

from account_delete_http import Browser as BaseBrowser
import account_delete_http as browser_module

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
DLL = Path(os.environ.get('QL_TEST_DLL', ROOT / 'data/S2-06/runtime/QL_PhongTro.dll'))
SOURCE = Path(os.environ.get('DatabasePath', APP / 'Data/local-dev.sqlite')).resolve()
BASE = os.environ.get('QL_TEST_BASE', 'http://localhost:5266')
browser_module.BASE = BASE
class Browser(BaseBrowser):
    def login(self, address, password):
        return self.post('/Account/Login', {'TaiKhoanDangNhap':address, 'MatKhau':password})
FOLDER = ROOT / 'data/S2-06' / (datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(3))
FOLDER.mkdir(parents=True)
print('Artifacts:', FOLDER, flush=True)
digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
original = digest(SOURCE) if SOURCE.exists() else None
DB = FOLDER / 'demo.sqlite'
password = secrets.token_urlsafe(20) + 'aA1!'
env = dict(os.environ, DatabasePath=str(DB), ASPNETCORE_ENVIRONMENT='Development',
           ASPNETCORE_URLS=BASE, PasswordReset__PickupDirectory=str(FOLDER / 'mail'),
           LocalAdmin__Email='s206-admin@example.test', LocalAdmin__Phone='0902060000',
           LocalAdmin__Password=password)

def cli(flag, path=DB, success=True):
    r = subprocess.run(['dotnet', str(DLL), flag], cwd=APP,
        env=dict(env, DatabasePath=str(path)), capture_output=True, text=True, encoding='utf-8', errors='replace')
    assert (r.returncode == 0) == success, r.stdout + r.stderr
    return r.stdout + r.stderr

def query(sql, args=()):
    with sqlite3.connect(DB) as c:
        c.execute('PRAGMA foreign_keys=ON')
        return c.execute(sql, args).fetchall()

def verify():
    cli('--initialize-database')
    h = digest(DB)
    cli('--initialize-database', success=False)
    assert digest(DB) == h, 'Existing file overwritten'
    cli('--check-database')
    assert query('SELECT COUNT(*) FROM tai_khoan')[0][0] == 0
    cli('--initialize-rental-requests')
    h = digest(DB)
    cli('--initialize-rental-requests')
    assert digest(DB) == h, 'Repeated module update wrote data'
    cli('--create-local-admin')
    admin = query("SELECT id FROM tai_khoan WHERE email='s206-admin@example.test'")[0][0]
    for i in (1, 2):
        query("""INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,
              ngay_tao,ngay_cap_nhat,email_confirmed,is_deleted)
              SELECT ?,?,?,mat_khau,'KHACH_THUE',1,ngay_tao,ngay_cap_nhat,1,0 FROM tai_khoan WHERE id=?""",
              ('Khách thử ' + str(i), f's206-tenant{i}@example.test', f'090206000{i}', admin))
    query("INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi) VALUES (?,'Tòa thử S2-06','Địa chỉ giả')", (admin,))
    building = query('SELECT max(id) FROM toa_nha')[0][0]
    now = datetime.now(timezone.utc).replace(tzinfo=None)
    query("""INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
             VALUES (?,'S206-101',1,25,2000000,1000000,4,'TRONG',?)""", (building, now.isoformat(' ')))
    room = query('SELECT max(id) FROM phong_tro')[0][0]
    for state, expiry in [('DANG_HIEN_THI', now+timedelta(days=30)), ('NHAP', now+timedelta(days=30)), ('TAM_AN', now+timedelta(days=30)), ('DA_CHO_THUE', now+timedelta(days=30))]:
        query("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,ngay_dang,ngay_het_han,trang_thai,ngay_tao) VALUES (?,?,?,?,?,?,?,?)",
              (room, admin, 'Phòng mẫu S2-06', 'Dữ liệu giả để thử gửi yêu cầu.', str(now), str(expiry), state, str(now)))
    listing = query("SELECT id FROM tin_dang WHERE trang_thai='DANG_HIEN_THI'")[0][0]

    # Upgrade only a consistent copy; preserve every pre-existing business row.
    if original:
        copy = FOLDER / 'upgrade-copy.sqlite'
        with sqlite3.connect(SOURCE.as_uri()+'?mode=ro', uri=True) as src, sqlite3.connect(copy) as dst:
            src.backup(dst)
        with sqlite3.connect(copy) as c:
            tables = [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table' AND name<>'sqlite_sequence'")]
            before = {t: c.execute('SELECT * FROM "'+t+'"').fetchall() for t in tables}
        cli('--update-database', copy)
        cli('--initialize-rental-requests', copy)
        with sqlite3.connect(copy) as c:
            for t, rows in before.items():
                actual = c.execute('SELECT * FROM "'+t+'"').fetchall()
                assert (all(row in actual for row in rows) if t == 'app_schema_version' else actual == rows), t
            assert c.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
            assert not c.execute('PRAGMA foreign_key_check').fetchall()
        if 'rental_request_schema' not in tables:
            assert list(FOLDER.glob('upgrade-copy.sqlite.before-rental-*.bak'))

    with (FOLDER / 'server.log').open('w', encoding='utf-8') as log:
        p = subprocess.Popen(['dotnet', str(DLL)], cwd=APP, env=env, stdout=log, stderr=log)
    try:
        for _ in range(100):
            try:
                if Browser().request('/Account/Login')[0] == 200: break
            except OSError: pass
            if p.poll() is not None: raise AssertionError((FOLDER/'server.log').read_text(encoding='utf-8'))
            time.sleep(.2)
        else: raise AssertionError('Startup timeout')
        tenant = Browser()
        assert tenant.login('s206-tenant1@example.test', password)[0] == 302
        path = f'/TinDang/ChiTiet/{listing}'
        post = f'/TinDang/GuiYeuCau/{listing}'
        def check_listing_menu(browser):
            code, body, _ = browser.request('/TinDang')
            assert code == 200
            assert '<h1 id="listing-index-title">Tin đăng cho thuê</h1>' in body
            assert 'aria-label="Điều hướng chính"' in body
        check_listing_menu(tenant)
        check_listing_menu(Browser())
        code, _, headers = tenant.request('/Modules/TIN_DANG')
        assert code == 302 and headers['Location'] == '/TinDang'
        for hidden in query("SELECT id FROM tin_dang WHERE trang_thai<>'DANG_HIEN_THI'"):
            assert tenant.request('/TinDang/ChiTiet/'+str(hidden[0]))[0] == 404
        today = datetime.now(timezone(timedelta(hours=7))).date()
        last_day = today + timedelta(days=60)
        code, body, _ = tenant.request(path)
        date_input = re.search(r'<input[^>]*name="Form.NgayMongMuon"[^>]*>', body).group(0)
        assert f'min="{today.isoformat()}"' in date_input
        assert f'max="{last_day.isoformat()}"' in date_input
        valid = {'Form.LoaiYeuCau':'XEM_PHONG', 'Form.NgayMongMuon':today.isoformat(), 'Form.SoNguoiDuKien':2,
                 'Form.LoiNhan':'<script>alert(1)</script>', 'KhachThueId':999, 'TaiKhoanId':admin, 'TinDangId':999}
        before_invalid = {table: query(f'SELECT * FROM {table}') for table in
                          ['yeu_cau_thue','khach_thue','rental_request_counter','nhat_ky_hoat_dong']}
        people_input = re.search(r'<input[^>]*name="Form.SoNguoiDuKien"[^>]*>', body).group(0)
        assert 'max="4"' in people_input and 'min="1"' in people_input
        for maximum in [4, 2]:
            # Fixture-only change proves the server reads the room's current limit.
            query('UPDATE phong_tro SET so_nguoi_toi_da=? WHERE id=?', (maximum, room))
            for kind in ['XEM_PHONG', 'THUE_NGAY']:
                code, rejected, _ = tenant.post(post, dict(valid, **{
                    'Form.LoaiYeuCau':kind, 'Form.SoNguoiDuKien':maximum+1}), path)
                assert code == 200
                error = re.search(r'<span[^>]*data-valmsg-for="Form.SoNguoiDuKien"[^>]*>(.*?)</span>', rejected, re.S)
                assert error and f'Phòng chỉ cho phép tối đa {maximum} người.' in unescape(error.group(1))
                assert f'value="{maximum+1}"' in rejected
                assert all(query(f'SELECT * FROM {table}') == rows for table, rows in before_invalid.items()), 'Over-capacity request changed data'
        query('UPDATE phong_tro SET so_nguoi_toi_da=4 WHERE id=?', (room,))
        for kind in ['XEM_PHONG','THUE_NGAY']:
            for date, message in [(today-timedelta(days=1), 'Ngày mong muốn không được là ngày trong quá khứ.'),
                                  (today+timedelta(days=61), 'Ngày mong muốn không được quá 60 ngày kể từ hôm nay.')]:
                # Direct HTTP POST with a real CSRF token bypasses browser min/max validation.
                code, body, _ = tenant.post(post, dict(valid, **{'Form.LoaiYeuCau':kind,
                                            'Form.NgayMongMuon':date.isoformat()}), path)
                assert code == 200
                field_error = re.search(r'<span[^>]*data-valmsg-for="Form.NgayMongMuon"[^>]*>(.*?)</span>', body, re.S)
                assert field_error and message in unescape(field_error.group(1)), body
                assert f'value="{date.isoformat()}"' in body
        assert all(query(f'SELECT * FROM {table}') == rows for table, rows in before_invalid.items()), 'Invalid dates changed data'
        for missing in ['Form.LoaiYeuCau','Form.NgayMongMuon','Form.SoNguoiDuKien']:
            invalid = dict(valid); invalid.pop(missing)
            code, body, _ = tenant.post(post, invalid, path)
            assert code == 200 and 'text-danger' in body
        for field, value in [('Form.LoaiYeuCau','BAD'),('Form.NgayMongMuon','bad-date'),('Form.SoNguoiDuKien','0'),('Form.SoNguoiDuKien','1.5')]:
            assert tenant.post(post, dict(valid, **{field:value}), path)[0] == 200
        assert query('SELECT COUNT(*) FROM yeu_cau_thue')[0][0] == 0
        assert tenant.request(post, valid)[0] == 400, 'Missing CSRF accepted'
        assert Browser().request(post, valid)[0] == 302
        staff = Browser(); assert staff.login(env['LocalAdmin__Email'], password)[0] == 302
        check_listing_menu(staff)
        assert staff.post(post, valid, path)[0] == 403
        month = datetime.now(timezone(timedelta(hours=7))).strftime('%Y%m')
        locations = []
        for kind, date in [('XEM_PHONG',today),('THUE_NGAY',last_day)]:
            # Closing a fixture request simulates PO's allowed resend rule; no production endpoint is added.
            query("UPDATE yeu_cau_thue SET trang_thai='TU_CHOI'")
            code, _, headers = tenant.post(post, dict(valid, **{'Form.LoaiYeuCau':kind,
                                             'Form.NgayMongMuon':date.isoformat()}), path)
            assert code == 302
            locations.append(headers['Location'])
            code, body, _ = tenant.request(headers['Location'])
            assert code == 200 and 'Gửi yêu cầu thành công' in body
            assert re.search(r'YC-'+month+r'-\d{4}', body)
            assert '<script>alert(1)</script>' not in body and '&lt;script&gt;' in body
            request_id = int(headers['Location'].split('/')[-1])
            for state in ['MOI', 'DA_HEN_LICH', 'DA_DUYET']:
                query('UPDATE yeu_cau_thue SET trang_thai=? WHERE id=?', (state, request_id))
                before_duplicate = {table: query(f'SELECT * FROM {table}') for table in
                                    ['yeu_cau_thue','khach_thue','rental_request_counter','nhat_ky_hoat_dong']}
                for retry_kind in ['XEM_PHONG', 'THUE_NGAY']:
                    code, duplicate, _ = tenant.post(post, dict(valid, **{'Form.LoaiYeuCau':retry_kind}), path)
                    duplicate = unescape(duplicate)
                    assert code == 200 and 'Bạn đã có yêu cầu đang mở' in duplicate
                    old_link = re.search(r'<a[^>]*id="open-existing-request"[^>]*href="([^"]+)"', duplicate).group(1)
                    assert old_link == f'/TinDang/YeuCau/{request_id}'
                    code, old_body, _ = tenant.request(old_link)
                    assert '<script>alert(1)</script>' not in old_body
                    old_body = unescape(old_body)
                    assert code == 200 and 'Thông tin yêu cầu' in old_body
                    assert query('SELECT ma_yeu_cau FROM yeu_cau_thue WHERE id=?', (request_id,))[0][0] in old_body
                    assert date.strftime('%d/%m/%Y') in old_body and 'Số người dự kiến: 2' in old_body
                assert all(query(f'SELECT * FROM {table}') == rows for table, rows in before_duplicate.items()), 'Duplicate changed data'
        rows = query('''SELECT r.loai_yeu_cau,r.tin_dang_id,k.tai_khoan_id,r.ma_yeu_cau,r.ngay_mong_muon,r.so_nguoi_du_kien,r.loi_nhan
                      FROM yeu_cau_thue r JOIN khach_thue k ON k.id=r.khach_thue_id ORDER BY r.id''')
        account = query("SELECT id FROM tai_khoan WHERE email='s206-tenant1@example.test'")[0][0]
        assert [r[0] for r in rows] == ['XEM_PHONG','THUE_NGAY']
        assert all(r[1:3] == (listing,account) and r[4:7] == (date.isoformat(),2,valid['Form.LoiNhan'])
                   for r, date in zip(rows, [today,last_day]))
        assert len({r[3] for r in rows}) == 2
        other = Browser(); assert other.login('s206-tenant2@example.test', password)[0] == 302
        assert other.request(locations[0])[0] == 404
        old_request = int(locations[-1].split('/')[-1])
        old_path = f'/TinDang/YeuCau/{old_request}'
        assert other.request(old_path)[0] == 404
        assert Browser().request(old_path)[0] == 302
        assert staff.request(old_path)[0] == 403
        assert other.request('/TinDang/YeuCau/999999')[0] == 404
        # Another account can send to A; the first account can send to a different listing B.
        assert other.post(post, valid, path)[0] == 302
        query("""INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
                 VALUES (?,'S206-102',1,25,2000000,1000000,4,'TRONG',?)""", (building, str(now)))
        room_b = query('SELECT max(id) FROM phong_tro')[0][0]
        query("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,ngay_dang,ngay_het_han,trang_thai,ngay_tao) VALUES (?,?,?,?,?,'DANG_HIEN_THI',?)",
              (room_b, admin, 'Tin B S2-06', str(now), str(now+timedelta(days=30)), str(now)))
        listing_b = query('SELECT max(id) FROM tin_dang')[0][0]
        assert tenant.post(f'/TinDang/GuiYeuCau/{listing_b}', valid, f'/TinDang/ChiTiet/{listing_b}')[0] == 302
        # All 1..4 are allowed after closing the prior request with DA_HUY.
        for people in [1, 3, 4]:
            query("UPDATE yeu_cau_thue SET trang_thai='DA_HUY'")
            code, _, headers = tenant.post(post, dict(valid, **{'Form.SoNguoiDuKien':people}), path)
            assert code == 302
            request_id = int(headers['Location'].split('/')[-1])
            assert query('SELECT so_nguoi_du_kien FROM yeu_cau_thue WHERE id=?', (request_id,)) == [(people,)]
        browsers = [Browser() for _ in range(4)]
        for b in browsers:
            assert b.login('s206-tenant1@example.test', password)[0] == 302
        def simultaneous(b):
            return b.post(post, valid, path)[0]
        query("UPDATE yeu_cau_thue SET trang_thai='DA_HUY'")
        count_before_concurrent = query('SELECT COUNT(*) FROM yeu_cau_thue')[0][0]
        counter_before_concurrent = query('SELECT so_cuoi FROM rental_request_counter WHERE thang=?', (month,))[0][0]
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            assert sorted(pool.map(simultaneous, browsers)) == [200,200,200,302]
        assert query('SELECT COUNT(*),COUNT(DISTINCT ma_yeu_cau) FROM yeu_cau_thue')[0] == (count_before_concurrent+1,)*2
        assert query('SELECT so_cuoi FROM rental_request_counter WHERE thang=?', (month,))[0][0] == counter_before_concurrent+1
        audit = query("SELECT du_lieu_sau FROM nhat_ky_hoat_dong WHERE loai_doi_tuong='yeu_cau_thue'")
        assert len(audit) == count_before_concurrent+1 and all('loi_nhan' not in r[0] for r in audit)
        query("UPDATE yeu_cau_thue SET trang_thai='DA_HUY'")
        # Force audited save failure; both request and allocated counter must roll back.
        count_before = query('SELECT COUNT(*) FROM yeu_cau_thue')[0][0]
        counter_before = query('SELECT so_cuoi FROM rental_request_counter WHERE thang=?', (month,))[0][0]
        query("CREATE TRIGGER s206_fail_audit BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='yeu_cau_thue' BEGIN SELECT RAISE(ABORT,'s206 audit fixture'); END")
        assert tenant.post(post, valid, path)[0] == 500
        assert query('SELECT COUNT(*) FROM yeu_cau_thue')[0][0] == count_before
        assert query('SELECT so_cuoi FROM rental_request_counter WHERE thang=?', (month,))[0][0] == counter_before
        query('DROP TRIGGER s206_fail_audit')
        query('UPDATE rental_request_counter SET so_cuoi=9999 WHERE thang=?', (month,))
        code, body, _ = tenant.post(post, valid, path)
        assert code == 200 and 'Đã hết mã yêu cầu' in unescape(body), (code, unescape(body))
        assert query('SELECT COUNT(*) FROM yeu_cau_thue')[0][0] == count_before
        query('UPDATE rental_request_counter SET so_cuoi=? WHERE thang=?', (counter_before,month))
        query("UPDATE tin_dang SET ngay_het_han=? WHERE id=?", (str(now-timedelta(days=1)),listing))
        code, expired_detail, _ = tenant.request(path)
        assert code == 200 and 'không còn hiển thị công khai' in unescape(expired_detail)
        assert 'id="gui-yeu-cau"' not in expired_detail
        assert tenant.request(old_path)[0] == 200, 'Old request must remain readable after listing expires'
        query("UPDATE tin_dang SET ngay_het_han=?,trang_thai='DANG_HIEN_THI' WHERE id=?", (str(now+timedelta(days=30)),listing))
        query("UPDATE phong_tro SET trang_thai='DANG_THUE' WHERE id=?", (room,))
        code, rented_detail, _ = tenant.request(path)
        assert code == 200 and 'không còn hiển thị công khai' in unescape(rented_detail)
        assert 'id="gui-yeu-cau"' not in rented_detail
        query("UPDATE phong_tro SET trang_thai='TRONG' WHERE id=?", (room,))
        assert query('PRAGMA integrity_check')[0][0] == 'ok'
        assert not query('PRAGMA foreign_key_check')
        # Leave one open request for the duplicate demo, with available monthly codes.
        assert tenant.post(post, valid, path)[0] == 302
        (FOLDER / 'access.json').write_text(json.dumps({'database':str(DB),'runtime':str(DLL.resolve()),'url':BASE,'listing':listing, 'listing_b':listing_b,
            'email':'s206-tenant1@example.test','password':password}, ensure_ascii=False, indent=2), encoding='utf-8')
        (ROOT / 'data/S2-06/latest.txt').write_text(str(FOLDER), encoding='utf-8')
        print('PASS: duplicates rejected for MOI/DA_HEN_LICH/DA_DUYET and both types without writes; reopen owned request, reject other accounts, expired listing still readable; different listing/account accepted, TU_CHOI/DA_HUY allow resend, four concurrent sends create one request/code/audit; capacity/date/validation/CSRF/roles, initialization/refuse overwrite, audit rollback/exhaustion, integrity/FK', flush=True)
    finally:
        p.terminate(); p.wait(timeout=15)

try:
    verify()
finally:
    if original:
        assert digest(SOURCE) == original, 'Source database changed'
        print('Source SHA-256 unchanged', flush=True)
