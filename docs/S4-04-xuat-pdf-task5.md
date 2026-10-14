# S4-04 task 5 — Tải bảng tất toán PDF

PO đã chốt: A4, thương hiệu NhàTốt; thông tin hợp đồng/phòng/hai bên/ngày trả, tiền phòng–điện–nước, cọc, công nợ, từng hư hỏng, lý do và phạt trước hạn, kết quả hoàn hoặc thu thêm, người/thời điểm xác nhận (UTC+7), trạng thái Đã kết thúc. Không có căn cước hoặc ảnh giấy tờ. Các khoản thu/hoàn sau chốt không thay đổi tổng PDF.

## Triển khai

- `GET /TatToan/TaiPdf/{hopDongId}` trả PDF attachment, no-store, nosniff. Kiểm tra tài khoản hoạt động, quyền module và sở hữu hợp đồng bằng cùng chính sách xem bảng tất toán. Chủ nhà/khách thuê xem hợp đồng của mình; ADMIN giữ quyền chỉ xem hiện có. Trái quyền trả 403; chưa chốt trả 409.
- Tải từ banner kết quả trả phòng và mục Tài liệu liên quan, áp dụng cả giao diện Chủ nhà và Khách thuê. Bản nháp không có nút tải.
- QuestPDF đã có sẵn trong dự án, phông Unicode Lato nhúng trong PDF, bảng hóa đơn lặp tiêu đề khi sang trang. Không thêm thư viện hoặc schema.
- Đọc trong một transaction: bảng DA_CHOT, xác nhận, chi tiết khấu trừ, công nợ JSON tại chốt, chấm dứt trước hạn, hóa đơn cuối đã phát hành và các dòng snapshot. Chỉ đọc tên hai bên/phòng từ hồ sơ liên kết; không truy vấn sổ thu/hoàn hiện tại để tính tiền PDF.
- Dư nợ kỳ cuối sau chốt đã gồm hư hỏng/phạt: chỉ trừ một lần. Hiển thị riêng tổng hóa đơn và phần đã thanh toán/cấn trước chốt để đối chiếu. Đối chiếu kết quả với số tiền xác nhận; từ chối nếu dữ liệu không khớp.
- PDF sinh trong bộ nhớ sau kiểm tra quyền, không đặt file trong wwwroot, không lưu/chia sẻ đường dẫn công khai.

## Kiểm thử

`RoomServicesTests.TatToanPdf.cs` kiểm tra file PDF mở được bằng PdfPig, tiếng Việt có dấu, số tiền có/không phạt, hai bên xem cùng nội dung, HTTP attachment/no-store, 403 chủ nhà ngoài hợp đồng, từ chối nháp, giữ nguyên nội dung sau thu/hoàn và 40 khoản hư hỏng nhiều trang. Không thay đổi file project test.

Chạy bộ hồi quy:

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~TatToan|FullyQualifiedName~ChamDut|FullyQualifiedName~XacNhan|FullyQualifiedName~Schema|FullyQualifiedName~Collection|FullyQualifiedName~BaoHong|FullyQualifiedName~TenantInvoice|FullyQualifiedName~InvoiceDetail'
```

Tạo demo mới (không ghi đè demo cũ):

```powershell
.\verification\New-S404Demo.ps1 -Task5
.\verification\Start-S404Demo.ps1 -Task5 -Port 5255
```

Task5 mode chốt hợp đồng 1 có phạt và tạo sẵn PDF để đối chiếu. Thông tin đăng nhập chỉ nằm trong access.json của thư mục data demo. Mở `/TatToan/ChiTiet/1` bằng Chủ nhà hoặc Khách thuê để tải. Hợp đồng 2 vẫn cho phép demo trả phòng mới. Không nâng cấp CSDL đang dùng hoặc thay mật khẩu tài khoản cũ.

## Kết quả xác minh 15/10/2026

- Build thành công; cảnh báo ImageSharp license và nullable AuthController đã có sẵn.
- Hồi quy 192/192 đạt; sau đó nhóm PDF đầy đủ 4/4 đạt, có ca bổ sung tiền phòng 13/31 ngày, điện/nước từ chỉ số cuối, tổng 795.355 đ. Báo cáo: `data/s404-task5-test-results/s404-task5-final.trx` và `s404-task5-pdf-complete.trx`.
- Edge desktop và 360px: hai vai trò có nút tải, trả application/pdf mở được, chủ nhà/khách thuê ngoài hợp đồng bị 403. Ảnh và hai PDF tải thực tại `data/s404-task5-demo/20261015-task5-verified/browser-check-840d7e1e/`.
- Demo `http://localhost:5255/TatToan/ChiTiet/1`; PDF mẫu `Bang-tat-toan-HD-2026-0024.pdf` trong thư mục demo. Cọc 7.000.000 đ, kỳ cuối gồm hư hỏng/phạt 5.813.742 đ, nợ cũ 1.200.000 đ, khách trả thêm 13.742 đ, khớp trang kết quả.
- Chưa kiểm tra Safari hoặc thiết bị thật. Không chạy updater trên DB đang dùng, không đổi schema cho task 5, không commit DB/credential.
