# Báo cáo xử lý lỗi S4-07:2 — 14/10/2026

Nhánh `feature/S4-07/02-repair-cost`, HEAD `436428071cafc7122ddc9e2905e09947edbbdb1c`. Giữ toàn bộ thay đổi triển khai trước phiên; không checkout/commit/push/merge/rebase, không tác động dev.

## Lỗi01 — hai test hóa đơn

**Nguyên nhân:** `QL_PhongTro/Views/HoaDonDichVu/Details.cshtml:17–19` chọn `_CollectionDetails` khi Chủ nhà xem hóa đơn không còn NHAP và đã có dữ liệu thanh toán. Nhãn “Cố định theo phòng” ở nhánh giao diện còn lại (`Details.cshtml:44`) không được render. `_CollectionDetails.cshtml:35` chỉ in `SoLuong` dạng số, không in đơn vị/người hoặc cách tính; vì vậy không có “2 người”. Dữ liệu tiền/số người vẫn vượt qua các assertion trước đó.

**Đối chiếu trước S4-07:2:** dùng `git archive 4364280` xuất mã đã commit vào thư mục ignored riêng `data/s407-cost/bugfix-baseline-4364280`, restore offline từ package cache, chạy nguyên hai test. Không checkout hoặc hoàn tác file ở workspace. Hai test cùng FAIL với chính nhãn bị thiếu trên baseline và code hiện tại:

- `RoomServicesTests.MonthlyAllAmountsSnapshotSurvivesChanges`, `Tests/QL_PhongTro.Tests/RoomServicesTests.MonthlySupplemental.cs:129`: thiếu “Cố định theo phòng”.
- `RoomServicesTests.DepartureHttpWarningSnapshotsAndFutureUnissuedBill`, `Tests/QL_PhongTro.Tests/RoomServicesTests.DepartureBilling.cs:35`: thiếu “2 người”.

Bằng chứng: `data/s407-cost/bugfix-results/bug01-before.trx` và `bug01-baseline.trx`, mỗi lần0 PASS/2 FAIL/0 SKIP. Hai Razor View này không thay đổi so với HEAD; history ghi nhận `_CollectionDetails` từ các commit module ghi thu trước đó.

**Xử lý:** đây là lỗi UI có trước S4-07:2; theo giới hạn người dùng, không sửa chức năng hóa đơn cũ hoặc sửa test. Đề xuất PO phê duyệt bổ sung nhãn cách tính và đơn vị/số người vào `_CollectionDetails` dựa trên snapshot hóa đơn, tái sử dụng quy tắc hiển thị của nhánh Details còn lại; không đổi phép tính hay truy vấn giá hiện tại. Khi được duyệt, giữ nguyên các assertion và chạy lại164 test.

**Ảnh hưởng:** không thay đổi bất kỳ hành vi hóa đơn cũ nào trong phiên sửa lỗi này. Hai lỗi còn tồn tại.

## Lỗi02 — thông báo validation cũ

**Nguyên nhân:** trang chi tiết trước đây chỉ render lỗi server trong `asp-validation-for`, không nạp validation script và không có listener cập nhật lỗi khi người dùng nhập lại. Regex ViewModel chỉ kiểm tra chữ số; kiểm tra >0/Int64 vẫn nằm ở service. Vì vậy thông báo sau POST với0 tồn tại đến lần gửi trang tiếp theo.

**Sửa:** thêm `QL_PhongTro/wwwroot/js/repair-cost.js:8` (cập nhật span/class/aria-invalid/customValidity), `:20` (validation tiền), `:35–42` (input/change/submit), chỉ gắn cho form có `data-repair-cost-form` tại `Views/BaoHong/_ChiPhi.cshtml:19`. Nạp asset có version ở `Views/BaoHong/Details.cshtml:12`. So sánh chữ số và chuỗi giới hạn Int64, không dùng floating point để so tiền. Không xóa lỗi phân quyền/trạng thái/xung đột trong validation summary. Không sửa validation server, service, model hay schema.

