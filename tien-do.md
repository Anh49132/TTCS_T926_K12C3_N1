### 14/10/2026 – S4-04 task 2: bảng tất toán cọc và khấu trừ
- PO xác nhận dùng cọc thực giữ, trừ thanh toán/cấn cọc đã xác nhận khỏi nợ; kết quả = cọc − dư nợ kỳ cuối − nợ kỳ trước − hư hỏng. Làm tròn từng khoản AwayFromZero; hư hỏng cho phép bằng 0, không cho âm/tên rỗng.
- Theo yêu cầu người dùng, khôi phục task 1 đang thiếu ở HEAD 29d4f02, nối hóa đơn kỳ cuối sang bảng tất toán. Chủ nhà thêm/sửa/xóa hư hỏng, xem dự tính từ máy chủ, lưu/sửa NHAP; khách xem cùng snapshot, không sửa. ADMIN chỉ xem, QUAN_LY không truy cập tài chính.
- Schema v31 bổ sung sổ cọc, bảng tất toán và chi tiết khấu trừ; phiên bản/dấu vân tay nguồn, BEGIN IMMEDIATE, FK RESTRICT, audit và rollback. Không thu/hoàn/cấn cọc trên UI, không phạt, chốt/kết thúc hợp đồng/đổi trạng thái phòng/PDF.
- Nâng trên bản sao DB giả v30: bảo toàn hash tất cả dòng/cột cũ, backup, integrity/FK và rerun đạt. DB local vẫn giữ nguyên v5/chưa có module hợp đồng/hóa đơn.
- Demo riêng localhost:5252; New-S404Demo.ps1/Start-S404Demo.ps1, tài khoản ngẫu nhiên trong access.json ignored. Edge desktop/360px: thêm/sửa/xóa, dự tính/lưu, khách xem cùng số liệu, sửa/khách lạ 403; không tràn ngang ở 360px. Chưa Safari/thiết bị thật, review/hợp nhất nhánh.
- Build cuối đạt; 121/121 test liên quan đạt (38 S4-04/khôi phục task 1 + 83 hồi quy). HTTP danh sách chủ nhà/khách 200; Edge lượt cuối đạt trên data/s404-task2-final, ảnh/report ignored browser-check-c27c22b6. Demo integrity=ok/FK=0. Cảnh báo ImageSharp/CS8601 có sẵn.
- Bàn giao và lệnh kiểm thử: [docs/S4-04-tat-toan-task2.md](docs/S4-04-tat-toan-task2.md).

### 13/10/2026 – Dữ liệu kiểm thử Sprint 3 bổ sung
- Thêm vào DB local tòa kiểm thử ID 7 cho tài khoản chủ nhà được yêu cầu: 60 phòng, 54 hợp đồng, 54 tài khoản khách mẫu, 6 yêu cầu, 6 hóa đơn. Giữ dữ liệu và tài khoản cũ.
- Đã nạp thử bản sao; backup, integrity_check=ok, foreign_key_check=0; kiểm tra bảo toàn mọi bản ghi cũ và schema. Không build/test ứng dụng vì không đổi mã.
- Hướng dẫn và thông tin đăng nhập mẫu nằm trong thư mục ignored data/sprint3-extra-20261013/live-verified/. Chưa nghiệm thu AC trên UI/thiết bị hay đo hiệu năng. Ba hóa đơn phát hành nhập snapshot còn tham chiếu kỳ/giá gốc; dùng nháp/kỳ mới để kiểm thử truy vết cùng hợp đồng.


- Bổ sung phân tầng S3-05: tòa mẫu ID 7 có 6 tầng × 10 phòng, giữ mã phòng. T051 tầng 2 thiếu điện, T052 tầng 4 thiếu nước kỳ 10/2026. Đã thử trên bản sao, backup, kiểm tra integrity/FK và đối chiếu các trường còn lại giữ nguyên. Mapping trong data/sprint3-extra-20261013/floors/mapping.json.


