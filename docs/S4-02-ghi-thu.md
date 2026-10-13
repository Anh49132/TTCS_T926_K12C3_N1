# S4-02 — Ghi một lần thu trên hóa đơn

Triển khai ngày 13/10/2026, phạm vi task 1 / AC1–AC3. **Chưa có hủy lần thu, lý do hủy, xem khoản đã hủy; chưa nối báo thanh toán S4-01.** Không đánh dấu toàn bộ S4-02 hoàn tất.

## Hành vi

- Chủ nhà có quyền ghi TAI_CHINH chỉ thu hóa đơn của tòa mình. Khách, quản lý và ADMIN không ghi thu. POST có anti-forgery; người xác nhận lấy từ phiên đăng nhập, kiểm tra lại tài khoản hoạt động phía service.
- Chỉ thu hóa đơn đã phát hành, còn nợ; số nguyên đồng >0, tối đa bằng dư nợ; ba hình thức TIEN_MAT, CHUYEN_KHOAN, KHAC. Ngày thu bắt buộc, trong [hôm nay VN −30 ngày, hôm nay VN]; ghi chú tùy chọn ≤255 ký tự.
- `/HoaDonDichVu/Details/{id}` có tóm tắt tổng/đã thu/còn phải trả/hạn, nhãn thanh toán, tiến độ, chi tiết và lịch sử theo ngày thu tăng dần (cùng ngày theo id). Modal theo ảnh, mặc định dư nợ/ngày hôm nay, lỗi cạnh từng ô, khóa người xác nhận.
- `POST /HoaDonDichVu/Collect`: input HoaDonId, SoTien (chuỗi chữ số không định dạng), NgayThu, HinhThuc, GhiChu, KhoaChongTrung (GUID N). Trả JSON summary/lịch sử; sai input 400, trái quyền 403, xung đột/lỗi ghi DB 409. `GET /HoaDonDichVu/CollectionDetails?id=…` lấy summary theo quyền chủ nhà.
- JavaScript cập nhật tóm tắt, nhãn, tiến độ, lịch sử ngay sau thành công; hết nợ ẩn nút. Tiền hiển thị dấu chấm nghìn; thời điểm UTC hiển thị giờ VN. “In / lưu PDF” dùng hộp thoại in của trình duyệt, không giả định có API tải PDF hóa đơn riêng.

## Dữ liệu và giao dịch

Giữ `thanh_toan` là nguồn duy nhất chứa số tiền/ngày/trạng thái. **Không thêm cột tổng đã thu hoặc trạng thái thanh toán vào hoa_don**, vì đó là dữ liệu suy ra; không sửa chứng từ đã khóa. Các truy vấn tài chính hiện có vẫn cộng thanh_toan DA_XAC_NHAN nên đọc được khoản mới ngay.

Bước v26 thêm `lan_thu_hoa_don`: id, thanh_toan_id unique FK RESTRICT, hinh_thuc, ghi_chu, nguoi_xac_nhan_id FK RESTRICT, ten_nguoi_xac_nhan snapshot, thoi_diem_ghi UTC, khoa_chong_trung unique. Đây là metadata một-một của lần thu, không phải sổ tiền thứ hai. Model thanh toán cũ giữ nguyên để tránh làm hỏng các truy vấn/fixture dùng schema tối thiểu. Khoản cũ không bị sửa; metadata thiếu hiển thị “Chưa có thông tin (dữ liệu cũ)”.

Service `GhiThuHoaDonService` bắt đầu SQLite IMMEDIATE trước khi đọc lại hóa đơn/dư nợ; hai người ghi khác khóa không thể cùng vượt nợ. Khóa chống trùng trả lần ghi cũ khi retry cùng actor/nội dung, từ chối nếu đổi nội dung. Thanh toán, metadata, audit allowlist và kiểm tra summary cùng transaction; bất kỳ lỗi nào rollback. Nhật ký chỉ ghi id hóa đơn, số tiền, ngày, hình thức và trạng thái; không ghi chú tự do, khóa chống trùng hay giấy tờ.

Nếu response bị mất sau khi server đã commit, không thể kết luận “chưa thu”. Modal giữ khóa/nội dung và cho thử lại cùng lần thu; không tạo khoản dở dang. Nếu người dùng tải lại trang, phải xem lịch sử trước khi tạo lần thu mới. Test cancellation/rollback không chứng minh mất mạng sau commit sẽ đảo giao dịch thành công.

Cọc là điểm nối S4-04: nếu có bảng giao_dich_coc tương thích, dư nợ trừ CAN_TRU đã xác nhận cùng hóa đơn nhưng không cộng vào tiền thu. DB hiện tại chưa có sổ cọc; chưa nghiệm thu tích hợp cấn cọc hoặc cập nhật toàn bộ màn hình tương lai của S4-04. Báo chờ không giảm nợ.

