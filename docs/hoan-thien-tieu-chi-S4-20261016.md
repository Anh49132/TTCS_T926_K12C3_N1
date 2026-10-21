# Hoàn thiện tiêu chí S4-01 đến S4-09 — 16/10/2026

> Cập nhật sau khi người dùng báo `run.bat` lỗi schema: đã sao lưu DB local ra ngoài repository, nâng chính `QL_PhongTro/Data/local-dev.sqlite` từ v27 lên **v37**, đối chiếu giữ nguyên 3.922 dòng / 40 bảng cũ, integrity/FK đạt. `run.bat --check-only` đã PASS. Các ghi chú giữ local v27 bên dưới mô tả trạng thái trước lần xử lý này. Backup: `%TEMP%/NhaTot-database-backups/20261016-114109/local-dev.before-v37.sqlite`; biên bản kiểm tra: `data/local-upgrade-20261016-114109/result.json` (ignored).

Phạm vi theo tệp tiêu chí người dùng gửi. Tài liệu [kiểm tra ban đầu](kiem-tra-tieu-chi-S4-20261016.md) là ảnh chụp hiện trạng trước sửa; các lỗi trong đó đã được xử lý như bên dưới.

## Thay đổi

- **Schema v37:** hòa giải hai lịch sử v35/v36 của báo cáo, báo thanh toán, chứng từ và nhật ký email. Giữ nguyên các bước cũ; thêm bước mới, cùng transaction, kiểm tra integrity/FK trước khi ghi marker. Giữ backup tự động và tính lặp an toàn. Cài module hóa đơn sau này cũng thêm đủ các cột/index tương ứng.
- **Báo trả và xác nhận thu:** trang hóa đơn cũ có ô ảnh và multipart như trang chi tiết mới. Chủ nhà xác nhận đúng bản ghi chờ của khách, giữ nguyên số tiền/ngày/hình thức/ảnh, thêm người xác nhận; kiểm tra lại dư nợ sau khi giữ khóa ghi SQLite. Gửi lặp không cộng tiền hai lần. Xác nhận cạnh tranh với hủy được tuần tự hóa. Gửi/hủy/xác nhận ghi audit cùng transaction; lỗi audit rollback nghiệp vụ. Giao diện hiển thị kết quả xác nhận/lỗi.
- **Công nợ:** dùng cùng truy vấn cho trang và CSV, lọc tòa theo chủ nhà, chặn vai trò khách/quản lý và tòa ngoài phạm vi. Nối tên qua hồ sơ `khach_thue`; gộp theo phòng, giữ tên của các hợp đồng còn nợ, trừ cấn cọc đã xác nhận. Ngày quá hạn lấy từ `ITimeProvider` theo Việt Nam. CSV UTF-8 BOM có tổng tập lọc và bảo vệ tên có ký tự công thức bảng tính.
- **Thông báo:** cả email phát hành và nhắc hạn có kỳ, tổng tiền, còn phải trả, hạn và liên kết. Tác vụ nhắc hạn dùng transaction ghi để chống tạo trùng khi chạy đồng thời; không yêu cầu bảng cọc khi module này chưa cài. Giữ lịch 08:00 Việt Nam, một lần theo mốc và chính sách 1 lần đầu + 3 thử lại.
- **360px:** chỉnh header nhân viên để tên tài khoản dài không gây tràn ngang; bảng dữ liệu lớn vẫn cuộn bên trong bảng.

## Đối chiếu từng tiêu chí

AC đánh số theo thứ tự dấu đầu dòng trong tệp người dùng.

