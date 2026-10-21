"""Read-only business checks against the isolated S4-07:03 demo (login uses auth storage)."""
import argparse
import html
import json
import re
import runpy
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(folder):
    folder = folder.resolve()
    assert folder.parent == ROOT / 'data/s407-overdue' and folder.name.startswith('demo-')
    access = json.loads((folder / 'access.json').read_text(encoding='utf-8-sig'))
    assert Path(access['database']).resolve().parent == folder
    Session = runpy.run_path(str(Path(__file__).with_name('S407Demo.py')))['Session']
    manager = Session(access)
    manager.login('manager.demo@demo.local')
    ids = access['overdue_cases']

    def snapshot():
        with sqlite3.connect(Path(access['database']).as_uri() + '?mode=ro', uri=True) as db:
            return [db.execute('SELECT * FROM ' + name + ' ORDER BY id').fetchall()
                    for name in ('bao_hong', 'bao_hong_trao_doi', 'chi_phi_bao_hong', 'lich_su_chi_phi_bao_hong', 'nhat_ky_hoat_dong')]

    before = snapshot()
    status, listing, headers = manager.page('/QuanLyBaoHong')
    assert status == 200 and 'no-store' in headers['Cache-Control']
    for key, report_id in ids.items():
        status, detail, headers = manager.page(f'/QuanLyBaoHong/Details/{report_id}')
        assert status == 200 and 'no-store' in headers['Cache-Control']
        card = next(card for card in re.findall(r'<article\b.*?</article>', listing, re.S)
                    if f'/QuanLyBaoHong/Details/{report_id}"' in card)
        expected = key == 'urgent_26h'
        assert ('class="repair-overdue-label"' in detail) == expected, key
        assert ('class="repair-overdue-label"' in card) == expected, key
        if expected:
            assert 'Gấp — Quá 24 giờ' in detail and 'Gấp — Quá 24 giờ' in card
            assert re.search(r'26 giờ \d+ phút', detail) and re.search(r'26 giờ \d+ phút', card)
            assert 'repairs-card-overdue' in card
    assert snapshot() == before, 'GET changed business rows or audit'
    # A manager outside the building sees no warning data and cannot open a report.
    outsider = Session(access)
    outsider.login('manager.other.s407@demo.local')
    assert outsider.request(f'/QuanLyBaoHong/Details/{ids["urgent_26h"]}')[0] == 403
    assert 'repair-overdue-label' not in outsider.page('/QuanLyBaoHong')[1]
    report = {'passed': True, 'cases': list(ids), 'checked': ['list/detail warning and duration', 'ordinary/recent/handled exclusions', 'scope403', 'no-store', 'GET preserves business and audit']}
    (folder / 'overdue-http-results.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path)
    verify(parser.parse_args().folder)
