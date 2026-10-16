# Đối chiếu tiêu chí S4-01 đến S4-09 — 16/10/2026

> Đây là kết quả **trước sửa**. Xem [bàn giao hoàn thiện](hoan-thien-tieu-chi-S4-20261016.md) cho trạng thái sau sửa và bằng chứng kiểm thử mới.

Nguồn: tệp tiêu chí người dùng gửi, mã tại checkout hiện tại, đọc metadata DB local ở chế độ chỉ đọc và bộ test liên quan. Không sửa mã nghiệp vụ, không chạy updater trên DB local.

## Kết luận

Chưa đủ điều kiện nghiệm thu. Cả 9 story có mã triển khai, nhưng có lỗi tích hợp schema, lỗi công nợ, thiếu nội dung email và thiếu bước xác nhận báo thanh toán của khách. “Có mã” bên dưới không đồng nghĩa đã vượt kiểm thử hoặc nghiệm thu giao diện.

## Lỗi cần xử lý

1. **Chặn khởi tạo/nâng cấp:** `DatabaseUpdates.CurrentVersion = 36`, nhưng cuối `Update` chỉ gọi `InvoicePaymentReportSchema.Upgrade` (ghi v35), rồi `Check`. Không gọi `ReportPerformanceSchema.Upgrade`, `EmailGuiLogSchema.Upgrade`, `InvoicePaymentProofSchema.Upgrade`. Report và payment report cùng dùng v35; email log và payment proof cùng dùng v36. Tạo DB tạm thất bại với `Required report index ix_hop_dong_report_room_status_return is missing or has an unexpected definition.` Cần kế hoạch phiên bản thống nhất, bảo toàn các bước đã chia sẻ và kiểm tra trên bản sao.
2. **Công nợ lộ phạm vi dữ liệu:** `ModulesController.CongNoPage` và `ExportCsv` không lọc `building.ChuNhaId` theo người đăng nhập; cả danh sách tòa cũng lấy toàn bộ. Qua `/Modules/TAI_CHINH?section=cong-no`, quyền module không thay thế kiểm tra sở hữu. Các route `/CongNo` và `/CongNo/ExportCsv` lại dùng `[ModuleAccess]` không truyền mã module; không có route `code`, nên người dùng thường bị từ chối do mã rỗng.
3. **Công nợ tính/hiển thị sai:** nối `contract.KhachDungTenId` trực tiếp với `TaiKhoans.Id`, trong khi FK thật trỏ tới `khach_thue.id`; phải qua hồ sơ thuê. Dư nợ không trừ `CAN_TRU` đã xác nhận. Ngày quá hạn dùng `DateTime.Today` thay cho ngày Việt Nam qua `ITimeProvider`.
4. **Chưa nối báo trả → xác nhận thu:** `GhiThuHoaDonService.GhiAsync` tạo khoản `DA_XAC_NHAN` mới, không chuyển báo cáo `CHO_XAC_NHAN` tương ứng. Chưa thấy action/service xác nhận báo cáo đã gửi. Vì vậy ghi thu không giải quyết báo cáo chờ; khách có thể tiếp tục thấy “Chờ xác nhận”.
5. **Email nhắc hạn thiếu thông tin:** `NhacHanService.TaoThongBao` chỉ có số còn phải trả và hạn thanh toán; thiếu kỳ hóa đơn và tổng tiền. `PasswordEmailSender.SendIssuedAsync` chỉ nối thêm liên kết, không bổ sung hai trường này.

## Ma trận AC

Các AC đánh số theo thứ tự dấu đầu dòng trong tệp người dùng gửi.

