# S3-07 — Chi tiết hóa đơn cho khách thuê

## Quy ước PO

- Tiền phòng là khoản cố định theo hợp đồng.
- Điện và nước tính theo chỉ số; giá điện phẳng, không chia bậc.
- Internet, phí dịch vụ và các dịch vụ cấu hình khác lấy mức cố định đã chốt cho phòng. Không dùng khoản khoán theo người trong danh sách khoản mục của AC này.
- Giữ lượng tiêu thụ tối đa ba chữ số thập phân. Thành tiền làm tròn từng dòng tới đồng bằng `AwayFromZero`.
- Tiền hiển thị theo VND, dấu chấm phân tách hàng nghìn và hậu tố `đ`.
- Khoản không có chỉ số hiển thị `—` ở hai cột chỉ số.

Các dòng đã lưu tiếp tục là snapshot bất biến của hóa đơn. Không đổi schema: kỳ, hợp đồng, tên khoản, chỉ số, lượng, đơn giá và thành tiền đã có trong `hoa_don` / `chi_tiet_hoa_don`.

## AC2–AC3: tổng kết, thanh toán và quá hạn

- Tổng cộng bằng tổng thành tiền các dòng: `GIAM_TRU` được lưu số dương nhưng trừ khỏi tổng; các dòng phí cộng vào. Cọc và công nợ kỳ trước không gộp vào hóa đơn.
- Hạn thanh toán được lưu trên hóa đơn; mặc định bằng ngày phát hành + 7 ngày. Chỉ khoản thanh toán `DA_XAC_NHAN` được cộng vào số đã thanh toán; khoản chờ xác nhận/từ chối/đã hủy không giảm nợ.
- Số còn phải trả là `max(tổng cộng - đã thanh toán, 0)`. Khi trả dư, hiển thị số đã thanh toán thực tế và số còn phải trả bằng 0.
- Quá hạn khi hóa đơn đang `DA_PHAT_HANH`, số còn phải trả > 0 và ngày hiện tại tại Việt Nam lớn hơn ngày hạn. Ngày hạn chưa quá hạn; ngày lịch đầu tiên sau hạn là trễ 1 ngày. Hóa đơn đã trả đủ không có nhãn dù thanh toán sau hạn.
- Nhãn dùng nền đỏ nhạt/chữ đỏ với nội dung `Quá hạn N ngày`. Hạn hiển thị `dd/MM/yyyy`. `Asia/Ho_Chi_Minh` được tính bằng UTC+7 trước khi so ngày lịch.
- Bảng thanh toán được thêm bằng updater schema v23 (bảng `thanh_toan`), FK hóa đơn `ON DELETE RESTRICT`; các schema đã cài và dữ liệu cũ được giữ nguyên.
- Quy tắc hiện hành của dự án: VND là số nguyên đồng; thành tiền làm tròn từng dòng `AwayFromZero`. Báo cáo thanh toán nhận số nguyên đồng, không tự làm tròn số khách nhập. Hình thức báo cáo trong phạm vi này là Tiền mặt hoặc Chuyển khoản.

## Báo cáo thanh toán của khách thuê

