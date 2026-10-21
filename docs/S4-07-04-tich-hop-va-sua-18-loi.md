# S4-07:04 — Tích hợp dev và xử lý 18 lỗi baseline

Ngày15/10/2026. Theo yêu cầu người dùng, toàn bộ phần hòa giải ban đầu ở worktree thử đã được chuyển về **feature/S4-07/04-repair-request-filters**. Parent feature43f8998, dev lấy tại edef5ea. Chỉ nhánh S4-07:04 được phép đẩy; không đẩy dev, nhánh thử hoặc tag.

## Tích hợp giữ chức năng hai nhánh

Giữ S4-07:01–04 (trạng thái/phản hồi, chi phí, cảnh báoGấp>24giờ, bộ lọc) và S4-04 của dev (trả phòng, chấm dứt, tất toán, xác nhận/cấn cọc/hoàn, PDF). Chi tiết hòa giải v31→v34 tại [S4-07-dev-schema-v34.md](S4-07-dev-schema-v34.md). Các module có đủ mapping, guard hóa đơn và fixture; không chọn bỏ toàn bộ một phía khi conflict.

## 18 lỗi baseline và cách xử lý

| Nhóm | Số ca | Nguyên nhân và thay đổi |
|---|---:|---|
| Nâng schema lịch hẹn, quyềnv13, chỉ sốv17 và hợp nhấtUI/Dev |6|Các test mong phiên bản23/25 dù updater hiện là34; hai test giảm marker nhưng giữ extension mới khiến validator từ chối. Dùng helper chỉ tháo extension rỗng trong CSDL giả trước khi hạ phiên bản; cập nhật kỳ vọng34. Giữ kiểm tra bảo toàn dữ liệu, quyền bị thu hồi, integrity/FK và rerun. Không nới validator production hoặc giả mạo phiên bản DB thật.|
| Lưu lại hợp đồng nháp |1|Kho tài liệu từ chối mọi thư mục đã có, kể cả bản nháp. Controller xác định hợp đồng nháp thuộc quyền trước khi cho phép thay manifest. Ghi thư mục staging, giữ tệp cũ và khóa tệp, đổi thư mục có backup; chưa hoàn tất giao dịch thì Dispose khôi phục bản cũ, hoàn tất SQL mới dọn backup. Hợp đồng hiệu lực vẫn không được lưu lại, tài liệu có sẵn của ID mới vẫn không ghi đè. Thêm2test bảo toàn tệp/điều khoản và rollback.|
| Tạo/sửa/xóa tòa và phòng |2|Form test thiếu phường/xã, quận/huyện, tỉnh/thành phố bắt buộc. Bổ sung dữ liệu hợp lệ cho cả tạo/sửa; cập nhật nhãn Tất cả cơ sở/Chỉnh sửa phòng. Giữ kiểm tra POST/redirect/CSRF, CRUD thật, thông báo, dữ liệu sai và phòng có tham chiếu.|
| Menu theo ma trận quyền |1|Sidebar thiếu lối vào module tài khoản cho vai trò có quyền. Bổ sung liên kết qua Modules/TAI_KHOAN, vẫn lọc bằng PermissionService.MenuAsync. Giữ toàn bộ test API/MVC/menu bốn vai trò.|
| Yêu cầu thuê chưa xử lý/quá24giờ |1|Hàm đếm và cờ quá hạn có sẵn nhưng giao diện mới không còn badge menu/nhãn từng dòng. Nối lại các phần này; selector test lấy đúng từng data-request-id, kiểm tra cảMOI/DA_HEN_LICH và loại trừDA_DUYET. Cập nhật tên phần chi tiết theo view hiện hành, vẫn kiểm tra form hẹn/từ chối/duyệt đúng loại.|
| Trang chủ công khai |1|Controller vẫn truy vấn tin công khai hợp lệ nhưng view chỉ còn hero/tìm kiếm. Hiển thị lại tin mới nhất từ Model.TinMoiNhat và phần giới thiệu ba giá trị; tái dùng ảnh phòng/placeholder hiện có, không tạo ảnh. Bố cục Bootstrap một cột trên màn nhỏ. Không đổi điều kiện công khai/phân quyền.|
| Dashboard chủ nhà |1|Test vẫn tìm widget owner-buildings đã thay bằng bộ chọn cơ sở và card tổng quan. Selector mới kiểm tra cả hai cơ sở thuộc quyền, loại cơ sở ngoài quyền/ngừng hoạt động, tổng1/2phòng thuê và50%lấp đầy.|
| Đăng/gỡ/đăng lại/tin hết hạn |3|View quản lý tin đã dùng liên kết sửa theoID và menu trạng thái; test còn đợi linkphongId/nhãn cũ. Cập nhật selector/nhãn tương ứng, giữ các POST và assertion dữ liệu/trái quyền/phạm vi/tự tạm ẩn.|
| Hiện mật khẩu đăng ký |1|Container có cảlogin-field vàpassword-field, test so nguyên thuộc tính class. Dùng regex nhận tokenpassword-field trong danh sách class; vẫn kiểm tra nút gắnMatKhau và email xác nhận có thể sửa/gửi lại.|
| Chỉ số điện nước |1|Tiêu đề đổi thànhChỉ số điện nước. Test kiểm tra đúngh1meter-title và giữ mọi assertion dữ liệu, quyền/không thay DB/CSRF/readonly.|

## Kiểm tra

- Lượtold-fixes-first:14/16PASS,2FAIL; sau đó tiếp tục sửa, không bỏ test.
- Lượtold-fixes-second:147/149PASS; ca lưu lại hợp đồng nháp đạt và các hồi quy hợp đồng đạt. Hai lỗi tiếp theo ở assertion/chuẩn bị form đã được xử lý.
- Lượtold-fixes-third:3/5PASS; ba ca tài liệu/rollback đạt. Hai ca còn lại được cập nhật dữ liệu địa chỉ và nhãn chi tiết theo view hiện tại.
- Lượtold-fixes-final:2/2PASS.
- Full suite trên chính nhánh S4-07:04: **993/993PASS,0FAIL/0SKIP**,18phút11giây; TRX xác nhận cả18ca lỗi baseline đềuPASS. Binary khớp nguồn, không sửa mã khi chạy. Bao gồm2test tài liệu mới và6test hòa giải schema. Bằng chứng full-final.trx/full-final.log/final-comparison.json. Kết quả áp dụng tích hợp devedef5ea, chưa bao gồm4commit S4-05 mới trên deva934630.

Bằng chứng ignored: `data/s407-final-fixes/*.trx`, `*.log`. Không sửa project test, credential, dữ liệu local/demo gốc. Không chụp/tạo thêm ảnh. Safari vẫn chưa kiểm chứng; testHTTP không thay cho nghiệm thuSafari.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --logger 'console;verbosity=normal' --logger 'trx;LogFileName=full-final.trx' --results-directory data/s407-final-fixes
```

## Đồng bộ dev theo yêu cầu tiếp theo

Người dùng yêu cầu chuyển D:/DU_AN_KHO về dev mới nhất. Phần sửa/hòa giải trên S4-07:04 được lưu commit cục bộ trước khi checkout để không mất công việc; không đưa các thay đổi này lên dev. Dev mới nhất a934630 có thêm S4-05; kết quả993/993 ở trên không phải kết quả test dev mới.
