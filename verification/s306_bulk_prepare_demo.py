"""Add 50 synthetic ready rooms to a NEW isolated S3-06 demo; preserve existing fixtures."""
import json
import sqlite3
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = Path(sys.argv[1]).resolve()
assert folder.is_relative_to(root / 'data/s306-demo')
manifest = folder / 'access.json'
access = json.loads(manifest.read_text(encoding='utf-8-sig'))
database = Path(access['database']).resolve()
assert database.is_relative_to(folder)
with sqlite3.connect(database) as db:
    db.execute('PRAGMA foreign_keys=ON')
    assert not db.execute("SELECT 1 FROM toa_nha WHERE ten_toa_nha='Demo phát hành 50 phòng'").fetchone(), 'Use a new demo'
    original = access['monthlyBuildingId']
    owner, manager = db.execute('SELECT chu_nha_id,quan_ly_id FROM toa_nha WHERE id=?', (original,)).fetchone()
    building = db.execute("INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) VALUES(?,?,'Demo phát hành 50 phòng','Địa chỉ mẫu kiểm thử S3-06',31,1) RETURNING id", (owner, manager)).fetchone()[0]
    db.execute('INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) SELECT ?,dich_vu_id,0 FROM dich_vu_toa_nha WHERE toa_nha_id=?', (building, original))
    db.execute('INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) SELECT ?,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao FROM cau_hinh_dich_vu WHERE toa_nha_id=?', (building, original))
    meter_services = dict(db.execute("SELECT ma_dich_vu,id FROM dich_vu WHERE ma_dich_vu IN ('DIEN','NUOC')"))
    for index in range(1, 51):
        room = db.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?,?,?,25,3500000,4,'DANG_THUE','2026-08-01') RETURNING id", (building, f'B{index:03}', (index-1)//10+1)).fetchone()[0]
        tenant = db.execute("INSERT INTO khach_thue(ho_ten,ngay_tao) VALUES(?,'2026-08-01') RETURNING id", (f'Khách mẫu 50 phòng {index}',)).fetchone()[0]
        contract = db.execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,ngay_chot_hang_thang,nguoi_lap_id,ngay_tao) VALUES(?,?,?,'DANG_HIEU_LUC',31,?,'2026-08-01') RETURNING id", (f'HD-S306-BULK-{index:03}', room, tenant, owner)).fetchone()[0]
        db.execute("INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(?,1,'2026-08-01','2028-07-31',24,3500000,?,'2026-08-01')", (contract, owner))
        db.execute('INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id) SELECT ?,id FROM dich_vu_toa_nha WHERE toa_nha_id=?', (room, building))
        for code, before, after in [('DIEN','100','150'),('NUOC','20','25')]:
            db.execute("INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(?,?,'2026-10-01','2026-10-31',?,?,?,'2026-10-08T00:00:00Z')", (contract, meter_services[code], before, after, manager))
    # Give the missing-water room a legitimate previous-period reference, so the manager can complete it through the UI.
    contract = access['monthlyExamples'][-1]['contractId']
    db.execute("INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(?,?,'2026-09-01','2026-09-30','20','20',?,'2026-09-30T00:00:00Z'),(?,?,'2026-09-01','2026-09-30','100','100',?,'2026-09-30T00:00:00Z')", (contract,meter_services['NUOC'],manager,contract,meter_services['DIEN'],manager))
    assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
access['bulkBuildingId'] = building
access['bulkExpectedTotal'] = 203000000
manifest.write_text(json.dumps(access, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Prepared building={building}: 50 ready rooms, 4,060,000 VND each; integrity/FK OK')
