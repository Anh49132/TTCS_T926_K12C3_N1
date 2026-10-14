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

### 15/10/2026 – S4-04 task 3: chấm dứt trước hạn và phạt cọc
- Khôi phục task 1–2 từ commit 79d428a trên nhánh task 3 bắt đầu ở 29d4f02. Chưa commit/push.
- PO chốt: ngày trả < ngày kết thúc kỳ cuối (kể cả gia hạn); 4 lý do, Khác cần ghi rõ; Có phạt thì số nguyên đồng >0 và ≤ cọc thỏa thuận, Không phạt =0.
- Đã thêm chọn lý do/Có–Không/mức phạt ở trả phòng và tất toán, lưu cùng transaction, hiển thị cho chủ nhà/khách thuê; PHAT_COC riêng tối đa một dòng, không cộng lặp hư hỏng, cập nhật tổng khấu trừ và hoàn/trả thêm.
- Schema v32 thêm cham_dut_hop_dong, tien_phat và loai_khoan; unique/ CHECK/FK RESTRICT/trigger, snapshot/phiên bản/fingerprint chống stale, audit không ghi nội dung lý do khác. Giữ bước v31; nâng bản sao v31 có dữ liệu, bảo toàn mọi cột cũ, backup/integrity/FK/rerun đạt. Không nâng DB local v5 hoặc demo task 2 v31.
- Build đạt; 145/145 test liên quan đạt (24 task 3 + 121 hồi quy). Edge kiểm tra trả phòng, đổi Có/Không, thêm/sửa/xóa hư hỏng, lưu một dòng phạt, khách xem cùng số liệu, 403 khi sửa/trái sở hữu; DOM 360px không tràn. Ảnh/report ở browser-check-* trong bộ demo ignored.
- Demo riêng localhost:5253; data/s404-task3-demo/latest.txt trỏ bộ mới. Cọc 7m − cuối 1.863.742 − cũ 1.2m − hư hỏng450k − phạt3.5m => khách trả thêm13.742đ. New-S404Demo.ps1/Start-S404Demo.ps1 có -Task3; không ghi đè demo cũ.
- Chưa xác nhận/chốt tất toán, kết thúc hợp đồng/phòng/tin, ghi CAN_TRU/hoàn cọc hay PDF. Khi làm bước chốt phải chuyển phạt/hư hỏng thành PHAT_SINH và tránh cộng hai lần theo AGENTS 7.6. Chưa nghiệm thu Safari/thiết bị thật/review thành viên khác. Hướng dẫn: docs/S4-04-cham-dut-truoc-han-task3.md.

### 15/10/2026 – S4-04 task 4: phần nền xác nhận tất toán (đang làm, chờ PO)
- Nhánh task 4 bắt đầu từ nền v30, chưa có task 1–3; đã khôi phục worktree từ commit 3f3c477 (task 3). Không commit/push, không sửa CSDL local hoặc demo task 2–3.
- Đã chuẩn bị schema v33: xac_nhan_tat_toan (người/thời điểm/số tiền, unique, bất biến) và lich_su_trang_thai_phong; trigger khóa tạo/phát hành hóa đơn khi hợp đồng DA_KET_THUC, chặn hủy hóa đơn của tất toán đã chốt. Updater nối thêm, giữ bước cũ, có backup/integrity/FK; đã thử bảo toàn toàn bộ dữ liệu cũ trên bản sao v32.
- Đã chuẩn bị Razor kiểm tra/xác nhận/modal/kết quả và DTO; chưa nối action HoanTat hoặc thực thi transaction chốt, vì đang chờ quy tắc thanh toán do PO chốt. Không coi task 4 đã hoàn tất; UI mới chưa kiểm chứng qua trình duyệt.
- Câu hỏi đang chờ: chốt nghĩa vụ và tiếp tục thu/hoàn sau (CAN_TRU được ghi khi chốt), hay chỉ kết thúc sau khi đã thu/hoàn đủ. Đây là điều kiện nghiệp vụ bắt buộc của task 4, không tự suy đoán.
- Build đạt; 167/167 ca kiểm tra phần chung/hồi quy đạt, gồm chặn tạo/phát hành sau kết thúc tại service và SQLite, nâng bản sao v32/rerun/bảo toàn dữ liệu. Kết quả ignored: data/s404-task4-test-results/s404-task4-common.trx. Cảnh báo ImageSharp/CS8601 có sẵn. Chưa có demo task 4, chưa triển khai PDF.

