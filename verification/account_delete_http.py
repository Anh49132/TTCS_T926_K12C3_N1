"""Account deletion/reuse regression: real HTTP, SQLite copy, pickup email only."""
import email
from html import unescape
from email import policy
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import tempfile
import time

import http.cookiejar
import hashlib
import urllib.request
import urllib.parse
import urllib.error
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
BASE = 'http://localhost:5263'

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None

class Browser:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, path, data=None, headers=None):
        raw = urllib.parse.urlencode(data).encode() if isinstance(data, dict) else data
        try:
            response = self.opener.open(urllib.request.Request(BASE+path, raw, headers or {}), timeout=20)
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
        return self.post('/Account/Login', dict(Identifier=address, Password=password))

def api(browser, path, data=None, token=None):
    headers = {'Content-Type':'application/json'}
    if token:
        headers['Authorization'] = 'Bearer '+token
    return browser.request(path, json.dumps(data or {}).encode(), headers)

CookieJar = http.cookiejar.CookieJar
http = SimpleNamespace(APP=APP, SOURCE=APP/'Data/local-dev.sqlite',
    DLL=Path(os.environ.get('QL_TEST_DLL', str(APP/'bin/Debug/net10.0/QL_PhongTro.dll'))), BASE=BASE, Browser=Browser, api=api,
    digest=lambda p: hashlib.sha256(p.read_bytes()).hexdigest(),
    cookiejar=SimpleNamespace(CookieJar=CookieJar))


