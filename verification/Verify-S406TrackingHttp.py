"""Mutating checks for a dedicated fake demo-tracking-* only; no screenshots."""
import html, json, re, runpy, sqlite3, struct, sys, urllib.error, urllib.request, zlib
from pathlib import Path

folder = Path(sys.argv[1]).resolve()
assert folder.parent.name == 's406' and folder.name.startswith('demo-tracking-'), 'Dedicated tracking demo required'
scope = runpy.run_path(str(Path(__file__).with_name('Verify-S406Http.py')))
Session, tenant, base, access = (scope[n] for n in ('Session', 'tenant', 'base', 'access'))
manager = Session(); manager.login('manager.demo@demo.local')
outsider = Session(); outsider.login('manager.other.demo@demo.local')

def raw(session, path, data=None, headers=None):
    request = urllib.request.Request(base + path, data, headers or {})
    try: response = session.opener.open(request, timeout=30)
    except urllib.error.HTTPError as error: response = error
    with response: return response.status, response.read(), response.headers

def hidden(page, name):
    return re.search(r'name="' + re.escape(name) + r'"[^>]*value="([^"]*)"', page).group(1)

def update_form(id):
    status, page, _ = manager.request(f'/QuanLyBaoHong/Details/{id}'); assert status == 200
    return {'__RequestVerificationToken': hidden(page, '__RequestVerificationToken'),
            'CapNhat.PhienBan': hidden(page, 'CapNhat.PhienBan'),
            'CapNhat.KhoaChongTrung': hidden(page, 'CapNhat.KhoaChongTrung'),
            'CapNhat.TrangThai': 'DANG_XU_LY', 'CapNhat.GhiChu': 'Đã kiểm tra, đang hẹn thợ.'}

def reply_form(id, message):
    status, page, _ = manager.request(f'/QuanLyBaoHong/Details/{id}'); assert status == 200
    return {'__RequestVerificationToken': hidden(page, '__RequestVerificationToken'),
            'PhanHoi.KhoaChongTrung': hidden(page, 'PhanHoi.KhoaChongTrung'), 'PhanHoi.NoiDung': message}

# Generate a tiny PNG using the standard library, then exercise real multipart upload.
def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind+data))
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b'\x00\x78\x96\xb4')) + chunk(b'IEND', b'')
_, fields = tenant.form(); fields['Input.MoTa'] = 'Demo trạng thái và phản hồi của quản lý.'
boundary = 'tracking-demo-upload'
parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode() for key, value in fields.items()]
parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="Input.Images"; filename="equipment.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+png+b'\r\n')
parts.append(f'--{boundary}--\r\n'.encode())
status, _, headers = raw(tenant, '/BaoHong/Create', b''.join(parts), {'Content-Type': f'multipart/form-data; boundary={boundary}'})
assert status == 302; id = int(headers['Location'].rsplit('/', 1)[-1])
detail_url = f'/BaoHong/Details/{id}'; manager_url = f'/QuanLyBaoHong/Details/{id}'
assert manager_url in manager.request('/QuanLyBaoHong')[1]
assert manager.request('/Modules/BAO_HONG')[2]['Location'] == '/QuanLyBaoHong'
assert raw(manager, f'/QuanLyBaoHong/Image/{id}?order=1')[1] == png
assert raw(outsider, f'/QuanLyBaoHong/Image/{id}?order=1')[0] == 403
assert outsider.request(manager_url)[0] == 403
assert manager_url not in outsider.request('/QuanLyBaoHong')[1]
assert manager.request('/QuanLyBaoHong/Details/999999')[0] == 404

data = update_form(id)
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', {k:v for k,v in data.items() if k != '__RequestVerificationToken'})[0] == 400
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', dict(data, **{'CapNhat.TrangThai':'HOAN_THANH'}))[0] == 200
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', dict(data, **{'CapNhat.GhiChu':'   '}))[0] == 200
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', data)[0] == 302
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', data)[0] == 302
stale = dict(data, **{'CapNhat.KhoaChongTrung': __import__('uuid').uuid4().hex})
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', stale)[0] == 409
assert 'Đang xử lý' in tenant.request(detail_url)[1]

