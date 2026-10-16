# Thông tin nhận chuyển khoản của chủ nhà — 16/10/2026

> Cập nhật giao diện hóa đơn theo yêu cầu tiếp theo: danh sách hiện mở thẳng `/ThongBao/HoaDon/{id}`. Màn `ThongBao/ChiTiet` và JavaScript riêng đã gỡ; URL cũ theo mã chỉ chuyển hướng sau khi kiểm tra sở hữu. Khối ngân hàng được giữ trong form báo thanh toán của trang HoaDon thống nhất. Những mô tả hai form bên dưới là trạng thái tại lần bàn giao ngân hàng ban đầu.

Chủ nhà vào **Hồ sơ của tôi** (`/HoSo/CuaToi`) → **Thông tin nhận chuyển khoản** → **Chỉnh sửa thông tin**. Nhập ngân hàng, số tài khoản; có thể tải QR JPG/JPEG/PNG tối đa 5 MiB. Có xem trước, thay/xóa ảnh, hủy chỉnh sửa và lưu bằng AJAX. Xóa ảnh chỉ có hiệu lực sau khi lưu. Không bắt buộc có QR để lưu ngân hàng và số tài khoản.

Khách chọn **Chuyển khoản** trong form báo thanh toán để xem thông tin ngân hàng hiện hành của chủ nhà sở hữu phòng. Khối này đứng trước ảnh chứng từ, có nút sao chép số tài khoản; chọn **Tiền mặt** thì ẩn. Áp dụng cho cả `/ThongBao/ChiTiet?maHoaDon=...` và `/ThongBao/HoaDon/{id}`. Khi chưa cấu hình hoặc không tải được QR, hiển thị thông báo tương ứng và không đổi hình thức thanh toán.

QR là ảnh do chủ nhà cung cấp. Không sinh QR động, không gọi ngân hàng, không đọc số tiền từ QR. Xem QR không tạo khoản thu. Báo thanh toán vẫn chờ xác nhận và chỉ được tính vào đã thu sau khi chủ nhà xác nhận theo luồng hiện có. Header, sidebar và danh sách hóa đơn giữ nguyên.

## Schema v38

Thêm `tai_khoan_ngan_hang`, không sửa cấu trúc hoặc dữ liệu bảng nghiệp vụ cũ:

| Cột | Nội dung |
| --- | --- |
| `chu_nha_id` | PK, FK `tai_khoan(id)`, ON DELETE RESTRICT; một cấu hình/chủ nhà |
| `ten_ngan_hang` | Bắt buộc, trim, tối đa 120 ký tự |
| `so_tai_khoan` | Bắt buộc, 1–50 chữ/số ASCII, giữ số 0 đầu |
| `qr_key`, `qr_content_type` | Nullable, khóa file riêng tư và MIME đã xác minh |
| `ngay_tao`, `ngay_cap_nhat` | UTC qua `ITimeProvider` |
| `phien_ban` | Token khóa lạc quan; dữ liệu đã đổi trả 409 |

Ảnh lưu trong `<DatabasePath>.bank-qr`, ngoài `wwwroot`; không lưu base64 hoặc nhân bản thông tin ngân hàng vào hóa đơn. Backend tái sử dụng kiểm tra extension, nội dung JPG/PNG, kích thước, giải mã và giới hạn pixel của `BaoHongImageStore`. File mới được ghi xong trước khi commit DB; thất bại thì rollback, dọn file mới, giữ file cũ. Chỉ dọn file cũ sau commit. Như các storage hiện có, crash hoặc lỗi xóa file có thể để lại file mồ côi; không ảnh hưởng quyền truy cập vì URL không nhận khóa file tùy ý.

Updater thêm v38 sau v37, có transaction, integrity/FK check và bản sao lưu tự động theo cơ chế hiện có. Chạy lại không thay dữ liệu. Đã thử nâng bản sao DB đang dùng: dữ liệu trong cả 53 bảng cũ giữ nguyên, integrity `ok`, FK không lỗi. DB đang dùng chưa được nâng cấp trong task này theo quy tắc AGENTS.md §3.1.

