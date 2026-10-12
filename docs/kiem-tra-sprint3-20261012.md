# Kiểm tra 44 tiêu chí Sprint 3 — 12/10/2026

**Lưu ý:** đây là kết quả trước khi sửa. Sau yêu cầu tự hoàn thiện, các lỗi đã được xử lý và kiểm tra lại; xem [kết quả hoàn thiện](hoan-thien-sprint3-20261012.md).

Nguồn tiêu chí: tệp Pasted text.txt người dùng gửi trong phiên này (S3-01…S3-10). Đối chiếu mã hiện tại, chạy test trên SQLite tạm và tái hiện riêng các lỗi. Không sửa mã nghiệp vụ, schema, test gốc hoặc database đang dùng. Ghi chú “Sprint 3 chưa làm” trong AGENTS.md đã lỗi thời: mã có triển khai cả 10 story.

**Chưa thể xác nhận Sprint 3 đạt đầy đủ.** Có lỗi luồng chuyển đi trên giao diện, thiếu snapshot dịch vụ khi tạo hợp đồng và lỗi trang chi tiết trên DB hợp lệ chưa cài module hóa đơn. Giao diện chốt chỉ số 360px và PDF chưa được nghiệm thu trực quan trong lần này. Các test hiện tại cũng cần cập nhật fixture/kỳ vọng.

## Cách đọc kết quả

- **Đạt mã/test:** đã có triển khai và bằng chứng mã hoặc test phù hợp; không thay cho nghiệm thu thủ công, review/hợp nhất hoặc thiết bị thật.
- **Một phần / chưa đạt:** có khoảng trống hoặc lỗi cụ thể.
- **Chưa xác minh:** đã có mã nhưng bằng chứng runtime bị chặn hoặc chưa kiểm tra UX thực tế.
- **Khác đặc tả:** hành vi khác câu chữ bản gửi; cần áp dụng quyết định hiện hành hoặc làm rõ chính sách, không tự sửa.

## Các lỗi và khoảng trống cần xử lý

1. **S3-02 AC4 — mất thao tác chuyển đi trên web.** `Views/HopDong/Details.cshtml` mở tab people qua `_Residents.cshtml`; partial này chỉ nạp form `_ResidentCreate`, không render danh sách người ở hoặc `_NguoiOGhep` chứa form ChuyenDi. Backend ghi chuyển đi và tính giảm phí tháng sau còn tồn tại, nhưng chủ nhà không thao tác được từ tab này. Test `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` thất bại vì không còn nút “Ghi nhận chuyển đi”.
2. **S3-03 AC2 — hợp đồng mới không có bảng dịch vụ/đơn giá trong PDF.** Luồng `HopDongController.Create` đọc giá dịch vụ vào `PhiDichVus` để xem trước, nhưng không lưu `HopDongDichVus`. Trang khách/PDF chỉ đọc snapshot `hop_dong_dich_vu`. Tái hiện trên SQLite tạm: phòng có 1 dịch vụ Internet được gán, form hiển thị 1 dịch vụ; tạo hợp đồng thành công; số snapshot dịch vụ hợp đồng = 0, chi tiết chủ nhà có 0 dịch vụ. `GanVaoHopDongAsync` có khả năng ghi snapshot nhưng không được gọi từ luồng tạo hợp đồng hoặc controller khác. Test PDF hiện tại tự seed snapshot nên PASS không chứng minh luồng tạo → PDF đầy đủ.
3. **Hợp đồng/người ở/kích hoạt — lỗi khi chưa cài module hóa đơn tùy chọn.** `HopDongController.DetailModel` truy vấn `HopDongDichVus` vô điều kiện ở dòng 352. Đã tái hiện trên DB tạm v25 được `DatabaseUpdates.Check` chấp nhận: tạo hợp đồng thành công, mở Details ném `no such table: hop_dong_dich_vu`. Các luồng trả lại Details sau validation hoặc rollback cũng gặp lỗi này. 32 test thất bại tại cùng truy vấn; đây vừa là vấn đề thiết lập test vừa là lỗi tương thích với DB hợp lệ không có module hóa đơn, không thể chỉ bỏ qua là lỗi fixture.
4. **S3-10 AC4 — cần khôi phục/xác minh cảnh báo tới ngày chốt.** Service vẫn tính `MeterAlerts` theo ngày chốt và số phòng thiếu. Dashboard hiện render thẻ “Phòng chưa chốt điện nước” cho kỳ đang chọn và cộng số thiếu vào tổng việc, nhưng không render danh sách cảnh báo tới hạn theo tòa/kỳ từ `MeterAlerts`. Test cảnh báo tới ngày chốt thất bại. Không kết luận dashboard hoàn toàn thiếu thông tin chốt chỉ số; thiếu bằng chứng cảnh báo rõ ràng theo đúng AC tới ngày chốt.
5. **Bộ kiểm thử chưa theo schema/giao diện mới.** 11 test dựng schema cũ bằng cách xóa marker phiên bản nhưng giữ cột v24, bị updater từ chối đúng nguyên tắc. Một test thanh toán bỏ bảng nhưng giữ marker mới. Một số test HTTP còn kỳ vọng nhãn/route trước khi đổi giao diện. Không sửa updater hoặc dữ liệu thật để ép test qua.

