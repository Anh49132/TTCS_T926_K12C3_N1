# Hướng dẫn đọc dự án Quản lý phòng trọ

## Quyết định công nghệ hiện hành

- Backend: ASP.NET Core MVC, ngôn ngữ C#.
- Giao diện: HTML5 và CSS3 trong Razor Views (`.cshtml`); dùng JavaScript khi cần tương tác. Không dùng React.
- CSDL: giữ nguyên CSDL SQLite và dữ liệu hiện có; không tạo CSDL mới.
- Truy cập dữ liệu: Entity Framework Core ánh xạ vào schema hiện có; không tự ý đổi tên bảng/cột hoặc chạy migration làm thay đổi cấu trúc/dữ liệu.
- Nghiệp vụ: giữ các yêu cầu của hệ thống hiện tại. Mô hình 22 bảng trong `02-csdl.dbml` là đặc tả tham chiếu; chỉ triển khai phần cần cho task.
- Tài liệu `01-yeu-cau-he-thong.md` được trích từ Excel. Stack ở nguồn Excel là nội dung lịch sử, các lựa chọn công nghệ được cập nhật trong tài liệu này là quyết định hiện hành.

## Thứ tự đọc

1. Đọc hướng dẫn này và yêu cầu task mới nhất.
2. Đọc tài liệu tiến độ nếu có trong dự án.
3. Đọc `02-csdl.md` hoặc `02-csdl.dbml` để nắm schema; hai file chứa cùng mô hình.
4. Đọc `01-yeu-cau-he-thong.md` để hiểu yêu cầu nghiệp vụ và backlog.
5. Kiểm tra mã nguồn, cấu hình và CSDL thực tế trước khi sửa.

DBML là văn bản UTF-8, không cần cài trình phân tích DBML để đọc. Không coi DBML là migration đã chạy hoặc bằng chứng schema trong tệp SQLite đã khớp hoàn toàn.

## Quy tắc giữ tương thích

- Sau khi pull thay đổi CSDL, làm theo [quy trình cập nhật SQLite](cap-nhat-csdl.md): dừng app, chạy `--update-database`, rồi `--check-database`. Schema v6 thêm `tin_dang` và `anh_phong`, nên DB cũ phải được cập nhật trước khi web khởi động. Updater cũng áp dụng bước tái sử dụng tài khoản v4 → v5 nếu còn thiếu. Mỗi PR đổi schema phải kèm bước nâng cấp có phiên bản và xác minh bảo toàn dữ liệu trên bản sao.

- Yêu cầu trực tiếp mới nhất của người dùng ưu tiên hơn ghi chú cũ trong tài liệu.
- Khi cập nhật README, giữ nguyên khối lệnh chạy nhanh (dừng phiên cũ, restore, cập nhật DB, cấu hình/tạo ADMIN và chạy web). Theo yêu cầu trực tiếp ngày 02/10/2026, không tự thay đổi email, mật khẩu hoặc số điện thoại ADMIN mẫu trong khối này; chỉ đổi khi người dùng yêu cầu rõ. Quy ước này áp dụng cho ví dụ README, không đặt lại mật khẩu tài khoản trong database.
- Giữ nguyên chức năng và quy tắc nghiệp vụ hiện hành khi thay công nghệ.
- Giữ nguyên CSDL cũ: trước khi ánh xạ, kiểm tra bảng, cột, khóa và kiểu dữ liệu thực tế. Nếu schema thực tế khác DBML, báo rõ và không tự sửa schema.
- Không xóa dữ liệu, migrations hoặc bảng hiện có. Không chạy lệnh tạo/sửa schema trên CSDL cũ nếu chưa được yêu cầu rõ.
- Không tự triển khai toàn bộ 22 bảng hoặc backlog; chỉ làm phạm vi task.
- Không lưu mật khẩu dạng rõ; dùng cơ chế băm mật khẩu an toàn của ASP.NET Core và kiểm tra tương thích khi chuyển dữ liệu tài khoản cũ.
- Với đăng nhập bằng cookie, bật các biện pháp bảo vệ phù hợp và xác thực quyền ở backend; không dựa riêng vào ẩn/hiện giao diện.
- Khi kết thúc, ghi rõ phạm vi đã hoàn thành, giả định và việc còn lại trong tài liệu tiến độ của dự án nếu có.

Đây là bộ tài liệu yêu cầu và mô hình dữ liệu; mã nguồn thực tế là nguồn xác nhận cấu hình và schema đang chạy.
