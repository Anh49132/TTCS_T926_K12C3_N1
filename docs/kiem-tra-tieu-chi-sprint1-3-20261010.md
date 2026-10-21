# Đối chiếu tiêu chí Sprint 1–3 — 10/10/2026

> Cập nhật sau audit theo yêu cầu người dùng: đã khôi phục phí THEO_NGUOI trong hóa đơn riêng lẻ, hàng loạt và phát hành Nháp. Số lượng lấy từ danh sách người ở của hợp đồng; tháng chuyển đi tính đủ, kỳ sau giảm. Điện/nước khoán theo người không yêu cầu chỉ số. Nội dung bên dưới là kết quả audit trước sửa; phát hiện 5 và hạn chế THEO_NGUOI trong các dòng S2-10/S3-02/S3-06 đã được xử lý. Kết quả kiểm thử sau sửa được ghi ở mục mới nhất trong `tien-do.md`.

Phạm vi: 30 story S1-01…S3-10 trong ba tệp người dùng gửi. Mã được rà soát: `dev`, commit `265dd83`. Đây là kiểm tra mã, schema đọc trực tiếp và kiểm thử tự động; không phải biên bản nghiệm thu toàn bộ trên thiết bị thật.

**Kết luận: chưa đáp ứng đầy đủ tất cả tiêu chí gốc.** Không suy trạng thái từ phần backlog cũ trong AGENTS.md: tài liệu đó còn ghi Sprint 3 chưa làm, nhưng checkout hiện tại đã có hợp đồng, người ở ghép, chỉ số, gia hạn và hóa đơn đầy đủ hơn.

Quy ước: **Có** = tìm thấy triển khai tương ứng; **Một phần** = có tiêu chí thiếu/khác; **Khác đặc tả** = khác yêu cầu gửi nhưng có quyết định hiện hành trong tài liệu; **Chưa xác nhận** = cần nghiệm thu bổ sung. “Có” không đồng nghĩa mọi AC đã được kiểm thử thủ công.

## Các điểm chưa khớp cần xử lý hoặc cập nhật yêu cầu

1. **S1-02, AC khóa đăng nhập:** `CredentialValidationService.HandleFailedLogin` cộng số lần sai nhưng không lưu/so sánh mốc bắt đầu cửa sổ 15 phút. Năm lần sai cách xa nhau vẫn có thể khóa; chưa đúng “5 lần trong 15 phút”. Lần sai thứ năm trả lỗi chung, thông báo thời gian khóa xuất hiện ở lần kiểm tra tiếp theo. Mật khẩu nhập ngắn hơn 8 ký tự bị trả lỗi trước bước đếm, nên không góp vào số lần sai.
2. **S2-04, AC phân trang:** `TimTinViewModel.KichThuocTrang = 6`, trong khi tiêu chí yêu cầu **12 tin/trang**.
3. **S3-01, AC giá thuê chốt:** `GiaThue` là `[BindNever]`; POST lấy lại giá phòng. Chủ nhà không nhập được giá thỏa thuận riêng khi lập hợp đồng.
4. **S3-01, AC cọc:** chỉ chọn `SoThangCoc` nguyên 0…3 rồi nhân giá phòng. Mặc định và giới hạn đúng, nhưng không nhập được số tiền bất kỳ trong khoảng cho phép, ví dụ cọc 1.500.000 đ với giá thuê 2.000.000 đ.
5. **S3-02 AC giảm phí theo người; S3-06 AC khoản khoán theo người; S2-10 mục tiêu nước khoán:** `LayDichVuKhoanTheoNguoiAsync` và bước phát hành chặn `THEO_NGUOI`, kể cả dịch vụ cố định phụ trợ tính theo người. Cấu hình vẫn cho lưu cách tính này. Tiến độ ghi đây là quyết định PO ngày 09/10; vì vậy khác tiêu chí gốc, không nên tự mở lại trước khi thống nhất yêu cầu.
6. **S1-10 AC nhật ký thanh toán:** có model/bảng thanh toán tối thiểu nhưng chưa có luồng ghi nhận thanh toán hoàn chỉnh; `ThanhToanHoaDon` không nằm trong allowlist audit. Chưa thể kết luận AC nhật ký thanh toán đã đáp ứng.
7. **S1-01 AC đăng nhập ngay:** tài khoản mới phải xác nhận mã email trước khi đăng nhập. Đây là hành vi hiện hành được AGENTS.md ghi nhận, nhưng khác câu chữ tiêu chí gốc.
8. **S1-04 AC chuyển trang khi trái quyền:** MVC trả trực tiếp 403 có thông báo; không redirect tới trang báo không đủ quyền. Phù hợp quyết định hiện hành trong AGENTS.md, khác tiêu chí gửi.
9. **S1-06 AC chủ nhà xem căn cước rõ:** quyền xem còn áp dụng cho hợp đồng chờ hiệu lực và đã kết thúc; rộng hơn “phòng đang thuê”. AGENTS.md đã ghi rõ quyết định này.

