# Ma trận kiểm thử S4-05 phần 1 — 15/10/2026

Phạm vi: xác nhận kết thúc hợp đồng qua tất toán, giải phóng phòng cùng giao dịch, bảo vệ quyền/dữ liệu, gửi lặp/đồng thời và giao diện đọc trạng thái mới. Dùng SQLite tạm/bộ demo riêng; không chạy test vào `QL_PhongTro/Data/local-dev.sqlite`.

| Nhóm | Trường hợp cần kiểm tra | Kết quả bắt buộc |
| --- | --- | --- |
| Thành công | Chủ nhà đúng tòa, đủ quyền, đầy đủ tất toán, hai xác nhận hợp lệ | Hợp đồng Đã kết thúc, phòng Trống, phiên bản tăng một lần |
| Hợp đồng | Nháp, Chờ hiệu lực, Đã hủy, Đã kết thúc | Từ chối, toàn bộ dữ liệu giữ nguyên |
| Phòng | Trống, Đã đặt cọc, Ngừng cho thuê | Không kết thúc/giải phóng nhầm |
| Trùng phòng | Có hợp đồng khác đang hiệu lực cùng phòng (dữ liệu cũ không nhất quán) | Từ chối, không giải phóng phòng của hợp đồng khác |
| Sở hữu | Chủ nhà khác hoặc phòng đổi tòa sau khi mở xác nhận | Từ chối, không thay đổi dữ liệu |
| Tài khoản | Chủ nhà bị khóa hoặc xóa trước khi gửi | Từ chối |
| Quyền | Thu hồi HOP_DONG hoặc TAI_CHINH sau khi mở form | Từ chối tại service/backend |
| Vai trò HTTP | Anonymous, Khách thuê, Quản lý được gán tòa, ADMIN, Chủ nhà khác | GET/POST xác nhận bị chặn; anonymous tới Login, vai trò trái quyền 403 |
| CSRF/xác nhận | Thiếu token, thiếu từng xác nhận hoặc dữ liệu form cũ | Không chốt và không giải phóng |
| Tài chính | Chưa có bảng tất toán, thiếu thông tin chấm dứt, hóa đơn cuối hủy/sai tổng, còn khoản chờ hoặc Nháp khác | Từ chối trước khi kết thúc |
| Số liệu thay đổi | Chỉ số, điều khoản, cọc/công nợ, số tiền, fingerprint hoặc room version thay đổi | Yêu cầu kiểm tra lại, không lưu dở dang |
| Biên phiên bản | Phiên bản phòng đạt Int32.MaxValue | Rollback, không tràn âm hoặc lưu hợp đồng riêng lẻ |
| Gửi lặp | Gửi lại form đã hoàn tất | 409 theo luồng cũ, không tạo thêm giao dịch/lịch sử/phiên bản |
| Đồng thời | Hai yêu cầu cùng phiên bản | Chỉ một lần chốt thành công, không cấn cọc/giải phóng hai lần |
| Lỗi cập nhật phòng | Trigger mô phỏng chặn cập nhật TRONG | Rollback hợp đồng, phòng, hóa đơn/dòng tiền, cấn cọc, audit và dữ liệu liên quan |
| Lỗi sau cập nhật | Lỗi insert lịch sử mới hoặc audit xác nhận | Rollback cả trạng thái và lịch sử cũ đã đóng |
| Lịch sử có sẵn | Có một khoảng Đang thuê đang mở | Đóng đúng thời điểm; mở một khoảng Trống, không chồng lấn |
| Phòng cho thuê tiếp | Phòng đã Đang thuê sau khi hợp đồng cũ kết thúc, gửi lại form cũ | Không giải phóng lại; xem kết quả cũ đọc đúng trạng thái hiện tại |
| Hiển thị | Trang kết quả, nút làm mới, liên kết chi tiết phòng/hợp đồng | Thấy Đã kết thúc/Trống ngay sau chốt; làm mới không ghi dữ liệu |
| Khách thuê | Xem cùng bảng tất toán sau khi kết thúc | Chỉ đọc, không có thao tác kết thúc |
| Giao diện | Desktop và mobile 360px | Không tràn ngang; thao tác xác nhận/hiển thị trạng thái hoạt động |
| Bảo toàn | CSDL gốc, snapshot tài chính đã chốt, file người dùng | Không bị ghi đè; audit/lịch sử chỉ thêm theo giao dịch hợp lệ |

Các ca mới nằm trong `Tests/QL_PhongTro.Tests/RoomServicesTests.AutoReleaseRoom.cs`; ca nền về tất toán/CSRF/race/audit nằm trong `RoomServicesTests.XacNhan.cs`, `RoomServicesTests.TatToan.cs`, `RoomServicesTests.ChamDut.cs`. Kiểm tra trình duyệt dùng `verification/S404Browser` ở chế độ task4, luôn tạo bộ demo mới trước khi chạy.

## Chạy lại

```powershell
dotnet test verification/S308Regression/Regression.csproj -p:NuGetAudit=false -p:OutputPath="$pwd/data/s405-release/" --filter 'FullyQualifiedName~AutoReleaseRoom|FullyQualifiedName~XacNhan|FullyQualifiedName~TatToan|FullyQualifiedName~ChamDut|FullyQualifiedName~ContractActivationTests'
dotnet test verification/S308Regression/Regression.csproj -p:NuGetAudit=false -p:OutputPath="$pwd/data/s405-all/" --logger 'trx;LogFileName=all.trx' --results-directory data/s405-all-results
```

Lượt chuyên biệt hoàn tất: **28/28 PASS** (`data/s405-release-results/release.trx`, log `data/s405-release.log`), bao gồm 18 ca bổ sung sau lượt đầu có 10 ca S4-05. Các nhóm nền về tất toán, CSRF, đồng thời, audit, tiền/PDF đã đạt trong lượt 93/93 trước đó. Kiểm tra trình duyệt desktop/360px đã đạt trong `data/s405-browser2.log`.

Lượt toàn dự án đã hoàn tất: **901 PASS, 22 FAIL / 923**; cả 22 FAIL cũng lỗi trên commit gốc, không có test lỗi mới trong phạm vi đối chiếu. Tổng hợp với 5 ca HTTP bổ sung: **906 PASS, 22 FAIL / 928 test không trùng**. [Kết quả đầy đủ, bằng chứng và danh sách lỗi cũ](s4051-ket-qua-kiem-thu.md).

Không thể chứng minh mọi tình huống tưởng tượng hoặc mọi lần merge tương lai đều không có lỗi. Safari/thiết bị cảm ứng thật/tải production cần nghiệm thu riêng; không ghi là đã kiểm chứng.
