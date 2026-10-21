"""Upload the five Sprint 2 fixture images through the real HTTP endpoint."""
import argparse
import html
import http.cookiejar
import json
from pathlib import Path
import re
import sqlite3
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Browser:
    def __init__(self, base_url):
        self.base_url = base_url.rstrip("/")
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(self, route, payload=None, headers=None):
        request = urllib.request.Request(self.base_url + route, payload, headers=headers or {})
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.code, response.read().decode("utf-8", errors="replace")

    def form_token(self, route):
        code, page = self.request(route)
        assert code == 200, (route, code)
        match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        assert match, f"Missing antiforgery token at {route}"
        return html.unescape(match.group(1))

    def post_form(self, route, data, form_route=None):
        data = dict(data)
        data["__RequestVerificationToken"] = self.form_token(form_route or route)
        return self.request(route, urllib.parse.urlencode(data).encode())

    def upload(self, room_id, image_path):
        form_route = f"/PhongTro/Edit/{room_id}"
        token = self.form_token(form_route)
        boundary = "----sprint2-demo-" + uuid.uuid4().hex
        body = (
            f"--{boundary}\r\n"
            'Content-Disposition: form-data; name="__RequestVerificationToken"\r\n\r\n'
            f"{token}\r\n"
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{image_path.name}"\r\n'
            "Content-Type: image/png\r\n\r\n"
        ).encode() + image_path.read_bytes() + f"\r\n--{boundary}--\r\n".encode()
        return self.request(
            f"/PhongTro/UploadImage/{room_id}", body,
            {"Content-Type": "multipart/form-data; boundary=" + boundary})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("access", type=Path)
    args = parser.parse_args()
    access = json.loads(args.access.read_text(encoding="utf-8-sig"))
    assert urllib.parse.urlparse(access["url"]).hostname in ("localhost", "127.0.0.1")
    browser = Browser(access["url"])
    for _ in range(120):
        try:
            if browser.request("/Account/Login")[0] == 200:
                break
        except OSError:
            pass
        time.sleep(.25)
    else:
        raise RuntimeError("Demo server did not become ready in 30 seconds")

    code, _ = browser.post_form("/Account/Login", {
        "TaiKhoanDangNhap": "owner.demo@demo.local",
        "MatKhau": access["password"]
    })
    assert code == 302, ("login", code)
    room_id = access["imageUploadRoom"]
    samples = Path(access["sampleImages"])
    with sqlite3.connect(access["database"]) as db:
        assert db.execute("SELECT COUNT(*) FROM anh_phong WHERE phong_id=?", (room_id,)).fetchone()[0] == 0
    for index in range(1, 6):
        code, body = browser.upload(room_id, samples / f"room-{index:02}.png")
        assert code == 200, (index, code, body[:300])

    storage = Path(access["roomImagesPath"])
    with sqlite3.connect(Path(access["database"]).as_uri() + "?mode=ro", uri=True) as db:
        rows = db.execute(
            "SELECT duong_dan,duong_dan_anh_nho,thu_tu FROM anh_phong WHERE phong_id=? ORDER BY thu_tu",
            (room_id,)).fetchall()
    assert len(rows) == 5 and [row[2] for row in rows] == [1, 2, 3, 4, 5]
    for original, thumbnail, _ in rows:
        assert (storage / original.removeprefix("/uploads/rooms/")).is_file()
        assert (storage / thumbnail.removeprefix("/uploads/rooms/")).is_file()

    report = args.access.parent / "report.md"
    with report.open("a", encoding="utf-8") as handle:
        handle.write("\n- Xác minh ảnh: đã upload 5 PNG qua HTTP thật; DB có 5 đường dẫn gốc/thumbnail khớp tệp, thứ tự 1–5.\n")
    print("PASS: uploaded 5 room images through HTTP and verified original/thumbnail paths.")


if __name__ == "__main__":
    main()
