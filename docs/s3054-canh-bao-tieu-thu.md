# S3-05:4 — Cảnh báo và xác nhận tiêu thụ bất thường

## Quyết định PO và phạm vi

Theo quyết định PO ngày 08/10/2026: C là chỉ số mới trừ chỉ số tham chiếu server; A là trung bình tiêu thụ của ba kỳ hợp lệ gần nhất trước kỳ hiện tại, cùng hợp đồng và dịch vụ. C > 2A mới bất thường; bằng ngưỡng vẫn bình thường. Thiếu ba kỳ thì không cảnh báo. Không bắt buộc lịch sử liên tiếp. Nếu đủ ba kỳ nhưng A=0, C=0 bình thường và C>0 cần xác nhận.

Chỉ DIEN/NUOC hiệu lực THEO_CHI_SO tham gia. THEO_NGUOI không tạo bản chỉ số. Không triển khai S3-05:5, phát hành hóa đơn S3-06, khóa kỳ S3-10 hoặc sửa baseline failures.

## Schema v18

`DatabaseUpdates` thêm bước `MeterAnomalySchema` sau v17:

```sql
ALTER TABLE chi_so_dien_nuoc
ADD COLUMN da_xac_nhan_bat_thuong INTEGER NOT NULL DEFAULT 0
CHECK(da_xac_nhan_bat_thuong IN (0,1));
```

Giữ PK, FK RESTRICT, unique hợp đồng/dịch vụ/kỳ, các CHECK và trigger v17. Không rebuild bảng, không thêm audit fields và không backfill chỉ số. Bản cũ nhận false, không suy đoán đã được xác nhận. Cờ mới nằm trong allowlist audit hiện có và lưu cùng transaction với nghiệp vụ.

Bước nâng dùng transaction, kiểm integrity/FK rồi ghi marker 18; updater tạo backup trước nâng. Chạy lại version 18 không thay dữ liệu. Thiếu marker 18 chỉ được nhận cột INTEGER NOT NULL DEFAULT 0 có CHECK tương ứng. Kiểm tra phục hồi marker v17 được mở rộng để nhận chính xác phần cột v18 đã thêm, đồng thời vẫn kiểm toàn bộ định nghĩa bảng và trigger v17; không thay DDL của bước v17 và không nhận schema lạ/thiếu bảo vệ.

Dừng app và chỉ chạy updater khi được phép nâng đúng DB mục tiêu. Không web auto-update. Đặt `DatabasePath` trỏ tới bản sao riêng trước khi thử:

