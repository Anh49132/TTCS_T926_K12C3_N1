# S4-05 phần 1 — Tự động giải phóng phòng

Nhánh `feature/S4-05/01-auto-release-room`, nền `edef5ea` (cùng commit dev lúc tải ngày 15/10/2026). Thực hiện qua luồng **Trả phòng → Bảng tất toán → Xác nhận hoàn tất** của S4-04; không thêm nút đổi trạng thái hợp đồng trực tiếp để bỏ qua kiểm tra tài chính.

## Điều kiện và giao dịch

- Chủ nhà đang hoạt động, sở hữu tòa chứa hợp đồng/phòng, có quyền ghi cả HOP_DONG và TAI_CHINH. Khách/Quản lý/ADMIN/chủ nhà khác không kết thúc hợp đồng.
- Hợp đồng đang DANG_HIEU_LUC, phòng đang DANG_THUE và không có hợp đồng khác đang hiệu lực cùng phòng. Có đúng hóa đơn kỳ cuối, thông tin trả phòng và bảng tất toán Nháp hợp lệ; không còn khoản thanh toán chờ hoặc Nháp khác chưa xử lý. Chỉ số, điều khoản chấm dứt, cọc, công nợ, khấu trừ và fingerprint/phiên bản vẫn khớp dữ liệu hiện tại.
- Xác nhận đã kiểm tra và đã đối chiếu hai bên. Giữ quy tắc S4-04: chốt nghĩa vụ không bắt buộc đã thu/hoàn hết tiền; công nợ còn lại vẫn thu/hoàn sau chốt.
- `HoanTatAsync` giữ transaction SQLite immediate hiện có. `GiaiPhongPhongAsync` chỉ chạy trong transaction này, kiểm tra lại quyền và trạng thái trước khi cập nhật DA_KET_THUC, ngày trả, TRONG và tăng phiên bản phòng đúng một lần. Lỗi phòng/audit hoặc bước khác rollback toàn bộ giao dịch, không để hợp đồng kết thúc riêng lẻ.
- Gửi lại yêu cầu đã hoàn tất trả lỗi 409/đã xác nhận theo luồng cũ, không ghi/cấn cọc/giải phóng lần nữa. Không giải phóng lại phòng đã cho thuê tiếp từ yêu cầu cũ. Gửi đồng thời chỉ một lần chốt thành công.

Nền dev **đã có** cập nhật hợp đồng/phòng và tạo tin Nháp trong S4-04. Phần này hoàn thiện guard và giao diện trên luồng đó, giữ hành vi tạo tin có sẵn; không triển khai thêm nhân bản, bật tin hoặc danh sách cần đăng của các phần S4-05 tiếp theo. Không đổi schema v33, migration hay CSDL gốc.

## Giao diện và test nhanh

Sau hoàn tất, màn hình kết quả hiển thị hợp đồng **Đã kết thúc**, phòng tại thời điểm trả **Trống**, cùng **Phòng hiện tại** đọc từ CSDL. Có **Làm mới trạng thái phòng** và **Xem phòng**. Nếu phòng đã cho thuê tiếp/ngừng cho thuê, kết quả hợp đồng cũ vẫn hiển thị đúng trạng thái hiện tại, không ghi đè lại thành Trống. Endpoint hiện có dùng `no-store`.

```powershell
./verification/New-S405Demo.ps1
./verification/Start-S405Demo.ps1
```

Mỗi lần tạo dùng thư mục mới trong `data/s405-demo/`. Tài khoản/mật khẩu sinh riêng, lưu tại `access.json`; file pointer `data/s405-demo/latest.txt` chỉ đúng bộ mới. Không đưa credentials lên Git. Web mặc định cổng **5251**. Bộ demo khác bộ S3-08; dùng tài khoản `Owner` và mật khẩu `Password` trong access.json của bộ này.

