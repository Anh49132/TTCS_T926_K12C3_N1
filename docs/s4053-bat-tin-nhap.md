# S4-05 phần 3 — Đăng tin Nháp bằng một nút

Nhánh `feature/S4-05/03-publish-draft-listing`, nền `ed0100a` của phần 2. Phạm vi: bật tin Nháp đã lưu thành tin công khai; chưa làm danh sách phòng vừa trống trên trang chủ.

## Quy tắc và giao diện

Người dùng đã xác nhận **giữ quy tắc hiện tại: mô tả và ảnh không bắt buộc**. Tiêu đề bắt buộc, tối đa 200 ký tự; mô tả nếu có tối đa 4.000 ký tự theo form hiện tại. Giá thuê, diện tích, sức chứa phải dương; có địa chỉ cơ sở; phòng Trống, tòa hoạt động và không có tin công khai khác của cùng phòng. Nếu tin cũ có mô tả dài hơn giới hạn form, bản Nháp vẫn giữ nguyên nội dung và từ chối đăng kèm hướng dẫn rút gọn, không tự cắt nội dung.

Chủ nhà sở hữu tòa hoặc quản lý được phân công có quyền ghi `TIN_DANG` được đăng. ADMIN vẫn giữ giao diện quản lý hiện có nhưng không có thao tác đăng Nháp mới. Khách/ẩn danh/trái phạm vi/thiếu CSRF bị chặn ở backend.

Tại `/TinDang/QuanLy`, mỗi tin Nháp có nút **Đăng tin** ngay dưới tiêu đề, nhìn thấy ở 360px. Thành công quay lại danh sách, thông báo tin đang hiển thị, badge Công khai và ngày đăng cập nhật; nút Đăng tin biến mất. Tin chưa hợp lệ giữ Nháp và hiện đầy đủ các lỗi được phát hiện. Trang sửa vẫn cho chỉnh nội dung rồi đăng theo luồng cũ.

## Giao dịch và tương thích

- Thêm partial controller `TinDangController.Publish.cs`, tái sử dụng EF, bảng và nhật ký hiện có; không nâng schema, không cập nhật CSDL gốc.
- Nút mới chỉ chuyển `NHAP → DANG_HIEN_THI` trên cùng bản ghi, giữ tiêu đề, mô tả, liên kết nguồn và ảnh phòng. Giá hiển thị vẫn lấy từ hồ sơ phòng hiện tại. Ghi ngày UTC và hết hạn sau 30 ngày.
- Mở SQLite transaction IMMEDIATE trước khi đọc trạng thái/phòng/quyền sở hữu; ghi trạng thái và audit cùng transaction. Lỗi lưu/nhật ký thì rollback. Bấm lại tin đã đăng không đổi ngày đăng/hết hạn, không tạo bản ghi hoặc audit mới. Hai Nháp cùng phòng chỉ một bản được đăng.
- Menu đổi trạng thái Nháp và form sửa khi đăng Nháp cũng dùng kiểm tra chung. Form lưu tin hiện tại được mở transaction trước khi đọc để hạn chế tranh chấp với thao tác đăng một nút. Không viết lại các luồng yêu cầu thuê, hợp đồng, hóa đơn hoặc tất toán.
- Ban đầu giữ nguyên ba test giao diện cũ ngoài phạm vi. Sau yêu cầu xử lý của người dùng ngày 15/10/2026, đã cập nhật kỳ vọng giao diện của ba test này; xem phần kết quả mới cuối tài liệu. Không sửa file `UnitTest1.cs` của người dùng, dữ liệu/ảnh/mật khẩu demo hoặc DB thật.

## Tự kiểm tra trên web

Trong PowerShell tại thư mục dự án:

```powershell
.\verification\New-S4053Demo.ps1
.\verification\Start-S4053Demo.ps1
```

Demo mới riêng trong `data/s4053-demo/`, cổng 5253; tài khoản/mật khẩu trong `access.json` của thư mục mà `data/s4053-demo/latest.txt` trỏ tới. Không commit thông tin này. Không ghi đè bộ demo phần 2.

