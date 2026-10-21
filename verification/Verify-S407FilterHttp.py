"""GET-only business verification of the dedicated S4-07:04 fake demo."""
import argparse
import json
import re
import runpy
import sqlite3
from pathlib import Path
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]


def verify(folder):
    folder = folder.resolve()
    assert folder.parent == ROOT / 'data/s407-filters' and folder.name.startswith('demo-')
    access = json.loads((folder / 'access.json').read_text(encoding='utf-8-sig'))
    assert Path(access['database']).resolve().parent == folder
    Session = runpy.run_path(str(Path(__file__).with_name('S407Demo.py')))['Session']
    manager = Session(access)
    manager.login('manager.demo@demo.local')
    a, b, empty, foreign = (access['filter_buildings'][x] for x in ['A', 'B', 'Trống', 'Ngoài phạm vi'])

    def snapshot():
        with sqlite3.connect(Path(access['database']).as_uri() + '?mode=ro', uri=True) as db:
            return [db.execute('SELECT * FROM ' + name + ' ORDER BY id').fetchall() for name in
                    ('bao_hong', 'bao_hong_trao_doi', 'chi_phi_bao_hong', 'lich_su_chi_phi_bao_hong', 'nhat_ky_hoat_dong')]

    before = snapshot()
    cases = [({'toaNhaId': a}, 5), ({'toaNhaId': b}, 1),
             ({'toaNhaId': a, 'trangThai': 'MOI'}, 2),
             ({'toaNhaId': a, 'mucDo': 'GAP'}, 3),
             ({'toaNhaId': a, 'trangThai': 'MOI', 'mucDo': 'GAP'}, 1),
             ({'toaNhaId': empty, 'trangThai': 'MOI', 'mucDo': 'GAP'}, 0)]
    for query, expected in cases:
        status, page, headers = manager.page('/QuanLyBaoHong?' + urlencode(query))
        assert status == 200 and 'no-store' in headers['Cache-Control']
        assert len(re.findall(r'<article\b', page)) == expected
        options = re.search(r'<select[^>]*id="repair-building".*?</select>', page, re.S).group(0)
        assert f'value="{foreign}"' not in options
        if len(query) == 3 and expected:
            assert 'Gấp — Quá 24 giờ' in page
            detail = re.search(r'<a[^>]*href="([^"]*/Details/[^"]+)"', page).group(1)
            assert 'toaNhaId=' in detail and 'trangThai=MOI' in detail and 'mucDo=GAP' in detail
            status, detail_page, _ = manager.page(detail)
            assert status == 200 and 'Gấp — Quá 24 giờ' in detail_page
            back = re.search(r'<a[^>]*href="([^"]+)"[^>]*>← Báo hỏng được phân công</a>', detail_page).group(1)
            assert manager.page(back)[1].count('<article ') == 1
        if expected == 0:
            assert 'Không có báo hỏng phù hợp với bộ lọc đã chọn.' in page
        reset = re.search(r'<a[^>]*href="([^"]+)"[^>]*>Xóa bộ lọc</a>', page).group(1)
        assert reset == '/QuanLyBaoHong'
    assert len(re.findall(r'<article\b', manager.page('/QuanLyBaoHong')[1])) > 5
    assert manager.request('/QuanLyBaoHong?' + urlencode({'toaNhaId': foreign}))[0] == 403
    for query in ['trangThai=BAD', 'mucDo=BAD', 'toaNhaId=abc']:
        assert manager.request('/QuanLyBaoHong?' + query)[0] == 400
    assert snapshot() == before
    report = {'passed': True, 'queries': len(cases), 'checks': ['individual/combined filters', 'empty/reset', 'detail/back context', 'overdue label', 'scope403', 'invalid400', 'GET preserves business/audit']}
    (folder / 'filter-http-results.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path)
    verify(parser.parse_args().folder)
