### 14/10/2026 – S4-04 task 2: bảng tất toán cọc và khấu trừ
- PO xác nhận dùng cọc thực giữ, trừ thanh toán/cấn cọc đã xác nhận khỏi nợ; kết quả = cọc − dư nợ kỳ cuối − nợ kỳ trước − hư hỏng. Làm tròn từng khoản AwayFromZero; hư hỏng cho phép bằng 0, không cho âm/tên rỗng.
- Theo yêu cầu người dùng, khôi phục task 1 đang thiếu ở HEAD 29d4f02, nối hóa đơn kỳ cuối sang bảng tất toán. Chủ nhà thêm/sửa/xóa hư hỏng, xem dự tính từ máy chủ, lưu/sửa NHAP; khách xem cùng snapshot, không sửa. ADMIN chỉ xem, QUAN_LY không truy cập tài chính.
- Schema v31 bổ sung sổ cọc, bảng tất toán và chi tiết khấu trừ; phiên bản/dấu vân tay nguồn, BEGIN IMMEDIATE, FK RESTRICT, audit và rollback. Không thu/hoàn/cấn cọc trên UI, không phạt, chốt/kết thúc hợp đồng/đổi trạng thái phòng/PDF.
- Nâng trên bản sao DB giả v30: bảo toàn hash tất cả dòng/cột cũ, backup, integrity/FK và rerun đạt. DB local vẫn giữ nguyên v5/chưa có module hợp đồng/hóa đơn.
- Demo riêng localhost:5252; New-S404Demo.ps1/Start-S404Demo.ps1, tài khoản ngẫu nhiên trong access.json ignored. Edge desktop/360px: thêm/sửa/xóa, dự tính/lưu, khách xem cùng số liệu, sửa/khách lạ 403; không tràn ngang ở 360px. Chưa Safari/thiết bị thật, review/hợp nhất nhánh.
- Build cuối đạt; 121/121 test liên quan đạt (38 S4-04/khôi phục task 1 + 83 hồi quy). HTTP danh sách chủ nhà/khách 200; Edge lượt cuối đạt trên data/s404-task2-final, ảnh/report ignored browser-check-c27c22b6. Demo integrity=ok/FK=0. Cảnh báo ImageSharp/CS8601 có sẵn.
- Bàn giao và lệnh kiểm thử: [docs/S4-04-tat-toan-task2.md](docs/S4-04-tat-toan-task2.md).

### 14/10/2026 – S4-07:2 giai đoạn 2/6: schema v31
- Thêm bảng chi phí/lịch sử riêng, view trạng thái, CHECK/FK/UNIQUE/index/trigger: một tổng phí, chỉ HOAN_THANH/Quản lý đúng phân công, hợp đồng gốc, tiền nguyên dương, nguồn hóa đơn NHAP giữ chỗ, chống hai khoản hiệu lực và giữ snapshot hủy/thay thế. Không tự tạo khoản thu/hóa đơn.
- Chỉ nâng data/s407-cost/stage1-20261014-01/s407-cost-v30.sqlite: v31, integrity/FK đạt;45 bảng/299 bản ghi cũ giữ nguyên; thêm2 bảng rỗng và một dòng phiên bản. Rerun không đổi byte/schema/dữ liệu. Hash nguồn S407 và backup v30 giữ nguyên.
- Build0 lỗi,22/22 test schema trên bản sao tạm đạt; không seed/reset và không có dữ liệu test vĩnh viễn trong bản thử nghiệm. Hai lỗi công cụ kiểm chứng (handle khi đọc hash/tuple-list) được xử lý ở test hoặc đối chiếu chỉ đọc, không sửa DB nghiệp vụ.
- Không sửa service/controller/ViewModel/Razor; chưa chạy full suite/HTTP/UI, chưa commit/push/merge. Tích hợp giai đoạn3–5 và giới hạn fixture/optional invoice module ghi trong docs/S4-07-02-schema-v31.md. Dừng ở giai đoạn2.

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

