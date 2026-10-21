"""Exercise a NEW disposable S4-02 demo only; leaves its invoice paid.
Usage: python verification/Verify-S402Http.py <demo-directory> [http://localhost:5248]
"""
from pathlib import Path
from datetime import datetime, timezone, timedelta
import http.cookiejar
import html
import json
import re
import sys
import urllib.request
import urllib.parse
import urllib.error
import uuid

directory = Path(sys.argv[1]).resolve()
base = sys.argv[2] if len(sys.argv)>2 else "http://localhost:5248"
assert directory.parent.name == "s402-demo", "Only synthetic S4-02 demo directories are allowed"
access = json.loads((directory/"access.json").read_text(encoding="utf-8"))
def client(): return urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def call(opener,path,values=None):
    data = urllib.parse.urlencode(values).encode() if values is not None else None
    try:
        with opener.open(base+path,data,timeout=20) as response: return response.status,response.read().decode()
    except urllib.error.HTTPError as error: return error.code,error.read().decode()
def token(opener,path):
    status,body=call(opener,path); assert status==200
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body).group(1))
def login(account):
    opener=client()
    call(opener,"/Account/Login",{"TaiKhoanDangNhap":account["email"],"MatKhau":account["password"],"__RequestVerificationToken":token(opener,"/Account/Login")})
    return opener
owner=login(access["accounts"][0])
status,page=call(owner,"/HoaDonDichVu/Details/1")
assert status==200 and "ic-dialog" in page and "3.000.000" in page
assert call(owner,"/css/invoice-collection.css")[0]==200
assert call(owner,"/js/invoice-collection.js")[0]==200
today=datetime.now(timezone(timedelta(hours=7))).date().isoformat()
values={"HoaDonId":"1","SoTien":"1000000","NgayThu":today,"HinhThuc":"CHUYEN_KHOAN","GhiChu":"CK tiền phòng kiểm thử","KhoaChongTrung":uuid.uuid4().hex,"__RequestVerificationToken":token(owner,"/HoaDonDichVu/Details/1")}
for account in access["accounts"][1:]:
    other=login(account)
    assert call(other,"/HoaDonDichVu/Collect",dict(values,__RequestVerificationToken=token(other,"/Account/ChangePassword")))[0]==403
for invalid in ("0","-1","1.5","3000001"):
    status,body=call(owner,"/HoaDonDichVu/Collect",dict(values,SoTien=invalid))
    assert status==400 and "SoTien" in json.loads(body)["errors"]
status,body=call(owner,"/HoaDonDichVu/Collect",values); summary=json.loads(body)
assert status==200 and summary["tongDaThu"]==1000000 and summary["conPhaiTra"]==2000000 and summary["trangThai"]=="THANH_TOAN_MOT_PHAN"
status,body=call(owner,"/HoaDonDichVu/Collect",values); assert len(json.loads(body)["lanThus"])==1
status,page=call(owner,"/HoaDonDichVu/Details/1")
assert status==200 and "2.000.000" in page and "CK tiền phòng kiểm thử" in html.unescape(page)
values.update(SoTien="2000000",KhoaChongTrung=uuid.uuid4().hex)
status,body=call(owner,"/HoaDonDichVu/Collect",values); summary=json.loads(body)
assert status==200 and summary["conPhaiTra"]==0 and summary["trangThai"]=="DA_THANH_TOAN" and len(summary["lanThus"])==2
status,page=call(owner,"/HoaDonDichVu/Details/1")
assert status==200 and re.search(r'id="ic-open"[^>]*hidden',page)
print("S4-02 HTTP PASS: 4 unauthorized roles/owners, invalid amounts, partial/full, replay, receipt history, hidden button, CSS/JS delivery.")