## Sprint 1

| Story | Đánh giá | Đối chiếu AC |
| --- | --- | --- |
| S1-01 | Một phần / khác đặc tả | Có 4 trường, kiểm tra SĐT 10 số đầu 0, mật khẩu ≥8 có chữ+số, BCrypt, phát hiện trùng email/SĐT và luôn gán KHACH_THUE. AC đăng nhập ngay khác: phải xác nhận email. Gửi lại đăng ký đang chờ với cùng email/SĐT tiếp tục xác nhận thay vì báo trùng. |
| S1-02 | Một phần | Có đăng nhập email/SĐT, JWT 30 phút, refresh 7 ngày, lỗi chung và thu hồi token khi logout. Cửa sổ đếm 15 phút chưa đúng như phát hiện 1. |
| S1-03 | Có; email thật chưa xác nhận | Có tạo CHU_NHA/QUAN_LY/ADMIN, mật khẩu tạm, buộc đổi; khóa thu hồi refresh và phiên, cookie kiểm tra tài khoản mỗi request. Có lọc vai trò/trạng thái và 20 dòng/trang. Có sender SMTP/pickup, chưa thử giao nhận inbox thật trong lần audit này. |
| S1-04 | Khác đặc tả ở redirect | Quyền lưu DB, thiếu cặp từ chối; menu và kiểm tra backend có triển khai; API cơ chế quyền trả JSON 403. MVC trực tiếp 403 theo quyết định mới. Chưa dùng việc ẩn menu làm bằng chứng đầy đủ cho tất cả endpoint. |
| S1-05 | Có | Có xác minh mật khẩu hiện tại, mật khẩu mới khác cũ; reset email 30 phút/một lần; 3 lần/giờ/email; đổi phiên để vô hiệu cookie/JWT/refresh cũ. Quên mật khẩu chỉ dành KHACH_THUE hoạt động theo quyết định hiện hành. |
| S1-06 | Có; phạm vi xem rõ khác bản gốc | Có hồ sơ, căn cước 9/12 số, ảnh JPG/PNG ≤5MB, resize rộng ≤1600px; lưu ảnh private và mask phía server. Chủ nhà liên quan được xem rõ cả hợp đồng chờ/đã kết thúc theo quyết định mới. |
| S1-07 | Có | Có trường tòa/địa chỉ/phường/quản lý/ghi chú, một quản lý nhiều tòa; chặn xóa tòa còn phòng và ngừng hoạt động; danh sách có số phòng/trống, tìm tên hoặc địa chỉ. |
| S1-08 | Có | Có dữ liệu phòng, bốn trạng thái, kiểm tra mã trùng trong tòa, giá nguyên ≥500.000 đ, định dạng Việt Nam, tạo nhanh theo tầng/số phòng. Có test luồng tạo phòng còn thất bại; xem kết quả kiểm thử. |
| S1-09 | Có | Có ba cách tính trong cấu hình, năm dịch vụ mặc định, lịch giá có ngày hiệu lực, snapshot hóa đơn, chặn xóa dịch vụ được tham chiếu. Khả năng phát hành với THEO_NGUOI bị hạn chế như phát hiện 5. |
| S1-10 | Một phần | Có audit phòng/giá/hợp đồng/chỉ số/hóa đơn, tên/vai trò/thời điểm và trường trước/sau, bộ lọc và trigger cấm sửa/xóa. Chưa đủ audit thanh toán. |

Bằng chứng: `Controllers/AccountController*.cs`, `AuthController.cs`, `ManagedAccountsController.cs`, `PermissionsController.cs`, `HoSoController.cs`, `PhongTroController.cs`, `NhatKyController.cs`; `Services/CredentialValidationService.cs`, `AuthService.cs`, `TokenService.cs`, `SessionVersionStore.cs`, `PasswordResetService.cs`, `GiayToImageStore.cs`, `HoSoAccess.cs`, `DichVuService*.cs`; `Authorization/AppCookieEvents.cs`, `Data/AppDbContext.Audit.cs`, `Data/AuditSchema.cs`.

