"""Measure complete HTTP responses against 500 synthetic listings in a NEW DB.
Usage: python verification/timtin_performance.py --runtime path/to/QL_PhongTro.dll --serve
Requires only Python 3 and the built application. Never opens a user's database for writing.
"""
import argparse
import html
import json
import math
import os
from pathlib import Path
import platform
import re
import socket
import sqlite3
import statistics
import subprocess
import time
from urllib.parse import urlencode
from urllib.request import urlopen
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime', type=Path, required=True)
    parser.add_argument('--serve', action='store_true')
    parser.add_argument('--repeats', type=int, default=10)
    args = parser.parse_args()
    assert args.repeats >= 1
    runtime = args.runtime.resolve()
    assert runtime.is_file(), 'Build the application first'
    folder = ROOT / 'data/timtin-demo' / ('performance-' + uuid4().hex)
    folder.mkdir(parents=True)
    database = folder / 'performance.sqlite'
    env = dict(os.environ, DatabasePath=str(database), ASPNETCORE_ENVIRONMENT='Development',
               PasswordReset__PickupDirectory=str(folder / 'mail'))
    init = subprocess.run(['dotnet', str(runtime), '--initialize-database'], cwd=ROOT / 'QL_PhongTro',
                          env=env, capture_output=True, text=True)
    (folder / 'initialize.log').write_text(init.stdout + init.stderr, encoding='utf-8')
    assert init.returncode == 0, init.stdout + init.stderr
    records = []
    with sqlite3.connect(database) as c:
        c.execute('PRAGMA foreign_keys=ON')
        # An inactive synthetic owner with an unusable random hash; no fixed password.
        c.execute("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat,email_confirmed,is_deleted) VALUES('Chủ nhà mẫu','owner@example.invalid','0900000000',?,'CHU_NHA',0,0,0,'2026-01-01','2026-01-01',1,0)", (uuid4().hex,))
        owner = c.execute('SELECT last_insert_rowid()').fetchone()[0]
        buildings = []
        for area in range(1, 6):
            c.execute('INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,quan_huyen) VALUES(?,?,?,?)',
                      (owner, f'Tòa mẫu {area}', f'Địa chỉ mẫu {area}', f'Quận {area}'))
            buildings.append(c.execute('SELECT last_insert_rowid()').fetchone()[0])
        for i in range(500):
            area = i % 5 + 1
            price = 1000000 + (i % 13) * 250000
            size = 10 + (i % 17) * 1.25
            capacity = i % 4 + 1
            state = ['DANG_HIEN_THI'] * 6 + ['TAM_AN', 'NHAP', 'DA_CHO_THUE', 'DANG_HIEN_THI']
            state = state[i % 10]
            expiry = '2000-01-01 00:00:00' if i % 10 == 9 else '2099-01-01 00:00:00'
            date = f'2026-01-{i % 28 + 1:02d} 00:00:00'
            title = f'PERF-{i:03d}'
            c.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?,?,1,?,?,?,'TRONG','2026-01-01')",
                      (buildings[area - 1], title, size, price, capacity))
            room = c.execute('SELECT last_insert_rowid()').fetchone()[0]
            c.execute('INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_dang,ngay_het_han,ngay_tao) VALUES(?,?,?,?,?,?,?)',
                      (room, owner, title, state, date, expiry, '2026-01-01'))
            identity = c.execute('SELECT last_insert_rowid()').fetchone()[0]
            if state == 'DANG_HIEN_THI' and expiry.startswith('2099'):
                records.append(dict(title=title, area=f'Quận {area}', price=price, size=size,
                                    capacity=capacity, date=date, id=identity))
        assert c.execute('SELECT count(*) FROM tin_dang').fetchone()[0] == 500
        assert not c.execute('PRAGMA foreign_key_check').fetchall()
        assert c.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://localhost:{port}'
    log = (folder / 'server.log').open('w', encoding='utf-8')
    startup_started = time.perf_counter()
    server = subprocess.Popen(['dotnet', str(runtime), '--urls', base], cwd=ROOT / 'QL_PhongTro', env=env,
                              stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    report = {'database': str(database), 'url': base + '/TimTin', 'pid': server.pid,
              'rows': 500, 'valid_rows': len(records), 'platform': platform.platform(),
              'build': str(runtime), 'measurement': 'localhost HTTP GET through complete HTML body; sequential single user; no browser rendering/static assets',
              'scenarios': []}
    try:
        # Wait for TCP only; don't warm the search endpoint before measuring first request.
        for _ in range(120):
            if server.poll() is not None:
                raise RuntimeError((folder / 'server.log').read_text(encoding='utf-8'))
            try:
                with socket.create_connection(('localhost', port), timeout=0.5):
                    break
            except OSError:
                time.sleep(0.25)
        else:
            raise RuntimeError('Server startup timeout')

        report['startup_ready_ms'] = round((time.perf_counter() - startup_started) * 1000, 3)

        def measure(filters):
            start = time.perf_counter()
            with urlopen(base + '/TimTin?' + urlencode(filters), timeout=10) as response:
                raw = response.read()
                status = response.status
            elapsed = (time.perf_counter() - start) * 1000
            page = html.unescape(raw.decode())
            assert status == 200
            expected = records[:]
            if filters.get('QuanHuyen'):
                expected = [r for r in expected if r['area'] == filters['QuanHuyen']]
            for field, key, lower in [('GiaToiThieu', 'price', True), ('GiaToiDa', 'price', False),
                                      ('DienTichToiThieu', 'size', True), ('DienTichToiDa', 'size', False)]:
                if field in filters:
                    value = float(filters[field])
                    expected = [r for r in expected if (r[key] >= value if lower else r[key] <= value)]
            if 'SoNguoiToiDa' in filters:
                expected = [r for r in expected if r['capacity'] == int(filters['SoNguoiToiDa'])]
            total = len(expected)
            pages = math.ceil(total / 12)
            mode = filters.get('SapXep', 'moi-nhat')
            expected.sort(key=lambda r: (r['price'], -r['id']) if mode == 'gia-tang'
                          else (r['price'], r['id']) if mode == 'gia-giam' else (r['date'], r['id']),
                          reverse=mode != 'gia-tang')
            number = max(1, min(int(filters.get('Trang', 1)), max(1, pages)))
            actual = re.findall(r'<h2[^>]*>(?:<a[^>]*>)?(PERF-\d+)(?:</a>)?</h2>', page)
            assert actual == [r['title'] for r in expected[(number - 1) * 12:number * 12]], (filters, actual)
            assert f'{total} tin phù hợp · {pages} trang' in page
            assert ('Bạn hãy nới rộng khoảng giá thuê' in page) == (total == 0)
            assert 'id="tim-tin-loading"' in page and '/js/tim-tin.js' in page
            return round(elapsed, 3), total

        first_ms, _ = measure({})
        report['first_search_ms'] = first_ms
        scenarios = [
            ('no_filters', {}), ('no_results', {'GiaToiDa': 1}), ('district', {'QuanHuyen': 'Quận 1'}),
            ('price', {'GiaToiThieu': 1500000, 'GiaToiDa': 3000000}),
            ('area', {'DienTichToiThieu': 15, 'DienTichToiDa': 25}),
            ('capacity', {'SoNguoiToiDa': 3}),
            ('all_filters', {'QuanHuyen': 'Quận 1', 'GiaToiThieu': 1500000, 'GiaToiDa': 3000000,
                             'DienTichToiThieu': 15, 'DienTichToiDa': 25, 'SoNguoiToiDa': 3}),
            ('newest', {'SapXep': 'moi-nhat'}), ('price_asc', {'SapXep': 'gia-tang'}),
            ('price_desc', {'SapXep': 'gia-giam'}), ('page_2', {'Trang': 2}),
            ('last_page', {'Trang': math.ceil(len(records) / 12)}),
            ('filtered_page_2', {'QuanHuyen': 'Quận 1', 'SapXep': 'gia-tang', 'Trang': 2})]
        for name, filters in scenarios:
            times = []
            for _ in range(args.repeats):
                ms, total = measure(filters)
                times.append(ms)
            ordered = sorted(times)
            report['scenarios'].append(dict(name=name, filters=filters, count=total, samples_ms=times,
                                            median_ms=round(statistics.median(times), 3),
                                            p95_ms=ordered[math.ceil(len(times) * 0.95) - 1], max_ms=max(times)))
        report['passed'] = first_ms < 2000 and all(s['max_ms'] < 2000 for s in report['scenarios'])
        (folder / 'performance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps({k: v for k, v in report.items() if k != 'scenarios'}, ensure_ascii=False, indent=2))
        for scenario in report['scenarios']:
            print(f"{scenario['name']}: median={scenario['median_ms']}ms p95={scenario['p95_ms']}ms max={scenario['max_ms']}ms count={scenario['count']}")
        assert report['passed'], 'At least one response exceeded 2 seconds; see performance.json'
        pointer = 'latest-performance.txt' if args.serve else 'latest-performance-test.txt'
        (folder.parent / pointer).write_text(str(folder), encoding='utf-8')
        if args.serve:
            print('Demo remains running; stop only the PID shown above when finished.')
            server = None
    finally:
        if server is not None:
            server.terminate()
            server.wait(timeout=15)
        log.close()


if __name__ == '__main__':
    main()