**Kiểm chứng:** UI thực tế0 → hiển thị lỗi/aria-invalid=true; đổi250000 → span rỗng/aria-invalid=false, lưu được và toast thành công. UI chặn -1,0,250000.5,9223372036854775808. HTTP POST trực tiếp các giá trị này vẫn bị từ chối, không thêm lịch sử chi phí. Các test tiền trong95 ca vẫn PASS. CSRF thiếu token trả400. Bằng chứng ảnh `validation-zero.jpg`, `validation-corrected.jpg`, `corrected-mobile.jpg`, `corrected-desktop.jpg`; script HTTP riêng `http-validation.py` trong thư mục kết quả ignored.

**Ảnh hưởng:** phản hồi UI của form chi phí; không ảnh hưởng form đổi trạng thái/phản hồi hoặc màn hình không có form chi phí. Tất cả quy tắc server giữ nguyên.

## Lỗi03 — hướng dẫn bên chịu phí

**Nguyên nhân:** hướng dẫn “Khách thuê chịu...” được viết cố định trong partial, không xét giá trị đang chọn.

**Sửa:** `Views/BaoHong/_ChiPhi.cshtml:27–34` render hướng dẫn theo bên chịu và thêm data attributes/aria-live. `wwwroot/js/repair-cost.js:31–36` đổi hướng dẫn khi chọn Chủ nhà/Khách thuê; chưa chọn hiển thị yêu cầu chọn. Chủ nhà: không tạo khoản chờ thu từ khách. Khách thuê: chờ hóa đơn kỳ sau của hợp đồng gốc và không tự thêm vào hóa đơn.

**Kiểm chứng:** chọn hai bên trên browser thấy đúng hướng dẫn ngay, không cần lưu. Sau POST tiền không hợp lệ với CHU_NHA, HTML server vẫn có hướng dẫn Chủ nhà. Lưu250000 Chủ nhà thành công trên bản sao. Kiểm tra360x800 không tràn ngang (scrollWidth345<=360), desktop1366x900; có ảnh.

**Ảnh hưởng:** thay văn bản hướng dẫn, không đổi cờ chờ thu, kỳ dự kiến hoặc nghiệp vụ phí.

## Lỗi04 — bảo vệ nguồn chi phí/hóa đơn

Không phát hiện lỗi trong các trường hợp được yêu cầu; không sửa schema hoặc nghiệp vụ để tránh thay đổi dữ liệu nghiệm thu. Đã chạy lại các test tự động sau:

| Trường hợp | Test chứng minh |
|---|---|
| Một chi phí hiện hành/báo hỏng; retry/đồng thời | OneTotalPayerFlagPeriodAndEndedContract; ServiceSavesEditsReplaysAndDisplaysForAllRoles; ServiceConcurrentCreateHasOneWinner; ConcurrentCreationHasOneSourceAndStaleEditsCannotOverwrite |
| Không có hai khoản trên hóa đơn hiệu lực | ConcurrentInvoiceAllocationHasOneEffectiveCharge; DraftReservesSourceAndLocksCostAndProtectsSnapshot |
| Nháp/phát hành/hủy/thay thế giữ nguồn và lịch sử | IssuedCancelledAndReplacementKeepOneEffectiveChargeAndHistory; InvoiceServicePreservesRepairOnDraftCancelAndReplacement |
| Không chuyển hợp đồng | OwnerCostWrongContractAndWrongAmountCannotBeCharged; OriginalContractForeignKeysAndHistorySnapshotsCannotBeForged |
| Chủ nhà chịu không thu khách | ServiceSavesEditsReplaysAndDisplaysForAllRoles; OwnerCostWrongContractAndWrongAmountCannotBeCharged |
| Khóa sửa khi đã gắn hóa đơn nháp | ServiceLocksInvoiceLinkedCostAndRejectsStaleAndChangedReplay; DraftReservesSourceAndLocksCostAndProtectsSnapshot |
| Lỗi giữa chừng rollback | ServiceAuditFailureRollsBackCostAndHistory; WholeTransactionRollbackLeavesNoCostHistoryInvoiceOrCharge |

