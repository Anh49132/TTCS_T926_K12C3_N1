# Email báo và xác nhận thanh toán — 18/10/2026

- Khách báo thanh toán hoặc hủy báo cáo: gửi chủ nhà hiện tại của tòa.
- Chủ nhà xác nhận hoặc từ chối: gửi khách đã báo, kiểm tra lại liên kết hợp đồng trước khi gửi.
- Thông báo trong ứng dụng giữ nguyên. Email chứa nội dung nghiệp vụ và liên kết hóa đơn theo vai trò; không đính kèm chứng từ, URL ký hoặc ghi chú tự do.
- Dùng `email_gui_log` hiện có, khóa `THANH_TOAN:{notificationId}:{kind}`. Không đổi schema (v41), không gửi bù lịch sử. Tạo hàng đợi cùng transaction nghiệp vụ; lỗi hàng đợi rollback thay đổi.
- Worker mỗi 30 giây, tối đa 10 thư/lượt; một lần đầu và tối đa ba lần thử lại sau 5/10/15 phút. Lease hai phút, số lần thử làm fencing để worker cũ không ghi đè kết quả mới. SMTP thành công rồi tiến trình dừng trước khi lưu kết quả vẫn có thể gửi lại; không cam kết exactly-once qua SMTP.
- Kiểm tra lại tài khoản hoạt động, email đã xác thực, vai trò, quyền tài chính và sở hữu trước khi gửi. Báo mới đã được xử lý/hủy thì bỏ email báo mới để tránh nhắc việc lỗi thời.
- Admin tra cứu tại `/EmailGuiLog?Nhom=THANH_TOAN`, lọc trạng thái/ngày, phân trang. Lỗi SMTP được thay bằng thông báo chung, không ghi bí mật máy chủ.
- Không đổi cách tính tiền: khoản chờ xác nhận không giảm dư nợ.

Kiểm thử: 23 ca đạt với bộ lọc `PaymentEmail|PaymentWorkflow|PaymentProof|RepairEmail`. Có kiểm tra người nhận, chống trùng, hủy/từ chối, thu hồi quyền, retry qua context mới, rollback khi hàng đợi lỗi và số dư. Kết quả: `data/payment-email-tests/payment-email-final.trx` (không đưa vào Git).

Chưa thử SMTP/inbox thật. Cấu hình pickup hiện có vẫn chỉ lưu thư local; muốn gửi thật cần cấu hình SMTP và tắt pickup. Không chạy updater hoặc server trên DB đang dùng cho thay đổi này.
