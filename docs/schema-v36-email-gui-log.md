# Schema v36: nhật ký gửi email hóa đơn

Phiên bản 36 bổ sung bảng `email_gui_log` để lưu trạng thái gửi và thử lại email hóa đơn. Bản cập nhật chỉ tạo bảng và chỉ mục khi chưa có; không sửa hóa đơn, thanh toán hoặc nhật ký hiện có. Bảng có khóa ngoại `RESTRICT`, khóa chống gửi trùng theo hóa đơn/người nhận/loại thông báo và chỉ mục cho hàng đợi thử lại.

`DatabaseUpdates.Update` tạo bản sao lưu trước khi chạy cập nhật. Để nâng cấp thủ công, dừng ứng dụng rồi chạy:

```powershell
dotnet run --project QL_PhongTro -- --update-database
```

Giữ nguyên `DatabasePath` giữa bước nâng cấp và bước chạy ứng dụng. Kiểm tra kết quả bằng `PRAGMA integrity_check`, `PRAGMA foreign_key_check` và `DatabaseUpdates.Check`. Bộ test xác minh migration trên SQLite tạm; không cập nhật cơ sở dữ liệu đang dùng.