- Tại `/ThongBao/ChiTiet?maHoaDon=<mã>` và `/ThongBao/HoaDon/{id}`, khách thuê có thể gửi số tiền, ngày thanh toán và hình thức Tiền mặt/Chuyển khoản cho hóa đơn đã phát hành còn dư nợ.
- Backend xác thực quyền sở hữu hóa đơn; số tiền phải là số nguyên đồng lớn hơn 0 và không vượt số còn phải trả sau khi trừ các lần thu đã xác nhận và cấn trừ cọc.
- Báo cáo được lưu thành khoản thanh toán `CHO_XAC_NHAN`; không sửa trạng thái phát hành bất biến của hóa đơn. Trang chi tiết và danh sách khách thuê hiển thị “Chờ xác nhận”. Trong khi có báo cáo chờ xác nhận, khách không thể gửi/sửa báo cáo khác cho hóa đơn đó.
- Updater schema v35 thêm cột tùy chọn `hinh_thuc_bao_cao`, giới hạn hai hình thức trên; v36 thêm metadata ảnh tùy chọn và giữ nguyên các dòng thanh toán lịch sử.
- Chuyển khoản bắt buộc có ảnh chứng từ; tiền mặt cho phép bỏ trống. Chỉ nhận JPG/JPEG/PNG tối đa 5 MiB (5.242.880 byte); backend xác thực định dạng thực tế và lưu ngoài thư mục public.
- Chủ nhà sở hữu hóa đơn và Quản trị hệ thống có thể yêu cầu liên kết xem ảnh được ký bảo mật, gắn với tài khoản hiện tại và hết hạn sau 15 phút. Server kiểm tra lại quyền ở mỗi lần tải ảnh; phản hồi không lưu cache.
- Khi báo cáo còn `CHO_XAC_NHAN`, khách thuê có thể hủy. Báo cáo được giữ lại với trạng thái `DA_HUY`; hóa đơn vẫn giữ trạng thái phát hành gốc và trạng thái thanh toán hiển thị được suy ra lại từ số tiền đã xác nhận/cấn trừ. Nếu báo cáo đã được xác nhận hoặc không còn chờ, yêu cầu hủy bị từ chối.
- Sau khi hủy thành công, giao diện tải lại trạng thái hóa đơn và mở form cho phép khách gửi báo cáo mới; lịch sử báo cáo đã hủy vẫn được bảo toàn.
- Chưa có thao tác chủ nhà từ chối báo cáo trong phạm vi này.

### Bổ sung quyết định PO: không phát hành dịch vụ khoán theo người

- Không tạo mới hoặc phát hành hóa đơn nếu dịch vụ áp dụng cho phòng đang có cách tính `THEO_NGUOI`. Quy tắc áp dụng cho xem trước, tạo Nháp tháng, phát hành đơn lẻ/hàng loạt và phát hành Nháp đã tồn tại.
- Bản xem trước đánh dấu phòng cần xử lý, nêu tên dịch vụ và yêu cầu đổi sang cố định (`CO_DINH`). Các API ghi hóa đơn từ chối với cùng hướng dẫn; không tạo hóa đơn một phần.
- Nháp cũ cũng không thể phát hành nếu snapshot hoặc cấu hình dịch vụ liên quan vẫn là `THEO_NGUOI`. Dịch vụ cần được đổi sang cố định trước khi lập/phát hành lại hóa đơn.

## Truy cập và giao diện

- Mở `/ThongBao/ChiTiet?maHoaDon=<mã>` để xem trang chi tiết. Trang lấy dữ liệu từ `/ThongBao/ChiTietDuLieu?maHoaDon=<mã>`.
- Chỉ tài khoản `KHACH_THUE` có hồ sơ đứng tên hợp đồng được truy vấn hóa đơn đã phát hành. Hóa đơn của tài khoản khác, mã không tồn tại, bản Nháp hoặc module chưa cài trả cùng HTTP 404 và thông báo “Không tìm thấy hóa đơn.”
- Trang có trạng thái đang tải/lỗi, kỳ hóa đơn, phòng, bảng khoản mục theo `so_thu_tu` và bốn dòng tổng kết: tổng cộng, đã thanh toán, còn phải trả, hạn thanh toán. Hai cột chỉ số để `—` với khoản cố định. Nhãn quá hạn nằm gần hạn và chỉ hiện khi còn nợ, sau ngày hạn.
- Cả `/ThongBao/ChiTiet` và chi tiết từ thông báo (`/ThongBao/HoaDon/{id}`) hiển thị phần tổng kết. Trang `/ThongBao/DanhSach` và dữ liệu `/ThongBao/DanhSachDuLieu` chỉ trả hóa đơn đã phát hành của khách đang đăng nhập; không phụ thuộc bản ghi thông báo và không trả bản nháp/bản đã hủy.
- Dữ liệu cá nhân của khách khác và các trường quản trị nội bộ không được trả về.

## AC4: danh sách và bộ lọc