1. Đăng nhập Owner, mở `/TatToan/XacNhan/1`, đối chiếu và hoàn tất trả phòng theo hai ô đồng ý trên màn hình.
2. Mở `/TinDang/QuanLy`, thấy A103 có Nháp tự tạo; ảnh/mô tả cũ, giá hiện tại 4.500.000đ.
3. Bấm **Đăng tin** đúng một lần. Danh sách đổi sang Công khai, có ngày đăng và không còn nút Đăng tin của bản này.
4. Mở chi tiết công khai, kiểm tra mô tả, ảnh và giá. Gửi lại request cũ không tạo thêm tin/lượt đăng.
5. Muốn thử lỗi: dùng bộ demo khác, xóa tiêu đề rồi lưu Nháp hoặc đổi phòng sang không Trống trước khi bấm; tin phải giữ Nháp và có lý do. Các giá trị giá/diện tích/sức chứa 0 vốn bị SQLite chặn, chỉ được tiêm vào CSDL dùng một lần trong test để kiểm tra phòng vệ backend.

## Bằng chứng kiểm thử

- Edge desktop/360px trên dữ liệu riêng: PASS hoàn tất trả phòng, tạo Nháp, bấm nút thật đăng tin trong **0,96 giây**, nút biến mất, nội dung/giá công khai đúng và gửi lặp thành công mà không tạo thêm lượt. Log `data/s4053-browser.log`; ảnh `data/s4053-demo/b587f809673c434b99010d2eb6f5c078/browser-check-0c471482/`. Đây là thời gian đo trên máy local/dữ liệu demo, không cam kết thời gian production.
- Tích hợp ảnh/kích hoạt hợp đồng/tin công khai/quản lý tin: **36 PASS, 3 FAIL / 39**; `data/s4053-integration-results/integration.trx`. Ba lỗi cũ: `ChuNha_DangLaiVaGoTinCuaPhongMinh`, `QuanLy_ChiDangVaGoTinCuaToaDuocPhanCong`, `TinQuaHan_TuDongTamAnVaCanhBaoChuNha`. Đối chiếu kết quả phần 2, không sửa kỳ vọng của các test này.
- **28/28 ca mới PASS** trên bản mã cuối, không bỏ qua ca nào: `data/s4053-final-results/publish-final.trx`, log `data/s4053-final.log`. Gồm có/không ảnh, mô tả NULL, bảo toàn nội dung/ngày/ảnh, 11 trường hợp dữ liệu không hợp lệ, tin công khai khác, trạng thái đã đổi, vai trò/sở hữu/quản lý được gán/quyền chỉ đọc/CSRF, ẩn danh/GET/id không tồn tại, bốn request đồng thời, hai Nháp cạnh tranh, rollback audit và tương thích form sửa/menu.
- **124/124 ca hồi quy** tạo Nháp/trả phòng/tất toán/xác nhận liên quan PASS, trích từ `data/s4053-verified-results/verified.trx`. Lượt gộp này có 151 PASS/1 FAIL trên 152 vì một ca mới còn dựng giá 0 mà chưa bật tiêm dữ liệu trên DB tạm; sau sửa, cả 28 ca mới đã chạy lại PASS trong TRX cuối nêu trên. Không coi lượt gộp đầu là 152/152 PASS. Tổng các ca không trùng sau sửa: **186 PASS, 3 lỗi cũ / 189 (hai ca xuất hiện ở cả bộ hồi quy và tích hợp)**.
- Các lần chạy đầu phát hiện lỗi xử lý SQLite khi ghi audit thất bại đã được sửa; dữ liệu dựng test được sửa để khớp tên cột quyền và tiêm giá trị vi phạm CHECK chỉ trên DB tạm. Các sửa này không thay đổi kỳ vọng nghiệp vụ để bỏ qua lỗi.
- CSDL gốc giữ SHA-256 `F6CDE72D3CAEE154A71EC9E61352AD3ECE1EE0C4FBF912C34F67C9E337513B36`. Server dùng kiểm thử cổng 5253 đã dừng, bộ demo phần 2 không đổi. Script tạo demo phần 3 chạy thành công, log `data/s4053-demo-script.log`; bộ mới chưa hoàn tất trả phòng dành cho người dùng là `data/s4053-demo/68c2072d005e4f939f3ba9d188fd3230`.

