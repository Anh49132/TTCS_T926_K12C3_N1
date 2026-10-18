# Giao diện và quyền Admin — 18/10/2026

## Phạm vi đã hoàn thành

- Tổng quan quản trị mới với đường dẫn đến quản lý tài khoản, tạo tài khoản nội bộ, ma trận quyền, nhật ký đầy đủ, tra cứu email thông báo lỗi và đổi mật khẩu.
- Danh sách tài khoản giữ bộ lọc vai trò/trạng thái, 20 dòng/trang. Khóa/mở khóa được tài khoản các vai trò, không tự khóa; thu hồi phiên đăng nhập khi khóa. Tạo nội bộ vẫn chỉ Chủ nhà/Quản lý/Admin, gửi mật khẩu tạm qua cơ chế email sẵn có, buộc đổi lần đầu. Không đặt lại mật khẩu tài khoản hiện có.
- Admin đọc các trang nghiệp vụ, không thêm/sửa/xóa. Đã nối trang danh sách hóa đơn, chỉ số và báo hỏng vào dữ liệu thật, bổ sung xem tin nháp/tạm ẩn; tra cứu chỉ số gồm cả hợp đồng kết thúc, lọc tòa/kỳ, 50 bản ghi/trang.
- Nhật ký giữ toàn bộ dữ liệu và giá trị trước/sau, bộ lọc và 50 dòng/trang, không giới hạn theo chủ nhà/tòa, không có sửa/xóa. Các biện pháp bảo vệ bí mật và bất biến giữ nguyên.
- Tra cứu gửi thông báo lỗi nối tới nhật ký email hóa đơn hiện có, có lọc trạng thái/ngày và kiểm tra bộ lọc sai. Đây không phải nhật ký tập trung cho mọi loại email (mật khẩu tạm/reset/lịch hẹn vẫn dùng cơ chế email hiện có).

## Cấu hình quyền và dữ liệu cũ

Đã bỏ đặc cách `ADMIN => true` trong PermissionService. Menu và action đọc quyền DB; thiếu cặp hoặc NONE đều từ chối. Admin có trần chỉ xem với module nghiệp vụ, kể cả DB cũ còn WRITE/FULL. Module TAI_KHOAN vẫn theo quyền DB để quản lý tài khoản. Ma trận hiển thị quyền hiệu lực và giải thích giới hạn này.

Seed mới: Admin FULL ở TAI_KHOAN, READ ở tám module nghiệp vụ. Không chạy seed/updater hoặc ghi vào DB đang dùng; các hàng FULL lịch sử vẫn giữ nguyên trong DB nhưng không cho phép ghi nghiệp vụ. Không cần nâng schema. Chủ nhà, quản lý và khách giữ quyền hiện có.

## Kiểm chứng

- Build đạt; còn cảnh báo ImageSharp và CS8601 có sẵn.
- Chạy rộng nhóm quyền/nhật ký: 227 đạt, 11 lỗi. Một lỗi thuộc fixture mới thiếu bảng hoa_don, đã bổ sung initializer chỉ trên DB test và chạy lại đạt. Mười lỗi còn lại là kiểm thử trang công khai kỳ vọng `/TinDang` trả 200 thay vì redirect, hoặc nhãn trang chủ/giới thiệu cũ; không sửa lan sang các trang công khai.
- Năm kiểm thử trọng tâm đạt: trần chỉ xem với DB FULL cũ, GET/POST trái quyền trả 403, thiếu/thu hồi quyền ẩn menu và chặn action, khóa khách thu hồi phiên, không tự khóa, seed và ma trận. TRX: `data/admin-access-tests/admin-targeted-final.trx`.
- Edge headless trên bản sao DB đạt các trang tài khoản, quyền, nhật ký, lỗi email, tin đăng, chỉ số, hóa đơn, công nợ, báo hỏng và báo cáo; mở được chi tiết tin đăng/hóa đơn/báo hỏng. Đã xem ảnh tổng quan 1440px và 360px, không tràn ngang. Ảnh: `data/admin-access-qa/browser-check-b26704eb`. Bộ chạy: `verification/AdminAccessBrowser`.
- Chưa thử SMTP thật, Safari hoặc thiết bị thật. Server QA đã dừng. Không commit/push, không sửa DB/tài khoản đang dùng.
