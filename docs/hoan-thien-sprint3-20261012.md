# Hoàn thiện Sprint 3 — 12/10/2026

Phạm vi: sửa các điểm thiếu/lỗi đã xác nhận trong [báo cáo 44 tiêu chí](kiem-tra-sprint3-20261012.md), cập nhật kiểm thử và xác minh trên dữ liệu giả. Không nâng cấp hoặc chỉnh dữ liệu database local; không đổi schema, thư viện ứng dụng hay công thức tính tiền.

## Các thay đổi

- Khôi phục danh sách người đứng tên, người đang ở, sắp vào và đã rời phòng trên tab người ở ghép; khôi phục form ghi nhận chuyển đi có CSRF/quyền/phiên bản. Ngày gợi ý chuyển đi không trước ngày bắt đầu ở. Giữ form thêm người và giao diện đã thiết kế.
- Lưu snapshot dịch vụ khi tạo hợp đồng hoặc sửa Nháp, cùng transaction với hợp đồng, kỳ, chỉ số, phòng/tin và audit. Giá lấy theo ngày bắt đầu, chỉ dịch vụ đã gán cho phòng, ưu tiên giá riêng; sửa Nháp không tạo snapshot trùng. Hợp đồng đã hiệu lực không bị sửa snapshot bởi luồng này. Chi tiết/PDF dùng thông tin đã lưu, không bị thay theo giá phòng sửa sau đó.
- Truy vấn chi tiết hợp đồng kiểm tra sự tồn tại của bảng dịch vụ/hóa đơn tùy chọn, giữ kiểm tra quyền tài chính. DB hợp lệ chưa cài module hóa đơn vẫn xem hợp đồng/người ở và xử lý validation/rollback được; web không tự cài bảng.
- Dashboard chủ nhà hiển thị cảnh báo đến hạn theo tòa/kỳ, số phòng thiếu và liên kết đúng trang tiến độ. Không bỏ thẻ phòng chưa chốt hiện có.
- PDF phân biệt người chưa ở với người đã chuyển đi. Khách ở ghép vẫn được tính đang ở trong chính ngày chuyển đi, đồng nhất quy tắc khoảng ngày bao gồm hai đầu.
- Sửa fixture nâng cấp schema: loại phần mở rộng phòng v24/v25 rỗng trước khi hạ marker trên DB giả, không nới kiểm tra của updater. Cập nhật kỳ vọng phiên bản, route/nhãn, số điện thoại được che và context HTTP. Kiểm thử kích hoạt đồng thời chạy hai tác vụ trên worker riêng, tránh chặn continuation trong synchronization context của xUnit.
- Bộ S3-08 dùng bảng thanh toán thật của fixture hiện tại, giữ bảng này sau kiểm tra; sửa bản sao schema cũ và cho phép Edge khi máy không cài Chrome.

Không tự bổ sung giá/dịch vụ vào hợp đồng lịch sử đã hiệu lực thiếu snapshot: không có đủ bằng chứng giá thỏa thuận cũ để suy đoán. Các hợp đồng mới và Nháp được sửa dùng luồng snapshot mới.

## Kiểm chứng

- Bộ xUnit Sprint 3: **373/373 PASS, 0 FAIL, 0 SKIP**, khoảng 3 phút 39 giây; `data/sprint3-audit-20261012/sprint3-final.trx`. Bao gồm toàn bộ 370 ca của lượt rà soát và 3 ca mới về tạo hợp đồng → PDF, giá riêng/giá có hiệu lực, giữ snapshot, lưu lại Nháp không trùng và rollback khi snapshot lỗi.
- Sau chỉnh nhãn PDF/biên ngày chuyển đi: **21/21 PASS**, gồm 2 ca mới kiểm tra ngày chuyển đi và ngày kế tiếp; `sprint3-pdf-boundary-final.trx`. Tổng cộng **375 ca duy nhất đã đạt**, không cộng trùng 19 ca chạy lại. Ảnh PDF được render và kiểm tra lại từ endpoint sau chỉnh sửa, hiển thị đúng “Chưa ở” cho người chưa bắt đầu ở.
- Bộ riêng `verification/S308`: **193 assertions PASS**, exit 0; kiểm tra HTTP/quyền/CSRF, Nháp, phát hành/thông báo/email giả, hủy/thay thế, lịch sử, concurrency/rollback, integrity/FK, nâng cấp bản sao và giao diện 360px. Không bỏ qua nhóm migration hoặc browser trong lượt này.
- Kiểm tra trình duyệt Edge trên DB giả: trang người ở ghép, chốt điện nước, hợp đồng khách thuê đều không tràn ngang ở 360px; đầu vào công tơ dùng bàn phím số, lưu một phòng thành công. Đã chụp và kiểm tra ảnh, gồm người sắp vào và form chuyển đi.
- Đã mở PDF thật từ endpoint khách thuê trong trình duyệt, kiểm tra trang A4 có thông tin hợp đồng, người ở, bảng dịch vụ/đơn giá, số trang; không thấy cắt chữ hoặc chồng nội dung. Sau sửa nhãn người chưa ở, chạy lại kiểm tra PDF/biên ngày và render.
- Các ca phát hành 50 hóa đơn, chống gửi lặp, quyền khác tòa và khóa chỉ số đạt ngưỡng dưới 30 giây. Build C#/Razor đạt; còn các cảnh báo ImageSharp license/CS8601 đã có trước.
- `git diff --check` đạt. Không sửa project test `.csproj`, không stage/commit DB, backup, dữ liệu cá nhân hoặc thư mục `data/`.

Lượt tổng hợp trước đó bị treo ở bài kiểm tra kích hoạt đồng thời và đã được dừng; không dùng lượt bị abort làm bằng chứng PASS. Lượt 373 ca phía trên chạy lại sau sửa cách tổ chức test và kết thúc bình thường, exit 0.

Kết quả/ảnh QA được giữ trong `data/sprint3-audit-20261012` (đã ignore). Các web host kiểm thử dùng DB giả và được dừng sau khi kiểm tra.

## Chính sách giữ nguyên và giới hạn

- Chống hóa đơn trùng theo **hợp đồng/tháng**, theo quyết định hiện hành; hai khách nối tiếp cùng phòng có hóa đơn riêng.
- Gia hạn giữa tháng tiếp tục chia tiền phòng theo từng đoạn ngày với giá từng kỳ, giữ snapshot hóa đơn cũ. Không tự đổi sang đợi tháng sau; chính sách này vẫn cần PO chốt nếu muốn hành vi khác.
- Cảnh báo công tơ tiếp tục dùng tiêu thụ mới > 2 lần trung bình 3 kỳ hợp lệ; chưa đủ 3 kỳ thì không có cảnh báo so sánh.
- Chưa nghiệm thu Safari/thiết bị cảm ứng thật, SMTP thật hoặc review/hợp nhất bởi thành viên khác. Chưa thể gọi toàn bộ Definition of Done đã hoàn thành chỉ từ mã và kiểm thử tự động.

Khởi động lại ứng dụng để nạp mã mới. Database local hiện ở v25 nên các thay đổi này không cần chạy updater.
