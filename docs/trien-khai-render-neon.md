# Phương án Render Free + Neon Free — NhàTốt

## Cập nhật triển khai 23/10/2026

Người dùng yêu cầu URL `https://nhatot.onrender.com` và đã cho phép rõ ràng lưu bản sao 61 bảng/19.489 dòng lên Neon main. Lệnh cutover đầu bị auto-review chặn vì phê duyệt cũ chỉ dành cho nhánh test; chưa truyền dữ liệu ở lệnh bị chặn. Sau câu trả lời mới, đang chạy `--cutover-empty-main`, chỉ nhận nhánh main đã pin, từ chối public có bất kỳ object nào, đối chiếu giá trị và 49 EF entity trước commit, giữ sequence cả ID đã xóa và giữ nguyên nguồn. Các mục trạng thái ban đầu bên dưới là lịch sử.

- Đã chuyển đủ 2.889 file đang tham chiếu, **180.007.913 byte**, vào bucket private trên main; đọc lại SHA-256 khớp toàn bộ. Không chuyển file mồ côi. Manifest/credential chỉ nằm trong ignored `data/postgres-port/`.
- Tích hợp AWS SDK S3 4.0.104.5, path-style và TLS; các thư mục local là cache, upload phải thành công trước nghiệp vụ commit. Đường dẫn file private vẫn phục vụ qua endpoint quyền cũ. Ảnh phòng công khai tải qua app, không mở public bucket. Kho khóa Data Protection online dùng AES-GCM với master secret riêng, lưu bền vững; không chuyển DPAPI Windows.
- Web chọn provider theo cấu hình; PostgreSQL yêu cầu kho private và cache scope rõ ràng, kiểm tra schema v42/61 bảng/60 trigger, không tự tạo/nâng cấp. SQLite local giữ cách chạy cũ. Có Dockerfile build từ nguồn riêng cho Render và context allowlist loại toàn bộ dữ liệu local.
- **183/183** kiểm thử ảnh/tài liệu/Brevo đạt. Kiểm thử PostgreSQL với dữ liệu giả đã đạt: commit thu tiền một phần/đủ, replay, trái chủ sở hữu, kiểm tra phiên, hai khoản thu đồng thời không vượt nợ, lỗi audit rollback, trigger bất biến và reorder 8 ảnh giữ CHECK/unique. Bản schema v5 sửa alias `old` xung đột record OLD của PL/pgSQL.
- Đã kiểm tra ghi/đọc dữ liệu giả và phục hồi cache sau restart trên Neon thật. Test phụ phát hiện và sửa mất password khi tạo kết nối phụ từ connection đã mở: dùng Npgsql CloneWith giữ authentication mà không bật PersistSecurityInfo. Adapter giữ nguyên dollar-quoted function body và positional parameter.
- Secret Render chỉ ở `data/postgres-port/render-env.json`; không in/commit. Background jobs tạm tắt trong bước xác minh để không phát thư tồn đọng. Chưa coi trạng thái tạo service là deploy thành công; phải đợi live và kiểm tra HTTP thực tế. Không nâng gói hoặc tạo compute trả phí.

## Quyết định và trạng thái

Người dùng yêu cầu tự triển khai sau khi xem phương án đổi hosting/CSDL. Tiếp tục giới hạn 0 phí: chỉ Free, không thêm thẻ/nâng gói/nạp credit. SQLite gốc phải giữ nguyên, không chạy initializer/updater trên nguồn. Railway vẫn tạm dừng build Free/Trial tại lần kiểm tra này.

Neon và Render đã kết nối. Người dùng đã chọn **My Workspace** (`tea-dbd3aaui0phs73824n7g`) để triển khai Render compute **Free**. Chưa tạo Render service, chưa có URL online. Không cần yêu cầu chọn workspace hoặc kết nối lại.

