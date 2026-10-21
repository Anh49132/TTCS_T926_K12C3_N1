# S4-02 — Ghi một lần thu trên hóa đơn

Triển khai ngày 13/10/2026: task 1 / AC1–AC3 và task 2 / AC4 đã có trong mã, gồm ghi thu nhiều lần, hủy có lý do và giữ lịch sử. Chưa nối báo thanh toán S4-01 (story riêng). Chưa đánh dấu nghiệm thu đầy đủ DoD vì còn kiểm tra trực quan 360px/Safari, review/merge.

## Hành vi

- Chủ nhà có quyền ghi TAI_CHINH chỉ thu hóa đơn của tòa mình. Khách, quản lý và ADMIN không ghi thu. POST có anti-forgery; người xác nhận lấy từ phiên đăng nhập, kiểm tra lại tài khoản hoạt động phía service.
- Chỉ thu hóa đơn đã phát hành, còn nợ; số nguyên đồng >0, tối đa bằng dư nợ; ba hình thức TIEN_MAT, CHUYEN_KHOAN, KHAC. Ngày thu bắt buộc, trong [hôm nay VN −30 ngày, hôm nay VN]; ghi chú tùy chọn ≤255 ký tự.
- `/HoaDonDichVu/Details/{id}` có tóm tắt tổng/đã thu/còn phải trả/hạn, nhãn thanh toán, tiến độ, chi tiết và lịch sử theo ngày thu tăng dần (cùng ngày theo id). Modal theo ảnh, mặc định dư nợ/ngày hôm nay, lỗi cạnh từng ô, khóa người xác nhận.
- `POST /HoaDonDichVu/Collect`: input HoaDonId, SoTien (chuỗi chữ số không định dạng), NgayThu, HinhThuc, GhiChu, KhoaChongTrung (GUID N). Trả JSON summary/lịch sử; sai input 400, trái quyền 403, xung đột/lỗi ghi DB 409. `GET /HoaDonDichVu/CollectionDetails?id=…` lấy summary theo quyền chủ nhà.
- JavaScript cập nhật tóm tắt, nhãn, tiến độ, lịch sử ngay sau thành công; hết nợ ẩn nút. Tiền hiển thị dấu chấm nghìn; thời điểm UTC hiển thị giờ VN. “In / lưu PDF” dùng hộp thoại in của trình duyệt, không giả định có API tải PDF hóa đơn riêng.
- `POST /HoaDonDichVu/CancelCollection`: ThanhToanId, PhienBan=0 của lần thu đang hiệu lực, LyDo. Lý do trim, bắt buộc 5–500 ký tự. Sai lý do400; trái quyền403; đã hủy/phiên bản sai409. Người hủy lấy từ tài khoản đăng nhập, bỏ qua trường người do client gửi. Quyết định người dùng: **chỉ CHU_NHA có quyền ghi tài chính và sở hữu tòa hiện tại**, không cần là người đã ghi thu, không giới hạn tuổi lần thu; QUAN_LY/ADMIN không hủy.
- Hộp thoại hủy có số tiền/ngày/hình thức/người ghi, ô lý do, người hủy không sửa được, dự kiến số tiền sau hủy. Thành công cập nhật ngay summary và lịch sử; mở lại nút ghi thu nếu còn nợ. Dòng đã hủy gạch ngang số tiền, nhãn Đã hủy, lý do/người/thời điểm; không có khôi phục. Lỗi xác định giữ nguyên lý do và số liệu giao diện.

## Dữ liệu và giao dịch

Giữ `thanh_toan` là nguồn duy nhất chứa số tiền/ngày/trạng thái. **Không thêm cột tổng đã thu hoặc trạng thái thanh toán vào hoa_don**, vì đó là dữ liệu suy ra; không sửa chứng từ đã khóa. Các truy vấn tài chính hiện có vẫn cộng thanh_toan DA_XAC_NHAN nên đọc được khoản mới ngay.

Bước v26 thêm `lan_thu_hoa_don`: id, thanh_toan_id unique FK RESTRICT, hinh_thuc, ghi_chu, nguoi_xac_nhan_id FK RESTRICT, ten_nguoi_xac_nhan snapshot, thoi_diem_ghi UTC, khoa_chong_trung unique. Đây là metadata một-một của lần thu, không phải sổ tiền thứ hai. Model thanh toán cũ giữ nguyên để tránh làm hỏng các truy vấn/fixture dùng schema tối thiểu. Khoản cũ không bị sửa; metadata thiếu hiển thị “Chưa có thông tin (dữ liệu cũ)”.

