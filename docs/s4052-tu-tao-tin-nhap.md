# S4-05 phần 2 — Tự tạo tin Nháp sau trả phòng

Nhánh `feature/S4-05/02-auto-create-draft-listing` kế thừa `35dfa7f` của phần 1. Giữ luồng xác nhận tất toán, giải phóng phòng và các quy tắc tài chính hiện có; hoàn thiện phần tạo tin Nháp trong cùng transaction.

## Quy tắc triển khai

Hai lựa chọn đã được hỏi người dùng trong phiên làm việc, chưa nhận trả lời. Tạm dùng các phương án đề xuất dưới đây; đây là giả định triển khai, **không ghi nhận là PO đã xác nhận**:

- Chọn tin cùng phòng không ở trạng thái Nháp, mới nhất theo `ngay_dang` (dữ liệu cũ thiếu ngày đăng dùng `ngay_tao`), nếu bằng nhau lấy id lớn hơn. Không chọn tin của phòng khác hoặc Nháp thủ công mới tạo.
- Không có tin phù hợp, kể cả trường hợp chỉ có Nháp thủ công: tạo tin mới từ mã phòng và mô tả công khai của phòng. Không lấy ghi chú nội bộ. Nháp thủ công có sẵn vẫn giữ nguyên.
- Sao chép nguyên tiêu đề và mô tả, kể cả mô tả dài, rỗng hoặc NULL. Không sửa văn bản hoặc tự nối giá vào mô tả. Nếu mô tả có giá cũ viết bằng văn bản, Chủ nhà cần rà lại trước khi đăng; trường giá vẫn lấy đúng giá phòng hiện tại.
- Tin mới có id mới, trạng thái **NHAP**, `phong_id` của phòng vừa trả, `nguoi_dang_id` là Chủ nhà hiện tại, `tin_goc_id` liên kết nguồn khi có. Đặt ngày tạo UTC mới, ngày đăng/hết hạn NULL. Không sao chép trạng thái, người đăng, ngày đăng/hết hạn cũ và không tự công khai.
- Giá và ảnh vốn thuộc `phong_tro`/`anh_phong`, không có giá/ảnh riêng trên `tin_dang`. Tin mới dùng giá phòng hiện tại và cùng bộ ảnh theo thứ tự. Không nhân đôi hoặc ghi đè hàng ảnh/tệp. Nếu Chủ nhà thay ảnh phòng về sau, các tin cùng phòng dùng bộ ảnh mới theo thiết kế hiện có; phần này không tạo snapshot ảnh lịch sử mới.
- Tin nguồn không bị sửa mô tả, giá hay trạng thái. Nếu còn tin DANG_HIEN_THI, bước kiểm tra/xác nhận trả phòng bị chặn với hướng dẫn tạm ẩn tin trước, tránh tin cũ tự công khai lại khi phòng Trống. Không âm thầm đổi trạng thái tin cũ.
- Transaction SQLite immediate và xác nhận duy nhất theo hợp đồng hiện có ngăn gửi lặp/đồng thời tạo thêm Nháp. Nếu tạo tin hoặc ghi marker thất bại, rollback cả hợp đồng/phòng/hóa đơn/cấn cọc/lịch sử/audit của giao dịch.

## Giao diện và dữ liệu

Danh sách `/TinDang/QuanLy` vẫn theo phòng như hiện có. Tin mới có nhãn **Nháp tự tạo sau trả phòng** ngay dưới tiêu đề/mã tin để nhìn thấy cả ở mobile, chưa công khai. Mở sửa thấy nội dung đã sao chép và giá phòng hiện tại. Quyền quản lý tin giữ nguyên; khách/Chủ nhà khác không được mở Nháp.

Dùng bảng nhật ký chỉ thêm hiện có với action `TAO_NHAP_TRA_PHONG`, ghi id hợp đồng/phòng/tin nguồn và giá phòng tại thời điểm tạo để nhận diện nguồn tự động. Không ghi mô tả tự do, không thêm bảng/cột, không đổi schema v33/migration. CSDL gốc giữ nguyên SHA256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Giữ nguyên `UnitTest1.cs` sẵn có và các lỗi ngoài phạm vi của code gốc.

## Demo

```powershell
./verification/New-S4052Demo.ps1
./verification/Start-S4052Demo.ps1
```

Mỗi lần tạo một bộ mới trong `data/s4052-demo/`, không ghi đè dữ liệu cũ. Tài khoản/mật khẩu ở `access.json` của bộ mà `data/s4052-demo/latest.txt` trỏ tới; không commit các file này. Dùng `Owner`/`Password` trong file, mặc định tài khoản `owner.s404@demo.local`. Cổng **5252** tách demo phần 1.

