"""Role/relationship integration demo. ONLY use a disposable SQLite copy.
Start app on that copy, then python tests/s1_06_permissions.py http://localhost:5247 data/S1-06-permissions.sqlite
Creates isolated sessions via existing registration; sets roles/leases in test DB only.
"""
import base64, datetime, html, http.cookiejar, pathlib, re, sqlite3, sys, uuid
import urllib.request, urllib.parse, urllib.error

base, path = sys.argv[1:3]
image_directory = pathlib.Path(sys.argv[3]) if len(sys.argv)>3 else None
db = sqlite3.connect(path)
db.execute('PRAGMA foreign_keys=ON')
today = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).date()

class Session:
    def __init__(self, role='KHACH_THUE'):
        self.client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        email = uuid.uuid4().hex+'@example.test'
        data = dict(HoTen='Permissions test', Email=email, SoDienThoai='0'+str(uuid.uuid4().int % 10**9).zfill(9), MatKhau='TestOnly106')
        assert self.request('/Account/Register',data)[0]==200
        self.id = db.execute('SELECT id FROM tai_khoan WHERE email=?',(email,)).fetchone()[0]
        db.execute('UPDATE tai_khoan SET vai_tro=? WHERE id=?',(role,self.id)); db.commit()
    def request(self, route, data=None):
        payload = None if data is None else urllib.parse.urlencode(data).encode()
        try:
            with self.client.open(base+route,payload) as r: return r.status,r.read().decode(),r.headers,r.url
        except urllib.error.HTTPError as e: return e.code,e.read().decode(),e.headers,e.url
    def save(self, number, **extra):
        body=self.request('/HoSo')[1]
        token=html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body)[1])
        data=dict(HoTen='Khách hồ sơ thử',NgaySinh='2000-01-02',SoCanCuoc=number,QueQuan='Huế',NgheNghiep='Sinh viên',__RequestVerificationToken=token)
        data.update(extra)
        return self.request('/HoSo',data)

tenant,other,owner,wrong,manager,admin = [Session(role) for role in ['KHACH_THUE','KHACH_THUE','CHU_NHA','CHU_NHA','QUAN_LY','ADMIN']]
assert tenant.save('012345678')[0]==200
profile=db.execute('SELECT id FROM khach_thue WHERE tai_khoan_id=?',(tenant.id,)).fetchone()[0]
building=db.execute('INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(?,?,?,?)',(owner.id,manager.id,'Test building','Test address')).lastrowid
room=db.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?, 'TEST',1,20,1000000,2,'DANG_THUE',?)",(building,str(today))).lastrowid
contract=db.execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao) VALUES(?,?,?,'DANG_HIEU_LUC',?,?)",(uuid.uuid4().hex,room,profile,owner.id,str(today))).lastrowid
period=db.execute('INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(?,1,?,?,1,1000000,?,?)',(contract,str(today-datetime.timedelta(days=1)),str(today+datetime.timedelta(days=30)),owner.id,str(today))).lastrowid
db.commit()

def check(viewer, number, full=False, target=profile):
    code,body,headers,_=viewer.request('/HoSo/Xem?id='+str(target))
    expected=number if full else '*'*(len(number)-4)+number[-4:]
    assert code==200 and expected in body, (viewer.id,full)
    assert 'no-store' in headers['Cache-Control']
    if not full: assert number not in body

for number in ['012345678','001234567890']:
    assert tenant.save(number)[0]==200
    for viewer in [tenant,other,wrong,manager]: check(viewer,number)
    for viewer in [owner,admin]: check(viewer,number,True)
    edit=tenant.request('/HoSo')[1]
    assert number not in edit and '*'*(len(number)-4)+number[-4:] in edit
    assert number not in tenant.save(number,HoTen='')[1]  # invalid form must not echo raw ModelState
    assert tenant.save('',NgheNghiep='Đã sửa')[0]==200
    assert db.execute('SELECT so_giay_to,nghe_nghiep FROM khach_thue WHERE id=?',(profile,)).fetchone()==(number,'Đã sửa')
    assert number not in other.request('/HoSo/Xem?id='+str(profile)+'&role=ADMIN&XemDayDu=true')[1]
    assert other.request('/HoSo/Anh?id='+str(profile)+'&mat=truoc')[0]==404

number='001234567890'
for status in ['NHAP','CHO_HIEU_LUC','DA_KET_THUC','DA_HUY']:
    db.execute('UPDATE hop_dong SET trang_thai=? WHERE id=?',(status,contract));db.commit();check(owner,number)