**Cập nhật sau lỗi run.bat do người dùng gửi ngày 16/10/2026:** đã sao lưu và nâng DB acceptance đang dùng lên v38; kiểm tra giữ nguyên 53 bảng cũ / 25.036 bản ghi, integrity=ok/FK=0, `run.bat --check-only` đạt. Ghi chú DB chưa nâng ở trên là trạng thái khi bàn giao tính năng ban đầu.

Với các DB khác, dừng web, sao lưu ra ngoài repository, chọn **đúng file DB đang sử dụng** trước khi chạy:

```powershell
$env:DatabasePath = 'D:\duong-dan\database-dang-dung.sqlite'
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
.\run.bat
```

`dotnet run` không tự đọc `.env.local`; phải đặt cùng `DatabasePath` như `run.bat`. Không chạy updater cùng web trên một file. Nếu phục hồi bản sao v37 thì cần chạy mã tương thích v37, hoặc nâng lại trước khi chạy mã v38.

## API và quyền

Tất cả response thông tin/ảnh có `no-store`. Không trả khóa file hoặc đường dẫn storage trong JSON.

| Method / route | Quyền và tác dụng |
| --- | --- |
| GET `/api/ngan-hang/cua-toi` | CHU_NHA lấy cấu hình của chính mình; `{bank:null}` khi chưa có |
| POST `/api/ngan-hang/cua-toi` | CHU_NHA, CSRF, multipart; tạo/cập nhật cấu hình của chính mình |
| GET `/api/ngan-hang/cua-toi/qr` | CHU_NHA xem QR của mình |
| GET `/api/ngan-hang/hoa-don/{id}` | KHACH_THUE + quyền TAI_CHINH; cấu hình chủ nhà của hóa đơn được phép xem |
| GET `/api/ngan-hang/hoa-don/{id}/qr` | Cùng kiểm tra quyền hóa đơn, trả nội dung ảnh private |

POST nhận `TenNganHang`, `SoTaiKhoan`, `AnhQr` tùy chọn, `XoaQr`, `PhienBan`. Không nhận OwnerId; tài khoản đích luôn lấy từ phiên đăng nhập. JSON `bank` chứa `tenNganHang`, `soTaiKhoan`, `coQr`, `phienBan`, `ngayCapNhat`. Lỗi validation trả 400 và `errors` theo tên trường; xung đột phiên bản hoặc lỗi lưu trả 409. Thiếu CSRF trả 400; chưa đăng nhập trả 401; trái quyền trả 403; chưa có ảnh trả 404.

Service kiểm tra tài khoản còn hoạt động, chưa xóa và đúng vai trò. QUAN_LY, ADMIN và khách không được sửa cấu hình chủ nhà. Khách chỉ đọc hóa đơn đã phát hành, thuộc hợp đồng đã chốt: người đứng tên hoặc người ở ghép có khoảng cư trú giao với kỳ hóa đơn (bao gồm hai đầu). Chủ nhà được xác định qua `hoa_don → hop_dong → phong_tro → toa_nha.chu_nha_id`. Không dựa riêng vào quyền xem thông báo. Hóa đơn trái quyền và QR tương ứng đều trả 403, không chứa dữ liệu ngân hàng. Tính năng này không mở rộng quyền báo thanh toán cho người ở ghép của luồng cũ.

## Các file thay đổi