### 15/10/2026 — S4-04 task 5: Xuất và tải bảng tất toán PDF
- Đã áp dụng mẫu A4 NhàTốt do PO xác nhận: hai bên/hợp đồng/phòng/ngày trả, toàn bộ dòng hóa đơn cuối, cọc, công nợ cũ, từng hư hỏng, lý do/phạt, kết quả, người/thời điểm xác nhận và trạng thái hợp đồng.
- GET /TatToan/TaiPdf/{hopDongId}: kiểm tra quyền module và sở hữu ở backend, chỉ xuất bảng DA_CHOT của hợp đồng đã kết thúc; attachment/no-store/nosniff. Nút tải cho Chủ nhà và Khách thuê ở banner và Tài liệu liên quan, theo theme ảnh mẫu và responsive 360px.
- Số tiền dùng snapshot đã chốt/chi tiết hóa đơn đã phát hành, không trừ hư hỏng/phạt hai lần; PDF giữ nguyên nội dung sau thu/hoàn tiếp theo. QuestPDF và font Lato sẵn có; không thêm thư viện/schema, không nâng cấp DB đang dùng.
- Build đạt; hồi quy 192/192, nhóm PDF đầy đủ 4/4 (bao gồm ca bổ sung tiền phòng theo ngày + điện nước). PdfPig mở/trích tiếng Việt có dấu và kiểm tra PDF nhiều trang. Edge desktop/360px: hai bên tải được, hai tài khoản ngoài hợp đồng bị 403. Chưa kiểm tra Safari/thiết bị thật.
- Demo mới: http://localhost:5255/TatToan/ChiTiet/1; tài khoản riêng trong data/s404-task5-demo/20261015-task5-verified/access.json. Tạo/chạy lại bằng verification/New-S404Demo.ps1 -Task5 và Start-S404Demo.ps1 -Task5 -Port 5255. Giữ nguyên các demo cũ và DB local.
- Chi tiết và lệnh kiểm thử: docs/S4-04-xuat-pdf-task5.md. Chưa commit/push/merge hoặc review bởi thành viên khác.
# 14/10/2026 — S4-07:2 chi phí sửa chữa

Theo phê duyệt mới, database demo chính S4-07 đã nâng v30→v31; dữ liệu cũ và backup v30 bảo toàn, integrity/FK và rerun đạt. Hoàn thiện service/POST/CSRF/quyền/phân công/transaction/chống trùng, chi tiết dùng chung ba vai trò và toast. Tích hợp bảo vệ snapshot hóa đơn nháp/hủy/thay thế, không tự đưa phí vào hóa đơn hay phát hành trên demo. Runtime launcher đã build mới tại data/s407/runtime. Báo cáo và AC/test: [docs/S4-07-02-chi-phi.md](docs/S4-07-02-chi-phi.md). Test ghi chỉ trên bản sao; chi phí demo chính vẫn0 dòng. Chờ nghiệm thu; không commit/push/merge.
# 14/10/2026 — S4-07:2 xử lý lỗi nghiệm thu

Đã sửa validation cập nhật ngay khi sửa tiền hợp lệ và hướng dẫn động theo bên chịu phí; server validation/phân quyền/schema giữ nguyên. 95/95 báo hỏng/chi phí PASS; hóa đơn162/164 PASS, hai lỗi nhãn được chạy lại trên baseline4364280 và cũng FAIL, không tự sửa ngoài phạm vi. Kiểm tra chống trùng/vòng đời hóa đơn PASS. SHA256 và snapshot mọi bảng database demo chính đầu/cuối khớp, dữ liệu nghiệm thu giữ nguyên. Runtime launcher đã build lại. Báo cáo: [docs/S4-07-02-xu-ly-loi.md](docs/S4-07-02-xu-ly-loi.md). Chờ nghiệm thu và quyết định hai lỗi hóa đơn cũ; không commit/push/merge.
# 14/10/2026 — S4-07:2 RC-01 trước commit

Sửa service chi phí để hỗ trợ database v31 chưa cài module hóa đơn: kiểm tra module trong transaction, bỏ truy vấn bảng không tồn tại khi module chưa cài, giữ khóa/chống trùng/kỳ phát hành khi có module; không tạo bảng hóa đơn. Thêm hai case tự tạo DB giả tái hiện FAIL trước sửa và kiểm tra lưu/sửa/lịch sử/replay/ba vai trò. Báo cáo tại docs/S4-07-02-chi-phi.md. Không tác động database demo gốc/backup, không sửa20FAIL cũ, không commit/push/merge; chờ duyệt.

# 15/10/2026 — S4-07:02 rà soát để người dùng tự commit

Sửa dấu ngoặc thừa khiến cài trigger hóa đơn v31 lỗi SQL; chuyển fixture schema chi phí sang dữ liệu giả tự tạo, không phụ thuộc backup ignored; bổ sung cách tính/đơn vị snapshot trên chi tiết hóa đơn. Thêm test lưu chi phí trước rồi cài module hóa đơn, giữ lịch sử và khóa nguồn đã liên kết. Không đổi công thức, không tự đưa phí vào hóa đơn, không chạy updater/seed trên demo.