Đã xác minh Neon organization đang ở **Free** và tạo project `nhatot` (`calm-shadow-85538869`), PostgreSQL 17 tại Singapore, compute min/max 0,25 CU. Nhánh `main` (`br-bitter-voice-b360ggd5`) vẫn trống. Nhánh `codex-migration-test` (`br-spring-smoke-b33dbe1e`) dành riêng cho kiểm thử; schema public v1 đầu tiên còn trống, không dùng để chạy app. Các lượt nhập bản sao mới tạo schema transaction riêng và rollback, không ghi đè schema public này.

Người dùng đã cho phép rõ ràng chuyển bản sao **61 bảng/19.489 dòng**, gồm tài khoản/hash, hồ sơ và tài chính, lên nhánh thử để đối chiếu rồi rollback. Phê duyệt này được lấy sau khi auto-review chặn lần xuất đầu; lần bị chặn chưa truyền dữ liệu. Không suy diễn phê duyệt đó thành cho phép phát email tồn đọng hoặc công khai file riêng.

## Kiểm kê nguồn đã chạy

Chạy chỉ đọc bằng Python bundled:

```powershell
& 'C:/Users/cter4/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' deployment/Inspect-PostgresPort.py
```

Kết quả thực tế ngày 22/10/2026: schema v42, **61 bảng, 19.489 dòng, 51 index tường minh, 60 trigger và 1 view nghiệp vụ**. Integrity đạt, không có vi phạm foreign key, SHA256 file nguồn trước/sau giống nhau. Không in giá trị từng dòng, hash mật khẩu, thông tin cá nhân hoặc credential. Inventory mới có cả view nằm trong `data/postgres-port/20261022T163744Z-4a88723b/inventory.json` (ignored); không commit báo cáo này.

Script tự mở `mode=ro`, bật `query_only`, dùng một transaction đọc; không tạo DB mới nếu đường dẫn nguồn sai. Script không phải công cụ chuyển schema tự động.

## Phần PostgreSQL đã kiểm chứng

