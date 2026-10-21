# Giao diện báo hỏng – 16/10/2026

- Khách: `/BaoHong` có tổng quan, tab trạng thái, tìm mã/thiết bị/phòng/người báo, lọc trạng thái/mức độ và phân trang trên toàn bộ dữ liệu của mình. Hiển thị thời điểm cập nhật và số phản hồi thực tế, không suy đoán phản hồi chưa đọc.
- `/BaoHong/Create`: chọn hợp đồng đang thuê, thiết bị, mức độ, mô tả; kéo thả/chọn/bỏ tối đa 3 ảnh JPG/PNG, mỗi ảnh tối đa 5 MiB. Tóm tắt cập nhật theo biểu mẫu. Backend vẫn kiểm tra hợp đồng, ảnh và chống gửi trùng.
- Chi tiết khách: thông tin sự cố, ảnh riêng tư, lịch sử phản hồi/chuyển trạng thái, tiến trình và chi phí đã ghi nhận.
- Quản lý: `/QuanLyBaoHong` chỉ gồm tòa được giao; thẻ cần tiếp nhận/đang xử lý/quá hạn/hoàn thành tháng, ảnh đại diện, tìm kiếm/lọc/sắp xếp. Cảnh báo chỉ dành cho báo Gấp ở trạng thái Mới quá 24 giờ theo quy tắc hiện hành.
- Chi tiết quản lý: chọn tiếp nhận, hoàn thành hoặc từ chối theo trạng thái hợp lệ; tiếp tục xử lý bằng phản hồi. Ghi chi phí sau hoàn thành, giữ khóa chi phí đã lên hóa đơn. Nút trên danh sách mở chi tiết, không thay trạng thái bằng GET.

Không thêm lịch hẹn có cấu trúc, phân công kỹ thuật viên, SLA mới, ảnh hóa đơn sửa chữa hoặc trạng thái chưa đọc. Lịch dự kiến được trao đổi trong phản hồi hiện có. Không đổi schema, tài khoản hoặc DB đang dùng.

## Kiểm chứng

- Build đạt; nhóm báo hỏng 135/135 kiểm thử đạt, gồm hai ca mới kiểm tra tìm/lọc toàn bộ lịch sử, phân trang, quyền theo người/tòa, thứ tự, metadata và thống kê hoàn thành tháng. TRX: `data/repair-upgrade-tests/repair-upgrade-final.trx`.
- Edge headless trên bản sao DB v40: cả 5 màn ở 1440px/360px không tràn ngang; rà ảnh sau khi sửa kích thước cột. Kiểm tra tóm tắt trực tiếp, thêm/bỏ ảnh và giới hạn 3 ảnh, nút chọn trạng thái xử lý. Ảnh: `data/repair-upgrade-qa/browser-check-408a36ae`.
- Bộ chạy: `dotnet run --project verification/RepairUpgradeBrowser -- <thu-muc-QA-co-access.json> <base-url>`. Chỉ dùng DB sao chép; không đưa access.json vào Git. Các thao tác ảnh của bộ chạy kiểm tra giao diện, không gửi file giả lên server.
- Đã dừng server chính trước build và dừng server QA sau kiểm tra. Chưa kiểm tra Safari/thiết bị thật hoặc chạy toàn bộ suite. Cảnh báo license ImageSharp là cảnh báo sẵn có.