Các test trên thuộc `BaoHongChiPhiSchemaTests.cs` và `BaoHongChiPhiServiceTests.cs`, tất cả PASS. Fixture ghi vào bản sao riêng, không vào demo chính. Quyền đọc/ghi, phân công, Chủ nhà/Khách không sửa, trạng thái và server validation đều có test hồi quy trong95 ca; HTTP trên bản sao xác nhận hai vai trò xem được250000 và POST sửa trả403.

## Tổng kết kiểm thử

| Bộ kiểm thử | PASS | FAIL | SKIP |
|---|---:|---:|---:|
| Báo hỏng/chi phí |95|0|0|
| Hóa đơn/dịch vụ |162|2|0|
| Tổng lần chạy sau sửa |257|2|0|
| Hai test trên baseline4364280 |0|2|0|

Lệnh kiểm thử:

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~BaoHong|FullyQualifiedName~RoomServicesTests' --logger 'trx;LogFileName=bugfix-final.trx' --results-directory data/s407-cost/bugfix-results
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore -o data/s407/runtime
node --check QL_PhongTro/wwwroot/js/repair-cost.js
```

Build/runtime đạt0 lỗi. Cảnh báo hiện có NU1900, ImageSharp license; CS8601 khi biên dịch lại. Runtime `Start-S407Demo.ps1` đã cập nhật. Không chạy demo chính tự động; server kiểm thử5283 đã dừng. JavaScript syntax đạt. Test không bị sửa để che FAIL; không thêm test mirror implementation cho thay đổi văn bản UI.

## Dữ liệu và Git

Database chính ban đầu đang chạy nên chưa hash được; người dùng xác nhận dừng demo rồi mới ghi nhận baseline. Toàn bộ sửa/test có ghi sau đó dùng database riêng.

- Chính: `D:\DU_AN_KHO\data\s407\demo-20261014-164333-49008c\s407.sqlite`.
- SHA256 đầu/cuối cùng: `d43aa581e9c2bf264a55a5f75aa0920e89f1261679262de3f8150b4e02f380b3`.
- Schema31, integrity=ok, foreign_key_check=0 lỗi; hash dữ liệu từng bảng và toàn bộ byte của file khớp đầu/cuối. Giữ4 báo hỏng,2 hóa đơn,5 dòng hóa đơn,2 chi phí và3 dòng lịch sử chi phí đã nghiệm thu. Không nâng schema/seed/reset/xóa database hoặc thay đổi trạng thái cũ.
- Bản sao UI: `data/s407-cost/bugfix-ui-b1c747823f5a4aa59616d3148470cbec/test.sqlite`; ảnh báo hỏng không copy nên lần UI này tập trung form chi phí, các test ảnh cũ vẫn PASS.
- Snapshot `main-before.json`/`main-after.json`, kết quả TRX và ảnh trong `data/s407-cost/bugfix-results/`, ignored.
- Trong phiên này chỉ sửa `Views/BaoHong/Details.cshtml`, `Views/BaoHong/_ChiPhi.cshtml`; thêm `wwwroot/js/repair-cost.js`; thêm báo cáo này và cập nhật `tien-do.md`. Giữ nguyên các file thay đổi đã có trước phiên. Toàn bộ Git status lưu ở `data/s407-cost/bugfix-results/git-status.txt`; nhánh/HEAD giữ nguyên, không stage/commit/push/merge/rebase. diff --check đạt.

## Đánh giá nghiệm thu

Lỗi02/03 đã xử lý và lỗi04 có kiểm chứng đạt, không có lỗi S4-07:2 mới trong kiểm thử. Có thể nghiệm thu lại phạm vi chi phí/validation/hướng dẫn. **Chưa thể tuyên bố toàn bộ hồi quy đạt hoặc đóng toàn bộ phiếu lỗi** vì hai lỗi UI hóa đơn cũ còn FAIL. Cần PO phê duyệt phạm vi sửa hai lỗi cũ hoặc ghi nhận ngoại lệ nghiệm thu; chưa tự thực hiện sửa ngoài phạm vi.
