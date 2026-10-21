"""HTTP checks against the isolated room-notes-demo-v25 copy only."""
from pathlib import Path
import re,uuid,urllib.request,urllib.error,sqlite3,html
ns={'__file__':str(Path('verification/verify_sprint2_demo.py').resolve()),'__name__':'check'}
exec(compile(Path('verification/verify_sprint2_demo.py').read_text(encoding='utf-8-sig').split('with sqlite3.connect')[0],ns['__file__'],'exec'),ns)
database=Path('data/room-notes-demo-v25/notes.sqlite')
assert database.exists()
s=ns['Session']();s.login('owner.demo@demo.local');status,page=s.get('/PhongTro');url=re.search(r'href="([^"]*NhatKyPhong[^"]*)"',page).group(1);status,page=s.get(url)
form=re.search(r'<form[^>]*action="/PhongTro/LuuGhiChuPhong"[^>]*>.*?</form>',page,re.S).group(0)
token=re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',form).group(1)
room=re.search(r'name="phongId"[^>]*value="([^"]+)"',form).group(1)
def post(fields,filename=None,filedata=b''):
 boundary='----notes'+uuid.uuid4().hex;parts=[]
 for name,value in fields.items():parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
 if filename:parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="tep"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode()+filedata+b'\r\n')
 parts.append(f'--{boundary}--\r\n'.encode());request=urllib.request.Request(ns['base']+'/PhongTro/LuuGhiChuPhong',b''.join(parts),headers={'Content-Type':'multipart/form-data; boundary='+boundary})
 try:response=s.opener.open(request)
 except urllib.error.HTTPError as error:response=error
 with response:
  return response.status
key=str(uuid.uuid4());fields={'phongId':room,'tieuDe':'Kiểm thử ghi chú riêng tư','noiDung':'Nội dung cần giữ riêng trong ghi chú.','ghim':'true','khoaChongTrung':key,'__RequestVerificationToken':token}
assert post(fields,'kiem-tra.pdf',b'%PDF-1.4\n%%EOF')==302
with sqlite3.connect(database) as db:
 note=db.execute('select id,ghim,noi_dung_tep from ghi_chu_phong where khoa_chong_trung=?',(key,)).fetchone();assert note and note[1]==1
 audit=db.execute("select du_lieu_sau from nhat_ky_hoat_dong where loai_doi_tuong='ghi_chu_phong' and doi_tuong_id=? order by id desc limit 1",(note[0],)).fetchone()[0];assert 'Nội dung cần giữ riêng' not in audit
assert post(fields,'kiem-tra.pdf',b'%PDF-1.4\n%%EOF')==302
with sqlite3.connect(database) as db:assert db.execute('select count(*) from ghi_chu_phong where khoa_chong_trung=?',(key,)).fetchone()[0]==1
status,page=s.get(url);assert status==200 and fields['noiDung'] in page
status,result=s.get('/PhongTro/TepGhiChuPhong?id='+str(note[0]));assert status==200 and result.startswith('%PDF-')
invalid=dict(fields,khoaChongTrung=str(uuid.uuid4()));assert post(invalid,'gia.pdf',b'not a pdf')==302
with sqlite3.connect(database) as db:assert not db.execute('select 1 from ghi_chu_phong where khoa_chong_trung=?',(invalid['khoaChongTrung'],)).fetchone()
assert post(dict(fields,__RequestVerificationToken=''))==400
anonymous=ns['Session']();status,_=anonymous.get('/PhongTro/TepGhiChuPhong?id='+str(note[0]));assert status in (302,401,403)
with sqlite3.connect(database) as db:
 foreign=db.execute('select p.id from phong_tro p join toa_nha b on b.id=p.toa_nha_id where b.chu_nha_id<>(select chu_nha_id from toa_nha b2 join phong_tro p2 on p2.toa_nha_id=b2.id where p2.id=?) limit 1',(room,)).fetchone()
 assert foreign
assert post(dict(fields,phongId=str(foreign[0]),khoaChongTrung=str(uuid.uuid4())))==403
print('PASS: save, pin, attachment, duplicate prevention, audit privacy, invalid file, CSRF and foreign-room denial')

rollback_key=str(uuid.uuid4())
with sqlite3.connect(database) as db:db.execute("CREATE TRIGGER notes_test_audit_fail BEFORE INSERT ON nhat_ky_hoat_dong WHEN NEW.loai_doi_tuong='ghi_chu_phong' BEGIN SELECT RAISE(ABORT,'test audit failure'); END")
try:
 assert post(dict(fields,khoaChongTrung=rollback_key))==302
 with sqlite3.connect(database) as db:assert not db.execute('select 1 from ghi_chu_phong where khoa_chong_trung=?',(rollback_key,)).fetchone()
finally:
 with sqlite3.connect(database) as db:db.execute('DROP TRIGGER notes_test_audit_fail')
print('PASS: audit failure rolls back note insertion')
