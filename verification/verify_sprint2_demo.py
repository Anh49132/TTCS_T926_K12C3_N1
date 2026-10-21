"""Read-only fixture/HTTP smoke checks (login records may update last-login).
Use standard Python 3: python verification/verify_sprint2_demo.py
Server must already be running via Start-Sprint2Demo.ps1.
"""
import html
import http.cookiejar
import argparse
import json
from pathlib import Path
import re
import sqlite3
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--access", type=Path)
arguments = parser.parse_args()
folder = arguments.access.parent if arguments.access else Path((ROOT / "data/sprint2-demo/latest.txt").read_text(encoding="utf-8-sig").strip())
access = json.loads((folder / "access.json").read_text(encoding="utf-8-sig"))
base = access["url"]
assert urllib.parse.urlparse(base).hostname in ("localhost", "127.0.0.1"), "Local demo URL required"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None


class Session:
    def __init__(self):
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def get(self, path, data=None):
        request = urllib.request.Request(base + path,
            urllib.parse.urlencode(data).encode() if data is not None else None)
        try:
            response = self.opener.open(request, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, html.unescape(response.read().decode("utf-8"))

    def login(self, email):
        status, page = self.get("/Account/Login")
        assert status == 200
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1)
        status, page = self.get("/Account/Login", {"TaiKhoanDangNhap": email,
            "MatKhau": access["password"], "__RequestVerificationToken": token})
        assert status == 302, (email, status)


with sqlite3.connect(Path(access["database"]).as_uri() + "?mode=ro", uri=True) as db:
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert not db.execute("PRAGMA foreign_key_check").fetchall()
    assert db.execute("SELECT COUNT(*) FROM tin_dang").fetchone()[0] == 41
    assert db.execute("SELECT COUNT(*) FROM tai_khoan").fetchone()[0] == 7
    assert db.execute("SELECT COUNT(*) FROM yeu_cau_thue").fetchone()[0] == 8
    assert dict(db.execute("SELECT trang_thai,COUNT(*) FROM yeu_cau_thue GROUP BY trang_thai")) == {
        "MOI": 3, "DA_HEN_LICH": 2, "DA_DUYET": 1, "TU_CHOI": 1, "DA_HUY": 1}
    assert db.execute("""SELECT COUNT(*) FROM tin_dang t JOIN phong_tro p ON p.id=t.phong_id
        JOIN toa_nha n ON n.id=p.toa_nha_id WHERE t.trang_thai='DANG_HIEN_THI'
        AND t.ngay_het_han>CURRENT_TIMESTAMP AND p.trang_thai='TRONG' AND n.dang_hoat_dong=1""").fetchone()[0] == 35
    assert db.execute("SELECT COUNT(DISTINCT n.quan_huyen) FROM tin_dang t JOIN phong_tro p ON p.id=t.phong_id JOIN toa_nha n ON n.id=p.toa_nha_id WHERE t.trang_thai='DANG_HIEN_THI' AND t.ngay_het_han>CURRENT_TIMESTAMP AND p.trang_thai='TRONG'").fetchone()[0] >= 4
    assert db.execute("SELECT COUNT(*) FROM cau_hinh_dich_vu WHERE toa_nha_id=?", (access["buildings"]["a2"],)).fetchone()[0] == 0
    assert db.execute("SELECT COUNT(*) FROM anh_phong WHERE phong_id=?", (access["imageUploadRoom"],)).fetchone()[0] == 5
    assert db.execute("SELECT COUNT(*) FROM anh_phong WHERE phong_id=?", (access["emptyImageRoom"],)).fetchone()[0] == 0
    assert [x[0] for x in db.execute("SELECT tong_tien FROM hoa_don ORDER BY id")] == [3720000, 3650000]
    for original, thumb in db.execute("SELECT duong_dan,duong_dan_anh_nho FROM anh_phong"):
        for path in (original, thumb):
            assert (Path(access["roomImagesPath"]) / path.removeprefix("/uploads/rooms/")).is_file()

report = {"accounts_verified": [], "pages_verified": [], "search_ms": []}
sessions = {}
for account in access["accounts"]:
    session = Session()
    session.login(account["email"])
    sessions[account["email"]] = session
    report["accounts_verified"].append(account["email"])

owner = sessions["owner.demo@demo.local"]
for path, expected in [
    ("/TinDang/QuanLy", "Đã hết hạn"), ("/DichVuPhong?phongId=1", "270.000"),
    ("/DichVuPhong?phongId=2", "200.000"), ("/DichVu/DienNuoc?toaNhaId=1", access["nextPeriod"]),
    ("/PhongTro/Edit/5", "Sửa phòng"), ("/HoaDonDichVu/Details/1", "3.720.000"),
    ("/HoaDonDichVu/Details/2", "3.650.000"), ("/LichHen/ChiTiet/2", None),
    ("/YeuCau", "Quá 24 giờ"), ("/TinDang/Tao?phongId=4", "Đăng tin")]:
    status, page = owner.get(path)
    assert status == 200 and (expected is None or expected in page), (path, status, expected)
    report["pages_verified"].append(path)
tenant = sessions["tenant1.demo@demo.local"]
status, page = tenant.get("/YeuCau")
assert status == 200 and "Yêu cầu của tôi" in page
period_month, period_year = access["currentPeriod"].split("/")
assert f"YC-{period_year}{period_month}-0003" not in page
status, page = tenant.get("/TinDang/ChiTiet/8")
assert status == 200 and 'name="Form.SoNguoiDuKien"' in page
status, _ = sessions["owner.b.demo@demo.local"].get("/DichVuPhong?phongId=1")
assert status in (403, 404)
guest = Session()
status, page = guest.get("/TinDang/ChiTiet/1")
assert status == 200 and "3.770.000" in page and "4.200" in page and "80.000" in page
for path in ("/TinDang/ChiTiet/4", "/TinDang/ChiTiet/5", "/TinDang/ChiTiet/6", "/TinDang/ChiTiet/11", "/TinDang/ChiTiet/12", "/TinDang/ChiTiet/13"):
    assert guest.get(path)[0] == 404, path
for _ in range(5):
    start = time.perf_counter()
    status, page = guest.get("/TimTin?SapXep=gia-tang")
    elapsed = round((time.perf_counter() - start) * 1000, 3)
    assert status == 200 and elapsed < 2000
    # Current TimTinViewModel uses six listings per page.
    assert len(re.findall(r'<article class="nhatot-listing-card timtin-listing-card">', page)) == 6
    assert "/TinDang/ChiTiet/" in page
    report["search_ms"].append(elapsed)
report["passed"] = True
(folder / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