Bước **v27** bổ sung `huy_lan_thu_hoa_don`: id, thanh_toan_id unique FK RESTRICT, ly_do_huy, nguoi_huy_id FK RESTRICT, ten_nguoi_huy snapshot, thoi_diem_huy UTC, phien_ban=1. Đây là sự kiện kết thúc của lần thu; tiền/ngày/người ghi gốc giữ nguyên, chỉ thanh_toan.trang_thai đổi DA_XAC_NHAN → DA_HUY. Tách sự kiện để hủy được cả khoản legacy thiếu metadata v26 mà không bịa người/hình thức thu ban đầu. Bản ghi đang hiệu lực có phiên bản0; chuyển duy nhất sang phiên bản1 khi hủy. Unique + cập nhật có điều kiện trạng thái trong IMMEDIATE transaction chặn hai request hủy, kể cả hai phiên đăng nhập. Trigger chặn sửa/xóa lịch sử hủy và chặn sửa/khôi phục lần thu đã hủy. Không sửa bước v26 hay đổi dữ liệu cũ khi nâng cấp.

Service `GhiThuHoaDonService` bắt đầu SQLite IMMEDIATE trước khi đọc lại hóa đơn/dư nợ; hai người ghi khác khóa không thể cùng vượt nợ. Khóa chống trùng trả lần ghi cũ khi retry cùng actor/nội dung, từ chối nếu đổi nội dung. Thanh toán, metadata, audit allowlist và kiểm tra summary cùng transaction; bất kỳ lỗi nào rollback. Nhật ký chỉ ghi id hóa đơn, số tiền, ngày, hình thức và trạng thái; không ghi chú tự do, khóa chống trùng hay giấy tờ.

Hủy cũng dùng cùng cơ chế serialize với ghi thu; đổi trạng thái, append sự kiện hủy, audit HUY, tính summary và commit cùng transaction. Không xóa thanh_toan/lan_thu_hoa_don. Audit trước/sau chỉ chứa trạng thái, phiên bản, id người hủy, thời điểm; lý do tự do nằm trong lịch sử riêng, không có trong audit. Tổng đã thu vẫn chỉ là SUM thanh_toan DA_XAC_NHAN. Summary trả cả DA_XAC_NHAN và DA_HUY (không cộng báo chờ), kèm DaHuy, LyDoHuy, NguoiHuy, ThoiDiemHuy, PhienBan, ChoPhepHuy. GET dùng một read snapshot để trạng thái và lịch sử không bị lệch khi request khác đang hủy. Các màn hình khách/quản lý hóa đơn/trang chủ vốn tính DA_XAC_NHAN tự loại khoản hủy.

Nếu response bị mất sau khi server đã commit, không thể kết luận “chưa thu”. Modal giữ khóa/nội dung và cho thử lại cùng lần thu; không tạo khoản dở dang. Nếu người dùng tải lại trang, phải xem lịch sử trước khi tạo lần thu mới. Test cancellation/rollback không chứng minh mất mạng sau commit sẽ đảo giao dịch thành công.

Với hủy: response mất thì giữ nguyên id/phiên bản/lý do, khóa sửa và cho “Thử lại và đối chiếu”. Server từ chối hủy lần đã hủy, không append lần nữa; client đọc lại summary để đối chiếu trạng thái nếu retry409 sau kết quả không rõ. Thông báo đối chiếu không suy đoán người đã hủy; lịch sử là nguồn xác nhận. Một lỗi validation/403/409 thông thường không tự thay số liệu giao diện. Không hứa mạng mất sau commit sẽ khôi phục tiền thu; chỉ bảo đảm toàn bộ giao dịch hoặc không thay đổi gì.

Cọc là điểm nối S4-04: nếu có bảng giao_dich_coc tương thích, dư nợ trừ CAN_TRU đã xác nhận cùng hóa đơn nhưng không cộng vào tiền thu. DB hiện tại chưa có sổ cọc; chưa nghiệm thu tích hợp cấn cọc hoặc cập nhật toàn bộ màn hình tương lai của S4-04. Báo chờ không giảm nợ.

## Nâng cấp có kiểm soát

Không chạy updater lúc app còn dùng cùng file. Không chạy trên DB thật nếu chưa được yêu cầu. Bước v23–v26 giữ nguyên; v27 chỉ thêm sự kiện/trigger hủy khi module thanh toán tồn tại. Nếu chưa cài module hóa đơn, initializer hóa đơn sau này tạo metadata v26 và sự kiện v27 theo phiên bản hiện tại. Check kiểm tra cột, FK RESTRICT, unique và trigger bảo vệ lịch sử.

1. Dừng app đang dùng DB đích. Tạo bản sao mới (không ghi đè), gồm kho tệp liên quan nếu cần chạy đầy đủ app.
2. Đặt DatabasePath là đường dẫn tuyệt đối của **bản sao**.
3. Chạy `dotnet run --no-build --project QL_PhongTro -- --update-database`; updater tự tạo backup. Chạy lại và `--check-database`.
4. Kiểm tra integrity_check, foreign_key_check, số bản ghi và nội dung bảng cũ; không chỉ dựa vào version.
5. Chạy thử web với cùng DatabasePath. Chỉ nâng DB thật khi có yêu cầu rõ và backup.

