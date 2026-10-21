# S4-05 phần 4 — Phòng trống chưa có tin hiển thị

Nhánh `feature/S4-05/04-vacant-rooms-without-listings`, kế thừa `cf499a6` của phần 3. Bổ sung luồng từ trang chủ Chủ nhà tới phòng cần đăng tin và bản Nháp đã lưu, giữ nguyên nghiệp vụ trả phòng/tất toán và đăng tin của các phần trước.

## Phạm vi và quy tắc

Cách hiểu đã thông báo khi triển khai: **mọi phòng đang Trống nhưng chưa có tin công khai còn hiệu lực**, ưu tiên phòng vừa trả; không giới hạn chỉ phòng đã có hợp đồng trước đây. Đây là giả định triển khai theo tên nhánh và AC tổng của S4-05, chưa ghi nhận xác nhận PO riêng cho phần 4 trong hội thoại.

- Chỉ phòng thuộc tòa đang hoạt động của Chủ nhà đang đăng nhập. Phòng đang thuê/đặt cọc/ngừng cho thuê không thuộc danh sách.
- Có bất kỳ tin `DANG_HIEN_THI` còn hiệu lực thì loại phòng khỏi danh sách, kể cả phòng đó còn Nháp khác. Ngày hết hạn NULL nghĩa là **không hết hạn**, không phải chưa đăng; tại đúng thời điểm hết hạn tin vẫn hợp lệ (`>= now`), sau thời điểm đó mới là hết hạn.
- Phòng không có tin, chỉ có Nháp/Tạm ẩn/Đã cho thuê hoặc tin đã hết hạn đều được liệt kê một lần theo phòng, không nhân dòng theo số tin.
- Tin mới nhất theo id được dùng làm liên kết thao tác. Có Nháp: **Kiểm tra Nháp**; có tin khác: **Kiểm tra tin**; chưa có tin: **Tạo tin**. Không tự tạo hoặc đăng tin khi mở danh sách.
- Ưu tiên mốc vào trạng thái Trống đang mở trong `lich_su_trang_thai_phong`, có liên kết hợp đồng; mới nhất trước. Phòng chưa có mốc trả phòng vẫn xuất hiện phía sau, theo tòa/mã phòng/id để phân trang ổn định. Mốc thời gian hiển thị UTC+7, giá lấy từ hồ sơ phòng hiện tại.

## Giao diện và quyền

Trang chủ `/` dùng lại khung việc cần xử lý: tổng số phòng cần đăng, tối đa bốn dòng và **Xem tất cả**. Bộ lọc tòa trên trang chủ được giữ khi mở danh sách đầy đủ.

Trang `/Home/PhongTrongChuaDangTin` có bộ lọc tòa, làm mới, 15 phòng/trang, trạng thái tin gần nhất và liên kết tới đúng tin/phòng. Trang âm hoặc quá lớn được đưa về trang hợp lệ; không nhân tràn phép tính offset. Khi đăng thành công bằng luồng phần 3, tải lại trang chủ/danh sách sẽ không còn phòng đó; tạm ẩn tin thì phòng xuất hiện lại nếu vẫn Trống.

Chỉ CHU_NHA có quyền đọc cả `PHONG_TRO` và `TIN_DANG` được xem. Quyền chỉ đọc được xem nhưng không có liên kết tạo/sửa; backend đăng/sửa vẫn áp dụng quyền ghi như cũ. Thu hồi quyền có hiệu lực ở request tiếp theo. Khách/Quản lý/Admin không mở được danh sách riêng của Chủ nhà, chưa đăng nhập chuyển Login. Tòa của người khác, tòa ngừng hoạt động hoặc id không có trả 403 khi cố lọc trực tiếp. Trang không được cache.

## Thay đổi kỹ thuật

- Thêm `VacantRoomListingService`: truy vấn chỉ đọc dùng chung điều kiện chọn phòng cho khung trang chủ và danh sách. Không đổi schema33, không thay giá/phòng/hợp đồng/tin nguồn.
- `HomeController` chỉ thay khối gợi ý phòng chưa đăng cũ bằng helper; phần truy vấn mới và action đặt trong `HomeController.VacantRooms.cs`. Giữ các phần tài chính, yêu cầu, chỉ số và trang chủ theo vai trò hiện có.
- Không thay đổi logic phát hành tin/ảnh/tất toán. Dùng lại cơ chế tạm ẩn tin hết hạn hiện có khi tải trang, tương tự trang chủ; truy vấn phòng không ghi dữ liệu hay audit.
- Thêm view và CSS riêng cho danh sách; cập nhật khung trang chủ. Không thêm thư viện hay migration.
- CSDL gốc `QL_PhongTro/Data/local-dev.sqlite` giữ SHA-256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. File `UnitTest1.cs` của người dùng được giữ nguyên.

## Cách thử trên web

Từ thư mục dự án:

```powershell
.\verification\New-S4054Demo.ps1
.\verification\Start-S4054Demo.ps1
```

Demo mới riêng ở `data/s4054-demo/`, mặc định cổng **5254**. Tài khoản/mật khẩu trong `access.json` của thư mục mà `data/s4054-demo/latest.txt` trỏ tới; không ghi thông tin đó vào Git. Demo phần 3 không đổi.

