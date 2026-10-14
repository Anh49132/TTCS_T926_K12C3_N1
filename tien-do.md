
### 14/10/2026 – S4-07:1: tiếp nhận và tiến độ báo hỏng
- UI-01: thông báo thành công chuyển sang `_Toast` dùng chung, giữ tới khi đóng/rời trang; toast mặc định khác/lỗi vẫn tự ẩn3 giây. Chỉ sửa Details/_Toast/site.js, không đổi nghiệp vụ/DB. Build0 lỗi, 50/50 test báo hỏng đạt. Trình duyệt desktop1280/mobile360 xác minh thông báo, nút đóng, không lặp sau reload, lỗi không hiện success. Thử trên bản sao data/s407-ui01/demo/ui01.sqlite, SHA256 nguồn demo không đổi; hướng dẫn cập nhật runtime ở docs/S4-07-tiep-nhan-tien-do.md.
- PO chốt Chủ nhà chỉ xem nội dung/ảnh/trạng thái/lịch sử thuộc tòa hiện sở hữu, kể cả hợp đồng kết thúc; không cập nhật/phản hồi. Giữ chuyển trạng thái/ghi chú bắt buộc của S4-06.
- Thêm service/controller GET Chủ nhà với quyền backend ở list/detail/image, nối menu tới dữ liệu thật; dùng chung view, thêm tên người gửi và mô tả sự cố trong danh sách. CSS màn hình nhỏ chỉ trong trang báo hỏng.
- Không đổi schema/migration/updater. Demo riêng data/s407/demo-20261014-164333-49008c, localhost5271; SHA256 nguồn Sprint2 trước/sau không đổi, integrity/FK đạt.
- Build đạt0 lỗi; 50/50 test báo hỏng đạt (40 hồi quy +10 mới), HTTP403/CSRF400/ghi chú/chuyển trạng thái/stale409/retry/ảnh/lịch sử/hợp đồng kết thúc đạt. Lần test đầu bị sandbox chặn testhost; chạy lại ngoài sandbox đạt.
- Trình duyệt Chủ nhà có tên/ảnh/lịch sử, không form ghi; Quản lý có form đúng. DOM360px Chủ nhà354/Quản lý345, ảnh tải được. Chưa nghiệm thu Safari/thiết bị thật/review thành viên khác. Chưa commit/push/merge.
- Hướng dẫn, TC01–TC16 và giới hạn: docs/S4-07-tiep-nhan-tien-do.md. Chạy verification/Start-S407Demo.ps1, ID2 giữ trạng thái Mới để nghiệm thu.

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
