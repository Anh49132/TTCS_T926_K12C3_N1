# Hòa giải S4-07:01–04 với dev — schema v34

Ngày 15/10/2026. Worktree riêng trên nhánh `codex/s407-dev-reconcile`, gộp dev `edef5eafea48f06e20800bd94994203331dd117c` và S4-07:04 `43f89984b50821cefadbed7afbd8d95df0dd9453`. Không sửa nhánh nguồn, không push hoặc nâng DB local/demo thật.

## Giải quyết xung đột

Giữ cả DbSet/mapping chi phí báo hỏng và cọc/tất toán/chấm dứt/xác nhận/lịch sử phòng. Bộ khởi tạo hóa đơn giữ guard cọc, hợp đồng kết thúc và nguồn phí báo hỏng. Chọn cài guard chi phí bằng sự tồn tại thực tế của module, vì v31 từng được hai nhánh dùng cho hai nội dung khác nhau. Guard nghiệp vụ hóa đơn gộp tự động đã được đọc lại: bảo vệ snapshot báo hỏng cùng tồn tại với chặn sửa kỳ cuối, chặn phát hành sau kết thúc và bảo vệ hóa đơn của tất toán đã chốt.

Fixture kiểm thử giữ cả hai nhóm helper tháo extension; test giả lập schema cũ tháo các module về đúng hình dạng nguồn rồi mới giảm dấu phiên bản. Kỳ vọng phiên bản cuối trong các test nâng cấp liên quan chuyển sang34; không đổi test nghiệp vụ để làm xanh. `tien-do.md` giữ cả lịch sử S4-04 và S4-07.

## Vì sao cần v34

Dev đã dùng v31 cho `TatToanSchema`, v32 cho `ChamDutHopDongSchema`, v33 cho `XacNhanTatToanSchema`. Feature đã dùng v31 cho `BaoHongChiPhiSchema`. Không đổi/xóa các dấu phiên bản hoặc dữ liệu đã phát hành trên hai nhánh.

`BranchSchemaReconciliation` nhận diện cả nhóm bảng/view thực tế, từ chối nhóm thiếu/không xác định và kiểm tra module có sẵn trước khi cài bổ sung. CSDL v31 chi phí được bổ sung nền tất toán trước các bước32–33. CSDL v31–33 tất toán được bổ sung chi phí ở bước34. Hai installer v31 giữ đường gọi cũ và cùng DDL/validation; thêm đường gọi nội bộ không ghi lại marker31 để tránh trùng lịch sử. Dấu34 chỉ được ghi sau khi cả hai module và integrity/FK hợp lệ. Updater vẫn sao lưu trước bước thay đổi; chạy lại ở34 chỉ kiểm tra, không ghi đè bảng/dữ liệu. Như updater hiện có, các bước có transaction riêng; lỗi phải giữ backup và trạng thái đã áp dụng để rà soát/rerun, không tự phục hồi đè dữ liệu.

Hỗ trợ nguồn v30, v31 chi phí, v31/v32/v33 tất toán. Sau nâng phải có cả chi phí báo hỏng và tất toán. Không tự tạo bản ghi nghiệp vụ, không tự đưa phí vào hóa đơn và không tự chốt tất toán.

## Kiểm chứng

