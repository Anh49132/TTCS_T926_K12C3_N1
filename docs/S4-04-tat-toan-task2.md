# S4-04 task 2 — Bảng tất toán tiền cọc và khấu trừ

Ngày thực hiện: 14/10/2026.

## Phạm vi và quyết định đã chốt

Người dùng xác nhận:

- Cọc là số thực thu còn giữ, không lấy tiền cọc thỏa thuận trên hợp đồng thay cho tiền đã nhận.
- Cọc còn giữ = tổng THU_COC − HOAN_COC − CAN_TRU, chỉ tính DA_XAC_NHAN.
- Dư nợ hóa đơn = tổng hóa đơn − thanh toán đã xác nhận − cấn cọc đã xác nhận. Khoản chờ xác nhận, bị từ chối hoặc đã hủy không giảm nợ.
- Kết quả = cọc còn giữ − dư nợ kỳ cuối − công nợ kỳ trước − hư hỏng. Dương hoàn khách, âm khách trả thêm, bằng 0 không thu/hoàn thêm.
- Làm tròn từng khoản hư hỏng đến đồng bằng decimal/AwayFromZero trước khi cộng; cho phép bằng 0, từ chối số âm. Tên bắt buộc, tối đa 200 ký tự; ghi chú dòng tối đa 500 ký tự; tối đa 100 dòng.
- Khôi phục task 1 trong checkout này rồi nối sang task 2. Khi bắt đầu, HEAD 29d4f02 không có mã trả phòng của lượt trước.

Task 2 chỉ lưu NHAP. Chưa phạt chấm dứt trước hạn, phát hành kỳ cuối, xác nhận/chốt tất toán, hoàn/cấn cọc thực tế, kết thúc hợp đồng, đổi trạng thái phòng hoặc PDF.

## Luồng sử dụng

1. Chủ nhà vào Hợp đồng → Ghi nhận trả phòng → nhập ngày trả và chỉ số cuối → kiểm tra và lưu hóa đơn kỳ cuối nháp.
2. Trên hóa đơn đã lưu, chọn **Tiếp tục lập bảng tất toán**. Cũng có thể vào `/TatToan` hoặc nút **Bảng tất toán** ở danh sách/chi tiết hợp đồng.
3. Hệ thống lấy cọc thực giữ, dư nợ kỳ cuối và các hóa đơn kỳ trước DA_PHAT_HANH. Hóa đơn nháp kỳ trước/đã hủy không cộng nợ. Có thể mở đối chiếu từng hóa đơn: tổng, đã trả, đã cấn và còn nợ.
4. Thêm, sửa, xóa các dòng hư hỏng. Dự tính tự cập nhật từ máy chủ; có nút **Xem dự tính** để kiểm tra lại. Không hư hỏng thì để danh sách trống.
5. **Lưu bản nháp** lưu cả bảng, các khoản khấu trừ và snapshot chi tiết công nợ. **Sửa bảng nháp** tải số liệu tài chính hiện tại để đối chiếu và lưu lại.
6. Khách thuê vào Hợp đồng của tôi → Chi tiết → **Xem bảng tất toán của tôi**, hoặc `/TatToan`, để xem bảng đã lưu. Không có nút sửa hoặc xác nhận.

Trang xem đọc snapshot đã lưu, không tự đổi khi khách trả thêm hoặc sổ cọc thay đổi. Chủ nhà và khách thuê luôn xem cùng bản. Trang sửa tính lại từ nguồn; nếu số liệu thay đổi giữa lúc mở form và lưu thì từ chối và yêu cầu tải lại.

## Phân quyền

| Vai trò | Xem | Lập/sửa nháp |
| --- | --- | --- |
| Chủ nhà | Chỉ hợp đồng ở tòa sở hữu | Chỉ hợp đồng đang hiệu lực, có quyền ghi HOP_DONG và TAI_CHINH |
| Khách thuê | Hợp đồng gắn hồ sơ tài khoản: người đứng tên hoặc đã được ghi ở ghép; không NHAP/DA_HUY | Không |
| Quản lý | Không truy cập tài chính | Không |
| ADMIN | Chỉ xem theo ma trận hệ thống | Không |

