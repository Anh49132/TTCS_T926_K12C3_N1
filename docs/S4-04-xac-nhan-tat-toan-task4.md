# S4-04 task 4 — Xác nhận tất toán và kết thúc hợp đồng

Ngày thực hiện: 15/10/2026. Nhánh `feature/S4-04/04-settlement-confirmation`; khôi phục phụ thuộc task 1–3 từ commit `3f3c477` vào worktree. Không commit/push hoặc cập nhật DB dùng thật.

## Quy tắc PO đã chốt

- Chỉ Chủ nhà sở hữu hợp đồng, tài khoản hoạt động và có quyền ghi cả HOP_DONG/TAI_CHINH được xác nhận.
- Xác nhận là **chốt nghĩa vụ**, không tự coi tiền đã thu hoặc hoàn. Số tiền cần thu thêm/hoàn cọc được giữ trên biên bản cuối cùng; nghiệp vụ thu/hoàn thực tế ghi riêng sau đó.
- Khi chốt: chuyển hư hỏng/phạt thành PHAT_SINH trong hóa đơn cuối, phát hành, cấn số cọc còn giữ vào nợ bằng CAN_TRU đúng một lần. Phân bổ nợ cũ trước, rồi hóa đơn cuối theo thứ tự kỳ và ID.
- Chốt bảng, ghi người/thời điểm/số tiền, kết thúc hợp đồng, trả phòng về TRONG, ghi lịch sử phòng và tạo tin NHAP **cùng một transaction**. Tin chưa được đăng công khai.
- Không có xuất PDF trong task 4.

## Điều kiện chốt

Hợp đồng đang hiệu lực, phòng đang thuê và không có hợp đồng khác cùng hiệu lực trên phòng; bảng nháp đã lưu, đủ thông tin trả phòng/chấm dứt và khấu trừ; đúng một hóa đơn cuối NHAP, chi tiết/tổng tiền khớp. Không còn hóa đơn nháp khác hoặc báo thanh toán CHO_XAC_NHAN cần xử lý. Chủ nhà phải xác nhận đã kiểm tra và đã đối chiếu số liệu.

Nguồn tài chính hiện tại phải khớp bản đã lưu: cọc thực giữ, dư nợ kỳ cuối/kỳ trước, chi tiết thu và cấn. Điều khoản kỳ cuối/cọc thỏa thuận/lý do/phạt phải còn hợp lệ. Nếu nguồn đổi, phải sửa/lưu lại bảng rồi kiểm tra lại; không tự thay số tiền khi bấm xác nhận.

Fingerprint bước kiểm tra bao gồm nguồn, phiên bản bảng, chi tiết hóa đơn/khấu trừ, phòng và chỉ số nguồn. Kiểm tra lại bên trong SQLite BEGIN IMMEDIATE; phiên bản/fingerprint cũ hoặc gửi đồng thời không ghi thêm biên bản/giao dịch. Lỗi ở bất kỳ bước nào, kể cả audit, rollback toàn bộ.

## Tiền và chứng từ

Trước chốt, task 2–3 hiển thị hư hỏng/phạt dự tính riêng. Khi chốt, các khoản dương trở thành dòng PHAT_SINH của hóa đơn cuối. Các khoản hư hỏng 0 đ vẫn được giữ trên bảng đối chiếu nhưng không tạo dòng phát sinh 0 đ.

Sau chốt, `no_hoa_don_cuoi` và snapshot chi tiết công nợ đã bao gồm các khoản này. `tien_hu_hong`/`tien_phat` chỉ còn là phân tích, giao diện ghi rõ đã gồm trong hóa đơn cuối; không trừ lần hai. Kết quả trước/sau chốt giữ nguyên.

CAN_TRU không ghi vào thanh_toan và không được tính là tiền mặt đã thu. Chức năng ghi thu hiện có tiếp tục hoạt động với hóa đơn còn nợ của hợp đồng đã kết thúc. Danh sách hóa đơn/chi tiết khách và trạng thái quá hạn tính cả cấn cọc để không thu thừa hoặc báo nợ sai.