def verify(folder):
    db = folder / 'test.sqlite'
    with sqlite3.connect(http.SOURCE.as_uri() + '?mode=ro', uri=True) as src:
        with sqlite3.connect(db) as dst:
            src.backup(dst)

    def query(sql, args=()):
        with sqlite3.connect(db) as c:
            c.execute('PRAGMA foreign_keys=ON')
            return c.execute(sql, args).fetchall()

    env = dict(os.environ, DatabasePath=str(db), ASPNETCORE_ENVIRONMENT='Development',
               ASPNETCORE_URLS=http.BASE, PasswordReset__PickupDirectory=str(folder / 'mail'),
               PasswordReset__PublicBaseUrl=http.BASE, LocalAdmin__Email='delete-admin@example.test',
               LocalAdmin__Phone='0911111100', LocalAdmin__Password='AdminTest123!')
    result = subprocess.run(['dotnet', str(http.DLL), '--create-local-admin'], cwd=http.APP,
                            env=env, capture_output=True)
    assert result.returncode == 0, result.stderr.decode(errors='replace')
    admin_id = query('SELECT id FROM tai_khoan WHERE email=?', (env['LocalAdmin__Email'],))[0][0]
    # Force a v4 fixture even after the source eventually upgrades to v5.
    query('DROP INDEX ux_account_email_normalized')
    query('DROP INDEX ux_account_phone')
    # v5 source may already contain reused contacts. Isolate old deleted fixture
    # contacts before recreating v4's unconditional indexes, on the copy only.
    query("UPDATE tai_khoan SET email='fixture-deleted-'||id||'@example.test',so_dien_thoai='deleted-'||id WHERE is_deleted=1")
    query('CREATE UNIQUE INDEX ux_account_email_normalized ON tai_khoan(lower(trim(email)))')
    query('CREATE UNIQUE INDEX ux_account_phone ON tai_khoan(so_dien_thoai)')
    query('DELETE FROM app_schema_version WHERE version=5')
    query("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat,is_deleted) SELECT 'Legacy deleted','legacy-delete@example.test','0911111101',mat_khau,'QUAN_LY',1,0,0,ngay_tao,ngay_cap_nhat,1 FROM tai_khoan WHERE id=?", (admin_id,))
    legacy = query("SELECT id FROM tai_khoan WHERE email='legacy-delete@example.test'")[0][0]
    query("INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES (?,?,'Delete fixture','Test')", (admin_id, legacy))
    building = query('SELECT max(id) FROM toa_nha')[0][0]
    history = query('SELECT * FROM nhat_ky_hoat_dong')
    profiles = query('SELECT * FROM khach_thue')
    historical_tables = [r[0] for r in query("SELECT name FROM sqlite_master WHERE type='table' AND name IN ('hop_dong','ky_hop_dong','hoa_don')")]
    historical_rows = {t: query('SELECT * FROM '+t) for t in historical_tables}
    # A schema we do not recognize must fail without changing any data/index.
    bad = folder / 'unexpected.sqlite'
    with sqlite3.connect(db) as src, sqlite3.connect(bad) as dst:
        src.backup(dst)
        dst.execute('CREATE UNIQUE INDEX unexpected_account_unique ON tai_khoan(email)')
    bad_hash = http.digest(bad)
    result = subprocess.run(['dotnet', str(http.DLL), '--update-database'], cwd=http.APP,
                            env=dict(env, DatabasePath=str(bad)), capture_output=True)
    assert result.returncode != 0
    assert b'Unexpected account UNIQUE index' in result.stderr
    assert http.digest(bad) == bad_hash

    def start():
        log = (folder / 'server.log').open('a', encoding='utf-8')
        p = subprocess.Popen(['dotnet', str(http.DLL)], cwd=http.APP, env=env, stdout=log, stderr=log)
        log.close()
        for _ in range(100):
            try:
                if http.Browser().request('/Account/Login')[0] == 200:
                    return p
            except OSError:
                pass
            if p.poll() is not None:
                raise AssertionError((folder / 'server.log').read_text(encoding='utf-8'))
            time.sleep(.2)
        p.terminate(); p.wait()
        raise AssertionError('Server startup timeout')

    def mail_body(address):
        messages = sorted((folder / 'mail').glob('*.eml'), key=lambda p: p.stat().st_mtime_ns)
        for path in reversed(messages):
            msg = email.message_from_bytes(path.read_bytes(), policy=policy.default)
            if msg['To'] == address:
                return msg.get_content()
        raise AssertionError('Missing pickup email')

    def register(address, phone):
        b = http.Browser()
        response = b.post('/Account/Register', dict(HoTen='Reuse tenant', Email=address,
            SoDienThoai=phone, MatKhau='TenantTest123!', XacNhanMatKhau='TenantTest123!'))
        assert response[0] == 302, response[:2]
        return b

    def confirm(b, address):
        code = re.search(r'\b\d{6}\b', mail_body(address))[0]
        assert b.post('/Account/ConfirmEmail', dict(Email=address, Code=code))[0] == 302

    p = start()
    try:
        assert query('SELECT max(version) FROM app_schema_version') == [(5,)]
        assert query('SELECT quan_ly_id FROM toa_nha WHERE id=?', (building,)) == [(None,)]
        assert query('SELECT dang_hoat_dong FROM tai_khoan WHERE id=?', (legacy,)) == [(0,)]
        assert len(list(folder.glob('*.before-account-reuse-*.bak'))) == 1
        with sqlite3.connect(next(folder.glob('test.sqlite.before-account-reuse-*.bak'))) as backup:
            assert backup.execute('SELECT max(version) FROM app_schema_version').fetchone() == (4,)
            assert backup.execute('SELECT quan_ly_id FROM toa_nha WHERE id=?',(building,)).fetchone() == (legacy,)
        admin = http.Browser()
        assert admin.login(env['LocalAdmin__Email'], env['LocalAdmin__Password'])[0] == 302
        for suffix in ['', '?status=locked', '?role=QUAN_LY&page=999']:
            body = admin.request('/ManagedAccounts'+suffix)[1]
            assert f'data-account-id="{legacy}"' not in body
        # Previously deleted contact pair works immediately, without a CLI update.
        address, phone = 'legacy-delete@example.test', '0911111101'
        tenant = register(address, phone)
        assert 'Chế độ thử nghiệm' in unescape(tenant.request('/Account/ConfirmEmail')[1])
        mail_dir = folder / 'mail'
        saved_mail = folder / 'saved-mail'
        mail_dir.rename(saved_mail)
        mail_dir.write_text('simulate delivery failure')
        try:
            assert tenant.post('/Account/ResendConfirmation',dict(email=address),'/Account/ConfirmEmail?email='+address)[0] == 302
            notice = unescape(tenant.request('/Account/ConfirmEmail')[1])
            assert 'Không gửi được mã' in notice
            assert 'mã mới đã được gửi' not in notice
        finally:
            mail_dir.unlink()
            saved_mail.rename(mail_dir)
        assert tenant.post('/Account/ResendConfirmation',dict(email=address),'/Account/ConfirmEmail?email='+address)[0] == 302
        assert 'Chế độ thử nghiệm' in unescape(tenant.request('/Account/ConfirmEmail')[1])
        pending_rows = query('SELECT id,mat_khau FROM tai_khoan WHERE email=? AND is_deleted=0',(address,))
        retry = http.Browser()
        response = retry.post('/Account/Register',dict(HoTen='Retry',Email=address,SoDienThoai=phone,
            MatKhau='DifferentPass123!',XacNhanMatKhau='DifferentPass123!'))
        assert response[0] == 302 and response[2]['Location'] == '/Account/ConfirmEmail'
        assert query('SELECT id,mat_khau FROM tai_khoan WHERE email=? AND is_deleted=0',(address,)) == pending_rows
        assert address in retry.request('/Account/ConfirmEmail')[1]
        # Matching only one contact must not resume a different pending account.
        assert retry.post('/Account/Register',dict(HoTen='Retry',Email=address,SoDienThoai='0911111199',
            MatKhau='DifferentPass123!',XacNhanMatKhau='DifferentPass123!'))[0] == 200
        assert http.api(http.Browser(), '/api/auth/login', {'email':address, 'matKhau':'TenantTest123!'})[0] == 401
        confirm(tenant, address)
        current = query('SELECT id FROM tai_khoan WHERE email=? AND is_deleted=0', (address,))[0][0]
        assert current != legacy
        # SQLite itself must reject each duplicate independently, even if validation is bypassed.
        for email_value, phone_value in [(address.upper(), '0911111198'), ('different@example.test', phone)]:
            try:
                query("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat) SELECT ho_ten,?,?,mat_khau,vai_tro,1,0,0,ngay_tao,ngay_cap_nhat FROM tai_khoan WHERE id=?", (email_value,phone_value,current))
            except sqlite3.IntegrityError:
                pass
            else:
                raise AssertionError('SQLite accepted duplicate live credentials')
        assert tenant.login(phone, 'TenantTest123!')[0] == 302
        tenant_home = unescape(tenant.request('/')[1])
        assert 'data-menu-module="PHONG_TRO"' not in tenant_home
        assert 'Quản lý tòa nhà' not in tenant_home
        assert 'Hồ sơ cá nhân' in tenant_home
        for path in ['/PhongTro','/PhongTro/ToaNha']:
            assert tenant.request(path)[0] == 403
            assert admin.request(path)[0] == 200
        assert tenant.post('/PhongTro/TaoToaNha',{},'/Account/ChangePassword')[0] == 403
        code, body, _ = http.api(http.Browser(), '/api/auth/login', {'email':address, 'matKhau':'TenantTest123!'})
        assert code == 200, body
        tokens = json.loads(body)
        assert int(tokens['userId']) == current
        assert tenant.post('/Account/ForgotPassword', {'Email':address})[0] in (200,302)
        old_token = re.search(r'token=([A-Za-z0-9_-]+)', mail_body(address))[1]
        assert query('SELECT account_id FROM password_reset_token WHERE used_at IS NULL ORDER BY expires_at DESC LIMIT 1') == [(current,)]
        # Keep a historical profile attached to the old account.
        query("INSERT INTO khach_thue(tai_khoan_id,ho_ten,ngay_tao) VALUES (?,'Historical tenant','2026-09-30')", (current,))
        profile = query('SELECT * FROM khach_thue WHERE tai_khoan_id=?', (current,))
        assert tenant.post('/ManagedAccounts/Delete', {'id':current,'confirmed':'true'}, '/Account/ChangePassword')[0] == 403
        assert admin.request('/ManagedAccounts/Delete', {'id':current,'confirmed':'true'})[0] == 400
        assert admin.post('/ManagedAccounts/Delete', {'id':current}, '/ManagedAccounts')[0] == 400
        assert admin.post('/ManagedAccounts/Delete', {'id':admin_id,'confirmed':'true'}, '/ManagedAccounts')[0] == 400
        # Inject an audit failure: all business changes must roll back.
        query("CREATE TRIGGER test_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test rollback'); END")
        assert admin.post('/ManagedAccounts/Delete', {'id':current,'confirmed':'true'}, '/ManagedAccounts')[0] == 500
        assert query('SELECT is_deleted FROM tai_khoan WHERE id=?',(current,)) == [(0,)]
        query('DROP TRIGGER test_audit_failure')
        assert admin.post('/ManagedAccounts/Delete', {'id':current,'confirmed':'true'}, '/ManagedAccounts')[0] == 302
        assert not json.loads(tenant.request('/Account/SessionStatus')[1])['authenticated']
        assert http.api(http.Browser(), '/api/auth/me', token=tokens['accessToken'])[0] == 401
        assert http.api(http.Browser(), '/api/auth/refresh', {'refreshToken':tokens['refreshToken']})[0] == 401
        assert admin.post('/ManagedAccounts/SetActive', {'id':current,'active':'true'}, '/ManagedAccounts')[0] == 404
        assert admin.post('/ManagedAccounts/Delete', {'id':current,'confirmed':'true'}, '/ManagedAccounts')[0] == 404
        assert f'data-account-id="{current}"' not in admin.request('/ManagedAccounts')[1]
        assert query('SELECT * FROM khach_thue WHERE tai_khoan_id=?', (current,)) == profile
        replacement = register(address, phone)
        confirm(replacement, address)
        newer = query('SELECT id FROM tai_khoan WHERE email=? AND is_deleted=0', (address,))[0][0]
        assert newer not in (legacy, current)
        assert replacement.login(address.upper(), 'TenantTest123!')[0] == 302
        old_hash = query('SELECT mat_khau FROM tai_khoan WHERE id=?',(current,))
        assert http.Browser().post('/Account/ResetPassword', {'Token':old_token,'NewPassword':'ResetTest456!','ConfirmPassword':'ResetTest456!'}, '/Account/Login')[0] == 400
        assert query('SELECT mat_khau FROM tai_khoan WHERE id=?',(current,)) == old_hash
        assert replacement.post('/Account/ForgotPassword', {'Email':address})[0] in (200,302)
        reset_token = re.search(r'token=([A-Za-z0-9_-]+)', mail_body(address))[1]
        assert query('SELECT account_id FROM password_reset_token WHERE used_at IS NULL ORDER BY expires_at DESC LIMIT 1') == [(newer,)]
        assert http.Browser().post('/Account/ResetPassword', {'Token':reset_token,'NewPassword':'ResetTest456!','ConfirmPassword':'ResetTest456!'}, '/Account/ResetPassword?token='+reset_token)[0] == 302
        assert http.Browser().login(phone, 'ResetTest456!')[0] == 302
        # ADMIN creation and manager unassignment; locked contacts remain reserved.
        managed = dict(HoTen='Delete manager',Email='delete-manager@example.test',SoDienThoai='0911111102',VaiTro='QUAN_LY')
        assert admin.post('/ManagedAccounts/Create', managed)[0] == 302
        manager = query('SELECT id FROM tai_khoan WHERE email=? AND is_deleted=0',(managed['Email'],))[0][0]
        query('UPDATE toa_nha SET quan_ly_id=? WHERE id=?',(manager,building))
        query("CREATE TRIGGER test_audit_failure BEFORE INSERT ON nhat_ky_hoat_dong BEGIN SELECT RAISE(ABORT,'test rollback'); END")
        assert admin.post('/ManagedAccounts/Delete', {'id':manager,'confirmed':'true'}, '/ManagedAccounts')[0] == 500
        assert query('SELECT quan_ly_id FROM toa_nha WHERE id=?',(building,)) == [(manager,)]
        assert query('SELECT is_deleted FROM tai_khoan WHERE id=?',(manager,)) == [(0,)]
        query('DROP TRIGGER test_audit_failure')
        assert admin.post('/ManagedAccounts/SetActive', {'id':manager,'active':'false','confirmed':'true'}, '/ManagedAccounts')[0] == 302
        assert admin.post('/ManagedAccounts/Create', managed)[0] == 200
        assert admin.post('/ManagedAccounts/Delete', {'id':manager,'confirmed':'true'}, '/ManagedAccounts')[0] == 302
        assert query('SELECT quan_ly_id FROM toa_nha WHERE id=?',(building,)) == [(None,)]
        assert admin.post('/ManagedAccounts/Create', managed)[0] == 302
        assert query('SELECT count(*) FROM tai_khoan WHERE email=?',(managed['Email'],)) == [(2,)]
        assert admin.post('/ManagedAccounts/Create', dict(managed,Email=managed['Email'].upper()))[0] == 200
        for status, condition in [('', ''), ('active', ' AND dang_hoat_dong=1'), ('locked', ' AND dang_hoat_dong=0')]:
            expected = [row[0] for row in query('SELECT id FROM tai_khoan WHERE is_deleted=0'+condition+' ORDER BY id DESC')]
            for page in range(1, max(1,(len(expected)+19)//20)+1):
                body = admin.request(f'/ManagedAccounts?status={status}&page={page}')[1]
                assert [int(x) for x in re.findall(r'data-account-id="(\d+)"',body)] == expected[(page-1)*20:page*20]
        pending_address = 'expired-delete@example.test'
        pending = register(pending_address, '0911111103')
        pending_id = query('SELECT id FROM tai_khoan WHERE email=?',(pending_address,))[0][0]
        query('UPDATE email_confirmation SET expires_at=0 WHERE account_id=?',(pending_id,))
        assert pending.post('/Account/ConfirmEmail',dict(Email=pending_address,Code='000000'))[0] == 200
        assert query('SELECT is_deleted FROM tai_khoan WHERE id=?',(pending_id,)) == [(1,)]
        pending = register(pending_address, '0911111103')
        confirm(pending, pending_address)
        assert query('PRAGMA integrity_check') == [('ok',)]
        assert query('PRAGMA foreign_key_check') == []
        assert query('SELECT * FROM nhat_ky_hoat_dong ORDER BY id')[:len(history)] == history
        assert query('SELECT * FROM khach_thue ORDER BY id')[:len(profiles)] == profiles
        for table, rows in historical_rows.items():
            assert query('SELECT * FROM '+table) == rows
        p.terminate(); p.wait(timeout=15)
        before = query('SELECT * FROM tai_khoan')
        p = start()
        assert query('SELECT * FROM tai_khoan') == before
        assert len(list(folder.glob('*.before-account-reuse-*.bak'))) == 1
        p.terminate(); p.wait(timeout=15)
        saved_hash = http.digest(db)
        for command in ['--update-database', '--check-database']:
            result = subprocess.run(['dotnet',str(http.DLL),command],cwd=http.APP,env=env,capture_output=True)
            assert result.returncode == 0, result.stderr.decode(errors='replace')
        assert http.digest(db) == saved_hash
        print('PASS: automatic v5 + legacy repair, HTTP delete/reuse, confirmation, login/reset, authorization, tokens, manager buildings, rollback, history/FKs, restart idempotence')
    finally:
        p.terminate(); p.wait(timeout=15)


if __name__ == '__main__':
    before = http.digest(http.SOURCE)
    folder = Path(tempfile.mkdtemp(prefix='account-delete-'))
    print('Artifacts:', folder, flush=True)
    try:
        verify(folder)
    finally:
        assert http.digest(http.SOURCE) == before, 'Source database changed!'
        print('Source database SHA-256 unchanged')