- `deployment/Build-PostgresSchema.py` tạo SQL ứng viên từ DDL thực tế, giữ PK/FK/CHECK/index/trigger và view; từ chối kiểu cột hoặc thân trigger chưa hỗ trợ. SQL v4 hiện ở `data/postgres-port/candidate-v4.sql`, chưa phải migration được phép áp dụng vào DB đang dùng. Có 7 kiểm thử thuần Python cho null-safe equality, literal, GLOB, kiểu thời điểm, view và từ chối cấu trúc không hiểu.
- `deployment/PostgresPort` là công cụ Release riêng, không khởi chạy web/initializer. `--rehearse` chỉ nhận secret config nằm trong ignored `data/postgres-port`, pin nhánh/host thử, kết nối TLS VerifyFull, backup SQLite bằng API vào file mới, kiểm tra integrity/FK, nhập vào schema UUID riêng trong một transaction, đối chiếu rồi rollback. Không có chế độ commit/cutover. Lỗi chỉ in loại lỗi/SQLSTATE và tên metadata, không in Detail hoặc giá trị dòng.
- 61 bảng/19.489 dòng đã nhập thử và đối chiếu hash theo giá trị từng dòng. Toàn bộ scalar của **49 entity EF** đã đối chiếu giữa SQLite và Npgsql. Kết quả đọc `DashboardFinanceService`, toàn bộ 20 yêu cầu lịch hẹn và 20 hóa đơn qua `GhiThuHoaDonService` khớp. Các lượt đều rollback; file SQLite nguồn không thay đổi.
- Có **85 giá trị DateTime với chữ số lẻ thứ 7**; PostgreSQL timestamp chỉ giữ microsecond. Vì vậy cột DateTime đích giữ TEXT nguyên bản và converter C# giữ 100 ns. Converter còn khớp cách SQLite .NET 10 chuẩn hóa offset về UTC. Bool giữ integer 0/1, decimal dùng numeric, DateOnly dùng date với nhập ISO nghiêm ngặt. Không sửa các thời điểm nguồn.
- Đã thêm Npgsql EF 10.0.3 và ánh xạ PostgreSQL có điều kiện. **Web vẫn dùng UseSqlite trong Program.cs**, chưa chuyển startup sang PostgreSQL. Không coi việc đọc model thành công là đã chuyển toàn bộ ứng dụng.
- API transaction runtime đã tách khỏi cast SqliteConnection. SQLite vẫn dùng BeginTransaction(deferred) như cũ; PostgreSQL lấy advisory transaction lock chung trước khi đọc dư nợ/trạng thái, dùng ReadCommitted cho writer. Đã chứng minh EF transaction chặn ADO transaction trên kết nối khác, và rollback giải phóng khóa. Quan sát lock theo khóa, không theo ProcessID vì Neon pooler dùng PID proxy.
- Adapter ADO/EF chỉ chuẩn hóa named parameter, alias `Value` và thời điểm raw; không bỏ PRAGMA/constraint. Nhánh PostgreSQL sắp xếp ảnh dùng một UPDATE với unique deferrable; CHECK 1..8 vẫn bật. Cần kiểm thử ghi/commit trên fixture PostgreSQL riêng trước cutover.
- Hồi quy đầu: 545/548 đạt. Hai ca migration vẫn kỳ vọng v40 trong khi updater đã là v42, một ca nút hủy kỳ vọng nhãn cũ. Đã cập nhật đúng kỳ vọng hiện hành, không đổi UI/schema; lượt bổ sung sửa ba ca này và kiểm tra converter/SQL/lỗi/counter đạt 26/26. Tổng hợp kết quả cuối theo tên test qua ba lượt: **561 ca khác nhau Passed, 0 Failed, 0 Skipped**. Kết quả chi tiết TRX ở `data/postgres-port/tests/`. Chưa phải full suite PostgreSQL hoặc nghiệm thu Linux/UI.
- Bản SQL ứng viên có phiên bản để review: `deployment/postgres/v1/schema.candidate.sql`; hướng dẫn và giới hạn đi cùng README. Không chạy trực tiếp vào schema public thử v1 đang có, không dùng làm updater SQLite.

## File và Object Storage

