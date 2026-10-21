# S3-06 — Chống tạo trùng và phát hành hàng loạt

## Quyết định PO và phạm vi

PO xác nhận: khi chạy lại, liệt kê riêng **Đã có hóa đơn**, liên kết xem bản hiện có, tiếp tục phát hành cho phòng mới đủ dữ liệu. Giữ khóa nghiệp vụ **một hóa đơn chưa hủy / hợp đồng / tháng**; hai hợp đồng nối tiếp cùng phòng không bị gộp. Không đổi schema (v20), không nâng cấp hoặc ghi vào DB local trong task này.

Kỳ vẫn là tháng dương lịch. Giá theo ngày chốt từng hợp đồng; tiền phòng, điện, nước, dịch vụ cố định và khoán đầu người giữ nguyên công thức/snapshot. Ngày nghiệp vụ được chọn, phát hành ngay; hạn mặc định +7, có thể sửa trước khi xác nhận.

## Cách hoạt động

- Kiểm tra lại hóa đơn chưa hủy trước khi đọc chỉ số: hóa đơn nháp cũng chặn tạo thêm và có liên kết xem. Phòng thiếu chỉ số không tạo hóa đơn; phòng không đủ hợp đồng/giá hoặc không được chọn được ghi rõ ở nhóm bỏ qua khác.
- `BEGIN IMMEDIATE` trên SQLite giữ kiểm tra và ghi hóa đơn/khóa chỉ số/audit trong một transaction. Hai lượt đồng thời được tuần tự hóa, có unique index hợp đồng/năm/tháng làm ràng buộc cuối. Lỗi audit/ghi rollback toàn bộ, không khóa chỉ số dang dở.
- Danh sách, ngày và fingerprint đã xem vẫn được bảo vệ bằng Data Protection; phòng vừa bổ sung dữ liệu cần **Kiểm tra lại** để vào danh sách xác nhận. Gửi lại chính POST trước cũng an toàn. Khi không có phòng sẵn sàng, vẫn cho xác nhận chạy lại để nhận kết quả 0 mới và danh sách đã có/thiếu chỉ số.
- Sau POST chuyển sang `/HoaDonDichVu/Results?runId=...`: banner, số phát hành mới/thiếu chỉ số/đã có/bỏ qua khác, tổng tiền hóa đơn mới, ngày/hạn, bảng từng phòng với tab/tìm/lọc tầng/phân trang/liên kết xem, thông tin lần chạy và thời gian đo thực tế. Không có thanh cấu hình–kiểm tra–phát hành.
- Báo cáo CSV UTF-8 BOM gồm toàn bộ phòng của lần chạy, không chỉ trang đang hiển thị; escape dấu nháy và chặn công thức bảng tính. GET báo cáo/CSV kiểm lại người thực hiện và sở hữu tòa, trái quyền 403; POST giữ CSRF và quyền ghi TAI_CHINH.
- Snapshot báo cáo lưu phía máy chủ trong `IMemoryCache` tối đa 24 giờ, hết khi khởi động lại. Không nhét 50 dòng vào cookie và không thêm bảng lịch sử chạy. Hóa đơn, chi tiết và audit trong SQLite vẫn lưu lâu dài; xem lại kỳ trên Monthly sau khi báo cáo hết hạn.
- Thời gian trên màn hình đo bằng Stopwatch từ khi bắt đầu dịch vụ (gồm chờ khóa, kiểm lại, ghi/audit/commit). Kiểm tra hiệu năng trình duyệt đo riêng từ bấm xác nhận đến khi nhận và tải xong trang kết quả.

## Kiểm chứng ngày 08/10/2026

