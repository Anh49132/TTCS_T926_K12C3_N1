# Email báo hỏng và cập nhật — v41

## Sự kiện và người nhận

| Sự kiện mới | Người nhận |
| --- | --- |
| Khách gửi báo hỏng | Chủ nhà sở hữu tòa và quản lý đang được phân công |
| Quản lý đổi trạng thái: tiếp nhận, hoàn thành, từ chối | Khách đã gửi báo hỏng và chủ nhà |
| Quản lý gửi phản hồi | Khách đã gửi báo hỏng và chủ nhà |

Không gửi cho chính người thao tác hoặc tài khoản không hoạt động/chưa xác thực. Trước khi gửi kiểm tra lại quyền module Báo hỏng, vai trò, quyền sở hữu/phân công, email và trạng thái tài khoản; thay đổi quyền thì hủy thư đang chờ. Không gửi cho người ở ghép khác hoặc quản lý tòa khác.

Email có mã báo hỏng, phòng/tòa, thiết bị, mức Thường/Gấp, trạng thái, thời điểm giờ Việt Nam và liên kết chi tiết đúng vai trò. Nội dung phản hồi tự do, mô tả và ảnh chỉ xem sau đăng nhập, không đính kèm trong email. Không tự gửi bù các báo hỏng/lịch sử cũ. Chưa thêm email cho thay đổi chi phí sửa chữa riêng.

## Gửi thư và xử lý lỗi

- Hàng đợi `email_bao_hong` lưu trong cùng transaction với báo hỏng/phản hồi/đổi trạng thái. Lỗi ghi hàng đợi hoàn tác nghiệp vụ. Unique theo sự kiện/người nhận, gửi lại cùng biểu mẫu không nhân bản thư.
- Tác vụ chạy khoảng 30 giây/lần, xử lý tối đa 10 thư/lượt. Khóa nhận việc hai phút để tránh hai worker gửi đồng thời. Khởi động lại tiếp tục thư chờ và thu hồi lượt bị treo khi hết khóa.
- Tối đa bốn lượt: một lần đầu và ba lần thử lại sau 5, 10, 15 phút; hết lượt chuyển Thất bại. Lỗi lưu thông báo chung, không lưu thông tin bí mật của SMTP.
- Gửi mail là tác vụ ngoài transaction. SMTP không bảo đảm exactly-once: nếu máy dừng sau khi máy chủ đã nhận thư nhưng trước khi ghi thành công, lần xử lý lại có thể gửi trùng. Unique ngăn tạo trùng công việc nhưng không loại bỏ được trường hợp này.
- Admin: **Tra cứu gửi thông báo lỗi → Báo hỏng và cập nhật**, lọc trạng thái, xem số lượt gửi/lỗi/lịch thử tiếp, 50 dòng/trang. Chủ nhà/quản lý/khách không được truy cập nhật ký email toàn hệ thống.

## Cấu hình và nâng cấp

Dùng chung `PasswordReset__*` với email hiện có. `PasswordReset__PickupDirectory` có đường dẫn thì thư chỉ lưu `.eml`/`.txt` local. Muốn gửi tới hộp thư thật cần tắt pickup, cấu hình SMTP hợp lệ và khởi động lại server. `PublicBaseUrl` dùng HTTPS; Development cho phép HTTP loopback. Không đổi SMTP/credential của người dùng trong task này, không gửi email thử tới tài khoản thật.

Schema v41 chỉ thêm hàng đợi và index, không sửa/xóa dữ liệu nghiệp vụ cũ. Updater có backup tự động. Dừng server, dùng đúng DatabasePath khi chạy:

```powershell
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

Không chạy updater trên DB đang dùng trước khi được người dùng cho phép theo AGENTS.md §3.1. Web yêu cầu v41; `run.bat` không tự nâng DB cũ.

## Kiểm chứng

- Build đạt, cảnh báo ImageSharp/CS8601 có sẵn.
- 86 kiểm thử báo hỏng/schema/email đạt, trong đó bảy ca mới về người nhận, idempotency, retry qua nhiều scope, thu hồi quyền/phân công, rollback, khóa hết hạn, tạo thư pickup/liên kết an toàn và HTTP nhật ký Admin. TRX: `data/repair-email-tests/repair-email-regression-final.trx`.
- Bản sao từ DB v40 nâng v41 giữ nguyên 58 bảng/25.130 dòng cũ; integrity/FK đạt, chạy updater lần hai không thay dữ liệu, check schema đạt. Báo cáo: `data/repair-email-upgrade/c8020af1bfad4880a2a5bcfc9614071f/report.json`. Tái kiểm bằng `python verification/repair_email_upgrade.py <database-nguon>` (chỉ cập nhật bản sao mới).
- Người dùng đã cho phép sao lưu/nâng DB đang dùng: v40→v41 đạt, backup tự động được tạo; giữ nguyên 58 bảng/25.130 dòng cũ, integrity/FK và check schema đạt. Hàng đợi mới rỗng, không gửi bù lịch sử. Báo cáo: `data/repair-email-live-upgrade/report.json`.
- Chưa nghiệm thu gửi SMTP tới inbox thật; cấu hình pickup của người dùng giữ nguyên. Server vẫn tắt. Chưa push.