1. Mở `http://localhost:5251/TatToan/XacNhan/1`, đăng nhập Chủ nhà. Hợp đồng 1 đã có hóa đơn cuối/bảng tất toán Nháp và sẵn sàng xác nhận.
2. Kiểm tra các khoản, chọn **Tôi đã kiểm tra…**, bấm **Xem trước & xác nhận**.
3. Chọn xác nhận đã đối chiếu hai bên, bấm **Hoàn tất trả phòng**. Trang kết quả phải có **Đã kết thúc**, **Phòng hiện tại: Trống**.
4. Bấm **Làm mới trạng thái phòng**, rồi **Xem phòng A103**; trạng thái vẫn Trống. Mở chi tiết hợp đồng kiểm tra Đã kết thúc.
5. Thử gửi lại biểu mẫu cũ ở tab trước: nhận thông báo dữ liệu/đã xác nhận, không thay đổi trạng thái hoặc tạo thêm giao dịch. Sau mỗi lần test toàn luồng cần bộ demo mới.
6. Đăng nhập Tenant của cùng bộ, xem `/TatToan/ChiTiet/1`: chỉ đọc, không có thao tác kết thúc. ForeignOwner/ForeignTenant bị chặn xem và sửa hợp đồng này.

Lỗi cập nhật phòng và race được mô phỏng bằng test tự động trên SQLite tạm; không tạo trigger lỗi trong demo hoặc CSDL gốc.

## Kiểm chứng

Kiểm thử bổ sung theo yêu cầu: **28/28 ca S4-05 PASS**; lượt toàn dự án **901/923 PASS**, 22 FAIL đều tái hiện trên commit gốc. Tổng hợp các ca không trùng là 906 PASS/22 FAIL trên 928; không phát hiện test lỗi mới. [Ma trận kiểm thử](s4051-ma-tran-kiem-thu.md), [báo cáo chi tiết](s4051-ket-qua-kiem-thu.md). Các số liệu bên dưới ghi lại lượt kiểm tra đầu trước khi bổ sung ca mới.

```powershell
dotnet restore verification/S308Regression/Regression.csproj --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
dotnet test verification/S308Regression/Regression.csproj --no-restore -p:OutputPath="$pwd/data/s405-check/" --filter 'FullyQualifiedName~AutoReleaseRoom|FullyQualifiedName~XacNhan|FullyQualifiedName~TatToan'
```

- **93/93 PASS**, gồm 10 test S4-05 mới và test tất toán/xác nhận/PDF hiện có. Có test rollback toàn bộ khi trigger cập nhật phòng thất bại, quyền chéo, trạng thái phòng không hợp lệ, room version cũ, gửi lặp, đọc trạng thái mới khi phòng đã đổi và HTTP làm mới. Log `data/s405-related2.log`, TRX `data/s405-related-results/related.trx`.
- Kiểm tra mở rộng kích hoạt/chấm dứt/trả phòng: **35 PASS, 1 FAIL / 36** (`data/s405-integration-results/integration.trx`). Tổng hai nhóm không trùng test: **128 PASS, 1 FAIL / 129**. Test `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` không tìm thấy chuỗi giao diện “2 người” trên trang hóa đơn (line 35); các assert dữ liệu hóa đơn trước đó vẫn đạt. Đã chạy đúng test trên bản sao `git archive HEAD` của nền edef5ea, không có mã S4-05: FAIL cùng chuỗi/cùng line 35 (`data/s405-baseline-results/baseline.trx`). Đây là lỗi/kỳ vọng UI cũ, không phải hồi quy do S4-05; không sửa giao diện hóa đơn ngoài phạm vi.
- Trình duyệt Edge headless kiểm tra thực tế xác nhận, trạng thái Trống/làm mới, hợp đồng Đã kết thúc, gửi lại 409, khách chỉ đọc và trái quyền 403. Desktop/360px đạt, không tràn ngang. Log `data/s405-browser2.log`; artifacts trong bộ demo kiểm thử `browser-check-26b6aa5e` (tách bộ mới cho người dùng).
- Project verification bổ sung PdfPig 0.1.11 vì các test PDF mới từ dev đã dùng nó nhưng project chạy test thiếu reference. Dependency ứng dụng giữ nguyên; QuestPDF có sẵn trong dev được restore. Script PowerShell mới dùng UTF8 BOM để chạy tiếng Việt trên Windows PowerShell 5.
- CSDL gốc giữ SHA256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Giữ `UnitTest1.cs` có sẵn. Chưa kiểm chứng Safari, tải production hoặc xung đột với commit được thêm sau này. Chưa commit/push phần này.