## Sprint 2

| Story | Đánh giá | Đối chiếu AC |
| --- | --- | --- |
| S2-01 | Có | Có gán mặc định khi tạo phòng mới, chọn riêng, giá riêng thắng giá tòa, bảng/tổng phí cố định và lịch ngừng từ tháng sau; bản phát hành giữ snapshot. Thay mặc định không tự gán phòng cũ theo giả định hiện hành. |
| S2-02 | Có; thao tác kéo thả chưa nghiệm thu lại | Có giới hạn 8 ảnh/5MB/JPG-PNG, sắp xếp, ảnh đầu đại diện, thumbnail tối đa rộng 400px, xác nhận xóa và retry dọn tệp. Retry không bảo đảm xóa vật lý thành công tức thì nếu tệp bị khóa. |
| S2-03 | Có | Có chặn phòng không trống, lấy thông tin phòng, 4 trạng thái, hạn 30 ngày, chặn hai tin đang hiển thị và tác vụ hết hạn. |
| S2-04 | Một phần | Có lọc kết hợp khu vực/giá/diện tích/sức chứa, kiểm tra hiển thị/hết hạn/phòng trống/tòa hoạt động, 3 cách sắp xếp và gợi ý nới giá. **6 tin/trang**, khác 12. Chưa đo lại mốc <2 giây/500 tin trong audit này. |
| S2-05 | Có; 360px chưa nghiệm thu lại | Có bộ ảnh/thông số/cọc/mô tả, bảng dịch vụ, ước tính phòng+phí và chú thích điện nước, cho xem không đăng nhập. Chưa xác nhận trực quan 360px/Safari trong lần này. |
| S2-06 | Có | Có loại/ngày/số người/lời nhắn; ngày Việt Nam từ hôm nay đến +60, giới hạn sức chứa, chống yêu cầu mở trùng và chỉ dẫn yêu cầu cũ, mã YC-yyyyMM-xxxx. |
| S2-07 | Có | Có trường danh sách, lọc trạng thái/tòa, mới nhất trước, cả MOI và DA_HEN_LICH được tính chưa xử lý/quá 24h, badge menu. Lưu ý riêng: danh sách trả SĐT rõ, cần rà lại với quy tắc mask SĐT trong AGENTS.md. |
| S2-08 | Có | Có lịch hẹn/cảnh báo 30 phút, lý do từ chối bắt buộc, lịch sử cho khách, duyệt Thuê ngay giữ chỗ và đường dẫn lập hợp đồng. |
| S2-09 | Có | Có danh sách của khách, lịch hẹn/lý do, chỉ hủy MOI/DA_HEN_LICH, không khôi phục và liên kết đúng tin. |
| S2-10 | Có ở cấu hình; luồng hóa đơn khác đặc tả | Chọn theo chỉ số/theo người, yêu cầu giá >0, hiển thị hiệu lực kỳ sau, lưu lịch cấu hình. Tuy nhiên cấu hình THEO_NGUOI không đi tiếp được tới phát hành hóa đơn. |

Bằng chứng: `Controllers/TinDangController.cs`, `TimTinController.cs`, `YeuCauController.cs`, `LichHenController.cs`, `DichVuPhongController.cs`; `Services/DichVuPhongService.cs`, `DichVuService.DienNuoc.cs`, `RoomImageStore.cs`, `RoomImageDeletionService.cs`, `TinDangExpirationService.cs`, `YeuCauThueService.cs`, `LichHenService.cs`; `ViewModels/TimTinViewModel.cs`.

## Sprint 3

