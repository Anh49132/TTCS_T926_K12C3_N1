"""Browser acceptance + actual confirmation-to-result timing on isolated synthetic data."""
import json
import sqlite3
import sys
import time
from pathlib import Path

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root / 'data/task1-build/browser-packages'))
from playwright.sync_api import sync_playwright

folder = Path(sys.argv[1]).resolve()
assert folder.is_relative_to(root / 'data/s306-demo')
access = json.loads((folder / 'access.json').read_text(encoding='utf-8-sig'))
base = access['url']
assert base.startswith('http://localhost:')
database = Path(access['database']).resolve()
assert database.is_relative_to(folder)
report = {'checks': [], 'timings': []}

def review(page, building):
    page.goto(f'{base}/HoaDonDichVu/Monthly?toaNhaId={building}&nam=2026&thang=10&ngayPhatHanh=2026-11-07&hanThanhToan=2026-11-14', wait_until='networkidle')

def login(page, email):
    page.goto(base + '/Account/Login')
    page.locator('[name="TaiKhoanDangNhap"]').fill(email)
    page.locator('[name="MatKhau"]').fill(access['password'])
    page.locator('button[type="submit"]').click()
    page.wait_for_load_state('networkidle')

def issue(page, expected, label):
    page.locator('[data-review-open]').click()
    page.locator('#issue-confirmation [name="xacNhan"]').check()
    started = time.perf_counter()
    page.locator('#issue-confirmation button[type="submit"]').click()
    page.wait_for_url('**/HoaDonDichVu/Results?runId=*')
    page.wait_for_load_state('networkidle')
    seconds = time.perf_counter() - started
    assert page.locator('#issued-count').inner_text() == str(expected)
    assert page.locator('.mi-steps').count() == 0
    assert seconds < 30, f'{label}: {seconds:.3f}s'
    server_seconds = float(page.locator('#run-duration').inner_text().replace('.', '').replace(',', '.'))
    report['timings'].append({'case': label, 'roomsIssued': expected, 'confirmationToResultSeconds': round(seconds, 3), 'serverSeconds': server_seconds, 'resultUrl': page.url})
    return page.url

if len(sys.argv) > 2 and sys.argv[2] == '--visual-only':
    saved = json.loads((folder / 'bulk-browser-report.json').read_text(encoding='utf-8'))
    url = next(x['resultUrl'] for x in saved['timings'] if x['roomsIssued'] == 50)
    with sync_playwright() as p:
        browser = p.chromium.launch(channel='chrome', headless=True)
        page = browser.new_page(viewport={'width':1672,'height':945}, locale='vi-VN')
        login(page, 'owner.demo@demo.local')
        page.goto(url, wait_until='networkidle')
        page.evaluate('window.scrollTo(0,0)')
        assert page.locator('.ir-banner a.btn-dark').evaluate("e=>getComputedStyle(e).color") == 'rgb(255, 255, 255)'
        page.screenshot(path=str(folder / 'bulk-results-desktop.png'), full_page=True)
        page.set_viewport_size({'width':360,'height':900})
        page.evaluate('window.scrollTo(0,0)')
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
        page.screenshot(path=str(folder / 'bulk-results-mobile.png'), full_page=True)
        browser.close()
    print('Read-only visual check: desktop, white button text and mobile 360px passed')
    sys.exit(0)

