"""Verify loading state in installed Chrome against the isolated 500-listing demo.
Install Playwright into data/task1-build/browser-packages; run after performance --serve.
"""
import asyncio
import json
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'data/task1-build/browser-packages'))
from playwright.async_api import async_playwright

async def main():
    folder = Path((ROOT / 'data/timtin-demo/latest-performance.txt').read_text(encoding='utf-8').strip())
    result = json.loads((folder / 'performance.json').read_text(encoding='utf-8'))
    checks = []
    async with async_playwright() as playwright:
        browser = await playwright.chromium.launch(channel='chrome', headless=True)
        page = await browser.new_page(viewport={'width': 1280, 'height': 900})
        shown = asyncio.Event()
        observed = []
        def on_console(message):
            if message.text.startswith('LOADING-CHECK:'):
                observed.append(json.loads(message.text.split(':', 1)[1]))
                shown.set()
        page.on('console', on_console)
        await page.add_init_script('''
            document.addEventListener('DOMContentLoaded', () => {
                const status = document.getElementById('tim-tin-loading');
                if (!status) return;
                new MutationObserver(() => {
                    if (!status.hidden) {
                        const rect = status.getBoundingClientRect();
                        console.log('LOADING-CHECK:' + JSON.stringify({
                            visible: getComputedStyle(status).display !== 'none' && rect.width > 0 && rect.height > 0,
                            busy: document.querySelector('main').getAttribute('aria-busy')
                        }));
                    }
                }).observe(status, {attributes: true, attributeFilter: ['hidden']});
            });
        ''')
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        await page.goto(result['url'], wait_until='networkidle')
        status = page.locator('#tim-tin-loading')
        assert not await status.is_visible()
        assert await page.locator('article').count() == 12
        checks.append('Initial spinner hidden; 12 rendered listings')

        async def navigate_and_check(trigger):
            arrived = asyncio.Event()
            release = asyncio.Event()
            async def hold(route):
                arrived.set()
                await release.wait()
                await route.continue_()
            await page.route('**/TimTin?**', hold)
            shown.clear()
            task = asyncio.create_task(trigger())
            try:
                await asyncio.wait_for(arrived.wait(), timeout=15)
                await asyncio.wait_for(shown.wait(), timeout=10)
                assert observed[-1]['visible'] and observed[-1]['busy'] == 'true', observed[-1]
            finally:
                release.set()
            await task
            await page.wait_for_load_state('networkidle')
            await page.unroute('**/TimTin?**', hold)
            assert not await page.locator('#tim-tin-loading').is_visible()
            assert await page.locator('main').get_attribute('aria-busy') is None
            assert await page.locator('article').count() <= 12

        await page.locator('#QuanHuyen').select_option('Quận 1')
        await page.locator('#SapXep').select_option('gia-tang')
        await navigate_and_check(lambda: page.get_by_role('button', name='Tìm kiếm').click())
        assert await page.locator('#QuanHuyen').input_value() == 'Quận 1'
        checks.append('Filter submit shows spinner/aria-busy while HTTP held; cleared after response; selection retained')
        await navigate_and_check(lambda: page.get_by_role('link', name='Trang sau').click())
        assert 'Trang=2' in page.url
        assert await page.locator('#SapXep').input_value() == 'gia-tang'
        checks.append('Page navigation shows spinner; clears after response; filter/sort retained')
        await page.go_back(wait_until='networkidle')
        assert not await page.locator('#tim-tin-loading').is_visible()
        checks.append('Browser back leaves loading indicator hidden')
        await page.locator('#GiaToiThieu').fill('-1')
        await page.get_by_role('button', name='Tìm kiếm').click()
        assert not await page.locator('#tim-tin-loading').is_visible()
        assert not await page.locator('#GiaToiThieu').evaluate('(e) => e.checkValidity()')
        checks.append('Browser rejects invalid form without showing spinner')
        await page.locator('#GiaToiThieu').fill('0')
        await page.locator('#GiaToiDa').fill('1')
        await navigate_and_check(lambda: page.get_by_role('button', name='Tìm kiếm').click())
        assert await page.locator('article').count() == 0
        assert await page.get_by_text('Không có tin phù hợp.', exact=False).is_visible()
        assert await page.get_by_text('Bạn hãy nới rộng khoảng giá thuê', exact=False).is_visible()
        checks.append('Zero results render visible suggestion to widen price range')
        await page.locator('#GiaToiThieu').fill('2100000')
        await page.locator('#GiaToiDa').fill('2200000')
        await page.locator('#DienTichToiThieu').fill('15')
        await page.locator('#DienTichToiDa').fill('25')
        await page.locator('#SoNguoiToiDa').select_option('3')
        await navigate_and_check(lambda: page.get_by_role('button', name='Tìm kiếm').click())
        assert await page.locator('article').count() == 0
        assert await page.locator('#tim-tin-suggested-range').is_visible()
        await navigate_and_check(lambda: page.get_by_role('link', name='Áp dụng khoảng giá gợi ý').click())
        assert await page.locator('#GiaToiThieu').input_value() == '1600000'
        assert await page.locator('#GiaToiDa').input_value() == '2700000'
        assert await page.locator('#QuanHuyen').input_value() == 'Quận 1'
        assert await page.locator('#DienTichToiThieu').input_value() == '15'
        assert await page.locator('#DienTichToiDa').input_value() == '25'
        assert await page.locator('#SoNguoiToiDa').input_value() == '3'
        assert await page.locator('#SapXep').input_value() == 'gia-tang'
        assert await page.locator('article').count() > 0
        assert await page.locator('#tim-tin-price-suggestion').count() == 0
        checks.append('Task 5: apply wider price range searches again, preserves area/size/capacity/sort, yields listings, hides suggestion')
        assert not errors, errors
        await page.screenshot(path=str(folder / 'browser-result.png'))
        await browser.close()
    (folder / 'browser.json').write_text(json.dumps({'passed': True, 'checks': checks}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'passed': True, 'checks': checks, 'artifacts': str(folder)}, ensure_ascii=False, indent=2))

if __name__ == '__main__':
    asyncio.run(main())