### 14/10/2026 – S4-06 task 2: ảnh báo hỏng
- PO chốt JPG/JPEG, PNG; tối đa 3 ảnh × 5 MiB (5.242.880 byte), nhận đúng giới hạn.
- Đã thêm chọn/xem trước/bỏ ảnh, validation giao diện và nội dung máy chủ, kho riêng ngoài wwwroot, endpoint ảnh kiểm tra quyền người gửi/no-store/nosniff, mở ảnh từ chi tiết.
- Schema v29 thêm anh_bao_hong, giữ bước v28 và dữ liệu cũ. Ghi báo hỏng/metadata/mã/audit cùng transaction; lỗi ảnh rollback, retry so hash/thứ tự chống trùng.
- Nâng bản sao v28 giữ 42 bảng/251 dòng, integrity/FK đạt, rerun an toàn. DB local/Sprint2/task1 giữ nguyên.
- HTTP upload/biên/ảnh giả/storage failure/403 đạt; trình duyệt bỏ/chọn lại/gửi 3 ảnh BH-202610-0013/Mới; DOM 360px không tràn, không chụp màn hình theo yêu cầu.
- 24 test báo hỏng/ảnh và 36 hồi quy thu/hủy thu đạt. Bộ rộng RoomServicesTests có 3 lỗi: 2 lỗi text tái hiện ở baseline c4b6fc6; ca login500 đạt khi chạy riêng và trên baseline. Cảnh báo ImageSharp/CS8601 hiện hữu.
- Demo localhost:5249; data/s406/images-latest.txt chứa đường dẫn ignored. Hướng dẫn/lệnh/test/giới hạn tại docs/S4-06-anh-bao-hong.md. Chưa có cập nhật trạng thái, phản hồi quản lý, dọn tệp mồ côi sau crash; chưa nghiệm thu trực quan/Safari/review thành viên khác. Không push task2.

### 14/10/2026 – S4-06 task 3: trạng thái và phản hồi quản lý
- PO chốt Mới→Đang xử lý/Từ chối, Đang xử lý→Hoàn thành; trạng thái cuối không mở lại; mỗi lần đổi có ghi chú, phản hồi ở mọi trạng thái. Chỉ QUAN_LY được gán tại tòa xử lý; khách gửi giữ quyền xem sau hết hợp đồng.
- Đã thêm danh sách/chi tiết quản lý trong phạm vi, xem ảnh riêng tư, đổi trạng thái, gửi phản hồi, lưu người/snapshot tên/nội dung/thời điểm và lịch sử trên cùng chi tiết của khách.
- v30 thêm bao_hong.phien_ban và bao_hong_trao_doi bất biến; chống stale409/retry trùng; trạng thái/lịch sử/audit cùng transaction, rollback khi lỗi; audit không lưu nội dung tự do.
- 76/76 test liên quan đạt (40 báo hỏng/ảnh/theo dõi, 36 thu/hủy thu); HTTP CSRF/403/chuyển trạng thái/phản hồi/stale/reload/hết hợp đồng/ảnh/integrity/FK đạt.
- Nâng bản sao v29 giữ 43 bảng/280 dòng và ảnh, integrity/FK/rerun đạt. DB dùng thật và demo cũ không nâng. Demo mới localhost:5250, data/s406/tracking-latest.txt ignored.
- Trình duyệt ID13: quản lý cập nhật Đang xử lý/gửi phản hồi, khách reload thấy cùng trang và 3 ảnh. DOM 360px không tràn; không chụp màn hình. Chưa nghiệm thu trực quan/Safari/review thành viên khác.
- Phạm vi S4-06 đủ AC1–AC4 về chức năng; chưa triển khai thông báo tự động, khách trả lời, chi phí và phần khác của S4-07. Hướng dẫn tại docs/S4-06-trang-thai-phan-hoi.md. Nhánh bàn giao Task3: `feature/S4-06/3.maintenance-request-status-feedback-tracking`.