with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True)
    owner_context = browser.new_context(viewport={'width':1672, 'height':945}, locale='vi-VN')
    page = owner_context.new_page()
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    login(page, 'owner.demo@demo.local')
    review(page, access['monthlyBuildingId'])
    assert page.locator('.room-select').count() == 4, 'Use a fresh, unissued demo'
    first = issue(page, 4, 'Mixed first run')
    assert page.locator('#missing-count').inner_text() == '1'
    assert page.locator('#existing-count').inner_text() == '0'
    page.screenshot(path=str(folder / 'bulk-mixed-desktop.png'), full_page=True)
    page.locator('[data-result-tab="missing"]').click()
    assert 'A105' in page.locator('#result-table tbody tr:visible').inner_text()
    assert 'Thiếu chỉ số nước' in page.locator('#result-table tbody tr:visible').inner_text()
    page.locator('#retry-period').click();page.wait_for_load_state('networkidle')
    assert page.locator('.room-select').count() == 0
    assert page.locator('#existing-invoices tbody tr').count() == 4
    issue(page, 0, 'Mixed retry before missing reading completed')
    assert page.locator('#existing-count').inner_text() == '4'
    assert page.locator('#missing-count').inner_text() == '1'
    assert page.locator('#result-table tbody tr:visible a').count() == 4
    report['checks'].append('Repeat same period: 4 existing invoices linked separately; missing room remains unbilled')

    # Complete the missing room using the real manager form, not a database write.
    manager_context = browser.new_context(locale='vi-VN')
    manager_page = manager_context.new_page()
    login(manager_page, 'manager.demo@demo.local')
    manager_page.goto(f'{base}/ChiSoDienNuoc?toaNhaId={access["monthlyBuildingId"]}', wait_until='networkidle')
    room = manager_page.locator('[data-room-code="A105"]')
    assert room.count() == 1
    room.locator('[name="DienMoi"]').fill('150')
    room.locator('[name="NuocMoi"]').fill('25')
    room.get_by_role('button', name='Lưu phòng A105').click()
    manager_page.wait_for_load_state('networkidle')
    assert 'Đã lưu chỉ số' in manager_page.locator('.alert-success').inner_text()
    review(page, access['monthlyBuildingId'])
    assert page.locator('.room-select').count() == 1
    issue(page, 1, 'Retry after manager completed A105')
    assert page.locator('#existing-count').inner_text() == '4'
    assert page.locator('#missing-count').inner_text() == '0'
    report['checks'].append('Manager completed missing water through UI; retry issued only A105 and kept 4 existing invoices')

    review(page, access['bulkBuildingId'])
    assert page.locator('.room-select').count() == 50
    result_url = issue(page, 50, 'Fifty rooms with rent, electricity, water, two fixed services and per-person fee')
    assert page.locator('#existing-count').inner_text() == '0'
    assert page.locator('#result-table tbody tr:visible').count() == 5
    assert '203.000.000 đ' in page.locator('.ir-summary').inner_text()
    with page.expect_download() as download:
        page.get_by_role('link', name='Tải báo cáo kết quả', exact=False).click()
    download.value.save_as(folder / 'bulk-result-50.csv')
    csv = (folder / 'bulk-result-50.csv').read_text(encoding='utf-8-sig')
    assert len(csv.splitlines()) == 51
    assert '4060000' in csv
    page.get_by_role('button', name='Trang 2', exact=True).click()
    assert 'B006' in page.locator('#result-table tbody tr:visible').first.inner_text()
    page.locator('#result-search').fill('B050')
    assert page.locator('#result-table tbody tr:visible').count() == 1
    page.locator('#result-search').fill('')
    page.locator('#result-floor').select_option('3')
    assert 'B021' in page.locator('#result-table tbody tr:visible').first.inner_text()
    page.locator('#result-floor').select_option('')
    page.evaluate('window.scrollTo(0,0)')
    page.screenshot(path=str(folder / 'bulk-results-desktop.png'), full_page=True)
    page.set_viewport_size({'width':360,'height':900})
    assert page.evaluate('document.documentElement.scrollWidth <= innerWidth')
    page.evaluate('window.scrollTo(0,0)')
    page.screenshot(path=str(folder / 'bulk-results-mobile.png'), full_page=True)
    page.set_viewport_size({'width':1672,'height':945})
    page.locator('#retry-period').click();page.wait_for_load_state('networkidle')
    issue(page, 0, 'Fifty-room retry')
    assert page.locator('#existing-count').inner_text() == '50'
    assert page.locator('#missing-count').inner_text() == '0'
    page.screenshot(path=str(folder / 'bulk-rerun-desktop.png'), full_page=True)
    report['checks'].append('50-room report, CSV, search, floor filter, pagination, mobile 360px and zero-duplicate retry passed')
    # Ownership is checked on report and CSV, not only on the issuance POST.
    other = browser.new_context().new_page()
    login(other, 'owner.b.demo@demo.local')
    assert other.goto(result_url).status == 403
    assert other.goto(result_url.replace('/Results?', '/ExportResults?')).status == 403
    report['checks'].append('Another owner cannot access the report or CSV (403)')
    assert not errors, errors
    browser.close()

with sqlite3.connect(database.as_uri()+'?mode=ro', uri=True) as db:
    invoice_count = db.execute('SELECT COUNT(*) FROM hoa_don i JOIN hop_dong h ON h.id=i.hop_dong_id JOIN phong_tro p ON p.id=h.phong_id WHERE p.toa_nha_id=? AND i.nam=2026 AND i.thang=10 AND i.trang_thai<>\'DA_HUY\'', (access['bulkBuildingId'],)).fetchone()[0]
    assert invoice_count == 50
    assert not db.execute('SELECT hop_dong_id,nam,thang,COUNT(*) FROM hoa_don WHERE trang_thai<>\'DA_HUY\' GROUP BY hop_dong_id,nam,thang HAVING COUNT(*)>1').fetchall()
    assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
report['checks'].append('Read-only SQLite verification: exactly 50 invoices, no duplicates, integrity/FK OK')
(folder / 'bulk-browser-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False, indent=2))
