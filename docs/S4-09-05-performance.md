# S4-09-05 — Tối ưu báo cáo doanh thu và lấp đầy

## Thay đổi

- Tổng thanh toán đã xác nhận và khoản cấn trừ được nhóm theo hóa đơn trong SQLite, giảm số dòng chuyển và cộng ở bộ nhớ.
- Truy vấn lịch sử phòng chỉ lấy các khoảng có thể giao với khoảng báo cáo.
- Bảng lấp đầy theo tòa dùng lại phòng, lịch sử và hợp đồng đã truy vấn cho bảng tháng.
- Biểu đồ cộng trực tiếp tổng hóa đơn đã phát hành theo tháng; không tính lại trạng thái phòng cho sáu tháng biểu đồ.
- Updater schema v35 thêm chỉ mục phục vụ truy vấn hóa đơn, hợp đồng, kỳ hợp đồng, thanh toán và lịch sử phòng. Bước này chỉ thêm chỉ mục, giữ nguyên dữ liệu và bước updater cũ.

## Kiểm tra

- `BaoCaoDoanhThuTests` kiểm tra báo cáo 500 phòng/12 tháng, các tổng tài chính, lấp đầy, sáu cột biểu đồ, chỉ mục v35 và request HTTP đã warm-up gồm đăng nhập, controller, tổng hợp và Razor render.
- Cần chạy test trên máy nghiệm thu với ứng dụng đã dừng; thời gian request được đo trên máy test hiện tại, chưa phải kết quả của môi trường nghiệm thu.
- Chạy nâng cấp trên bản sao CSDL v34, sau đó kiểm tra `PRAGMA integrity_check`, `PRAGMA foreign_key_check`, số dòng nghiệp vụ trước/sau và `EXPLAIN QUERY PLAN` cho truy vấn báo cáo.