db.execute("UPDATE hop_dong SET trang_thai='DANG_HIEU_LUC' WHERE id=?",(contract,));db.commit()
for start,end in [(today-datetime.timedelta(days=30),today-datetime.timedelta(days=1)),(today+datetime.timedelta(days=1),today+datetime.timedelta(days=30))]:
    db.execute('UPDATE ky_hop_dong SET ngay_bat_dau=?,ngay_ket_thuc=? WHERE id=?',(str(start),str(end),period));db.commit();check(owner,number)
db.execute('UPDATE ky_hop_dong SET ngay_bat_dau=?,ngay_ket_thuc=? WHERE id=?',(str(today),str(today),period));db.commit();check(owner,number,True)
db.execute('UPDATE hop_dong SET ngay_tra_phong=? WHERE id=?',(str(today-datetime.timedelta(days=1)),contract));db.commit();check(owner,number)
db.execute('UPDATE hop_dong SET ngay_tra_phong=NULL WHERE id=?',(contract,))
db.execute('UPDATE toa_nha SET chu_nha_id=? WHERE id=?',(wrong.id,building));db.commit();check(owner,number);check(wrong,number,True)
db.execute('UPDATE toa_nha SET chu_nha_id=? WHERE id=?',(owner.id,building));db.commit()
db.execute("UPDATE tai_khoan SET vai_tro='KHACH_THUE' WHERE id=?",(admin.id,));db.commit();check(admin,number)
db.execute("UPDATE tai_khoan SET vai_tro='ADMIN',dang_hoat_dong=0 WHERE id=?",(admin.id,));db.commit()
assert urllib.parse.urlparse(admin.request('/HoSo/Xem?id='+str(profile))[3]).path=='/'
db.execute('UPDATE tai_khoan SET dang_hoat_dong=1 WHERE id=?',(admin.id,));db.commit();check(admin,number,True)

# Roommate must currently live in the same active lease, not merely have historical membership.
assert other.save('098765432')[0]==200
roommate=db.execute('SELECT id FROM khach_thue WHERE tai_khoan_id=?',(other.id,)).fetchone()[0]
db.execute('INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao,ky_bat_dau_tinh_phi) VALUES(?,?,?,?)',(contract,roommate,str(today),str(today.replace(day=1))));db.commit()
check(owner,'098765432',True,roommate)
db.execute('UPDATE nguoi_o_ghep SET ngay_chuyen_di=? WHERE khach_thue_id=?',(str(today),roommate));db.commit();check(owner,'098765432',False,roommate)
db.execute('ALTER TABLE hop_dong RENAME TO test_missing_hop_dong');db.commit();check(owner,number)
db.execute('ALTER TABLE test_missing_hop_dong RENAME TO hop_dong');db.commit();check(owner,number,True)
if image_directory:
    # Synthetic image fixture, never a real identity document.
    image_directory.mkdir(parents=True, exist_ok=True)
    image_name=uuid.uuid4().hex+'.png'
    pixels=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF2kAAAAASUVORK5CYII=')
    (image_directory/image_name).write_bytes(pixels)
    db.execute('UPDATE khach_thue SET anh_giay_to_truoc=? WHERE id=?',(image_name,profile));db.commit()
    for viewer in [tenant,owner,admin]:
        with viewer.client.open(base+'/HoSo/Anh?id='+str(profile)+'&mat=truoc') as response:
            assert response.status==200 and response.read()==pixels
    for viewer in [other,wrong,manager]:
        assert viewer.request('/HoSo/Anh?id='+str(profile)+'&mat=truoc')[0]==404
    db.execute('UPDATE tai_khoan SET dang_hoat_dong=0 WHERE id=?',(owner.id,));db.commit()
    assert urllib.parse.urlparse(owner.request('/HoSo/Anh?id='+str(profile)+'&mat=truoc')[3]).path=='/'
    db.execute('UPDATE tai_khoan SET dang_hoat_dong=1 WHERE id=?',(owner.id,));db.commit()
assert tenant.request('/HoSo/Xem?id=2147483647')[0]==404
anon=urllib.request.build_opener()
with anon.open(base+'/HoSo/Xem?id='+str(profile)) as response: assert '/Account/Register' in response.url
assert not db.execute('PRAGMA foreign_key_check').fetchall()
db.close()
print('PASS: 9/12 masking, tenant/other/wrong owner/manager, true owner/admin, server HTML, edit preservation, role refresh, disabled account, current lease/date/owner/roommate, missing schema fail-closed, anonymous access.')