Task1 đã nâng bản sao lên v26. Task2 kiểm tra lại trên hai bản sao mới trong `data/s402-cancel-upgrade-20261013-115018-697874`: v25→v27 giữ nguyên digest của **38 bảng, 3.922 bản ghi**; v26→v27 giữ nguyên **39 bảng, 68 bản ghi**. Chạy lại/check đều đạt, integrity=ok, FK=0. DB mặc định vẫn v25; DB demo v26 cũ cũng không bị nâng/sửa. Mã mới cần **v27** để startup check đạt, không khởi động với DB mặc định chưa nâng.

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

Demo mới từ New-S402Demo.py có bốn vai trò + chủ nhà khác; một phòng A103, hóa đơn3 triệu ID1 chưa thu. Mật khẩu ngẫu nhiên chỉ trong access.json ignored, không có tài khoản thật/SMTP. Chạy từ đầu không ghi đè bộ cũ. Script dùng Python bcrypt đã có, không cài thư viện mới. Không commit data/, access.json, DB/backup/keys/logs.

Mở `/HoaDonDichVu/Details/1`, thu1 triệu bằng chuyển khoản → Trả một phần/còn2 triệu; thu tiếp2 triệu → Đã thanh toán/còn0/nút ẩn. Hủy dòng2 triệu, thử lý do trống rồi nhập “Nhập nhầm số tiền” → Trả một phần/còn2 triệu/nút ghi thu hiện lại; dòng hủy còn lý do/người/thời điểm sau tải lại. Ghi thu MỚI2 triệu → Đã thanh toán, dòng cũ vẫn hủy. Thử vượt dư nợ, ngày sai, hủy trùng và giả người thực hiện phải bị chặn hoặc bỏ qua trường người do client gửi.

## Kiểm chứng bổ sung task 2 / AC4

- Build đạt; **69/69** test ghi/hủy thu và hồi quy hóa đơn khách/schema/hủy hóa đơn đạt (`s402-cancel-regression.trx`). Trong đó 36 ca ghi/hủy thu trực tiếp; hủy lý do null/trống/whitespace/4/501 bị từ chối, 5/500 được nhận; last receipt về Chưa thanh toán; hủy một trong hai về Trả một phần; giữ lịch sử và ghi mới đến đủ; retry không khôi phục; legacy không metadata vẫn hủy; chuyển chủ nhà rồi chủ mới hủy được, chủ cũ bị chặn; không giới hạn tuổi lần thu; mất quyền tài chính bị chặn.
- Đồng thời: hai context/request chỉ một thành công, một sự kiện và một audit HUY. Schema hiện một chủ nhà/tòa, không thêm quan hệ đồng sở hữu; không giới hạn người ghi được kiểm tra bằng chuyển sở hữu cho chủ nhà khác. Rollback khi lỗi metadata/audit và khi cancellation token bị ngắt sau UPDATE trạng thái, trước lưu sự kiện. SQL trực tiếp sửa/xóa lịch sử/khôi phục khoản đã hủy bị trigger từ chối.
- HTTP thật trên DB giả v27: Verify-S402Http.py rồi Verify-S402CancelHttp.py đạt toàn luồng3 triệu, thu1+2, hủy2 triệu, tải lại lịch sử, hiện lại nút ghi thu, ghi mới2 triệu đến đủ; CSRF, owner khác/khách/quản lý/ADMIN, người hủy giả, lý do sai/hủy trùng được kiểm chứng.
- Cú pháp JS/ Python và diff kiểm tra; chưa chạy toàn suite. Công cụ browser inventory vẫn rỗng nên **chưa nghiệm thu JavaScript trong trình duyệt và bố cục trực quan desktop/360px/Safari**; HTTP/HTML và syntax không thay thế việc này.
- Demo sẵn hai lần thu: chạy Verify-S402Http.py trên bộ giả mới (nó ghi1+2 triệu, chưa hủy), rồi để người dùng thử hủy. Verify-S402CancelHttp.py dùng bộ đã trả đủ đó và sẽ để thêm một dòng hủy cùng một lần thu mới; không dùng trên DB thật.

## Cập nhật DB local sau lỗi run.bat (13/10/2026)

Đã sao lưu và nâng DB mặc định từ v25 lên v27 sau khi xử lý lỗi người dùng báo. Giữ nguyên digest của 38 bảng/3.922 bản ghi cũ, integrity=ok/FK=0; run.bat --check-only đạt. Backup: data/backups/local-v27-20261013-121438-038806/before.sqlite. Các ghi chú DB mặc định v25 ở phần kiểm thử trước là lịch sử. Không sửa run.bat để tự nâng DB, không chạy tác vụ thu/hủy thử hoặc thay tài khoản/mật khẩu trên DB thật.