Backend kiểm tra tài khoản đang hoạt động, quyền module và sở hữu/tham gia hợp đồng. Tất cả POST có CSRF, phản hồi private no-store. Truy cập dữ liệu người khác trả 403.

## Dữ liệu và schema v31

Đã kiểm tra DB local bằng kết nối chỉ đọc: tối đa v5, chưa có hợp đồng, hóa đơn, sổ cọc hoặc tất toán. Không chạy updater/initializer trên DB này. Schema thực tế khác ghi chú cũ trong tài liệu tiến độ; không coi DBML là schema đã cài.

v31 thêm ba bảng, giữ nguyên các bước cập nhật cũ:

- `giao_dich_coc`: nguồn giao dịch cọc thực thu/hoàn/cấn, phân biệt số thỏa thuận. Task này chỉ đọc, chưa thêm luồng thu/hoàn/cấn cọc trên giao diện. Không có giao dịch thì số dư bằng 0 và có giải thích trên form.
- `thanh_ly_hop_dong`: một bảng/hợp đồng, liên kết kỳ cuối, snapshot cọc/nợ/hư hỏng/kết quả, thời điểm UTC, ghi chú, trạng thái và `phien_ban`.
- `chi_tiet_khau_tru`: các dòng tên/số tiền/ghi chú, thứ tự unique trong bảng.

Tất cả FK dùng RESTRICT. Bảng tất toán không xóa; bảng đã chốt và các dòng của nó được bảo vệ bằng trigger. Bước v31 từ chối schema không đầy đủ/không tương thích; schema tương thích đã có từ nhánh khác được kiểm tra rồi ghi phiên bản, không tạo lại bảng. Có kiểm tra cột, FK, unique và trigger. Guard cấn trừ cùng hợp đồng chỉ cài khi module hóa đơn tồn tại; initializer hóa đơn gọi guard sau khi tạo module. Như vậy schema lõi chưa có hóa đơn vẫn nâng được và không làm lỗi ALTER của module khác.

Save dùng SQLite BEGIN IMMEDIATE, tính lại nguồn, so dấu vân tay và phiên bản. Lưu thay đổi và audit trong cùng transaction; lỗi audit rollback cả bảng và dòng. Không sửa/xóa hóa đơn, khoản thu, giao dịch cọc hoặc lịch sử tài chính khi sửa bảng nháp. Audit allowlist không ghi tên hư hỏng hay ghi chú tự do.

Các dòng hư hỏng ở task 2 là đề xuất trong bảng NHAP, chưa ghi vào sổ cọc hoặc hóa đơn. Theo quy tắc 7.6, khi triển khai bước **chốt** sau này phải đưa chúng vào dòng PHAT_SINH của hóa đơn cuối, rồi cấn vào hóa đơn. Khi đó không được trừ phần hư hỏng thêm lần nữa trong tổng nợ. Task 2 chưa thực hiện bước ghi nhận nghĩa vụ đó nên không làm thay tổng hóa đơn kỳ cuối và không khấu trừ cọc hai lần.

## Nâng cấp và phục hồi

Chỉ chạy khi đã dừng ứng dụng dùng DB tương ứng. Thử trên bản sao mới trước:

