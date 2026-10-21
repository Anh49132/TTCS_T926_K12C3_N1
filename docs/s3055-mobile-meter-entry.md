# S3-05:5 — Quy trình ghi chỉ số trên điện thoại

Ngày kiểm chứng: 08/10/2026. Nhánh `feature/S3-05/05-mobile-meter-entry`, HEAD `02ea568`. Chỉ hoàn thiện presentation và vị trí/focus của S3-05; không thêm nghiệp vụ, schema, migration, hóa đơn hoặc S3-06.

## Đối chiếu AC5

| Yêu cầu | Thay đổi và kiểm chứng |
| --- | --- |
| 1–2: 360px, không overflow trang | Cùng bảng/DOM chuyển thành thẻ bằng CSS ở <=767px; cột tham chiếu desktop ẩn trên mobile, tham chiếu cạnh ô nhập vẫn hiện. Đo tại viewport 360: scrollWidth bằng clientWidth; đạt ở danh sách, lỗi, warning, lưu và reload. Không dùng overflow-x hidden để che lỗi. |
| 3: một tay | Nhập tuần tự theo chiều dọc, nút lưu cuối mỗi phòng và rộng toàn thẻ trên mobile; không có nút lưu hàng loạt hay autosave. Tính thuận tiện trên tay thật chưa nghiệm thu. |
| 4: header/kỳ/tòa/progress | Tiêu đề đầy đủ trong nội dung, kỳ MM/yyyy, chọn tòa không vượt chiều rộng; progress nổi bật và xuống dòng khi cần. |
| 5: tầng/phòng | Giữ GroupBy tầng và thứ tự service cung cấp, nhãn Phòng/mã và tiêu đề tầng rõ. |
| 6: kỳ trước gần ô nhập | Mỗi dịch vụ có tham chiếu ngay trước ô nhập, nhận đúng giá trị server và dấu Bàn giao; aria-describedby liên kết tham chiếu và lỗi. |
| 7: touch/decimal | Input cao >=48px, chữ 16px, inputmode decimal; giữ number, required, min=0, max=99999999999.999, step=0.001 và readonly/disabled hiện có. |
| 8: mới < trước | Validate ngay khi input, lỗi đúng dịch vụ/phòng, aria-invalid và live region; focus/cuộn về ô lỗi. Submit kiểm mọi input cùng phòng rồi tập trung ô lỗi đầu. |
| 9–10: warning/xác nhận | Khối warning xuống dòng, focus được, cuộn tới đầu khối dưới header; nút xác nhận rộng toàn khối, >=48px. Đã kiểm warning điện và warning cả điện+nước. |
| 11: trạng thái/Còn lại | Badge Đã chốt/Chưa chốt có chữ và màu; progress đọc DB sau save/reload, không thay quy tắc lát 3. |
| 12: thao tác chính | Select/input/nút trong module có chiều cao tối thiểu 48px. Đo trực tiếp input/save 48px, confirm 48px. |
| 13: giữ vị trí | Success redirect thêm fragment meter-room-ID; POST lỗi/warning đánh dấu phòng từ Model.Input, ưu tiên field error → room error → warning → phòng. JS focus và scrollIntoView với scroll-margin tránh header. |
| 14: desktop | Bảng vẫn dùng cùng form/input/ID. Đã kiểm 1280x900: không overflow, có heading bảng, lưu độc lập và focus đúng phòng. |

Mỗi phòng chỉ có một form POST, mỗi form một antiforgery token; không clone DOM/form/input. HTTP tests kiểm ID duy nhất, form/phòng và token. Controller chỉ đổi một dòng redirect thành công để thêm fragment, không sửa quyền, CSRF hoặc save service. JavaScript bật noValidate khi có JS để lỗi submit đi qua vùng lỗi riêng; vẫn đọc validity của các ràng buộc number hiện có và kiểm server vẫn giữ nguyên. Khi không có JS, native constraints vẫn hoạt động.

## Kết quả tự động

- Build `--no-restore --output data/s3055-build -p:IntermediateOutputPath=obj/s3055/`: PASS, 0 lỗi. Lần mặc định trước đó bị Access denied khi ghi cache obj cũ; không xóa cache. Build cách ly ban đầu có cảnh báo ImageSharp và CS8601 hiện có; build incremental cuối chỉ còn cảnh báo ImageSharp.
- Filter Meter: **98/98 PASS**, gồm toàn bộ **95/95** meter/schema lát 4 và ba HTTP test mới trong MeterReadingMobileTests.cs.
- Bộ cuối gồm Meter và ba test nâng schema bảo toàn dữ liệu: **101/101 PASS**, 0 lỗi/0 skipped; `data/test-results/s3055-final-targeted.trx`.
- JS harness trên source cuối: **8/8 PASS**: lỗi tức thời/focus, sửa lỗi, first-invalid submit, decimal hợp lệ, server field error, room/concurrency error, warning dài, anchor sau save. Đây là harness DOM giả, tách biệt với nghiệm thu trình duyệt.
- Regression cùng bốn nhóm lát 4: **306/314 PASS, 8 FAIL, 0 skipped**. S3-05 91/91; ContractCreationTests 99/99; PermissionTests 60/67; RoomServicesTests 56/57. Tăng ba test mobile so với 303/311.
- So tên failure trực tiếp với `s3054-regression-final.trx`: tập tên giống hệt, **0 lỗi mới**. Không sửa tám baseline.