- Build C#/Razor/test: đạt,0lỗi; cảnh báo ImageSharp license và CS8601 có sẵn.
- 6/6 test hòa giải: năm hình dạng nguồn trên + từ chối schema thiếu. So sánh mọi dòng/cột cũ và marker lịch sử, có dữ liệu chi phí/lịch sử hoặc cọc, integrity/FK, validate cả hai nhóm schema, chạy updater lần hai không thêm backup hoặc đổi dữ liệu.
- Hồi quy báo hỏng + hóa đơn/dịch vụ + trả phòng/tất toán:382ca,381PASS/1FAIL/0SKIP. Ca `CollectionCancellationHttpValidatesRightsCsrfAndPersistsCanceledHistory` lỗi500 ở bước Login trước nghiệp vụ; chạy riêng lại cùng binary đạt1/1, không đổi assertion. Không coi lượt đầu là xanh.
- Nhóm còn lại609ca:589PASS/20FAIL/0SKIP. Trong20FAIL,18ca trùng baseline; hai ca `TenantHomeInvoicesExcludeCancelledButKeepDebtAfterContractEnds` và `UsageInvalidDraftForeignHistoryAndHandoverNotCounted` tạo dữ liệu hóa đơn sau khi hợp đồng đã kết thúc, bị guard v33 của dev từ chối. Đã sửa riêng thứ tự chuẩn bị dữ liệu: hóa đơn lịch sử tạo khi hợp đồng còn hiệu lực, sau đó mới kết thúc; giữ nguyên assertion nghiệp vụ và toàn bộ guard.
- Rebuild sau hai chỉnh sửa fixture, chạy lại hai ca + nhóm hóa đơn trang chủ/guard xác nhận tất toán/hòa giải:52/52PASS. Không sửa mã production sau lượt hồi quy382ca và nhóm609ca.
- Hai nhóm đầu phủ991ca khác nhau, không trùng lặp:970PASS/21FAIL ban đầu. Tổng hợp kết quả mới nhất của từng ca sau các lượt chạy lại:973PASS/18FAIL/0SKIP;18FAIL trùng tên hoàn toàn baseline43f8998, không còn ca lỗi mới. Đây là tổng hợp nhiều lượt, không phải một full suite xanh; lỗi Login500 từng xuất hiện vẫn cần theo dõi, chưa xác định nguyên nhân gốc. Danh sách từng ca/so sánh có trong final-comparison.json.
- Không chạy lại các ca đã đạt khi chỉ sửa hai fixture độc lập; nhóm52ca kiểm tra trực tiếp hai sửa đổi và các ràng buộc liên quan.
- Không tạo/chụp ảnh. Safari chưa kiểm chứng; lượt này không nghiệm thu UI mới.

Bằng chứng ignored tại `D:/DU_AN_KHO/data/s407-integration/reconcile/`: build.log, reconciliation.trx, integration-regression.trx, login-recheck.trx, integration-remaining.trx, integration-fixture-final.trx và final-comparison.json.

```powershell
dotnet build Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj -p:UseSharedCompilation=false
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~BaoHong|FullyQualifiedName~RoomServicesTests|FullyQualifiedName~TraPhong'
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName!~BaoHong&FullyQualifiedName!~RoomServicesTests&FullyQualifiedName!~TraPhong'
```

Không chạy updater trên DB đang dùng. Khi triển khai thật phải dừng app, giữ backup ngoài repo, kiểm thử trên bản sao đúng DB nguồn trước. Việc cập nhật DB thật/push/merge dev chưa được thực hiện.

## Trạng thái bàn giao

Cả7file đã đánh dấu giải quyết xung đột, không còn unmerged entry hoặc conflict marker. Thay đổi nằm trong worktree riêng, merge chưa commit; nhánh feature43f8998 và devedef5ea trên GitHub giữ nguyên. Không stage project test, database/backup/credential/data ignored. CSDL nguồn và backup S4-07 giữ SHA256 đã ghi trong báo cáo slice04.

## Cập nhật theo yêu cầu người dùng

Toàn bộ cây mã đã được chuyển nguyên vẹn về merge trên nhánh `feature/S4-07/04-repair-request-filters`. Người dùng yêu cầu sửa cả18lỗi baseline và chỉ push nhánh này. Kết quả/triển khai tiếp theo tại [S4-07-04-tich-hop-va-sua-18-loi.md](S4-07-04-tich-hop-va-sua-18-loi.md); ghi chú chưa chuyển/chưa sửa ở các mục trên là lịch sử lượt kiểm tra đầu.