```powershell
$env:DatabasePath='D:\S3\TTCS_T926_K12C3_N1\data\s3054-acceptance\acceptance.sqlite'
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

Rollback bằng backup trước nâng khi app đã dừng; không ghi đè DB của đồng đội. Đối với DB thử nghiệm disposable, có thể tạo bản sao mới để thử lại. Không chạy lệnh này với DB demo chính khi chưa được phép.

## Lịch sử và phép tính

Server lấy bản chi_so_dien_nuoc trước kỳ hiện tại; bổ sung dòng legacy hóa đơn DA_PHAT_HANH, DICH_VU/THEO_CHI_SO có đủ chi_so_dau và chi_so_cuoi. Loại số âm, cuối < đầu, ngoài giới hạn hoặc quá ba số lẻ và khoảng ngày không hợp lệ. Lọc cùng hợp đồng/dịch vụ; không đếm bàn giao hay kỳ hiện tại/tương lai.

Gộp theo `(tu_ngay,den_ngay)`, ưu tiên bản chỉ số hợp lệ; legacy chỉ bù kỳ không có bản chỉ số hợp lệ. Mỗi kỳ một giá trị, sắp ngày kết thúc mới nhất rồi ngày bắt đầu, lấy tối đa ba kỳ. Tiêu thụ lịch sử = cuối - đầu. Không fake kỳ thiếu hoặc đổi NULL thành 0.

Dùng decimal và so sánh `3*C > 2*sum(consumption)` để không làm tròn trung bình trước khi quyết định. UI hiển thị trung bình/ngưỡng tối đa sáu số lẻ; quyết định server không dùng số đã định dạng. Không chia cho A hoặc hiển thị phần trăm giả khi A=0.

## Xác nhận và bảo mật

POST đầu có bất thường không ghi meter/audit, trả warning tại đúng phòng và đúng điện/nước, gồm chỉ số trước/mới, tiêu thụ hiện tại, tiêu thụ kỳ lịch sử gần nhất, trung bình và ngưỡng. Người dùng sửa rồi bấm Lưu phòng để kiểm tra lại, hoặc chủ động bấm Xác nhận vẫn muốn lưu.

Token ASP.NET Data Protection có hạn 15 phút, gắn với người nhập, tòa, phòng, hợp đồng, kỳ, phiên bản phòng, toàn bộ dịch vụ cần nhập, chỉ số tham chiếu/mới, phiên bản/khóa bản hiện tại và nội dung/nguồn/phiên bản ba kỳ lịch sử. Thay số hoặc lịch sử làm token cũ mất hiệu lực. Token xác nhận chỉ được chấp nhận cùng thao tác xác nhận rõ ràng; browser gửi boolean đơn độc không đủ.

Mỗi POST vẫn kiểm quyền module/QUAN_LY/phân công, CSRF, quan hệ server, tham chiếu, số mới >= trước, giới hạn và optimistic version. BEGIN IMMEDIATE bao toàn bộ đọc lại, tính và lưu; lỗi SQL/audit rollback cả phòng. Không tin average, consumption, threshold hoặc abnormal flag từ client. Normal save đặt false; confirmed abnormal đặt true đúng từng dịch vụ; dịch vụ bình thường giữ false. Thành công redirect GET, Đã chốt/progress dùng nguyên quy tắc lát 2/3 và reload đọc DB.

## Nghiệm thu trình duyệt trên bản sao

Đã dùng `data/s3054-acceptance/acceptance.sqlite` (ignored), sao lưu SQLite từ nguồn mở read-only. Trước chuẩn bị fixture, nâng v17→v18 giữ nguyên mọi dòng cũ, integrity=ok/FK rỗng; chạy lặp không đổi dữ liệu.

Fixture riêng: hợp đồng 1 bắt đầu 01/05/2026 (số tháng 27 khớp ngày kết thúc), cấu hình điện bắt đầu tháng 5; thêm bàn giao cho hợp đồng 1/2 và ba bản điện tháng 5/7/9 của hợp đồng 1, mỗi kỳ tiêu thụ 10, cuối kỳ gần nhất 30. Nước giữ THEO_NGUOI. Không sửa nguồn demo hoặc backup chính.

Trình duyệt in-app localhost: nhập điện 51 → warning C=21, A=10, ngưỡng=20, chưa lưu bản tháng 10, progress 0/2. Sửa 40 → save trực tiếp, cờ 0, progress 1/2. Dừng app và reset riêng bản tháng 10 trong fixture disposable để kiểm độc lập: 51 → warning → xác nhận → save cờ 1, Đã chốt và progress 1/2. Phòng còn lại nhập 10 → lưu trực tiếp cờ 0, progress 2/2; reload giữ đúng hai bản. Không có bản nước giả; integrity=ok/FK rỗng. App nghiệm thu đã dừng.

Ảnh bằng chứng local ignored: `data/s3054-acceptance/warning.png`, `confirmed.png`. Chrome/Safari thiết bị thật và tối ưu 360px chưa nghiệm thu ở lát này.

## File thay đổi

Production sửa: `QL_PhongTro/Data/AccountReuseSchema.cs`, `AppDbContext.Audit.cs`, `DatabaseUpdates.cs`, `MeterReadingSchema.cs`; `QL_PhongTro/Models/ChiSoDienNuoc.cs`; `QL_PhongTro/Services/ChiSoDienNuocService.cs`; `QL_PhongTro/ViewModels/ChiSoDienNuocViewModel.cs`; `QL_PhongTro/Views/ChiSoDienNuoc/Index.cshtml`.

Production mới: `QL_PhongTro/Data/MeterAnomalySchema.cs`, `QL_PhongTro/Services/ChiSoDienNuocService.Usage.cs`.

Test sửa: `Tests/QL_PhongTro.Tests/MeterReadingListTests.cs`, `MeterReadingSaveTests.cs`, `MeterSchemaTests.cs`. Test mới: `Tests/QL_PhongTro.Tests/MeterAnomalySchemaTests.cs`, `MeterUsageWarningTests.cs`. Không sửa csproj hoặc tám test baseline. Tài liệu mới là file này.

## Lệnh xác minh cuối

Chạy tại repository root. Mỗi test dùng DB GUID trong thư mục temp theo fixture sẵn có; không chạy trên DB demo.

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --output data/s3054-build

dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s3054-reviewed --filter 'FullyQualifiedName~Meter|FullyQualifiedName~UpgradeAddsMissingColumnsWithoutReplacingLegacyContract|FullyQualifiedName~V7CopyUpgradePreservesIssuedInvoicesAndPrivatePrices|FullyQualifiedName~MigrationPreservesServicesAndRoomsAndRefusesOverwrite|FullyQualifiedName~UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi' --logger 'trx;LogFileName=s3054-reviewed.trx' --results-directory data/test-results

dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --output data/s3054-reviewed --filter 'FullyQualifiedName~MeterReadingListTests|FullyQualifiedName~ContractCreationTests|FullyQualifiedName~PermissionTests|FullyQualifiedName~RoomServicesTests' --logger 'trx;LogFileName=s3054-regression-final.trx' --results-directory data/test-results

git -c safe.directory=D:/S3/TTCS_T926_K12C3_N1 diff --check
git -c safe.directory=D:/S3/TTCS_T926_K12C3_N1 status --short
```