| Story | AC đã đối chiếu | Bằng chứng |
| --- | --- | --- |
| S4-01 | 1. Form tiền/ngày/hình thức/ảnh; 2. >0 và ≤ dư nợ; 3. ảnh ≤5 MiB, private, chủ nhà sở hữu/ADMIN, liên kết 15 phút; 4. chờ xác nhận, không sửa, chỉ hủy khi còn chờ | `InvoicePaymentReportTests`, `RoomServicesTests.PaymentProof`, kiểm thử trình duyệt gửi ảnh PNG và chủ nhà xem/xác nhận |
| S4-02 | 1. Nhiều lần thu, đủ metadata; 2. trả thiếu/đủ và chặn vượt; 3. cập nhật ngay; 4. hủy có lý do, giữ lịch sử và tính lại nợ | `InvoiceCollectionTests`, `InvoiceCollectionCancellationTests`; các ca xác nhận báo trả, replay, sai sở hữu, dư nợ đổi, audit rollback và chạy đồng thời trong `RoomServicesTests.Sprint4Completion` |
| S4-03 | 1. Phòng/người/số HĐ/dư nợ/ngày quá hạn; 2. lọc tòa/ngày/tiền; 3. tổng tập lọc; 4. ≥5 trả thiếu + 2 quá hạn nhiều kỳ; 5. CSV UTF-8 | Fixture mới có 5 phòng, 7 hóa đơn còn nợ, hai phòng có hai kỳ quá hạn, thêm cấn cọc và khoản hủy; tổng 2.450.000 đ, hai phòng nhiều kỳ tổng 1.250.000 đ, tập lọc 900.000 đ. Kiểm tra số liệu, HTTP, quyền và bytes CSV |
| S4-04 | 1. Ngày/chỉ số cuối; 2. hóa đơn kỳ cuối chia ngày + điện nước; 3. cọc/khấu trừ/nợ/hoàn hoặc thu thêm; 4. lý do/phạt trước hạn; 5. kết thúc/chặn hóa đơn mới/PDF cho khách | Test TraPhong/TatToan/ChamDut/XacNhan/Pdf; chạy trình duyệt thêm/sửa/xóa khấu trừ, chốt thật, tải PDF hai vai trò và 403 cho người ngoài |
| S4-05 | 1. Kết thúc và phòng trống cùng transaction; 2. nhân bản nháp, giữ ảnh/mô tả và giá hiện tại; 3. một nút đăng <1 phút; 4. dashboard phòng chưa đăng | Test VacantRooms/AutoDraftListing/DraftListingPublish; trình duyệt đăng trong **0,63 giây**, phòng biến khỏi dashboard/danh sách sau đăng, nội dung công khai đúng |
| S4-06 | 1. Thiết bị/mô tả/mức/3 ảnh ×5 MiB; 2. mã theo tháng và trạng thái mới; 3. đúng phòng/hợp đồng hiệu lực; 4. theo dõi trạng thái/phản hồi | Nhóm `BaoHongTests` và kiểm thử trình duyệt khách gửi báo Gấp, theo dõi đến Hoàn thành ở 360px |
| S4-07 | 1. Chuỗi trạng thái và ghi chú; 2. chi phí/bên chịu/đánh dấu kỳ sau; 3. Gấp >24h; 4. lọc tòa/trạng thái/mức | Test tracking/cost/overdue/filter, quyền/phân công/rollback; trình duyệt quản lý chuyển hai bước, ghi 350.000 đ khách chịu, khách thấy kỳ dự kiến 11/2026 |
| S4-08 | 1. Phát hành/trước hạn 2 ngày/quá hạn; 2. đủ nội dung; 3. 08:00 VN, không trùng; 4. tối đa 3 retry và log cho admin; 5. chuông/chưa đọc/đánh dấu đọc | Test lịch và retry; ca mới chạy tác vụ thật hai lần, kiểm tra nội dung, chạy dispatcher với sender lỗi đủ bốn lần rồi dừng, kiểm tra log và API số chưa đọc/đánh dấu đọc |
| S4-09 | 1. Tổng phát hành/thu/còn thu/tỷ lệ; 2. lấp đầy cuối tháng; 3. lọc ≤12 tháng; 4. biểu đồ 6 tháng khớp DB; 5. <3 giây với 500 phòng ×12 tháng | `BaoCaoDoanhThuTests`, gồm benchmark service và HTTP render, kiểm tra cấn cọc không làm tăng doanh thu tiền mặt, lịch sử phòng và dữ liệu không phát hành |

## CSDL và kiểm chứng nâng cấp

