# S4-04 task 3 — Chấm dứt trước hạn và phạt cọc

Ngày thực hiện: 15/10/2026. Nhánh `feature/S4-04/03-early-termination-penalty` bắt đầu từ `29d4f02`; đã khôi phục phần phụ thuộc task 1–2 từ task 2 commit `79d428a` vào worktree. Không commit/push hoặc thay đổi dữ liệu dùng thật.

## Quy tắc PO đã xác nhận

- Trả trước hạn khi ngày trả thực tế **nhỏ hơn ngày kết thúc kỳ cuối cùng**, kể cả các kỳ gia hạn. Bằng ngày kết thúc là đúng hạn.
- Lý do: Khách chủ động chuyển đi; Hai bên thỏa thuận; Khách vi phạm hợp đồng; Lý do khác. Lý do khác bắt buộc ghi rõ, tối đa 500 ký tự.
- Trước hạn luôn cần lý do, kể cả không phạt. Trả đúng hạn không áp dụng phạt.
- Chọn Có: mức phạt là số nguyên đồng, lớn hơn 0, không vượt **cọc thỏa thuận** của hợp đồng. Cọc thỏa thuận bằng 0 thì không thể chọn mức phạt dương.
- Chọn Không: số phạt chuẩn hóa bằng 0; giá trị cũ trong ô mức phạt không được cộng.
- Kết quả = cọc thực giữ − dư nợ kỳ cuối − dư nợ kỳ trước − hư hỏng − phạt cọc. Dương hoàn khách, âm khách trả thêm, bằng 0 vừa đủ. Hư hỏng vẫn làm tròn từng dòng AwayFromZero theo task 2; mức phạt lẻ đồng bị từ chối.

Ngày đối chiếu và giới hạn cọc do backend đọc từ CSDL, không nhận từ trường ẩn do trình duyệt gửi lên.

## Luồng và giao diện

1. Chủ nhà mở hợp đồng → Ghi nhận trả phòng. Ngày trả tự xác định trạng thái trước hạn; chọn lý do, Có/Không phạt và nhập mức phạt khi Có. Lý do khác mở thêm ô ghi rõ.
2. Tính/kiểm tra hóa đơn rồi lưu. Lưu hóa đơn và thông tin chấm dứt trong cùng transaction; chuyển qua bảng tất toán giữ nguyên lựa chọn vừa nhập.
3. Bảng tất toán cho chủ nhà đối chiếu/sửa lựa chọn phạt và các dòng hư hỏng. Máy chủ tính lại dự tính; nút Xem dự tính hoạt động khi không dùng JavaScript. Lưu bản nháp ghi đồng thời thông tin chấm dứt, bảng và chi tiết khấu trừ.
4. Mỗi bảng có tối đa một dòng loại `PHAT_COC`, tách khỏi các dòng `HU_HONG`. Sửa hoặc lưu lại thay dòng phạt, không thêm lặp. Chuyển Không loại bỏ dòng phạt và giữ các dòng hư hỏng.
5. Chủ nhà và khách thuê mở cùng trang chi tiết đã lưu, cùng lý do và số tiền. Khách chỉ xem; chủ nhà khác/quản lý/khách không liên quan bị chặn. Bảng đã chốt không sửa được.

Giao diện dùng theme NhàTốt và bố cục thẻ nhập liệu/khối kết quả theo ảnh tham chiếu, có trạng thái trước hạn, chọn lý do, Có/Không phạt, mức phạt, tổng khấu trừ và kết quả màu đỏ/xanh. Mobile chuyển một cột. Mọi POST có CSRF; controller kiểm tra module, service kiểm tra tài khoản/vai trò/sở hữu.

Trang xem dùng snapshot đã lưu; không tự đổi số liệu khi nguồn nợ/điều khoản thay đổi. Trang sửa đối chiếu nguồn hiện tại. Fingerprint bao gồm điều khoản, cọc thỏa thuận, phiên bản thông tin chấm dứt và dữ liệu tài chính; lưu nguồn cũ bị từ chối. SQLite BEGIN IMMEDIATE và phiên bản bảng ngăn ghi đồng thời. Nhật ký cùng transaction, không ghi lý do khác/ghi chú tự do.

## Schema v32 và dữ liệu cũ

