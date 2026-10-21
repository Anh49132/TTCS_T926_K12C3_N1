# S4-06 — Đính kèm ảnh báo hỏng

Phạm vi task 2: khách gửi tối đa 3 ảnh và xem/mở lại ảnh trên chi tiết. Giữ quy tắc hợp đồng, người đứng tên/người ở ghép, mã tháng, trạng thái Mới và quyền xem riêng từ task 1. Chưa triển khai quản lý đổi trạng thái hay phản hồi.

## Quyết định PO ngày 14/10/2026

- Nhận JPG/JPEG và PNG. Mỗi ảnh tối đa **5 MiB = 5.242.880 byte**, nhận đúng giới hạn, từ chối khi vượt. Ảnh không bắt buộc.
- Giao diện kiểm tra số lượng, đuôi tên và dung lượng; máy chủ đọc có giới hạn, kiểm tra định dạng nội dung khớp đuôi và giải mã ảnh. Tệp rỗng, hỏng hoặc giả ảnh bị từ chối. Giới hạn giải mã an toàn 50 megapixel giống kho ảnh phòng hiện có; không bổ sung thư viện.
- Có xem trước và bỏ ảnh; chọn thêm cộng vào danh sách hiện có. Khi lỗi máy chủ, giữ nội dung biểu mẫu và nhắc chọn lại ảnh vì trình duyệt không cho tự phục hồi file input.

## Lưu trữ và quyền

- Updater thêm **v29**, giữ nguyên bước v28. Bảng `anh_bao_hong` lưu báo hỏng, thứ tự 1..3, khóa ngẫu nhiên, content type, số byte và SHA-256. FK RESTRICT; unique báo hỏng/thứ tự bảo đảm không thể lưu quá 3 ảnh. Không đổi bảng nghiệp vụ cũ. Đây là bảng ảnh chuẩn hóa bổ sung cho schema thực tế, không coi DBML tham chiếu là migration.
- Tệp nằm ở `<DatabasePath>.repair-images`, ngoài wwwroot; chỉ endpoint `/BaoHong/Image/{id}?order={1..3}` đọc tệp sau kiểm tra quyền và người gửi báo hỏng. Ảnh khách khác trả 403; ảnh không tồn tại 404; người chưa đăng nhập được chuyển đăng nhập. Response no-store và nosniff, mở ảnh bằng link tab mới.
- Chuẩn bị/kiểm tra toàn bộ ảnh trước khi ghi. Báo hỏng, bộ đếm, metadata ảnh và audit cùng transaction SQLite IMMEDIATE. Lỗi lưu/audit rollback và xóa các tệp vừa ghi; báo lỗi rõ, không tạo yêu cầu thiếu ảnh. Retry cùng khóa so cả nội dung và hash/thứ tự ảnh, không tạo tệp/báo hỏng trùng.
- Tệp gốc được giữ nguyên. Sao lưu cần sao lưu đồng thời DB và thư mục ảnh. Crash giữa ghi tệp và commit có thể để lại tệp mồ côi; chưa có tác vụ dọn tự động, giống giới hạn kho tệp hiện hữu. Không thể bảo đảm giao dịch nguyên tử DB/filesystem khi hệ điều hành dừng đột ngột.

## Chạy demo

Demo ảnh hiện tại nằm trong `data/s406/images-latest.txt` (ignored), URL localhost:5249. Đăng nhập khách demo đã có, mật khẩu Demo@2026. DB local mặc định, demo Sprint2 gốc và demo task1 không bị nâng cấp.

```powershell
$demoFolder = (Get-Content data/s406/images-latest.txt -Raw).Trim()
.\verification\Start-S406Demo.ps1 -Directory $demoFolder
```

Runtime hiện là bản sao DLL Debug đã build trong data/s406-images/runtime. Sau khi sửa mã, dừng đúng server demo trước khi build rồi chạy lại. Máy khác cần tạo bản sao DB v28, đặt DatabasePath của bản sao, chạy updater/check; web yêu cầu v29. Không chạy updater trên DB đang dùng.

## Kiểm chứng

- Build C#/Razor đạt; cảnh báo ImageSharp license và CS8601 hiện hữu.
- 24/24 test báo hỏng/ảnh đạt: gửi 0/1/3, retry, giới hạn đúng/vượt 5 MiB, 4 ảnh, định dạng/giả tệp, sở hữu, lỗi kho ảnh, rollback audit/tệp/bộ đếm; nâng v27 và v28, rerun. Hồi quy thu/hủy thu: 36/36 đạt.
- HTTP thực tế đạt gửi 1/3 ảnh, đúng giới hạn, từ chối 4/vượt/sai định dạng, mô phỏng thư mục ảnh không ghi được và không tạo báo hỏng, retry, byte ảnh gốc, 403 khác khách, anonymous, no-store/nosniff, integrity/FK; có hồi quy gửi không ảnh và CSRF/task1.
- Nâng bản sao v28→v29 giữ toàn bộ 42 bảng/251 bản ghi cũ; integrity=ok, FK=0, chạy lại không đổi. Chỉ bản sao demo được nâng.
- Trình duyệt: từ chối chọn 4 ảnh; chọn 2 ảnh, bỏ 1, chọn lại thêm 2, gửi đủ 3; BH-202610-0013/Mới, cả 3 ảnh tải thành công. Biểu mẫu và chi tiết tại 360px có documentWidth345 ≤ viewport360. Chỉ dùng DOM, **không chụp màn hình**. Chưa nghiệm thu trực quan/Safari/thiết bị thật hay review thành viên khác.
- Bộ rộng RoomServicesTests: 161/164 đạt; hai lỗi text hóa đơn (`MonthlyAllAmountsSnapshotSurvivesChanges`, `DepartureHttpWarningSnapshotsAndFutureUnissuedBill`) cũng tái hiện trên commit trước c4b6fc6. Một ca `TenantInvoiceListIsPrivateSortedPagedAndKeepsFiltersForDetails` lỗi login500 trong lượt rộng, nhưng đạt trên baseline; đạt khi chạy riêng trên mã mới (1/1). Không sửa nghiệp vụ hóa đơn trong task ảnh.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~BaoHongTests|FullyQualifiedName~.Collection'
# Script có ghi dữ liệu; chỉ dùng bộ demo giả mới, chưa có thư mục repair-images:
python verification/Verify-S406ImagesHttp.py $demoFolder
```

Script ảnh nhúng ảnh mẫu tổng hợp, không cần ảnh cá nhân. Báo cáo HTTP nằm trong thư mục demo ignored. Không commit dữ liệu, ảnh demo, mật khẩu hay backup.