Đã xác minh Neon Object Storage dùng được ở nhánh thử Singapore; tạo bucket **private** `nhatot-private`, hiện chưa upload file hoặc tạo credential S3. Theo [gói Neon hiện tại](https://neon.com/blog/neon-free-plan-1-gb-per-project), Free gồm 5 GB Object Storage/project. Không tạo AI Gateway, Functions tính phí hoặc tài nguyên Render trả phí.

`deployment/Inspect-StoragePort.py` kiểm kê chỉ đọc, không in key/tên file/nội dung. Có 270.694.929 byte (~258 MiB) trên các thư mục local, gồm cả tệp không được DB tham chiếu. **2.889 file đang tham chiếu đều tồn tại**: 2.880 ảnh phòng/thumbnail, 2 ảnh giấy tờ, 2 ảnh báo hỏng, 1 chứng từ, 1 QR và 3 avatar. Không có tài liệu hợp đồng ở nguồn hiện tại. Không tự upload các file mồ côi, đặc biệt 224 ảnh giấy tờ không có tham chiếu. Báo cáo ignored: `data/postgres-port/storage-de9f9dca.json`.

Chưa tích hợp S3 vào các store; các thư mục private còn suy ra từ DataSource SQLite. Cần tách file scope, giữ endpoint kiểm tra quyền/no-store và link 15 phút; không để file hoặc key ring bền vững trên filesystem tạm Render. Tạo key ring online riêng, bảo vệ và lưu bền vững; không sao chép key Windows DPAPI sang Linux rồi coi là dùng được.

## Những phần phải chuyển trước khi public

1. Tạo Neon project Free sau khi kiểm tra gói thực tế. Chọn region gần Render, lấy kết nối TLS vào secret; không đưa connection string ra log/Git. PostgreSQL đích phải mới, không ghi đè dữ liệu có sẵn.
2. Ánh xạ đúng 61 bảng, PK/FK, 51 index (kể cả unique có điều kiện) và 60 trigger. Giữ tên bảng/cột, trạng thái và lịch sử. Chuyển riêng cú pháp SQLite sang PostgreSQL; không coi `EnsureCreated` từ EF model là tương đương schema đầy đủ.
3. Thêm provider PostgreSQL song song SQLite. Thay các cast `SqliteConnection`, lỗi SQLite và SQL đặc thù trong nghiệp vụ. Các transaction `BEGIN IMMEDIATE` hiện dùng cho thu tiền/cọc/chốt hóa đơn/giữ phòng cần cơ chế khóa tương đương ở PostgreSQL (khóa dòng trong transaction, kiểm tra lại dư nợ và xung đột), không được chỉ bỏ cast để code build.
4. Trích dữ liệu bằng backup API trên bản sao nhất quán sau khi dừng app nguồn. Chuyển giữ nguyên ID, số tiền, chuỗi ngày/UTC, NULL, hash mật khẩu và bytes chứng từ; chỉnh sequence đích sau import. Không seed lại account hoặc reset mật khẩu. Đối chiếu số dòng và giá trị từng bảng, integrity/FK/index/trigger trên DB đích; thử rollback và từ chối sửa chứng từ đã khóa.
5. Chuyển file sang lưu trữ bền vững miễn phí, kiểm tra khả năng Object Storage trong tài khoản Neon thực tế. Ảnh giấy tờ/avatar/chứng từ/QR/báo hỏng/tài liệu hợp đồng và Data Protection keys không được nằm trên filesystem tạm Render. Giữ endpoint kiểm tra quyền, `no-store` và link chứng từ 15 phút. Chỉ ảnh phòng có đường dẫn công khai. Những chỗ lấy thư mục từ `GetDbConnection().DataSource` phải tách khỏi tên host PostgreSQL.
6. Tạo Render Web Service với compute Free và Docker cho .NET 10 sau khi xác minh tài khoản. Không dùng Render Postgres Free cho lưu trữ lâu dài vì DB đó hết hạn sau 30 ngày. Render Free ngủ khi không có truy cập; các tác vụ nền thông báo/nhắc hạn không được coi là bảo đảm chạy 24/7.
7. Giữ Brevo HTTPS API đã kiểm tra gửi/nhận. Chuyển key bằng secret hosting, cấu hình người gửi và URL HTTPS thực tế; không gửi lại email thử khi chưa có yêu cầu.
8. Kiểm thử trên PostgreSQL thật: đăng nhập/thu hồi phiên/quyền 403, 2 yêu cầu giữ cùng phòng, 2 khoản thu/cấn cọc đồng thời, audit rollback, bất biến chứng từ, import giữ dữ liệu, file private, mobile và toàn luồng nghiệp vụ. Chỉ public sau khi kiểm tra xong.

## Điều kiện triển khai và giới hạn

- Tài liệu gói: [Neon Free](https://neon.com/pricing), [Render Free](https://render.com/docs/free), [Docker trên Render](https://render.com/docs/docker).
- Không cam kết có URL ngay chỉ bằng đổi connection string. Mã hiện có phụ thuộc SQLite ở nhiều module; bỏ kiểm tra schema/trigger/khóa để app chạy không phải hoàn thành triển khai.
- Máy hiện có Docker CLI nhưng Docker Linux engine chưa chạy. Chưa có PostgreSQL local đang dùng để kiểm chứng; có thể dùng DB nhánh thử trên Neon sau khi kết nối và xác minh Free. Không tự bật dịch vụ tính phí làm môi trường test.
- Bản Railway/Brevo Release sạch vẫn giữ tại `data/online/20261022-193959-6891a421`; chưa thay bằng một bản PostgreSQL chưa kiểm chứng.
