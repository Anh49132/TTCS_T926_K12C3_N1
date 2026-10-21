# Hoàn thiện Sprint 2 — 04/10/2026

Phạm vi: S2-01 đến S2-10, dựa trên dev tại f058836. Không bao gồm màn hình soạn hợp đồng S3-01.

## Các thay đổi sau rà soát

- Dịch vụ phải được gán cho phòng mới được lấy giá để hiển thị trên tin hoặc lập hóa đơn. Phòng không chọn gửi xe không còn bị cộng phí gửi xe của tòa. Giá riêng vẫn ưu tiên; lịch ngừng dịch vụ và snapshot hóa đơn cũ giữ nguyên.
- Duyệt Thuê ngay cập nhật yêu cầu, phòng Đã đặt cọc và tin Đã cho thuê trong cùng transaction. Nếu ghi lịch sử lỗi, cả ba trạng thái rollback.
- Đổi cách tính điện/nước có giá riêng không còn bị chặn. Quy tắc: giữ số tiền riêng, áp dụng cách tính và đơn vị mới từ kỳ kế tiếp. Ví dụ 4.200 đ/kWh chuyển thành 4.200 đ/người/tháng nếu chủ nhà không sửa giá riêng. Form thông báo rõ trước lưu; chủ nhà cần kiểm tra giá riêng để phù hợp đơn vị mới. Cấu hình kỳ hiện tại và hóa đơn đã phát hành không đổi.
- Mở lại bản nháp/đăng lại lấy tiêu đề và mô tả đã lưu. Tin đã cho thuê không bị gắn cảnh báo hết hạn của tin tạm ẩn.
- Kết quả tìm kiếm có thumbnail ảnh đại diện và liên kết tới chi tiết. Chỉ trả phòng trống trong tòa đang hoạt động, đồng nhất với trang chi tiết; giữ bộ lọc, thứ tự và phân trang 12 tin.
- Script HTTP/performance nhận diện tiêu đề có liên kết, tiếp tục kiểm tra kết quả chính xác.

## Đối chiếu story

| Story | Phần đã có và được giữ/hoàn thiện |
| --- | --- |
| S2-01 | Gán mặc định khi tạo phòng, chọn riêng, giá riêng ưu tiên, tổng cố định, ngừng từ kỳ sau, hóa đơn cũ giữ nguyên; bổ sung kiểm tra dịch vụ chưa gán. |
| S2-02 | 8 ảnh/phòng, 5 MiB/ảnh, JPG/PNG, reorder, ảnh đầu đại diện, thumbnail tối đa 400px không phóng ảnh nhỏ, xác nhận xóa và retry xóa tệp. |
| S2-03 | Phòng trống, lấy thông tin/ảnh/dịch vụ, nháp/hiển thị/ẩn/đã cho thuê, 30 ngày, unique tin hiển thị, auto-expire và cảnh báo. |
| S2-04 | Bộ lọc kết hợp, trạng thái/hạn, 3 cách sắp xếp, 12 tin/trang, gợi ý nới giá; đo HTTP 500 tin dưới 2 giây. |
| S2-05 | Chi tiết ảnh/thông tin/cọc/mô tả, đơn giá và khoản cố định đúng dịch vụ phòng, tổng ước tính cùng chú thích, anonymous và bố cục 360px. |
| S2-06 | Hai loại yêu cầu, ngày 0–60, sức chứa, chống yêu cầu mở trùng và dẫn tới yêu cầu cũ, mã YC-yyyyMM-xxxx. |
| S2-07 | Đủ cột, lọc trạng thái/tòa, mới nhất trước, nổi bật 24 giờ, badge menu. |
| S2-08 | Xác nhận/đổi lịch, cảnh báo trùng 30 phút, lý do từ chối, lịch sử, duyệt đặt cọc và nút hợp đồng. |
| S2-09 | Danh sách của khách, lịch/lý do, hủy Mới/Đã hẹn lịch, liên kết tin kể cả tin không còn public. |
| S2-10 | Theo đồng hồ/đầu người, giá bắt buộc >0, version kỳ kế tiếp, hiển thị kỳ và quy tắc giá riêng. |

## Kiểm chứng

- Full xUnit sau sửa: 249/249 PASS, không bỏ qua test. Kết quả: `verification/sprint2-audit/sprint2-completion.trx`.
- Bổ sung kiểm tra bản nháp và rollback trạng thái tin; kết quả riêng: `verification/sprint2-audit/sprint2-final-checks.trx`.
- Đo 500 tin giả (300 hợp lệ), 13 kịch bản bộ lọc/sắp xếp/phân trang, 3 mẫu/kịch bản: lần đầu 211,961 ms, mẫu chậm nhất 49,275 ms. Đo HTML qua HTTP localhost, không phải mạng 3G, tải đồng thời hoặc thời gian render trình duyệt.
- Browser thật 360×800: mở chi tiết từ kết quả tìm kiếm, anonymous, trang tìm kiếm và chi tiết không tràn ngang (scrollWidth 345px, viewport 360px). Screenshot: `verification/sprint2-audit/detail-360.jpg`.
- Mọi kiểm tra dùng database giả mới hoặc SQLite tạm; không nâng cấp hay sửa database cá nhân.
- Cảnh báo hiện có: không lấy được dữ liệu vulnerability NuGet (NU1900), thiếu license ImageSharp. Cần xử lý license phù hợp trước khi phát hành sản phẩm.

## Giới hạn bàn giao

- Chưa nghiệm thu 3G thật hoặc thiết bị cảm ứng vật lý. Các thao tác upload/reorder/delete đã có kiểm thử tự động từ dự án.
- Nút lập hợp đồng dẫn đến màn hình thông báo S3-01; việc soạn và ký hợp đồng chưa thuộc phần hoàn thiện Sprint 2 này.
- Mã nguồn được sửa tại workspace; chưa commit hoặc push lên GitHub.
