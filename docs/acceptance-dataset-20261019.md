# Bộ nghiệm thu mở rộng 19/10/2026

Theo yêu cầu thay DB, đã tạo DB mới schema v41 và chuyển cấu hình local sau khi kiểm chứng, giữ nguyên DB cũ và tạo thêm backup/config phục hồi. Không sửa schema hoặc dữ liệu DB cũ.

10 tòa, 480 phòng, 280 hợp đồng, 840 hóa đơn tháng 7–9/2026; 5 chủ nhà, 4 quản lý, 300 khách, 1 Admin. Có dịch vụ/giá, ảnh phòng, giấy tờ mẫu trống, chỉ số, thu cọc, người ở ghép, lịch sử phòng, thanh toán đủ/thiếu/chờ, thông báo, báo hỏng, yêu cầu xem phòng và tin đăng. Tài khoản được yêu cầu dùng đúng tên/email/mật khẩu trong file riêng bị ignore; không ghi credential vào tài liệu theo dõi Git.

Tệp cục bộ: `data/acceptance-20261019/HUONG-DAN.md`, `tai-khoan.csv`, `access.json`, `hoa-don-doi-chieu.csv`, `verification.json`, `switch-report.json`. Không commit dữ liệu/ảnh/mật khẩu/config local. Email pickup, không gửi SMTP thật. Server để tắt khi bàn giao.

Đã xác minh integrity/FK, số liệu/số dư, tổng dòng hóa đơn, không thu vượt, chỉ số nối tiếp, ảnh tồn tại và mật khẩu 310 tài khoản. Smoke HTTP 4 vai trò trên 20 lượt mở trang đạt; trang công nợ/hóa đơn/báo cáo mở được. Chưa nghiệm thu UI trên thiết bị thật hoặc SMTP.

Script tạo dữ liệu nhận đường dẫn DB trống đã khởi tạo và file yêu cầu riêng, từ chối DB có tài khoản: `verification/Seed-Acceptance20261019.py`. Script kiểm chứng: `verification/Verify-Acceptance20261019.py`. Cần Python bcrypt/Pillow sẵn có, .NET initializer cài nền cùng module hóa đơn và yêu cầu thuê. Script không tự đổi cấu hình chạy; thao tác chuyển DB đã làm riêng sau kiểm chứng.