1. Đăng nhập Owner, mở `http://localhost:5254/TatToan/XacNhan/1` và hoàn tất trả phòng theo hai ô đồng ý trên màn hình.
2. Mở `http://localhost:5254/`. Cuộn tới **Phòng trống chưa có tin hiển thị**, thấy A103, giá **4.500.000đ**, trạng thái **Có tin Nháp** và mốc Trống từ.
3. Bấm **Xem tất cả** để mở danh sách, thử chọn cơ sở rồi **Áp dụng**, hoặc **Làm mới**.
4. Bấm **Kiểm tra Nháp**, thấy nội dung/ảnh cũ đúng trên form. Bấm **Đăng tin** hoặc quay sang quản lý tin và dùng nút đăng phần 3.
5. Quay lại trang chủ hoặc danh sách, phòng A103 không còn nằm trong nhóm chưa có tin hiển thị. Tạm ẩn tin rồi làm mới để kiểm tra chiều ngược lại.
6. Dùng Tenant: mở đường dẫn danh sách phải bị 403. Tạo demo mới nếu muốn thử lại luồng trả phòng từ đầu.

## Kiểm thử

- Lượt đầu: **21/21 ca HTTP PASS**, `data/s4054-results/vacant.trx`. Bao phủ trạng thái phòng/tin, hết hạn/NULL, nhiều tin, chưa có tin, liên kết Nháp mới nhất, sở hữu/tòa hoạt động/bộ lọc, các vai trò và quyền bị thu hồi/chỉ xem, rỗng, phân trang 18 phòng và thay đổi sau đăng/gỡ.
- **23/23 ca mới PASS** trong lượt cuối, gồm 21 ca HTTP và hai ca tích hợp nghiệp vụ: trả phòng thật xong xuất hiện đầu danh sách với Nháp mới/giá hiện tại; thời điểm hết hạn chính xác và truy vấn không đổi trạng thái tin.
- Edge desktop và 360px PASS: trả phòng → trang chủ → danh sách → đúng Nháp → đăng tin → biến mất khỏi hai danh sách; Khách bị 403. Log `data/s4054-browser.log`, ảnh `data/s4054-browser-demo/469b0ffeb99f41728b2017cc6f76ef0e/browser-check-17aa1d6b/`. Đã xem ảnh trang chủ và trang danh sách 360px, không tràn ngang. Thời gian đăng một nút trên máy local: **0,83 giây**.

Chưa chạy toàn bộ test dự án hoặc nghiệm thu Safari/thiết bị thật/tải production. Không kết luận mọi mã khác không còn lỗi hay mọi lần merge tương lai không xung đột. Các cảnh báo ImageSharp/CS8601 có sẵn vẫn còn. Chưa commit/push phần 4.

Bộ demo riêng cho người dùng đã tạo thành công bằng New-S4054Demo.ps1 tại `data/s4054-demo/54306ff844c74459bc597c21025b3f04`; Start-S4054Demo.ps1 đang chạy cổng5254. Đã xác minh đăng nhập Owner và mở danh sách trả HTTP200, còn 0 phòng trong danh sách vì hai hợp đồng demo chưa trả phòng. Không dùng dữ liệu đã tiêu thụ bởi test trình duyệt làm demo người dùng. Log khởi tạo `data/s4054-demo-script.log`, web `data/s4054-web.log`.

### Kết quả cuối

- Bộ gộp phần 4 và hồi quy đăng Nháp/tạo Nháp/giải phóng phòng/tất toán/xác nhận/PDF/hợp đồng/trang chủ hợp đồng Khách thuê: **208/208 PASS**, không bỏ qua, 8 phút 38 giây. TRX `data/s4054-verified-results/verified.trx`, log `data/s4054-verified.log`.
- Bộ tích hợp tin/ảnh/kích hoạt hợp đồng: **39/39 PASS**, `data/s4054-integration-results/integration.trx`. Có ca trùng giữa hai bộ; tổng không trùng **219/219 PASS**, không có test lỗi trong phạm vi đã chạy.
- Các bằng chứng trình duyệt và demo riêng ở trên đều đạt. Bộ dữ liệu dùng trình duyệt đã dừng; web cho người dùng chạy trên bộ mới riêng chưa trả phòng. CSDL gốc kiểm tra cuối vẫn đúng hash đã nêu, `git diff --check` đạt.
- Không sửa mã nghiệp vụ sau lượt build dùng cho các kiểm thử cuối. Chưa commit/push phần 4.
### Kiểm thử lại theo yêu cầu ngày 15/10/2026

- Bộ gộp nghiệp vụ/hồi quy chạy lại: **209/209 PASS**, không bỏ qua, 10 phút 3 giây; `data/s4054-retest-results/retest.trx`, log `data/s4054-retest.log`. Filter lần này gồm đúng tên ca quản lý được phân công (lượt trước ca này có trong bộ tích hợp riêng).
- Bộ tích hợp chạy lại: **39/39 PASS**; `data/s4054-integration-retest-results/integration-retest.trx`. Tổng không trùng vẫn **219/219 PASS**, trong đó **23 ca mới phần 4**; không có lỗi trong phạm vi kiểm thử.
- Edge desktop/360px trên dữ liệu mới riêng tại cổng5255: PASS trả phòng → trang chủ → danh sách → Nháp → đăng → biến mất; gửi lặp và quyền Khách đạt. Thời gian đăng local **0,79 giây**. Log `data/s4054-browser-retest.log`, ảnh `data/s4054-retest-demo/0f90ba7ba138461c873009e933a5fd4a/browser-check-a2149cc8/`.
- Không sửa mã nghiệp vụ trong lượt này. Server thử5255 đã dừng, demo người dùng vẫn trỏ `54306ff844c74459bc597c21025b3f04`, CSDL gốc giữ đúng SHA-256 đã nêu. Chưa commit/push.

Theo yêu cầu kiểm tra toàn bộ S4-05 từng nhóm và mở web bên cạnh VS Code, đã hoàn tất 219/219 ca không trùng và luồng trình duyệt trên dữ liệu riêng. Bảng từng bước, thời gian/log: [kiểm thử tuần tự S4-05](s405-kiem-thu-tuan-tu.md). Không sửa mã nghiệp vụ hoặc CSDL gốc trong lượt này.
