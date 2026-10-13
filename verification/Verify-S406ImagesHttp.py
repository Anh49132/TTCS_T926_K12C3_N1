"""Runs only against the dedicated fake S4-06 demo; intentionally creates reports."""
import html, json, runpy, sqlite3, sys, urllib.error, urllib.request, uuid
from pathlib import Path

scope = runpy.run_path(str(Path(__file__).with_name('Verify-S406Http.py')))
tenant, other, guest = (scope[n] for n in ('tenant', 'other', 'guest'))
folder, access, base = (scope[n] for n in ('folder', 'access', 'base'))
# Tiny generated fixture photos; no personal or external files.
png = __import__("base64").b64decode("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGOsmLaFgYGBiQEMABUGAcbKcAAtAAAAAElFTkSuQmCC")
jpg = __import__("base64").b64decode("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDQooor0jgP/9k=")

def raw(session, path, data=None, headers=None):
    request = urllib.request.Request(base + path, data, headers or {})
    try: response = session.opener.open(request, timeout=30)
    except urllib.error.HTTPError as error: response = error
    with response: return response.status, response.read(), response.headers

def upload(fields, files):
    boundary = 'repair-' + uuid.uuid4().hex
    chunks = []
    for key, value in fields.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
    for name, contents in files:
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="Input.Images"; filename="{name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + contents + b'\r\n')
    chunks.append(f'--{boundary}--\r\n'.encode())
    return raw(tenant, '/BaoHong/Create', b''.join(chunks), {'Content-Type': f'multipart/form-data; boundary={boundary}'})

def count():
    with sqlite3.connect(access['database']) as db: return db.execute('SELECT COUNT(*) FROM bao_hong').fetchone()[0]

before = count()
for files in ([('x.png', png)] * 4, [('x.png', png + bytes(5242881-len(png)))], [('x.gif', png)], [('x.jpg', b'not an image')], [('x.jpg', png)]):
    _, fields = tenant.form()
    status, body, _ = upload(fields, files)
    assert status == 200 and ('Input.Images' in body.decode())
    assert count() == before

image_root = Path(access['database'] + '.repair-images')
assert not image_root.exists(), 'Use a fresh fake demo for the storage-failure test'
image_root.write_bytes(b'block directory creation')
try:
    _, fields = tenant.form()
    status, body, _ = upload(fields, [('x.png',png)])
    assert status == 200 and 'Không lưu được ảnh' in html.unescape(body.decode()), (status,html.unescape(body.decode())[-4000:])
    assert count() == before
finally: image_root.unlink()

created = []
for files in ([('one.jpeg', jpg)], [('one.png',png), ('two.jpg',jpg), ('limit.png',png + bytes(5242880-len(png)))]):
    _, fields = tenant.form()
    status, body, headers = upload(fields, files)
    assert status == 302, (status,body[:1000])
    location = headers['Location']; created.append(location)
    assert upload(fields,files)[2]['Location'] == location
    report_id = location.rsplit('/',1)[-1]
    detail = tenant.request(location)[1]
    assert detail.count('alt="Ảnh thiết bị') == len(files)
    for order, (_, contents) in enumerate(files,1):
        path = f'/BaoHong/Image/{report_id}?order={order}'
        status, actual, headers = raw(tenant,path)
        assert status == 200 and actual == contents
        assert headers['Content-Type'] in ('image/png','image/jpeg')
        assert 'no-store' in headers['Cache-Control'] and headers['X-Content-Type-Options'] == 'nosniff'
        assert raw(other,path)[0] == 403
        assert raw(guest,path)[0] in (302,401)
    assert raw(tenant,f'/BaoHong/Image/{report_id}?order=4')[0] == 404
    assert upload(fields,[])[0] == 200
assert count() == before + 2
with sqlite3.connect(access['database']) as db:
    keys = [r[0] for r in db.execute('SELECT khoa_luu FROM anh_bao_hong')]
    assert len(keys) == 4 and len(list(image_root.iterdir())) == 4
    assert not db.execute('pragma foreign_key_check').fetchall()
    assert db.execute('pragma integrity_check').fetchone()[0] == 'ok'
assert raw(guest,'/uploads/rooms/' + keys[0])[0] == 404
report = {'passed': True, 'details': created, 'checked': ['no-image regression', '1/3 photos', '4 rejected', 'exact 5 MiB', 'over 5 MiB', 'wrong format/content', 'storage failure rollback', 'retry no duplicates', 'image bytes', 'private 403', 'anonymous challenge', 'no-store/nosniff', 'integrity/FK']}
(folder / 'image-http-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
