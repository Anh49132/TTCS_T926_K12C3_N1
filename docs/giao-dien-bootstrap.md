# Giao diện Bootstrap NhàTốt

## Phạm vi

- Dùng Bootstrap 5.3.3 local và Razor, không thêm dependency giao diện.
- Nút/menu #30343B, điểm nhấn #BFA77A, nền #F5F5F4, thẻ trắng, chữ #25282D.
- `bootstrap-brand.css`: thành phần chung và các chỉnh sửa có giới hạn theo trang.
- `rental-workspace.css`: chỉ nạp ở module phòng, hợp đồng và hóa đơn.
- `document-print.css`: chỉ nạp cho chế độ in của trang chi tiết hợp đồng/hóa đơn.
- Trang chủ công khai hiển thị tối đa 6 tin mới từ dữ liệu `TinMoiNhat` đã được backend lọc; không có dữ liệu mẫu gắn cứng.
- Không đổi schema, giá, quyền backend, mật khẩu hay dữ liệu thật. Các thao tác đổi ảnh dùng lại endpoint và CSRF hiện có.

## Kiểm tra tự động

```powershell
dotnet restore Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore
Get-ChildItem verification -Filter '*.test.cjs' | ForEach-Object {
    node $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Test thất bại: $($_.Name)" }
}
git diff --check
```

Các test `.test.cjs` dùng script giao diện thật với DOM/fetch/đồng hồ giả. Chúng không mở trình duyệt, chụp ảnh hoặc truy cập DB. Không coi chúng là bằng chứng đã nghiệm thu bố cục thực tế.

Test .NET dùng fixture DB tạm. Chỉ chạy bộ test sau khi kiểm tra cấu hình fixture; không trỏ test vào `local-dev.sqlite`.

## Các bước nghiệm thu còn lại

Theo yêu cầu người dùng, không tự chụp ảnh màn hình. Khi kiểm tra trên dữ liệu demo/bản sao riêng:

1. Ở 360px, kiểm tra trang chủ, danh sách/chi tiết tin, phòng, hợp đồng và hóa đơn không làm trang tràn ngang. Bảng dài chỉ cuộn trong vùng bảng.
2. Dùng Tab, Shift+Tab, Escape và phím mũi tên kiểm tra menu công khai, menu khách thuê và bộ lọc tùy chỉnh; focus luôn nhìn thấy.
3. Kiểm tra form có lỗi: chữ lỗi tương ứng đúng trường, hướng dẫn vẫn còn, sửa trường làm trạng thái lỗi cập nhật. Thử trình đọc màn hình với cả ô native và hộp chọn cơ sở/trạng thái.
4. Với ảnh phòng demo, thử nút Trước/Sau, kéo thả, tải lại trang để đối chiếu thứ tự, kiểm tra lỗi mạng có khôi phục thứ tự. Không thao tác trên ảnh thật khi chưa có yêu cầu.
5. Giả lập mất mạng khi xem hóa đơn; thử tải lại, giữ bộ lọc/trang và hiển thị lỗi quyền phù hợp. Kiểm tra lỗi thông báo giữ đến khi đóng, thông báo thành công dừng đếm khi hover/focus.
6. Kiểm tra bản xem trước khi in: bỏ menu/nút/form, không mất dòng tiền hoặc ghi chú; lịch sử nhiều trang có tiêu đề cột. Bộ sinh PDF hợp đồng hiện có không thay đổi.
7. Kiểm tra Chrome, Safari và thiết bị cảm ứng thật. Hiện chưa có kết quả nghiệm thu cho các bước này.

Không tự chạy updater, đổi tài khoản mẫu, gửi email thật hoặc xuất/in chứng từ thật để kiểm thử giao diện.
