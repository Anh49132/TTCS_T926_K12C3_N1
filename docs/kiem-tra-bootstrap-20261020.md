# Kiểm tra giao diện Bootstrap — 20/10/2026

## Phạm vi và môi trường

- Working tree sạch khi bắt đầu. Thay đổi giao diện đã nằm trong commit `9682efb` và `937f7c5`; đối chiếu phần cập nhật ngày 19–20/10 ở cuối `tien-do.md`.
- Chạy mã nguồn hiện tại bằng .NET 10, bản build Debug riêng trong `data/ui-review-20261020/runtime`, tại `http://localhost:5247`.
- Sao chép SQLite của bộ demo hiện có sang `data/ui-review-20261020/review.sqlite` trước khi chạy. Không chạy initializer/updater, không dùng DB local thật. Đăng nhập chỉ tác động bản sao; email dùng pickup.
- Kiểm tra trực quan bằng trình duyệt tích hợp Chromium, viewport desktop 1440px và mobile giả lập 360px. Có cuộn xem phần dưới, mở dropdown và drawer; không chỉ kiểm tra HTTP.

## Các trang đã kiểm tra ở cả hai kích thước

| Vai trò | Trang / đường dẫn | Kết quả |
| --- | --- | --- |
| Khách thuê | Tổng quan `/` | Thẻ phòng, thống kê và panel hiển thị đúng; thu về một cột trên mobile. |
| Khách thuê | Tìm phòng `/TimTin` | Lưới 3/1 cột, thanh tìm kiếm, bộ lọc và header phù hợp chiều rộng. |
| Khách thuê | Hồ sơ `/HoSo/Xem` | Thẻ tài khoản, tab và thông tin chuyển cột đúng. |
| Khách thuê | Form hồ sơ `/HoSo` | Trường nhập, ảnh giấy tờ và nút lưu/hủy nằm trong trang. Không lưu/xóa hồ sơ. |
| Khách thuê | Hợp đồng `/HopDongCuaToi` | Thẻ, ngày/giá và nút rõ; dropdown vai trò mở được, chọn Người đứng tên vẫn hiện 2 hợp đồng. |
| Khách thuê | Chi tiết hợp đồng `/HopDongCuaToi/Details?maHopDong=HD-DEMO-C001` | Breadcrumb, thông tin, người ở, tài liệu và tóm tắt hiển thị đúng. |
| Khách thuê | Trả phòng `/YeuCauTraPhong` | Form 2/1 cột, ghi chú và lịch sử trống hiển thị đúng. Không gửi yêu cầu. |
| Khách thuê | Báo hỏng `/BaoHong` | Thống kê, tab, trạng thái trống và lọc responsive; chọn Gấp rồi Lọc gửi đúng `mucDo=GAP`. |
| Khách thuê | Gửi báo hỏng `/BaoHong/Create` | Đã sửa vùng tải ảnh bị dồn ngang. Chọn phòng có nhãn dài vẫn xuống dòng trong nút và cập nhật tóm tắt đúng. Không gửi báo hỏng. |
| Khách thuê | Yêu cầu `/YeuCau` | Danh sách chuyển sang thẻ trên mobile; chọn Đã hẹn lịch còn đúng 1 yêu cầu và mở được chi tiết. |
| Khách thuê | Chi tiết yêu cầu `/TinDang/YeuCau/2` | Thẻ thông tin, lịch hẹn, phòng và lịch sử hiển thị đúng. Không hủy yêu cầu. |
| Khách thuê | Hóa đơn `/ThongBao/DanhSach` | Thống kê, bộ lọc, bảng/thẻ và phân trang responsive; lọc Chưa thanh toán gửi đúng giá trị GET và giữ 2 hóa đơn. |
| Khách thuê | Chi tiết hóa đơn `/ThongBao/HoaDon/2` | Thống kê, tổng tiền, lịch sử và form báo trả hiển thị đúng. Bảng khoản thu có vùng cuộn ngang riêng ở 360px. Không gửi thanh toán. |
| Khách thuê | Thông báo `/ThongBao` | Trạng thái trống có chuông và phân trang hiển thị đúng. Demo không có thông báo để kiểm tra thẻ có dữ liệu. |
| Chủ nhà | Quản lý tòa nhà / Tiến độ thu tiền `/PhongTro/ToaNha` | Bộ chọn kỳ đúng kích thước và xuống hàng trên mobile. Chọn 09/2026 tự tải lại `ky=2026-09`, tổng hóa đơn hiển thị 7.370.000 đ. |

Drawer Khách thuê mở đúng chiều rộng 360px, hiển thị logo và các liên kết. Menu tài khoản desktop mở và đăng xuất được trong phiên demo.

## Lỗi đã sửa

`repair-upgrade.css`: selector vùng tải ảnh chưa đủ độ ưu tiên, bị quy tắc nhãn form ghi đè thành `display: block`. Biểu tượng, lời hướng dẫn và input tệp vì vậy dồn thành một hàng trên desktop. Dùng selector cụ thể cho `label.ru-upload-zone` trong form Khách thuê để khôi phục flex cột, căn giữa và khoảng cách; không sửa HTML, JavaScript, validation, upload hoặc nghiệp vụ.

Đã tải lại và xem lại vùng này ở 1440px/360px; computed style là `display: flex`, mobile có `scrollWidth = clientWidth = 282px`.

## Xác minh và giới hạn

- `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --nologo -o data/ui-review-20261020/runtime`: thành công, 0 lỗi; còn cảnh báo có sẵn NU1900, license ImageSharp và CS8601 trong AuthController.
- Đo DOM trên 14 trang Khách thuê ở cả hai kích thước: `document.documentElement.scrollWidth` lần lượt 1425/345px, không vượt viewport 1440/360px. Trang Chủ nhà ở 360px cũng là 345px. Bảng dài cuộn trong vùng riêng.
- Không có console error trong lượt đo cuối. `git diff --check` đạt.
- Thay đổi duy nhất trong mã ứng dụng là CSS vùng tải ảnh; không đổi backend, nghiệp vụ hoặc schema. Không thêm thư viện hay test tự động cho thay đổi CSS này.
- Chưa nghiệm thu Safari, điện thoại vật lý, upload tệp thực tế hoặc các thao tác ghi nghiệp vụ. Báo hỏng, thông báo và yêu cầu trả phòng trong demo đang rỗng; không khẳng định đã kiểm tra các trạng thái có dữ liệu của chúng.
- Ô ngày và nút chọn tệp native còn dùng ngôn ngữ/định dạng của trình duyệt kiểm tra (tiếng Anh); nội dung ngày/tiền do ứng dụng hiển thị dùng định dạng Việt Nam. Không thay control native hoặc logic ngày/upload trong lần rà soát này.
