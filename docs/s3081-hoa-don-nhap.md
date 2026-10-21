# S3-08 — Lát 1: xem và sửa hóa đơn Nháp

Nhánh `feature/S3-08/01-invoice-draft-edit`. Luồng tạo trên web (từng hợp đồng, bảng kiểm tra tháng hoặc cả tòa) lưu trạng thái `NHAP`, chưa có người/thời điểm phát hành, chưa khóa chỉ số gốc và chưa gửi thông báo.

## Quy tắc tính tiền

- Giữ quy tắc hiện hành: tính bằng decimal, làm tròn từng dòng đến đồng bằng AwayFromZero, rồi cộng các dòng. Ví dụ 0,001 × 3.500 = 4 đồng.
- Chỉ số không âm, tối đa 3 chữ số thập phân; cuối không nhỏ hơn đầu. Sửa snapshot chỉ số trên hóa đơn, giữ đơn giá đã lấy theo ngày chốt và không sửa bảng chỉ số gốc.
- `PHAT_SINH` cộng vào tổng; `GIAM_TRU` lưu số dương và trừ khỏi tổng. Không cho tổng âm hay tràn số nguyên 64 bit.
- Mỗi khoản thêm có tên (1–200 ký tự), số tiền nguyên đồng > 0 và ghi chú (1–1000 ký tự). Hai ô khoản trống hoàn toàn được bỏ qua. Có thể thêm tiếp khi lưu lần sau.
- **Chờ PO xác nhận:** danh mục loại chi tiết. Lát này dùng hai nhóm PHAT_SINH/GIAM_TRU và tên tự nhập; chưa tự đặt danh mục thương mại. Làm tròn kế thừa AGENTS.md mục 3.3, chưa coi là PO xác nhận mới.

## Phân quyền và dữ liệu

Chỉ Chủ nhà có quyền ghi TAI_CHINH và sở hữu tòa được tạo/sửa Nháp. Kiểm tra backend, CSRF trên POST, phiên bản hóa đơn chống lưu đè. Hóa đơn đã phát hành không sửa được. Lưu dòng + tổng + phiên bản trong một transaction; lỗi rollback.

Không thay đổi schema: ánh xạ cột `chi_tiet_hoa_don.ghi_chu` đã có. Không chạy updater trên CSDL gốc. Không thêm chức năng phát hành/hủy/thay thế hoặc màn hình nhật ký; cơ chế audit sẵn có của DbContext tiếp tục hoạt động. Các hàm phát hành cũ giữ phục vụ mã hiện có, nhưng các action tạo trên web truyền chế độ tạo Nháp.

## Chạy kiểm thử tự động

Từ gốc dự án:

```powershell
dotnet run --project verification/S308/S308.csproj -p:OutputPath="$pwd/data/s308-check/" -- .
```

Chương trình tự tạo CSDL giả mới dưới thư mục tạm, không lấy DatabasePath của web hoặc CSDL local. Có ví dụ tính tay, tạo Nháp đơn lẻ/hàng loạt, chống trùng, sửa chỉ số, cộng/trừ khoản, ghi chú bắt buộc, tổng âm/tràn, phiên bản cũ, chủ nhà khác, bất biến hóa đơn phát hành, integrity/FK. HTTP kiểm tra đăng nhập, render form, binding hai khoản trống, CSRF, quyền ghi 4 vai trò và không tạo email.

File project của bộ xUnit hiện thiếu trong checkout; chương trình này dùng project riêng, không sửa các file test có sẵn. Chưa nghiệm thu trình duyệt trực quan 360px/Safari hoặc SMTP thật.

## Demo trên bản sao riêng

```powershell
.\verification\Start-S308Demo.ps1
```

Đã kiểm tra 46 kiểm tra service/HTTP PASS; đăng nhập và trang chi tiết Nháp demo trả HTTP 200. Hóa đơn demo sẵn có: `/HoaDonDichVu/Details/3`. Demo bổ sung hóa đơn đã phát hành cho khách thuê và đường dẫn trực tiếp theo mã được ghi ở [S3-07](s307-hoa-don-khach-thue.md).

Script dùng bản sao `data/s308-demo/.../s308.sqlite` được chuẩn bị trong phiên này; không đọc CSDL mặc định. Cổng mặc định 5249. Thông tin tài khoản demo nằm trong access.json của bộ Sprint 2 nguồn, không chép mật khẩu vào Git. Script dừng nếu bản sao chưa có.

1. Đăng nhập Chủ nhà, mở `/HoaDonDichVu/Monthly`, chọn tòa/kỳ chưa có hóa đơn và đủ chỉ số.
2. Bấm Xem → Tạo hóa đơn Nháp để chỉnh sửa, hoặc tạo Nháp cho các phòng đã chọn.
3. Mở chi tiết bản nháp; sửa chỉ số, nhập phát sinh và giảm trừ kèm ghi chú, lưu rồi xem tổng mới.
4. Thử bỏ ghi chú: hệ thống báo lỗi và không lưu tiền/chỉ số của lần gửi đó.
5. Kiểm tra trạng thái vẫn Nháp, chưa phát hành, chưa có email. Danh sách tháng có bản nháp để mở sửa lại.

Ví dụ: phòng 1.000.000 đồng, điện 100 → 120 với giá 3.500 đồng, phát sinh 50.000 và giảm trừ 20.000: tổng = 1.100.000 đồng.
