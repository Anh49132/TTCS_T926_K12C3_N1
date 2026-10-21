-- Dữ liệu mẫu S2-07. Chỉ chạy trên database demo/bản sao đã có khách, tòa và phòng.
-- Không chạy trên database local đang sử dụng nếu chưa sao lưu.
INSERT INTO yeu_cau (ma_yeu_cau, khach_thue_id, phong_id, toa_nha_id, loai_yeu_cau, ngay_mong_muon, trang_thai, ngay_tao)
SELECT 'YC-DEMO-001', k.id, p.id, t.id, 'Sửa điện nước', date('now', '+1 day'), 'MOI', datetime('now', '-2 day')
FROM khach_thue k JOIN phong_tro p ON p.id = (SELECT id FROM phong_tro ORDER BY id LIMIT 1)
JOIN toa_nha t ON t.id = p.toa_nha_id
WHERE NOT EXISTS (SELECT 1 FROM yeu_cau WHERE ma_yeu_cau = 'YC-DEMO-001')
  AND k.id = (SELECT id FROM khach_thue ORDER BY id LIMIT 1);
INSERT INTO yeu_cau (ma_yeu_cau, khach_thue_id, phong_id, toa_nha_id, loai_yeu_cau, ngay_mong_muon, trang_thai, ngay_tao)
SELECT 'YC-DEMO-002', k.id, p.id, t.id, 'Báo hỏng thiết bị', date('now'), 'DA_HEN_LICH', datetime('now', '-1 day')
FROM khach_thue k JOIN phong_tro p ON p.id = (SELECT id FROM phong_tro ORDER BY id LIMIT 1 OFFSET 1)
JOIN toa_nha t ON t.id = p.toa_nha_id
WHERE NOT EXISTS (SELECT 1 FROM yeu_cau WHERE ma_yeu_cau = 'YC-DEMO-002')
  AND k.id = (SELECT id FROM khach_thue ORDER BY id LIMIT 1 OFFSET 1);
INSERT INTO yeu_cau (ma_yeu_cau, khach_thue_id, phong_id, toa_nha_id, loai_yeu_cau, ngay_mong_muon, trang_thai, ngay_tao)
SELECT 'YC-DEMO-003', k.id, p.id, t.id, 'Đăng ký dịch vụ', date('now', '+3 day'), 'HOAN_THANH', datetime('now')
FROM khach_thue k JOIN phong_tro p ON p.id = (SELECT id FROM phong_tro ORDER BY id LIMIT 1 OFFSET 2)
JOIN toa_nha t ON t.id = p.toa_nha_id
WHERE NOT EXISTS (SELECT 1 FROM yeu_cau WHERE ma_yeu_cau = 'YC-DEMO-003')
  AND k.id = (SELECT id FROM khach_thue ORDER BY id LIMIT 1 OFFSET 2);
