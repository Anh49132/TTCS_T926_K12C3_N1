# S4-07:2 — chi phí sửa chữa

> Cập nhật 15/10/2026: xem [rà soát trước commit](S4-07-02-ra-soat-20261015.md). Đã sửa lỗi cú pháp trigger, bỏ phụ thuộc backup local khỏi fixture và bổ sung nhãn hóa đơn. Báo hỏng/chi phí 98/98, hóa đơn/dịch vụ 164/164 PASS; toàn dự án 853 PASS/18 FAIL cũ/0 SKIP. Các kết quả/giới hạn phía dưới là lịch sử trước đợt rà soát này.

Ngày 14/10/2026. Nhánh `feature/S4-07/02-repair-cost`, HEAD `436428071cafc7122ddc9e2905e09947edbbdb1c`. Thực hiện theo phê duyệt mới: nâng database demo chính và hoàn thiện luồng chi phí; không tự thêm phí vào hóa đơn, không phát hành hóa đơn trên demo, không commit/push/merge.

## Database chính và bảo toàn

- Chính: `D:\DU_AN_KHO\data\s407\demo-20261014-164333-49008c\s407.sqlite`.
- Backup v30 giữ nguyên: `D:\DU_AN_KHO\data\s407-cost\stage1-20261014-01\source-v30.backup.sqlite`.
- Trước nâng: đúng nhánh, thay đổi chỉ thuộc schema giai đoạn 2; không listener5271; tiến trình khác dùng database tại D:\hh, không kết thúc chúng; hai file mở độc quyền chỉ đọc được. Schema30, integrity=ok, FK=0, hash khớp giai đoạn1.
- Updater tự tạo backup nguyên trạng: `s407.sqlite.before-update-d4e25c765e134dbdb0d77c88179eeaab.bak` cạnh file chính. Không ghi đè backup.
- Sau nâng: schema31, 577.536 bytes, integrity=ok, FK=0. Tất cả snapshot dữ liệu bảng cũ (bao gồm sqlite_sequence, ngoại trừ dòng phiên bản mới) không đổi. Báo hỏng4, lịch sử xử lý6, ảnh3, hóa đơn2, chi tiết hóa đơn5. Hai bảng chi phí/lịch sử mới đều0 dòng trên demo chính.
- SHA256 chính trước: `e61a904283f9c47ed0856e54a872c5eead99bcc3f70064e0fa5071a46d2709e5`.
- SHA256 chính sau: `181acc26d6fcd13e452bdb8ac8352836fdc7d49603d22eadf19ce24a1d4186f9`; updater lần2 không đổi hash, kết thúc toàn bộ kiểm thử vẫn đúng hash này.
- SHA256 backup trước/sau: `e873aba7f5fc0d3e0eddb1a0277926de2da20464403396dc7239047ddb512924`.
- Lệnh updater đầu truyền --update-database trước --DatabasePath khiến parser cấu hình không lấy đường dẫn, dừng do database mặc định không tồn tại. Không có ghi dữ liệu. Lệnh thành công dùng biến môi trường DatabasePath tạm, khôi phục biến sau lệnh. Không khởi tạo database mặc định.

Kiểm thử ghi chỉ trên bản sao riêng trong `data/s407-cost/schema-tests/` và `data/s407-cost/acceptance-*/`, không dùng database thử nghiệm giai đoạn1 làm runtime chính, không seed/reset demo hoặc Sprint2. Runtime của launcher hiện tại đã build mới tại `data/s407/runtime`.

## Thành phần và nghiệp vụ

