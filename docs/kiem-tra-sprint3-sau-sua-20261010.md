# Kiểm tra lại Sprint 3 sau sửa phí theo người — 10/10/2026

Đối chiếu 10 story trong tệp người dùng gửi với mã hiện tại, bao gồm thay đổi THEO_NGUOI chưa commit. Đây là rà soát mã và test trên SQLite tạm; không xác nhận nghiệm thu thiết bị thật hoặc SMTP thật.

## Kết luận theo story

| Story | Kết luận | Bằng chứng / phần còn thiếu |
| --- | --- | --- |
| S3-01 | Chưa đủ | Có lấy khách/phòng từ yêu cầu duyệt, tự tính kết thúc, chống chồng lấn/chỉ mã vướng, mã HD-yyyy-xxxx, trạng thái và chỉ số đầu kỳ. `HopDongCreateViewModel.GiaThue`/`TienCoc` có BindNever; POST dùng giá phòng và cọc theo số tháng nguyên 0…3. Chưa nhập được giá thuê chốt và tiền cọc tùy ý trong khoảng cho phép. |
| S3-02 | Đáp ứng trong phạm vi mã/test | Có người đứng tên, thêm người ở ghép, kiểm tra sức chứa theo mốc ngày, ghi chuyển đi/lịch sử. Đã mở lại tính tiền theo số người: tháng chuyển đi tính đủ, giảm tháng sau; hóa đơn đã lưu giữ nguyên. Bộ hồi quy phí/người ở/hóa đơn vừa chạy 140/140 PASS. |
| S3-03 | Có triển khai; cần chốt ngày hết hạn | Có trang chi tiết/PDF thông tin + bảng dịch vụ, quyền người đứng tên/ở ghép và 403 người ngoài. `SapHetHan` chỉ áp dụng 1…29 ngày; ngày hết hạn 0 ngày chưa cảnh báo. Chưa render/kiểm tra trực quan PDF trong lượt này. |
| S3-04 | Đáp ứng trong phạm vi mã/test | Đổi hợp đồng/phòng/tin cùng transaction; truy vấn công khai loại tin/phòng không hợp lệ; audit phòng và test rollback. |
| S3-05 | Có triển khai; chưa nghiệm thu UX | Có danh sách tầng/mã, chỉ số trước, kiểm tra giảm, cảnh báo tiêu thụ >2 lần trung bình 3 kỳ và xác nhận, lưu từng phòng, đếm còn thiếu, input số/CSS mobile. Chưa kiểm tra dùng một tay trên 360px/Safari/thiết bị thật. Không cảnh báo khi chưa đủ ba kỳ hợp lệ. |
| S3-06 | Chưa đủ luồng web một lần | Có các khoản phòng/chỉ số/cố định/theo người, giá ngày chốt, ngày/hạn +7, bỏ qua thiếu chỉ số công tơ, chống trùng theo hợp đồng/tháng. Đã hỗ trợ điện/nước khoán không cần công tơ. `IssueMonthly` gọi `taoNhap: true`: web chỉ tạo Nháp hàng loạt, `PublishDraft` phát hành từng bản. Test service phát hành 50 và test HTTP tạo 50 Nháp đều có ngưỡng <30 giây, nhưng chưa chứng minh một thao tác web phát hành toàn tòa kèm thông báo. |
| S3-07 | Đáp ứng trong phạm vi mã/test | Có dòng chỉ số/số lượng/giá/tiền, tổng/đã trả/còn trả/hạn, quá hạn theo ngày Việt Nam, lọc kỳ/trạng thái. Kết quả liên quan nằm trong bộ hồi quy 140 PASS; phần thu tiền Sprint 4 vẫn chưa hoàn chỉnh. |
| S3-08 | Có triển khai | Có Nháp/sửa chỉ số/điều chỉnh bắt buộc ghi chú, phát hành mới tạo thông báo, khóa bản đã phát hành, hủy lý do và lưu bản cũ/thay thế, lịch sử sửa trước/sau. Không suy test đầy đủ của S3-08 chỉ từ xUnit: suite riêng `verification/S308` chưa chạy lại trong lượt kiểm tra này. |
| S3-09 | Đáp ứng trong phạm vi mã/test | Có kỳ hạn/giá mới mặc định cũ, nối ngày, chống chồng lấn, lịch sử gia hạn/người/giá, giá kỳ gia hạn và giữ snapshot cũ. |
| S3-10 | Một phần | Có tổng/đã chốt/còn thiếu, danh sách phòng/quản lý, khóa kỳ và cảnh báo trang chủ. Nhưng `TienDoChiSoService.GetConfirmedContractsAsync` chỉ đánh dấu đủ khi có cả DIEN và NUOC trong bảng chỉ số; không xét cấu hình THEO_NGUOI. Phòng có điện công tơ đã chốt + nước khoán vẫn có thể bị báo còn thiếu trước phát hành. |