Chưa chạy toàn bộ bộ test dự án, Safari, mạng 3G hoặc tải production; không kết luận dự án không còn lỗi hoặc mọi merge tương lai đều không xung đột. Cảnh báo ImageSharp/CS8601 có sẵn vẫn còn. Chưa commit/push phần 3.

Đã chạy Start-S4053Demo.ps1 thành công trên bộ demo mới riêng; trang đăng nhập http://localhost:5253/Account/Login trả HTTP 200. Web đang chạy cho người dùng, log data/s4053-web.log. Bộ dùng test trình duyệt đã dừng và không được dùng làm demo mới.

## Chạy lại kiểm thử ngày 15/10/2026

- Bộ gộp nghiệp vụ/hồi quy chạy lại trên DLL cuối: **152/152 PASS**, 0 bỏ qua, 3 phút 19 giây; gồm 28 ca đăng Nháp và 124 ca liên quan. TRX `data/s4053-retest-results/retest.trx`, log `data/s4053-retest.log`.
- Tích hợp chạy lại: **36 PASS, 3 FAIL / 39**, tên ba test lỗi không đổi so với lượt trước; `data/s4053-integration-retest-results/integration-retest.trx`. Tổng không trùng: **186 PASS, 3 lỗi cũ / 189**.
- Edge chạy lại trên dữ liệu mới riêng tại cổng 5254: PASS một lần bấm đăng trong **0,69 giây**, giao diện 360px, nội dung/giá, gửi lặp, luồng trả phòng và quyền khách. Log `data/s4053-browser-retest.log`, ảnh `data/s4053-retest-demo/6d1d865c2693469398d6fc0d62cd000a/browser-check-ca847bec/`.
- Không sửa mã nghiệp vụ trong lượt test lại. Server thử 5254 đã dừng; bộ demo người dùng `68c2072d005e4f939f3ba9d188fd3230` và hash CSDL gốc giữ nguyên. Chưa commit/push.

## Đã xử lý ba test giao diện cũ theo yêu cầu người dùng

Ngày 15/10/2026, người dùng yêu cầu xử lý ba lỗi còn lại. Nguyên nhân là các test còn tìm đường dẫn `Tao?phongId=...`, nhãn Đăng lại và tiêu đề cũ trong khi giao diện hiện dùng liên kết Sửa theo id, menu đổi trạng thái và tiêu đề Tin đăng cho thuê.

Đã sửa `OwnerListingManagementTests.cs`: kiểm tra đúng dòng tin, liên kết Sửa, phạm vi tòa, form đổi trạng thái với id/trạng thái trước đó/CSRF, khả năng đăng lại và nhãn hết hạn. Giữ các kiểm tra POST đăng/gỡ tin, trạng thái/người đăng trong DB, từ chối phòng/tin ngoài phạm vi và khách không xem được tin đã gỡ/hết hạn. Không thay đổi mã nghiệp vụ trong lần xử lý này.

- Chạy riêng ba ca: **3/3 PASS**, `data/s4053-ui-fix-results/ui-fix.trx`.
- Chạy lại bộ đăng Nháp và tích hợp quản lý tin/ảnh/kích hoạt hợp đồng trên mã test cuối: **65/65 PASS**, 0 bỏ qua, 2 phút 19 giây; `data/s4053-ui-fixed-final-results/ui-fixed-final.trx`, log `data/s4053-ui-fixed-final.log`. Ba lỗi cũ đã được giải quyết, không còn lỗi trong lượt này. Hai nhóm có ca trùng nhau nên dùng số ca thực tế của TRX.
- Lượt hồi quy 152/152 trước đó vẫn là bằng chứng riêng; không chạy lại toàn dự án sau sửa test. CSDL gốc giữ nguyên SHA-256 đã nêu. Chưa commit/push.
