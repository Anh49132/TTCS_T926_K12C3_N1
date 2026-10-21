"""Create a new local demo copy with random demo credentials; never overwrite a file."""
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import secrets

sys.stdout.reconfigure(encoding='utf-8')

root = Path(__file__).resolve().parents[1]
app = root / 'QL_PhongTro'
dll = app / 'bin/Debug/net10.0/QL_PhongTro.dll'
target = (root / (sys.argv[1] if len(sys.argv) > 1 else 'data/s103-demo.sqlite')).resolve()
if target.exists():
    raise SystemExit('Destination exists. Reuse it, or pass a NEW path. Nothing changed.')
env = dict(os.environ)
env.pop('DatabasePath', None)
subprocess.run(['dotnet', str(dll), '--create-permission-demo', str(target)], cwd=app, env=env, check=True)
env['DatabasePath'] = str(target)
subprocess.run(['dotnet', str(dll), '--update-database'], cwd=app, env=env, check=True)
c = sqlite3.connect(target)
try:
    password_hash = c.execute("SELECT mat_khau FROM tai_khoan WHERE email LIKE 'demo-quan_ly-%@example.test' ORDER BY id DESC LIMIT 1").fetchone()[0]
    suffix = secrets.token_hex(4)
    for i in range(53):
        while True:
            phone = '09' + str(secrets.randbelow(100000000)).zfill(8)
            if not c.execute('SELECT 1 FROM tai_khoan WHERE so_dien_thoai=?', (phone,)).fetchone():
                break
        c.execute("""INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,ngay_tao,ngay_cap_nhat)
                     VALUES(?,?,?,?,'QUAN_LY',?,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP)""",
                  (f'Demo Quan ly {i+1:02}', f's103-{suffix}-{i}@example.test', phone, password_hash, int(i < 45)))
    c.commit()
finally:
    c.close()
print('Demo ready:', target)
print('Added 45 active + 8 locked manager fixtures. Original database unchanged.')