### 15/10/2026 – S4-04 task 4: hoàn tất xác nhận tất toán và kết thúc hợp đồng
- PO đã xác nhận phương án chốt nghĩa vụ: chỉ Chủ nhà sở hữu có quyền ghi HOP_DONG + TAI_CHINH; phát hành kỳ cuối có hư hỏng/phạt, CAN_TRU đúng một lần; thu/hoàn thực tế ghi sau. Mục này thay trạng thái chờ PO ở ghi chú phần nền trước đó.
- Đã nối kiểm tra bản đã lưu → 2 bước đồng ý/modal → Hoàn tất → kết quả theo ảnh tham chiếu. Kiểm tra thiếu bảng/hóa đơn/dữ liệu, pending thanh toán, nháp khác, nguồn thay đổi, trạng thái phòng và phiên bản/fingerprint. Chủ nhà/khách thuê xem cùng bản bất biến; gửi trùng/đồng thời chỉ một lần thành công.
- Cùng transaction: PHAT_SINH vào hóa đơn cuối, phát hành/khóa snapshot và nguồn chỉ số, thông báo, CAN_TRU nợ cũ trước rồi kỳ cuối, DA_CHOT, dữ liệu người/tên/UTC/số tiền, hợp đồng DA_KET_THUC, phòng TRONG/lịch sử, tin NHAP và audit. Lỗi audit rollback tất cả. Không cộng hư hỏng/phạt hai lần sau khi đã gộp vào hóa đơn.
- Đã có ghi nhận hoàn cọc thực tế sau chốt (từng phần, chống trùng/vượt cọc), liên kết ghi thu hóa đơn còn nợ; cập nhật dư nợ/trạng thái của khách và quản lý hóa đơn để tính CAN_TRU mà không tính nó là tiền mặt đã thu. Bảng chốt giữ nguyên khi thu/hoàn sau đó.
- Schema v33 thêm xac_nhan_tat_toan và lich_su_trang_thai_phong; khóa tạo/phát hành sau DA_KET_THUC và hủy hóa đơn thuộc bảng chốt; giữ bước v31/v32, backup/integrity/FK/rerun, bảo toàn toàn bộ dòng v32 trên bản sao.
- Phát hiện bảng chi_so_dien_nuoc thực tế chỉ nhận kỳ trọn tháng. Không sửa CHECK/không tạo kỳ giả: chỉ số cuối kỳ lẻ lưu trong snapshot hóa đơn bất biến và khóa nguồn. Lần demo phát hiện lỗi đã rollback, bổ sung test task1→4 giữa tháng đạt.
- Build đạt, 189/189 test đạt (21 task4). Edge desktop/360px đạt toàn luồng, modal/kết quả không tràn, replay409, khách xác nhận/khách ngoài hợp đồng403, xem trạng thái Đã kết thúc và một dòng phạt. Ảnh/report: data/s404-task4-demo/20261015-task4-final/browser-check-d64d6978/ ignored.
- Demo cuối localhost:5254; data/s404-task4-demo/latest.txt trỏ bộ cuối. Hợp đồng1 đã chốt: hóa đơn cuối5.813.742đ gồm hư hỏng450k/phạt3.5m; nợ cũ1.2m; hai CAN_TRU tổng7m; còn thu13.742đ, một biên bản/tin nháp/lịch sử phòng, không tự thêm thanh_toan. Integrity/FK đạt. Hợp đồng2 còn hiệu lực để demo nhập từ đầu.
- DB local v5, demo task2 v31/task3 v32 giữ nguyên. Chưa PDF, chưa nghiệm thu Safari/thiết bị thật/review thành viên khác/hợp nhất. Hướng dẫn: docs/S4-04-xac-nhan-tat-toan-task4.md. Chưa commit/push.
