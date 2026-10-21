"""Requires Pillow; run ONLY against a disposable DB and image directory.
python tests/s1_06_images.py http://localhost:5247 data/S1-06-images.sqlite data/S1-06-images
"""
import html, http.cookiejar, io, pathlib, re, sqlite3, sys, uuid
import urllib.request, urllib.parse, urllib.error
from PIL import Image

base, database, directory = sys.argv[1:]
db = sqlite3.connect(database)
root = pathlib.Path(directory)
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

def request(path, data=None, content_type=None, opener=client):
    req = urllib.request.Request(base + path, data=data)
    if content_type: req.add_header('Content-Type', content_type)
    try:
        with opener.open(req) as r: return r.status, r.read(), r.headers, r.url
    except urllib.error.HTTPError as e: return e.code, e.read(), e.headers, e.url

def register():
    email = uuid.uuid4().hex + '@example.test'
    payload = dict(HoTen='Image test', Email=email, SoDienThoai='0'+str(uuid.uuid4().int % 10**9).zfill(9), MatKhau='TestOnly106')
    assert request('/Account/Register', urllib.parse.urlencode(payload).encode(), 'application/x-www-form-urlencoded')[0] == 200
    return db.execute('SELECT id FROM tai_khoan WHERE email=?', (email,)).fetchone()[0]

def picture(fmt, size):
    b = io.BytesIO(); Image.new('RGB', size, (80,140,200)).save(b,format=fmt); return b.getvalue()

def upload(files):
    body = request('/HoSo')[1].decode()
    token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body)[1])
    values = dict(HoTen='Image test', NgaySinh='2000-01-02', SoCanCuoc='001234567890', QueQuan='Huế', NgheNghiep='Sinh viên', __RequestVerificationToken=token)
    boundary = uuid.uuid4().hex
    chunks = []
    for name,value in values.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    for field,(name,data) in files.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{field}"; filename="{name}"\r\nContent-Type: image/jpeg\r\n\r\n'.encode()+data+b'\r\n')
    chunks.append(f'--{boundary}--\r\n'.encode())
    return request('/HoSo', b''.join(chunks), f'multipart/form-data; boundary={boundary}')

account = register()
def saved():
    return db.execute('SELECT anh_giay_to_truoc,anh_giay_to_sau FROM khach_thue WHERE tai_khoan_id=?',(account,)).fetchone()

assert upload({'AnhMatTruoc':('front.JPG',picture('JPEG',(2400,1200))), 'AnhMatSau':('back.png',picture('PNG',(800,500)))})[0] == 200
first = saved(); assert all(first)
assert Image.open(root/first[0]).size == (1600,800)
assert Image.open(root/first[1]).size == (800,500)
for side,mime in [('truoc','image/jpeg'),('sau','image/png')]:
    code,data,headers,_ = request('/HoSo/Anh?mat='+side)
    assert code == 200 and headers['Content-Type']==mime
    assert 'no-store' in headers['Cache-Control']
    Image.open(io.BytesIO(data)).verify()
assert b'/HoSo/Anh?mat=truoc' in request('/HoSo')[1]
for field in ['AnhMatTruoc','AnhMatSau']:
    for name,data in [('bad.gif',picture('GIF',(100,100))),('fake.jpg',b'not an image'),('renamed.jpg',picture('PNG',(100,100))),('large.jpg',b'x'*(5*1024*1024+1)),('empty.png',b'')]:
        before=saved(); count=len(list(root.iterdir()))
        other='AnhMatSau' if field=='AnhMatTruoc' else 'AnhMatTruoc'
        result=upload({field:(name,data),other:('valid.png',picture('PNG',(320,200)))})
        assert result[0]==200
        assert re.search(r'class="[^"]*field-validation-error[^\"]*"[^>]*data-valmsg-for="'+field+'"',result[1].decode()), (field,name)
        assert saved()==before and len(list(root.iterdir()))==count
edge=picture('PNG',(1600,900))
edge += b'\0'*(5*1024*1024-len(edge))
assert upload({'AnhMatTruoc':('edge.png',edge)})[0]==200
second=saved(); assert second[0]!=first[0] and second[1]==first[1]
assert not (root/first[0]).exists() and Image.open(root/second[0]).size==(1600,900)
assert upload({})[0]==200 and saved()==second
assert request('/App_Data/identity-images/'+second[0])[0]==404
anonymous=urllib.request.build_opener()
assert '/Account/Register' in request('/HoSo/Anh?mat=truoc',opener=anonymous)[3]
register()
assert request('/HoSo/Anh?mat=truoc&taiKhoanId='+str(account))[0]==404
db.close()
print('PASS: JPG/PNG, both sides, format/content errors, empty/over-5MB per side, exact-5MB, resize, no upscale, replacement, preservation, private access.')
