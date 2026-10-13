"""Exercise cancellation on a disposable S4-02 demo already paid in two installments.
Run Verify-S402Http.py first. This script cancels the second receipt and records a NEW one.
Usage: python verification/Verify-S402CancelHttp.py <demo-directory> [http://localhost:5248]
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

directory=Path(sys.argv[1]).resolve()
base=sys.argv[2] if len(sys.argv)>2 else "http://localhost:5248"
assert directory.parent.name=="s402-demo", "Only synthetic S4-02 demo directories are allowed"
access=json.loads((directory/"access.json").read_text(encoding="utf-8"))
def client(): return urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def call(opener,path,values=None):
    data=urllib.parse.urlencode(values).encode() if values is not None else None
    try:
        with opener.open(base+path,data,timeout=20) as response: return response.status,response.read().decode()
    except urllib.error.HTTPError as error: return error.code,error.read().decode()
def token(opener,path):
    status,body=call(opener,path);assert status==200
    return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body).group(1))
def login(account):
    opener=client()
    call(opener,"/Account/Login",{"TaiKhoanDangNhap":account["email"],"MatKhau":account["password"],"__RequestVerificationToken":token(opener,"/Account/Login")})
    return opener
owner=login(access["accounts"][0])
status,body=call(owner,"/HoaDonDichVu/CollectionDetails?id=1");initial=json.loads(body)
assert status==200 and initial["tongDaThu"]==3000000 and len(initial["lanThus"])==2 and initial["huySanSang"]
receipt=next(row for row in initial["lanThus"] if row["soTien"]==2000000)
values={"ThanhToanId":str(receipt["id"]),"PhienBan":str(receipt["phienBan"]),"LyDo":"Nhập nhầm số tiền","NguoiHuyId":"2","__RequestVerificationToken":token(owner,"/HoaDonDichVu/Details/1")}
no_token=dict(values);no_token.pop("__RequestVerificationToken")
assert call(owner,"/HoaDonDichVu/CancelCollection",no_token)[0]==400
for account in access["accounts"][1:]:
    other=login(account)
    assert call(other,"/HoaDonDichVu/CancelCollection",dict(values,__RequestVerificationToken=token(other,"/Account/ChangePassword")))[0]==403
for reason in ("","   ","abcd","a"*501):
    status,body=call(owner,"/HoaDonDichVu/CancelCollection",dict(values,LyDo=reason))
    assert status==400 and "LyDo" in json.loads(body)["errors"]
status,body=call(owner,"/HoaDonDichVu/CancelCollection",values);partial=json.loads(body)
assert status==200 and partial["tongDaThu"]==1000000 and partial["conPhaiTra"]==2000000 and partial["trangThai"]=="THANH_TOAN_MOT_PHAN"
canceled=next(row for row in partial["lanThus"] if row["id"]==receipt["id"])
assert canceled["daHuy"] and canceled["lyDoHuy"]=="Nhập nhầm số tiền" and canceled["nguoiHuy"]=="Chủ nhà kiểm thử S4-02" and canceled["thoiDiemHuy"]
assert call(owner,"/HoaDonDichVu/CancelCollection",values)[0]==409
status,page=call(owner,"/HoaDonDichVu/Details/1");decoded=html.unescape(page)
assert status==200 and "<del>2.000.000 đ</del>" in decoded and "Đã hủy" in decoded and "Nhập nhầm số tiền" in decoded
assert re.search(r'id="ic-open"(?![^>]*hidden)',page)
today=datetime.now(timezone(timedelta(hours=7))).date().isoformat()
status,body=call(owner,"/HoaDonDichVu/Collect",{"HoaDonId":"1","SoTien":"2000000","NgayThu":today,"HinhThuc":"CHUYEN_KHOAN","GhiChu":"Ghi lại sau khi hủy","KhoaChongTrung":uuid.uuid4().hex,"__RequestVerificationToken":token(owner,"/HoaDonDichVu/Details/1")})
full=json.loads(body)
assert status==200 and full["trangThai"]=="DA_THANH_TOAN" and full["conPhaiTra"]==0 and len(full["lanThus"])==3
assert sum(row["soTien"] for row in full["lanThus"] if not row["daHuy"])==full["tongDaThu"]==3000000
assert sum(row["daHuy"] for row in full["lanThus"])==1
print("S4-02 cancellation HTTP PASS: CSRF/4 unauthorized roles, invalid reasons, cancel/repeat, persisted history, reopened recording button, recollection to full.")