- Giữ nguyên bước v31, nối thêm `ChamDutHopDongSchema` v32.
- Thêm `cham_dut_hop_dong` (unique hợp đồng và hóa đơn cuối): ngày trả, ngày kết thúc/cọc thỏa thuận đối chiếu, trạng thái trước hạn, lý do, lý do khác, áp dụng phạt, số phạt, phiên bản, người/thời điểm UTC.
- Bổ sung `thanh_ly_hop_dong.tien_phat` mặc định 0; `chi_tiet_khau_tru.loai_khoan` mặc định HU_HONG. Index unique có điều kiện bảo đảm tối đa một PHAT_COC/bảng.
- FK RESTRICT, CHECK ngày/lý do/số tiền và trigger bảo vệ dữ liệu chấm dứt cùng hợp đồng/hóa đơn cuối, chống xóa/sửa bản đã chốt.
- Không suy đoán lý do cũ, không tự tạo hồ sơ chấm dứt cho bảng task 2. Trang xem báo chưa đối chiếu; chủ nhà chọn lý do khi lưu lại bảng nháp trước hạn.
- Updater sao lưu trước, nâng cấp trong transaction, integrity/FK và kiểm tra schema, chạy lại an toàn. Test bản sao v31 có bảng/dòng hư hỏng đã lưu so sánh toàn bộ cột cũ của mọi bảng trước/sau. Bản gốc không đổi.
- Đã đọc schema thật: `QL_PhongTro/Data/local-dev.sqlite` v5; demo task 2 `data/s404-task2-demo/20261014-task2-verified/s404.sqlite` v31. **Không nâng hoặc sửa hai DB này.** Schema mới chỉ cài trên DB giả/bản sao và demo task 3 mới.

Task 3 vẫn là tất toán **nháp**. Phạt/hư hỏng đang là khoản dự tính khấu trừ; chưa ghi thu/hoàn/cấn cọc thật hoặc phát hành hóa đơn. Bước xác nhận sau này phải đưa các khoản này vào PHAT_SINH của hóa đơn cuối và CAN_TRU theo AGENTS.md mục 7.6, rồi loại trừ việc cộng lại ở sổ tất toán. Không triển khai xác nhận kết thúc hợp đồng, đổi phòng/tin đăng hay PDF trong task này.

## Kiểm chứng

- Build thành công. Cảnh báo ImageSharp license và CS8601 tại AuthController có sẵn.
- **145/145** test liên quan đạt, gồm **24** ca task 3, 38 task 1–2 và 83 hồi quy schema/thu tiền/báo hỏng. Có đúng hạn, trước hạn có/không phạt, lý do thiếu/sai/khác, mức phạt thiếu/0/âm/lẻ/vượt cọc, giới hạn cọc, kỳ gia hạn, stale, lưu/sửa/bỏ phạt không trùng, tổng tiền, chia sẻ/phân quyền/CSRF, audit rollback và nâng cấp bản sao v31/v30.
- Edge headless kiểm tra thực tế nhập ngày/chỉ số/lý do/phạt ở trả phòng; thêm/sửa/xóa hư hỏng; Có→Không→Có phạt tự cập nhật; lưu và khách xem đúng một dòng phạt. Khách sửa và khách ngoài hợp đồng nhận 403. DOM 360px rộng 360 ở trả phòng, chủ nhà và khách thuê.
- Demo tính tay: 7.000.000 − 1.863.742 − 1.200.000 − 450.000 − 3.500.000 = **−13.742 đ**, khách trả thêm 13.742 đ. Không phạt: hoàn 3.486.258 đ.
- Kết quả máy tại `data/s404-task3-test-results/s404-task3.trx`; ảnh/report trình duyệt ở thư mục `browser-check-*` của demo (ignored).
- Chưa kiểm tra Safari/thiết bị thật hoặc được thành viên khác review/hợp nhất; chưa gọi đó là nghiệm thu toàn story.

## Chạy và demo

```powershell
.\verification\New-S404Demo.ps1 -Task3
.\verification\Start-S404Demo.ps1 -Task3 -Port 5253
```

Đăng nhập lấy thông tin riêng trong `data/s404-task3-demo/<bộ-mới>/access.json`; `latest.txt` trỏ bộ mới nhất. Không in/commit mật khẩu, DB hoặc backup. Tạo demo từ chối ghi đè thư mục cũ; task 2 giữ `latest.txt` riêng.

- `/TraPhong/Nhap/2`: phòng A104 để nhập thử trả trước hạn và kiểm tra hóa đơn.
- `/TatToan/Nhap/1`: bảng A103 sửa lý do/phạt/hư hỏng; `/TatToan/ChiTiet/1`: bản đã lưu cho cả hai vai trò.
- `/TraPhong/ChiTiet/2`: hóa đơn cuối A103.

```powershell
dotnet build Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s404-task3-tests
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore -o data/s404-task3-tests --filter 'FullyQualifiedName~TatToan|FullyQualifiedName~ChamDut|FullyQualifiedName~Schema|FullyQualifiedName~Collection|FullyQualifiedName~BaoHong'
dotnet run --project verification/S404Browser -- <đường-dẫn-demo> http://localhost:5253 task3
```

Dừng app trước build/nâng DB. Khi cần nâng dữ liệu thật: sao lưu và thử bản sao, kiểm tra kết quả với người phụ trách CSDL rồi mới chọn DB để chạy lệnh quản trị; các trang MVC không tự nâng schema.
