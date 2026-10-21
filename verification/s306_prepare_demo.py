"""Add isolated S3-06 examples to a freshly generated Sprint2 demo, never local-dev.sqlite."""
import json
import sqlite3
import sys
from pathlib import Path

folder = Path(sys.argv[1]).resolve()
root = Path(__file__).resolve().parents[1]
assert folder.is_relative_to(root / "data" / "s306-demo"), "S3-06 demo directory required"
manifest = folder / "access.json"
access = json.loads(manifest.read_text(encoding="utf-8-sig"))
database = Path(access["database"]).resolve()
assert database.is_relative_to(folder), "Only the isolated demo database may be changed"
with sqlite3.connect(database) as db:
    db.execute("PRAGMA foreign_keys=ON")
    assert not db.execute("SELECT 1 FROM toa_nha WHERE ten_toa_nha='Demo hóa đơn đầy đủ'").fetchone(), "Already prepared; use a new demo"
    owner, manager = db.execute("SELECT chu_nha_id,quan_ly_id FROM toa_nha WHERE id=1").fetchone()
    building = db.execute("INSERT INTO toa_nha(chu_nha_id,quan_ly_id,ten_toa_nha,dia_chi,ngay_chot_hang_thang,dang_hoat_dong) VALUES(?,?,'Demo hóa đơn đầy đủ','Địa chỉ mẫu S3-06',31,1) RETURNING id", (owner, manager)).fetchone()[0]
    person_service = db.execute("INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu,dang_hoat_dong) VALUES('VESINH_S306','Vệ sinh theo người',1) RETURNING id").fetchone()[0]
    services = {code: sid for sid, code in db.execute("SELECT id,ma_dich_vu FROM dich_vu")}
    catalogs = {}
    for code, price, kind, unit in [("DIEN", 4000, "THEO_CHI_SO", "kWh"), ("NUOC", 16000, "THEO_CHI_SO", "m³"), ("INTERNET", 150000, "CO_DINH", "phòng/tháng"), ("RAC", 50000, "CO_DINH", "phòng/tháng"), ("VESINH_S306", 80000, "THEO_NGUOI", "người/tháng")]:
        sid = services[code]
        catalogs[code] = db.execute("INSERT INTO dich_vu_toa_nha(toa_nha_id,dich_vu_id,ap_dung_mac_dinh) VALUES(?,?,0) RETURNING id", (building, sid)).fetchone()[0]
        db.execute("INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,dang_ap_dung,da_chot_gia,nguoi_tao_id,ngay_tao) VALUES(?,?,?,?,?,'2026-08-01',1,1,?,'2026-10-08T00:00:00Z')", (building,sid,kind,unit,price,owner))
    room_examples = []
    for index in range(1, 6):
        code = f"A10{index}"
        tenant = db.execute("INSERT INTO khach_thue(ho_ten,ngay_tao) VALUES(?,'2026-08-01') RETURNING id", (f"Khách thuê demo {index}",)).fetchone()[0]
        room = db.execute("INSERT INTO phong_tro(toa_nha_id,ma_phong,tang,dien_tich,gia_thue,so_nguoi_toi_da,trang_thai,ngay_tao) VALUES(?,?,1,25,3500000,4,'DANG_THUE','2026-08-01') RETURNING id", (building,code)).fetchone()[0]
        contract = db.execute("INSERT INTO hop_dong(ma_hop_dong,phong_id,khach_dung_ten_id,trang_thai,ngay_chot_hang_thang,nguoi_lap_id,ngay_tao) VALUES(?,?,?,'DANG_HIEU_LUC',31,?,'2026-08-01') RETURNING id", (f"HD-S306-{code}",room,tenant,owner)).fetchone()[0]
        db.execute("INSERT INTO ky_hop_dong(hop_dong_id,so_thu_tu,ngay_bat_dau,ngay_ket_thuc,so_thang,gia_thue,nguoi_lap_id,ngay_tao) VALUES(?,1,'2026-08-01','2028-07-31',24,3500000,?,'2026-08-01')",(contract,owner))
        selected = ["DIEN", "NUOC"] + (["INTERNET", "RAC"] if index >= 2 else []) + (["VESINH_S306"] if index >= 3 else [])
        for service in selected:
            db.execute("INSERT INTO dich_vu_phong(phong_id,dich_vu_toa_nha_id) VALUES(?,?)", (room,catalogs[service]))
        for service, before, after in [("DIEN","100","150"),("NUOC","20","25")]:
            if index == 5 and service == "NUOC":
                continue
            db.execute("INSERT INTO chi_so_dien_nuoc(hop_dong_id,dich_vu_id,tu_ngay,den_ngay,chi_so_dau,chi_so_cuoi,nguoi_nhap_id,ngay_nhap) VALUES(?,?,'2026-10-01','2026-10-31',?,?,?,'2026-10-08T00:00:00Z')",(contract,services[service],before,after,manager or owner))
        if index == 4:
            for number in range(2):
                roommate = db.execute("INSERT INTO khach_thue(ho_ten,ngay_tao) VALUES(?,'2026-09-01') RETURNING id",(f"Người ở ghép demo {number+1}",)).fetchone()[0]
                db.execute("INSERT INTO nguoi_o_ghep(hop_dong_id,khach_thue_id,ngay_vao) VALUES(?,?,'2026-09-01')",(contract,roommate))
        room_examples.append({"room":code,"contractId":contract,"expectedTotal": {1:3780000,2:3980000,3:4060000,4:4220000,5:None}[index]})
    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    assert not db.execute("PRAGMA foreign_key_check").fetchall()
access["monthlyBuildingId"] = building
access["monthlyExamples"] = room_examples
access["runtime"] = str(root / "QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll")
manifest.write_text(json.dumps(access, ensure_ascii=False, indent=2), encoding="utf-8")
(folder / "s306-report.md").write_text("# Demo hóa đơn đầy đủ — kỳ 10/2026\n\n" + "\n".join(f"- {x['room']}: {x['expectedTotal'] if x['expectedTotal'] else 'thiếu nước, bỏ qua'}" for x in room_examples), encoding="utf-8")
print(f"Prepared S3-06 demo: building={building}; 4 ready rooms, 1 missing water; integrity/FK OK")
