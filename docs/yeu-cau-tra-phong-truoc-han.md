# Yêu cầu trả phòng trước hạn

## Sử dụng

- Khách vào **Hợp đồng của tôi → Yêu cầu trả phòng trước hạn**, hoặc nút trong chi tiết hợp đồng. Chỉ người đứng tên có tài khoản đang hoạt động được gửi; người ở ghép không gửi thay. Chọn hợp đồng đang hiệu lực, ngày đề nghị từ hôm nay và trước ngày kết thúc kỳ cuối (kể cả gia hạn), lý do tối đa 500 ký tự.
- Chủ nhà vào **Hợp đồng & người ở ghép → Yêu cầu trả phòng của khách**, hoặc thông báo trên chuông. Chủ nhà chỉ xem/xử lý tòa mình sở hữu; cần quyền ghi HOP_DONG để tiếp nhận/từ chối. Khách/ADMIN/QUAN_LY không được tiếp nhận.
- Khi tiếp nhận, chủ nhà chọn ngày bàn giao đã thống nhất, từ hôm nay đến ngày kết thúc hợp đồng; có thể ghi phản hồi. Từ chối hoặc đóng lịch đã tiếp nhận bắt buộc có lý do. Khách nhận thông báo và xem phản hồi trên cùng màn.
- Khách chỉ hủy khi còn MOI; nội dung đã gửi không sửa. Mỗi hợp đồng có tối đa một yêu cầu MOI/DA_TIEP_NHAN. Sau hủy/từ chối có thể gửi yêu cầu mới, lịch sử vẫn giữ nguyên.
- Đến ngày bàn giao, nút **Bắt đầu xử lý trả phòng** mở luồng TraPhong hiện có, điền gợi ý ngày trả. Chủ nhà vẫn phải nhập ngày thực tế, chỉ số điện nước, lý do chấm dứt và quyết định phạt cọc. Quy tắc hóa đơn kỳ cuối, tiền cọc, khấu trừ, công nợ và PDF giữ nguyên.
- Khi chốt tất toán thành công, yêu cầu đang mở chuyển HOAN_TAT và gửi thông báo cho khách trong cùng transaction. Đây là chốt trả phòng/nghĩa vụ; việc thu/hoàn tiền thực tế vẫn theo nghiệp vụ hiện có.

Gửi, tiếp nhận, hủy hoặc từ chối yêu cầu **không** thay đổi hợp đồng/phòng, không tạo hóa đơn hay cấn/hoàn cọc. Ngày bàn giao tương lai chỉ là lịch hẹn, không được dùng làm ngày trả thực tế sớm. Nếu hợp đồng đã có hóa đơn kỳ cuối, không tạo yêu cầu mới; liên hệ chủ nhà để điều chỉnh bàn giao hiện có.

## Kỹ thuật và quyền

- `/YeuCauTraPhong` GET: danh sách theo quyền, lọc hợp đồng, 20 yêu cầu/trang, yêu cầu mới ưu tiên. POST Gui/XuLy có CSRF, kiểm tra vai trò/sở hữu, ngày, độ dài, trạng thái và phien_ban ở backend.
- Giao dịch SQLite immediate và unique index có điều kiện chống gửi trùng. Hủy/từ chối/tiếp nhận/hoàn tất dùng khóa lạc quan. Khi dữ liệu đổi, người dùng phải tải lại; không ghi đè quyết định của người trước.
- Bảng yêu cầu giữ lý do/phản hồi để hai bên tra cứu; trigger chặn xóa/sửa nội dung gốc và chuyển trạng thái sai. Nhật ký chỉ lưu ID/ngày/trạng thái/phiên bản và snapshot người thao tác, không ghi lý do/phản hồi tự do. Lỗi nhật ký hoặc thông báo rollback nghiệp vụ.
- Thông báo mới là in-app, chưa gửi email. Chuông hiện có thêm nguồn CHECKOUT, read/read-all chỉ cho người nhận và còn quyền HOP_DONG; API chuông vẫn cần TAI_CHINH theo thiết kế hiện hành. Nếu quyền tài chính bị thu hồi, vẫn vào màn yêu cầu bằng quyền hợp đồng, không mở rộng quyền tài chính vì tính năng này.

## Nâng cấp v40

Thêm `yeu_cau_tra_phong` và `thong_bao_tra_phong`; không sửa các bước v1–39 hay dữ liệu tài chính cũ. Dừng web, sao lưu, đặt cùng DatabasePath với startup rồi chạy:

```powershell
$env:DatabasePath = '<DB cần nâng>'
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

Updater tạo backup riêng, kiểm tra integrity/FK. Luôn thử trên bản sao trước; DB rỗng dùng initializer chuẩn của dự án. Khôi phục thử vào đường dẫn riêng bằng backup, không chép đè DB đang dùng khi chưa có quyết định phục hồi.

## Kiểm chứng ngày 16/10/2026

- Nhóm mới và liên quan ban đầu đạt 12/12; nhóm hồi quy cuối đạt 98/98 (`data/checkout-request-tests/checkout-regression.trx`): gửi/nhận/từ chối/hủy/gửi lại, cạnh tranh gửi trùng, CSRF/quyền/sở hữu/thông báo đúng người, rollback khi lỗi thông báo/nhật ký, dữ liệu gốc bất biến; nối chốt tất toán kỳ cuối thành HOAN_TAT và giữ các quy tắc tài chính cũ.
- Nâng bản sao v39→40 giữ nguyên 57 bảng/25.123 dòng cũ; integrity=ok, FK=0, chạy lại byte-identical. Kiểm tra hash sau thử trình duyệt: bảng hợp đồng/phòng/hóa đơn/chi tiết/thanh toán/cọc không đổi.
- `verification/CheckoutRequestBrowser` chạy Edge với thư mục QA riêng chứa access.json và case.json (contractId). Đã thử gửi → chuông chủ nhà → tiếp nhận lịch tương lai → từ chối/đề nghị lại → tiếp nhận ngày hiện tại → mở form trả phòng, desktop và 360px không tràn ngang. Ảnh/log trong `data/checkout-request-*` ignored; không dùng dữ liệu thử để ghi nghiệp vụ vào DB thật.
- Theo xác nhận riêng của người dùng, DB đang dùng đã sao lưu ngoài repo tại `%TEMP%/NhaTot-database-backups/checkout-v40-20261016-222912`, nâng v40 và đối chiếu hash: giữ nguyên 57 bảng/25.123 dòng cũ (schema chỉ thêm dòng40). Hai bảng mới rỗng; integrity=ok/FK=0; chạy updater lại byte-identical; `run.bat --check-only` đạt. Không đổi tài khoản hoặc ghi yêu cầu thử vào DB thật. Server được để tắt.
- Chưa kiểm tra Safari/thiết bị thật, chưa chạy toàn bộ suite và không gửi email thật.