Rebuild 0 lỗi; smoke4/4 PASS; full suite871ca: **853PASS/18FAIL/0SKIP**, gồm **98/98 báo hỏng/chi phí và164/164 hóa đơn/dịch vụ PASS**. Hai lỗi nhãn hóa đơn cũ đã hết; 18FAIL còn lại trùng danh sách lỗi lịch sử ở module khác, không sửa assertion để che lỗi. Chưa nghiệm thu lại UI/mobile/Safari/SMTP. Chi tiết lỗi, lệnh chạy, bằng chứng và giới hạn: [docs/S4-07-02-ra-soat-20261015.md](docs/S4-07-02-ra-soat-20261015.md). Giữ thay đổi có sẵn và project test; không stage/commit/push.

# 15/10/2026 — S4-07:03 cảnh báo báo hỏng Gấp quá 24 giờ

PO chốt tính từ lúc gửi, chỉ GAP + MOI + chờ >24 giờ; đúng24giờ chưa cảnh báo, dừng khi DANG_XU_LY/HOAN_THANH/TU_CHOI. Phản hồi không đặt lại giờ chờ. Danh sách và chi tiết Quản lý dùng cùng bộ tính theo ITimeProvider, nhãn “Gấp — Quá 24 giờ”, tổng giờ/phút chờ và viền/nền nổi bật; tải lại tự đánh giá theo giờ hiện tại. Giữ quyền/phân công, thứ tự và phân trang, không đổi schema hoặc ghi audit khi đọc.

Build/Razor đạt0lỗi; **114/114 test báo hỏng/chi phí PASS**, gồm16ca mới: mốc24giờ±1tick,26giờ, các trạng thái loại trừ, cập nhật theo giờ/đổi trạng thái, quyền403/CSRF400 và không ghi dữ liệu khi đọc. HTTP demo riêng5291 sáu ca đạt, DB nguồn/backup giữ hash. Không chạy lại full suite trong lát này. Có script tạo/chạy/kiểm tra demo độc lập; dữ liệu và tài khoản ignored. Chưa nghiệm thu UI desktop/360px/Safari vì automatic approval review chặn mở trình duyệt do hết hạn mức; không dùng công cụ khác vượt chặn. Bàn giao: [docs/S4-07-03-canh-bao-qua-24-gio.md](docs/S4-07-03-canh-bao-qua-24-gio.md). Nhánh kế thừa01+02; chưa stage/commit/push.

Cập nhật nghiệm thu UI15/10/2026: cùng công cụ trình duyệt đã hoạt động lại. Bản demo mới riêng `demo-20261015-044703-a7e78c`, desktop1366×900 và360×800: danh sách/chi tiết hiển thị cảnh báo đúng, scrollWidth1351≤1366 và345≤360. Nhận xử lý trên360px lưu thành công, có toast/lịch sử và cảnh báo biến mất ở cả hai trang. Ảnh bằng chứng ignored được ghi trong tài liệu bàn giao. Không đổi mã trong lượt UI, không chạm DB nguồn. Chưa kiểm tra Safari vì không có Safari kết nối; không coi giả lập360px là Safari thật. Chưa commit/push.

# 15/10/2026 — S4-07:04 lọc danh sách báo hỏng

Nhánh `feature/S4-07/04-repair-request-filters` tạo từ `a9c69a4`, chứa đủ01–03. Thêm bộ lọc GET tòa/trạng thái/mức độ kết hợpAND trước phân trang; chỉ tòa được giao, tòa ngoài phạm vi403, tham số sai400. Nhãn Đã xong ánh xạHOAN_THANH. Giữ cảnh báo quá24giờ, điều kiện/trang khi mở chi tiết/quay lại/phân trang/validation/lưu trạng thái/phản hồi/chi phí. Có thông báo rỗng và Xóa bộ lọc về mặc định. Không đổi schema hoặc nghiệp vụ tiền/trạng thái/CSRF.

Build/Razor đạt0lỗi; **125/125testPASS** (114hồi quy+11mới), không skip. HTTP demo5292 với6truy vấn lọc đạt, GET không đổi snapshot nghiệp vụ/audit. Đã nghiệm thu trực quan desktop1366×900 và360×800: lọcTòaA/Mới/Gấp→đúng1kết quả+cảnh báo, detail/back giữ lựa chọn, tòa trống hiện thông báo, xóa lọc về10thẻ mặc định; không tràn ngang. Chạy lại full suite15/10/2026: **898ca,880PASS/18FAIL/0SKIP**,125ca báo hỏng đềuPASS; đối chiếu TRX với baseline853/871 xác nhận18FAIL trùng hoàn toàn, không có lỗi mới. Chưa Safari vì danh sách trình duyệt khả dụng không có kết nối Safari; cần macOS/iOS, không coi360px là Safari. Lượt cuối không tạo/chụp thêm ảnh theo yêu cầu. Demo riêng/ảnh cũ/TRX/credential ignored; DB nguồn vàbackup giữ hash, không chạm local. Chưa đủ toàn bộ DoD/Safari, chưa stage/commit/push. Chi tiết/lệnh chạy/test: [docs/S4-07-04-loc-bao-hong.md](docs/S4-07-04-loc-bao-hong.md).

