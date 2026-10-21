"""Create a NEW synthetic S4 acceptance DB. Credentials come from an ignored JSON file.
Usage: python verification/New-Sprint4AcceptanceDemo.py <private-request.json>
Never edits the existing database or changes the launch configuration.
"""
import calendar
from datetime import datetime, timedelta, timezone, date
import json
import os
from pathlib import Path
import secrets
import sqlite3
import subprocess
import sys
import uuid
import bcrypt

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'QL_PhongTro'
UTC = timezone.utc
VN = timezone(timedelta(hours=7))


def main():
    config = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    folder = ROOT / 'data/s4-acceptance' / (datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:6])
    generator = ROOT / 'data/s4-fix/demo-generator/S404Demo.dll'
    if not generator.exists():
        raise RuntimeError('Build verification/S404Demo into data/s4-fix/demo-generator first.')
    # The established fixture supplies real service prices, initial readings,
    # a checkout draft with damage/penalty, and a second active checkout contract.
    result = subprocess.run(['dotnet', str(generator), str(APP), str(folder), 's4052'],
                            capture_output=True, text=True, encoding='utf-8', errors='replace')
    if result.returncode:
        raise RuntimeError('Base fixture failed: ' + result.stderr[-1200:])
    (folder / 'generation.log').write_text(result.stdout, encoding='utf-8')
    dbpath = folder / 's404.sqlite'
    now = datetime.now(UTC)
    today = now.astimezone(VN).date()
    stamp = now.isoformat()
    month_index = today.year * 12 + today.month - 1
    periods = [divmod(month_index - offset, 12) for offset in range(11, -1, -1)]
    periods = [(year, zero_month + 1) for year, zero_month in periods]
    start = date(*periods[0], 1)
    end = date(today.year + 2, today.month, calendar.monthrange(today.year + 2, today.month)[1])
    access = []
    extra_password = 'S4-' + secrets.token_urlsafe(14) + '1a'
    with sqlite3.connect(dbpath) as db:
        db.execute('PRAGMA foreign_keys=ON')
        for account, role, email, password in [
            (1, 'CHU_NHA', config['ownerEmail'], config['ownerPassword']),
            (3, 'KHACH_THUE', config['tenantEmail'], config['tenantPassword']),
            (2, 'CHU_NHA', 'other.owner@s4.example.test', extra_password),
            (4, 'KHACH_THUE', 'other.tenant@s4.example.test', extra_password),
        ]:
            hashed = bcrypt.hashpw(password.encode(), bcrypt.gensalt(rounds=10)).decode()
            db.execute('UPDATE tai_khoan SET email=?,mat_khau=?,must_change_password=0 WHERE id=?', (email.strip().lower(), hashed, account))
            access.append(dict(role=role, email=email, password=password))
        hashed = bcrypt.hashpw(extra_password.encode(), bcrypt.gensalt(rounds=10)).decode()
        for account, role, email, name in [(5, 'QUAN_LY', 'manager@s4.example.test', 'Quản lý kiểm thử'),
                                           (6, 'ADMIN', 'admin@s4.example.test', 'Quản trị kiểm thử')]:
            db.execute('''INSERT INTO tai_khoan(id,ho_ten,email,so_dien_thoai,mat_khau,vai_tro,dang_hoat_dong,email_confirmed,ngay_tao,ngay_cap_nhat)
                VALUES(?,?,?,?,?,?,1,1,?,?)''', (account, name, email, f'090040400{account}', hashed, role, stamp, stamp))
            access.append(dict(role=role, email=email, password=extra_password))
        db.execute('UPDATE toa_nha SET quan_ly_id=5 WHERE id=1')
        db.execute("INSERT INTO toa_nha(id,chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi) VALUES(3,1,5,'Tòa kiểm thử công nợ và báo cáo','Địa chỉ dữ liệu giả')")

        for room in range(3, 501):
            active = room <= 480
            building = 1 if room <= 250 else 3
            code = f'TC{room:03d}'
            price = 3000000
            db.execute('''INSERT INTO phong_tro(id,toa_nha_id,ma_phong,tang,dien_tich,gia_thue,tien_coc_du_kien,so_nguoi_toi_da,trang_thai,ngay_tao)
                VALUES(?,?,?,?,25,?,?,3,?,?)''', (room, building, code, 1 + (room % 5), price, price, 'DANG_THUE' if active else 'TRONG', stamp))
            if not active:
                if room <= 490:
                    db.execute('''INSERT INTO tin_dang(phong_id,nguoi_dang_id,tieu_de,noi_dung,trang_thai,ngay_tao)
                        VALUES(?,1,?,?,'NHAP',?)''', (room, f'Phòng trống {code}', 'Tin nháp kiểm thử đăng một nút.', stamp))
                continue
            profile = 1 if room <= 12 else 1000 + room
            if profile != 1:
                db.execute('INSERT INTO khach_thue(id,ho_ten,ngay_tao) VALUES(?,?,?)', (profile, f'Khách dữ liệu giả {room:03d}', stamp))
            db.execute('''INSERT INTO hop_dong(id,ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,tien_coc_thoa_thuan,ngay_chot_hang_thang,nguoi_lap_id,ngay_tao)
                VALUES(?,?,?,?,'DANG_HIEU_LUC',?,1,1,?)''', (room, f'HD-S4-{room:04d}', room, profile, price, stamp))
            months = (end.year - start.year) * 12 + end.month - start.month + 1
            db.execute('''INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao)
                VALUES(?,1,?,?,?,?,1,?)''', (room, str(start), str(end), months, price, stamp))
            for year, month in periods:
                last = date(year, month, calendar.monthrange(year, month)[1])
                current = (year, month) == (today.year, today.month)
                previous = (year, month) == periods[-2]
                due = last + timedelta(days=7)
                paid = price
                if current and 3 <= room <= 7:
                    paid = (room - 2) * 300000  # five distinct partial payments
                    due = today - timedelta(days=room)
                elif previous and room in (3, 4):
                    paid = 1000000             # two rooms overdue in multiple periods
                    due = today - timedelta(days=40)
                elif current and room in (8, 9, 10):
                    paid = 0
                    due = today + timedelta(days={8: 2, 9: -1, 10: 7}[room])
                issue = min(last, due, today)
                invoice = db.execute('''INSERT INTO hoa_don(ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,
                    ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
                    VALUES(?,?,?,?,?,?,?,1,?,?,?,'NHAP',1) RETURNING id''',
                    (f'HD-S4-{room:04d}-{year}{month:02d}', room, month, year, str(date(year, month, 1)), str(last), str(last), stamp, str(due), price)).fetchone()[0]
                db.execute('''INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,loai_khoan,ten_khoan,don_vi_tinh,so_luong,don_gia,thanh_tien)
                    VALUES(?,1,'TIEN_PHONG','Tiền phòng','tháng',1,?,?)''', (invoice, price, price))
                db.execute("UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh=?,ngay_phat_hanh_nghiep_vu=?,nguoi_phat_hanh_id=1 WHERE id=?", (stamp, str(issue), invoice))
                if paid:
                    payment = db.execute("INSERT INTO thanh_toan(hoa_don_id,so_tien,ngay_thanh_toan,trang_thai) VALUES(?,?,?,'DA_XAC_NHAN') RETURNING id",
                                         (invoice, paid, str(min(today, due)))).fetchone()[0]
                    db.execute('''INSERT INTO lan_thu_hoa_don(thanh_toan_id,hinh_thuc,nguoi_xac_nhan_id,ten_nguoi_xac_nhan,thoi_diem_ghi,khoa_chong_trung)
                        VALUES(?,'CHUYEN_KHOAN',1,'Chủ nhà kiểm thử',?,?)''', (payment, stamp, uuid.uuid4().hex))
                if current and room <= 12:
                    notice(db, invoice, config['tenantEmail'], stamp, month, year, price, price - paid, due)
        notice(db, 1, config['tenantEmail'], stamp, 9, 2026, 1700000, 1200000, date(2026, 10, 7))
        for number, state, level in [(1, 'MOI', 'GAP'), (2, 'MOI', 'THUONG'), (3, 'DANG_XU_LY', 'GAP'), (4, 'HOAN_THANH', 'THUONG'), (5, 'TU_CHOI', 'THUONG')]:
            created = (now - timedelta(hours=26 + number)).isoformat()
            report = db.execute('''INSERT INTO bao_hong(ma_bao_hong,nguoi_bao_id,khach_thue_id,phong_id,hop_dong_id,loai_thiet_bi,mo_ta,muc_do,trang_thai,ngay_tao,khoa_chong_trung)
                VALUES(?,3,1,2,2,'Vòi nước',?,?,?, ?,?) RETURNING id''',
                (f'BH-{today:%Y%m}-{number:04d}', f'Báo hỏng dữ liệu giả - {state}', level, state, created, uuid.uuid4().hex)).fetchone()[0]
            if state != 'MOI':
                db.execute('''INSERT INTO bao_hong_trao_doi(bao_hong_id,nguoi_gui_id,ten_nguoi_gui,loai,noi_dung,thoi_diem,khoa_chong_trung)
                    VALUES(?,5,'Quản lý kiểm thử','PHAN_HOI','Đã kiểm tra thiết bị; đây là phản hồi mẫu.',?,?)''', (report, stamp, uuid.uuid4().hex))
        db.execute('INSERT INTO bao_hong_so_ma(thang,so_cuoi) VALUES(?,5)', (today.strftime('%Y%m'),))
        assert db.execute('PRAGMA integrity_check').fetchone() == ('ok',)
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
        counts = {table: db.execute(f'SELECT COUNT(*) FROM {table}').fetchone()[0] for table in ['phong_tro', 'hop_dong', 'hoa_don', 'thanh_toan', 'bao_hong']}
        debts = db.execute('''SELECT p.ma_phong,COUNT(*),SUM(h.tong_tien-COALESCE(t.paid,0)) FROM hoa_don h
            JOIN hop_dong c ON c.id=h.hop_dong_id JOIN phong_tro p ON p.id=c.phong_id
            LEFT JOIN (SELECT hoa_don_id,SUM(so_tien) paid FROM thanh_toan WHERE trang_thai='DA_XAC_NHAN' GROUP BY hoa_don_id) t ON t.hoa_don_id=h.id
            WHERE h.trang_thai='DA_PHAT_HANH' AND h.tong_tien>COALESCE(t.paid,0) GROUP BY p.id ORDER BY p.ma_phong''').fetchall()
    env = dict(os.environ, DatabasePath=str(dbpath), ASPNETCORE_ENVIRONMENT='Development')
    check = subprocess.run(['dotnet', str(APP / 'bin/Debug/net10.0/QL_PhongTro.dll'), '--check-database'], cwd=APP, env=env,
                           capture_output=True, text=True, encoding='utf-8', errors='replace')
    (folder / 'schema-check.log').write_text(check.stdout + check.stderr, encoding='utf-8')
    if check.returncode: raise RuntimeError('Schema check failed; launch settings unchanged.')
    (folder / 'access.json').write_text(json.dumps(dict(database=str(dbpath), accounts=access), ensure_ascii=False, indent=2), encoding='utf-8')
    manifest = dict(database=str(dbpath), today=str(today), counts=counts, periods=periods, debts=debts,
                    totalDebt=sum(row[2] for row in debts), checkoutContract=1, freshCheckoutContract=2)
    (folder / 'cases.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    (ROOT / 'data/s4-acceptance/latest.txt').write_text(str(folder), encoding='utf-8')
    print(json.dumps(dict(directory=str(folder), **counts, totalDebt=manifest['totalDebt']), ensure_ascii=False))


def notice(db, invoice, email, stamp, month, year, total, remaining, due):
    money = lambda value: f'{value:,}'.replace(',', '.')
    db.execute('''INSERT INTO thong_bao(hoa_don_id,nguoi_nhan_id,loai_thong_bao,tieu_de,noi_dung,duong_dan,email_nhan,ngay_tao,trang_thai_email)
        VALUES(?,3,'HOA_DON_MOI',?,?,?,?,?,'CHO_GUI')''',
        (invoice, f'Hóa đơn kỳ {month:02d}/{year}', f'Tổng tiền {money(total)} đ; còn phải trả {money(remaining)} đ; hạn {due:%d/%m/%Y}.',
         f'/ThongBao/HoaDon/{invoice}', email, stamp))


if __name__ == '__main__':
    main()