Ràng buộc “một hóa đơn/phòng/kỳ” trong bản gửi khác quyết định hiện hành “một hóa đơn chưa hủy/hợp đồng/tháng”. Hai khách nối tiếp cùng phòng có thể có hóa đơn riêng; không coi đây là lỗi nếu giữ quyết định hiện hành trong AGENTS.md.

## Bằng chứng trực tiếp

- S3-01: `QL_PhongTro/ViewModels/HopDongCreateViewModel.cs`, `Controllers/HopDongController.cs`.
- S3-02: `Services/HoaDonDichVuService.cs` (`LaySoNguoiAsync`), `Controllers/HopDongController.cs` (`ChuyenDi`/`ThemNguoi`), `Services/LichSuNguoiOService.cs`.
- S3-03: `Controllers/HopDongCuaToiController.cs`, `Services/HopDongPdfService.cs`, `ViewModels/HopDongCuaToiViewModels.cs`.
- S3-04: `Controllers/HopDongController.cs`, `Tests/QL_PhongTro.Tests/ContractActivationTests.cs`.
- S3-05: `Services/ChiSoDienNuocService.cs`, `ChiSoDienNuocService.Usage.cs`, `Controllers/ChiSoDienNuocController.cs`.
- S3-06: `Controllers/HoaDonDichVuController.cs` (`IssueMonthly`, `PublishDraft`), `Services/HoaDonDichVuService.Monthly.cs`, `Tests/QL_PhongTro.Tests/RoomServicesTests.MonthlyBulk.cs`.
- S3-07: `Controllers/ThongBaoController.cs`, `Tests/QL_PhongTro.Tests/InvoiceTenant*Tests.cs`.
- S3-08: `Services/HoaDonDichVuService.Draft.cs`, `.Publish.cs`, `.Cancel.cs`, `.History.cs`.
- S3-09: `Controllers/HopDongController.cs` (`GiaHan`), `Tests/QL_PhongTro.Tests/RoomServicesTests.RenewalPricing.cs`.
- S3-10: `Services/TienDoChiSoService.cs`, `KyChiSoLockService.cs`.

## Kiểm thử

- Lượt sửa phí theo người ngay trước lần kiểm tra: **140/140 PASS**, bao gồm hóa đơn/người ở/phí, 0 SKIP; `data/criteria-audit-results/per-person-final.trx`.
- Lượt kiểm tra thêm hợp đồng/chỉ số/lịch sử: **245 test, 241 PASS, 4 FAIL, 0 SKIP**, 1 phút 21 giây. Ba FAIL do expected schema 22 trong khi updater là 23; một FAIL fixture hồ sơ tạo bảng hop_dong đã tồn tại. Không phát hiện FAIL nghiệp vụ mới trong các test được chọn; các khoảng trống nêu trong bảng vẫn tồn tại dù test pass. TRX: `data/criteria-audit-results/sprint3-recheck.trx`. Hai bộ lọc có thể trùng test; không cộng số PASS thành số test duy nhất.
- Không sửa mã nghiệp vụ hoặc DB trong lượt kiểm tra lại này. Không chạy updater trên DB đang dùng.

## Cập nhật sau yêu cầu sửa tự động — 10/10/2026

