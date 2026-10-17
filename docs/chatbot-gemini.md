# Popup Trợ lý AI NhàTốt

Triển khai trên nhánh `feature/plus/chatbot`, không thêm bảng hoặc sửa dữ liệu nghiệp vụ.

## Sử dụng

Thêm cấu hình vào `.env.local` ở gốc dự án (đã bị Git bỏ qua), giữ nguyên các dòng khác:

```dotenv
Gemini__ApiKey=THAY_BANG_KHOA_RIENG
Gemini__Model=gemini-3.1-flash-lite
```

Chạy `run.bat`, đăng nhập rồi bấm **Trợ lý AI** ở góc phải. Model có thể đổi bằng cấu hình. Nếu chạy `dotnet run` trực tiếp thay vì `run.bat`, cần đặt biến môi trường `Gemini__ApiKey`/`Gemini__Model` tương ứng. Không đặt khóa vào appsettings được Git theo dõi. Không truyền khóa trong URL.

Popup nhận câu hỏi tự do, hiển thị trạng thái chờ và giữ ngữ cảnh 6 lượt gần nhất. Có nút thu nhỏ, bắt đầu lại, câu hỏi gợi ý theo vai trò và nút mở trang nghiệp vụ do backend cung cấp. Enter gửi, Shift+Enter xuống dòng, Escape thu nhỏ. Không lưu hội thoại trên trình duyệt hoặc SQLite; bộ nhớ tạm server hết hạn sau 30 phút. Bắt đầu lại tạo hội thoại mới; bản cũ tự hết hạn, không phải lịch sử chat lâu dài.

## Cách AI tra cứu

Backend gọi REST `generateContent` của Gemini, có function calling; Gemini tự quyết định công cụ và tham số cần dùng. Không khớp từ khóa để chọn câu trả lời dựng sẵn. Mỗi lượt tối đa 3 vòng gọi model; mỗi vòng tối đa 5 lời gọi công cụ. Tham khảo [Generate content](https://ai.google.dev/api/generate-content), [Function calling](https://ai.google.dev/gemini-api/docs/function-calling).

| Công cụ | Dữ liệu | Phạm vi |
| --- | --- | --- |
| invoices | Tổng phát hành, đã thu xác nhận, chờ xác nhận, cấn cọc, dư nợ, hạn và tối đa 20 dòng ưu tiên còn nợ | Khách đứng tên hợp đồng của mình; chủ nhà các tòa sở hữu; admin. Quản lý bị chặn |
| repairs | Thống kê trạng thái, tối đa 20 mã/thiết bị/trạng thái/mức độ mới nhất | Khách các báo hỏng do mình gửi; chủ nhà/quản lý đúng tòa; admin |
| rooms | Tổng theo trạng thái/tòa hiện tại, tối đa 20 phòng giá tăng dần | Chủ nhà/quản lý đúng tòa; admin |
| contracts | Tối đa 20 kỳ hợp đồng: mã, ngày, trạng thái, giá và cọc thỏa thuận | Khách đứng tên/ở ghép đúng hợp đồng; chủ nhà/quản lý đúng tòa; admin |

Tra cứu hóa đơn không có tháng/năm nghĩa là mọi kỳ; nếu có tháng nhưng thiếu năm thì dùng năm Việt Nam hiện tại. AI được cung cấp tổng toàn phạm vi, không cộng lại danh sách giới hạn để thay tổng. Dữ liệu phòng là trạng thái hiện tại, không phải báo cáo lấp đầy cuối tháng. Chưa có công cụ chốt điện nước, báo cáo doanh thu lịch sử, thao tác ghi hoặc tìm tin công khai cho khách chưa đăng nhập.

## Quyền và giới hạn

- Chỉ người đã đăng nhập dùng được, kiểm tra tài khoản đang hoạt động, vai trò và quyền module hiện tại tại backend. ID tài khoản/vai trò không lấy từ lời AI hay nội dung form.
- SQL và truy vấn đều cố định, chỉ đọc. Không có công cụ xác nhận thanh toán, sửa phòng, gửi báo hỏng hoặc chốt tất toán. Mọi công cụ lại kiểm tra quyền khi chạy, kể cả model yêu cầu công cụ không được khai báo.
- Khóa API chỉ ở server và header `x-goog-api-key` tới endpoint Google cố định; không trả khóa/nội dung lỗi thô nhà cung cấp về trình duyệt. POST có CSRF, no-store, tối đa 2.000 ký tự/câu, 10 yêu cầu/phút/tài khoản; mỗi hội thoại chỉ một lượt đang chạy.
- Dữ liệu gửi AI chỉ có các trường allowlist; không gửi email/SĐT/giấy tờ, mật khẩu, ảnh, chứng từ, mô tả sự cố hoặc ghi chú tự do từ DB. Câu hỏi do người dùng nhập và dữ liệu công cụ vẫn được xử lý bởi Google; popup ghi rõ điều này. Không nhập thông tin nhạy cảm vào chat.
- Hội thoại gắn với tài khoản và phạm vi quyền; tài khoản khác không dùng lại được ID. Thay đổi module hoặc phạm vi tòa yêu cầu bắt đầu lại. Văn bản AI được hiển thị bằng `textContent`, không render HTML; liên kết do backend tạo và chỉ cùng origin.
- AI vẫn có thể diễn đạt sai hoặc bị ảnh hưởng bởi nội dung người dùng. Số liệu cần kiểm tra lại trên trang nghiệp vụ. Lỗi hết hạn mức, cấu hình, mạng hoặc timeout có thông báo; câu hỏi được giữ lại để thử lại, không tự gửi lại âm thầm. Tốc độ/hạn mức phụ thuộc Google; không cam kết API luôn miễn phí.

## Kiểm chứng 17/10/2026

- Build đạt, 10/10 kiểm thử mới đạt: phạm vi người/tòa, thu hồi quyền, không cho quản lý xem tài chính, công cụ ngoài allowlist, hội thoại tài khoản khác, ngữ cảnh, CSRF/401, giới hạn tốc độ, tiền chờ không giảm nợ, khóa trong header và che lỗi nhà cung cấp. TRX: `data/chatbot-tests/chatbot-final.trx`.
- Gọi Gemini thật: khóa được chấp nhận, model `gemini-3.1-flash-lite` trả lời được; Edge trên bản sao DB đạt khách tra báo hỏng + câu hỏi nối tiếp, quản lý từ chối xem tài chính, chủ nhà tổng hợp 500 phòng theo tòa. Đã rà ảnh desktop1440/360px; đặt lại và Escape đạt. Ảnh: `data/chatbot-qa/browser-check-562c7346`; bộ chạy `verification/AiChatBrowser`.
- Có một lượt thử ban đầu bị timeout từ Google; đã bổ sung mức suy luận minimal cho dòng Gemini3. Giao diện vẫn giữ xử lý timeout, không coi lượt thử lại thành bảo đảm ổn định mạng.
- DB đang dùng/tài khoản không đổi; server QA đã dừng. Chưa kiểm tra Safari/thiết bị thật, tải đồng thời, toàn bộ suite hoặc chat khi chưa đăng nhập. Chưa commit/push.