- Mới: `Models/TaiKhoanNganHang.cs`, `Data/OwnerBankSchema.cs`, `Services/OwnerBankService.cs`, `ViewModels/OwnerBankViewModels.cs`, `Controllers/OwnerBankController.cs`.
- Tích hợp: `Data/AppDbContext.cs`, `Data/DatabaseUpdates.cs`, `Program.cs`.
- Giao diện: `Views/HoSo/CuaToi.cshtml`, `_BankSettings.cshtml`, `Views/Shared/_InvoiceBank.cshtml`, `Views/HoaDonDichVu/Details.cshtml`, `Views/ThongBao/ChiTiet.cshtml`, `wwwroot/css/owner-bank.css`, `wwwroot/js/owner-bank.js`, `wwwroot/js/invoice-bank.js`.
- Test mới: `Tests/QL_PhongTro.Tests/RoomServicesTests.OwnerBank.cs`; cập nhật kỳ vọng phiên bản hiện hành 37 → 38 trong các test schema sẵn có. Không sửa test csproj.
- Các file test cập nhật phiên bản (cùng thư mục test): `BaoCaoDoanhThuTests.cs`, `BaoHongChiPhiReconciliationTests.cs`, `BaoHongChiPhiSchemaTests.cs`, `BaoHongTests.cs`, `BaoHongTrackingTests.cs`, `InvoicePaymentSchemaTests.cs`, `LichHenMigrationTests.cs`, `MeterAnomalySchemaTests.cs`, `PermissionTests.cs`, `RoomServicesTests.TatToan.cs`, `RoomServicesTests.XacNhan.cs`, `SchemaBranchReconciliationTests.cs`.
- Kiểm thử trình duyệt: `verification/OwnerBankBrowser/OwnerBankBrowser.csproj`, `Program.cs`; đọc credential từ `access.json` riêng tư của bản QA, không nhúng tài khoản trong mã.
- Tài liệu: file này và `tien-do.md`.
- `.gitignore`: loại các thư mục `*.bank-qr/` khỏi Git, kể cả khi DB dùng vị trí mặc định trong dự án.

## Kiểm thử

Các test ngân hàng kiểm tra lưu/thay/xóa QR, bắt buộc ngân hàng/số tài khoản, file sai loại/hỏng/rỗng/quá 5 MiB, khóa lạc quan, upload/DB thất bại, quyền chủ nhà/khách/người ở ghép/quản lý/ẩn danh, CSRF, chống chọn chủ nhà khác bằng form, QR private, chưa cấu hình, bảo toàn hóa đơn và chỉ cập nhật dư nợ sau xác nhận. Test nâng cấp dùng DB tạm và bản sao; kiểm tra sao lưu, bảo toàn dữ liệu và chạy lặp.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~OwnerBank'
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore
dotnet run --project verification/OwnerBankBrowser -- '<thu-muc-QA-rieng>' http://localhost:5251
```

Thư mục QA cần `access.json` dạng `accounts:[{role,email,password}]`, `invoice-code.txt` chứa mã hóa đơn của khách, `qr-test.png`; chỉ chạy trên DB giả/bản sao. Browser kiểm tra lỗi bắt buộc cạnh trường, lưu AJAX, upload/xem QR, chủ nhà hiện hành, đổi chuyển khoản/tiền mặt, hủy chỉnh sửa và không tràn ngang ở 360px.

Kết quả: build đạt; lần full suite 1.149/1.151 đạt, 2 lỗi kỳ vọng phiên bản dạng chuỗi trong test migration đã sửa 37 → 38. Chạy lại cả nhóm migration đạt 4/4. Như vậy 1.151 ca duy nhất đã đạt qua các lần chạy; chưa có một lần full suite xanh sau sửa kỳ vọng. Nhóm kiểm tra riêng ban đầu 17/17 và 2 test bổ sung nâng cấp/thanh toán đều đạt. Browser lượt cuối đạt, đã kiểm tra ảnh desktop/360px. Log/TRX/ảnh nằm trong `data/owner-bank-*` ignored, chi tiết trong `tien-do.md`. Chưa nghiệm thu Safari hoặc thiết bị cảm ứng thật. Cảnh báo ImageSharp license và CS8601 trong AuthController là các cảnh báo sẵn có.
