"""Create an isolated room-image demo and leave it running on localhost:5259.

Build first:
  dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug -o data/room-images-demo/runtime
Run from repository root:
  python verification/prepare_room_images_demo.py

The script creates a fresh database and storage folder under git-ignored
data/room-images-demo. It never reads or writes the user's local database.
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
import struct
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zlib


ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "QL_PhongTro"
RUNTIME = ROOT / "data/room-images-demo/runtime/QL_PhongTro.dll"
BASE_URL = "http://localhost:5259"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Browser:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, route, data=None, headers=None):
        payload = urllib.parse.urlencode(data).encode() if data is not None else None
        request = urllib.request.Request(BASE_URL + route, payload, headers=headers or {})
        try:
            response = self.opener.open(request, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, response.read().decode("utf-8", errors="replace"), response.headers

    def post(self, route, data, form=None):
        code, body, _ = self.request(form or route)
        assert code == 200, (form or route, code)
        token = field(body, "__RequestVerificationToken")
        return self.request(route, dict(data, __RequestVerificationToken=token))

    def multipart(self, route, field_name, filename, content_type, content, form=None):
        code, body, _ = self.request(form or route)
        assert code == 200, (form or route, code)
        token = field(body, "__RequestVerificationToken")
        boundary = "----room-image-demo-" + uuid.uuid4().hex
        parts = []
        parts.append(
            f"--{boundary}\r\n"
            'Content-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n'
            f"{token}\r\n".encode())
        parts.append(
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="{field_name}"; filename="{filename}"\r\n'
            f"Content-Type: {content_type}\r\n\r\n".encode() + content + b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        request = urllib.request.Request(
            BASE_URL + route,
            b"".join(parts),
            headers={"Content-Type": "multipart/form-data; boundary=" + boundary})
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, response.read().decode("utf-8", errors="replace"), response.headers


def field(page, name):
    match = re.search(r'name="' + re.escape(name) + r'"[^>]*value="([^"]*)"', page)
    assert match, f"Missing form field: {name}"
    return html.unescape(match.group(1))


def png(width, height, rgb):
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xffffffff)

    raw = b"".join(b"\x00" + bytes(rgb) * width for _ in range(height))
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(raw, 6))
        + chunk(b"IEND", b""))


def main():
    assert RUNTIME.exists(), "Build the isolated demo runtime first."
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 5259))

    now = dt.datetime.now(dt.timezone(dt.timedelta(hours=7)))
    folder = ROOT / "data/room-images-demo" / (now.strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:6])
    folder.mkdir(parents=True, exist_ok=False)
    base_database = folder / "base.sqlite"
    database = folder / "demo.sqlite"
    storage = folder / "uploads" / "rooms"
    env = dict(
        os.environ,
        ASPNETCORE_ENVIRONMENT="Development",
        ASPNETCORE_URLS=BASE_URL,
        Logging__EventLog__LogLevel__Default="None",
        DataProtectionKeysPath=str(folder / "keys"),
        RoomImagesPath=str(storage),
        PasswordReset__PublicBaseUrl=BASE_URL,
        PasswordReset__PickupDirectory=str(folder / "mail"),
        IdentityImagePath=str(folder / "identity-images"))

    def command(database_path, *args, check=True):
        result = subprocess.run(
            ["dotnet", str(RUNTIME), *args],
            cwd=APP,
            env=dict(env, DatabasePath=str(database_path)),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120)
        if check and result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        return result

    def query(sql, params=()):
        with sqlite3.connect(database) as connection:
            connection.execute("PRAGMA foreign_keys=ON")
            return connection.execute(sql, params).fetchall()

    command(base_database, "--initialize-database")
    output = command(base_database, "--create-permission-demo", str(database)).stdout
    password = re.search(r"Demo password \(all four accounts\): (.+)", output).group(1).strip()
    command(database, "--update-database")
    command(database, "--initialize-rental-requests")
    command(database, "--check-database")

    accounts = {}
    for email in re.findall(r"Login: (\S+)", output):
        account_id, role = query("SELECT id,vai_tro FROM tai_khoan WHERE email=?", (email,))[0]
        accounts[role] = {"id": account_id, "email": email}
    assert "CHU_NHA" in accounts

    log = (folder / "server.log").open("w", encoding="utf-8")
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    process = subprocess.Popen(
        ["dotnet", str(RUNTIME)],
        cwd=APP,
        env=dict(env, DatabasePath=str(database)),
        stdout=log,
        stderr=log,
        creationflags=flags)
    try:
        owner = Browser()
        for _ in range(120):
            if process.poll() is not None:
                raise RuntimeError((folder / "server.log").read_text(encoding="utf-8", errors="replace"))
            try:
                if owner.request("/Account/Login")[0] == 200:
                    break
            except OSError:
                pass
            time.sleep(.25)
        else:
            raise RuntimeError("Demo startup timed out.")

        login = owner.post("/Account/Login", {
            "TaiKhoanDangNhap": accounts["CHU_NHA"]["email"],
            "MatKhau": password
        })
        assert login[0] == 302, login[:2]

        assert owner.post("/PhongTro/TaoToaNha", {
            "TenToaNha": "Demo anh phong",
            "DiaChi": "Dia chi demo",
            "SoTang": "2"
        })[0] == 302
        owner_id = accounts["CHU_NHA"]["id"]
        building = query("SELECT id FROM toa_nha WHERE chu_nha_id=? ORDER BY id DESC LIMIT 1", (owner_id,))[0][0]
        create = owner.post("/PhongTro/Create", {
            "ToaNhaId": str(building),
            "MaPhong": "IMG-101",
            "Tang": "1",
            "DienTich": "28",
            "GiaThueDisplay": "3500000",
            "SoNguoiToiDa": "4",
            "TrangThai": "TRONG"
        }, f"/PhongTro/Create?toaNhaId={building}")
        assert create[0] == 302, create[:2]
        room = query("SELECT id FROM phong_tro WHERE toa_nha_id=? AND ma_phong='IMG-101'", (building,))[0][0]

        colors = [(185, 71, 85), (60, 135, 190), (58, 154, 111), (218, 153, 58), (128, 94, 176)]
        for index, color in enumerate(colors, start=1):
            image = png(900 + index * 20, 600 + index * 10, color)
            upload = owner.multipart(
                f"/PhongTro/UploadImage/{room}",
                "file",
                f"demo-room-{index}.png",
                "image/png",
                image,
                f"/PhongTro/Edit/{room}")
            assert upload[0] == 200, upload[:2]

        created = now.astimezone(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
        with sqlite3.connect(database) as connection:
            connection.execute("PRAGMA foreign_keys=ON")
            listing = connection.execute(
                "INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,ngay_dang,ngay_het_han,trang_thai,ngay_tao) "
                "VALUES(?,?,?,?,?,'2099-01-01 00:00:00','DANG_HIEN_THI',?) RETURNING id",
                (room, owner_id, "TIN DEMO ANH PHONG", "Tin demo kiem tra anh dai dien va thumbnail.", created, created)
            ).fetchone()[0]
            connection.commit()

        rows = query("SELECT id,duong_dan,duong_dan_anh_nho,thu_tu FROM anh_phong WHERE phong_id=? ORDER BY thu_tu", (room,))
        assert len(rows) == 5 and [row[3] for row in rows] == [1, 2, 3, 4, 5]
        for _, original, thumbnail, _ in rows:
            assert original.startswith("/uploads/rooms/")
            assert thumbnail.startswith("/uploads/rooms/")
        check = command(database, "--check-room-image-storage", check=False)
        assert check.returncode == 0, check.stdout + check.stderr
        assert "missing: 0; orphan: 0" in check.stdout, check.stdout

        code, list_page, _ = owner.request("/TinDang")
        assert code == 200 and "TIN DEMO ANH PHONG" in list_page and rows[0][2] in list_page
        code, detail_page, _ = owner.request(f"/TinDang/ChiTiet/{listing}")
        assert code == 200 and rows[0][1] in detail_page

        manifest = {
            "url": BASE_URL,
            "database": str(database),
            "storage": str(storage),
            "pid": process.pid,
            "password": password,
            "accounts": accounts,
            "building_id": building,
            "room_id": room,
            "listing_id": listing,
            "owner_edit_url": f"{BASE_URL}/PhongTro/Edit/{room}",
            "public_list_url": f"{BASE_URL}/TinDang",
            "public_detail_url": f"{BASE_URL}/TinDang/ChiTiet/{listing}",
            "initial_images": len(rows),
            "storage_check": check.stdout.strip()
        }
        (folder / "access.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
        (ROOT / "data/room-images-demo/latest.txt").write_text(str(folder), encoding="utf-8")
        print(json.dumps(manifest, ensure_ascii=True, indent=2), flush=True)
        print("PASS: uploaded 5 PNG images through HTTP, generated thumbnails, public listing uses first thumbnail, detail uses first original, storage check reports 0 missing/0 orphan. Demo remains running.", flush=True)
    except BaseException:
        process.terminate()
        process.wait(timeout=15)
        raise
    finally:
        log.close()


if __name__ == "__main__":
    main()