- `Services/BaoHongChiPhiService.cs`: tiền nguyên ASCII digits trong giới hạn Int64, >0; whitelist CHU_NHA/KHACH_THUE; quyền tài khoản đang hoạt động+BAO_HONG WRITE/FULL+phân công tòa được đọc lại trong transaction IMMEDIATE. Chỉ HOAN_THANH. Unique nguồn, khóa gửi và phiên bản chống trùng/ghi đè; lưu chi phí+lịch sử+nhật ký có snapshot người cập nhật trong cùng transaction.
- `ViewModels/BaoHongChiPhiInput.cs`: chỉ nhận tiền, bên chịu, phiên bản, khóa gửi; hợp đồng, người/thời điểm và cờ chờ do backend xác định.
- `Controllers/QuanLyBaoHongController.cs`: POST ChiPhi, role QUAN_LY, quyền ghi, CSRF, lỗi trường; xung đột409, trái quyền403. PRG và toast sẵn có.
- `Services/BaoHongService.cs`, `ViewModels/BaoHongViewModels.cs`: bổ sung dữ liệu chi phí/lịch sử cho chi tiết sau kiểm tra quyền S4-07:1. Chủ nhà xem tòa đang sở hữu, khách xem báo hỏng của mình kể cả sau hết hợp đồng. Không mở thêm endpoint chi phí cho Chủ nhà/Khách; không xuất thông tin tài khoản nhạy cảm.
- `Views/BaoHong/_ChiPhi.cshtml`, `Details.cshtml`, `wwwroot/css/tenant-repairs.css`: tiền vi-VN, bên chịu, nhãn chờ và kỳ dự kiến, lịch sử; form chỉ cho quản lý có quyền ở HOAN_THANH và chưa gắn hóa đơn. Responsive; toast dùng thành phần hiện có.
- `Program.cs`: đăng ký service scoped.
- `Services/HoaDonDichVuService.RepairCosts.cs`: kiểm tra snapshot liên kết đã có, đúng nguồn/hợp đồng/tiền/bên chịu/kỳ, một khoản hiệu lực. Không thêm dòng phí. Gọi khi lưu nháp, phát hành nháp, tạo thay thế; việc phát hành chỉ được gọi trong kiểm thử database riêng.
- `HoaDonDichVuService.Draft.cs`, `Views/HoaDonDichVu/_DraftEditor.cshtml`: khoản có nguồn báo hỏng chỉ xem, từ chối POST sửa/xóa; khoản điều chỉnh không có nguồn giữ nguyên.
- `HoaDonDichVuService.Cancel.cs`, `.Publish.cs`, `.Monthly.cs`: giữ transaction, hủy/thay thế giữ nguồn và snapshot, retry thay thế không tạo thêm; fingerprint bổ sung BaoHongId.
- `Data/BaoHongChiPhiSchema.cs`, `DichVuSchemaInitializer.cs`: bổ sung tích hợp khi module hóa đơn được cài bằng lệnh explicit sau v31 không có hóa đơn. Cài guard, thêm khóa nguồn và view trạng thái trong cùng transaction của installer. Không chạy ở web startup và không sửa schema demo đã có guard. Updater28–30 giữ nguyên.
- Models, AppDbContext, DatabaseUpdates và schema v31 giai đoạn2 được giữ. Chi tiết CHECK/FK/index/UNIQUE xem `S4-07-02-schema-v31.md` (tài liệu giai đoạn2 lịch sử).

Kỳ dự kiến bắt đầu tháng sau lần ghi nhận chi phí đầu tiên (ngay_tao UTC+7), xét các kỳ của hợp đồng gốc. Bỏ tháng đã phát hành; NHAP có thể phù hợp. Hợp đồng kết thúc, hết khoảng thuê hoặc không có kỳ phù hợp: vẫn lưu chi phí khách chịu và cờ chờ, kỳNULL. Sửa trước liên kết có thể đổi số tiền/bên chịu; không tạo tổng phí thứ hai. Khi đã có snapshot hóa đơn bất kỳ, kể cả bản hủy, không tự mở khóa/requeue. Hủy và thay thế lưu hai snapshot lịch sử nhưng tối đa một bản hiệu lực. Không tạo khoản thu mới hoặc tính tổng vào hóa đơn tự động.

## AC01–AC14

| AC | Đáp ứng / bằng chứng |
|---|---|
| AC01 | PO chốt HOAN_THANH, TU_CHOI cấm, sửa trước liên kết; service + trigger + test trạng thái/khóa. |
| AC02 | Tháng sau ghi nhận, hợp đồng gốc, nháp phù hợp, một khoản hiệu lực; test kỳ/đồng thời/hủy/thay thế. |
| AC03 | ChiPhiBaoHong và SQL view trạng thái của v31. |
| AC04 | Partial _ChiPhi trong màn hình Details dùng chung. |
| AC05 | Form tiền/bên chịu và POST ChiPhi cho quản lý được phân công. |
| AC06 | Required, chữ số nguyên, ParseAmount/whitelist, CHECK; test null/âm/0/lẻ/vượtInt64/payer giả. |
| AC07 | FK BaoHongId, hợp đồng gốc; người/thời điểm trên nguồn và lịch sử; audit cùng transaction. |
| AC08 | Chi tiết của ba vai trò, kiểm tra quyền cũ trước load; service và HTTP test đọc lại. |
| AC09 | KHACH_THUE → ChoThuKhach=true, view CHO_DUA_VAO_HOA_DON; không tự thêm dòng. |
| AC10 | CHU_NHA → ChoThuKhach=false, kỳNULL, nhãn không thu khách; test service/HTTP. |
| AC11 | Nhãn “Chờ đưa vào hóa đơn kỳ sau” và kỳ/thông báo chưa tìm được kỳ phù hợp. |
| AC12 | UNIQUE nguồn/khóa, transaction/version, index/trigger bảo vệ dòng hóa đơn và kiểm tra service; test retry/đồng thời/thay thế. |
| AC13 | Role, quyền module, tài khoản và phân công kiểm tra backend; CSRF; chủ nhà/khách POST403. |
| AC14 | Matrix schema/service/HTTP phía dưới; kịch bản350000 hai bên chịu và đọc lại đã chạy trên bản sao. |