Kết quả cuối hiển thị riêng công nợ hiện tại và tình trạng hoàn cọc. Chủ nhà ghi nhận **đã hoàn thực tế** bằng số nguyên đồng dương, hình thức tiền mặt/chuyển khoản và checkbox xác nhận. Có thể hoàn từng phần; không vượt số còn phải hoàn hoặc cọc đang giữ. Khóa chống trùng lưu trong giao_dich_coc; thử lại cùng dữ liệu không tạo lần hoàn thứ hai. Biên bản chốt không sửa khi ghi thu/hoàn sau đó.

## Schema thực tế và nâng cấp

- Nối bước v33, giữ nguyên v31/v32. Thêm `xac_nhan_tat_toan`: unique hợp đồng và biên bản; người/tên xác nhận, UTC, số tiền tất toán có dấu, tổng cấn cọc. FK RESTRICT, kiểm tra đúng biên bản/hợp đồng/chủ nhà, trigger bất biến.
- Thêm `lich_su_trang_thai_phong`: khoảng `[từ, đến)`, unique mốc/phòng và một khoảng hiện tại. Chỉ đóng khoảng hiện tại; không xóa hoặc viết lại lịch sử. Không suy đoán/backfill các khoảng lịch sử chưa từng được ghi.
- Trigger khóa INSERT hóa đơn sau DA_KET_THUC và chuyển nháp sang phát hành; khóa hủy hóa đơn của tất toán đã chốt. Service cũng kiểm tra trước khi tạo/phát hành/thay thế. Biên bản, khấu trừ, chấm dứt và hóa đơn đã phát hành có bảo vệ bất biến.
- Guard hóa đơn cài khi có module hóa đơn; initializer module cài guard nếu nền đã ở v33. Không chạy initializer từ trang web.
- Updater có backup, transaction, integrity/FK, kiểm tra schema và chạy lại an toàn. Test bản sao v32 có bảng/dòng task 2–3 đã lưu so sánh toàn bộ dữ liệu cũ trước/sau; bản gốc không đổi.

**Lệch đặc tả cần giữ:** bảng `chi_so_dien_nuoc` thực tế có CHECK kỳ trọn tháng (`den_ngay = date(tu_ngay,'+1 month','-1 day')`). Vì vậy task 4 không tạo một kỳ giả trọn tháng hoặc sửa CHECK để lưu ngày trả giữa kỳ. Chỉ số cuối nằm trong snapshot bất biến của hóa đơn cuối; kiểm tra lại với chỉ số nguồn và khóa nguồn khi phát hành. Có test luồng task 1→4 trả giữa tháng trên schema này. Một lần chốt demo phát hiện ràng buộc này đã rollback toàn bộ trước khi sửa mã.

DB local và demo task 2–3 không được nâng/chỉnh sửa. Chỉ tạo demo task 4 mới và thử trên fixture/bản sao. Thông tin đăng nhập và các DB/backup/artifact đều ở `data/` ignored, không đưa vào Git.

## Giao diện và luồng

1. Trả phòng → hóa đơn cuối → bảng tất toán → Lưu bản nháp.
2. Chọn **Kiểm tra & xác nhận** trên bản đã lưu; màn hình đối chiếu toàn bộ cọc/nợ/khấu trừ/lý do và kết quả.
3. Tick đã kiểm tra → **Xem trước & xác nhận** → hộp xác nhận theo ảnh tham chiếu. Tick hai bên đã đối chiếu → **Hoàn tất trả phòng**.
4. Trang kết quả có trạng thái Đã kết thúc/Đã phát hành/Đã khóa, biên bản bất biến, người/thời điểm xác nhận, số tiền và theo dõi thu/hoàn sau chốt. Chủ nhà và khách thuê xem cùng bản qua `/TatToan/ChiTiet/{hopDongId}` hoặc `/TatToan/KetQua/{hopDongId}`.