# 15/10/2026 — Hòa giải S4-07 với dev S4-04

Nhánh tích hợp riêng `codex/s407-dev-reconcile` gộp devedef5ea và feature43f8998, giữ mapping/nghiệp vụ cả tất toán và báo hỏng. Giải quyết7file conflict; v34 nhận diện hai lịch sử v31 và cài module còn thiếu mà không đổi marker/dữ liệu cũ. Fixture giữ cả hai nhóm helper; các test nâng cấp liên quan kỳ vọng34. Build đạt0lỗi,6/6test hòa giải đạt,382hồi quy có381PASS/1FAIL Login500 (chạy riêng lại1/1PASS); nhóm còn lại609ca có589PASS/20FAIL, gồm18baseline+2fixture tạo hóa đơn sau kết thúc hợp đồng. Đã sửa thứ tự dữ liệu hai fixture, giữ guard/assertion, nhóm kiểm tra cuối52/52PASS. Phủ991ca khác nhau; tổng hợp kết quả cuối từng ca973PASS/18FAIL trùng baseline, không phải một full suite xanh; Login500 cần theo dõi. Không sửa18lỗi cũ ngoài phạm vi, không đổi DB local/demo thật, không tạo ảnh/không nghiệm thuSafari, chưa commit/push/mergedev. Chi tiết: [docs/S4-07-dev-schema-v34.md](docs/S4-07-dev-schema-v34.md).

# 15/10/2026 — Chuyển tích hợp về S4-07:04 và xử lý18lỗi baseline

Theo yêu cầu người dùng, toàn bộ phần hòa giải đã kiểm thử được chuyển từ worktree thử về `feature/S4-07/04-repair-request-filters`, gộp devedef5ea, giữ cảS4-04/S4-07. Người dùng yêu cầu sửa cả18lỗi cũ: sửa lưu lại tài liệu bản nháp (giữ tệp/backup/rollback/bất biến sau hiệu lực), phục hồi tin mới nhất trang chủ, badge yêu cầu/menu tài khoản và nhãn quá hạn từng dòng; sửa fixture/schema/selector theo chức năng hiện hành, giữ các assertion nghiệp vụ/quyền. Thêm2test bảo toàn tài liệu/rollback. Các ca lỗi nhỏ đã đạt khi kiểm tra lại, fullsuite đang chạy. Không tạo/chụp ảnh hoặc sửa DB thật, chỉ đẩy nhánhS4-07:04 sau khi kiểm tra hoàn tất. Báo cáo: [docs/S4-07-04-tich-hop-va-sua-18-loi.md](docs/S4-07-04-tich-hop-va-sua-18-loi.md).

Cập nhật kiểm thử cuối15/10/2026: fullsuite trên S4-07:04 tích hợp devedef5ea đạt **993/993PASS,0FAIL/0SKIP**, toàn bộ18ca baseline đãPASS. Không sửa mã khi chạy; kết quả tại data/s407-final-fixes/full-final.trx ignored. Theo yêu cầu tiếp theo, lưu phần sửa vào commit cục bộ S4-07:04 rồi chuyển thư mục chính sang dev mới nhất a934630; không coi test993ca là test bản S4-05 mới.

# 15/10/2026 — Người dùng yêu cầu tích hợp và push dev

Merge S4-07 commit903fe8a vào deva934630 có S4-05 task1–4. Một conflict test tin đăng được giải bằng selector/form/assertion cụ thể từ dev. Build đạt, hồi quy tích hợp **575/575PASS,0FAIL/0SKIP**; đầy đủ báo hỏng, nhóm hóa đơn/tất toán/thu-hủy/giải phóng phòng/tạo Nháp, hợp đồng và đăng Nháp/danh sách phòng trống/dashboard. Không thay DB thật hoặc tạo ảnh; chưaSafari, không coi575ca là fullsuite devmới. Báo cáo: [docs/S4-07-dev-push-20261015.md](docs/S4-07-dev-push-20261015.md). Push duy nhấtdev theo yêu cầu, khôngforce/tag.
