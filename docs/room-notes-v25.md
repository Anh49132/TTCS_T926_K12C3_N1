# Ghi chú phòng — schema v25

Thêm bảng `ghi_chu_phong`, không thay đổi các bảng cũ. Tệp PDF/PNG/JPG tối đa 10 MB được lưu BLOB trong SQLite để backup và giao dịch bao gồm cả tệp. Chỉ chủ nhà của phòng và ADMIN truy cập; khách thuê không xem được. Tải qua `PhongTro/TepGhiChuPhong`, kiểm tra quyền và `no-store`, luôn tải dưới dạng đính kèm.

Nội dung: tiêu đề 1–100 ký tự, nội dung 1–1.000 ký tự, ghim, một tệp tùy chọn. Không sửa/xóa ghi chú trong phiên bản này. Khóa UUID chống gửi trùng. Ghi chú và metadata audit trong cùng transaction; audit không lưu nội dung, tiêu đề, tên tệp hoặc bytes.

## Nâng cấp và phục hồi

1. Dừng ứng dụng, sao lưu SQLite ra thư mục riêng (kèm WAL nếu chưa checkpoint; nên dùng SQLite backup).
2. Chạy trên **bản sao** trước: đặt `DatabasePath` trỏ bản sao; chạy `dotnet run --project QL_PhongTro -- --update-database`, rồi `--check-database`.
3. Updater tự tạo `.before-update-<id>.bak`; v25 tạo bảng và index mới. Chạy lại không thay đổi dữ liệu.
4. Kiểm tra `PRAGMA integrity_check`, `PRAGMA foreign_key_check` và số lượng bản ghi cũ. Khi đã xác minh, người vận hành thực hiện nâng cấp DB thật lúc ứng dụng dừng.
5. Phục hồi: dừng app, giữ DB lỗi để điều tra, phục hồi bản backup vào đường dẫn riêng và chạy với phiên bản mã tương ứng v24. Không ghi đè DB đang mở.

## Kiểm thử

Bản sao `data/room-notes-demo-v25/notes.sqlite` được nâng từ v24. Không nâng DB local gốc. `verification/verify_room_notes.py` chỉ chạy khi server đang dùng bản sao này: kiểm tra lưu/ghim/tệp, gửi trùng, riêng tư audit, định dạng tệp, CSRF, sở hữu chéo và rollback khi audit thất bại. Backup kiểm tra integrity/FK và bảo toàn số bản ghi cũ trước khi thử thêm ghi chú.
