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
BASE = 'http://localhost:5259'

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

def api(browser, path, data=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    return browser.request(path, json.dumps(data or {}).encode(), headers)

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def verify(folder):
    db = folder / 'test.sqlite'
    src = sqlite3.connect(SOURCE.as_uri() + '?mode=ro', uri=True)
    dst = sqlite3.connect(db)
    src.backup(dst)
    src.close(); dst.close()
    env = dict(os.environ, DatabasePath=str(db), ASPNETCORE_ENVIRONMENT='Development',
               ASPNETCORE_URLS=BASE, PasswordReset__PickupDirectory=str(folder / 'mail'),
               PasswordReset__PublicBaseUrl=BASE)
    result = subprocess.run(['dotnet', str(DLL), '--update-database'], cwd=APP, env=env, capture_output=True)
    assert result.returncode == 0, result.stderr.decode(errors='replace')
    log = (folder / 'server.log').open('w', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(DLL)], cwd=APP, env=env, stdout=log, stderr=log)
    def query(sql, args=()):
        c = sqlite3.connect(db)
        try:
            rows = c.execute(sql, args).fetchall(); c.commit(); return rows
        finally:
            c.close()
    try:
        admin = Browser()
        for _ in range(60):
            try:
                assert admin.request('/Account/Login')[0] == 200
                break
            except (OSError, AssertionError):
                if process.poll() is not None:
                    raise AssertionError((folder / 'server.log').read_text(encoding='utf-8'))
                time.sleep(.25)
        stamp = str(time.time_ns())
        admin_email = 'admin' + stamp + '@example.test'
        assert admin.post('/Account/Register', {'HoTen': 'Admin test', 'Email': admin_email,
            'SoDienThoai': '0981234567', 'MatKhau': 'TestAdmin123!', 'XacNhanMatKhau': 'TestAdmin123!'})[0] == 302
        query("UPDATE tai_khoan SET vai_tro='ADMIN' WHERE email=?", (admin_email,))
        assert admin.request('/ManagedAccounts')[0] == 200
        create = {'HoTen': 'Chủ nhà thử nghiệm', 'Email': 'owner'+stamp+'@example.test',
                  'SoDienThoai': '0981234568', 'VaiTro': 'CHU_NHA'}
        assert admin.post('/ManagedAccounts/Create', dict(create, Email='not-an-email'))[0] == 200
        assert admin.post('/ManagedAccounts/Create', dict(create, SoDienThoai='123'))[0] == 200
        assert admin.post('/ManagedAccounts/Create', dict(create, VaiTro='ADMIN'))[0] == 200
        assert admin.request('/ManagedAccounts/Create', create)[0] == 400  # CSRF
        assert admin.post('/ManagedAccounts/Create', create)[0] == 302
        assert admin.post('/ManagedAccounts/Create', dict(create, Email=create['Email'].upper()))[0] == 200
        account_id = query('SELECT id FROM tai_khoan WHERE email=?', (create['Email'],))[0][0]
        assert query('SELECT must_change_password FROM tai_khoan WHERE id=?', (account_id,)) == [(1,)]
        messages = list((folder / 'mail').glob('*.eml'))
        assert len(messages) == 1
        message = email.message_from_bytes(messages[0].read_bytes(), policy=policy.default)
        assert message['To'] == create['Email']
        body = message.get_body(preferencelist=('plain',)).get_content() if message.is_multipart() else message.get_content()
        temporary = re.search(r'Mật khẩu tạm: (\S+)', body)[1]
        assert len(temporary) == 16 and all(re.search(p, temporary) for p in ['[A-Z]', '[a-z]', '[0-9]', '[!@#$%&*?]'])
        assert temporary not in admin.request('/ManagedAccounts')[1]
        owner = Browser()
        code, _, headers = owner.login(create['Email'], temporary)
        assert code == 302 and headers['Location'] == '/Account/ChangePassword'
        for path in ['/', '/ManagedAccounts', '/HoSo', '/Account/Register', '/Account/ForgotPassword']:
            code, _, headers = owner.request(path)
            assert code == 302 and headers['Location'] == '/Account/ChangePassword', path
        assert api(owner, '/api/auth/me')[0] == 403
        assert api(Browser(), '/api/auth/login', {'email': create['Email'], 'matKhau': temporary})[0] == 401
        assert owner.post('/Account/ChangePassword', {'CurrentPassword': temporary, 'NewPassword': 'weak', 'ConfirmPassword': 'weak'})[0] == 200
        assert owner.post('/Account/ChangePassword', {'CurrentPassword': temporary, 'NewPassword': temporary, 'ConfirmPassword': temporary})[0] == 200
        new_password = 'OwnerNew123!'
        assert owner.post('/Account/ChangePassword', {'CurrentPassword': temporary, 'NewPassword': new_password, 'ConfirmPassword': new_password})[0] == 302
        assert query('SELECT must_change_password FROM tai_khoan WHERE id=?', (account_id,)) == [(0,)]
        assert owner.login(create['Email'], temporary)[0] == 200  # rejected
        assert owner.login(create['Email'], new_password)[0] == 302
        assert owner.request('/')[0] == 200
        assert owner.request('/ManagedAccounts')[0] == 403
        assert owner.post('/ManagedAccounts/SetActive', {'id': account_id, 'active': 'false', 'confirmed': 'true'}, '/Account/ChangePassword')[0] == 403
        admin_id = query('SELECT id FROM tai_khoan WHERE email=?', (admin_email,))[0][0]
        assert admin.post('/ManagedAccounts/SetActive', {'id': admin_id, 'active': 'false', 'confirmed': 'true'}, '/ManagedAccounts')[0] == 400
        old_cookie = Browser()
        for cookie in owner.jar:
            old_cookie.jar.set_cookie(copy.copy(cookie))
        api_client = Browser()
        code, response, _ = api(api_client, '/api/auth/login', {'email': create['Email'], 'matKhau': new_password})
        assert code == 200, response
        tokens = json.loads(response)
        assert api(api_client, '/api/auth/me', token=tokens['accessToken'])[0] == 200
        assert admin.post('/ManagedAccounts/SetActive', {'id': account_id, 'active': 'false'}, '/ManagedAccounts')[0] == 400
        started = time.monotonic()
        assert admin.post('/ManagedAccounts/SetActive', {'id': account_id, 'active': 'false', 'confirmed': 'true'}, '/ManagedAccounts')[0] == 302
        assert json.loads(owner.request('/Account/SessionStatus')[1])['authenticated'] is False
        assert api(api_client, '/api/auth/me', token=tokens['accessToken'])[0] == 401
        assert api(api_client, '/api/auth/refresh', {'refreshToken': tokens['refreshToken']})[0] == 401
        assert time.monotonic() - started < 60
        assert Browser().login(create['Email'], new_password)[0] == 200
        assert admin.post('/ManagedAccounts/SetActive', {'id': account_id, 'active': 'true'}, '/ManagedAccounts')[0] == 302
        assert api(api_client, '/api/auth/me', token=tokens['accessToken'])[0] == 401
        assert owner.login(create['Email'], new_password)[0] == 302
        assert json.loads(old_cookie.request('/Account/SessionStatus')[1])['authenticated'] is False
        # Delivery failure keeps the account pending and offers a safe retry.
        mail = folder / 'mail'
        mail.rename(folder / 'saved-mail')
        mail.write_text('simulate inaccessible pickup directory')
        manager = dict(create, Email='manager'+stamp+'@example.test', SoDienThoai='0981234569', VaiTro='QUAN_LY')
        assert admin.post('/ManagedAccounts/Create', manager)[0] == 302
        assert 'chưa gửi được email' in unescape(admin.request('/ManagedAccounts')[1])
        manager_id = query('SELECT id FROM tai_khoan WHERE email=?', (manager['Email'],))[0][0]
        mail.unlink()
        (folder / 'saved-mail').rename(mail)
        assert admin.post('/ManagedAccounts/Resend', {'id': manager_id}, '/ManagedAccounts')[0] == 302
        manager_message = next(email.message_from_bytes(p.read_bytes(), policy=policy.default) for p in mail.glob('*.eml')
                               if manager['Email'] in email.message_from_bytes(p.read_bytes(), policy=policy.default)['To'])
        manager_temp = re.search(r'Mật khẩu tạm: (\S+)', manager_message.get_content())[1]
        manager_browser = Browser()
        assert manager_browser.login(manager['Email'], manager_temp)[0] == 302
        assert admin.post('/ManagedAccounts/Resend', {'id': manager_id}, '/ManagedAccounts')[0] == 302
        assert json.loads(manager_browser.request('/Account/SessionStatus')[1])['authenticated'] is False
        assert manager_browser.login(manager['Email'], manager_temp)[0] == 200
        newest = max(mail.glob('*.eml'), key=lambda p: p.stat().st_mtime_ns)
        latest_message = email.message_from_bytes(newest.read_bytes(), policy=policy.default)
        latest_temp = re.search(r'Mật khẩu tạm: (\S+)', latest_message.get_content())[1]
        assert manager_browser.login(manager['Email'], latest_temp)[0] == 302
        assert manager_browser.post('/Account/ChangePassword', {'CurrentPassword': latest_temp, 'NewPassword': 'ManagerNew123!', 'ConfirmPassword': 'ManagerNew123!'})[0] == 302
        assert manager_browser.login(manager['Email'], 'ManagerNew123!')[0] == 302
        assert admin.post('/ManagedAccounts/SetActive', {'id': manager_id, 'active': 'false', 'confirmed': 'true'}, '/ManagedAccounts')[0] == 302
        assert json.loads(manager_browser.request('/Account/SessionStatus')[1])['authenticated'] is False
        assert manager_browser.login(manager['Email'], 'ManagerNew123!')[0] == 200
        # Isolated fixtures for first/middle/last pages and combined filtering.
        seed_hash = query('SELECT mat_khau FROM tai_khoan WHERE id=?', (account_id,))[0][0]
        for i in range(45):
            query("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,ngay_tao,ngay_cap_nhat) VALUES(?,?,?,?,?,?,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)",
                  ('Manager '+str(i), f'manager{i}{stamp}@example.test', f'0700000{i:03}', seed_hash, 'QUAN_LY', 1))
        for role in ['ADMIN', 'CHU_NHA', 'QUAN_LY', 'KHACH_THUE']:
            expected_role = [r[0] for r in query('SELECT id FROM tai_khoan WHERE vai_tro=? ORDER BY id DESC LIMIT 20', (role,))]
            html = admin.request(f'/ManagedAccounts?role={role}')[1]
            assert [int(x) for x in re.findall(r'data-account-id="(\d+)"', html)] == expected_role
            for status, active in [('active', 1), ('locked', 0)]:
                expected = [r[0] for r in query('SELECT id FROM tai_khoan WHERE vai_tro=? AND dang_hoat_dong=? ORDER BY id DESC', (role, active))]
                pages = max(1, (len(expected)+19)//20)
                for page in range(1, pages+1):
                    code, html, _ = admin.request(f'/ManagedAccounts?role={role}&status={status}&page={page}')
                    assert code == 200
                    actual = [int(x) for x in re.findall(r'data-account-id="(\d+)"', html)]
                    assert actual == expected[(page-1)*20:page*20]
        for status, active in [('active', 1), ('locked', 0)]:
            expected_status = [r[0] for r in query('SELECT id FROM tai_khoan WHERE dang_hoat_dong=? ORDER BY id DESC LIMIT 20', (active,))]
            html = admin.request(f'/ManagedAccounts?status={status}')[1]
            assert [int(x) for x in re.findall(r'data-account-id="(\d+)"', html)] == expected_status
        assert query('PRAGMA integrity_check') == [('ok',)]
        assert not query('PRAGMA foreign_key_check')
        print('PASS S1-03: create/validation/duplicate/CSRF/email; forced password+API bypass; new login; lock/cookie/JWT/refresh/unlock; combined filters and 20-row pagination; integrity/FK')
    finally:
        process.terminate()
        process.wait(timeout=20)
        log.close()

before = digest(SOURCE)
with tempfile.TemporaryDirectory(prefix='s103-') as folder:
    verify(Path(folder))
assert digest(SOURCE) == before
print('PASS: original database unchanged')
