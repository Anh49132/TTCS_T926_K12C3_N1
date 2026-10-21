# S3-08, phần 2: Phát hành và thông báo hóa đơn

Nhánh: `feature/S3-08/02-invoice-publish-notify`, kế thừa phần sửa Nháp của nhánh 1.

## Thử trên web demo

1. Đăng nhập Chủ nhà, mở **Hóa đơn, thanh toán**, mở hóa đơn **Nháp**.
2. Nếu sửa chỉ số hoặc khoản tiền, bấm **Lưu nháp và tính lại tổng**. Có thay đổi chưa lưu thì nút phát hành bị khóa.
3. Bấm **Phát hành hóa đơn**. Hộp xác nhận hiển thị phòng, khách, kỳ và tổng tiền đã lưu. Kiểm tra ngày phát hành, hạn thanh toán, đánh dấu ô xác nhận rồi bấm **Xác nhận phát hành**.
4. Kết quả phải là **Đã phát hành**, có thời điểm phát hành; không còn ô sửa chỉ số, thêm khoản tiền hoặc nút lưu/phát hành.
5. Đăng xuất, đăng nhập Khách thuê đứng tên hợp đồng. Bấm biểu tượng chuông hoặc **Thông báo hóa đơn** trong menu; mở thông báo để xem hóa đơn chỉ đọc, có thể đánh dấu đã đọc.
6. Thử gửi lại yêu cầu phát hành, phát hành với bản cũ hoặc hợp đồng thiếu tài khoản khách đang hoạt động: không tạo thêm thông báo. Các vai trò khác và Chủ nhà của tòa khác không được phát hành.

Thông báo trong ứng dụng được lưu cùng transaction với trạng thái hóa đơn. Email chỉ được xử lý từ hàng đợi đã commit. Demo dùng thư mục `mail` trong thư mục demo để lưu email, không gửi ra địa chỉ thật. Môi trường thực dùng cấu hình SMTP và địa chỉ công khai đáng tin cậy trong `PasswordReset` như dịch vụ email hiện có. Email thất bại không làm mất thông báo trong ứng dụng hoặc đảo trạng thái hóa đơn; chính sách gửi lại email thất bại dành cho phần sau. Không cam kết SMTP giao đúng một lần khi tiến trình bị dừng ngay sau khi máy chủ email nhận thư.

## Dữ liệu và cách chạy lại

`./verification/Start-S308Demo.ps1` chạy tại cổng 5249, dùng bản demo trong `data/s308-demo/latest.txt`. Script nâng cấp chính bản demo được chọn, có backup; không dùng CSDL gốc. Không truyền đường dẫn CSDL thật vào tham số `Directory` khi thử.

Schema v21 bổ sung bảng `thong_bao`, khóa duy nhất hóa đơn/người nhận/loại và các cột hàng đợi email. Không thay dữ liệu nghiệp vụ cũ. Hóa đơn Nháp sửa snapshot; khi phát hành khóa bản chốt liên quan mà không ghi lại giá trị chỉ số nguồn. Chưa thêm hủy, hóa đơn thay thế hoặc giao diện lịch sử chỉnh sửa.

## Kiểm chứng tự động

Chạy tại gốc repository (cần .NET 10 và Chrome ở vị trí cài đặt chuẩn trên Windows):

```powershell
dotnet run --project verification/S308/S308.csproj -p:OutputPath="$pwd/data/s3082-check/" -- .
```

105 kiểm tra PASS trên SQLite tạm: phần Nháp, phát hành, rollback khi ghi thông báo lỗi, không thông báo khi phát hành thất bại, chống phát hành lặp, khóa sửa, quyền và CSRF, nâng cấp v20/v21 trên bản sao, email thành công/thất bại, trình duyệt xác nhận phát hành, chặn thay đổi chưa lưu, thông báo và xem hóa đơn của khách, đánh dấu đọc. Giao diện Chủ nhà/Khách thuê được kiểm tra ở chiều rộng 360px; ảnh được lưu trong thư mục kiểm thử tạm in ở cuối log. Chưa kiểm tra SMTP thật, Safari hoặc tải production. Đã thêm project kiểm chứng riêng để chạy mã xUnit có sẵn; kết quả hồi quy và giới hạn xem [báo cáo rà soát](s3082-ra-soat-loi.md). Giữ nguyên file test chưa theo dõi có sẵn.