| Kiểm tra | Kết quả |
| --- | --- |
| Build Debug C#/Razor | PASS, 0 lỗi; cảnh báo ImageSharp/CS8601 có sẵn |
| Test mới `MonthlyBulk` | 4/4 PASS |
| Hồi quy RoomServices/MeterReading/MeterAnomalySchema/SchemaBranchReconciliation | 188/189 PASS; lỗi cũ `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` thiếu nút “Ghi nhận chuyển đi”, ngoài task hóa đơn |
| Hai lượt dịch vụ đồng thời, 50 phòng | PASS: đúng 50 hóa đơn, 150 dòng, 100 chỉ số khóa; không trùng; dưới 30 giây |
| HTTP xác nhận → redirect → trang kết quả, 50 phòng | PASS dưới 30 giây; gửi lại POST và lần chạy 0 phòng đều đúng; CSV đủ 51 dòng; thu hồi sở hữu chặn báo cáo/CSV 403 |
| Chrome: 50 phòng đủ cả 6 khoản thu | **1,426 giây** xác nhận → kết quả; **0,80 giây** dịch vụ; 50 hóa đơn, 300 dòng, tổng **203.000.000 đ** |
| Chrome: chạy lại 50 phòng | **0,690 giây**; 0 mới, 50 đã có, không tăng số hóa đơn |
| Chrome: ca thiếu chỉ số → chốt bổ sung qua form quản lý → chạy lại | Lần đầu 4 mới/1 thiếu; lần hai 0 mới/4 đã có/1 thiếu; bổ sung nước A105 qua UI: 1 mới/4 đã có/0 thiếu |
| Giao diện / CSV | Tab, tìm, lọc tầng, phân trang, tải CSV, liên kết hóa đơn, desktop và 360px PASS; không tràn ngang cấp trang, bảng cuộn riêng |
| Sở hữu / dữ liệu | Chủ nhà khác mở báo cáo/CSV 403; SQLite đúng 50 hóa đơn, không trùng, integrity/FK PASS |

Các thời gian trên là đo thực tế tại máy local với Chrome headless và SQLite demo tổng hợp; chưa là chứng nhận tải trên máy chủ production, Safari hoặc thiết bị thật. Không sửa lỗi người ở ghép, không commit/push/merge hoặc tự ghi nhận review thành viên khác.

## Tái hiện

```powershell
# Dừng web trước khi build; tất cả fixture test tự dùng SQLite tạm riêng.
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter MonthlyBulk
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'RoomServicesTests|MeterReading|MeterAnomalySchema|SchemaBranchReconciliation'

# Tạo thư mục demo MỚI; không ghi đè demo cũ/local-dev.sqlite.
$demoDir = Join-Path (Get-Location) ('data/s306-demo/bulk-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
# Đặt $demoPassword trong terminal, không lưu vào Git.
dotnet QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll --contentRoot QL_PhongTro --environment Development --Sprint2Demo:Directory $demoDir --Sprint2Demo:Password $demoPassword --Sprint2Demo:Url http://localhost:5247 --create-sprint2-demo
python verification/s306_prepare_demo.py $demoDir
python verification/s306_bulk_prepare_demo.py $demoDir

# Khởi động web đúng database/keys/uploads theo access.json; sau đó:
python verification/s306_bulk_browser.py $demoDir
# Chỉ kiểm tra lại giao diện; không phát hành thêm (máy chủ vẫn giữ báo cáo):
python verification/s306_bulk_browser.py $demoDir --visual-only
```

Kết quả browser lưu trong thư mục demo: `bulk-browser-report.json` (thời gian + URL từng lượt), `bulk-result-50.csv`, `bulk-results-desktop.png`, `bulk-results-mobile.png`, `bulk-mixed-desktop.png`, `bulk-rerun-desktop.png`. Toàn bộ DB, credential và ảnh nghiệm thu trong `data/` bị ignore. Script chỉ chấp nhận thư mục nằm dưới `data/s306-demo`, từ chối fixture đã có khi chuẩn bị dữ liệu.

Để tự demo: đăng nhập chủ nhà theo `access.json`, chọn **Demo phát hành 50 phòng**, kỳ **10/2026**, kiểm tra rồi xác nhận. Chạy lại kỳ để thấy 0 mới/50 đã có. Với **Demo hóa đơn đầy đủ**, phòng A105 thiếu nước; đăng nhập quản lý, chọn cùng tòa, nhập điện 150/nước 25 và lưu A105, quay lại chủ nhà kiểm tra rồi xác nhận để chỉ tạo phòng vừa đủ dữ liệu. Các bước phát hành cần bộ demo mới nếu script browser đã phát hành hết.