Build thành công, 0 lỗi; cảnh báo license ImageSharp có sẵn. Targeted cuối: 98/99 pass, một fail baseline v13 (expected 15, actual 18); toàn bộ 95 test meter pass, gồm 88 test S3-05 (22 test usage mới và ba test schema v18 mới). Ba test regression nâng schema từng fail do v18 đã pass sau sửa validator; không sửa fixture của ba test này để che lỗi.

Regression cuối bốn nhóm: **303/311 pass, 8 fail baseline, 0 skipped**. Theo nhóm: S3-05 88/88; ContractCreationTests 99/99; RoomServicesTests 56/57; PermissionTests 60/67. Đây là regression bốn nhóm, không phải full suite toàn repository. Đối chiếu TRX trước S3-05:4 (`s3053-regression.trx`, 278/286 pass) cho đúng cùng tập tám failure; không có tên failure mới. Lỗi v13 vẫn là assertion hard-code expected version 15 (trước actual 17, nay actual 18).

Tám baseline giữ nguyên:

- PermissionTests.MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio — thiếu badge aria-label.
- PermissionTests.UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi — expected version 15.
- PermissionTests.MenuAndEveryModuleRouteMatchMatrixForAllRoles — menu không khớp assertion ma trận.
- PermissionTests.GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices — thiếu chuỗi thông báo.
- PermissionTests.RegisterHasPasswordToggleAndConfirmationEmailIsEditable — thiếu class password-field.
- PermissionTests.ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit — fixture tạo hop_dong đã tồn tại.
- PermissionTests.AnonymousGuestViewsNhaTotBrandedHomePage — thiếu link chi tiết tin trong assertion.
- RoomServicesTests.DepartureHttpWarningSnapshotsAndFutureUnissuedBill — thiếu chuỗi Ghi nhận chuyển đi.

`git diff --check` đạt; index rỗng. Đã review cả diff tracked và nội dung năm file mới. Không commit/push. READY cho review và người dùng test thủ công trên bản sao v18; DB demo chính v17 cần quyết định nâng riêng trước khi dùng source mới với nó.

DB demo chính giữ schema 17 và SHA256 `6c86a6e2022ec7b96a12a7e296b8f22bfe7769bb8b952b7b5bda29b4744284d9`. DB copy, backup mới của copy, ảnh và TRX/build đều ignored trong `data/`; không có artifact nhạy cảm trong diff. Git giữ branch `feature/S3-05/04-usage-warning`, HEAD `bdb171379aacf3983aa40d2758f100c220aec1b0`, toàn bộ thay đổi unstaged.