## Kiểm thử và nghiệm thu

`BaoHongChiPhiSchemaTests.cs` và `BaoHongChiPhiServiceTests.cs`: nâng/rerun, CHECK/FK/unique, tiền/payer/trạng thái, quyền đọc/ghi và phân công, chính xác nguồn/snapshot, kỳ đã phát hành/nháp/hết hạn, lưu/đọc/sửa, gửi trùng/nội dung key khác, stale, đồng thời, rollback khi audit lỗi, lịch sử bất biến, NHAP/phát hành/hủy/thay thế/retry. Test service hóa đơn dùng bản sao và không gửi email thực tế.

Các fixture hồi quy mô phỏng schema cũ (`SchemaTestFixture.cs`, `BaoHongTests.cs`, `BaoHongTrackingTests.cs`) loại bảng v31 rỗng trước khi hạ marker trên database giả, và kỳ vọng phiên bản cuối31. Không chạy thao tác hạ schema trên database demo.

- Build Debug/runtime thành công, không lỗi. Cảnh báo NU1900 (không truy cập NuGet vulnerability feed), license ImageSharp, CS8601 hiện có tùy lần biên dịch.
- HTTP thực tế trên cổng5282/bản sao: lưu350000 khách chịu; retry không tạo trùng; tiềnlẻ từ chối; CSRF400; chủ nhà/khách xem đúng và POST403; đổi chủ nhà không thu khách; không tạo dòng hóa đơn. Assertion HTML đầu chưa decode entities được sửa trong script kiểm chứng, không sửa dữ liệu demo.
- UI thực tế viewport360x800 và1366x900: tiền/bên chịu/nhãn/kỳ/toast đúng; scrollWidth345<=360 và1351<=1366. Ảnh trong data/s407-cost/stage2-results/cost-mobile.jpg và cost-desktop.jpg. Ảnh báo hỏng trên bản sao không được copy nên không nghiệm thu ảnh tại server này; test ảnh S4-07:1 vẫn nằm trong bộ hồi quy.
- Lần VSTest sandbox không kết nối testhost, aborted; chạy lại ngoài sandbox.
- Bộ báo hỏng/chi phí cuối: **95/95 đạt**, không skip; bao gồm service giữ nguồn trong vòng đời nháp→phát hành→hủy→thay thế, retry và khóa chi phí. Bằng chứng `data/s407-cost/stage2-results/repair-final.trx`.
- Hồi quy hóa đơn/dịch vụ: **162/164 đạt** trong lần chạy chung259 ca (`cost-final.trx`). Hai ca còn lỗi nhãn UI: `MonthlyAllAmountsSnapshotSurvivesChanges` thiếu “Cố định theo phòng”, `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` thiếu “2 người”. Các trang `Details.cshtml`/`_CollectionDetails.cshtml` của hóa đơn không thay đổi trong task này; chưa chạy baseline riêng để khẳng định thời điểm xuất hiện lỗi. Không sửa nhãn ngoài phạm vi chi phí.
- Lần chạy chung trên còn1 lỗi fixture mới thiếu principal cho audit hóa đơn; đã sửa fixture truyền người thực hiện Chủ nhà và chạy lại toàn bộ95 ca báo hỏng/chi phí đạt. Không bỏ cơ chế audit của production. Không tuyên bố toàn bộ hồi quy đều xanh.
- `--check-database` bằng runtime cuối đạt; git diff --check đạt; không có database/credential được theo dõi trong Git. Runtime demo đã cập nhật, không khởi động demo chính tự động.

