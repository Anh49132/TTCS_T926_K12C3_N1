# Sửa tiêu chí backlog — 20/10/2026

## Thay đổi

- Đăng nhập MVC/API dùng cửa sổ trượt `(hiện tại − 15 phút, hiện tại]`, lưu thời điểm UTC từng lần sai trong hạ tầng `login_failed_attempt`. Đủ 5 lần thì khóa 15 phút và trả thời gian khóa ngay. Mật khẩu sai ngắn vẫn được đếm; form trống vẫn báo thiếu dữ liệu. Poll trạng thái khóa không gia hạn khóa; hết khóa được thử lại. Đăng nhập đúng xóa các lần sai của tài khoản. Transaction ghi SQLite bảo vệ bộ đếm khi có yêu cầu đồng thời. Không lưu mật khẩu/identifier/token trong bảng mới và không audit dữ liệu đăng nhập.
- Danh sách hợp đồng của Quản lý loại giá thuê khỏi dữ liệu kỳ hợp đồng được trả cho view, loại tiền cọc và không render cột tiền thuê. Chủ nhà vẫn thấy giá gốc; DB không đổi giá. Các endpoint chi tiết hợp đồng của chủ nhà tiếp tục từ chối Quản lý.
- Tìm tin: 12 tin/trang.
- Người dùng chốt gia hạn giữa tháng: giá cũ đến hết kỳ cũ, giá mới từ ngày gia hạn. Công thức mỗi đoạn = giá tháng × số ngày của đoạn / số ngày thực tế của tháng; làm tròn từng dòng tới đồng (AwayFromZero), rồi cộng. Giữ nguyên công thức đã triển khai và hóa đơn đã phát hành; thêm giải thích trên form gia hạn và cập nhật AGENTS.md.

Ví dụ tháng 10/2026, giá cũ 1.000.000 đ, giá mới 2.000.000 đ từ 15/10: dòng cũ 14/31 = 451.613 đ; dòng mới 17/31 = 1.096.774 đ; tiền phòng 1.548.387 đ.

## Schema v42 và triển khai

Chỉ thêm bảng/index hạ tầng, không thay cột hoặc dữ liệu tài khoản cũ. Bước v42 giữ nguyên các bước v1–v41, chạy trong transaction, kiểm tra schema/integrity/FK trước marker. Updater chung tạo backup trước nâng cấp. Các bộ đếm cũ không có timestamp nên không suy diễn vào cửa sổ mới; khóa cũ còn hạn vẫn được tôn trọng.

Không nâng DB đang dùng trong task này. Trước chạy bản mã mới, dừng app, dùng đúng `DatabasePath`, sao lưu ra ngoài repository, rồi chạy:

```powershell
dotnet run --project QL_PhongTro --no-launch-profile -- --update-database
dotnet run --project QL_PhongTro --no-launch-profile -- --check-database
```

Phục hồi nếu cần: dừng app, giữ riêng bản DB sau nâng cấp, dùng backup trước nâng cấp cùng bản mã v41; không chép backup đè DB có phát sinh nghiệp vụ mới khi chưa đối chiếu.

## Kiểm thử

Nhóm mới phủ cửa sổ trượt, biên đúng 15 phút, thông báo lần thứ 5, hết khóa/poll, reset sau thành công, đồng thời; quyền Quản lý/Chủ nhà qua HTTP; phân trang 13 tin thành 12+1; 10 ví dụ tính tay gia hạn (28/29/30/31 ngày, ngày đầu/cuối tháng). Giữ test hóa đơn cũ không đổi sau gia hạn.

`LoginAttemptSchemaTests` tạo DB giả v41, nâng v42, so fingerprint từng giá trị của tất cả bảng cũ, integrity/FK và tính lặp không đổi bytes; nếu có biến `NHATOT_UPGRADE_SOURCE`, chỉ đọc/backup nguồn rồi nâng bản sao, xác nhận bytes DB nguồn không đổi. Không xuất dữ liệu cá nhân hoặc hash tài khoản trong báo cáo.

Kết quả cuối:

- Build C#/Razor/test đạt, 0 lỗi; còn cảnh báo NuGet NU1900, ImageSharp license và CS8601 có sẵn.
- Nhóm hồi quy liên quan: **188/188 PASS**, không bỏ qua; gồm API/MVC, 5 lần sai, cửa sổ trượt/biên 15 phút, hết khóa/poll, đồng thời, quyền, phân trang, tính tiền, báo cáo và các schema liên quan. Thời gian chạy test khoảng 1 phút 30 giây.
- Hai ca nâng cấp cọc/tất toán cũ bổ sung: **2/2 PASS**. Tổng cuối **190 ca khác nhau đạt** trên cùng bản mã, không cộng trùng những lượt chạy lại trước.
- Nâng DB giả và bản sao `data/ui-review-20261020/review.sqlite`: kiểm tra fingerprint mọi giá trị của tất cả bảng cũ, integrity/FK, updater lần hai không đổi bytes; file nguồn không đổi bytes. Không nâng DB nguồn. Đường dẫn mặc định `QL_PhongTro/Data/local-dev.sqlite` không tồn tại ở checkout này; khi triển khai phải chọn đúng DB hiện dùng.
- Bằng chứng cục bộ (ignored): `data/backlog-fix-20261020/backlog-regression.trx`, `backlog-legacy-upgrades.trx`, `backlog-upgrade.trx`. Các lượt đầu có lỗi thiết lập fixture (thiếu chỉ số đúng tháng, kiểm tra số ngày ở dòng trọn tháng, file SQLite còn trong pool và đường dẫn nguồn không tồn tại); đã sửa thiết lập và chạy lại, không bỏ qua test.
- `git diff --check` đạt; không sửa project test `.csproj`, không stage/commit, không thay mật khẩu/dữ liệu DB nguồn. Đã dừng web ở cổng 5247 trước build và để dừng khi bàn giao.

Chưa chạy lại full suite của toàn dự án hoặc nghiệm thu trực quan/thiết bị thật/SMTP trong lượt sửa này.