first = reply_form(id, 'Lịch sửa lúc 09:00 ngày mai. <script>alert(1)</script>')
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', dict(first, **{'PhanHoi.NoiDung':' \t '}))[0] == 200
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', first)[0] == 302
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', first)[0] == 302
second = reply_form(id, 'Đã kiểm tra lại, thiết bị hoạt động tốt.')
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', second)[0] == 302
data = update_form(id); data['CapNhat.TrangThai'] = 'HOAN_THANH'; data['CapNhat.GhiChu'] = 'Đã sửa xong.'
assert manager.request(f'/QuanLyBaoHong/CapNhat/{id}', data)[0] == 302
final = reply_form(id, 'Khách có thể sử dụng thiết bị bình thường.')
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', final)[0] == 302
page = tenant.request(detail_url)[1]
assert 'Hoàn thành' in page and 'Lịch sửa lúc' in page and 'Đã kiểm tra lại' in page and 'Khách có thể sử dụng' in page
assert page.index('Lịch sửa lúc') < page.index('Đã kiểm tra lại') < page.index('Khách có thể sử dụng')
assert '<script>alert(1)</script>' not in raw(tenant, detail_url)[1].decode()  # Razor encodes response content.
assert 'PhanHoi.NoiDung' not in page and 'CapNhat.TrangThai' not in page
assert 'Lưu trạng thái' not in manager.request(manager_url)[1]

# Mutations below are restricted to the explicitly guarded fake demo.
with sqlite3.connect(access['database']) as db:
    db.execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=1"); db.commit()
assert 'Khách có thể sử dụng' in tenant.request(detail_url)[1]
assert raw(tenant, f'/BaoHong/Image/{id}?order=1')[1] == png
assert manager.request(f'/QuanLyBaoHong/PhanHoi/{id}', reply_form(id, 'Phản hồi sau khi khách trả phòng.'))[0] == 302

_, foreign_page, _ = outsider.request('/QuanLyBaoHong')
foreign_token = hidden(foreign_page, '__RequestVerificationToken')
forged = dict(first, **{'__RequestVerificationToken': foreign_token})
assert outsider.request(f'/QuanLyBaoHong/PhanHoi/{id}', forged)[0] == 403
changed = dict(data, **{'__RequestVerificationToken': foreign_token})
assert outsider.request(f'/QuanLyBaoHong/CapNhat/{id}', changed)[0] == 403

for email in ('tenant1.demo@demo.local', 'owner.demo@demo.local', 'admin.demo@demo.local'):
    denied = Session(); denied.login(email); assert denied.request('/QuanLyBaoHong')[0] == 403
other_tenant = Session(); other_tenant.login('tenant3.demo@demo.local'); assert other_tenant.request(detail_url)[0] == 403
assert 'no-store' in tenant.request(detail_url)[2]['Cache-Control']
assert 'no-store' in raw(manager, f'/QuanLyBaoHong/Image/{id}?order=1')[2]['Cache-Control']
with sqlite3.connect(access['database']) as db:
    assert db.execute('SELECT COUNT(*) FROM bao_hong_trao_doi WHERE bao_hong_id=?', (id,)).fetchone()[0] == 6
    assert db.execute('pragma integrity_check').fetchone()[0] == 'ok'
    assert not db.execute('pragma foreign_key_check').fetchall()
report = {'passed': True, 'tenant_detail': detail_url, 'manager_detail': manager_url, 'checked': ['manager list/module route', 'images/scope 403', 'legal/illegal transitions', 'blank note/reply', 'CSRF', 'retry', 'stale 409', 'multiple ordered replies', 'Razor escaping', 'final status replies', 'expired contract history/images', 'role 403', 'integrity/FK']}
(folder/'tracking-http-verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