- PO chốt sắp xếp mặc định theo kỳ mới nhất trước; nếu cùng kỳ, mã hóa đơn mới tạo hơn trước. Phân trang 10 hóa đơn/trang.
- Danh sách có các cột mã hóa đơn, kỳ, tổng cộng, còn phải trả, hạn thanh toán và trạng thái. Bộ lọc kỳ chọn một tháng (`yyyy-MM`); bộ lọc trạng thái chọn một giá trị hoặc “Tất cả”. Hai bộ lọc được kết hợp bằng điều kiện AND.
- Trạng thái suy ra từ các khoản `DA_XAC_NHAN`, trừ khi có báo cáo `CHO_XAC_NHAN` thì hiển thị “Chờ xác nhận”; nếu không, còn 0 đồng là `Đã thanh toán`; còn nợ và đã quá hạn là `Quá hạn`; còn nợ, chưa quá hạn và đã trả một phần là `Thanh toán một phần`; các trường hợp còn lại là `Chưa thanh toán`. Quá hạn chỉ khi ngày Việt Nam đã qua hạn, không phải đúng ngày hạn. Nhãn `Quá hạn N ngày` trên dòng danh sách dùng cùng quy tắc với trang chi tiết.
- Kỳ phải đúng định dạng tháng `yyyy-MM`; trạng thái ngoài danh mục bị từ chối với HTTP 400 và thông báo cụ thể. Trang phân biệt “Bạn chưa có hóa đơn nào.” với “Không có hóa đơn nào khớp bộ lọc.”; lỗi tải danh sách cũng được thông báo.
- Chọn hóa đơn mở chi tiết; đường quay lại giữ kỳ, trạng thái và trang hiện tại. Xóa bộ lọc quay về trang đầu của toàn bộ danh sách. Danh sách, bộ lọc và chi tiết đều được giới hạn theo tài khoản khách thuê ở backend.

## Demo trên database demo S3-08

Chạy `.\verification\Start-S308Demo.ps1` để cập nhật và chuẩn bị dữ liệu trên bản sao nằm trong `data/s308-demo/`; script không dùng database mặc định. Bộ demo giữ hóa đơn chi tiết AC1 (giá dịch vụ chỉ minh họa) và tạo các hóa đơn mẫu thanh toán/quá hạn riêng. Mã mẫu được ghi trong `tenant-invoice-samples.txt`.

Đăng nhập bằng tài khoản khách thuê giả trong `access.json` của bộ Sprint 2 nguồn, mở `/ThongBao/DanhSach` để xem nhiều kỳ với các trạng thái. Mã hóa đơn của một khách khác cũng được ghi trong `tenant-invoice-samples.txt` để xác minh không xuất hiện trong danh sách của khách hiện tại. Chọn một hóa đơn trong danh sách để mở chi tiết; bộ lọc được giữ khi quay lại. Có thể mở riêng hóa đơn chi tiết AC1 bằng mã trong `tenant-invoice-code.txt`:

```text
/ThongBao/ChiTiet?maHoaDon=<mã trong tenant-invoice-code.txt>
```

Không đưa database, file mã, credential hoặc backup demo vào Git.

## Kiểm thử

```powershell
dotnet test Tests\QL_PhongTro.Tests\QL_PhongTro.Tests.csproj --filter "FullyQualifiedName~TenantPaymentReport|FullyQualifiedName~TenantCanOpenOnlyTheirPublishedInvoiceByCodeAndSeeAllOrderedLines|FullyQualifiedName~TenantInvoiceSummaryReflectsPaymentsDiscountsAndVietnamDueDates|FullyQualifiedName~Version23AddsPaymentHistoryWithoutChangingExistingInvoiceData|FullyQualifiedName~TenantInvoiceList"
```

Kiểm thử danh sách dùng SQLite tạm, xác minh quyền sở hữu, dữ liệu rỗng/phân trang/sắp xếp, từng trạng thái, bộ lọc đơn/kết hợp, lỗi kỳ/trạng thái, khớp nhãn quá hạn với chi tiết và giữ bộ lọc khi quay lại. Kiểm thử tổng kết/schema hiện có xác nhận phép tính tổng/giảm trừ, nhiều khoản thanh toán, hạn/biên ngày Việt Nam, không hiện nhãn khi đủ tiền, HTML và updater v23 bảo toàn hóa đơn cùng FK.

Build web/test và tiện ích demo S3-08 đạt 0 lỗi. Chưa xác nhận thực thi bộ test danh sách: lần chạy lại bị Windows Application Control chặn load DLL kiểm thử (`0x800711C7`). Chưa nghiệm thu trực quan trên trình duyệt, Safari hoặc thiết bị 360px.
