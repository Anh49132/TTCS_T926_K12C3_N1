"""Browser verification on an isolated S3-06 demo; publishes A104 only."""
import json
import sqlite3
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / "data/task1-build/browser-packages"))
from playwright.sync_api import sync_playwright

folder = Path(sys.argv[1]).resolve()
assert folder.is_relative_to(root / "data/s306-demo")
access = json.loads((folder / "access.json").read_text(encoding="utf-8-sig"))
base = access["url"]
assert base.startswith("http://localhost:"), "Local demo only"
building = access["monthlyBuildingId"]
examples = access["monthlyExamples"]
target = next(x for x in examples if x["room"] == "A104")
with sqlite3.connect(Path(access["database"]).as_uri()+"?mode=ro", uri=True) as db:
    already_issued = dict(db.execute("SELECT hop_dong_id,id FROM hoa_don WHERE nam=2026 AND thang=10 AND trang_thai='DA_PHAT_HANH'"))
expected_ready = sum(x["expectedTotal"] is not None and x["contractId"] not in already_issued for x in examples)
monthly = f"{base}/HoaDonDichVu/Monthly?toaNhaId={building}&nam=2026&thang=10"
preview = f"{base}/HoaDonDichVu/Preview?toaNhaId={building}&hopDongId={target['contractId']}&nam=2026&thang=10"
if target["contractId"] in already_issued:
    preview = f"{base}/HoaDonDichVu/Details/{already_issued[target['contractId']]}"
checks = []
with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True)
    page = browser.new_page(viewport={"width":1672,"height":945})
    errors = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.goto(base + "/Account/Login")
    page.locator('[name="TaiKhoanDangNhap"]').fill("owner.demo@demo.local")
    page.locator('[name="MatKhau"]').fill(access["password"])
    page.locator('button[type="submit"]').click()
    page.wait_for_load_state("networkidle")
    page.goto(monthly, wait_until="networkidle")
    assert page.locator('.room-select').count() == expected_ready
    assert page.locator(".mi-steps").count() == 0
    for example in examples[:4]:
        if example["contractId"] in already_issued:
            continue
        row = page.locator(".mi-table tbody tr").filter(has=page.get_by_role("rowheader", name=example["room"], exact=True))
        assert f"{example['expectedTotal']:,}".replace(",", ".") in row.inner_text()
    assert "A105" not in page.locator(".mi-table").inner_text()
    page.screenshot(path=str(folder / "monthly-desktop.png"), full_page=True)
    checks.append("Full totals and missing-water room skipped; no step bar")
    page.goto(preview, wait_until="networkidle")
    assert "4.220.000 đ" in page.locator(".id-total").inner_text()
    assert "3 người" in page.locator(".id-people").inner_text()
    assert page.locator(".id-charges tbody tr").count() == 6
    for text in ["Tiền phòng", "Điện", "Nước", "Internet", "Rác", "Vệ sinh theo người", "Khoán theo đầu người"]:
        assert text in page.locator(".id-charges").inner_text()
    page.screenshot(path=str(folder / "invoice-desktop.png"), full_page=True)
    page.evaluate("window.print = () => { window.invoicePrintCalled = true; }")
    page.get_by_role("button", name="Xem bản in").click()
    assert page.evaluate("window.invoicePrintCalled === true")
    page.emulate_media(media="print")
    assert not page.locator(".dashboard-sidebar").is_visible()
    page.screenshot(path=str(folder / "invoice-print.png"), full_page=True)
    page.emulate_media(media="screen")
    checks.append("Preview: 6 lines, 3 people, correct total and print action")
    page.set_viewport_size({"width":360,"height":900})
    page.goto(preview, wait_until="networkidle")
    assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Detail overflows at 360px"
    assert page.locator("h1").bounding_box()["width"] >= 260, "Mobile heading squeezed"
    assert page.locator(".id-heading").bounding_box()["height"] < 320, "Mobile heading too tall"
    page.screenshot(path=str(folder / "invoice-mobile.png"), full_page=True)
    page.goto(monthly, wait_until="networkidle")
    assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth"), "Monthly overflows at 360px"
    page.screenshot(path=str(folder / "monthly-mobile.png"), full_page=True)
    checks.append("Desktop and mobile 360px: no page horizontal overflow")
    page.goto(preview, wait_until="networkidle")
    if target["contractId"] not in already_issued:
        page.get_by_role("button", name="Xác nhận và phát hành hóa đơn").click()
        page.locator('#issue-confirmation [name="xacNhan"]').check()
        page.locator('#issue-confirmation').get_by_role('button',name='Xác nhận phát hành',exact=True).click()
        page.wait_for_load_state("networkidle")
        assert "Đã phát hành 1 hóa đơn" in page.locator("main").inner_text()
    with sqlite3.connect(Path(access["database"]).as_uri()+"?mode=ro", uri=True) as db:
        invoice, total, people, status = db.execute("SELECT id,tong_tien,so_nguoi_tinh_phi,trang_thai FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=10",(target["contractId"],)).fetchone()
        assert (total,people,status) == (4220000,3,"DA_PHAT_HANH")
        assert db.execute("SELECT COUNT(*) FROM chi_so_dien_nuoc WHERE hop_dong_id=? AND da_khoa=1",(target["contractId"],)).fetchone()[0]==2
        assert not db.execute("SELECT 1 FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=10",(examples[4]["contractId"],)).fetchone()
        assert db.execute("PRAGMA integrity_check").fetchone()[0]=="ok"
        assert not db.execute("PRAGMA foreign_key_check").fetchall()
    page.goto(f"{base}/HoaDonDichVu/Details/{invoice}", wait_until="networkidle")
    assert "4.220.000 đ" in page.locator(".id-total").inner_text()
    assert page.locator(".id-charges tbody tr").count()==6
    assert page.get_by_role("button",name="Xác nhận và phát hành hóa đơn").count()==0
    page.screenshot(path=str(folder / "issued-mobile.png"), full_page=True)
    checks.append("Published A104 through browser; all amounts saved and readings locked; DB integrity/FK OK")
    assert not errors, errors
    browser.close()
(folder / "browser-checks.json").write_text(json.dumps({"checks":checks,"javascriptErrors":errors},ensure_ascii=False,indent=2),encoding="utf-8")
print("PASS: " + "; ".join(checks))
