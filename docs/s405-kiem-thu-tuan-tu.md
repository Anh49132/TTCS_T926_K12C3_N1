# Kiểm thử tuần tự S4-05 — 15/10/2026

Nhánh đang mở: `feature/S4-05/04-vacant-rooms-without-listings`.

Chạy lần lượt, không chạy các nhóm song song. Dùng CSDL tạm riêng; không tiêu thụ bộ demo của người dùng hoặc cập nhật CSDL gốc. Không commit/push trong lượt kiểm thử.

| Bước | Nội dung | Trạng thái |
| --- | --- | --- |
| 0 | Build lại mã hiện tại vào thư mục riêng | Đạt: 0 lỗi, 1 cảnh báo ImageSharp có sẵn |
| 1 | Kết thúc hợp đồng, giải phóng phòng, rollback và hồi quy | Đạt: 111/111 |
| 2 | Tự tạo tin Nháp, giữ nội dung/ảnh, chống trùng | Đạt: 13/13 |
| 3 | Kiểm tra và đăng Nháp, quyền và gửi lặp | Đạt: 28/28 |
| 4 | Danh sách phòng trống, lọc, phân trang, quyền | Đạt: 23/23 |
| 5 | Hồi quy tin/ảnh, kích hoạt hợp đồng và trang chủ Khách thuê | Đạt: 46/46 |
| 6 | Luồng trình duyệt desktop và 360px từ đầu tới cuối | Đạt: toàn bộ luồng, desktop/360px |

Kết quả và đường dẫn log sẽ được bổ sung sau từng bước. Người dùng đã xác nhận: toàn bộ S4-05 gồm bốn phần, kèm các hồi quy liên quan; không phải toàn bộ test dự án.

Log đang chạy: [Nhóm 1 — trả phòng/tất toán](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/01-release.log).

Log nhóm 2: [Tạo Nháp](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/02-draft.log). Log nhóm 3: [Đăng Nháp](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/03-publish.log).

Log nhóm 4: [Danh sách phòng trống](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/04-vacant.log).

Log nhóm 5: [Hồi quy tích hợp](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/05-integration.log).

## Kết quả cuối

- Chạy tuần tự từng nhóm: 111/111, 13/13, 28/28, 23/23 và 46/46 đều PASS. Tổng 221 lượt chạy, gồm hai ca được chạy lại ở nhóm hồi quy; sau loại trùng: **219/219 ca PASS**, không bỏ qua.
- Đã đối chiếu thời điểm bắt đầu/kết thúc trong TRX: nhóm sau bắt đầu sau khi nhóm trước hoàn tất, không chạy các nhóm song song.
- Edge desktop/360px PASS: trả phòng → trang chủ → danh sách phòng trống → đúng Nháp → đăng tin → biến mất khỏi hai danh sách. Đăng một nút local: 0,92 giây; gửi lại xác nhận 409 và truy cập trái quyền 403 đúng.
- Web xem trực tiếp đã mở trong Edge ở bên phải, VS Code ở bên trái, đăng nhập Owner, tại `http://localhost:5254/TatToan/XacNhan/1`. Không tự bấm hoàn tất trên bộ demo người dùng; bước trình duyệt chạy bằng bộ riêng cổng5255 và server thử đã dừng.
- CSDL gốc so sánh SHA-256 trước/sau không đổi. Không sửa mã nghiệp vụ, không commit/push. Phạm vi là S4-05 và các hồi quy liên quan, không phải toàn bộ test dự án.
Log trình duyệt: [06-browser.log](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/06-browser.log). Ảnh: [thư mục kết quả](../data/s405-sequential/750dcd10d4954c098d0649ce3f8e9ac0/browser-demo/browser-check-0aadd64d/).
