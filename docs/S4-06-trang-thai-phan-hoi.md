# S4-06 task 3 — Theo dõi trạng thái và phản hồi

Đã triển khai AC4 trên nền hai task gửi báo hỏng và đính kèm ảnh: quản lý mở yêu cầu/ảnh, đổi trạng thái, gửi phản hồi; khách xem trạng thái hiện tại và toàn bộ lịch sử trên **cùng trang chi tiết báo hỏng**. Phạm vi chức năng của S4-06 đã đủ AC1–AC4. Chưa nghiệm thu toàn bộ Definition of Done (review thành viên khác/Safari/thiết bị thật).

## PO chốt ngày 14/10/2026

- `MOI` (Mới) → `DANG_XU_LY` (Đang xử lý) hoặc `TU_CHOI` (Từ chối).
- `DANG_XU_LY` → `HOAN_THANH` (Hoàn thành). Hoàn thành/Từ chối là trạng thái cuối; không mở lại, không từ chối khi đã Đang xử lý.
- Mỗi lần chuyển trạng thái bắt buộc có ghi chú. Quản lý được gửi phản hồi ở mọi trạng thái, kể cả trạng thái cuối.
- Chỉ tài khoản `QUAN_LY` có quyền ghi module BAO_HONG và được gán ở `toa_nha.quan_ly_id` của phòng mới được xử lý. Quyền READ chỉ xem. Vai trò, tài khoản hoạt động và phân công hiện tại đều được kiểm tra lại ở backend; POST kiểm tra trong transaction.
- Khách gửi báo hỏng tiếp tục xem nội dung/ảnh/trạng thái/phản hồi sau khi hợp đồng hết hiệu lực, với tài khoản và quyền truy cập còn hợp lệ. Người đứng tên/ở ghép khác vẫn không xem được báo hỏng của người gửi khác, dù cùng phòng.

## Dữ liệu, quyền và giao dịch

- Updater thêm **v30**, không sửa v28/v29. Thêm `bao_hong.phien_ban` (mặc định 0) để chống cập nhật từ trang cũ; thêm bảng `bao_hong_trao_doi` lưu phản hồi/ghi chú chuyển trạng thái, người gửi, snapshot tên, thời điểm UTC, trạng thái trước/sau và khóa chống trùng.
- FK RESTRICT, unique khóa gửi, index báo hỏng/thời điểm/id. Trigger chặn sửa/xóa lịch sử; không suy đoán hoặc tạo lịch sử giả cho các báo hỏng cũ. Trạng thái và phản hồi được sắp xếp thời điểm tăng dần, ID làm thứ tự khi cùng thời điểm; hiển thị giờ Việt Nam.
- Đổi trạng thái/phiên bản, thêm lịch sử và ghi audit cùng transaction SQLite IMMEDIATE. Lỗi audit rollback toàn bộ. Gửi lại cùng khóa/nội dung không tạo bản ghi trùng; khóa đã dùng cho nội dung khác bị từ chối. Phản hồi đồng thời được nối thêm, không ghi đè nhau.
- Chỉ đổi trạng thái tăng phiên bản. Cập nhật bằng phiên bản cũ trả HTTP409 và yêu cầu tải lại trang. Điều kiện chuyển trạng thái được kiểm tra bằng trạng thái thật trong DB, không tin lựa chọn từ trình duyệt.
- Phản hồi và ghi chú trim, bắt buộc có ký tự, tối đa 5.000 ký tự. Razor mã hóa nội dung khi hiển thị. Audit chỉ lưu ID và mã trạng thái, không lưu ghi chú/nội dung phản hồi tự do.
- `/QuanLyBaoHong` là danh sách quản lý (20 dòng/trang); `/QuanLyBaoHong/Details/{id}` dùng chung view chi tiết với khách. `/Modules/BAO_HONG` của quản lý chuyển tới danh sách mới. Tenant giữ `/BaoHong/Details/{id}`.
- Ảnh quản lý đi qua `/QuanLyBaoHong/Image/{id}?order=1..3`; kiểm tra phân công và quyền module như trang chi tiết, no-store/nosniff. Ảnh khách giữ endpoint cũ. Trái phạm vi 403; không tồn tại 404; chưa đăng nhập chuyển Login. Cả hai POST có CSRF.
- Không triển khai thông báo tự động, khách trả lời phản hồi, chi phí/bên chịu phí/đưa vào hóa đơn, lọc/cảnh báo Gấp của S4-07. Giới hạn kho ảnh/mồ côi sau crash từ task2 vẫn áp dụng.

## Demo

Bản sao riêng nằm trong `data/s406/tracking-latest.txt`, chạy **http://localhost:5250**. DB local/Sprint2 và các demo task1/task2 giữ nguyên schema; chỉ bản sao được nâng lên v30.