## Đối chiếu từng tiêu chí

| Story / AC | Tiêu chí rút gọn | Kết quả | Bằng chứng / giới hạn |
| --- | --- | --- | --- |
| S3-01.1 | Lấy khách/phòng từ yêu cầu duyệt; nhập cọc, giá, ngày, tháng, ngày chốt | Đạt mã/test | Approved, Create GET/POST và HopDongCreateViewModel. Có nhận giá/cọc thỏa thuận nguyên đồng. |
| S3-01.2 | Tự tính kết thúc, không sửa tay | Đạt mã/test | Getter không có setter; AddMonths(months).AddDays(-1), POST tính server. Có test cuối tháng/năm nhuận. |
| S3-01.3 | Chặn chồng lấn và chỉ hợp đồng vướng | Đạt mã/test | HopDongService.ChongLanAsync, khoảng bao gồm hai đầu; kiểm tra trong transaction ghi ngay; form hiển thị mã/khoảng vướng. |
| S3-01.4 | Cọc mặc định 1 tháng, trong 0…3 tháng giá thuê | Đạt mã/test | Prefill mặc định bằng giá thuê, kiểm tra decimal cọc ≤ 3 × giá chốt; test biên và tiền tùy ý PASS. |
| S3-01.5 | Mã HD-yyyy-xxxx, phòng Đang thuê, chỉ số đầu kỳ | Đạt mã/test | SinhMaAsync; Create lưu kỳ/chỉ số/phòng và audit cùng transaction. “Lưu nháp” cố ý chưa kích hoạt; nút Tạo hợp đồng thực hiện AC. |
| S3-02.1 | Đúng một người đứng tên | Đạt mã/test | KhachDungTenId; không cho lặp người đứng tên trong danh sách ở ghép. Luồng hoàn cọc thực tế thuộc Sprint 4. |
| S3-02.2 | Thêm người: tên, SĐT, CCCD, ngày vào | Có mã; kiểm thử chưa xanh | Form Input và ThemNguoi có kiểm tra server. Nhiều test validation/HTTP bị lỗi bảng dịch vụ tùy chọn hoặc route/kỳ vọng cũ. |
| S3-02.3 | Sức chứa gồm đứng tên + ở ghép; thông báo giới hạn | Có mã; kiểm thử chưa xanh | ThemNguoi kiểm tra số người tại các mốc ngày và phiên bản phòng; các test vượt giới hạn bị lỗi DetailModel khi trả trang lỗi trên DB thiếu module. |
| S3-02.4 | Ghi chuyển đi, giảm khoán kỳ sau | Chưa đạt luồng web | Backend và test tính phí giữ tháng chuyển đi/giảm tháng sau có triển khai; tab people không render nút chuyển đi. |
| S3-02.5 | Tra lịch sử theo khoảng thời gian | Đạt mã; test HTTP cần cập nhật | LichSuNguoiOService, action LichSuNguoiO và view có ngày vào/ra/bộ lọc. Test còn kỳ vọng tiêu đề cũ “Lịch sử người ở”. |
| S3-03.1 | Chi tiết mã, phòng, giá, cọc, ngày, người ở | Đạt mã/test | HopDongCuaToiController.LoadContractDetailsAsync, Details và test khách đứng tên/ở ghép PASS. |
| S3-03.2 | PDF đủ thông tin và dịch vụ/đơn giá | Một phần | Generator và test PDF với snapshot tự seed PASS; hợp đồng tạo qua web chưa lưu snapshot dịch vụ. Chưa render QA PDF. |
| S3-03.3 | Người đứng tên/ở ghép; người ngoài 403 | Đạt mã/test | LoadContractDetailsAsync kiểm tra profile/sở hữu trước Details, DownloadPdf và preview; test người ngoài PASS. |
| S3-03.4 | Cảnh báo dưới 30 ngày, kèm ngày còn lại | Đạt mã/test | SapHetHan = 0…29 ngày; hôm nay có thông báo riêng. Tests 0/1/29/30/31 và quá hạn PASS. |
| S3-04.1 | Hợp đồng/phòng/tin đổi cùng transaction | Đạt mã/test luồng thành công | Create và KichHoat dùng BEGIN IMMEDIATE; ActivationSyncsContractRoomAndAllListings PASS. |
| S3-04.2 | Tin thuê không hiện công khai lần tải tiếp | Đạt mã; nghiệm thu chưa đầy đủ | Truy vấn công khai kiểm tra tin, hạn, phòng TRONG, tòa hoạt động. Test nghiệm thu kết hợp trang chủ bị NullReference do kỳ vọng model cũ. |
| S3-04.3 | Lỗi một bước hoàn tác tất cả | Chưa xác minh đầy đủ lượt này | Transaction có triển khai; test giả lỗi hợp đồng/phòng/tin không hoàn thành vì nhánh xử lý lỗi gọi DetailModel thiếu bảng dịch vụ. |
| S3-04.4 | Nhật ký mọi lần đổi trạng thái phòng | Đạt mã/test luồng thành công | SaveChanges audit và transaction; test ghi snapshot kích hoạt thành công có PASS. Nhánh rollback còn lỗi nêu trên. |
| S3-05.1 | Phòng thuê theo tầng/mã, chỉ số trước | Đạt mã/test | DanhSachAsync sắp tầng/mã, lấy kỳ trước/hóa đơn cũ/bàn giao; test danh sách PASS. |
| S3-05.2 | Không giảm; báo lỗi tại dòng | Đạt mã/test | LuuAsync kiểm tra cuối ≥ trước; Save giữ input/lỗi theo phòng; test HTTP mobile PASS. |
| S3-05.3 | Cảnh báo >200% trung bình 3 kỳ và xác nhận | Đạt theo công thức đang triển khai | So sánh tiêu thụ mới > 2 × trung bình, chỉ khi đủ 3 kỳ hợp lệ; token xác nhận ràng buộc dữ liệu, hết hạn 15 phút. Công thức/thiếu lịch sử trong AGENTS.md vẫn chờ PO. |
| S3-05.4 | Lưu từng phòng, đếm chưa chốt | Đạt mã/test | Mỗi phòng một POST; SoPhongDaChot/SoPhongConLai; test stale/concurrent/rollback không đếm sai PASS. |
| S3-05.5 | Một tay 360px, bàn phím số | Chưa nghiệm thu UX | Có type=number/inputmode=decimal/CSS mobile và test HTML PASS. Chưa đo tràn ngang/thao tác một tay/Safari/thiết bị thật lượt này. |
| S3-06.1 | Phòng, điện nước, cố định, theo người | Đạt mã/test | Service tính theo decimal, làm tròn từng dòng, phí người từ lịch sử; test khoản phí/người/snapshot PASS. Luồng web tạo Nháp để kiểm tra rồi phát hành cả nhóm. |
| S3-06.2 | Giá có hiệu lực ngày chốt, giữ giá cũ | Đạt mã/test | LayGiaHoaDonAsync theo cutoff của hợp đồng; snapshot dòng, test sửa giá sau không đổi hóa đơn PASS. |
| S3-06.3 | Kỳ, phát hành, hạn mặc định +7; sửa trước phát hành | Đạt mã/test | NgayHoaDon, Monthly và PublishDraft/PublishMonthlyDrafts cho chỉnh ngày/hạn, tách ngày nghiệp vụ khỏi timestamp UTC. |
| S3-06.4 | Bỏ qua thiếu chỉ số, liệt kê lý do | Đạt mã/test | XemThangAsync/Results có lý do theo phòng; THEO_NGUOI không đòi công tơ; test PASS. |
| S3-06.5 | Không trùng, 50 phòng <30s | Đạt theo quyết định hiện hành; khác câu chữ | Unique một bản chưa hủy/hợp đồng/tháng, không phải phòng/tháng. Test HTTP phát hành 50 Nháp và service đồng thời PASS, toàn test lần lượt khoảng 4,49s và 1,88s. Hai khách nối tiếp cùng phòng có hóa đơn riêng. |
| S3-07.1 | Dòng chỉ số, tiêu thụ, giá, tiền | Đạt mã/test | ThongBaoController.HoaDon, view và InvoiceTenantDetailsTests PASS. |
| S3-07.2 | Tổng, đã trả, còn trả, hạn | Đạt mã/test | Thanh toán xác nhận và cấn cọc được đọc vào tổng kết; InvoiceTenantBalanceTests PASS. Không suy rằng luồng thu tiền Sprint 4 hoàn chỉnh. |
| S3-07.3 | Quá hạn/số ngày trễ theo giờ Việt Nam | Đạt mã/test | Ngày Việt Nam qua clock +7; test biên ngày/hạn và trạng thái PASS. |
| S3-07.4 | Lọc kỳ/trạng thái thanh toán | Đạt mã/test | Danh sách khách có tháng/trạng thái, phân trang, giữ query; InvoiceTenantListTests PASS. |
| S3-08.1 | Nháp; phát hành mới thông báo | Đạt core mã/test | IssueMonthly tạo Nháp; Publish tạo thông báo cùng transaction; email sau commit. Suite core kiểm tra không gửi từ Nháp và không gửi trùng. |
| S3-08.2 | Sửa chỉ số, thêm phát sinh/giảm trừ có ghi chú | Đạt core mã/test | LuuNhapAsync, _DraftEditor; validation/decimal/giảm trừ/phiên bản và HTTP CSRF được kiểm tra trong runner riêng. |
| S3-08.3 | Đã phát hành bất biến; hủy lý do/phát hành lại, giữ cũ | Đạt core mã/test | Cancel/Publish, trigger, thay thế chống trùng; runner core kiểm tra audit rollback, giữ nội dung/mã, thông báo mới và một bản chưa hủy. |
| S3-08.4 | Sửa Nháp audit trước/sau | Đạt core mã/test | History ghi cùng transaction; runner core kiểm tra trước/sau, no-op, stale, concurrent, lỗi audit và append-only. |
| S3-09.1 | Tháng/giá mới mặc định giữ cũ | Đạt mã | GiaHanModelAsync, POST và form GiaHan. |
| S3-09.2 | Nối ngày, không khoảng trống/chồng lấn | Đạt mã | Server lấy kỳ cuối +1 ngày, tính kết thúc, kiểm tra ChongLan trong transaction. Không nhận ngày bắt đầu tùy ý từ form. |
| S3-09.3 | Giá mới từ kỳ hóa đơn trong khoảng gia hạn | Khác cách diễn đạt; cần rõ kỳ giao cắt | Mã chia tiền phòng theo từng đoạn ngày: gia hạn 15/10 thì hóa đơn tháng 10 dùng giá cũ 14 ngày và giá mới 17 ngày. Test RenewalPricing PASS. Nếu AC muốn đợi tháng 11 mới đổi giá thì hiện chưa khớp; AGENTS.md cũng liệt kê chính sách giao cắt là câu hỏi PO. |
| S3-09.4 | Lịch sử từng lần/người/giá | Đạt mã | DetailModel.LichSuGiaHan và _ContractOverview render thứ tự, người, giờ, giá, khoảng kỳ. |
| S3-10.1 | Tổng/đã chốt/còn thiếu theo tòa/kỳ | Đạt mã/test | TienDoChiSoService, tính theo ngày/kỳ; THEO_NGUOI được xử lý đúng; test PASS. |
| S3-10.2 | Mở phòng thiếu và quản lý phụ trách | Đạt mã/test | PhongConThieuAsync, _MissingRooms, quyền chủ tòa; test cả quản lý có/chưa phân công PASS. |
| S3-10.3 | Khóa kỳ sau phát hành; không sửa chỉ số | Đạt mã/test | KyChiSoLockService lấy kỳ tòa đã phát hành; LuuAsync từ chối, nguồn chỉ số khóa. Test 50 hóa đơn khóa 100 chỉ số PASS. |
| S3-10.4 | Đến ngày chốt mà thiếu thì cảnh báo trang chủ | Một phần / cần xác minh lại UI | MeterAlerts vẫn tính, nhưng view không render cảnh báo tới hạn riêng theo tòa/kỳ; chỉ có thẻ phòng thiếu chung/tổng việc. Test HTTP cảnh báo tới ngày chốt FAIL. |

