# Kết quả kiểm thử mở rộng S4-05 phần 1 — 15/10/2026

## Kết luận

- **28/28 ca chuyên biệt S4-05 PASS**, gồm 18 ca bổ sung so với lượt đầu. Đã kiểm tra thành công, sai trạng thái, quyền/sở hữu/tài khoản thay đổi, các vai trò GET/POST, CSRF/xác nhận, phiên bản cũ và tràn phiên bản, gửi lặp/đồng thời, lỗi cập nhật phòng, lỗi lịch sử/audit, đọc trạng thái mới và bảo toàn dữ liệu.
- Lượt toàn dự án hoàn tất: **901 PASS, 22 FAIL / 923**, không skip, thời gian 26 phút 26 giây. Lượt này đã gồm 23 ca S4-05; bộ chuyên biệt chạy thêm 5 ca HTTP vai trò được bổ sung sau khi lượt toàn bộ bắt đầu. Tổng hợp không trùng theo tên test: **906 PASS, 22 FAIL / 928**. Không coi đây là một lượt duy nhất 928 ca.
- **Cả 22 FAIL cũng FAIL trên commit gốc edef5ea chưa có S4-05**. Đối chiếu theo tên test, không có test lỗi mới do phần này trong phạm vi đã chạy. Các lỗi cũ thuộc kỳ vọng schema/fixture, chuỗi và luồng giao diện, dashboard/tin đăng/hóa đơn/hợp đồng; không kết luận mọi FAIL đều chỉ là lỗi test hoặc toàn dự án hết bug.
- Không cần sửa thêm mã chức năng S4-05 sau đợt test; lượt này bổ sung test và tài liệu, giữ mã ngoài phạm vi. Chưa commit/push.

## Bằng chứng và chạy lại

- Chuyên biệt: `data/s405-release.log`, `data/s405-release-results/release.trx`.
- Toàn bộ: `data/s405-all.log`, `data/s405-all-results/all.trx`.
- Baseline là bản sao `git archive HEAD` của edef5ea; chỉ bổ sung dependency PdfPig cho project chạy test như project hiện tại, không áp dụng mã S4-05. Chạy từng bộ test lỗi tương ứng: `data/s405-baseline-failures-results/baseline-failures.trx` (17 FAIL), `data/s405-baseline-billing-results/baseline-billing.trx` (2 FAIL), `data/s405-baseline-contract-schema-results/baseline-contract-schema.trx` (3 FAIL, 2 PASS). Các file TRX này đều đã hoàn tất.
- Giao diện đã kiểm tra thực tế ở lượt trước trên Edge headless desktop và 360px; log `data/s405-browser2.log`, gồm trạng thái Trống/làm mới, hợp đồng Đã kết thúc, gửi lại 409 và trái quyền 403. Mã giao diện không đổi trong lượt test bổ sung.
- [Ma trận các trường hợp](s4051-ma-tran-kiem-thu.md), [hướng dẫn demo](s4051-tu-dong-giai-phong-phong.md).

```powershell
dotnet test verification/S308Regression/Regression.csproj --no-restore -p:OutputPath="$pwd/data/s405-release/" --filter 'FullyQualifiedName~AutoReleaseRoom'
dotnet test verification/S308Regression/Regression.csproj --no-restore -p:OutputPath="$pwd/data/s405-all/" --logger 'trx;LogFileName=all.trx' --results-directory data/s405-all-results
```

Project verification chạy các test được theo dõi trong repository, giữ nguyên và không đưa file UnitTest1.cs chưa được theo dõi của người dùng vào bộ này. Test dùng SQLite tạm; không chạy vào DB thật. CSDL gốc vẫn có SHA256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`.

Safari/thiết bị cảm ứng vật lý/tải production/SMTP thật và các lần merge tương lai chưa được kiểm chứng; kết quả không phải cam kết không thể có bug khác.

## 22 test còn FAIL, đã đối chiếu trên mã gốc

- `QL_PhongTro.Tests.ContractCreationTests.ResavedDraftRefreshesServicesWithoutDuplicatingThem`
- `QL_PhongTro.Tests.LichHenMigrationTests.CSDLTrong_UpdateLenV10_TaoDuHaiBang`
- `QL_PhongTro.Tests.LichHenMigrationTests.CSLDaCoV9_UpdateLenV10_KhongDungLaiBangCu`
- `QL_PhongTro.Tests.MeterReadingListTests.HttpMenuReadOnlyHtmlAndGetPreservesDatabase`
- `QL_PhongTro.Tests.MeterReadingListTests.ReconcileUi17AndDev18PreservesBusinessRows(uiDatabase: False)`
- `QL_PhongTro.Tests.MeterReadingListTests.ReconcileUi17AndDev18PreservesBusinessRows(uiDatabase: True)`
- `QL_PhongTro.Tests.MeterReadingListTests.UpgradeV17PreservesMeterRowsDefaultFalseAndReruns`
- `QL_PhongTro.Tests.MeterReadingListTests.UsageInvalidDraftForeignHistoryAndHandoverNotCounted`
- `QL_PhongTro.Tests.PermissionTests.AnonymousGuestViewsNhaTotBrandedHomePage`
- `QL_PhongTro.Tests.PermissionTests.BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection`
- `QL_PhongTro.Tests.PermissionTests.BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms`
- `QL_PhongTro.Tests.PermissionTests.ChuNha_DangLaiVaGoTinCuaPhongMinh`
- `QL_PhongTro.Tests.PermissionTests.MenuAndEveryModuleRouteMatchMatrixForAllRoles`
- `QL_PhongTro.Tests.PermissionTests.MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio`
- `QL_PhongTro.Tests.PermissionTests.OwnerDashboardAggregatesOnlyActiveOwnedBuildings`
- `QL_PhongTro.Tests.PermissionTests.QuanLy_ChiDangVaGoTinCuaToaDuocPhanCong`
- `QL_PhongTro.Tests.PermissionTests.RegisterHasPasswordToggleAndConfirmationEmailIsEditable`
- `QL_PhongTro.Tests.PermissionTests.TenantHomeInvoicesExcludeCancelledButKeepDebtAfterContractEnds`
- `QL_PhongTro.Tests.PermissionTests.TinQuaHan_TuDongTamAnVaCanhBaoChuNha`
- `QL_PhongTro.Tests.PermissionTests.UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi`
- `QL_PhongTro.Tests.RoomServicesTests.DepartureHttpWarningSnapshotsAndFutureUnissuedBill`
- `QL_PhongTro.Tests.RoomServicesTests.MonthlyAllAmountsSnapshotSurvivesChanges`
