# S4-03 — Công nợ theo phòng và tòa nhà

`/CongNo` là màn hình chung, có tổng quan, tổng hợp theo tòa, ưu tiên thu, danh sách phòng, tìm/lọc/sắp xếp và phân trang gọn. Không thêm trang riêng theo tòa. Các tổng đều theo toàn bộ kết quả đang lọc, không chỉ trang đang xem.

## Chi tiết và quy ước số liệu

- Nút **Chi tiết** hoặc **Xem công nợ** mở hộp cùng màn, tải `/CongNo/Phong/{id}`. Hộp liệt kê hóa đơn đã phát hành còn dư nợ: mã/kỳ/người đứng tên, tổng tiền, đã thu xác nhận, cấn cọc, còn thiếu, hạn trả, ngày quá hạn và tiền chờ xác nhận. Mã hóa đơn mở chi tiết hóa đơn hiện có.
- Dư nợ = tổng − khoản thanh toán DA_XAC_NHAN − cấn cọc CAN_TRU đã xác nhận. CHO_XAC_NHAN, TU_CHOI, DA_HUY không vào đã thu. Chờ xác nhận chỉ hiển thị để theo dõi.
- Ngày quá hạn = max(ngày Việt Nam hiện tại − hạn trả, 0). “Quá hạn nhiều kỳ” cần ít nhất hai kỳ tháng/năm khác nhau có hóa đơn còn nợ và đã quá hạn, không chỉ dựa vào tổng số hóa đơn.
- Hộp chi tiết luôn hiển thị toàn bộ dư nợ của phòng, không cắt hóa đơn theo bộ lọc số ngày/tiền của danh sách. Tổng hợp theo tòa dùng ID tòa, tránh gộp nhầm các tòa trùng tên. Tòa không nợ vẫn có dòng 0.
- Chủ nhà chỉ truy cập phòng thuộc tòa mình; ADMIN đọc; khách và quản lý không vào. Endpoint chi tiết kiểm tra sở hữu ở backend, dữ liệu trả no-store. Không sửa DB hoặc schema.

## Bộ lọc và CSV

Ngày/tiền là số nguyên không âm; tiền nhập bằng đồng không dấu phân cách. Chặn số âm, số thập phân, chuỗi không phải số, tràn số và từ lớn hơn đến ở trình duyệt lẫn server. Yêu cầu sai trả HTTP400, giữ lỗi trên form, không hiển thị kết quả hoặc cho xuất CSV. Không có kết quả có hướng dẫn nới/bỏ bộ lọc.

Sắp xếp theo tòa/phòng, tiền còn thiếu tăng/giảm hoặc ngày quá hạn tăng/giảm; phân trang giữ bộ lọc. CSV dùng toàn bộ kết quả đang lọc/tìm/sắp xếp, UTF-8 BOM, escape nội dung và chặn công thức từ tên người/phòng/tòa. Tên tệp chứa thời gian Việt Nam, ID tòa, khoảng ngày, khoảng tiền, trạng thái, sắp xếp; từ khóa được biểu diễn bằng hash 8 ký tự để tránh tên tệp chứa thông tin cá nhân. Có phản hồi đang xuất/thành công/thất bại; “thành công” nghĩa là trình duyệt đã nhận tệp để tải, không khẳng định người dùng đã lưu xuống đĩa.

## Kiểm chứng

- Test `DebtFivePartialPaymentsTwoMultiPeriodOverduesFiltersAndCsvMatch` dựng DB riêng với 5 phòng trả thiếu, 2 phòng quá hạn nhiều kỳ, một khoản chờ, một khoản hủy và cấn cọc. Đối chiếu dư nợ/chi tiết, bốn kiểu sắp xếp, CSV và quyền sở hữu.
- `RoomServicesTests.DebtFilters.cs` kiểm tra số âm, khoảng đảo, tiền sai định dạng/tràn số, sắp xếp sai, API CSV400 và service từ chối khoảng sai.
- Bộ `verification/DebtDashboardBrowser` chạy Edge trên DB sao chép; đầu vào thư mục QA chứa `access.json` (tài khoản riêng tư) và `debt-case.json` (`roomId` của phòng nhiều kỳ). Kiểm tra desktop/360px, hộp chi tiết, CSV thành công/thất bại, rỗng và lỗi bộ lọc. Không đưa đầu vào/ảnh/CSV vào Git.
- `python verification/Verify-S403Data.py <qa.sqlite> [YYYY-MM-DD]` kiểm tra chỉ đọc ít nhất 5 phòng trả thiếu và 2 phòng quá hạn nhiều kỳ, integrity/FK. Fixture hiện tại ngày 16/10/2026 có 6 phòng trả thiếu (TC003–TC007, TC010), 2 phòng quá hạn nhiều kỳ (TC003/TC004), tổng dư nợ 21.500.000 đ. Không đổi DB đang dùng để tạo ca thử.
- Nếu cần tạo bộ dữ liệu giả mới, dùng `verification/New-Sprint4AcceptanceDemo.py` với file cấu hình tài khoản riêng tư theo hướng dẫn trong script; script tạo DB mới, không đổi đường dẫn chạy hiện tại. Chưa kiểm tra Safari hoặc thiết bị thật.
