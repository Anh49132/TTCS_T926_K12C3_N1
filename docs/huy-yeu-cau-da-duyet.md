# Khách đổi ý sau khi yêu cầu đã được duyệt

Chủ nhà mở chi tiết yêu cầu → Thao tác xử lý yêu cầu → Khách đổi ý, không thuê. Nhập lý do 5–500 ký tự, xác nhận và bấm **Hủy yêu cầu đã duyệt**.

- Yêu cầu chuyển từ `DA_DUYET` sang `DA_HUY`; phòng giữ chỗ `DA_DAT_COC` về `TRONG`.
- Tin nguồn tự chuyển về `DANG_HIEN_THI`, cập nhật ngày đăng và hạn mới 30 ngày (kể cả tin nguồn đã ẩn/hết hạn). Không thay đổi tin khác. Chặn toàn bộ thao tác nếu tòa ngừng hoạt động hoặc có tin công khai khác của phòng.
- Chỉ chủ nhà sở hữu phòng, có quyền ghi module yêu cầu thuê, được thực hiện. Khách/quản lý/admin không được dùng thao tác này.
- Chặn nếu đã có hợp đồng gắn yêu cầu (kể cả nháp hoặc đã hủy), hợp đồng đang chiếm phòng, giao dịch cọc xác nhận hoặc giữ chỗ đã duyệt khác. Không tự hủy hợp đồng hay hoàn tiền.
- Kiểm tra phiên bản yêu cầu/phòng; gửi lại không ghi lịch sử lần hai. Trạng thái, nhật ký, lịch sử nghiệp vụ và thông báo in-app được lưu trong cùng transaction.
- Lý do được lưu trong lịch sử nghiệp vụ và hiển thị cho khách; không đưa ghi chú tự do vào nhật ký chung.

Không đổi schema, không chạy updater và không sửa dữ liệu trên DB đang dùng. Kiểm thử sử dụng DB tạm riêng.

Cập nhật theo yêu cầu tự đăng lại: 28/28 test PASS, gồm cấp lại hạn 30 ngày cho tin hết hạn, đăng lại tin nguồn đã ẩn, chặn tòa ngừng hoạt động và tin công khai khác. Quyền, chống gửi trùng và rollback vẫn PASS.

Xác minh 12/10/2026: build thành công; 25/25 test hủy yêu cầu PASS và 121 test hồi quy duyệt thuê/hủy cũ/lập-kích hoạt hợp đồng PASS. Bao gồm quyền vai trò/sở hữu/module, CSRF, phiên bản cũ, hợp đồng đã có, trạng thái phòng đã đổi, schema cọc không nhận diện, bấm đồng thời và rollback lỗi lịch sử/nhật ký/thông báo. Chưa nghiệm thu trực quan form mới trên thiết bị thật.
