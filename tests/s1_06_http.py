"""Run against a running app configured with a disposable SQLite copy only.
Usage: python tests/s1_06_http.py http://localhost:5247 data/S1-06-test.sqlite
"""
import html
import http.cookiejar
import re
import sqlite3
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

base, path = sys.argv[1:]
db = sqlite3.connect(path)
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

def request(route, data=None, client=opener):
    payload = None if data is None else urllib.parse.urlencode(data).encode()
    try:
        response = client.open(base + route, payload)
        return response.status, response.read().decode(), response.url
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode(), error.url

def token():
    status, body, _ = request('/HoSo')
    assert status == 200
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', body)[1])

anonymous = urllib.request.build_opener()
assert '/Account/Register' in request('/HoSo', client=anonymous)[2]
email = f'{uuid.uuid4().hex}@example.test'
phone = '0' + str(uuid.uuid4().int % 10**9).zfill(9)
assert request('/Account/Register', dict(HoTen='HTTP test', Email=email, SoDienThoai=phone, MatKhau='TestOnly106'))[0] == 200
account = db.execute('SELECT id FROM tai_khoan WHERE email=?', (email,)).fetchone()[0]
data = dict(HoTen='Hồ sơ thử', NgaySinh='2001-02-03', QueQuan='Huế', NgheNghiep='Sinh viên')
for value in ['012345678', '001234567890']:
    data.update(SoCanCuoc=value, __RequestVerificationToken=token())
    assert request('/HoSo', data)[0] == 200
    row = db.execute('SELECT ho_ten,ngay_sinh,so_giay_to,dia_chi_thuong_tru,nghe_nghiep FROM khach_thue WHERE tai_khoan_id=?', (account,)).fetchone()
    assert row == ('Hồ sơ thử', '2001-02-03', value, 'Huế', 'Sinh viên'), row
    page = request('/HoSo')[1]
    assert value not in page and ('*' * (len(value)-4) + value[-4:]) in page
for value in ['12345678', '1234567890', '12345678901', '1234567890123', '12345678a', '12345678!', '１２３４５６７８９', '123456789\n']:
    data.update(SoCanCuoc=value, __RequestVerificationToken=token())
    status, body, _ = request('/HoSo', data)
    assert status == 200 and 'field-validation-error' in body, value
    assert db.execute('SELECT so_giay_to FROM khach_thue WHERE tai_khoan_id=?', (account,)).fetchone()[0] == '001234567890'
data.update(HoTen='Đã sửa', NgaySinh='1999-12-31', QueQuan='Đà Nẵng', NgheNghiep='Kỹ sư', SoCanCuoc='000123456', __RequestVerificationToken=token(), TaiKhoanId='1', Id='1')
assert request('/HoSo', data)[0] == 200
assert db.execute('SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=?', (account,)).fetchone()[0] == 1
assert db.execute('SELECT ho_ten,ngay_sinh,so_giay_to,dia_chi_thuong_tru,nghe_nghiep FROM khach_thue WHERE tai_khoan_id=?', (account,)).fetchone() == ('Đã sửa', '1999-12-31', '000123456', 'Đà Nẵng', 'Kỹ sư')
assert db.execute('SELECT COUNT(*) FROM khach_thue WHERE tai_khoan_id=1').fetchone()[0] == 0
data.pop('__RequestVerificationToken')
assert request('/HoSo', data)[0] == 400
db.execute("UPDATE tai_khoan SET vai_tro='CHU_NHA' WHERE id=?", (account,)); db.commit()
assert urllib.parse.urlparse(request('/HoSo')[2]).path == '/'
db.close()
print('PASS: 9/12 digits, invalid server inputs, update/reopen, ownership, CSRF, anonymous and changed-role access.')
