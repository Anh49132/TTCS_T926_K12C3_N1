"""Task 1/2: fresh synthetic demo + HTTP checks; upgrade only a disposable copy.

Build first: dotnet build QL_PhongTro -o data/task1-build/runtime
Run: python verification/timtin_http.py [--serve]
No third-party Python dependencies. Never writes to the source database.
"""
import argparse
import hashlib
import html
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import time
from urllib.parse import urlencode, parse_qs, urlsplit, urljoin
from urllib.request import urlopen
from datetime import datetime, timezone
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
DLL = ROOT / 'data/task1-build/runtime/QL_PhongTro.dll'


def snapshot(connection):
    tables = [r[0] for r in connection.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name!='app_schema_version'")]
    return {t: connection.execute('SELECT * FROM "' + t + '" ORDER BY rowid').fetchall() for t in tables}


def main():
    global DLL
    parser = argparse.ArgumentParser()
    parser.add_argument('--runtime', type=Path, default=DLL, help='Path to the built QL_PhongTro.dll')
    parser.add_argument('--task5', action='store_true', help='Verify widened price suggestions and applying them')
    parser.add_argument('--task3', action='store_true', help='Also verify sorting and pagination with 25 synthetic listings')
    parser.add_argument('--serve', action='store_true', help='Keep the tested synthetic demo running')
    args = parser.parse_args()
    DLL = args.runtime.resolve()
    folder = ROOT / 'data/timtin-demo' / uuid4().hex
    folder.mkdir(parents=True)
    database = folder / 'demo.sqlite'
    env = os.environ.copy()
    env.update(DatabasePath=str(database), ASPNETCORE_ENVIRONMENT='Development')
    # No SMTP or fixed login credential is needed for this public search demo.
    env['PasswordReset__PickupDirectory'] = str(folder / 'mail')
    checks = []

    def run(flag, success=True):
        result = subprocess.run(['dotnet', str(DLL), flag], cwd=APP, env=env, capture_output=True, text=True)
        with (folder / 'commands.log').open('a', encoding='utf-8') as log:
            log.write(flag + '\n' + result.stdout + result.stderr + '\n')
        assert (result.returncode == 0) == success, result.stdout + result.stderr

    run('--initialize-database')
    with sqlite3.connect(database) as c:
        assert c.execute('SELECT count(*) FROM tai_khoan').fetchone()[0] == 0
        assert c.execute('SELECT max(version) FROM app_schema_version').fetchone()[0] == 9
    before = database.read_bytes()
    run('--initialize-database', success=False)
    assert before == database.read_bytes(), 'Initializer changed an existing file'
    checks.append('Fresh initialization v9, empty accounts, refuses overwrite')

    # SQLite consistent backup from a read-only source; all upgrade writes go to copy.
    source = Path(env.get('TIMTIN_SOURCE_DATABASE', str(APP / 'Data/local-dev.sqlite'))).resolve()
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    upgrade = folder / 'upgrade-copy.sqlite'
    with sqlite3.connect(source.as_uri() + '?mode=ro', uri=True) as c:
        original = snapshot(c)
        with sqlite3.connect(upgrade) as target:
            c.backup(target)
    env['DatabasePath'] = str(upgrade)
    run('--update-database')
    run('--check-database')
    run('--update-database')
    with sqlite3.connect(upgrade) as c:
        after = snapshot(c)
        assert all(after[t] == rows for t, rows in original.items()), 'Existing rows changed'
        assert c.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not c.execute('PRAGMA foreign_key_check').fetchall()
        assert c.execute('SELECT max(version) FROM app_schema_version').fetchone()[0] == 9
    assert list(folder.glob('upgrade-copy.sqlite.before-update-*.bak')), 'Missing upgrade backup'
    assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
    checks.append('Copy upgrade v9, backup, rerun, integrity/FK, all existing rows preserved, source hash unchanged')

    env['DatabasePath'] = str(database)
    with sqlite3.connect(database) as c:
        c.execute('PRAGMA foreign_keys=ON')
        now = datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S')
        c.execute("INSERT INTO tai_khoan(ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,is_staff,is_superuser,ngay_tao,ngay_cap_nhat,email_confirmed,is_deleted) VALUES(?,?,?,?,?,0,0,0,?,?,1,0)",
                  ('Chủ nhà mẫu', 'owner@example.invalid', '0900000000', uuid4().hex, 'CHU_NHA', now, now))
        owner = c.execute('SELECT last_insert_rowid()').fetchone()[0]
        districts = ['Quận 1', 'Quận 2']
        buildings = []
        for district in districts:
            c.execute('INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,quan_huyen) VALUES(?,?,?,?)', (owner, 'Tòa mẫu ' + district, 'Địa chỉ mẫu', district))
            buildings.append(c.execute('SELECT last_insert_rowid()').fetchone()[0])
        fixtures = [
            ('A', 0, 1000000, 10.25, 2, 'DANG_HIEN_THI', '2099-01-01 00:00:00'),
            ('B', 0, 2000000, 20.5, 3, 'DANG_HIEN_THI', '2099-01-01 00:00:00'),
            ('C', 1, 2000000, 20.5, 3, 'DANG_HIEN_THI', '2099-01-01 00:00:00'),
            ('D', 1, 3000000, 30.75, 4, 'DANG_HIEN_THI', '2099-01-01 00:00:00'),
            ('HIDDEN', 0, 2000000, 20.5, 3, 'TAM_AN', '2099-01-01 00:00:00'),
            ('EXPIRED', 0, 2000000, 20.5, 3, 'DANG_HIEN_THI', '2000-01-01 00:00:00'),
            ('DRAFT', 0, 2000000, 20.5, 3, 'NHAP', '2099-01-01 00:00:00'),
            ('RENTED', 0, 2000000, 20.5, 3, 'DA_CHO_THUE', '2099-01-01 00:00:00'),
            ('NULL_EXPIRY', 0, 2000000, 20.5, 3, 'DANG_HIEN_THI', None),
        ]
        for name, area, price, size, people, state, expiry in fixtures:
            c.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?,?,1,?,?,?,'TRONG',?)", (buildings[area], name, size, price, people, now))
            room = c.execute('SELECT last_insert_rowid()').fetchone()[0]
            c.execute('INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_het_han,ngay_tao) VALUES(?,?,?,?,?,?)', (room, owner, 'TIN-' + name, state, expiry, now))

    # Bind a free port for the isolated demo.
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://localhost:{port}'
    log = (folder / 'server.log').open('w', encoding='utf-8')
    server = subprocess.Popen(['dotnet', str(DLL), '--urls', base], cwd=APP, env=env, stdout=log, stderr=log,
                              creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    try:
        for _ in range(120):
            if server.poll() is not None:
                raise RuntimeError((folder / 'server.log').read_text(encoding='utf-8'))
            try:
                with urlopen(base + '/TimTin', timeout=2):
                    break
            except OSError:
                time.sleep(0.5)
        else:
            raise RuntimeError('Server did not start')

        def request(filters):
            with urlopen(base + '/TimTin?' + urlencode(filters), timeout=15) as response:
                assert response.status == 200
                return html.unescape(response.read().decode())

        cases = [
            ({}, {'A', 'B', 'C', 'D'}),
            ({'QuanHuyen': 'Quận 1'}, {'A', 'B'}),
            ({'QuanHuyen': 'Quận 2'}, {'C', 'D'}),
            ({'GiaToiThieu': 1000000, 'GiaToiDa': 2000000}, {'A', 'B', 'C'}),
            ({'GiaToiThieu': 2000000}, {'B', 'C', 'D'}),
            ({'GiaToiDa': 1000000}, {'A'}),
            ({'DienTichToiThieu': '10.25', 'DienTichToiDa': '20.5'}, {'A', 'B', 'C'}),
            ({'DienTichToiThieu': '20.5'}, {'B', 'C', 'D'}),
            ({'DienTichToiDa': '10.25'}, {'A'}),
            ({'SoNguoiToiDa': 3}, {'B', 'C'}),
            ({'SoNguoiToiDa': 2}, {'A'}),
            ({'SoNguoiToiDa': 4}, {'D'}),
            ({'QuanHuyen': 'Quận 1', 'GiaToiThieu': 2000000}, {'B'}),
            ({'GiaToiDa': 2000000, 'DienTichToiThieu': '20.5'}, {'B', 'C'}),
            ({'QuanHuyen': 'Quận 2', 'GiaToiThieu': 2000000, 'GiaToiDa': 2000000,
              'DienTichToiThieu': '20.5', 'DienTichToiDa': '20.5', 'SoNguoiToiDa': 3}, {'C'}),
            ({'QuanHuyen': '', 'GiaToiThieu': '', 'GiaToiDa': '', 'DienTichToiThieu': '', 'DienTichToiDa': '', 'SoNguoiToiDa': ''}, {'A', 'B', 'C', 'D'}),
            ({'GiaToiDa': 0}, set()),
        ]
        for filters, expected in cases:
            page = request(filters)
            actual = set(re.findall(r'<h2[^>]*>(?:<a[^>]*>)?TIN-([^<]+)(?:</a>)?</h2>', page))
            assert actual == expected, (filters, actual, expected)
            assert ('Bạn hãy nới rộng khoảng giá thuê' in page) == (not expected)
            for field, value in filters.items():
                if str(value) == '':
                    continue
                if field in ('QuanHuyen', 'SoNguoiToiDa'):
                    assert re.search(r'<option(?=[^>]*value="' + re.escape(str(value)) + r'")(?=[^>]*selected)[^>]*>', page), (field, 'selection lost')
                else:
                    assert re.search(r'<input(?=[^>]*name="' + field + r'")(?=[^>]*value="' + re.escape(str(value)) + r'")[^>]*>', page), (field, 'value lost')
        errors = [
            ({'GiaToiThieu': 3000000, 'GiaToiDa': 1000000}, 'Giá thuê tối thiểu không được lớn hơn'),
            ({'DienTichToiThieu': 30, 'DienTichToiDa': 10}, 'Diện tích tối thiểu không được lớn hơn'),
            ({'GiaToiThieu': -1}, 'Giá thuê phải là số không âm'),
            ({'DienTichToiDa': -1}, 'Diện tích phải là số không âm'),
            ({'SoNguoiToiDa': 0}, 'Số người ở tối đa phải là số nguyên dương'),
            ({'QuanHuyen': 'Không tồn tại'}, 'Quận/huyện không thuộc danh sách'),
            ({'SoNguoiToiDa': 99}, 'Số người ở tối đa không thuộc danh sách'),
            ({'GiaToiDa': 'abc'}, 'field-validation-error'),
        ]
        for filters, message in errors:
            page = request(filters)
            assert message in page and not re.search(r'<h2[^>]*>(?:<a[^>]*>)?TIN-', page), (filters, 'validation failed')
        checks.append(f'HTTP: {len(cases)} valid/combined/boundary/blank cases and {len(errors)} invalid cases; selections retained; hidden/expired/draft/rented/NULL expiry excluded')
        if args.task3:
            listings = []
            with sqlite3.connect(database) as c:
                c.execute('PRAGMA foreign_keys=ON')
                c.execute('INSERT INTO toa_nha(chu_nha_id,ten_toa_nha,dia_chi,quan_huyen) VALUES(?,?,?,?)',
                          (owner, 'Tòa phân trang mẫu', 'Địa chỉ mẫu', 'Quận 3'))
                building = c.execute('SELECT last_insert_rowid()').fetchone()[0]
                for i in range(1, 26):
                    price = 1000000 + i * 100000
                    date = f'2026-01-{(i * 7) % 25 + 1:02d} 00:00:00' if i < 25 else None
                    c.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?,?,1,25,?,5,'TRONG',?)",
                              (building, f'PAGE-{i}', price, now))
                    room = c.execute('SELECT last_insert_rowid()').fetchone()[0]
                    c.execute("INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,trang_thai,ngay_dang,ngay_het_han,ngay_tao) VALUES(?,?,?,'DANG_HIEN_THI',?,'2099-01-01 00:00:00',?)",
                              (room, owner, f'TIN-P{i:02d}', date, now))
                    listings.append((f'P{i:02d}', price, date or '', c.execute('SELECT last_insert_rowid()').fetchone()[0]))

            def titles(page):
                return re.findall(r'<h2[^>]*>(?:<a[^>]*>)?TIN-([^<]+)(?:</a>)?</h2>', page)

            tested = 0
            for mode in ('moi-nhat', 'gia-tang', 'gia-giam'):
                ordered = sorted(listings, key=(lambda t: (t[2], t[3])) if mode == 'moi-nhat'
                                 else (lambda t: (t[1], -t[3] if mode == 'gia-tang' else t[3])), reverse=mode != 'gia-tang')
                combined = []
                for number in (1, 2, 3):
                    page = request({'QuanHuyen': 'Quận 3', 'SapXep': mode, 'Trang': number})
                    expected = [t[0] for t in ordered[(number - 1) * 12:number * 12]]
                    assert titles(page) == expected, (mode, number, titles(page), expected)
                    assert '25 tin phù hợp · 3 trang' in page
                    assert f'Trang {number} / 3' in page
                    assert 'aria-label="Chuyển trang kết quả"' in page
                    combined.extend(titles(page))
                    tested += 1
                assert len(set(combined)) == 25
            default = request({'QuanHuyen': 'Quận 3'})
            assert titles(default) == titles(request({'QuanHuyen': 'Quận 3', 'SapXep': 'moi-nhat'}))
            tested += 1
            for count in (0, 11, 12, 13):
                page = request({'QuanHuyen': 'Quận 3', 'GiaToiDa': 1000000 + count * 100000})
                assert len(titles(page)) == min(count, 12)
                assert f'{count} tin phù hợp · {(count + 11) // 12} trang' in page
                assert ('aria-label="Chuyển trang kết quả"' in page) == (count > 12)
                tested += 1
            for number, expected_number in ((0, 1), (-1, 1), (999999, 3)):
                page = request({'QuanHuyen': 'Quận 3', 'Trang': number})
                assert f'Trang {expected_number} / 3' in page
                tested += 1
            filters = {'QuanHuyen': 'Quận 3', 'GiaToiThieu': '1100000', 'GiaToiDa': '3500000',
                       'DienTichToiThieu': '25', 'DienTichToiDa': '25', 'SoNguoiToiDa': '5', 'SapXep': 'gia-tang'}
            page = request(filters)
            links = re.findall(r'href="([^"]+)"[^>]*>Trang sau</a>', page)
            assert len(links) == 1
            target = links[0]
            query = parse_qs(urlsplit(target).query)
            assert all(query[k] == [v] for k, v in filters.items()) and query['Trang'] == ['2']
            with urlopen(urljoin(base, target), timeout=15) as response:
                second = html.unescape(response.read().decode())
            assert titles(second) == [t[0] for t in listings[12:24]]
            # A filter/sort form on page 2 must omit Trang; its GET submission starts at 1.
            form = re.search(r'<form\b.*?</form>', second, flags=re.S).group(0)
            assert 'method="get"' in form and not re.search(r'name="Trang"', form, flags=re.I)
            assert 'selected="selected" value="gia-tang"' in form or re.search(r'<option(?=[^>]*value="gia-tang")(?=[^>]*selected)', form)
            changed = dict(filters, SapXep='gia-giam')
            assert 'Trang 1 / 3' in request(changed)
            changed = dict(filters, GiaToiThieu='1200000')
            assert 'Trang 1 / 2' in request(changed)
            tested += 4
            # Equal prices/dates use descending unique ID to avoid unstable page boundaries.
            with sqlite3.connect(database) as c:
                c.execute("UPDATE phong_tro SET gia_thue=2000000 WHERE toa_nha_id=?", (building,))
                c.execute("UPDATE tin_dang SET ngay_dang='2026-01-01 00:00:00' WHERE phong_id IN (SELECT id FROM phong_tro WHERE toa_nha_id=?)", (building,))
            for mode in ('moi-nhat', 'gia-tang', 'gia-giam'):
                page = request({'QuanHuyen': 'Quận 3', 'SapXep': mode})
                assert titles(page) == [t[0] for t in reversed(listings[-12:])]
                tested += 1
            # Restore useful varied demo data after the tie checks.
            with sqlite3.connect(database) as c:
                for name, price, date, identity in listings:
                    c.execute('UPDATE tin_dang SET ngay_dang=? WHERE id=?', (date or None, identity))
                    c.execute('UPDATE phong_tro SET gia_thue=? WHERE id=(SELECT phong_id FROM tin_dang WHERE id=?)', (price, identity))
            page = request({'SapXep': 'invalid'})
            assert 'Cách sắp xếp không hợp lệ.' in page and not titles(page)
            tested += 1
            checks.append(f'Task 3: {tested} sorting/pagination/count/link/reset/tie/invalid-mode checks PASS; each page <=12 listings')
        if args.task5:
            tested = 0
            fixed = {'QuanHuyen': 'Quận 1', 'DienTichToiThieu': '10', 'DienTichToiDa': '25', 'SoNguoiToiDa': '2', 'SapXep': 'gia-tang'}
            suggestion_cases = [
                (dict(fixed, GiaToiThieu='1400000', GiaToiDa='1500000'), '900000', '2000000', {'A'}),
                (dict(fixed, GiaToiDa='500001'), '', '1000001', {'A'}),
                (dict(fixed, SoNguoiToiDa='3', GiaToiThieu='2200000'), '1700000', '', {'B'}),
                (dict(fixed, GiaToiThieu='9223372036854775807', GiaToiDa='9223372036854775807'), '9223372036854275807', '9223372036854775807', set()),
                (dict(fixed, GiaToiDa='0'), '', '500000', set()),
            ]
            for filters, minimum, maximum, expected in suggestion_cases:
                page = request(filters)
                assert not re.findall(r'<h2[^>]*>(?:<a[^>]*>)?TIN-', page)
                assert 'Không có tin phù hợp.' in page and 'Khoảng giá đề xuất:' in page
                match = re.search(r'<a(?=[^>]*id="tim-tin-price-suggestion")(?=[^>]*href="([^"]+)")[^>]*>', page)
                assert match, 'Missing apply link'
                target = match.group(1)
                query = parse_qs(urlsplit(target).query, keep_blank_values=True)
                assert query.get('GiaToiThieu', [''])[0] == minimum
                assert query.get('GiaToiDa', [''])[0] == maximum
                for key in ('QuanHuyen', 'DienTichToiThieu', 'DienTichToiDa', 'SoNguoiToiDa', 'SapXep'):
                    assert query[key] == [filters[key]], (key, query)
                assert query['Trang'] == ['1']
                with urlopen(urljoin(base, target), timeout=15) as response:
                    applied = html.unescape(response.read().decode())
                assert set(re.findall(r'<h2[^>]*>(?:<a[^>]*>)?TIN-([^<]+)(?:</a>)?</h2>', applied)) == expected
                if expected:
                    assert 'tim-tin-price-suggestion' not in applied and 'Khoảng giá đề xuất:' not in applied
                tested += 1
            for filters in (dict(fixed, DienTichToiThieu='50', DienTichToiDa='100'),
                            dict(fixed, GiaToiThieu='0', GiaToiDa='9223372036854775807', DienTichToiThieu='50', DienTichToiDa='100')):
                page = request(filters)
                assert 'Không có tin phù hợp.' in page and 'không còn giới hạn để nới thêm' in page
                assert 'tim-tin-price-suggestion' not in page
                tested += 1
            page = request(dict(fixed, GiaToiThieu='3000000', GiaToiDa='1000000'))
            assert 'Giá thuê tối thiểu không được lớn hơn' in page and 'tim-tin-price-suggestion' not in page
            tested += 1
            page = request(fixed)
            assert 'TIN-A' in page and 'tim-tin-price-suggestion' not in page
            tested += 1
            checks.append(f'Task 5: {tested} suggestion/apply/preserve/no-results/open-boundary/overflow/invalid cases PASS')
        result = {'url': base + '/TimTin', 'database': str(database), 'pid': server.pid, 'checks': checks}
        (folder / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        (folder.parent / 'latest.txt').write_text(str(folder), encoding='utf-8')
        print(json.dumps(result, ensure_ascii=False, indent=2))
        if args.serve:
            print('Demo server remains running. Stop only the PID shown above when finished.')
            server = None
    finally:
        if server is not None:
            server.terminate()
            server.wait(timeout=15)
        log.close()


if __name__ == '__main__':
    main()
