"""Verify review dates/skips/confirmation on an isolated v20 demo; publish one ready room."""
import json
import sqlite3
import sys
from pathlib import Path

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'data/task1-build/browser-packages'))
from playwright.sync_api import sync_playwright
folder=Path(sys.argv[1]).resolve()
assert folder.is_relative_to(root/'data/s306-demo')
access=json.loads((folder/'access.json').read_text(encoding='utf-8-sig'))
base=access['url']; assert base.startswith('http://localhost:')
building=access['monthlyBuildingId']
with sqlite3.connect(Path(access['database']).as_uri()+'?mode=ro',uri=True) as db:
    issued={x[0] for x in db.execute('SELECT hop_dong_id FROM hoa_don WHERE nam=2026 AND thang=10 AND trang_thai<>\'DA_HUY\'')}
ready=[x for x in access['monthlyExamples'] if x['expectedTotal'] is not None and x['contractId'] not in issued]
assert len(ready)>=2, 'Use a demo with at least two unissued rooms'
target=max(ready,key=lambda x:x['expectedTotal'])
url=f'{base}/HoaDonDichVu/Monthly?toaNhaId={building}&nam=2026&thang=10'
checks=[]
with sync_playwright() as p:
    browser=p.chromium.launch(channel='chrome',headless=True)
    page=browser.new_page(viewport={'width':1672,'height':945},locale='vi-VN')
    errors=[]; posts=[]
    page.on('pageerror',lambda error:errors.append(str(error)))
    page.on('request',lambda request:posts.append(request.url) if request.method=='POST' and '/IssueMonthly' in request.url else None)
    page.goto(base+'/Account/Login')
    page.locator('[name="TaiKhoanDangNhap"]').fill('owner.demo@demo.local')
    page.locator('[name="MatKhau"]').fill(access['password'])
    page.locator('button[type="submit"]').click();page.wait_for_load_state('networkidle')
    page.goto(url,wait_until='networkidle')
    assert page.locator('.mi-steps').count()==0
    assert page.locator('.room-select').count()==len(ready)
    assert page.locator('#building').input_value()==str(building)
    page.locator('#issue-date-text').fill('07/11/2026')
    assert page.locator('#due-date').input_value()=='2026-11-14'
    assert page.locator('[data-review-open]').is_disabled()
    page.locator('#due-date-text').fill('25/11/2026')
    page.get_by_role('button',name='Kiểm tra lại',exact=True).click();page.wait_for_load_state('networkidle')
    assert page.locator('#due-date').input_value()=='2026-11-25'
    assert not page.locator('[data-review-open]').is_disabled()
    checks.append('Chosen issue date 07/11 defaults due to 14/11; custom 25/11 survives review')
    page.locator('.mr-tabs [data-review-tab="skipped"]').click()
    assert 'Thiếu chỉ số nước' in page.locator('#skipped-table').inner_text()
    assert 'A105' in page.locator('#skipped-table').inner_text()
    if any(x['contractId'] in issued for x in access['monthlyExamples']):
        assert 'Đã có hóa đơn' in page.locator('#skipped-table').inner_text()
    page.locator('.mr-tabs [data-review-tab="ready"]').click()
    page.locator('#room-sort').select_option('total-desc')
    assert page.locator('#ready-table tbody tr:visible').first.get_attribute('data-room')==target['room']
    page.locator('#room-search').fill(target['room'])
    assert page.locator('#ready-table tbody tr:visible').count()==1
    page.locator('#room-search').fill('')
    with page.expect_download() as download:
        page.get_by_role('button',name='Xuất bảng kiểm tra').click()
    download.value.save_as(folder/'review-export.csv')
    assert 'A105' in (folder/'review-export.csv').read_text(encoding='utf-8-sig')
    page.screenshot(path=str(folder/'review-desktop.png'),full_page=True)
    checks.append('Ready/skipped tabs, missing-water reason, existing invoice, sorting/filter/export all work')
    page.set_viewport_size({'width':360,'height':900})
    assert page.evaluate('document.documentElement.scrollWidth<=innerWidth')
    page.screenshot(path=str(folder/'review-mobile.png'),full_page=True)
    page.set_viewport_size({'width':1672,'height':945})
    page.locator('#ready-table tr').filter(has=page.get_by_role('rowheader',name=target['room'],exact=False)).get_by_role('link',name='Xem',exact=True).click()
    page.wait_for_load_state('networkidle')
    assert page.locator('#issue-date').input_value()=='2026-11-07'
    assert page.locator('#due-date').input_value()=='2026-11-25'
    page.get_by_role('button',name='Xác nhận và phát hành hóa đơn').click()
    assert page.locator('#issue-confirmation').is_visible()
    assert '07/11/2026' in page.locator('#issue-confirmation').inner_text()
    assert '25/11/2026' in page.locator('#issue-confirmation').inner_text()
    page.locator('#issue-confirmation').get_by_role('button',name='Xác nhận phát hành',exact=True).click()
    assert not posts, 'Confirmation checkbox must be required before POST'
    page.screenshot(path=str(folder/'review-confirmation.png'),full_page=True)
    page.locator('#issue-confirmation [name="xacNhan"]').check()
    page.locator('#issue-confirmation').get_by_role('button',name='Xác nhận phát hành',exact=True).click()
    page.wait_for_load_state('networkidle')
    assert len(posts)==1
    assert 'Đã phát hành 1 hóa đơn' in page.locator('main').inner_text()
    page.locator('[data-result-tab="missing"]').click()
    assert 'A105' in page.locator('#issue-results').inner_text()
    assert 'Thiếu chỉ số nước' in page.locator('#issue-results').inner_text()
    page.locator('[data-result-tab="other"]').click()
    assert 'Không chọn' in page.locator('#issue-results').inner_text()
    page.locator('[data-result-tab="issued"]').click()
    page.screenshot(path=str(folder/'review-results.png'),full_page=True)
    with sqlite3.connect(Path(access['database']).as_uri()+'?mode=ro',uri=True) as db:
        invoice,business,due,total=db.execute('SELECT id,ngay_phat_hanh_nghiep_vu,han_thanh_toan,tong_tien FROM hoa_don WHERE hop_dong_id=? AND nam=2026 AND thang=10',(target['contractId'],)).fetchone()
        assert (business,due,total)==('2026-11-07','2026-11-25',target['expectedTotal'])
        skipped=next(x for x in access['monthlyExamples'] if x['room']=='A105')
        assert not db.execute('SELECT 1 FROM hoa_don WHERE hop_dong_id=?',(skipped['contractId'],)).fetchone()
        assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
    page.goto(f'{base}/HoaDonDichVu/Details/{invoice}',wait_until='networkidle')
    assert '07/11/2026' in page.locator('.id-info').inner_text()
    assert '25/11/2026' in page.locator('.id-info').inner_text()
    checks.append(f"Explicit confirmation required; {target['room']} saved custom dates and full total; every skipped room has no new invoice; per-room results shown")
    assert not errors, errors
    browser.close()
(folder/'review-browser-checks.json').write_text(json.dumps({'checks':checks,'javascriptErrors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: '+'; '.join(checks))