## Kết quả chạy thực tế

### xUnit

Build ứng dụng/test thành công, còn cảnh báo license ImageSharp. Bộ lọc liên quan hợp đồng/người ở/chỉ số/hóa đơn/chốt kỳ: **370 ca, 319 PASS, 51 FAIL, 0 SKIP**, khoảng 3 phút 39 giây. Bộ lọc cũng lấy một số ca hạ tầng dùng chung; không gọi đây là toàn bộ test của repo.

Phân nhóm 51 FAIL:

| Nhóm | Số | Diễn giải |
| --- | ---: | --- |
| hop_dong_dich_vu chưa cài | 32 | Fixture hợp đồng không cài hóa đơn; DetailModel hiện truy vấn bảng tùy chọn vô điều kiện. Đã tái hiện trên DB hợp lệ, là lỗi tương thích cần sửa. |
| Fixture schema cũ nhưng giữ cột v24 | 11 | Updater từ chối marker không khớp cột; cần dựng đúng schema cũ để test upgrade. |
| Fixture thanh_toan bị xóa nhưng marker mới còn | 1 | Kiểm tra schema từ chối; không chứng minh updater v24→v25 lỗi. |
| HTTP/HTML/model | 7 | Thiếu nút chuyển đi; cảnh báo dashboard; nhãn/form/hồ sơ che số/route đổi; NullReference model trang chủ trong test nghiệm thu. Không coi tất cả là lỗi nghiệp vụ. |

