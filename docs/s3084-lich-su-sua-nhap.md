# S3-08 phần 4 — Lịch sử sửa hóa đơn Nháp

Nhánh `feature/S3-08/04-invoice-draft-history` kế thừa commit phần 3 `46ef9ca`. Bổ sung lịch sử, giữ luồng tạo Nháp, tính tiền, phát hành, hủy và tạo bản thay thế hiện có.

## Quy tắc đã triển khai

- Một bản `SUA_NHAP` trong bảng `nhat_ky_hoat_dong` hiện có cho mỗi lần lưu **có thay đổi**. Ghi trước/sau tổng tiền và các dòng; chỉ số đầu/cuối, sản lượng, đơn giá, thành tiền, loại/tên khoản phát sinh hoặc giảm trừ, ghi chú. Lưu tên/người sửa và thời điểm UTC; giao diện hiển thị giờ Việt Nam. Đây là phạm vi theo yêu cầu story; không ghi dữ liệu tài khoản, mật khẩu hoặc giấy tờ.
- Một lần lưu nhiều thay đổi vẫn là một bản lịch sử. Bấm lưu khi không thay đổi không tăng phiên bản và không thêm bản rỗng. Yêu cầu sai quyền, dữ liệu không hợp lệ hoặc phiên bản cũ không ghi lịch sử.
- Lịch sử và sửa hóa đơn cùng transaction SQLite; ghi lịch sử thất bại rollback tiền, dòng, phiên bản và audit của lần lưu đó. Hai lần lưu đồng thời cùng phiên bản chỉ có một lần thành công.
- Mục **Lịch sử sửa hóa đơn Nháp** nằm cuối phần nội dung chi tiết, trước cột thông tin. Mới nhất ở trên, mỗi lần có người sửa, thời gian, tổng tiền trước → sau và những dòng đã thay đổi. Ghi chú được Razor mã hóa HTML.
- Chủ nhà chỉ xem lịch sử hóa đơn thuộc tòa của mình. Khách thuê không nhận lịch sử sửa nội bộ qua trang thông báo. Các bản nhật ký chỉ thêm, không sửa/xóa, kể cả sau phát hành hoặc hủy. Hóa đơn thay thế có lịch sử riêng; mở liên kết về bản cũ để xem lịch sử bản cũ.
- Không tạo bảng/cột, không đổi phiên bản schema (vẫn v22), không viết lại migration hoặc tính lại dữ liệu cũ. Những lần sửa trước khi có phần 4 chưa có bản tổng hợp `SUA_NHAP`; giữ nguyên audit cũ và không suy đoán/backfill lịch sử thiếu ghi chú. Nhãn trống là “Chưa có lần sửa Nháp được ghi nhận”.

## Test trên web

1. Mở `http://localhost:5249`, đăng nhập Chủ nhà bằng tài khoản demo trước đó. Mở hóa đơn Nháp từ danh sách. Nếu hóa đơn đã phát hành/hủy, tạo bản Nháp thay thế qua luồng phần 3.
2. Ghi lại chỉ số cuối và tổng tiền, tăng chỉ số cuối rồi bấm **Lưu nháp và tính lại tổng**. Kéo xuống mục **Lịch sử sửa hóa đơn Nháp**: có một lần lưu với người sửa, giờ, chỉ số trước/sau, sản lượng và tiền trước/sau.
3. Sửa lần thứ hai, lưu và kiểm tra xuất hiện bản mới phía trên; bản cũ vẫn nguyên.
4. Thêm phát sinh có tên, số tiền dương và ghi chú. Lưu; kiểm tra “Trước: chưa có khoản này”, loại khoản, thành tiền và ghi chú. Làm tương tự với giảm trừ; tổng tiền giảm đúng.
5. Bấm lưu mà không thay đổi: không thêm bản lịch sử. Thử khoản thiếu ghi chú: bị từ chối và không thêm lịch sử.
6. Mở cùng Nháp ở hai tab, lưu thay đổi ở tab đầu rồi thử lưu tab cũ: tab sau báo dữ liệu đã đổi; chỉ lần thành công được ghi.
7. Phát hành; kiểm tra nội dung lịch sử giữ nguyên. Hủy với lý do hợp lệ; lịch sử vẫn nguyên. Tạo bản thay thế và sửa: lịch sử mới thuộc bản thay thế, không ghi đè bản hủy.
8. Đăng nhập khách thuê, mở hóa đơn từ thông báo: hóa đơn chỉ đọc và không hiển thị lịch sử sửa nội bộ.

Web sử dụng bản sao mới dưới `data/s308-demo/`, không dùng CSDL gốc hay ghi đè demo trước. Chạy lại bằng `./verification/Start-S308Demo.ps1`. Tài khoản/mật khẩu nằm trong bộ demo bị Git ignore; không ghi vào tài liệu này.

## Kiểm tra tự động

```powershell
dotnet run --project verification/S308/S308.csproj -p:NuGetAudit=false -p:OutputPath="$pwd/data/s3084-check/" -- .
dotnet test verification/S308Regression/Regression.csproj -p:NuGetAudit=false -p:OutputPath="$pwd/data/s3084-regression/" --logger 'trx;LogFileName=s3084.trx' --results-directory data/s3084-regression-results
```

193 kiểm tra S3-08 PASS, gồm các phần 1–3 và phần lịch sử: bản trước/sau, ghi chú, thứ tự, actor/UTC, lưu rỗng, stale/race, rollback riêng khi insert `SUA_NHAP` thất bại, quyền chéo, append-only SQL, mã hóa HTML, giao diện 360px, giữ lịch sử sau phát hành/hủy và không lộ lịch sử qua trang khách. Log: `data/s3084-verified.log`; ảnh: thư mục tạm được in trong log. Không thay đổi `UnitTest1.cs` sẵn có của người dùng.

Bộ hồi quy toàn dự án: **560 PASS, 9 FAIL / 569**, một lần chạy hoàn tất trong 8 phút 26 giây (`data/s3084-regression-results/s3084.trx`). Đối chiếu theo tên test với kết quả phần 3: **0 test lỗi mới**. Bộ nhật ký hiện có chạy riêng trên mã cuối: **10/10 PASS** (`data/s3084-audit-results/audit.trx`). Các FAIL cũ:

- `MenuAndEveryModuleRouteMatchMatrixForAllRoles`.
- `AnonymousGuestViewsNhaTotBrandedHomePage`.
- `BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms`.
- `RegisterHasPasswordToggleAndConfirmationEmailIsEditable`.
- `GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices`.
- `ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit`.
- `DepartureHttpWarningSnapshotsAndFutureUnissuedBill`.
- `BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection`.
- `MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio`.

Các lỗi trên nằm ngoài phần lịch sử Nháp; giữ phạm vi, không sửa chức năng gốc không liên quan. Xem [đối chiếu baseline phần 3](s3083-huy-phat-hanh-lai.md).

CSDL gốc giữ nguyên SHA256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Demo trước cũng giữ SHA256 `F564458B432D87B0F3505938901BB5FCB2F591CFD8B01A3D77B462237FF83C2C`. Giới hạn: chưa kiểm chứng Safari, SMTP thật, tải production hoặc merge với những commit khác được thêm sau này; không thể khẳng định mọi bug của toàn dự án đã hết. Không commit/push phần 4 khi chưa được yêu cầu.