Tám failure giữ nguyên:

1. PermissionTests.MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio.
2. PermissionTests.UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi (expected 15, actual 18).
3. PermissionTests.MenuAndEveryModuleRouteMatchMatrixForAllRoles.
4. PermissionTests.GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices.
5. PermissionTests.RegisterHasPasswordToggleAndConfirmationEmailIsEditable.
6. PermissionTests.ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit.
7. PermissionTests.AnonymousGuestViewsNhaTotBrandedHomePage.
8. RoomServicesTests.DepartureHttpWarningSnapshotsAndFutureUnissuedBill.

Lệnh tái kiểm chứng:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --output data/s3055-build -p:IntermediateOutputPath=obj/s3055/
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s3055-tests -p:IntermediateOutputPath=obj/s3055/ --filter 'FullyQualifiedName~Meter|FullyQualifiedName~UpgradeAddsMissingColumnsWithoutReplacingLegacyContract|FullyQualifiedName~V7CopyUpgradePreservesIssuedInvoicesAndPrivatePrices|FullyQualifiedName~MigrationPreservesServicesAndRoomsAndRefusesOverwrite' --logger 'trx;LogFileName=s3055-final-targeted.trx' --results-directory data/test-results
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --output data/s3055-tests --filter 'FullyQualifiedName~MeterReadingListTests|FullyQualifiedName~ContractCreationTests|FullyQualifiedName~PermissionTests|FullyQualifiedName~RoomServicesTests' --logger 'trx;LogFileName=s3055-regression.trx' --results-directory data/test-results
git -c safe.directory=D:/S3/TTCS_T926_K12C3_N1 diff --check
git -c safe.directory=D:/S3/TTCS_T926_K12C3_N1 status --short
```

## Nghiệm thu trình duyệt trên bản sao riêng

Dùng SQLite backup API từ nguồn `data/s3054-acceptance/acceptance.sqlite` mở read-only sang file mới `data/s3055-acceptance/mobile.sqlite`; schema nguồn đã v18, không chạy updater/initializer. Thêm quản lý giả chỉ trong copy và gán tòa fixture; không reset mật khẩu tài khoản hiện có. DB demo chính không chạy web/test/updater và không có ghi test. App dùng cổng 5275, DatabasePath tuyệt đối chỉ tới copy. Runtime đầu lỗi logger Windows EventLog access denied; khởi động lại với EventLog=None và key path riêng trong thư mục fixture, không sửa cấu hình source/secret.

Giai đoạn 1 giữ nước THEO_NGUOI: nhập điện 29 <30 → lỗi đúng ô, không POST; sửa 51 → warning C=21/A=10, không ghi October; bấm Lưu phòng lần nữa vẫn warning; sửa 40 → save trực tiếp/progress 1/2; sửa 51 → warning → xác nhận → save cờ 1; phòng kế tiếp 10 → save/progress 2/2; reload đúng giá trị và focus/phòng. Không có bản meter nước giả. Desktop 1280px sửa phòng kế tiếp thành 11 và save được, progress giữ 2/2.

Dừng app trước khi chuẩn bị giai đoạn 2 trong copy: nước thành THEO_CHI_SO, thêm ba kỳ lịch sử giả 5/7/9 tiêu thụ 10. Khởi động lại: nước 29 <30 → lỗi đúng nước trong viewport; nước 51 cùng điện 51 → warning cả hai. Warning focus và cuộn đầu khối tại y~100px, cao ~433px, không overflow. Lưu lại không confirm vẫn warning; confirm → cả điện/nước 51, flags [1,1], progress 1/2. Phòng kế tiếp thiếu nước → submit client chặn/focus nước; sửa 1 → save/progress 2/2; reload giữ đủ bốn bản tháng 10. DB integrity ok, foreign_key_check rỗng.

SHA256 nguồn copy trước/sau cùng `72e40e7e057095e860ccc1900c5864e4b895a1f300ce7761df67d8f9e5ba19f1`. Ảnh local ignored: mobile-warning.png, mobile-complete.png, mobile-summary.png trong data/s3055-acceptance. Đã dừng app và reset viewport trình duyệt sau nghiệm thu.

Chưa kiểm bàn phím Android/iPhone thật, thao tác một tay trên thiết bị vật lý, Safari, mạng 3G và stress đồng thời. Đây là demo S3-05 end-to-end trên fixture v18; không tuyên bố toàn repository PASS hoặc nâng DB demo chính (tài liệu lát 4 ghi DB chính v17).

## Phạm vi thay đổi

Sửa Index.cshtml, meter-readings.js và đúng dòng success redirect trong ChiSoDienNuocController.cs. Thêm meter-readings.css, MeterReadingMobileTests.cs và tài liệu này; cập nhật docs/tien-do.md. Không sửa csproj/test baseline, service, schema, secret/.env. Build/TRX/ảnh/DBcopy đều ignored; không stage/commit/push/merge/PR.

Kết thúc: git diff --check PASS, index rỗng; status có đúng 4 modified + 3 untracked source/test/docs trên nhánh và HEAD đã nêu. Không có DB/WAL/SHM/backup/build artifact/secret hoặc file ngoài phạm vi trong Git status. READY cho review S3-05:5 và demo S3-05 trên fixture v18.
