# Nối dữ liệu biểu đồ Tổng quan và Tòa nhà — 19/10/2026

Nguyên nhân: Razor đang in cố định dấu gạch ngang và khung biểu đồ trống; bộ chọn khoảng/tháng chỉ thay nhãn ở JavaScript. Không phải thiếu thanh toán trong bộ nghiệm thu.

Đã nối truy vấn hóa đơn phát hành, thanh toán đã xác nhận, cọc CAN_TRU đã xác nhận. Dùng kỳ hóa đơn để nhóm, cùng quy ước với báo cáo doanh thu. Các khoản CHO_XAC_NHAN/DA_HUY không tăng đã thu. Dư nợ = tổng hóa đơn − thu xác nhận − cấn cọc xác nhận (không âm).

- Tổng quan: công nợ mọi kỳ, số hóa đơn nợ/quá hạn và biểu đồ 3/6/12 tháng đến kỳ được chọn. Giữ lọc tòa đang chọn; bộ chọn khoảng gửi request lấy lại số liệu.
- Quản lý tòa: chọn tháng gửi GET; biểu đồ và cột đã thu theo từng tòa, tổng hóa đơn/đã thu/còn nợ/cấn cọc của kỳ. Tháng không có hóa đơn trả số 0 cùng thông báo rõ.
- Kiểm tra quyền tài chính ở service và giới hạn tòa theo chủ sở hữu. Quản lý không nhận số liệu tài chính; Admin chỉ xem. Không đổi schema/DB; không thêm dữ liệu giả để làm biểu đồ.

Kiểm chứng trên DB tạm và bản sao DB nghiệm thu:

- 2 test tính tổng/chống lẫn pending/hủy/cọc, kỳ không hóa đơn, ngăn sở hữu chéo/quản lý/quyền bị thu hồi đạt (`data/dashboard-finance-tests/dashboard-finance.trx`).
- 6 lượt HTTP đối chiếu số liệu biểu đồ với SQL, lựa chọn tháng 9/10 và khoảng 3/12 tháng, kiểm quyền Chủ nhà/Quản lý/Admin đạt (`http-verification.json`).
- Edge headless desktop 1672px và 360px, 2 trang: có cột, không tràn ngang toàn trang, đã chụp ảnh và kiểm trực quan. Artifact: `data/dashboard-finance-tests/browser-check-95cd6e3e`.
- Kỳ 09/2026 của chủ nhà chính trên bản sao tại lúc kiểm: tổng 247.462.400đ, đã thu 163.014.700đ; số liệu phản ánh trạng thái DB khi kiểm, có thể thay đổi khi tiếp tục nghiệm thu.
- Build đạt, chỉ cảnh báo ImageSharp license và CS8601 đã có. Server kiểm thử đã tắt khi bàn giao; DB đang dùng không bị sửa.
