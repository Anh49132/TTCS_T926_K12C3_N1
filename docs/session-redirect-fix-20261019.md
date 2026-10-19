# Sửa chuyển về đăng nhập nhầm — 19/10/2026

Hai đường chuyển hướng sai đã tìm thấy trong mã:

- `session-status.js` chuyển về Login khi bất kỳ HTTP lỗi nào, gồm 500/503 tạm thời. Nay chỉ chuyển khi 401 hoặc JSON xác nhận `authenticated === false`; bỏ qua lỗi mạng/server/JSON và ngăn các lần kiểm tra chồng nhau.
- `auth-interceptor.js` refresh JWT lỗi chuyển về Login mà không kiểm tra cookie MVC. Token có thể bị thu hồi/thay khi dùng nhiều tab hoặc đăng nhập tài khoản khác, trong khi cookie vẫn hợp lệ. Nay chỉ xóa token khi refresh trả 401; lỗi mạng/5xx giữ token để lần sau thử lại. Trước khi chuyển trang phải kiểm tra phiên cookie bằng fetch gốc, không gọi đệ quy interceptor. Cookie hợp lệ không bị chuyển trang bởi JWT lỗi.

Không kéo dài thời hạn phiên, không bỏ kiểm tra quyền hoặc thu hồi phiên ở backend; không đổi DB. Thông báo hết phiên bỏ khẳng định cứng “sau 30 phút” vì phiên có gia hạn trượt và có thể bị thu hồi do đổi mật khẩu/khóa tài khoản.

13 ca JavaScript với môi trường giả lập đạt: lỗi 500/503/403, JSON không hợp lệ về trạng thái, phiên hợp lệ/hết hạn, kiểm tra chồng, refresh 401 với cookie còn/hết hạn, refresh 503 và mất mạng, API 401 khi MVC còn hợp lệ. Script: `verification/session-redirect-tests.mjs`, chạy bằng Node với hàm export `run(repositoryRoot)`.

6 ca backend đăng nhập/refresh/token hết hạn/thu hồi và cookie thiếu/sai/hết hạn đạt. Sửa vòng đời fixture AuthTests để dừng factory/worker, đóng scope và connection pool trước khi xóa DB tạm; lần chạy đầu lỗi khóa file ở bước dọn test, không phải assertion đăng nhập. Kết quả: `data/session-fix-tests/session-regression.trx`.

Chưa đo phiên liên tục 30 phút trên thiết bị người dùng. Nếu còn xảy ra, cần ghi nhận thời điểm và response của `/Account/SessionStatus` để phân biệt cookie bị thu hồi, đổi DB/host hoặc lỗi server khác. Không ghi token/cookie vào log.
