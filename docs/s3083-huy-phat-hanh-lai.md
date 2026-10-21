# S3-08 phần 3: Hủy hóa đơn và phát hành lại

Nhánh `feature/S3-08/03-invoice-cancel-reissue` kế thừa phần 1 và 2.

## Quy tắc triển khai

- Chỉ Chủ nhà có quyền ghi tài chính và sở hữu tòa được hủy hóa đơn **Đã phát hành**. Bắt buộc lý do sau khi bỏ khoảng trắng, tối đa 1.000 ký tự, xác nhận và đúng phiên bản đã xem.
- Bản cũ chuyển **Đã hủy**, lưu người hủy và thời điểm UTC; hiển thị giờ Việt Nam. Giữ nguyên mã hóa đơn, lần phát hành, tổng tiền, mọi dòng, chỉ số snapshot, ghi chú và chỉ số nguồn. Không sửa/xóa bản hủy.
- Chặn hủy khi có thanh toán/cấn cọc đang hiệu lực hoặc báo thanh toán đang chờ xử lý. Không tự hủy khoản thu, chuyển tiền, hoàn tiền hay cấn sang bản mới. Nếu schema module tiền không tương thích thì từ chối hủy, yêu cầu kiểm tra.
- Email đang gửi thì chặn hủy, kể cả lease đã hết hạn: đợi worker xử lý lại và tải lại trang. Hủy sẽ dừng email chưa gửi của bản cũ, giữ thông báo để tra cứu. Không phát thêm thông báo chỉ vì tạo Nháp thay thế.
- Một bản hủy có tối đa một bản thay thế trực tiếp. Bấm lại hoặc hai yêu cầu đồng thời trả về cùng bản. Nếu bản thay thế cũng hủy, tạo bản tiếp theo từ chính bản đó; giữ chuỗi liên kết.
- Bản thay thế bắt đầu **Nháp**, mã mới, cùng hợp đồng/kỳ, sao chép toàn bộ nội dung snapshot, ghi chú và trường tham chiếu dành sẵn. Không tính lại theo giá mới hoặc số người mới khi sao chép. Sửa bản mới bằng luồng Nháp và làm tròn từng dòng như phần 1.
- Phát hành bản thay thế dùng luồng phần 2, tạo thông báo mới cho khách. Cho phép dùng nguồn chỉ số đã khóa của kỳ cũ, không mở khóa, ghi lại chỉ số hoặc tăng phiên bản nguồn đã khóa.
- Danh sách tháng hiển thị bản hủy để tra cứu, đúng nhãn Nháp/Đã phát hành/Đã hủy. Kỳ đã có bản hủy phải tạo thay thế từ bản đó, không tạo rời qua màn hình sinh hóa đơn thông thường.
- Chủ nhà xem liên kết bản cũ/bản thay thế ở cả hai phía. Khách tiếp tục mở bản hủy từ thông báo cũ; chỉ nhìn thấy liên kết bản thay thế sau khi bản đó phát hành và chính khách có thông báo cho bản đó.

Đây là quy tắc triển khai dựa trên mã trạng thái và quan hệ sẵn có trong AGENTS.md/schema. Danh sách loại điều chỉnh chi tiết và chính sách hoàn/điều chỉnh hóa đơn đã thu vẫn cần PO quyết định; phần này chặn trường hợp đã có khoản tiền thay vì tự quyết cách hoàn.

## Thử trên web

1. Mở `http://localhost:5249/HoaDonDichVu/Details/3`, đăng nhập Chủ nhà bằng tài khoản demo đã dùng. Nếu hóa đơn đang Nháp, kiểm tra và phát hành trước.
2. Bấm **Hủy hóa đơn**. Thử để trống lý do: không hủy. Nhập lý do, đánh dấu xác nhận rồi bấm **Xác nhận hủy**.
3. Kiểm tra nhãn **Đã hủy**, lý do/thời điểm hủy và các khoản tiền cũ; không còn ô sửa hoặc nút phát hành.
4. Bấm **Tạo Nháp thay thế**. Kiểm tra mã mới, liên kết về bản đã hủy, tổng tiền ban đầu giống bản cũ. Sửa chỉ số/thêm khoản điều chỉnh có ghi chú rồi **Lưu nháp và tính lại tổng**.
5. Bấm **Phát hành hóa đơn**, kiểm tra ngày/hạn, xác nhận. Bản mới khóa sửa và có thời điểm phát hành.
6. Đăng nhập Khách thuê đứng tên hợp đồng, mở chuông thông báo: có thông báo bản mới, mở được hóa đơn chỉ đọc và liên kết về bản hủy. Mở thông báo cũ vẫn xem được bản hủy.