| Story | Đánh giá | Đối chiếu AC |
| --- | --- | --- |
| S3-01 | Một phần | Có lấy khách/phòng từ yêu cầu duyệt, ngày/kỳ hạn/ngày chốt, tự tính kết thúc, chặn chồng lấn và chỉ mã vướng, mã HD-yyyy-xxxx, lưu chỉ số bàn giao, chuyển phòng khi lưu hiệu lực. **Giá chốt không nhập được; cọc chỉ chọn số tháng nguyên.** Lưu Nháp giữ trạng thái phòng là hành vi bổ sung. |
| S3-02 | Một phần | Có một người đứng tên, thêm người/điện thoại/căn cước/ngày vào, chặn sức chứa tại các mốc ngày, ngày chuyển đi và lịch sử. Có tính số người kỳ sau nhưng không giảm được khoản khoán theo người qua luồng phát hành vì THEO_NGUOI đang bị chặn. Test HTTP chuyển đi còn thất bại. |
| S3-03 | Có | Có trang thông tin, danh sách người ở, PDF thông tin+dòng dịch vụ, kiểm tra người đứng tên/ở ghép và 403 người ngoài. Cảnh báo khi số ngày còn lại >0 và <30; ngày hết hạn (0 ngày) chưa có cảnh báo này, cần chốt nếu AC bao gồm hôm nay. Chưa render PDF để nghiệm thu bố cục trong audit này. |
| S3-04 | Có | Có đổi hợp đồng/phòng/tin cùng transaction, truy vấn công khai loại phòng thuê/tin đã cho thuê, audit phòng và test rollback khi lỗi các bước. |
| S3-05 | Có; trải nghiệm một tay chưa xác nhận | Có danh sách tầng/mã, chỉ số trước, kiểm tra giảm, trung bình ba kỳ và xác nhận bất thường, lưu từng phòng/đếm còn thiếu, input số và CSS mobile. Cảnh báo theo ngưỡng tiêu thụ mới >2 lần trung bình; chỉ thực hiện khi đủ 3 kỳ hợp lệ. Chưa nghiệm thu thiết bị thật 360px. |
| S3-06 | Một phần / khác quyết định PO | Có tiền phòng/điện/nước/cố định, chọn giá ngày chốt, ngày phát hành/hạn +7, bỏ qua thiếu chỉ số, chống trùng **theo hợp đồng/tháng** theo quyết định hiện hành. Chặn THEO_NGUOI. Luồng web hàng loạt tạo Nháp rồi phát hành theo S3-08; cần phân biệt hiệu năng tạo 50 Nháp với phát hành/gửi thông báo cho 50 hóa đơn. |
| S3-07 | Có | Có các dòng chỉ số/số lượng/giá/tiền; tổng/đã trả/còn trả/hạn; quá hạn và ngày trễ theo Việt Nam; danh sách lọc kỳ/trạng thái thanh toán. Bảng thanh toán v23 chỉ phục vụ tổng kết, không chứng minh luồng thu tiền Sprint 4 đã có. |
| S3-08 | Có | Có tạo Nháp chưa gửi, sửa chỉ số/thêm phát sinh/giảm trừ bắt buộc ghi chú, chỉ phát hành mới tạo thông báo, khóa bản phát hành, hủy có lý do/lưu cũ/thay thế, nhật ký sửa trước/sau cùng transaction. Test riêng tại verification/S308 có báo cáo lịch sử, chưa tính là đã chạy lại trong lượt xUnit này. |
| S3-09 | Có | Có kỳ hạn/giá mới mặc định cũ, nối ngày kỳ trước, chặn khoảng trống/chồng lấn, giữ hợp đồng và lịch sử gia hạn/người/giá. Có test giá kỳ gia hạn và kỳ giao cắt; không ghi đè giá hóa đơn đã phát hành. |
| S3-10 | Có | Có tổng/đã chốt/còn thiếu theo tòa và kỳ, danh sách thiếu/quản lý, khóa kỳ khi có phát hành, cảnh báo chủ nhà tới ngày chốt. Khóa được giữ khi bản đã phát hành sau đó bị hủy. |

Bằng chứng: `Controllers/HopDongController.cs`, `HopDongCuaToiController.cs`, `ChiSoDienNuocController.cs`, `TienDoChiSoController.cs`, `HoaDonDichVuController*.cs`, `ThongBaoController.cs`; `Services/HopDongService.cs`, `HopDongPdfService.cs`, `LichSuNguoiOService.cs`, `ChiSoDienNuocService*.cs`, `TienDoChiSoService.cs`, `KyChiSoLockService.cs`, `HoaDonDichVuService*.cs`; `ViewModels/HopDongCreateViewModel.cs`.

## CSDL thực tế và giới hạn

Đọc SQLite ở đường dẫn trong log người dùng: `data/s306-demo/20261008-232715-reset/s306.sqlite` bằng kết nối **mode=ro**. Hiện tại MAX(app_schema_version.version) = **23**, integrity_check = **ok**, foreign_key_check = **0 lỗi**. Khác log trước đó ở v22: DB đã được cập nhật trước khi audit đọc. Audit không chạy updater hoặc initializer trên DB này.