Form POST có CSRF; role/module/sở hữu kiểm tra backend; các bước kiểm tra/chốt không dựa vào trường tiền do trình duyệt gửi. Có thể dùng luồng xác nhận không cần JavaScript. Không có nút PDF giả hoặc thông báo đã tải tài liệu chưa được tạo.

## Chạy và kiểm tra

```powershell
.\verification\New-S404Demo.ps1 -Task4
.\verification\Start-S404Demo.ps1 -Task4 -Port 5254
```

`data/s404-task4-demo/latest.txt` trỏ thư mục mới; đọc access.json tại đó để đăng nhập, không in/commit mật khẩu. `/TatToan/XacNhan/1` để chốt bản nháp; `/TatToan/ChiTiet/1` để xem lại. `/TraPhong/Nhap/2` là hợp đồng khác để thử lại từ bước nhập ngày/chỉ số.

```powershell
dotnet build Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s404-task4-tests
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore -o data/s404-task4-tests --filter 'FullyQualifiedName~TatToan|FullyQualifiedName~ChamDut|FullyQualifiedName~XacNhan|FullyQualifiedName~Schema|FullyQualifiedName~Collection|FullyQualifiedName~BaoHong|FullyQualifiedName~TenantInvoice|FullyQualifiedName~InvoiceDetail'
dotnet run --project verification/S404Browser -- <đường-dẫn-demo> http://localhost:5254 task4
```

Dừng app trước build/nâng cấp/sao chép DB. Các bài kiểm tra luôn dùng DB giả/bản sao; chưa nghiệm thu Safari/thiết bị thật hoặc review/hợp nhất bởi thành viên khác. Kết quả cuối và đường dẫn ảnh/report được ghi ở tiến độ sau khi chạy kiểm chứng.

## Kết quả kiểm chứng cuối

- Build đạt; **189/189** test liên quan đạt, trong đó **21** test task 4. Bao gồm chia ngày/chỉ số và task 1–3, chốt có hoàn/vừa đủ/thu thêm/không cọc, thiếu dữ liệu, pending, consent, stale, quyền/module/CSRF, concurrent/replay, audit rollback, ghi thu sau kết thúc, hoàn từng phần/chống trùng/vượt số tiền, schema/bảo toàn dữ liệu và hồi quy thu/hóa đơn/báo hỏng.
- Edge desktop và 360px đạt: trả phòng, sửa bảng, kiểm tra, hộp xác nhận, chốt, kết quả, trạng thái hợp đồng, khách xem; gửi lại chốt trả 409, khách xác nhận/sở hữu chéo trả 403. Các trang kiểm tra/modal/kết quả không tràn ngang. Đã xem ảnh desktop và modal mobile; chưa kiểm tra Safari hoặc thiết bị thật.
- Demo cuối `data/s404-task4-demo/20261015-task4-final`, cổng **5254**, `browser-check-d64d6978/report.json` và ảnh PNG ignored. Hợp đồng 1 **đã chốt** trong quá trình demo; dùng `/TatToan/KetQua/1` hoặc `/TatToan/ChiTiet/1`. Hợp đồng 2 còn hiệu lực để nhập thử từ đầu.
- Đối chiếu DB chỉ đọc: một xác nhận, hai CAN_TRU tổng **7.000.000 đ**, một tin NHAP và một lịch sử phòng TRONG; không tạo thêm thanh_toan. Hóa đơn cuối **5.813.742 đ**, đã gồm 450.000 đ hư hỏng và 3.500.000 đ phạt; cộng nợ cũ 1.200.000 đ, trừ cọc 7.000.000 đ → khách còn phải thu **13.742 đ**. Integrity `ok`, FK không lỗi.
- DB local vẫn v5, demo task 2 v31 và task 3 v32; không nâng/sửa dữ liệu các bộ này. Script trình duyệt chốt thật, nên **mỗi lần chạy toàn luồng cần bộ demo mới**, không xóa/sửa biên bản đã chốt để chạy lại.
- Cảnh báo ImageSharp license/CS8601 có sẵn; không thêm thư viện mới. Không có PDF hoặc tuyên bố đã review/hợp nhất.
