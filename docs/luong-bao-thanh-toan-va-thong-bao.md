# Báo thanh toán và đối chiếu thủ công

## Luồng sử dụng

1. Khách mở `/ThongBao/DanhSach`, chọn hóa đơn để đến thẳng `/ThongBao/HoaDon/{id}`. Trang có tổng tiền, khoản thu đã xác nhận, dư nợ, lịch sử và form báo trả. Chuyển khoản hiển thị ngân hàng/QR của chủ nhà; khách nhập số tiền, ngày trả, ảnh chứng từ và ghi chú tối đa 300 ký tự.
2. Sau gửi, trang hiện riêng khoản chờ xác nhận và chứng từ. Khoản chờ không giảm dư nợ. Khách chỉ hủy báo cáo của mình khi còn chờ; báo cáo đã gửi không sửa được.
3. Chủ nhà nhận thông báo trên chuông và mở mục **Chờ xác nhận** trong `/HoaDonDichVu/Management`. Nút **Kiểm tra** đến báo cáo trong chi tiết hóa đơn.
4. Chủ nhà xem chứng từ, tự kiểm tra tiền thực nhận, tích ba mục đối chiếu rồi xác nhận. Backend kiểm tra lại sở hữu, trạng thái và dư nợ, chống xác nhận trùng. Xác nhận mới ghi khoản thu và giảm nợ.
5. Từ chối bắt buộc nhập lý do tối đa 500 ký tự. Không ghi khoản thu; khách nhận thông báo, thấy lý do và có thể gửi báo cáo mới. Hủy/từ chối vẫn giữ lịch sử và chứng từ.

Chuông phân biệt số thông báo chưa đọc với số báo cáo đang chờ xử lý. Thông báo mới và kết quả xử lý được lưu cùng transaction với nghiệp vụ; lỗi lưu thông báo làm rollback. Các thông báo thanh toán mới là in-app, chưa gửi email. Hệ thống không kết nối ngân hàng để tự xác thực giao dịch.

## Quyền và chứng từ

- Chủ nhà chỉ xử lý hóa đơn tại tòa mình; khách chỉ gửi/hủy/xem chứng từ thuộc hóa đơn của mình. Quản lý không truy cập tài chính. Mọi POST kiểm tra CSRF.
- Chứng từ ngoài wwwroot, endpoint kiểm tra quyền, phản hồi no-store. Token ảnh gắn với người xem và hết hạn sau 15 phút. Khách dùng `/api/chung-tu-thanh-toan/{id}/link`; chủ nhà dùng endpoint chứng từ hiện có.
- Ghi chú/lý do không đưa vào nhật ký hoạt động. Báo cáo cũ không có thời điểm gửi thì hiển thị chưa có dữ liệu, không suy đoán từ ngày trả.

## Schema v39 và vận hành

Updater thêm `bao_cao_thanh_toan` lưu metadata và `thong_bao_thanh_toan` lưu thông báo, giữ nguyên các bảng nghiệp vụ cũ. Không sửa bước nâng cấp v38. Dừng web và sao lưu trước khi chạy; dùng cùng DatabasePath với khởi động:

```powershell
$env:DatabasePath = '<đường dẫn DB đang dùng>'
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

DB rỗng dùng initializer chuẩn của dự án rồi updater, không dùng EnsureCreated/EnsureDeleted. Updater tự tạo backup trước nâng cấp. Kiểm tra phục hồi bằng bản sao backup riêng, không ghi đè DB đang chạy.

## Kiểm chứng ngày 16/10/2026

- Nhóm tích hợp liên quan đạt 31/31: quyền, CSRF, ảnh 15 phút, dữ liệu bất biến, hủy/từ chối/gửi lại, xác nhận chống trùng, rollback khi thông báo thất bại và nâng schema.
- Edge trên DB sao chép chạy hết gửi ảnh → chuông chủ nhà → từ chối → gửi lại → xác nhận; kiểm tra dư nợ và ảnh riêng tư. Desktop và 360px không tràn ngang. Bộ chạy: `verification/PaymentWorkflowBrowser`, cần thư mục QA riêng với access.json/case.json/proof-test.png; không chạy trên DB thật.
- Nâng bản sao v38→39 giữ 54 bảng/25.067 bản ghi cũ, integrity=ok, FK=0; chạy lại không đổi byte DB.
- Theo xác nhận người dùng, DB local đã sao lưu ngoài repo và nâng lên39; hash các bảng nghiệp vụ cũ giữ nguyên 25.067 dòng; `run.bat --check-only` đạt. Server được để tắt.
- Chưa kiểm tra Safari, thiết bị thật hoặc SMTP thật; chưa chạy lại toàn bộ suite.