Nếu nút hủy bị chặn vì email đang gửi, đợi xử lý xong rồi tải lại. Email demo được lưu trong thư mục `mail` của demo, không gửi ra hộp thư thật. Chạy lại web: `./verification/Start-S308Demo.ps1`.

## Schema và bảo toàn dữ liệu

v22 thay trigger bảo vệ hóa đơn: chỉ cho phép cập nhật trạng thái phát hành → hủy cùng metadata hợp lệ, không cho đổi nội dung tài chính. Thêm guard cho liên kết bản thay thế; không thêm bảng/cột nghiệp vụ mới. Các cột hủy/thay thế và trường snapshot vốn có trong SQLite nay được ánh xạ EF. Giữ các migration/SQL đã chia sẻ trước đó; initializer module hóa đơn gọi guard v22 khi được cài sau schema nền v22.

Updater có backup, transaction và integrity/FK; đã kiểm tra nâng v21 → v22 trên bản sao và chạy lại không thay dữ liệu. Không chạy trên `QL_PhongTro/Data/local-dev.sqlite`. Demo phần 3 dùng bản sao mới; cả CSDL gốc và bản demo trước đó được giữ nguyên.

## Kiểm thử

```powershell
dotnet run --project verification/S308/S308.csproj -p:OutputPath="$pwd/data/s3083-check/" -- .
dotnet test verification/S308Regression/Regression.csproj -p:OutputPath="$pwd/data/s3083-regression/" --logger "trx;LogFileName=s3083.trx" --results-directory data/s3083-regression-results
```

Bộ S3-08 kiểm tra backend/HTTP/Chrome: lý do, phiên bản, quyền, CSRF, khoản thu/cấn cọc, email đang gửi, rollback audit/ghi dòng, hai yêu cầu tạo thay thế đồng thời, bảo vệ dữ liệu bằng trigger, sao chép đủ snapshot, tổng tiền sửa lại chính xác, phát hành với nguồn chỉ số đã khóa, quyền khách trên liên kết và trọn luồng mobile 360px. Các test đều dùng tài khoản và SQLite tạm; ảnh được ghi trong thư mục tạm in cuối log.

Lỗi tìm và sửa trong phần 3: partial dùng chung khi mở từ controller ThongBao cần đường dẫn đầy đủ; phát hành bản thay thế phải nhận biết chỉ số đã khóa bởi bản cũ; giữ bản hủy trong danh sách/thông báo và sửa nhãn “Đã tạo nháp” bị áp nhầm cho bản phát hành. Chưa kiểm tra SMTP thật, Safari, mạng di động thật hoặc tải production. Chưa thêm giao diện nhật ký chi tiết trước/sau mỗi lần sửa.

### Kết quả ngày 09/10/2026

- 174 kiểm tra S3-08 PASS; log `data/s3083-final.log`.
- Lần chạy toàn bộ hồi quy: 556 PASS, 13 FAIL trên 569 test (`data/s3083-regression-results/s3083.trx`). Sửa fixture nâng cấp v20 phải bỏ trigger trước khi xóa cột ngày phát hành; cập nhật ba kỳ vọng phiên bản cũ lên v22 và fixture hạ v9 phải xóa mọi marker từ v10 trở lên. Chạy lại các test liên quan: cả 7 test schema PASS (6 trong `data/s3083-schema-final-results/schema-final.trx`, test cuối trong `data/s3083-schema-last-results/schema-last.trx`). Tổng hợp theo từng test: **560 PASS, 9 FAIL / 569**, không phải một lần chạy toàn bộ mới.
- 9 FAIL còn lại đã tồn tại trên baseline trước phần 2: ma trận menu/quyền; yêu cầu thuê quá 24 giờ; fixture hồ sơ tạo trùng bảng hợp đồng; trang chủ khách; giá thuê khách; đăng ký/đổi email; hai test CRUD tòa/phòng; thông báo chuyển đi. Đối chiếu baseline tại [báo cáo phần 2](s3082-ra-soat-loi.md). Chưa sửa các lỗi ngoài phạm vi này; không kết luận toàn dự án hết bug.
- SHA256 CSDL gốc vẫn là `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Demo trước cũng giữ nguyên; web cổng 5249 dùng bản sao v22 mới.