Chỉ nâng **bản sao mới** của DB local. Script tái lập:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore
python verification/Verify-Sprint4Upgrade.py QL_PhongTro/Data/local-dev.sqlite data/<thu-muc-moi>/copy.sqlite
```

Kết quả tại `data/s4-fix/upgrade/upgrade-result.json`: **v27 → v37**, bảo toàn toàn bộ giá trị của **3.922 dòng / 40 bảng cũ** (không tính marker phiên bản); `integrity_check=ok`, FK lỗi=0; updater lần hai giữ nguyên bytes DB. Hash DB gốc không đổi. Có thêm test bản sao nhánh v35 và v36 thiếu từng phần.

DB local gốc vẫn ở **v27**. Không tự nâng DB đang dùng, không đổi tài khoản/mật khẩu, không commit DB/backup/credential. Khi triển khai cần dừng app, sao lưu và chạy updater với đúng `DatabasePath`; bước nâng cấp đã được thử trên bản sao như trên.

## Kiểm thử và giới hạn

- Build Debug/Razor: đạt; còn cảnh báo license ImageSharp và CS8601 đã có từ trước.
- Các ca tích hợp/HTTP/schema mới và migration liên quan: **13/13 đạt**; fixture công nợ hoàn thiện được chạy riêng lại sau khi bổ sung hai trường hợp quá hạn nhiều kỳ.
- Ca HTTP bổ sung xác nhận thu: **1/1 đạt**, chặn chủ nhà khác/khách/quản lý/ADMIN và quyền tài chính đã bị thu hồi; kiểm tra xác nhận hợp lệ cùng thông báo thành công trên UI.
- `BaoCaoDoanhThuTests`: **14/14 đạt**, bao gồm benchmark 500 phòng/12 tháng với ngưỡng <3 giây cho service và request HTTP đã warm-up.
- Hồi quy toàn dự án: **1.127 PASS / 2 FAIL / 0 SKIP trên 1.129 ca**, 18 phút 42 giây. Hai lỗi đều là assertion phiên bản cũ ở `LichHenMigrationTests`; đã cập nhật sang v37 và chạy lại trong nhóm 13/13 ở trên. Tổng hợp kết quả cuối từ full run và các lượt chạy lại/bổ sung: **1.130 ca khác nhau, toàn bộ PASS**, không còn lỗi chưa xử lý trong tập đã chạy. Đây là tổng hợp theo từng ca, không phải một lượt full suite xanh duy nhất. Bằng chứng và cách gộp tại `data/s4-fix/final-test-summary.json`, cùng bốn TRX `s4-full`, `s4-final-targeted`, `s4-debt-fixture`, `s4-confirm-rights` (ignored).
- Sau hồi quy, đối chiếu lại toàn bộ schema/dòng của DB local với bản backup trước nâng cấp trên bản sao: khớp hoàn toàn, local vẫn v27. Không so hash file SQLite giữa hai bản backup vì backup có thể khác cấu trúc trang/header dù dữ liệu logic giống nhau.
- Trình duyệt Edge headless: 360px và desktop; gửi ảnh/xem ảnh/xác nhận thu/cập nhật công nợ, khách báo hỏng/quản lý hoàn thành/chi phí kỳ sau, chốt trả phòng/tin nháp/đăng/dashboard và tải PDF. Ảnh và JSON kiểm tra trong các thư mục `browser-check-*` của hai demo riêng.
- Chưa thử Safari/iOS, thiết bị cảm ứng vật lý, SMTP/inbox thật và mạng 3G thật. Kiểm thử email dùng sender giả lập lỗi và pickup; không coi đây là nghiệm thu SMTP thật. Chưa có review thành viên khác hoặc merge/commit/push trong task này.

Lệnh test chính:

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --logger "trx;LogFileName=s4-full.trx" --results-directory data/s4-fix
```

Để kiểm tra UI, runner `verification/S404Browser` có mode `s4` cho fixture riêng với khóa credential `Owner`, `Tenant`, `Manager`, và các mode có sẵn `s4054`, `task5` cho tất toán/đăng/PDF. Chỉ chạy trên dữ liệu giả; runner thực hiện thay đổi nghiệp vụ thật trong demo. Có thể cần cấu hình EventLog tắt và `DataProtectionKeysPath` riêng khi sandbox Windows không cho ghi EventLog/key mặc định.
