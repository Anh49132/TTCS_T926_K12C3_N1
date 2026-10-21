"""Read-only SQL validation plus optional local HTTP smoke with private account file."""
from pathlib import Path
import sqlite3,json,sys,bcrypt,time,re,html,http.cookiejar,urllib.request,urllib.parse,urllib.error
p=Path(sys.argv[1]).resolve();db=sqlite3.connect(p.as_uri()+'?mode=ro',uri=True)
assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
assert not db.execute('PRAGMA foreign_key_check').fetchall()
counts={t:db.execute(f'SELECT COUNT(*) FROM {t}').fetchone()[0] for t in ['toa_nha','phong_tro','hop_dong','hoa_don','anh_phong','khach_thue','tai_khoan','chi_so_dien_nuoc','nguoi_o_ghep','tin_dang','bao_hong','yeu_cau_thue']}
assert [counts[t] for t in ['toa_nha','phong_tro','hop_dong','hoa_don','anh_phong']]==[10,480,280,840,1440]
assert db.execute('SELECT COUNT(*) FROM (SELECT DISTINCT nam,thang FROM hoa_don)').fetchone()[0]==3
assert not db.execute('SELECT h.id FROM hoa_don h JOIN chi_tiet_hoa_don c ON c.hoa_don_id=h.id GROUP BY h.id HAVING h.tong_tien<>SUM(CASE WHEN c.loai_khoan=\'GIAM_TRU\' THEN -c.thanh_tien ELSE c.thanh_tien END)').fetchall()
assert not db.execute('SELECT h.id FROM hoa_don h JOIN thanh_toan t ON t.hoa_don_id=h.id AND t.trang_thai=\'DA_XAC_NHAN\' GROUP BY h.id HAVING SUM(t.so_tien)>h.tong_tien').fetchall()
assert not db.execute('SELECT phong_id FROM hop_dong GROUP BY phong_id HAVING COUNT(*)>1').fetchall()
assert not db.execute('SELECT id FROM chi_so_dien_nuoc WHERE CAST(chi_so_cuoi AS NUMERIC)<CAST(chi_so_dau AS NUMERIC) OR da_khoa<>1').fetchall()
assert not db.execute("SELECT t.id FROM tin_dang t JOIN phong_tro p ON p.id=t.phong_id WHERE t.trang_thai='DANG_HIEN_THI' AND p.trang_thai<>'TRONG'").fetchall()
assets=0
for a,b in db.execute('SELECT duong_dan,duong_dan_anh_nho FROM anh_phong'):
 for x in [a,b]:assert (Path(__file__).resolve().parents[1]/'QL_PhongTro/wwwroot'/x.lstrip('/')).is_file();assets+=1
accounts=json.loads((p.parent/'access.json').read_text(encoding='utf-8'))
for a in (accounts if len(sys.argv)==2 else accounts[:4]):
 hashed=db.execute('SELECT mat_khau FROM tai_khoan WHERE id=?',(a['id'],)).fetchone()[0]
 assert bcrypt.checkpw(a['password'].encode(),hashed.encode())
debt_sql="""SELECT h.id,h.hop_dong_id,h.tong_tien-COALESCE(t.paid,0) debt,h.han_thanh_toan,COALESCE(t.paid,0) paid FROM hoa_don h LEFT JOIN (SELECT hoa_don_id,SUM(so_tien) paid FROM thanh_toan WHERE trang_thai='DA_XAC_NHAN' GROUP BY hoa_don_id)t ON t.hoa_don_id=h.id WHERE h.trang_thai='DA_PHAT_HANH'"""
debt=db.execute(debt_sql).fetchall()
report=dict(counts=counts,roles=dict(db.execute('SELECT vai_tro,COUNT(*) FROM tai_khoan GROUP BY vai_tro')),totalDebt=sum(r[2] for r in debt),paidInvoices=sum(r[2]==0 for r in debt),partialInvoices=sum(r[2]>0 and r[4]>0 for r in debt),unpaidInvoices=sum(r[2]>0 and r[4]==0 for r in debt),pendingReports=db.execute("SELECT COUNT(*) FROM thanh_toan WHERE trang_thai='CHO_XAC_NHAN'").fetchone()[0],assetsVerified=assets,passwordsVerified=len(accounts),integrity='ok',foreignKeys='ok',http=[])
if len(sys.argv)>2:
 base=sys.argv[2];assert urllib.parse.urlparse(base).hostname in ('localhost','127.0.0.1')
 class NoRedirect(urllib.request.HTTPRedirectHandler):
  def redirect_request(self,*a):return None
 def request(opener,url,data=None):
  req=urllib.request.Request(base+url,urllib.parse.urlencode(data).encode() if data else None)
  try:r=opener.open(req,timeout=25)
  except urllib.error.HTTPError as e:r=e
  with r:return r.status,html.unescape(r.read().decode('utf-8'))
 routes={'CHU_NHA':['/','/HoSo/CuaToi','/HoaDonDichVu/Management','/HoaDonDichVu/Details/3','/CongNo','/BaoCao','/TinDang/QuanLy'], 'KHACH_THUE':['/','/ThongBao/DanhSach','/ThongBao/HoaDon/3','/BaoHong','/HopDong'], 'QUAN_LY':['/','/QuanLyBaoHong','/ChiSoDienNuoc'], 'ADMIN':['/','/ManagedAccounts','/Permissions','/NhatKy','/EmailGuiLog']}
 for role,paths in routes.items():
  a=next(x for x in accounts if x['role']==role);op=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
  status,page=request(op,'/Account/Login');token=re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',page).group(1)
  status,_=request(op,'/Account/Login',dict(TaiKhoanDangNhap=a['email'],MatKhau=a['password'],__RequestVerificationToken=token));assert status==302,(role,'login',status)
  for url in paths:
   start=time.perf_counter();status,page=request(op,url);report['http'].append(dict(role=role,path=url,status=status,ms=round((time.perf_counter()-start)*1000)));assert status in (200,302),(role,url,status)
  if role=='QUAN_LY':assert request(op,'/HoaDonDichVu/Management')[0]==403
  if role=='CHU_NHA':assert request(op,'/Permissions')[0]==403
  if role=='CHU_NHA':assert request(op,'/HoaDonDichVu/Details/169')[0]==403
  if role=='KHACH_THUE':assert request(op,'/ThongBao/HoaDon/4')[0]==403
  if role=='QUAN_LY':assert request(op,'/QuanLyBaoHong/Details/11')[0]==403
(p.parent/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