Có bảng hợp đồng/kỳ/người ở/chỉ số/hóa đơn/dòng/thanh toán/thông báo. Không thấy bảng giao dịch cọc, thanh lý hay lịch sử trạng thái phòng. Các bảng này thuộc phần khác của vòng đời; chưa thể coi toàn luồng thu tiền/trả phòng đã hoàn chỉnh. Kiểm tra schema/integrity không thay thế kiểm tra nghiệp vụ và không xác nhận DB này là DB mà tiến trình web hiện tại đang dùng.

Chưa nghiệm thu lại Chrome/Safari 360px, kéo thả/cảm ứng thật, giao nhận SMTP thật, 3G và tải đồng thời production. Không dùng kết quả cũ trong tiến độ làm kết quả chạy mới.

## Kết quả kiểm thử lần này

Build thành công; xUnit **604 test: 587 PASS, 17 FAIL, 0 SKIP**, thời gian kiểm thử 3 phút 33 giây. Test dùng SQLite tạm; output riêng không ghi đè web đang chạy. Lần đầu bị sandbox từ chối ghi output/cache; chạy lại với quyền phù hợp đã hoàn tất.

Phân loại 17 FAIL:

| Nhóm | Số | Bằng chứng và kết luận |
| --- | ---: | --- |
| Kỳ vọng schema cũ | 6 | Hai ca `ReconcileUi17AndDev18PreservesBusinessRows`, `UpgradeV17PreservesMeterRowsDefaultFalseAndReruns`, hai ca `LichHenMigrationTests`, `UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi`: expected 22, actual 23. Đây là kỳ vọng test lỗi thời ở assertion phiên bản; không chứng minh nâng cấp hỏng. |
| Fixture hồ sơ | 1 | `ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit`: tạo bảng hop_dong đã tồn tại; test chưa tới kiểm tra quyền. |
| Chuyển đi | 1 | `DepartureHttpWarningSnapshotsAndFutureUnissuedBill`: không thấy chuỗi “Ghi nhận chuyển đi”, test dừng trước POST. Cần kiểm tra form thực tế, chưa kết luận lưu chuyển đi hỏng. |
| Menu/badge | 2 | `MenuAndEveryModuleRouteMatchMatrixForAllRoles` và `MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio`: thiếu marker menu/aria-label kỳ vọng. Cần đối chiếu DOM và hành vi hiện tại. |
| Quản lý tin | 2 | `ChuNha_DangLaiVaGoTinCuaPhongMinh`: thiếu tiêu đề cũ; `TinQuaHan_TuDongTamAnVaCanhBaoChuNha`: kỳ vọng “tự động chuyển”, view hiện có “tự chuyển” và cảnh báo. Riêng assertion cảnh báo khác câu chữ, không phải thiếu cảnh báo. |
| Tòa/phòng | 2 | `BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection`, `BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms`: helper tạo tòa chỉ gửi tên/địa chỉ/số tầng, nhận 200 thay vì redirect; cần cập nhật dữ liệu form theo trường bắt buộc trước khi dùng kết quả để đánh giá CRUD. |
| Chuỗi/class giao diện | 3 | `GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices`, `RegisterHasPasswordToggleAndConfirmationEmailIsEditable`, `AnonymousGuestViewsNhaTotBrandedHomePage`: thiếu câu phí/class mật khẩu/liên kết tin kỳ vọng. Không tự quy tất cả thành lỗi nghiệp vụ hoặc bỏ qua. |

Các nhóm đáng chú ý: 13/13 AuthTests PASS; 99/99 test thuộc lớp ContractCreationTests PASS; 11/11 ContractActivationTests PASS; 18/18 HopDongCuaToiTests PASS; RoomServicesTests 110 PASS/1 FAIL; MeterReadingListTests 92 PASS/3 FAIL. Test pass theo mô hình hiện tại **không chứng minh khớp yêu cầu gốc**: ví dụ vẫn chưa có cửa sổ đếm sai 15 phút dù 13 test Auth pass.

TRX đầy đủ: `data/criteria-audit-results/criteria.trx` (artifact local không commit). Lệnh:

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -p:BaseOutputPath=D:/DevSprint2/TTCS_T926_K12C3_N1/data/criteria-audit-output/ --logger 'trx;LogFileName=criteria.trx' --results-directory data/criteria-audit-results
```

Không sửa mã nghiệp vụ, test, cấu hình hay dữ liệu đang dùng trong lượt rà soát này.