Nghiệm thu chính: chạy `.\verification\Start-S407Demo.ps1` (runtime mới, database chính v31). Quản lý mở báo hỏng0001 hoặc0002 HOAN_THANH, nhập350000, chọn Khách thuê, lưu; kiểm tra nhãn/toast/lịch sử và đọc lại bằng Chủ nhà/Khách. Trước khi gắn hóa đơn, sửa sang Chủ nhà chịu và kiểm tra không chờ thu khách. 0003 TU_CHOI và0004 DANG_XU_LY không có form chi phí. Không có dữ liệu chi phí demo được lưu tự động trong phiên triển khai.

Không khởi động lại demo chính tự động. Database/thông tin đăng nhập/backup/kết quả và ảnh test đều ignored. HEAD và nhánh giữ nguyên, toàn bộ thay đổi chưa commit.

## Hoàn thiện RC-01 trước commit

Database v31 hợp lệ có thể chưa cài module hóa đơn. Trước sửa, service truy vấn `chi_tiet_hoa_don` để khóa phí và `hoa_don` để loại kỳ đã phát hành mà không kiểm tra module. Cả Chủ nhà/Khách thuê chịu phí đều lỗi. Hai case mới trong `BaoHongChiPhiOptionalInvoiceTests` đã FAIL trước sửa với đúng lỗi `no such table: chi_tiet_hoa_don`.

`BaoHongChiPhiService.HasInvoiceModuleAsync` đọc sqlite_master trong transaction ghi hiện có: 0 bảng nghĩa là module chưa cài; 2 bảng giữ đầy đủ kiểm tra khóa và kỳ đã phát hành; 1 bảng là schema không hoàn chỉnh và bị từ chối. Không tạo bảng hóa đơn, không catch lỗi SQL để bỏ qua bảo vệ. Khi module chưa cài, không có kỳ đã phát hành để loại; kỳ vẫn tính từ tháng sau ghi nhận theo kỳ thuê/hợp đồng gốc. Quyền, trạng thái, phiên bản, unique, lịch sử và audit cùng transaction giữ nguyên.

Test mới tự tạo database giả riêng bằng initializer dự án và cài module yêu cầu thuê để hợp đồng hợp lệ, không phụ thuộc database demo/backup. Kiểm tra cả hai bên chịu: lưu350000, replay, stale, sửa250000/đổi bên chịu, đọc lại ba vai trò, hai lịch sử và audit, kỳ11/2026 hoặcNULL, và không có bảng hóa đơn sau test. Các test schema giai đoạn2 vẫn cần backup local v30 được ignore; đây là điều kiện chạy local đã ghi nhận, chưa xác nhận checkout CI mới có fixture này.

Trong đợt RC-01 chỉ sửa service trên, thêm `Tests/QL_PhongTro.Tests/BaoHongChiPhiOptionalInvoiceTests.cs`, cập nhật tài liệu này và tiến độ. Giữ toàn bộ thay đổi S4-07:2 trước đó. Không sửa hai lỗi nhãn hóa đơn hoặc20testFAIL cũ.

| Kiểm thử RC-01 và hồi quy | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| Hai ca RC-01 trước sửa |0|2|0|
| Hai ca RC-01 sau sửa, chạy riêng |2|0|0|
| Báo hỏng/chi phí và S4-07:1, gồm hai ca mới |97|0|0|
| Hóa đơn/dịch vụ |162|2|0|
| Tổng bộ liên quan sau sửa |259|2|0|

Hai FAIL sau sửa đúng các test nhãn hóa đơn đã được chứng minh FAIL trên baseline4364280. Không phát hiện FAIL mới trong bộ261ca đã chạy. Không chạy lại toàn bộ870ca trong đợt sửa tối thiểu này; kết quả audit toàn dự án trước sửa là848PASS/20FAIL/0SKIP trên868ca,20FAIL đều tái hiện trên baseline. Không tuyên bố full suite xanh.

Build ứng dụng và build toàn solution `QL_PhongTro/QL_PhongTro.slnx` cuối đều đạt0lỗi. Lần build solution trong lúc testhost chạy gặp khóa DLL/MSB3027; chờ test kết thúc và build lại tuần tự đạt. Cảnh báo cũ NU1900/ImageSharp; CS8601 khi biên dịch lại. Không đổi code để bỏ qua warning hoặc khóa file.