- **S3-01:** Form nhận giá thuê chốt và cọc nguyên đồng. Backend kiểm tra giá ≥ 500.000 đ, cọc từ 0 đến ba tháng giá chốt; cọc không cần là bội số nguyên tháng. Kỳ hợp đồng lưu giá thỏa thuận, không sửa giá niêm yết. Mở lại Nháp giữ đúng giá/cọc đã thỏa thuận. Ngày kết thúc vẫn tự tính và không nhận từ form.
- **S3-03:** Cảnh báo gồm ngày còn lại 0…29; ngày 0 hiển thị “Hợp đồng hết hạn hôm nay”.
- **S3-06:** Trên trang hóa đơn tháng, chủ nhà chọn tối đa 50 bản Nháp đã kiểm tra, xác nhận nội dung/số tiền và phát hành một lần. Backend kiểm tra chủ sở hữu, tòa/kỳ, phiên bản từng bản, ngày/hạn và dữ liệu chi tiết. Phát hành, khóa chỉ số, nhật ký và tạo thông báo dùng cùng một transaction; lỗi một bản hoàn tác cả lần. Gửi lại không tạo thông báo trùng. Email vẫn theo hàng đợi hiện có.
- **S3-10:** Tiến độ lấy cách tính/giá đang áp dụng theo phòng và ngày chốt. Chỉ dịch vụ THEO_CHI_SO cần bản chỉ số; THEO_NGUOI không bị báo thiếu công tơ. Cấu hình thiếu/giá không hợp lệ vẫn chưa sẵn sàng.
- Đồng bộ kỳ vọng schema 23 trong test và sửa fixture hồ sơ dùng bảng hợp đồng thật, khởi tạo module yêu cầu thuê trên DB test. Fixture nghiệm thu kích hoạt truyền giá/cọc theo form mới.
- Không đổi schema, không chạy updater trên DB đang dùng, không thay đổi dữ liệu thật. App đang chạy cần khởi động lại để nhận bản build mới. Chưa nghiệm thu trực quan 360px/Safari/PDF hoặc gửi qua SMTP thật.

### Kết quả kiểm thử sau sửa

- **365 kiểm thử liên quan đã qua sau sửa:** `autofix-final.trx` có 362 PASS và ba lỗi fixture của kiểm thử giới hạn; bổ sung khởi tạo bảng trên DB tạm, chạy lại đúng ba test đạt 3/3 trong `autofix-limits-final.trx`. Không cộng trùng test. Bao gồm hợp đồng, kích hoạt, người ở, chỉ số, hóa đơn, fixture hồ sơ và schema.
- Test HTTP phát hành 50 Nháp, kiểm tra khóa 100 chỉ số, 50 thông báo, gửi lặp, CSRF và tòa khác đạt PASS; toàn test mất khoảng **2,18 giây**, nên thao tác phát hành đạt ngưỡng <30 giây. Test hai yêu cầu đồng thời không tạo thông báo trùng đạt PASS. Phiên bản cũ hoặc lỗi audit hoàn tác nội dung, trạng thái, khóa chỉ số và thông báo đạt PASS.
- Lượt suite đầy đủ trước các chỉnh fixture cuối: **617 test, 600 PASS, 17 FAIL**, `autofix-full.trx`. Bảy lỗi fixture nghiệm thu kích hoạt và một lỗi fixture hồ sơ đã được sửa, xác minh PASS trong lượt cuối. Chín lỗi còn lại dưới đây đều tồn tại từ lần rà soát đầu; chưa xử lý trong phạm vi Sprint 3, không khẳng định toàn bộ suite đã PASS:
  - `AnonymousGuestViewsNhaTotBrandedHomePage`
  - `MenuAndEveryModuleRouteMatchMatrixForAllRoles`
  - `BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection`
  - `BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms`
  - `MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio`
  - `GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices`
  - `ChuNha_DangLaiVaGoTinCuaPhongMinh`
  - `RegisterHasPasswordToggleAndConfirmationEmailIsEditable`
  - `TinQuaHan_TuDongTamAnVaCanhBaoChuNha`
- Build thành công; `git diff --check` PASS. Giữ nguyên project test; không commit DB hoặc đầu ra test.