```powershell
$env:DatabasePath = 'D:\duong-dan\ban-sao-moi.sqlite'
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

Updater tự tạo backup `*.before-update-<id>.bak`, kiểm tra integrity/FK trước khi sửa. Không nâng DB live trong lượt này. Nếu nâng lỗi, dừng ứng dụng và giữ cả DB lỗi lẫn backup để đối chiếu; phục hồi backup sang **đường dẫn mới**, trỏ DatabasePath vào bản phục hồi, không chép đè DB có dữ liệu mới. Kiểm tra `PRAGMA integrity_check`, `PRAGMA foreign_key_check` và đúng phiên bản code trước khi chạy lại.

Kiểm thử nâng cấp tạo DB giả v30 có dữ liệu rồi sao sang đường dẫn mới; so hash toàn bộ dòng/cột cũ trước/sau, kiểm tra backup, integrity/FK và chạy lặp. Nguồn và bản sao đều giữ toàn bộ dữ liệu cũ. Tạo DB rỗng qua LocalDatabaseInitializer cũng đã chạy được đến v31.

## Demo độc lập

```powershell
.\verification\New-S404Demo.ps1
.\verification\Start-S404Demo.ps1             # mặc định http://localhost:5252
```

Script từ chối ghi đè thư mục đã có, tạo DB giả riêng trong `data/s404-task2-demo/`, không dùng DB local. Tài khoản và mật khẩu ngẫu nhiên nằm trong `access.json` của demo, không commit. `latest.txt` chỉ đến bộ mới nhất.

Bộ đã tạo và kiểm tra: `data/s404-task2-demo/20261014-task2-verified`. Hợp đồng HD-2026-0024, A103: cọc thực nhận 7.000.000 đ, kỳ cuối 1.863.742 đ (tiền phòng/điện/nước), nợ cũ còn 1.200.000 đ sau khi trả 500.000 đ, hư hỏng 450.000 đ; hoàn dự tính **3.486.258 đ**. Không thêm phí cố định hoặc phạt ngoài phạm vi.

Các trang: `/TraPhong/ChiTiet/2`, `/TatToan/Nhap/1`, `/TatToan/ChiTiet/1`. Chủ nhà khác và khách không thuộc hợp đồng không xem được bảng.

## Xác minh

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s404-task2-tests -p:UseSharedCompilation=false --filter 'FullyQualifiedName~TatToan|FullyQualifiedName~Schema|FullyQualifiedName~Collection|FullyQualifiedName~BaoHong'
```

Đã có 10 ví dụ tất toán tính tay; các ca nhiều/không hư hỏng, cọc đủ/thiếu/vừa đủ, số âm/tên rỗng, làm tròn từng dòng, không dùng cọc thỏa thuận, loại khoản chờ/hủy, cấn cọc, stale/version, ghi đồng thời, rollback khi audit lỗi, snapshot chủ nhà/khách và kiểm tra 403/CSRF. Bổ sung 10 ví dụ tiền phòng để bảo vệ task 1 khôi phục, gồm trường hợp 500.025 × 1 / 30 → 16.668 đ.

Kết quả cuối: build không incremental thành công; **121/121 test liên quan đạt**, trong đó 38 test S4-04/task 1 khôi phục và 83 ca hồi quy schema, thu/hủy thu, báo hỏng và quyền. Output cuối `data/s404-task2-final`. Fixture giả lập phiên bản cũ đã được điều chỉnh để loại phần mở rộng rỗng v31 trước khi hạ mốc phiên bản; không sửa bước migration cũ. HTTP danh sách chủ nhà và khách đều 200, chỉ hiện hợp đồng thuộc phạm vi.

Edge headless qua công cụ không cần thư viện ngoài `verification/S404Browser`: đã kiểm tra thêm/sửa/xóa dòng và dự toán máy chủ, lưu, khách xem cùng số tiền, khách sửa 403 và khách lạ xem 403. Đã xem ảnh desktop; màn hình chủ nhà và khách 360px đều có document width = viewport width = 360, không tràn ngang. Lỗi phần tài khoản đầu trang trên mobile đã được sửa chỉ trong phạm vi các trang trả phòng/tất toán.

Ảnh/report lượt cuối trên bản build cuối đã lưu ở `data/s404-task2-demo/20261014-task2-verified/browser-check-c27c22b6/`. DB demo integrity_check=ok, foreign_key_check=0; DB local vẫn v5. Chưa nghiệm thu Safari hoặc thiết bị cảm ứng thật; chưa có review/hợp nhất nhánh theo DoD. Cảnh báo ImageSharp license/CS8601 có sẵn vẫn còn.