## Nâng cấp có kiểm soát

Không chạy updater lúc app còn dùng cùng file. Không chạy trên DB thật nếu chưa được yêu cầu. Bước v23–v25 giữ nguyên; v26 chỉ thêm bảng metadata khi module hóa đơn tồn tại. Nếu chưa cài module hóa đơn, initializer hóa đơn sau này tạo bảng này khi schema ≥26. Check kiểm tra cột, FK RESTRICT và các unique.

1. Dừng app đang dùng DB đích. Tạo bản sao mới (không ghi đè), gồm kho tệp liên quan nếu cần chạy đầy đủ app.
2. Đặt DatabasePath là đường dẫn tuyệt đối của **bản sao**.
3. Chạy `dotnet run --no-build --project QL_PhongTro -- --update-database`; updater tự tạo backup. Chạy lại và `--check-database`.
4. Kiểm tra integrity_check, foreign_key_check, số bản ghi và nội dung bảng cũ; không chỉ dựa vào version.
5. Chạy thử web với cùng DatabasePath. Chỉ nâng DB thật khi có yêu cầu rõ và backup.

Trong task này đã nâng bản sao `data/s402-upgrade-verified/copy.sqlite`, giữ nguyên digest/số hàng của **38 bảng, 3.922 bản ghi**; v26, chạy lại không đổi dữ liệu cũ, integrity=ok, FK=0. DB mặc định vẫn v25, không đổi dữ liệu/tài khoản/mật khẩu. Mã mới cần DB v26 để startup check đạt; vì vậy không khởi động trực tiếp với DB mặc định chưa nâng.

## Kiểm thử và demo

- Build Debug đạt; cảnh báo ImageSharp license và CS8601 AuthController đã có từ trước.
- 53/53 test tập trung và hồi quy đạt (`s402-final.trx` trong TestResults ignored): thu đủ/thiếu/hai lần, tiền 0/âm/lẻ/vượt1đ, ngày thiếu/tương lai/−31/−30, hình thức thiếu/sai, trạng thái nháp/hủy/đã trả, owner chéo, CSRF/khách/anonymous, hai request khác khóa/cùng khóa, lỗi metadata/audit/cancellation rollback, lịch sử/snapshot người, nâng cấp giữ khoản legacy và không cộng báo chờ; hồi quy hóa đơn khách/schema v23/hủy hóa đơn.
- HTTP app thật trên DB giả: `python verification/Verify-S402Http.py <thư-mục-demo-mới>` đạt thu 1 triệu +2 triệu trên tổng3 triệu, retry chỉ một bản ghi, lịch sử đầy đủ, nút ẩn khi trả đủ, file CSS/JS được phục vụ; owner khác/khách/quản lý/ADMIN đều403. Script để hóa đơn demo đã trả đủ; chỉ chạy trên bộ giả mới.
- Kiểm tra cú pháp JavaScript bằng Node VM và `git diff --check` đạt. Chưa chạy toàn bộ test suite.
- **Chưa nghiệm thu trực quan desktop/360px/Safari hoặc hành vi JavaScript qua trình duyệt**: công cụ không có browser khả dụng (iab báo unavailable). CSS có bố cục responsive, dialog cuộn trong viewport, bảng cuộn bên trong; HTTP/test không thay thế kiểm tra bằng mắt.

Tạo bộ demo mới, dữ liệu hoàn toàn giả và độc lập:

```powershell
# Dừng app của dự án trước build.
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore
python verification/New-S402Demo.py
# Lấy thư mục mới script in ra, đọc access.json để đăng nhập.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DatabasePath = 'D:\TTCS\TTCS_T926_K12C3_N1\data\s402-demo\<thư-mục-mới>\demo.sqlite'
dotnet run --no-build --no-launch-profile --project QL_PhongTro --urls http://localhost:5248
```

Demo có bốn vai trò + chủ nhà khác; một phòng A103, hóa đơn3 triệu ID1 chưa thu. Mật khẩu ngẫu nhiên chỉ trong access.json ignored, không có tài khoản thật/SMTP. Chạy từ đầu không ghi đè bộ cũ. Script dùng Python bcrypt đã có, không cài thư viện mới. Không commit data/, access.json, DB/backup/keys/logs.

Mở `/HoaDonDichVu/Details/1`, thu1 triệu bằng chuyển khoản → Trả một phần/còn2 triệu; thu tiếp2 triệu → Đã thanh toán/còn0/nút ẩn. Thử vượt dư nợ, ngày sai và đổi người xác nhận qua request phải bị chặn hoặc bị bỏ qua trường người do client gửi. Hủy lần thu trong ảnh chưa có trong task này.
