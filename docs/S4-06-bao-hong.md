# S4-06 task 1 — Khách gửi và xem lại báo hỏng

Triển khai ngày 14/10/2026. Phạm vi: thông tin báo hỏng của AC1, danh sách/chi tiết AC2–AC3. Chưa triển khai ảnh, thay đổi trạng thái hoặc phản hồi/phân công của quản lý. Chưa đánh dấu hoàn thành toàn bộ story/DoD.

## Quyết định PO đã chốt trong phiên làm việc

- Hợp đồng `DANG_HIEU_LUC` và hôm nay Việt Nam nằm trong một kỳ thuê, bao gồm ngày đầu/cuối. Nếu đã ghi ngày trả phòng thì không được trước hôm nay.
- Người đứng tên và người ở ghép đang ở được gửi. Người ở ghép phải có ngày vào ≤ hôm nay và ngày ra NULL hoặc ≥ hôm nay; trong chính ngày chuyển đi vẫn được gửi.
- Khách có nhiều hợp đồng chọn phòng/hợp đồng từ danh sách. Server chỉ nhận id hợp đồng rồi suy ra phòng, người gửi và hồ sơ; không tin các id chủ thể/trạng thái/mã từ client.
- `BH-yyyyMM-xxxx`: bộ đếm chung toàn hệ thống theo tháng Việt Nam, bắt đầu 0001 mỗi tháng, tối đa 9999. Hết dải thì từ chối, không chuyển sang năm chữ số hoặc tái dùng mã. Thời điểm gửi UTC; hiển thị giờ Việt Nam.
- Loại thiết bị bắt buộc, tối đa 100 ký tự; mô tả bắt buộc, tối đa 5.000 ký tự; trim và chặn toàn khoảng trắng phía server. Mức `THUONG`/`GAP`; trạng thái mới `MOI`.

## Dữ liệu, quyền và transaction

v28 thêm `bao_hong` và `bao_hong_so_ma`; không sửa bước v27 hoặc các bảng cũ. Bảng báo lưu account người gửi, hồ sơ khách, phòng, hợp đồng, mã, loại thiết bị, mô tả, mức, trạng thái, UTC và khóa chống trùng. FK RESTRICT bảo vệ dữ liệu tham chiếu; mã và khóa chống trùng unique. Không thêm cột ảnh/chi phí/phản hồi của task khác. Mô hình 22 bảng chỉ là tham chiếu; phần mở rộng của task được ghi rõ tại đây.

`BaoHongService.GuiAsync` giữ SQLite IMMEDIATE trước khi kiểm tra lại account/quyền, hợp đồng và kỳ/ngày ở. Đọc đồng hồ một lần sau khi giữ khóa ghi để dùng cùng ngày cho hiệu lực và tháng mã. Cấp số, tạo báo hỏng và audit cùng transaction; lỗi rollback cả bộ đếm. Retry cùng khóa/người/nội dung trả báo cũ; khác nội dung bị từ chối. Không hứa mất kết nối sau commit sẽ đảo giao dịch; kiểm tra danh sách hoặc thử lại cùng biểu mẫu.

Account phải là KHACH_THUE hoạt động, đã xác nhận email, không bị xóa/buộc đổi mật khẩu. Quyền BAO_HONG đọc/ghi kiểm tra ở controller và service; quyền bị thu hồi bị chặn. POST có anti-forgery. Danh sách 20 dòng/trang chỉ người gửi; chi tiết người khác trả 403, id không tồn tại 404. Người đứng tên không tự có quyền xem báo người ở ghép gửi. Lịch sử của mình vẫn xem được sau khi hết hợp đồng/chuyển đi. Nhân viên không truy cập controller khách.

Nhật ký chỉ chứa mã/id phòng/hợp đồng/mức/trạng thái, không mô tả hay loại thiết bị tự do. `/Modules/BAO_HONG` chuyển khách sang danh sách thật; màn hình nhân viên hiện có giữ nguyên cho story sau. Dashboard khách chưa tích hợp số báo hỏng.

## Chạy demo đã chuẩn bị

```powershell
.\verification\Start-S406Demo.ps1
```

Bản chọn nằm trong `data/s406/latest.txt`. Cổng 5248, runtime và DB riêng trong data/s406; không dùng DB mặc định hoặc thay dữ liệu demo Sprint 2 gốc. Tài khoản/mật khẩu trong access.json ignored, giữ mật khẩu đã yêu cầu của demo Sprint 2. Khách 1 đứng tên hai hợp đồng C001/C002; khách 2 là người ở ghép C001 trong bản sao; khách 3 không có phòng.

Đăng nhập khách 1 → `/BaoHong/Create` → chọn phòng → nhập thiết bị/mô tả → Thường hoặc Gấp → Gửi. Chi tiết có mã và Mới; trở về `/BaoHong` rồi mở lại. Đăng nhập khách 2 thử gửi với vai trò người ở ghép; khách 3 không có phòng và không xem được báo người khác. Bộ smoke đã để sẵn 3 báo, thao tác trình duyệt thêm báo BH-202610-0004.

Kiểm tra HTTP chỉ trên bộ giả riêng (script có guard):

```powershell
$demoFolder = (Get-Content data/s406/latest.txt -Raw).Trim()
python verification/Verify-S406Http.py $demoFolder
```

Script có ghi dữ liệu, retry giữ cùng khóa và thêm báo mới ở mỗi lượt; không dùng trên dữ liệu thật. Python 3 thư viện chuẩn, không cài thư viện mới.

## Nâng cấp DB khác

Không tự nâng DB thật trong task này. Dừng app dùng DB đích, sao lưu DB/kho tệp, tạo bản sao mới rồi đặt DatabasePath của bản sao. Build, chạy `--update-database`, `--check-database`, kiểm tra integrity/FK và đối chiếu mọi nội dung bảng cũ. Chỉ nâng DB đang dùng khi được yêu cầu rõ; mã mới yêu cầu v28 nên DB v27 không startup được với bản build mới.

## Bằng chứng kiểm chứng

- Build C#/Razor đạt; cảnh báo license ImageSharp/CS8601 đã có trước.
- 54/54 test đạt: 18 ca báo hỏng và hồi quy ghi/hủy thu. Báo hỏng kiểm tra Thường/Gấp, hai phòng, trim/bắt buộc, phòng khác, hợp đồng hết hiệu lực giữa form/gửi, các trạng thái không hiệu lực, người ở ghép/ngày ra, xem chéo, đổi tháng UTC+7, biên9999, 8 request đồng thời, retry đồng thời, rollback audit/bộ đếm, account khóa/quyền thu hồi và nâng cấp/rerun. Không chạy toàn suite.
- HTTP app thật đạt gửi, chi tiết/danh sách, hai phòng, người ở ghép, required/CSRF, phòng không hợp lệ, retry không trùng, cross-tenant403, staff403, anonymous challenge, integrity/FK. Chi tiết no-store.
- Nâng v27→v28 trên bản sao backup giả giữ nguyên toàn bộ nội dung 40 bảng/241 bản ghi; rerun không thay đổi, integrity=ok/FK=0. Kết quả trong data/s406/upgrade-check-* ignored.
- Trình duyệt đã gửi thật báo Gấp, nhận BH-202610-0004/Mới. Biểu mẫu và chi tiết tại 360px không tràn ngang, documentWidth345≤viewport360; ảnh data/s406/demo-proof-360.png. Chưa nghiệm thu Safari/thiết bị cảm ứng thật hoặc review của thành viên khác.

DB mặc định và demo Sprint 2 gốc vẫn v27. Không commit DB, ảnh, mật khẩu, backup hoặc thư mục data/.
