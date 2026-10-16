"""Read-only check: python verification/Verify-S403Data.py <qa.sqlite> [YYYY-MM-DD]."""
import json
import sqlite3
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

today = sys.argv[2] if len(sys.argv) > 2 else datetime.now(timezone(timedelta(hours=7))).date().isoformat()
with sqlite3.connect(Path(sys.argv[1]).resolve().as_uri() + '?mode=ro', uri=True) as db:
    rows = db.execute('''WITH paid AS (
        SELECT hoa_don_id,SUM(so_tien) amount FROM thanh_toan WHERE trang_thai='DA_XAC_NHAN' GROUP BY hoa_don_id
    ), offsets AS (
        SELECT hoa_don_id,SUM(so_tien) amount FROM giao_dich_coc WHERE loai_giao_dich='CAN_TRU' AND trang_thai='DA_XAC_NHAN' GROUP BY hoa_don_id
    ) SELECT p.id,p.ma_phong,h.nam,h.thang,h.han_thanh_toan,COALESCE(t.amount,0),
        h.tong_tien-COALESCE(t.amount,0)-COALESCE(o.amount,0)
        FROM hoa_don h JOIN hop_dong c ON c.id=h.hop_dong_id JOIN phong_tro p ON p.id=c.phong_id
        LEFT JOIN paid t ON t.hoa_don_id=h.id LEFT JOIN offsets o ON o.hoa_don_id=h.id
        WHERE h.trang_thai='DA_PHAT_HANH' AND h.tong_tien>COALESCE(t.amount,0)+COALESCE(o.amount,0)''').fetchall()
    partial = sorted({r[1] for r in rows if r[5] > 0})
    multi = sorted({r[1] for r in rows if len({(b[2], b[3]) for b in rows if b[0] == r[0] and b[4] < today}) >= 2})
    assert len({r[0] for r in rows if r[5] > 0}) >= 5, 'Need at least five rooms with confirmed partial receipts'
    assert len({r[0] for r in rows if len({(b[2], b[3]) for b in rows if b[0] == r[0] and b[4] < today}) >= 2}) >= 2, 'Need two rooms overdue in distinct billing periods'
    assert db.execute('PRAGMA integrity_check').fetchone() == ('ok',)
    assert not db.execute('PRAGMA foreign_key_check').fetchall()
print(json.dumps(dict(today=today, partialRooms=partial, multiPeriodOverdueRooms=multi, outstanding=sum(r[6] for r in rows)), ensure_ascii=True))