HTTP chạy ở5285 trên bản sao có hóa đơn và5286 trên bản sao không có hóa đơn: lưu350000/đổi bên chịu/đọc lại/replay/CSRF400 đều đạt, kỳ11/2026 với khách vàNULL với chủ; môi trường5286 vẫn0bảng hóa đơn sau lưu. Browser5286 hiển thị đúng nhãn chờ và kỳ; sửa250000Chủ chịu lưu thành công. Ảnh và TRX tại `data/s407-cost/rc01-20fb5786182d43cdb1468eb9e4ce2eec/`, ignored. Test quyền/CSRF/validation/concurrency/nháp/phát hành/hủy/thay thế/rollback nằm trong bộ97/164; không đưa phí/phát hành tự động trên demo. Hai server bản sao đã dừng.

Database demo chính đầu/cuối cùngSHA256 `36309eefe07d5c774e7d6016fcc63deca65ac46be93e55667a536fc271196697`; backup v30 đầu/cuối `e873aba7f5fc0d3e0eddb1a0277926de2da20464403396dc7239047ddb512924`. Bytes/schema/snapshot mọi bảng không đổi; integrity/FK đạt ở cả hai. Không chạy updater trên database chính, không tạo/xóa/reset dữ liệu demo. Runtime kiểm thử xây riêng dưới thư mụcRC01; không cần nâng schema để dùng bản sửa service.

**Kết luận:** RC-01 đã xử lý; phạm vi chức năng S4-07:2 sẵn sàng đề xuất commit sau người dùng duyệt danh sách file. Chưa stage/commit/push. Điều kiện kiểm chứng cần giữ trong bàn giao: full suite còn20FAIL cũ và fixture schema có invoices cần backup localv30 đãignore; chưa xác nhận CI fresh checkout tự chạy được bộfixture này. Không đưa backup vàoGit để đáp ứng điều kiện test. AC01–AC14 dùng matrix và bằng chứng ở phần trên, cộng hai ca module optional này.

### File dự kiến đưa vào commit

- `QL_PhongTro/Controllers/QuanLyBaoHongController.cs`
- `QL_PhongTro/Data/AppDbContext.cs`
- `QL_PhongTro/Data/DatabaseUpdates.cs`
- `QL_PhongTro/Data/DichVuSchemaInitializer.cs`
- `QL_PhongTro/Program.cs`
- `QL_PhongTro/Services/BaoHongService.cs`
- `QL_PhongTro/Services/HoaDonDichVuService.Cancel.cs`
- `QL_PhongTro/Services/HoaDonDichVuService.Draft.cs`
- `QL_PhongTro/Services/HoaDonDichVuService.Monthly.cs`
- `QL_PhongTro/Services/HoaDonDichVuService.Publish.cs`
- `QL_PhongTro/ViewModels/BaoHongViewModels.cs`
- `QL_PhongTro/Views/BaoHong/Details.cshtml`
- `QL_PhongTro/Views/HoaDonDichVu/_DraftEditor.cshtml`
- `QL_PhongTro/wwwroot/css/tenant-repairs.css`
- `Tests/QL_PhongTro.Tests/BaoHongTests.cs`
- `Tests/QL_PhongTro.Tests/BaoHongTrackingTests.cs`
- `Tests/QL_PhongTro.Tests/SchemaTestFixture.cs`
- `tien-do.md`
- `QL_PhongTro/Data/BaoHongChiPhiSchema.cs`
- `QL_PhongTro/Models/ChiPhiBaoHong.cs`
- `QL_PhongTro/Models/LichSuChiPhiBaoHong.cs`
- `QL_PhongTro/Services/BaoHongChiPhiService.cs`
- `QL_PhongTro/Services/HoaDonDichVuService.RepairCosts.cs`
- `QL_PhongTro/ViewModels/BaoHongChiPhiInput.cs`
- `QL_PhongTro/Views/BaoHong/_ChiPhi.cshtml`
- `QL_PhongTro/wwwroot/js/repair-cost.js`
- `Tests/QL_PhongTro.Tests/BaoHongChiPhiOptionalInvoiceTests.cs`
- `Tests/QL_PhongTro.Tests/BaoHongChiPhiSchemaTests.cs`
- `Tests/QL_PhongTro.Tests/BaoHongChiPhiServiceTests.cs`
- `docs/S4-07-02-chi-phi.md`
- `docs/S4-07-02-schema-v31.md`
- `docs/S4-07-02-xu-ly-loi.md`

Không đưa data/, database, backup, access.json, tài khoản test, runtime, TRX, ảnh test hoặc script tạm vào Git. Các fixture sửa trong S4-07:2 chỉ cập nhật mô phỏng schema cũ; hai lỗi hóa đơn cũ và20FAIL ngoài phạm vi không bị sửa.
