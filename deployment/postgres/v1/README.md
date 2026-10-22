# PostgreSQL port v1 — triển khai từ SQLite v42

Schema ứng viên xuất từ SQLite thực tế v42: 61 bảng, 51 index tường minh, 60 trigger và 1 view nghiệp vụ. Có thêm catalog tương thích chỉ phục vụ kiểm tra module. Không phải script nâng cấp SQLite; không áp dụng vào database đang có dữ liệu.

`schema.candidate.sql` giữ tên cột/bảng và ràng buộc. DateTime giữ TEXT để bảo toàn 100 ns; DateOnly dùng date, bool integer 0/1, decimal numeric. Unique thứ tự ảnh được kiểm tra tại commit để sắp xếp trong một UPDATE trong khi CHECK 1..8 vẫn hoạt động.

`--rehearse` chỉ nhận nhánh Neon thử đã pin, sau khi dừng app nguồn. Công cụ dùng backup API tạo bản sao mới trong ignored data/, kiểm tra integrity/FK, nhập schema UUID trong transaction, đối chiếu giá trị hàng/entity và các kết quả đọc tài chính/lịch hẹn, rồi rollback. Không seed lại tài khoản hoặc reset mật khẩu.

Sau khi người dùng cho phép rõ ràng lưu bản sao vào main ngày 23/10/2026, thêm lệnh `--cutover-empty-main`. Lệnh chỉ nhận main đã pin, từ chối public có bất kỳ object nào, thực hiện cùng kiểm tra trước commit và đặt sequence bằng giá trị lớn nhất giữa ID hiện có và sqlite_sequence. Đã nhập 61 bảng/19.489 dòng, 49 EF entity khớp, nguồn không đổi. Không chạy lại vào main đã có dữ liệu.

Đã kiểm chứng ghi/commit/chống thu vượt đồng thời/replay, audit rollback, trigger bất biến, session và reorder trên schema giả riêng. 2.889 file tham chiếu đã chuyển vào private Object Storage và đọc lại hash khớp; key ring online riêng được mã hóa. Web mặc định SQLite local, chọn PostgreSQL bằng cấu hình. Xem `docs/trien-khai-render-neon.md` cho giới hạn hosting và trạng thái nghiệm thu HTTP/Linux.
