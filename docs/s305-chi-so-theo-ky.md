# S3-05:2 — Chỉ số điện nước theo kỳ

## Phạm vi và schema v17

`DatabaseUpdates` thêm bước v17 (`MeterReadingSchema`), không dùng EF migration. Bảng mới `chi_so_dien_nuoc` lưu một dòng cho mỗi hợp đồng/dịch vụ/tháng: id, hop_dong_id, dich_vu_id, tu_ngay, den_ngay, chi_so_dau, chi_so_cuoi, nguoi_nhap_id, ngay_nhap UTC, da_khoa và phien_ban. Tiền không lưu trong bảng này. Chỉ số decimal được ánh xạ TEXT theo convention EF SQLite hiện có, không tính bằng double.

FK hợp đồng/dịch vụ/tài khoản dùng RESTRICT. Unique `(hop_dong_id,dich_vu_id,tu_ngay,den_ngay)` và CHECK tháng dương lịch đầy đủ ngăn chốt trùng bằng khoảng ngày khác. CHECK chỉ số không âm, tối đa 3 số lẻ, không quá 99.999.999.999,999 và cuối >= đầu; trigger chỉ nhận mã DIEN/NUOC. `da_khoa` mặc định false, `phien_ban` mặc định 0 và được ánh xạ concurrency token. Có guard không sửa/xóa bản đã khóa; không có action khóa/mở khóa trong lát này.

Tòa/phòng suy ra từ hợp đồng, không lưu ID dư. Không thêm trạng thái nháp; tồn tại bản hợp lệ là có dữ liệu đã lưu. Hai dịch vụ được lưu cùng transaction của một phòng; không có trạng thái lưu dở điện rồi nước. Bản chưa khóa có thể lưu lại với phiên bản đúng; tăng phien_ban khi cập nhật. Không xóa bản chỉ số qua UI.

## Dịch vụ cần nhập và dữ liệu tham chiếu

Server chọn kỳ từ tháng hiện tại ở Việt Nam qua ITimeProvider. Phòng DANG_THUE, hợp đồng DANG_HIEU_LUC có kỳ giao tháng; ngày trả phòng giới hạn khoảng thuê. Sắp tầng rồi mã và gộp các kỳ/hợp đồng như lát 1. Dịch vụ phải được gán cho phòng, thuộc danh sách dịch vụ hợp đồng nếu đã có snapshot, chưa ngừng trong kỳ, còn hoạt động, có giá hợp lệ và cấu hình THEO_CHI_SO tại ngày chốt của hợp đồng (ngày 29–31 giới hạn theo tháng). Giá riêng phòng thắng mặc định tòa; cấu hình riêng ngừng không rơi về mặc định. THEO_NGUOI/CO_DINH không có ô nhập và không tạo bản chỉ số giả.

Chỉ số đầu được lấy lại trên server: bản chốt trước kỳ của đúng hợp đồng/dịch vụ; hóa đơn legacy DA_PHAT_HANH, dòng DICH_VU/THEO_CHI_SO có chỉ số cuối; bàn giao cùng hợp đồng có ngày phù hợp. Không dùng lịch sử hợp đồng khác, không đổi NULL thành 0. Thiếu tham chiếu hiển thị rõ và chặn lưu; không tạo bản không tính được tiêu thụ. Giá trị 0 thật được giữ.

Đã chốt khi tất cả dịch vụ theo chỉ số cần nhập có bản trong bảng mới của đúng hợp đồng/kỳ. Không chỉ đếm tồn tại hóa đơn. Các phòng không có meter service không có thao tác lưu; nguồn trạng thái legacy của lát 1 được giữ cho các phòng không có dịch vụ cần nhập.

## GET/POST, quyền và đồng thời

GET `/ChiSoDienNuoc`, POST `/ChiSoDienNuoc/Save`; giữ route module/menu từ lát 1. QUAN_LY phải có quyền module DIEN_NUOC (ghi với POST), tài khoản hoạt động và được phân công tòa. CSRF bắt buộc. Server kiểm lại phòng/hợp đồng/kỳ/dịch vụ/chỉ số đầu; không lấy các quan hệ hoặc số tham chiếu client làm nguồn sự thật. Các query đọc không trả dữ liệu tài chính/hồ sơ khách.

Form riêng cho mỗi phòng; sai điện/nước đánh dấu đúng ô và đúng dòng. JavaScript kiểm lại khi input/submit, lỗi biến mất khi sửa hợp lệ. Server kiểm thiếu/sai số, giới hạn, độ chính xác, >= tham chiếu, phiên bản phòng/bản chỉ số và da_khoa. Number input dùng binder bất biến để dấu chấm thập phân không lệ thuộc Windows locale.

POST dùng BEGIN IMMEDIATE trước đọc lại/kiểm tra, unique ngăn insert trùng, phien_ban kiểm cập nhật lạc quan. Người lưu sau có lỗi stale và phải GET lại; form lỗi không tự thay token mới để ghi đè. Ghi tất cả bản chỉ số và nhật ký cùng transaction; lỗi SQL/audit rollback. Thành công redirect GET, đọc lại số vừa lưu và Đã chốt; phòng khác không đổi.

## Nâng cấp database

Không web auto-update. Dừng app, backup DB của chính máy, đặt đúng DatabasePath, rồi chạy `dotnet run --project QL_PhongTro -- --update-database` và `--check-database` theo quy trình hiện có. Chỉ chạy khi đã được phép nâng DB mục tiêu. Task implementation không nâng DB demo.

v17 thêm bảng/index/trigger rỗng, giữ mọi dòng/bảng cũ và không backfill. Updater tạo backup trước nâng, bước v17 trong transaction và kiểm integrity/FK trước marker version. Chạy lại v17 không thay dữ liệu. Nếu marker bị thiếu nhưng bảng/trigger khớp chính xác định nghĩa bước v17, chỉ ghi lại marker; bảng lạ/thiếu protections bị từ chối, không sửa hoặc nhận ngầm schema không tương thích. AccountReuseSchema chỉ mở rộng khoảng version được kiểm tra tới 17, không đổi dữ liệu/index tài khoản đã ở v5+.

## Chưa triển khai

Không progress S3-05:3, cảnh báo 200% S3-05:4, tối ưu 360px S3-05:5, phát hành S3-06 hoặc action khóa kỳ S3-10. Khi tích hợp hóa đơn sau này phải đọc bản đã chốt, snapshot chỉ số và liên kết `chi_so_id`, khóa nguồn cùng transaction phát hành. Lát này không dùng/chỉnh hoa_don hoặc chi_tiet_hoa_don để chứa chỉ số mới và không sửa cột chi_so_id.

Không hỗ trợ reset đồng hồ, bổ sung chỉ số đầu legacy bị thiếu hoặc sửa kỳ cũ qua màn này. Nghiệm thu kỹ thuật tiếp theo đã điều khiển trình duyệt in-app trên hai DB copy: luồng một công tơ, hai công tơ, lỗi/correction/lưu/reload và thiếu tham chiếu PASS (xem cập nhật 07/10/2026 trong tien-do.md). Thiết bị thật/Chrome/Safari chưa kiểm tra; tối ưu 360px thuộc lát 5.