| Vai trò | Tài khoản demo | Mật khẩu |
| --- | --- | --- |
| Quản lý tòa Alpha | manager.demo@demo.local | Dùng mật khẩu demo đã chốt trong access.json (ignored) |
| Khách gửi | tenant1.demo@demo.local | Như trên |

Không lưu mật khẩu/hash vào Git. Hai vai trò cần dùng hai phiên đăng nhập hoặc lần lượt đổi tài khoản để demo.

- Báo hỏng **BH-202610-0013**, ID13: có 3 ảnh, Đang xử lý, ghi chú tiếp nhận và phản hồi quản lý từ lượt test trình duyệt. Quản lý mở `/QuanLyBaoHong/Details/13`; khách mở `/BaoHong/Details/13`.
- ID22: Hoàn thành, có 6 sự kiện/ phản hồi từ lượt HTTP, gồm phản hồi sau khi hợp đồng kết thúc.
- Trong **bản sao demo**, hợp đồng1 đã chuyển DA_KET_THUC để kiểm tra giữ quyền xem. Khách vẫn chọn hợp đồng2 khi muốn tạo yêu cầu mới; quản lý vẫn xử lý báo hỏng lịch sử của phòng trong phạm vi.

```powershell
$demoFolder = (Get-Content data/s406/tracking-latest.txt -Raw).Trim()
.\verification\Start-S406Demo.ps1 -Directory $demoFolder
```

Runtime demo trỏ tới `data/s406-tracking/build/QL_PhongTro.dll`. Dừng đúng server demo trước khi build vào thư mục đó; không cập nhật DB đang chạy. Máy khác: tạo bản sao DB v29 và thư mục `<DB>.repair-images`, đặt DatabasePath của bản sao, chạy updater/check trước khi chạy web. Updater tạo backup, không tự ghi đè DB dùng thật.

## Kết quả kiểm chứng

- Build C#/Razor đạt. Chỉ còn cảnh báo license ImageSharp và CS8601 hiện hữu; không chạy build Release.
- **76/76 test liên quan đạt**: 40 test báo hỏng/ảnh/theo dõi và 36 hồi quy thu/hủy thu. Ca mới kiểm tra chuyển hợp lệ/không hợp lệ, ghi chú/phản hồi trắng, nhiều phản hồi ở trạng thái cuối, quyền READ/ngoài phạm vi/tài khoản khóa/phân công đổi, snapshot tên, retry, audit không lộ nội dung, trigger bất biến, cập nhật đồng thời/stale, rollback và nâng cấp v29→v30. Không chạy lại toàn suite; các lỗi hóa đơn ngoài phạm vi đã ghi ở tài liệu task2.
- HTTP thật đạt danh sách/route module quản lý, xem ảnh, 403 ngoài phạm vi/khác vai trò, CSRF400, phản hồi trắng, retry, stale409, cập nhật và phản hồi theo thứ tự, mã hóa nội dung, khách reload và xem ảnh/lịch sử khi hợp đồng hết hiệu lực, integrity/FK.
- Nâng v29→v30 trên bản sao giữ toàn bộ dữ liệu **43 bảng/280 dòng** cũ (bảng báo hỏng chỉ thêm phiên bản0), giữ tệp ảnh. integrity=ok, FK=0, rerun không đổi dữ liệu và lịch sử mới trống. Sau xác minh mới thêm một tài khoản quản lý giả ngoài phạm vi cho test; không đổi tài khoản cũ.
- Trình duyệt: quản lý đổi ID13 sang Đang xử lý và gửi phản hồi; khách đăng nhập rồi reload thấy kết quả trên cùng chi tiết, 3 ảnh tải thành công. Cả hai giao diện ở 360px có documentWidth345≤viewport360. **Chỉ kiểm tra DOM, không chụp màn hình**. Chưa xác minh trực quan, Safari hoặc thiết bị cảm ứng thật.
- Báo cáo nằm trong thư mục demo ignored: `upgrade-verification.json`, `tracking-http-verification.json`. Không commit DB/tệp demo/mật khẩu/backup.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~BaoHongTests|FullyQualifiedName~.Collection'
# Có ghi dữ liệu/chuyển hợp đồng1 kết thúc: chỉ dùng bản sao demo giả mới, được gán manager ID6.
# Thêm manager.other.demo@demo.local vào bản sao để kiểm tra ngoài phạm vi (cùng mật khẩu demo).
python verification/Verify-S406TrackingHttp.py $demoFolder
```

Script có guard tên `data/s406/demo-tracking-*` và URL localhost; không chạy trên dữ liệu thật. Dữ liệu demo hiện tại đã chạy test nên tạo bản sao mới từ demo task2 khi cần chạy lại toàn kịch bản.
