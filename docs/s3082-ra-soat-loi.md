# Rà soát S3-08 phần 2 — 09/10/2026

## Lỗi đã tìm và sửa

Phần chuông thông báo truy vấn bảng hóa đơn trên mọi trang khách thuê. CSDL nền hợp lệ có thể chưa cài module S1-09 và chưa có bảng hóa đơn, khiến trang khách thuê trả lỗi 500. Đã kiểm tra module trước khi truy vấn hoặc chạy hàng đợi email, trả 404 cho trang thông báo khi thiếu module. Chuông và menu thông báo cũng kiểm tra quyền tài chính hiện hành.

Đã thêm kiểm thử HTTP cho CSDL nền chưa có module, khách khác xem hóa đơn, đánh dấu đọc trái quyền, thu hồi quyền tài chính và hàng đợi email khi thiếu module. Bộ S3-08 hiện có **105 kiểm tra PASS** trên dữ liệu tạm, bao gồm thao tác trình duyệt ở 360px.

## Tương thích với các luồng cũ

- Bốn kiểm thử nâng cấp v17/v18/v19 lên v21 PASS, giữ nguyên dữ liệu nghiệp vụ, integrity/FK, backup và chạy lại không đổi dữ liệu. Cập nhật kỳ vọng phiên bản và fixture giả lập schema cũ trong test; không viết lại migration đã chia sẻ.
- Ba test HTTP hóa đơn được cập nhật theo yêu cầu mới: tạo Nháp trước khi phát hành; trang xem trước POST tới CreateDraft. Kiểm tra 50 bản Nháp, chạy lại không trùng, chưa khóa chỉ số/chưa thông báo, hạn thanh toán, kiểm tra bản cũ và sở hữu chéo đều PASS.
- Các test cũ còn lỗi được chạy đối chiếu trên commit `6dc811d` trước phần 2 bằng checkout tạm dưới `data/`, không đổi nhánh đang làm. Không coi một test lỗi là bằng chứng lỗi nghiệp vụ nếu fixture hoặc kỳ vọng đã cũ.
- SHA-256 của `QL_PhongTro/Data/local-dev.sqlite` không đổi: `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Không chạy updater trên file này.

## Kết quả hồi quy

Lượt toàn bộ: **569 test, 554 PASS, 15 FAIL**. Sau khi cập nhật và chạy lại riêng ba test HTTP hóa đơn theo luồng Nháp, cả ba PASS. Kết quả tổng hợp: **557 PASS, 12 FAIL**; không phải một lượt toàn bộ mới. Bốn test schema cũng được chạy riêng và PASS. Bộ 105 kiểm tra S3-08 là bộ riêng, không cộng vào 569 test này.

Tất cả 12 test còn FAIL đã chạy đối chiếu và cũng FAIL trên commit `6dc811d` trước phần 2:

| Nhóm | Số test | Lỗi kiểm thử hiện có |
| --- | ---: | --- |
| Phiên bản schema | 3 | Test còn kỳ vọng v13/v15 khi updater đã lên phiên bản mới |
| Menu và chuông yêu cầu thuê | 2 | Kỳ vọng menu/aria-label không khớp giao diện hiện có |
| Trang chủ, giá phòng, đăng ký | 3 | Thiếu liên kết/chuỗi/class mà test kỳ vọng |
| Tạo tòa nhà/phòng | 2 | Test kỳ vọng redirect nhưng nhận trang trả HTTP 200 |
| Hồ sơ khách | 1 | Fixture tự tạo bảng hop_dong đã tồn tại |
| Chuyển đi | 1 | Test không tìm thấy “Ghi nhận chuyển đi” |

Chưa sửa các chức năng cũ ngoài phạm vi S3-08. Một số FAIL do test cũ; các lỗi giao diện/luồng cũ cần rà soát riêng trước khi kết luận toàn dự án sạch lỗi. Không phát hiện thêm regression do phần 2 trong phạm vi đã kiểm chứng.

Log/TRX local: `data/s3082-audit/fixed-results/fixed.trx`, `data/s3082-audit/monthly-results/monthly.trx`, `data/s3082-audit/schema-results/schema.trx`; các lượt đối chiếu trong `data/s3082-audit/baseline-results`. Những file này là artifact local, không commit.

## Lệnh kiểm thử

Do checkout thiếu file project test gốc, thêm project kiểm chứng riêng, sử dụng toàn bộ mã test có sẵn và giữ nguyên UnitTest1.cs chưa theo dõi. Chạy tại gốc repository, build vào runtime riêng để tránh ghi đè web đang chạy:

```powershell
dotnet test verification/S308Regression/Regression.csproj -p:OutputPath="$pwd/data/s3082-regression/" --logger "trx;LogFileName=regression.trx" --results-directory data/s3082-regression-results
dotnet run --project verification/S308/S308.csproj -p:OutputPath="$pwd/data/s3082-check/" -- .
```

Thử nghiệm dùng SQLite giả hoặc bản sao, email pickup và tài khoản giả. Chưa kiểm tra SMTP thật, Safari, thiết bị thật hay tải production. Không thể bảo đảm phần mềm hoàn toàn không có bug; kết luận chỉ áp dụng cho các trường hợp đã kiểm chứng.
