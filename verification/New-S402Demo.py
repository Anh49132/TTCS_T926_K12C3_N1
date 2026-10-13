"""Create disposable, synthetic S4-02 demo. Never opens the user's existing database.
Run after stopping the project app and building Debug. Requires existing Python bcrypt.
"""
from pathlib import Path
from datetime import datetime, timedelta, timezone
import bcrypt
import json
import os
import secrets
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "QL_PhongTro"
OUT = ROOT / "data" / "s402-demo" / datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
OUT.mkdir(parents=True, exist_ok=False)
DB = OUT / "demo.sqlite"
env = dict(os.environ, DatabasePath=str(DB), ASPNETCORE_ENVIRONMENT="Development")
runtime = APP / "bin/Debug/net10.0/QL_PhongTro.dll"
if not runtime.exists():
    raise RuntimeError("Build Debug first; this script does not build a running app.")
for command in ("--initialize-database", "--initialize-rental-requests", "--initialize-service-invoices"):
    result = subprocess.run(["dotnet", str(runtime), command], cwd=APP, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
    (OUT / (command[2:] + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
    if result.returncode:
        raise RuntimeError(f"{command} failed; inspect logs in {OUT}")
today = datetime.now(timezone(timedelta(hours=7))).date()
utc = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
password = "Demo-" + secrets.token_urlsafe(15) + "1a"
hashed = bcrypt.hashpw(password.encode(), bcrypt.gensalt(rounds=10)).decode()
accounts = [(1,"Chủ nhà kiểm thử S4-02","owner","CHU_NHA"),(2,"Chủ nhà khác","other","CHU_NHA"),
            (3,"Khách thuê kiểm thử","tenant","KHACH_THUE"),(4,"Quản lý kiểm thử","manager","QUAN_LY"),
            (5,"Quản trị kiểm thử","admin","ADMIN")]
with sqlite3.connect(DB) as c:
    c.execute("PRAGMA foreign_keys=ON")
    for ident,name,login,role in accounts:
        c.execute("""INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                     VALUES(?,?,?,?,?,?,1,1,?,?)""",(ident,name,login+"@s402.example.test",f"090000000{ident}",hashed,role,utc,utc))
    c.execute("INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(1,1,4,'Demo S4-02','Địa chỉ kiểm thử')")
    c.execute("""INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao,phien_ban)
                 VALUES(1,1,'A103',1,'25',2000000,2000000,2,'DANG_THUE',?,0)""",(utc,))
    c.execute("INSERT INTO khach_thue(id,tai_khoan_id,ho_ten,ngay_tao) VALUES(1,3,'Khách thuê kiểm thử',?)",(utc,))
    c.execute("""INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,nguoi_lap_id,ngay_tao)
                 VALUES(1,'HD-S402-DEMO',1,1,'DANG_HIEU_LUC',1,?)""",(utc,))
    c.execute("""INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
                 VALUES(1,1,?,?,12,2000000,1,?)""",(f"{today.year}-01-01",f"{today.year}-12-31",utc))
    c.execute("""INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,
                 ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
                 VALUES(1,'HD-S402-0051',1,?,?,?,?,?,1,?,?,3000000,'NHAP',1)""",
              (today.month,today.year,today.replace(day=1).isoformat(),today.isoformat(),today.isoformat(),utc,(today+timedelta(days=7)).isoformat()))
    for index,(name,amount) in enumerate([("Tiền phòng",2000000),("Điện",520000),("Nước",230000),("Internet",150000),("Rác",50000),("Vệ sinh",50000)],1):
        c.execute("""INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,loai_khoan,ten_khoan,don_vi_tinh,so_luong,don_gia,thanh_tien)
                     VALUES(1,?,?,?,?,1,?,?)""",(index,"TIEN_PHONG" if index==1 else "DICH_VU",name,"tháng",amount,amount))
    c.execute("UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh=?,ngay_phat_hanh_nghiep_vu=?,nguoi_phat_hanh_id=1 WHERE id=1",(utc,today.isoformat()))
    c.execute("""INSERT INTO thong_bao(hoa_don_id,nguoi_nhan_id,loai_thong_bao,tieu_de,noi_dung,duong_dan,email_nhan,ngay_tao,trang_thai_email)
                 VALUES(1,3,'HOA_DON_MOI','Hóa đơn kiểm thử','Dữ liệu demo S4-02','/ThongBao/ChiTiet?maHoaDon=HD-S402-0051','tenant@s402.example.test',?,'DA_HUY')""",(utc,))
    assert c.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert not c.execute("PRAGMA foreign_key_check").fetchall()
(OUT / "access.json").write_text(json.dumps({"database":str(DB),"accounts":[{"role":role,"email":login+"@s402.example.test","password":password} for _,_,login,role in accounts]},ensure_ascii=False,indent=2),encoding="utf-8")
(OUT / "README.txt").write_text("DB giả độc lập. Hóa đơn ID 1: 3.000.000 đ, chưa thu. Đăng nhập chủ nhà từ access.json.\nĐường dẫn: /HoaDonDichVu/Details/1\n",encoding="utf-8")
print("S4-02 synthetic demo created:",OUT)