1. Mở `http://localhost:5252/TatToan/XacNhan/1`. Hợp đồng 1 đã có bảng tất toán Nháp, tin cũ Đã cho thuê và 8 hàng ảnh minh họa dùng ảnh mẫu trong repository. Giá phòng đã đổi thành **4.500.000 đ/tháng**.
2. Kiểm tra số liệu, xác nhận đã kiểm tra rồi mở hộp xác nhận; đánh dấu hai bên đã đối chiếu, **Hoàn tất trả phòng**.
3. Mở `http://localhost:5252/TinDang/QuanLy`: phòng A103 có tin mới ở Nháp, nhãn tự tạo dưới tiêu đề, giá 4.500.000đ. Không có tin nào tự công khai.
4. Mở sửa tin mới: tiêu đề/mô tả cũ và 8 ảnh vẫn có, ngày đăng chưa có. Tin cũ vẫn giữ nguyên, liên kết nguồn lưu trong dữ liệu.
5. Gửi lại biểu mẫu xác nhận cũ: 409/đã xác nhận, không thêm Nháp. Sau một lượt test, tạo bộ demo mới để thử lại từ đầu.

## Kiểm thử

- **124/124 PASS** cho các ca tạo Nháp, giải phóng phòng, tất toán/xác nhận/PDF và các ca liên quan khác trong filter. Trong đó **13 ca mới phần 2** kiểm tra 0/1/8 ảnh, mô tả dài/NULL/rỗng, đổi người đăng, giá hiện tại, nhiều tin/tie theo id, phòng khác, không có nguồn/chỉ có Nháp thủ công, tin công khai, rollback insert/marker, gửi đồng thời và quyền xem Nháp. Log `data/s4052-verified.log`; TRX `data/s4052-verified-results/verified.trx`.
- Bộ tích hợp ảnh/kích hoạt/quản lý tin: **27 PASS, 3 FAIL / 30**. Tổng hai bộ không trùng: **151 PASS, 3 FAIL / 154**. Cả ba FAIL đã tồn tại trong kết quả phần 1 trên cùng nền `35dfa7f`, không có tên test lỗi mới trong phạm vi đối chiếu: `ChuNha_DangLaiVaGoTinCuaPhongMinh`, `TinQuaHan_TuDongTamAnVaCanhBaoChuNha`, `QuanLy_ChiDangVaGoTinCuaToaDuocPhanCong`. Log `data/s4052-integration.log`, TRX `data/s4052-integration-results/integration.trx`; đối chiếu `data/s405-all-results/all.trx` và [báo cáo phần 1](s4051-ket-qua-kiem-thu.md).
- Lỗi của lượt test đầu đã sửa: so sánh JSON ngày giờ phải đọc hai phía từ SQLite để tránh khác DateTimeKind; build sạch để không dùng DLL tạo trong lúc thêm guard. Không sửa kỳ vọng nghiệp vụ để bỏ qua lỗi.
- Edge desktop và 360px PASS trên giao diện cuối: hoàn tất trả phòng, gửi lại 409, nhãn Nháp dưới tiêu đề (không phải kéo ngang để thấy), giữ mô tả, giá hiện tại, khách chỉ đọc và trái quyền 403. Log `data/s4052-browser-final.log`; ảnh trong `data/s4052-demo/2ca3a763b9ea4ef98c68b304d041d6ab/browser-check-aca9d5a5/`. Demo cho người dùng là bản sao riêng chưa chốt, tách dữ liệu đã dùng kiểm thử.
- Chưa chạy lại toàn bộ 928 ca của phần 1 trong lượt này; không ghi là toàn dự án hết lỗi. Chưa kiểm chứng Safari, tải production, SMTP thật hoặc các lần merge tương lai. Chưa commit/push phần 2.

```powershell
dotnet build verification/S308Regression/Regression.csproj --no-restore -p:OutputPath="$pwd/data/s4052-check/"
dotnet test verification/S308Regression/Regression.csproj --no-build --no-restore -p:OutputPath="$pwd/data/s4052-check/" --filter 'FullyQualifiedName~AutoDraftListing|FullyQualifiedName~AutoReleaseRoom|FullyQualifiedName~XacNhan|FullyQualifiedName~TatToan'
```

## Kiểm thử lại ngày 15/10/2026

- Chạy lại cùng bộ nghiệp vụ: 124/124 PASS, không bỏ qua ca nào; `data/s4052-retest-results/retest.trx`.
- Chạy lại tích hợp: 27 PASS, 3 FAIL / 30. Đối chiếu tên test với lượt trước không có lỗi mới; ba lỗi cũ vẫn giữ nguyên. Kết quả: `data/s4052-integration-retest-results/integration-retest.trx`.
- Trình duyệt trên dữ liệu mới riêng tại cổng 5253: PASS tạo Nháp, mô tả cũ, giá hiện tại, nhãn tự tạo ở 360px, trạng thái phòng và làm mới; gửi lại 409, khách không sửa được và truy cập trái quyền 403. Log `data/s4052-browser-retest.log`; ảnh trong `data/s4052-retest-demo/04ee0a0b8a134c04bf6fb0006fc955ef/browser-check-060fd096/`.
- Tổng hai bộ tự động: 151 PASS, 3 lỗi cũ / 154. Không thay đổi mã nghiệp vụ trong lượt kiểm thử lại. CSDL gốc có SHA-256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`, không đổi. Con trỏ demo người dùng vẫn trỏ bộ `6cf34ec31ef04b0fa805e23e145e927a`; server thử 5253 đã dừng.
