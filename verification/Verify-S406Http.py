"""Mutating smoke test: use only a dedicated fake S4-06 demo, never a real database."""
import html, http.cookiejar, json, re, sqlite3, sys, urllib.error, urllib.parse, urllib.request
from pathlib import Path

folder = Path(sys.argv[1]).resolve()
assert folder.parent.name == "s406" and folder.name.startswith("demo-"), "Dedicated fake S4-06 demo required"
access = json.loads((folder / "access.json").read_text(encoding="utf-8-sig"))
base = access["url"]
assert urllib.parse.urlparse(base).hostname in ("localhost", "127.0.0.1")

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args): return None

class Session:
    def __init__(self):
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect(), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def request(self, path, data=None):
        request = urllib.request.Request(base + path, urllib.parse.urlencode(data).encode() if data is not None else None)
        try: response = self.opener.open(request, timeout=20)
        except urllib.error.HTTPError as error: response = error
        with response: return response.status, html.unescape(response.read().decode("utf-8")), response.headers
    def login(self, email):
        status, page, _ = self.request("/Account/Login"); assert status == 200
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1)
        assert self.request("/Account/Login", {"TaiKhoanDangNhap": email, "MatKhau": access["password"], "__RequestVerificationToken": token})[0] == 302
    def form(self):
        status, page, _ = self.request("/BaoHong/Create"); assert status == 200
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1)
        key = re.search(r'name="Input.KhoaChongTrung"[^>]*value="([^"]+)"', page).group(1)
        return page, {"__RequestVerificationToken": token, "Input.KhoaChongTrung": key, "Input.HopDongId": "1", "Input.LoaiThietBi": "Vòi nước", "Input.MoTa": "Nước rò dưới bồn rửa", "Input.MucDo": "THUONG"}

tenant = Session(); tenant.login("tenant1.demo@demo.local")
page, data = tenant.form(); assert "HD-DEMO-C001" in page and "HD-DEMO-C002" in page
invalid = dict(data, **{"Input.LoaiThietBi": " ", "Input.MoTa": " "})
assert tenant.request("/BaoHong/Create", invalid)[0] == 200
assert tenant.request("/BaoHong/Create", {k:v for k,v in data.items() if k != "__RequestVerificationToken"})[0] == 400
assert tenant.request("/BaoHong/Create", dict(data, **{"Input.HopDongId": "999999"}))[0] == 200
created = []
for contract, level in (("1", "THUONG"), ("2", "GAP")):
    _, data = tenant.form(); data["Input.HopDongId"] = contract; data["Input.MucDo"] = level
    status, _, headers = tenant.request("/BaoHong/Create", data); assert status == 302
    location = headers["Location"]; created.append(location)
    assert tenant.request("/BaoHong/Create", data)[2]["Location"] == location
    status, detail, _ = tenant.request(location)
    assert status == 200 and re.search(r"BH-\d{6}-\d{4}",detail) and "Mới" in detail and "Nước rò" in detail
    assert "no-store" in tenant.request(location)[2].get("Cache-Control", "")
assert all(x in tenant.request("/BaoHong")[1] for x in created)
other = Session(); other.login("tenant3.demo@demo.local")
assert other.request(created[0])[0] == 403
assert "BH-" not in other.request("/BaoHong")[1]
assert "Chưa có phòng đủ điều kiện" in other.request("/BaoHong/Create")[1]
roommate = Session(); roommate.login("tenant2.demo@demo.local")
_, data = roommate.form(); status, _, headers = roommate.request("/BaoHong/Create", data)
assert status == 302 and roommate.request(headers["Location"])[0] == 200
assert tenant.request(headers["Location"])[0] == 403
for email in ("owner.demo@demo.local", "manager.demo@demo.local", "admin.demo@demo.local"):
    staff = Session(); staff.login(email); assert staff.request("/BaoHong/Create")[0] == 403
guest = Session(); assert guest.request("/BaoHong")[0] in (302,401)
with sqlite3.connect(Path(access["database"]).as_uri()+"?mode=ro", uri=True) as db:
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert not db.execute("PRAGMA foreign_key_check").fetchall()
    count = db.execute("SELECT COUNT(*) FROM bao_hong").fetchone()[0]
    assert count == db.execute("SELECT COUNT(DISTINCT ma_bao_hong) FROM bao_hong").fetchone()[0]
report = {"passed": True, "reports": count, "checked": ["ordinary/urgent", "multiple rooms", "roommate", "required fields", "CSRF", "foreign room", "idempotent retry", "details/list", "cross-tenant 403", "staff denied", "integrity/FK"]}
(folder / "http-verification.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report))
