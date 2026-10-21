"""Create and verify an isolated S2-10 electricity/water configuration demo.

Build first:
  dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug -o data/s210-demo/runtime
Run from the repository root:
  python verification/prepare_s210_demo.py

The script never reads or writes the user's local database. It creates new files under
the git-ignored data/s210-demo directory and leaves the verified web server running.
"""
import datetime as dt
import html
import http.cookiejar
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "QL_PhongTro"
RUNTIME = ROOT / "data/s210-demo/runtime/QL_PhongTro.dll"
BASE_URL = "http://localhost:5251"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Browser:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, route, data=None):
        payload = urllib.parse.urlencode(data).encode() if data is not None else None
        try:
            response = self.opener.open(BASE_URL + route, payload, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, response.read().decode("utf-8"), response.headers

    def post(self, route, data, form=None):
        code, body, _ = self.request(form or route)
        assert code == 200, (form or route, code)
        token = field(body, "__RequestVerificationToken")
        return self.request(route, dict(data, __RequestVerificationToken=token))


def field(page, name):
    match = re.search(r'name="' + re.escape(name) + r'"[^>]*value="([^"]*)"', page)
    assert match, f"Missing form field: {name}"
    return html.unescape(match.group(1))


def main():
    assert RUNTIME.exists(), "Build the isolated demo runtime first."
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 5251))

    now = dt.datetime.now(dt.timezone(dt.timedelta(hours=7)))
    folder = ROOT / "data/s210-demo" / (now.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    base_database = folder / "base.sqlite"
    database = folder / "demo.sqlite"
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development",
               ASPNETCORE_URLS=BASE_URL, Logging__EventLog__LogLevel__Default="None",
               DataProtectionKeysPath=str(folder / "keys"),
               PasswordReset__PublicBaseUrl=BASE_URL,
               PasswordReset__PickupDirectory=str(folder / "mail"),
               IdentityImagePath=str(folder / "images"))

    def command(database_path, *args):
        command_env = dict(env, DatabasePath=str(database_path))
        result = subprocess.run(["dotnet", str(RUNTIME), *args], cwd=APP, env=command_env,
                                capture_output=True, text=True, encoding="utf-8",
                                errors="replace", timeout=120)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result.stdout

    command(base_database, "--initialize-database")
    output = command(base_database, "--create-permission-demo", str(database))
    password_match = re.search(r"Demo password \(all four accounts\): (.+)", output)
    assert password_match, output
    password = password_match.group(1).strip()
    command(database, "--update-database")
    command(database, "--check-database")

    def query(sql, params=()):
        with sqlite3.connect(database) as connection:
            connection.execute("PRAGMA foreign_keys=ON")
            return connection.execute(sql, params).fetchall()

    accounts = {}
    for email in re.findall(r"Login: (\S+)", output):
        account_id, role = query("SELECT id,vai_tro FROM tai_khoan WHERE email=?", (email,))[0]
        accounts[role] = {"id": account_id, "email": email}
    assert set(accounts) == {"ADMIN", "CHU_NHA", "QUAN_LY", "KHACH_THUE"}

    current_period = now.date().replace(day=1)
    next_period = (current_period.replace(day=28) + dt.timedelta(days=4)).replace(day=1)
    owner_id = accounts["CHU_NHA"]["id"]
    with sqlite3.connect(database) as connection:
        connection.execute("PRAGMA foreign_keys=ON")
        building = connection.execute(
            "INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) "
            "VALUES(?,?,?,?,1) RETURNING id",
            (owner_id, "Tòa demo cấu hình điện nước", "Địa chỉ giả lập S2-10", 1)).fetchone()[0]
        electricity = connection.execute(
            "INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES('DIEN','Điện',1) RETURNING id").fetchone()[0]
        water = connection.execute(
            "INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES('NUOC','Nước',1) RETURNING id").fetchone()[0]
        connection.executemany(
            "INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) VALUES(?,?,0)",
            [(building, electricity), (building, water)])
        connection.executemany(
            "INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,"
            "dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) VALUES(?,?,?,?,?,?,1,1,?,?)",
            [(building, electricity, "THEO_CHI_SO", "kWh", 4000, current_period.isoformat(), owner_id, now.isoformat()),
             (building, water, "THEO_NGUOI", "người/tháng", 80000, current_period.isoformat(), owner_id, now.isoformat())])
        connection.execute("INSERT INTO khoi_tao_dich_vu(toa_nha_id,ngay_tao) VALUES(?,?)", (building, now.isoformat()))
        connection.commit()

    log = (folder / "server.log").open("w", encoding="utf-8")
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    process = subprocess.Popen(["dotnet", str(RUNTIME)], cwd=APP,
                               env=dict(env, DatabasePath=str(database)), stdout=log, stderr=log,
                               creationflags=flags)
    try:
        owner = Browser()
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError((folder / "server.log").read_text(encoding="utf-8"))
            try:
                if owner.request("/Account/Login")[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.25)
        else:
            raise RuntimeError("Demo startup timed out.")

        login = owner.post("/Account/Login", {
            "TaiKhoanDangNhap": accounts["CHU_NHA"]["email"], "MatKhau": password
        })
        assert login[0] == 302, login[:2]
        route = f"/DichVu/DienNuoc?toaNhaId={building}"
        code, page, _ = owner.request(route)
        assert code == 200
        assert "Theo chỉ số đồng hồ" in page and "Khoán theo đầu người" in page
        assert f"Kỳ kế tiếp: {next_period:%m/%Y}" in page

        valid = {
            "ToaNhaId": str(building), "KyDaXem": field(page, "KyDaXem"),
            "TrangThaiDaXem": field(page, "TrangThaiDaXem"),
            "Dien.CachTinh": "THEO_NGUOI", "Dien.TienMotNguoi": "95000",
            "Nuoc.CachTinh": "THEO_CHI_SO", "Nuoc.DonGiaChiSo": "18000"
        }
        saved = owner.post("/DichVu/DienNuoc", valid, route)
        assert saved[0] == 302, saved[:2]
        rows = query("SELECT d.ma_dich_vu,c.cach_tinh,c.don_vi_tinh,c.don_gia,c.tu_ngay,c.den_ngay "
                     "FROM cau_hinh_dich_vu c JOIN dich_vu d ON d.id=c.dich_vu_id "
                     "WHERE c.toa_nha_id=? ORDER BY d.ma_dich_vu,c.tu_ngay", (building,))
        assert len(rows) == 4
        assert ("DIEN", "THEO_NGUOI", "người/tháng", 95000, next_period.isoformat(), None) in rows
        assert ("NUOC", "THEO_CHI_SO", "m³", 18000, next_period.isoformat(), None) in rows

        code, page, _ = owner.request(route)
        assert code == 200 and page.count(f"Áp dụng từ kỳ {next_period:%m/%Y}") >= 2
        before_invalid = rows
        invalid = {
            "ToaNhaId": str(building), "KyDaXem": field(page, "KyDaXem"),
            "TrangThaiDaXem": field(page, "TrangThaiDaXem"),
            "Dien.CachTinh": "THEO_NGUOI", "Dien.TienMotNguoi": "0",
            "Nuoc.CachTinh": "THEO_CHI_SO", "Nuoc.DonGiaChiSo": "18000"
        }
        rejected = owner.post("/DichVu/DienNuoc", invalid, route)
        (folder / "zero-price-response.html").write_text(rejected[1], encoding="utf-8")
        assert rejected[0] == 200 and "field-validation-error" in rejected[1]
        assert query("SELECT d.ma_dich_vu,c.cach_tinh,c.don_vi_tinh,c.don_gia,c.tu_ngay,c.den_ngay "
                     "FROM cau_hinh_dich_vu c JOIN dich_vu d ON d.id=c.dich_vu_id "
                     "WHERE c.toa_nha_id=? ORDER BY d.ma_dich_vu,c.tu_ngay", (building,)) == before_invalid
        assert query("PRAGMA integrity_check") == [("ok",)]
        assert not query("PRAGMA foreign_key_check")
        command(database, "--check-database")

        manifest = {
            "url": BASE_URL, "database": str(database), "pid": process.pid,
            "password": password, "accounts": accounts, "building_id": building,
            "building_name": "Tòa demo cấu hình điện nước",
            "current_period": current_period.strftime("%m/%Y"),
            "next_period": next_period.strftime("%m/%Y"),
            "current": {"electricity": "Theo chỉ số - 4.000 VND/kWh",
                        "water": "Theo đầu người - 80.000 VND/người/tháng"},
            "pending": {"electricity": "Theo đầu người - 95.000 VND/người/tháng",
                        "water": "Theo chỉ số - 18.000 VND/m³"}
        }
        (folder / "access.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        (ROOT / "data/s210-demo/latest.txt").write_text(str(folder), encoding="utf-8")
        print(json.dumps(manifest, ensure_ascii=True, indent=2), flush=True)
        print("PASS: UI fields/period, next-period persistence, zero-price rejection, integrity and FK. Demo remains running.", flush=True)
    except BaseException:
        process.terminate()
        process.wait(timeout=15)
        raise
    finally:
        log.close()


if __name__ == "__main__":
    main()
