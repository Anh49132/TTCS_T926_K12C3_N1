# S4-07 — Đồng bộ giao diện báo hỏng

Nhánh `feature/S4-07/ui-improvements`, nền `8e4eca4`. Phạm vi: danh sách và chi tiết báo hỏng của Quản lý, Chủ nhà, Khách thuê; bổ sung liên kết báo hỏng vào menu Khách thuê theo quyền.

## Thay đổi

- Quản lý: thống kê toàn phạm vi được phân công, tab trạng thái, bộ lọc GET theo tòa/trạng thái/mức độ, cảnh báo Gấp quá 24 giờ và bảng responsive. Bộ lọc có nút Áp dụng và Xóa; các liên kết giữ query/phân trang.
- Chủ nhà/Khách thuê: bảng danh sách chung, thống kê và tìm kiếm/lọc tại trình duyệt trên tối đa 20 dòng của trang hiện tại, có thông báo rõ phạm vi khi phân trang. Chủ nhà không có nút gửi báo hỏng.
- Chi tiết dùng chung bố cục thông tin sự cố, đường phân cách giữa các ô, ảnh private, lịch sử và chi phí. Tiêu đề gọn; màn hình nhỏ xếp một cột. Đường dẫn danh sách/ảnh chọn đúng controller theo vai trò.
- Form phản hồi, đổi trạng thái, ghi chi phí giữ điều kiện quyền hiện hữu. Không đổi luồng ghi nghiệp vụ, hóa đơn hoặc schema.
- Phần backend đã có trong nhánh chỉ bổ sung truy vấn tổng quan chỉ đọc `TongQuanAsync` cho danh sách Quản lý, dùng cùng truy vấn giới hạn phạm vi và kiểm tra quyền như danh sách. Có test thống kê/phân quyền/phân trang; test HTML điều chỉnh selector theo giao diện bảng.

## Kiểm tra

Build C#/Razor thành công. Nhóm test BaoHong đã đạt 133/133 trong lượt kiểm tra trước; lần kiểm tra trước push và kết quả ghi tại `tien-do.md`. Test dùng DB fixture tách biệt. Không đưa DB, bộ demo, credentials hoặc thư mục data vào Git.

Browser desktop và mobile 360px: danh sách, bộ lọc và chi tiết đã kiểm tra; không tràn ngang. Chi tiết khách thuê tải ảnh đúng endpoint BaoHong, hiện lịch sử/chi phí, không có form quản lý. Chưa kiểm tra Safari và chưa chạy full suite toàn dự án. Lịch sử kiểm tra chi tiết tại `tien-do.md`.