| Story | Đối chiếu từng AC |
| --- | --- |
| S4-01 | AC1 có form số tiền/ngày/hình thức/ảnh; AC2 có kiểm tra số nguyên > 0 và ≤ dư nợ, có trừ cấn cọc; AC3 có kho ảnh private, giới hạn 5 MiB, chủ nhà sở hữu/ADMIN, token gắn người xem và hạn 15 phút; AC4 có trạng thái chờ, không có sửa, hủy khi còn chờ, nhưng thiếu luồng xác nhận thực tế (lỗi 4). Schema ảnh chưa được updater nối đầy đủ (lỗi 1). |
| S4-02 | AC1 có nhiều khoản thu, ngày/hình thức/người xác nhận; AC2 có trả một phần/đủ, chặn vượt dư nợ bằng transaction IMMEDIATE; AC3 có tính lại summary sau ghi thu; AC4 có lý do hủy và lưu lịch sử bất biến. Phần xác nhận báo trả từ S4-01 chưa nối (lỗi 4). |
| S4-03 | AC1 có các cột nhưng sai tên người và dư nợ trong trường hợp cấn cọc; AC2 có tham số lọc nhưng lỗi quyền/route; AC3 có tổng của tập kết quả nhưng nguồn tính còn sai; AC4 chưa tìm thấy bộ test riêng ≥ 5 trả thiếu + 2 quá hạn nhiều kỳ; AC5 có CSV UTF-8 BOM nhưng kế thừa lỗi quyền/tính toán. Chưa đạt. |
| S4-04 | AC1–5 đều có triển khai: trả phòng/chỉ số cuối, hóa đơn kỳ cuối, bảng cọc/nợ/khấu trừ, chấm dứt trước hạn/phạt, chốt kết thúc và PDF. Có các test `RoomServicesTests` cho TraPhong/TatToan/ChamDut/XacNhan/Pdf; lần kiểm tra này bị chặn từ fixture, chưa xác nhận chạy đạt. |
| S4-05 | AC1 có giải phóng phòng cùng transaction; AC2 có nhân bản tin nháp; AC3 có đăng tin nháp và test `DraftListingPublishTests`, chưa đo thao tác người dùng < 1 phút; AC4 có danh sách phòng trống chưa đăng và test `VacantRoomsWithoutListingsTests`. Chưa xác nhận chạy đạt do lỗi schema. |
| S4-06 | AC1–4 có triển khai và test: thiết bị/mô tả/mức/3 ảnh × 5 MiB, mã theo tháng VN, kiểm tra hợp đồng/phòng của khách, theo dõi trạng thái/phản hồi. Test bị lỗi fixture schema, chưa xác nhận đạt. |
| S4-07 | AC1–4 có triển khai và test: chuyển trạng thái/ghi chú, chi phí/bên chịu và liên kết hóa đơn, cảnh báo gấp > 24h, bộ lọc. Có test rollback/quyền/sở hữu/đồng thời; chưa chạy qua fixture trong lần này. |
| S4-08 | AC1 có phát hành và nhắc trước 2 ngày/quá hạn; AC2 chưa đạt vì thiếu kỳ và tổng tiền trong email nhắc; AC3 có lịch 08:00 VN và chống tạo trùng theo mốc; AC4 có tối đa 4 lần gửi (1 đầu + 3 thử lại), trạng thái lỗi/log nhưng schema log chưa được updater cài; AC5 có chuông/số chưa đọc/đánh dấu đọc. Chưa nghiệm thu SMTP thật. |
| S4-09 | AC1–4 có service/view và test tổng hợp tài chính, lấp đầy cuối tháng, giới hạn 12 tháng, biểu đồ 6 tháng. AC5 có test benchmark 500 phòng/12 tháng gồm HTTP render, nhưng lần chạy này không qua khởi tạo DB, nên chưa có bằng chứng đạt < 3 giây. |

## Giới hạn kiểm chứng

- DB mặc định `QL_PhongTro/Data/local-dev.sqlite` đang ở v27; FK `hop_dong.khach_dung_ten_id` trỏ `khach_thue`. Không nâng cấp hoặc thay dữ liệu DB này.
- Build test thực hiện được, có cảnh báo license ImageSharp và CS8601 trong AuthController.
- Kết quả: **474 test, 37 đạt, 437 lỗi, 0 bỏ qua**, khoảng 1 phút 58 giây. Cả 437 lỗi đều có cùng thông báo thiếu/sai index `ix_hop_dong_report_room_status_return` ở bước khởi tạo; không được hiểu thành 437 lỗi nghiệp vụ độc lập. Kết quả lưu tại `data/s4-review/s4-review.trx`.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter "FullyQualifiedName~RoomServicesTests|FullyQualifiedName~Invoice|FullyQualifiedName~BaoHong|FullyQualifiedName~BaoCaoDoanhThu|FullyQualifiedName~NhacHan|FullyQualifiedName~VacantRoomsWithoutListings|FullyQualifiedName~DraftListingPublish" --logger "trx;LogFileName=s4-review.trx" --results-directory data/s4-review`.
- Chưa nghiệm thu trực quan 360px/thiết bị thật, SMTP thật, thời gian bấm đăng < 1 phút. Tài liệu AGENTS.md mục “chưa có Sprint 3/4” không còn phản ánh mã hiện tại.