Lệnh đã chạy:

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~Contract|FullyQualifiedName~Roommate|FullyQualifiedName~Occupant|FullyQualifiedName~HopDongCuaToi|FullyQualifiedName~Meter|FullyQualifiedName~InvoiceTenant|FullyQualifiedName~RoomServicesTests|FullyQualifiedName~S304NghiemThu' --logger 'trx;LogFileName=sprint3-audit-20261012.trx' --results-directory data/sprint3-audit-20261012
```

### Bộ kiểm tra riêng S3-08

- Chạy **suite gốc** `dotnet run --project verification/S308 --no-restore -- .`: 65 assertions PASS trước khi dừng ở fixture mô phỏng v20 nhưng giữ cột v24; **suite gốc không PASS toàn bộ**.
- Để kiểm tra tiếp core, tạo runner tạm trong `data/sprint3-audit-20261012/s308-core`, dùng mã kiểm tra có sẵn, bỏ hai đoạn migration fixture v20/v21 đã lỗi thời, thay bảng thanh toán tạm mới bằng bảng giả mà test hủy đang kỳ vọng. Không sửa test hoặc mã ứng dụng gốc. Runner dừng trước host kiểm tra browser thứ hai: **115 assertions PASS**, exit 0; bao gồm sửa Nháp, HTTP form/CSRF/quyền, phát hành/thông báo/email giả, hủy/thay thế, lịch sử/concurrent/rollback. Hai nhóm migration và browser chưa được xác nhận bởi runner này.
- Không cộng 65/115 assertions vào 319 xUnit PASS: khác đơn vị đo và có phần trùng.

### Tái hiện độc lập

`data/sprint3-audit-20261012/repro` dùng fixture SQLite tạm của test hiện có, bổ sung module hóa đơn và gán dịch vụ vào phòng rồi gọi controller tạo hợp đồng. Quan sát 1 dịch vụ ở form nhưng 0 snapshot sau lưu. Lượt khác dùng DB tạm không cài module hóa đơn: Check thành công nhưng Details lỗi bảng dịch vụ. Không chạy updater/initializer trên DB người dùng.

Kết quả chi tiết nằm trong thư mục đã ignore `data/sprint3-audit-20261012`: TRX, failures.json, s308.log, s308-core.log và repro.log. Database local được đọc metadata cuối lượt và đang ở v25; lần kiểm tra này không nâng cấp hay thay đổi nó.

## Việc còn lại trước nghiệm thu

Ưu tiên khôi phục danh sách/thao tác chuyển đi, lưu dịch vụ hợp đồng đúng giá phòng tại thời điểm thỏa thuận, bảo vệ truy vấn module tùy chọn, rồi làm rõ cảnh báo tới hạn và chính sách giá khi gia hạn giữa tháng. Sau đó cập nhật fixture schema/route/nhãn, chạy lại các ca lỗi, kiểm tra luồng tạo hợp đồng → khách tải PDF và UX 360px. Chưa thực hiện review/hợp nhất, kiểm tra Safari/thiết bị thật hoặc gửi SMTP thật trong lần này.
