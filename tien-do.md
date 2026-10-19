### 19/10/2026 – Thông tin chủ nhà trên tin công khai
- Thẻ tìm phòng và chi tiết tin hiển thị tên, số điện thoại chủ sở hữu tòa nhà, có liên kết gọi điện; không lấy nhầm liên hệ quản lý đăng tin. Thông tin lấy từ tài khoản hiện tại, không sửa DB/schema. Có thông báo khi chưa có số liên hệ.
- Build và 4 kiểm thử HTTP công khai đạt, gồm trường hợp quản lý đăng thay chủ nhà; kiểm chứng trên DB tạm, server để tắt. Kết quả local: data/listing-contact-tests/listing-contact.trx.

### 19/10/2026 – Biểu đồ Tổng quan/Tòa nhà dùng số liệu thật
- Thay placeholder bằng tổng hóa đơn/thu xác nhận/cấn cọc/công nợ; biểu đồ theo kỳ và bộ chọn 3/6/12 tháng, lọc tòa và chọn tháng tải lại số liệu. Không lẫn khoản chờ/hủy; không cho quản lý xem tài chính.
- 2 test nghiệp vụ, 6 lượt HTTP đối chiếu SQL, Edge desktop/360px đạt; build đạt. Chỉ thử DB tạm/bản sao, không đổi DB thật, server tắt. Chi tiết: [docs/dashboard-finance-20261019.md](docs/dashboard-finance-20261019.md).

### 19/10/2026 – Sửa tự chuyển về đăng nhập nhầm
- Bộ kiểm tra phiên không còn coi lỗi server tạm thời là hết phiên. JWT refresh thất bại kiểm tra cookie MVC trước khi chuyển Login, lỗi mạng/5xx không xóa token; không thay đổi thời hạn hoặc cơ chế thu hồi phiên backend.
- 13 ca JavaScript giả lập và 6 ca backend đạt; sửa dọn fixture AuthTests để dừng worker trước khi xóa DB tạm. Chi tiết và giới hạn kiểm chứng: [docs/session-redirect-fix-20261019.md](docs/session-redirect-fix-20261019.md). Không đổi DB.

### 19/10/2026 – Thay DB bằng bộ nghiệm thu mở rộng
- Theo yêu cầu trực tiếp, tạo DB mới v41: 10 tòa/480 phòng/280 hợp đồng/840 hóa đơn ba kỳ, chia 5 chủ nhà/4 quản lý/300 khách/1 Admin. Có ảnh, dịch vụ, chỉ số, cọc, người ở ghép, tin đăng, yêu cầu và báo hỏng; đủ trả đủ/thiếu/quá hạn/chờ xác nhận.
- DB cũ giữ nguyên, thêm backup đã kiểm integrity/FK và cấu hình phục hồi; chuyển `.env.local` sang bộ mới, email pickup, server tắt. File tài khoản xuất riêng trong thư mục data bị ignore.
- Kiểm số liệu, 310 mật khẩu, ảnh và HTTP 4 vai trò đạt. Bàn giao: [docs/acceptance-dataset-20261019.md](docs/acceptance-dataset-20261019.md).

### 18/10/2026 – Bỏ mục tài khoản trùng lặp trên sidebar Chủ nhà
- Bỏ mục Thông tin tài khoản và nhóm Tài khoản khỏi sidebar Chủ nhà; hồ sơ cá nhân tiếp tục truy cập từ menu tài khoản góc trên. Không đổi quyền backend hoặc DB.

### 18/10/2026 – Bỏ bảng phân quyền khỏi giao diện Chủ nhà
- Sửa đường dẫn module Thông tin tài khoản về `/HoSo/CuaToi`; Admin vẫn mở bảng phân quyền. Trang `/Permissions` chỉ cho Admin, vai trò khác nhận 403; không đổi cấu hình quyền trong DB.
- Kiểm thử HTTP đường dẫn tài khoản của chủ nhà, chặn bảng phân quyền, menu và ma trận Admin; không nâng DB.

### 18/10/2026 – Email báo và xác nhận thanh toán
- Thêm email khi khách báo/hủy báo thanh toán và khi chủ nhà xác nhận/từ chối; liên kết mở hóa đơn đúng vai trò. Hàng đợi dùng bảng email_gui_log hiện có, không nâng schema hay gửi bù lịch sử.
- Kiểm tra lại người nhận/quyền, bỏ thông báo lỗi thời, thử lại tối đa ba lần; Admin lọc riêng email thanh toán. Chờ xác nhận không giảm nợ; lỗi tạo hàng đợi rollback nghiệp vụ.
- 23 kiểm thử liên quan đạt; chưa gửi SMTP thật, pickup vẫn lưu thư local. Không khởi động server. Chi tiết: [docs/payment-email-20261018.md](docs/payment-email-20261018.md).

### 18/10/2026 – Email báo hỏng và cập nhật, schema v41
- Báo mới gửi chủ nhà/quản lý được phân công; đổi trạng thái/phản hồi gửi khách đã báo và chủ nhà. Hàng đợi cùng transaction, unique chống tạo trùng, kiểm tra lại quyền/người nhận lúc gửi, ba lần retry, tiếp tục sau khởi động lại.
- Admin có mục email báo hỏng trong tra cứu lỗi gửi; dùng chung SMTP/pickup hiện có, không đổi credential hoặc tự gửi bù lịch sử. Pickup vẫn chỉ tạo thư local.
- 86 kiểm thử đạt; bản sao v40→v41 giữ nguyên 58 bảng/25.130 dòng, integrity/FK và chạy lặp đạt. Sau khi người dùng cho phép, đã backup/nâng DB đang dùng lên v41, bảo toàn dữ liệu và check schema đạt; hàng đợi rỗng, không gửi bù. Giữ cấu hình pickup, server tắt. Bàn giao: [docs/repair-email-20261018.md](docs/repair-email-20261018.md).

### 18/10/2026 – Chỉnh sửa tài khoản cá nhân cho bốn vai trò
- Thêm trang sửa họ tên/SĐT và thay/xóa ảnh đại diện; liên kết từ menu và hồ sơ, ảnh hiện trên header. Đồng bộ tên/SĐT hồ sơ khách, email xác thực và vai trò giữ nguyên.
- Ảnh private JPG/PNG ≤5MB, xác minh/resize 512px/bỏ metadata, lưu cạnh DB và ignore Git; CSRF, sở hữu, chống trùng SĐT và biểu mẫu cũ ở backend. Không nâng schema, không sửa DB đang dùng.
- Build, 7 kiểm thử và Edge bản sao DB cho cả bốn vai trò ở desktop/360px đạt. Server QA đã dừng. Bàn giao: [docs/personal-account-20261018.md](docs/personal-account-20261018.md).

### 18/10/2026 – Kéo di chuyển chatbot
- Kéo bong bóng chat để đổi vị trí; bấm vẫn mở/thu nhỏ. Kéo thanh tiêu đề để di chuyển hộp chat độc lập; nút đóng/đặt lại và nội dung chat giữ thao tác bình thường.
- Pointer Events hỗ trợ chuột/cảm ứng; có ngưỡng phân biệt bấm và kéo, giữ trong màn hình và căn lại khi resize. Không lưu vị trí hoặc thay đổi API/DB.
- Edge headless kiểm tra chuột, cảm ứng, bấm mở/đóng, giới hạn màn hình và resize 1440→360 đạt trên trang QA tĩnh dùng đúng CSS/JS. Kết quả trong `data/chat-drag-qa`; chưa thử thiết bị cảm ứng thật. Không khởi động server.

### 18/10/2026 – Giao diện và quyền Admin theo phạm vi đã chốt
- Menu và tổng quan quản trị mới; đọc ma trận DB, bỏ đặc cách toàn quyền Admin, chặn ghi nghiệp vụ kể cả DB FULL cũ. Seed Admin READ ở tám module nghiệp vụ, FULL ở tài khoản. Không chạy updater/seed hoặc sửa DB hiện dùng.
- Nối danh sách hóa đơn, chỉ số lịch sử và báo hỏng vào dữ liệu thật; xem chi tiết tin đăng mọi trạng thái. Giữ nhật ký đầy đủ, chỉ đọc. Khóa được tài khoản các vai trò, không tự khóa; giữ gửi mật khẩu tạm và buộc đổi lần đầu.
- Build và năm kiểm thử trọng tâm đạt; Edge bản sao DB đạt trang/chi tiết và tổng quan desktop/360px. Nhóm rộng còn mười kiểm thử trang công khai với kỳ vọng redirect/nhãn cũ chưa khớp. Bàn giao: [docs/admin-access-20261018.md](docs/admin-access-20261018.md). Server QA đã dừng; chưa push.

### 18/10/2026 – Căn giãn dòng quản lý tài khoản
- Thu gọn padding bộ lọc/hàng bảng, khoảng cách nhóm thao tác; line-height 1.5, nhãn cách ô 6px. Giữ control 44px, chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap quản lý tài khoản
- Header/nút tạo, thẻ bộ lọc 44px, bảng hover, badge và nhóm thao tác inline; CSS managed-accounts-bootstrap riêng, responsive. Giữ form/CSRF/quyền và liên kết.
- Build và HTTP danh sách/lọc Admin đạt, CSRF giữ nguyên. Chưa nghiệm thu trực quan 360px/Safari; không thay đổi tài khoản.

### 18/10/2026 – Căn giãn dòng sidebar Admin
- Menu desktop 40px, thu khoảng cách mục/nhóm và brand; mobile giữ 44px. Chỉ CSS Admin.
- Diff và HTTP CSS đạt; chưa kiểm chứng trực quan.

### 18/10/2026 – Bootstrap sidebar Admin
- Căn icon/nhãn/badge theo cùng track, menu 42px (mobile 44px), active champagne/hover/focus rõ, brand và khoảng cách đồng bộ. Chỉ CSS Admin, giữ quyền và liên kết.
- Diff và HTTP CSS đạt; chưa kiểm chứng trực quan thiết bị thật.

### 18/10/2026 – Đồng bộ tiêu đề tổng quan Admin
- Header Trang chủ đổi thành Tổng quan cho Admin, kiểu chữ/cụm ba gạch đồng bộ chủ nhà. Không đổi tiêu đề trang công khai.
- Node syntax và diff đạt.

### 18/10/2026 – Bỏ icon home còn lại header chủ nhà
- Xử lý cả shortcut home ngoài nav và dấu phân cách, giữ nút ba gạch/tiêu đề.
- Node syntax và diff đạt.

### 18/10/2026 – Nút ba gạch trên header
- Thay icon home và dấu phân cách đầu breadcrumb bằng nút ba gạch hiện rõ trên desktop/mobile; dùng lại sidebar-toggle và logic ẩn/hiện/inert hiện có.
- Node syntax và diff đạt; chưa kiểm chứng tương tác trực quan.

### 18/10/2026 – Bỏ thẻ chào tài khoản sidebar
- Bỏ thẻ Xin chào/tên/vai trò trong sidebar theo ảnh yêu cầu; giữ menu tài khoản trên header.

### 18/10/2026 – Đồng bộ tài khoản Admin với chủ nhà
- Đồng bộ avatar 34px, typography, padding/viền/hover, mũi tên xuống và popover theo nút chủ nhà; giữ tên/vai trò Admin. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan.

### 18/10/2026 – Nút tài khoản Admin
- Nền sáng, avatar champagne, tên/vai trò và chevron có màu rõ; hover/focus/open đồng bộ Bootstrap, popover bo góc/bóng. CSS scoped header Admin.
- Diff và HTTP CSS đạt; chưa kiểm chứng tương tác trực quan.

### 18/10/2026 – Căn tìm kiếm cạnh avatar Admin
- Chuyển khoảng trống flex sang trước ô tìm kiếm, đưa tìm kiếm/chuông sát tài khoản ở tổng quan Admin; giữ tìm kiếm ẩn trên mobile và avatar bên phải.
- Diff và HTTP CSS đạt.

### 18/10/2026 – Căn giãn dòng tổng quan Admin
- Thu gọn khoảng đệm thẻ thống kê, bảng hoạt động và trạng thái; line-height nội dung 1.5, giữ khoảng cách dòng phụ 4px. Chỉ CSS Admin.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap tổng quan Admin
- CSS admin-dashboard-bootstrap chỉ nạp ADMIN: thống kê, bố cục vai trò/hoạt động/trạng thái, bảng hover/align-middle và spacing responsive. Giữ dữ liệu/quyền/liên kết.
- Build và HTTP admin đạt; owner không nạp CSS Admin. Chưa nghiệm thu trực quan 360px/Safari.

### 18/10/2026 – Dropdown Bootstrap trạng thái/mức độ báo hỏng
- Menu Bootstrap riêng chủ nhà cho trl-status/trl-priority, giữ select và change, đồng bộ reset; không ảnh hưởng giao diện khách thuê.
- Node syntax, diff và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Bỏ breadcrumb phụ báo hỏng chủ nhà
- Bỏ dòng Trang chủ › Báo hỏng tại tòa nhà của tôi trong nội dung chủ nhà; giữ header và điều hướng khách thuê.

### 18/10/2026 – Căn giãn dòng báo hỏng chủ nhà
- Thu gọn padding thống kê, toolbar, hàng bảng và empty state; đồng nhất line-height 1.5, giữ ô nhập/nút 44px. Chỉ CSS scoped chủ nhà.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap báo hỏng chủ nhà
- Form-control/form-select, nút Bootstrap và bảng hover; CSS owner-repairs-bootstrap chỉ nạp cho chủ nhà, chỉnh thống kê, bộ lọc 44px, empty state và khoảng cách responsive. Giữ quyền/hooks và dữ liệu.
- Build và HTTP chủ nhà đạt; CSS/table/filter/empty hooks đạt. Chưa nghiệm thu trực quan 360px/Safari hoặc danh sách có báo hỏng.

### 18/10/2026 – Dropdown Bootstrap đến tháng báo cáo
- Áp dụng dropdown cùng kiểu cho denThang, giữ giá trị GET và nhãn truy cập Đến tháng.
- Node syntax, diff và HTTP JS đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Dropdown Bootstrap tòa và từ tháng báo cáo
- Nâng toaNhaId/tuThang sang dropdown Bootstrap cuộn, dấu chọn và bóng nhẹ; giữ select/name để GET và fallback. Không tự gửi bộ lọc.
- Build, Node syntax và diff đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Căn giãn dòng báo cáo doanh thu
- Thu gọn padding bộ lọc/KPI/biểu đồ và hàng bảng; thống nhất line-height nội dung 1.5, khoảng cách dòng phụ 4–6px. Chỉ CSS, giữ ô nhập 44px.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap báo cáo doanh thu/lấp đầy
- Form-select/form-control, nút và bảng Bootstrap; CSS report-bootstrap riêng cho bộ lọc 44px, thẻ KPI, khung biểu đồ/bảng và khoảng cách responsive. Giữ công thức/dữ liệu và query bộ lọc.
- Build và HTTP báo cáo đạt; CSS/table/fields và tổng demo 7.370.000 đạt. Chưa nghiệm thu trực quan 360px/Safari; không đổi dữ liệu.

### 18/10/2026 – Căn giãn dòng chi tiết công nợ
- Thu gọn padding dialog, thông tin phòng, ghi chú và hàng bảng; thống nhất line-height 1.5, dòng phụ cách 4px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap chi tiết công nợ phòng
- Bảng Bootstrap hover/responsive; CSS dialog bo góc/bóng, nút đóng gọn, tổng nợ tách ô nổi bật, số tiền và dòng tổng canh phải; mobile xuống dòng. Giữ dữ liệu và liên kết.
- Build và HTTP phòng 6 đạt, số nợ giữ nguyên. Chưa nghiệm thu trực quan 360px/Safari; không thay đổi nghiệp vụ.

### 18/10/2026 – Cố định hàng ngang ô lọc công nợ
- Cố định track nhãn 18px + gap 6px + ô 44px cho mọi trường và dropdown; nút cùng chiều cao/canh đáy, menu vẫn absolute.
- Diff và HTTP CSS đạt; chưa kiểm chứng trực quan.

### 18/10/2026 – Đồng đều nhãn và control bộ lọc công nợ
- Dùng cùng track nhãn 18px/gap 6px cho label gốc và dropdown Bootstrap, bỏ margin control gây lệch baseline; khoảng cách cột 12px, hàng 18px, nút canh đáy.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan.

### 18/10/2026 – Căn thẳng hàng bộ lọc công nợ
- Đồng nhất nhãn 18px, cách ô 6px, control/nút 44px; bỏ padding lồng và khoảng đệm dư giữa hai hàng. Cho dropdown tràn ra ngoài thẻ để menu không bị cắt, responsive 3/1 cột.
- Diff và HTTP CSS đạt; chưa kiểm chứng trực quan.

### 18/10/2026 – Dropdown Bootstrap tòa nhà công nợ
- Áp dụng dropdown cùng kiểu cho toaNhaId, giữ giá trị GET và nút Áp dụng.
- Kiểm tra cú pháp JS, diff và HTTP asset đạt; chưa kiểm tra tương tác trực quan.

### 18/10/2026 – Dropdown Bootstrap bộ lọc công nợ
- Đổi trạng thái/sắp xếp/hiển thị sang dropdown Bootstrap, giữ select name và GET, hỗ trợ reset/change và fallback không JS. Menu bo góc/bóng/dấu chọn.
- Node kiểm tra cú pháp, diff và HTTP asset đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Căn giãn dòng công nợ
- Thu gọn khoảng đệm bộ lọc/thống kê/hàng bảng; thống nhất line-height 1.5, giữ ô nhập 44px và nghiệp vụ. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap công nợ
- CSS debt-bootstrap scoped: bộ lọc 44px, thống kê, bảng responsive/hover, tiền canh phải và nút/phân trang đồng bộ, responsive. Giữ fields/hooks và tính công nợ.
- Build và HTTP danh sách/bộ lọc đạt; CSS/table/filter/export/detail hooks đạt. Chưa nghiệm thu trực quan 360px/Safari; không đổi dữ liệu.

### 18/10/2026 – Căn giãn dòng hộp thoại ghi thu
- Thu gọn khoảng cách tiêu đề/thống kê/trường/người xác nhận/footer; line-height 1.5 và dòng phụ cách 4px. Giữ ô nhập/nút 44px, chỉ CSS scoped #ic-dialog.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap hộp thoại ghi thu tiền
- Form-control/form-select, ô 44px, thẻ tổng tiền, nút đóng, footer và CSS scoped #ic-dialog; bo góc/bóng/backdrop, responsive một cột. Giữ dialog, ID/hooks, CSRF và nghiệp vụ.
- Build và HTTP form/Bootstrap controls/CSRF đạt; chưa nghiệm thu trực quan mở dialog 360px/Safari. Không ghi thu.

### 18/10/2026 – Bỏ liên kết quay lại ở chi tiết thu tiền
- Bỏ liên kết Quay lại quản lý hóa đơn trên tiêu đề theo yêu cầu, giữ breadcrumb header.

### 18/10/2026 – Breadcrumb chi tiết hóa đơn
- Thêm Quản lý hóa đơn giữa trang chủ và tên phòng tại Details cho chủ nhà/admin; đồng bộ icon, kiểu chữ, màu, khoảng cách/mobile với chi tiết tòa.

### 18/10/2026 – Rút gọn tiêu đề điều hướng hóa đơn
- Bỏ tiền tố Chi tiết hóa đơn và dấu chấm giữa trên tiêu đề/breadcrumb chi tiết, giữ tên phòng; xem trước giữ nguyên.

### 18/10/2026 – Căn giãn dòng chi tiết hóa đơn
- Thu gọn padding thẻ, hàng khoản mục, thông tin và lịch sử trống; thống nhất line-height 1.5 và khoảng cách dòng phụ 4px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap chi tiết hóa đơn/thu tiền
- CSS invoice-collection-bootstrap scoped: tiêu đề dài, thống kê, bảng Bootstrap hover, tiến độ, thông tin và empty state; nút Bootstrap, responsive. Giữ ID/data hooks và nghiệp vụ thu tiền.
- Build và HTTP chi tiết đạt; CSS/table/form/CSRF hooks đạt. Chưa nghiệm thu trực quan 360px/Safari; không ghi thu.

### 18/10/2026 – Dropdown Bootstrap tầng/sắp xếp kiểm tra hóa đơn
- Nâng floor-filter/room-sort sang dropdown cùng kiểu tòa/tháng; giữ select ID và phát change để lọc/sắp xếp hiện có.
- Node kiểm tra cú pháp, diff và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Dropdown Bootstrap tòa/tháng kết quả kiểm tra
- Dùng lại dropdown building/month của trang Setup trên Monthly, CSS scoped review-bootstrap; giữ select/name/change để kiểm tra lại kỳ và cảnh báo dữ liệu thay đổi.
- Node kiểm tra cú pháp, diff và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan.

### 18/10/2026 – Bỏ điều hướng phụ trên kết quả kiểm tra
- Bỏ breadcrumb trong nội dung và liên kết Thay đổi thông tin kỳ theo yêu cầu; giữ breadcrumb trên header.

### 18/10/2026 – Rút gọn breadcrumb kết quả kiểm tra
- Bỏ kỳ tháng/năm khỏi mục hiện tại trên breadcrumb, giữ “Kết quả kiểm tra”; tiêu đề nội dung vẫn giữ kỳ để đối chiếu.

### 18/10/2026 – Thêm bước tạo hóa đơn trong breadcrumb
- Thêm liên kết Tạo hóa đơn giữa Quản lý hóa đơn và kết quả kỳ; giữ tòa/năm/tháng khi về Setup và kiểu hiển thị đồng bộ chi tiết tòa.

### 18/10/2026 – Breadcrumb kết quả kiểm tra kỳ
- Thêm Quản lý hóa đơn giữa trang chủ và tiêu đề kỳ; đồng bộ kiểu icon/chữ/khoảng cách và mobile với chi tiết tòa. Chỉ action Monthly.
- Build và HTTP breadcrumb/Management/current page đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Căn giãn dòng kết quả kiểm tra hóa đơn
- Thu gọn padding thẻ/hàng bảng, khoảng cách bộ lọc, thống kê và phần kết quả; thống nhất line-height 1.5 và dòng hướng dẫn 1.55. Giữ ô nhập/nút và nghiệp vụ, chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap kết quả kiểm tra kỳ hóa đơn
- CSS invoice-review-bootstrap riêng: bộ lọc 44px, thống kê/tổng tiền, bảng Bootstrap hover, tabs và thẻ kết quả; thanh xác nhận cuối nội dung không che danh sách; responsive. Giữ quyền, form, ID/hooks và tính tiền.
- Build và HTTP Monthly đạt; CSS/table/form/CSRF hooks đạt. Chưa nghiệm thu trực quan 360px/Safari; không tạo/phát hành hóa đơn.

### 18/10/2026 – Dropdown Bootstrap tòa/tháng tạo hóa đơn
- Nâng 2 select building/month sang dropdown Bootstrap bo góc, bóng nhẹ, cuộn, hover/dấu chọn; giữ select/name/change và fallback. Đồng bộ label, xử lý invalid đưa focus về nút.
- Node kiểm tra cú pháp, diff và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan/thiết bị thật. Không tạo hóa đơn.

### 18/10/2026 – Bỏ liên kết quay lại ở tạo hóa đơn
- Bỏ liên kết trên tiêu đề theo yêu cầu, giữ breadcrumb Quản lý hóa đơn.

### 18/10/2026 – Breadcrumb tạo hóa đơn tháng
- Thêm Quản lý hóa đơn giữa trang chủ và Tạo hóa đơn tháng; liên kết Management, đồng bộ icon/cỡ chữ/khoảng cách/màu và mobile với breadcrumb chi tiết tòa. Chỉ áp dụng action Setup.
- Build và HTTP breadcrumb/liên kết/current page đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Căn giãn dòng tạo hóa đơn tháng
- Thu gọn khoảng cách trường, thẻ thống kê, cách tính và bảng phòng bỏ qua; căn line-height 1.5, giữ ô nhập/nút 44px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 18/10/2026 – Bootstrap tạo hóa đơn tháng
- Thẻ Bootstrap, bảng responsive/hover và CSS invoice-setup-bootstrap riêng: form 44px, thống kê rõ, cảnh báo/bảng phòng bỏ qua, cách tính và nút đồng bộ. Căn line-height 1.5, mobile một cột.
- Build và HTTP Setup đạt; CSS/fields/form hook đạt. Chưa nghiệm thu trực quan 360px/Safari; không tạo hóa đơn.

### 18/10/2026 – Đổi nhãn nút hóa đơn
- Đổi nút mở xác nhận phát hành hàng loạt thành “Áp dụng” theo yêu cầu; giữ hành vi.

### 18/10/2026 – Dropdown Bootstrap trạng thái hóa đơn
- Áp dụng dropdown cùng kiểu tòa/tháng cho trạng thái; giữ select trangThai và nút Lọc.
- Node kiểm tra cú pháp, diff và HTTP JS đạt. Chưa kiểm chứng tương tác trực quan.

### 18/10/2026 – Dropdown Bootstrap tòa nhà/tháng hóa đơn
- Nâng 2 select bộ lọc sang dropdown Bootstrap có cuộn, hover, dấu chọn và menu bo góc/đổ bóng; giữ select/name để GET và fallback. Khởi tạo trước guard dialog để hoạt động ở tab chờ xác nhận; không tự gửi form.
- Node kiểm tra cú pháp, diff và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan trên trình duyệt/thiết bị thật.

### 18/10/2026 – Bootstrap và giãn dòng quản lý hóa đơn
- CSS invoice-management-bootstrap riêng: thống kê gọn, bộ lọc grid responsive, bảng hover/align-middle, mã dài xuống dòng, cột tiền canh phải, badge rõ và khoảng cách dòng 1.5. Giữ hooks, quyền, CSRF và nghiệp vụ.
- Build và HTTP danh sách/tab chờ xác nhận đạt, CSS/CSRF đạt; chưa nghiệm thu trực quan 360px/Safari. Không thay đổi hóa đơn.

### 18/10/2026 – Dropdown Bootstrap chọn kỳ tiến độ
- Nâng select kỳ thành Bootstrap dropdown có cuộn, bo góc, hover và dấu chọn; giữ select/name thang để GET/xuất CSV và fallback khi thiếu JS/Bootstrap. Không tự gửi form khi chọn, vẫn bấm Xem.
- Node kiểm tra cú pháp và HTTP JS/CSS đạt; chưa nghiệm thu tương tác trực quan/thiết bị thật.

### 18/10/2026 – Căn giãn dòng phòng còn thiếu chỉ số
- Thu gọn khoảng cách tiêu đề/cảnh báo/thẻ phòng; căn tên, quản lý, trạng thái, nhóm nút và hướng dẫn với line-height 1.5. Giữ nút 44px, chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap phòng còn thiếu chỉ số
- Thẻ Bootstrap, nhóm nút tách riêng trạng thái, nút 44px và CSS meter-missing-bootstrap cho bảng trượt/trang độc lập; căn nội dung, đóng và thông báo, responsive. Giữ liên kết phòng/chỉ số và dữ liệu.
- Build, HTTP standalone/drawer, 2 nhóm nút và liên kết chỉ số đạt; chưa nghiệm thu trực quan 360px/Safari. Không đổi dữ liệu nghiệp vụ.

### 18/10/2026 – Căn giãn dòng tiến độ chốt chỉ số
- Thu gọn padding thẻ thống kê, tiêu đề bảng và hàng; thống nhất line-height 1.5 và khoảng cách dòng phụ 2–4px, giữ nút/ô nhập 44px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 18/10/2026 – Bootstrap tiến độ chốt chỉ số
- Form-control/form-select, nút Bootstrap và bảng responsive/hover; CSS meter-progress-bootstrap riêng cho thống kê, cảnh báo, bộ lọc, bảng và khoảng cách. Giữ ID/data hooks lọc/xuất/phòng thiếu và logic nghiệp vụ.
- Build đạt; HTTP trang tiến độ và liên kết phòng thiếu đạt, CSS mới/filter/export hooks đạt. Chưa nghiệm thu trực quan 360px/Safari; không thay đổi nghiệp vụ.

### 17/10/2026 – Căn giãn dòng chỉ số điện nước
- Thu gọn thẻ thống kê, bộ lọc và khoảng đệm hàng phòng; căn nhãn/ô nhập, mô tả và lỗi; giữ chiều cao ô nhập và grid nghiệp vụ. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan thiết bị thật.

### 17/10/2026 – Bootstrap chỉ số điện nước
- CSS meter-bootstrap riêng: bộ lọc trong thẻ, thống kê/tiến độ rõ hơn, ô nhập 42–44px, nhãn/bảng/nút đồng bộ; Bootstrap form-control/form-select và nhóm nút. Giữ grid biểu mẫu, ID/data hooks, CSRF và khóa kỳ/lưu từng phòng.
- Build đạt; HTTP chủ nhà/quản lý đạt, CSS/form/input/bộ lọc/CSRF đúng. Chưa nghiệm thu trực quan 360px/Safari; không lưu chỉ số thử.

### 17/10/2026 – Căn giãn dòng gia hạn hợp đồng
- Thu gọn padding thẻ, khoảng cách tiêu đề/trường/nút; căn nhãn với ô nhập, dòng hướng dẫn cách 4px và line-height 1.5. Giữ ô nhập 44px, chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap gia hạn hợp đồng
- Thẻ biểu mẫu giới hạn 880px, tiêu đề/icon, mã hợp đồng, form-control 44px và nhóm lưu/hủy; CSS contract-renewal-bootstrap riêng, mobile một cột. Giữ trường, validation, CSRF và nghiệp vụ.
- Build và HTTP gia hạn đạt; CSS/field/CSRF đạt. Chưa nghiệm thu trực quan 360px/Safari; không gửi gia hạn vào DB.

### 17/10/2026 – Căn giãn dòng thông báo bảng tất toán
- Căn riêng padding thẻ và khoảng cách biểu tượng/tiêu đề/thông báo/nút, line-height 1.5; giữ nút 44px, không ảnh hưởng thông báo trả phòng.

### 17/10/2026 – Bootstrap thông báo bảng tất toán
- Dùng thẻ/alert/nút Bootstrap và CSS thông báo hiện có, thêm biểu tượng tài liệu, tiêu đề và hướng dẫn quay lại; giữ thông báo nghiệp vụ gốc. Khoảng cách gọn, responsive theo CSS dùng chung.
- Build và HTTP màn hình chưa có bảng tất toán đạt, liên kết quay lại đạt; không đổi dữ liệu. Chưa nghiệm thu trực quan thiết bị thật/Safari.

### 17/10/2026 – Căn giãn dòng thông báo trả phòng
- Thu gọn padding và khoảng cách biểu tượng/tiêu đề/thông báo/nút; line-height nội dung 1.5, giữ nút 44px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap thông báo ghi nhận trả phòng
- Thẻ Bootstrap có biểu tượng, tiêu đề, thông báo nghiệp vụ gốc và nút quay lại 44px; CSS riêng checkout-notice-bootstrap, responsive. Không đổi controller/dữ liệu.
- Build và HTTP màn hình thiếu chỉ số đạt, CSS/nút quay lại đạt; chưa nghiệm thu trực quan 360px/Safari. Không gửi thao tác trả phòng.

### 17/10/2026 – Căn giãn dòng yêu cầu trả phòng
- Thu gọn padding thẻ/bộ lọc/empty state, thống nhất line-height nội dung 1.5 và khoảng cách trường; giữ ô nhập/nút 44px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap yêu cầu trả phòng trước hạn
- CSS checkout-request-bootstrap riêng: tiêu đề, thông báo, bộ lọc và nút 44px, số yêu cầu, empty state có biểu tượng; căn thẻ yêu cầu/form và responsive 360px bằng CSS. Giữ quyền, query, validation và POST.
- Build đạt; HTTP chủ nhà/khách thuê cả danh sách và lọc hợp đồng đạt, CSS mới/empty state đạt. Chưa nghiệm thu trực quan 360px/Safari và trạng thái có yêu cầu; không gửi yêu cầu demo.

### 17/10/2026 – Căn giãn dòng tab người ở cùng
- Thu gọn khoảng cách danh sách, sức chứa, form thêm người và thẻ bên phải; nhãn cách ô 6px, dòng phụ 2–4px, khoảng cách trường 12px; giữ ô nhập 44px. Chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap tab người ở cùng
- CSS riêng contract-residents-bootstrap chỉ nạp ở tab người ở: sức chứa, danh sách, thẻ phòng, nhóm chọn cách thêm và form-control 44px. Căn nhãn/khoảng cách, responsive form một cột; giữ quyền, field, CSRF và xử lý nghiệp vụ.
- Build đạt; HTTP cả mode account/manual đạt, CSRF/form/CSS đúng; tổng quan không nạp CSS người ở. Chưa nghiệm thu trực quan 360px/Safari; không thêm người thử vào DB.

### 17/10/2026 – Bỏ liên kết quay lại trên chi tiết hợp đồng
- Bỏ liên kết đầu trang theo yêu cầu; vẫn dùng menu và breadcrumb để điều hướng.

### 17/10/2026 – Căn giãn dòng chi tiết hợp đồng
- Thu gọn hàng thông tin, thẻ tài chính/người ở; thống nhất line-height 1.5, khoảng cách dòng phụ 4px, giữa thẻ 16px. Đồng bộ lịch sử, tài liệu và điều khoản; chỉ CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap chi tiết hợp đồng
- Thanh nút nghiệp vụ riêng dưới tiêu đề; Bootstrap tabs, bảng responsive/hover; CSS contract-detail-bootstrap căn thẻ thông tin, tài chính 2 cột, người ở và khoảng cách đồng nhất. Mobile xuống một cột; giữ điều kiện quyền, liên kết, dữ liệu và thao tác.
- Build đạt; đăng nhập chủ nhà, tổng quan và tab người ở HTTP 200, CSS mới đạt. Chưa nghiệm thu trực quan 360px/Safari; không gửi thay đổi nghiệp vụ.

### 17/10/2026 – Căn giãn dòng trang lập hợp đồng
- Đồng nhất khoảng cách nhãn/ô nhập 6px, giữa trường 12px và giữa thẻ 16px; thu gọn dòng tóm tắt, hướng dẫn và thanh nút. Giữ chiều cao ô nhập 44px, chỉ đổi CSS.
- Diff và HTTP CSS đạt; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap trang lập hợp đồng
- Dùng form-control/form-select và bảng Bootstrap; CSS riêng contract-create-bootstrap với nhãn phía trên, ô nhập 44px, thẻ bo 12px, màu charcoal/gold, tóm tắt và thanh nút cuối biểu mẫu không che nội dung. Responsive một cột ở màn hình nhỏ, giữ ID/validation/CSRF/nghiệp vụ.
- Build đạt; HTTP trang Create trống, CSS Bootstrap và CSRF đạt. Demo không có yêu cầu đủ điều kiện trong danh sách; yêu cầu 7 trả 403 nên chưa kiểm chứng trạng thái đã chọn. Chưa nghiệm thu trực quan 360px/Safari và chưa gửi tạo hợp đồng.

### 17/10/2026 – Căn giãn dòng danh sách hợp đồng
- Căn line-height tiêu đề/nội dung, giảm padding hàng và thẻ; thống nhất khoảng cách khối 16px, nhãn và mô tả 4–6px. Bỏ gutter dọc Bootstrap dư trong thẻ thống kê. Chỉ đổi CSS.
- Kiểm tra diff và CSS phục vụ HTTP; chưa nghiệm thu trực quan trên thiết bị thật.

### 17/10/2026 – Bootstrap danh sách hợp đồng
- Đưa nút trả phòng/tất toán vào thanh nghiệp vụ dưới tiêu đề; dùng Bootstrap grid, form-select, form-control, table-hover và CSS riêng contracts-bootstrap cho trang danh sách. Giữ quyền, ID bộ lọc và liên kết nghiệp vụ.
- Build đạt; HTTP đăng nhập chủ nhà và danh sách đạt, tải CSS mới, 2 hợp đồng demo. Chưa nghiệm thu trực quan trên thiết bị 360px/Safari. Không thay đổi CSDL.

### 17/10/2026 – Căn giãn dòng Chi tiết yêu cầu thuê
- Thu khoảng đệm thẻ/tiến trình, dòng thông tin khách/phòng, lời nhắn và form lịch hẹn; chỉ CSS, giữ các thao tác nghiệp vụ.

### 17/10/2026 – Làm mới Chi tiết yêu cầu thuê bằng Bootstrap
- Đồng bộ tiến trình, thẻ khách/phòng, form lịch hẹn 44px, tóm tắt và nút thao tác; CSS riêng request-detail-bootstrap chỉ cho chủ nhà, giữ quyền và POST/CSRF.
- Build đạt; Edge 360/768/1440px không tràn ngang, tiến trình/khách/form/CSRF/quay lại đạt. Không xác nhận lịch hay đổi yêu cầu; chưa push.

### 17/10/2026 – Căn giãn dòng Yêu cầu thuê & lịch xem
- Thu khoảng đệm thống kê, tab/bộ lọc, dòng bảng và khung chi tiết/liên hệ; chỉ CSS, giữ các thao tác xử lý yêu cầu.

### 17/10/2026 – Làm mới Yêu cầu thuê & lịch xem bằng Bootstrap
- Đồng bộ thống kê, tab/bộ lọc, bảng cuộn responsive và panel chi tiết; CSS requests-bootstrap chỉ cho workspace, giữ quyền/luồng duyệt và trạng thái.
- Build đạt; Edge 360/768/1440px không tràn ngang, thống kê/tìm kiếm/panel/xuất danh sách đạt. Không duyệt hay đổi yêu cầu thử; chưa push.

### 17/10/2026 – Căn giãn dòng Tạo tin đăng
- Thu padding thẻ/nhóm ô chọn, khoảng cách nhãn/trường nhập, thông báo, xem trước và footer; chỉ áp dụng create-mode, giữ logic form.

### 17/10/2026 – Tinh chỉnh Tạo tin đăng Bootstrap
- Thêm scope create-mode, nhấn thẻ thông tin, nhóm ô chọn/phòng và thông số nền nhẹ, footer lưu và tiện nghi đồng bộ.
- Build và Edge 360/768/1440px đạt, không tràn ngang; xem trước tiêu đề/mô tả và Hủy/CSRF đạt. Không tạo tin thử; chưa push.

### 17/10/2026 – Căn giãn dòng form tin đăng
- Thu khoảng đệm thẻ, nhãn/ô nhập, nhóm thông tin phòng, dịch vụ và footer; chỉ CSS, giữ logic lưu và xem trước.

### 17/10/2026 – Làm mới form Chỉnh sửa tin đăng bằng Bootstrap
- Đồng bộ thẻ/form 44px, thông số, ảnh, xem trước/liên hệ/hiển thị và nút thao tác; stylesheet listing-form-bootstrap dùng chung form tạo/sửa, giữ logic POST/quyền.
- Build đạt; Edge form sửa 360/768/1440px không tràn ngang, cập nhật xem trước tiêu đề/mô tả, CSRF và Hủy đạt. Không lưu nội dung thử; chưa push.

### 17/10/2026 – Đổi nhãn thao tác Đăng tin
- Đổi Tin đăng công khai thành Đăng tin trong menu trạng thái, giữ nguyên xử lý.

### 17/10/2026 – Căn giãn dòng Tin đăng cho thuê
- Thu khoảng đệm thống kê, tab, bộ lọc, dòng bảng và phân trang; chỉ CSS, giữ tìm/lọc và trạng thái tin.

### 17/10/2026 – Làm mới Tin đăng cho thuê bằng Bootstrap
- Đồng bộ thống kê, thông báo, tab/dropdown 42px, bảng/badge/menu thao tác và phân trang; CSS riêng listing-manage-bootstrap, giữ quyền và trạng thái tin.
- Build đạt; Edge 360/768/1440px không tràn ngang, thống kê/tìm kiếm/lọc trạng thái/sắp xếp đạt. Không đổi dữ liệu tin; chưa push.

### 17/10/2026 – Căn giãn dòng Sửa đơn giá
- Thu khoảng đệm thẻ, dòng bảng, nhãn/ô nhập, phạm vi và tóm tắt; chỉ CSS, giữ logic đơn giá và hiệu lực.

### 17/10/2026 – Làm mới Sửa đơn giá bằng Bootstrap
- Đồng bộ bảng mức giá, thẻ/form 44px, phạm vi, tóm tắt, lịch sử và nút lưu/hủy; CSS riêng service-price-bootstrap, giữ logic hiệu lực/phiên bản/quyền và POST.
- Build đạt; Edge 360/768/1440px không tràn ngang, xem trước mức giá/kỳ áp dụng, CSRF và Hủy đạt. Không lưu giá thử; chưa push.

### 17/10/2026 – Căn giãn dòng Thêm dịch vụ
- Thu khoảng đệm thẻ, nhãn/ô nhập, lựa chọn cách tính, dòng tóm tắt và footer; chỉ CSS, giữ logic form.

### 17/10/2026 – Làm mới Thêm dịch vụ bằng Bootstrap
- Đồng bộ form-control/form-select 44px, thẻ, cách tính phí, phạm vi, hiệu lực, tóm tắt và nút lưu/hủy; CSS riêng service-create-bootstrap, giữ validation/quyền/POST.
- Build đạt; Edge 360/768/1440px không tràn ngang, xem trước tên/đơn giá/cách tính, CSRF và Hủy đạt. Không lưu dịch vụ thử; chưa push.

### 17/10/2026 – Bỏ nút Cấu hình điện nước
- Bỏ nút Cấu hình điện nước trong header Dịch vụ & đơn giá theo yêu cầu.

### 17/10/2026 – Căn giãn dòng Dịch vụ & đơn giá
- Thu khoảng đệm thẻ, tab, bộ lọc, dòng bảng và trạng thái rỗng; chỉ CSS, giữ chức năng và đơn giá.

### 17/10/2026 – Làm mới Dịch vụ & đơn giá bằng Bootstrap
- Đồng bộ thống kê, nút thao tác, tab/bộ lọc 42px, bảng dịch vụ và thẻ giải thích; CSS riêng service-bootstrap, giữ logic giá/quyền và chức năng chưa hỗ trợ.
- Build đạt; Edge 360/768/1440px không tràn ngang, thống kê, lọc cách tính và sắp xếp trên tòa đã có dịch vụ đạt. Không đổi đơn giá; chưa push.

### 17/10/2026 – Bỏ liên kết quay lại trên chi tiết hồ sơ
- Bỏ Quay lại danh sách theo yêu cầu; giữ breadcrumb điều hướng.

### 17/10/2026 – Căn giãn dòng Chi tiết hồ sơ khách thuê
- Thu padding thẻ, heading, dòng thông tin cá nhân/thuê và bảng hợp đồng; chỉ CSS, giữ quyền và dữ liệu.

### 17/10/2026 – Làm mới Chi tiết hồ sơ khách thuê bằng Bootstrap
- Đồng bộ banner/avatar/badge, dòng thông tin cá nhân, giấy tờ, thuê hiện tại/quyền truy cập và bảng hợp đồng; CSS riêng profile-detail-bootstrap, giữ quyền/che dữ liệu và endpoint ảnh giấy tờ.
- Build đạt; Edge 360/768/1440px không tràn ngang, 5 thông tin cá nhân, giấy tờ/bảng hợp đồng và link quay lại đạt. Chỉ đọc dữ liệu; chưa push.

### 17/10/2026 – Dropdown Bootstrap bộ lọc hồ sơ khách thuê
- Hai select tòa nhà/tình trạng được nâng cấp thành dropdown Bootstrap 44px, mũi tên và mục active champagne; giữ select gốc gửi POST, nhãn truy cập và CSRF.
- Build và Edge 360/768/1440px đạt; chọn hai dropdown/gửi lọc đúng, tìm kiếm và liên kết hồ sơ đạt. Không sửa dữ liệu; chưa push.

### 17/10/2026 – Đồng bộ breadcrumb Hồ sơ khách thuê
- Thay ký tự nhà bằng SVG nét 21px giống Tổng quan; thống nhất chữ 1rem, dấu / và gap, giữ liên kết điều hướng.

### 17/10/2026 – Căn giãn dòng Hồ sơ khách thuê
- Thu khoảng đệm thống kê, bộ lọc, dòng bảng và phân trang; căn gọn dòng phụ, giữ chức năng tra cứu và dữ liệu.

### 17/10/2026 – Làm mới Hồ sơ khách thuê bằng Bootstrap
- Đồng bộ 4 thẻ thống kê, bộ lọc 44px, bảng responsive/badge/phân trang và nút xem hồ sơ; CSS riêng tenant-profiles-bootstrap. Giữ quyền, che thông tin cá nhân và POST lọc/CSRF.
- Build đạt; Edge 360/768/1440px không tràn ngang, thống kê, tìm kiếm rỗng, lọc tòa/trạng thái, CSRF và link hồ sơ đạt. Chỉ đọc dữ liệu; chưa push.

### 17/10/2026 – Căn giãn dòng Lịch sử cư trú
- Thu khoảng đệm thống kê, bộ lọc, tab, dòng bảng và trạng thái rỗng; chỉ CSS, giữ chức năng tra cứu.

### 17/10/2026 – Làm mới Lịch sử cư trú bằng Bootstrap
- Đồng bộ thống kê, tìm kiếm/bộ lọc 42px, bảng/badge/trạng thái rỗng và nút xuất; CSS riêng occupancy-bootstrap responsive, giữ quyền và dữ liệu lịch sử.
- Build đạt; Edge 360/768/1440px không tràn ngang, 3 thống kê, tìm/lọc trạng thái, áp dụng ngày và CSV đạt. Kiểm tra chỉ đọc; chưa push.

### 17/10/2026 – Căn giãn dòng Nhật ký phòng
- Thu khoảng đệm thẻ, tab, bộ lọc, timeline, hồ sơ liên quan và form ghi chú; chỉ CSS, giữ chức năng và dữ liệu.

### 17/10/2026 – Làm mới Nhật ký phòng bằng Bootstrap
- Đồng bộ thẻ tóm tắt, tab/bộ lọc 42px, timeline, form ghi chú và hồ sơ liên quan; CSS riêng room-journal-bootstrap, giữ audit/POST/CSRF và xuất CSV.
- Build đạt; Edge 360/768/1440px không tràn ngang, tìm/lọc sự kiện/ngày, reset ghi chú, CSRF và export đạt. Không lưu ghi chú thử; chưa push.

### 17/10/2026 – Căn giãn dòng Chỉnh sửa phòng
- Thu khoảng đệm thẻ/cảnh báo, nhãn/ô nhập và dòng dịch vụ; chỉ CSS, giữ các thao tác và dữ liệu.

### 17/10/2026 – Làm mới Chỉnh sửa phòng bằng Bootstrap
- Đồng bộ form-control/form-select, thẻ/icon, cảnh báo hợp đồng, trạng thái, tiện nghi, ảnh và nút lưu/hủy; CSS riêng room-edit-bootstrap chỉ nạp khi sửa. Giữ POST, quyền và lưu dịch vụ/ảnh riêng.
- Build đạt; Edge 360/768/1440px không tràn ngang, mã phòng hiện tại, định dạng VND, state picker/Escape, CSRF và Hủy đạt. Không lưu phòng/dịch vụ/ảnh thử; chưa push.

### 17/10/2026 – Căn giãn dòng Chi tiết phòng
- Thu khoảng đệm thẻ, tab, dòng thông tin cơ bản/hợp đồng/chi phí và footer; chỉ CSS, giữ dữ liệu và liên kết.

### 17/10/2026 – Làm mới Chi tiết phòng bằng Bootstrap
- Đồng bộ heading/tab, thẻ thông tin/ảnh/hợp đồng/chi phí/ghi chú, badge và nút sửa; CSS riêng room-detail-bootstrap responsive. Giữ dữ liệu, quyền và ghi chú các chức năng chưa kết nối.
- Build đạt; Edge 360/768/1440px không tràn ngang, 6 thông tin, ảnh/liên kết mở ảnh, lịch sử/chỉnh sửa và trạng thái nút chưa hỗ trợ đạt. Kiểm tra chỉ đọc; chưa push.

### 17/10/2026 – Căn giãn dòng Tạo nhanh phòng
- Thu khoảng đệm thẻ, nhãn/trường nhập, hướng dẫn, trạng thái, dòng xem trước và footer; giữ logic tạo loạt.

### 17/10/2026 – Làm mới Tạo nhanh phòng bằng Bootstrap
- Đồng bộ thẻ, form-control/form-select 44px, thông tin bổ sung, trạng thái, khung xem trước và nút thao tác; CSS riêng room-bulk-bootstrap, giữ logic tạo loạt/validation/CSRF.
- Build đạt; Edge 360/768/1440px không tràn ngang, xem trước 15 phòng/mã đến 305/giá/sức chứa/trạng thái, mở bổ sung và Hủy đạt. Không tạo loạt thử; chưa push.

### 17/10/2026 – Căn giãn dòng Thêm phòng mới
- Thu padding thẻ, khoảng cách nhãn/ô nhập/tiện nghi, vùng ảnh và footer; giữ chức năng form.

### 17/10/2026 – Làm mới Thêm phòng bằng Bootstrap
- Form-control/form-select, thẻ và icon, trạng thái, tiện nghi có viền chọn, vùng tải ảnh, ghi chú và nút lưu/hủy đồng bộ; stylesheet room-create-bootstrap chỉ nạp khi tạo phòng, giữ logic form.
- Build đạt; Edge 360/768/1440px không tràn ngang; chọn tiện nghi, đếm ghi chú, định dạng VND, CSRF và Hủy đạt. Không tạo phòng thử; chưa push.

### 17/10/2026 – Căn giãn dòng Quản lý phòng
- Thu khoảng đệm thống kê, tab, toolbar, dòng bảng và phân trang; căn gọn dòng phụ, giữ ô lọc 42px và các thao tác.

### 17/10/2026 – Làm mới Quản lý phòng bằng Bootstrap
- Đồng bộ thống kê, tab, toolbar 42px, bảng/trạng thái/phân trang và nút thao tác; CSS riêng room-management-bootstrap, giữ logic tìm/lọc/export/quyền.
- Build đạt; Edge 360/768/1440px không tràn ngang, dialog thêm phòng, list/grid, phân trang, sắp xếp, CSV, tìm kiếm, lọc tòa/trạng thái đạt. Không sửa dữ liệu; chưa push.

### 17/10/2026 – Căn giãn dòng Cấu hình điện nước
- Thu padding thẻ và khoảng cách heading/tab/nhãn/nhóm cách tính/ghi chú/tóm tắt, giữ ô nhập 44px và logic form.

### 17/10/2026 – Làm mới Cấu hình điện nước bằng Bootstrap
- Đồng bộ thẻ, tab, lựa chọn cách tính 2 cột, ô nhập 44px, phạm vi, hiệu lực, tóm tắt và ví dụ; CSS riêng utility-bootstrap responsive, giữ cách tính/validation/POST/CSRF hiện có.
- Build đạt; Edge 360/768/1440px không tràn ngang, chuyển đồng hồ/người và bật-tắt input, tổng minh họa 425.000/275.000/240.000 đ, Hủy và CSRF đạt, không lỗi JS. Không lưu cấu hình thử; chưa push.

### 17/10/2026 – Căn giãn dòng Chỉnh sửa tòa nhà
- Thu padding thẻ, khoảng cách heading/tab/nhãn/trường nhập, tiện ích và footer; chỉ CSS, giữ chiều cao ô nhập và chức năng.

### 17/10/2026 – Làm mới Chỉnh sửa tòa nhà bằng Bootstrap
- Đồng bộ thẻ thông tin, tab, form-control/form-select 44px, tiện ích, khung ảnh/tóm tắt và nút lưu/hủy; CSS riêng building-edit-bootstrap responsive. Giữ validation, CSRF, quyền và POST hiện có.
- Build đạt; Edge 360/768/1440px không tràn ngang, dữ liệu ban đầu/xem trước địa chỉ/6 tiện ích/tab/Hủy đạt. Không lưu dữ liệu thử. Đã xem ảnh desktop; chưa push.

### 17/10/2026 – Đồng bộ breadcrumb Chỉnh sửa tòa nhà
- Đổi các dấu › thành / giống Chi tiết tòa nhà; giữ icon, tên tòa và liên kết điều hướng.

### 17/10/2026 – Rút gọn nhãn lọc thanh toán
- Đổi nhãn mặc định thành Thanh toán theo yêu cầu; vẫn lọc tháng hiện tại.

### 17/10/2026 – Triển khai lọc thanh toán và sắp xếp phòng
- Chi tiết tòa nhà lọc hóa đơn đã phát hành trong tháng Việt Nam hiện tại: chưa có/chưa trả/một phần/đã trả/quá hạn/chờ xác nhận. Số dư trừ thu đã xác nhận và cấn trừ cọc; không đổi dữ liệu/schema. Trạng thái ưu tiên đã trả, quá hạn, chờ xác nhận, một phần, chưa trả.
- Sắp xếp mã tăng/giảm, giá tăng/giảm, tầng tăng trước phân trang; giữ query qua tab và phân trang. Hiển thị trạng thái/số còn nợ theo phòng. Quản lý không thấy tài chính, query giả trả 403.
- Build đạt, 6 test trạng thái/số dư đạt; Edge 360/768/1440px, lọc/sắp xếp/phân trang và quyền quản lý đạt. Demo chạy lại; chưa push.

### 17/10/2026 – Đồng bộ breadcrumb hồ sơ với Tổng quan
- Bỏ nền/viền biểu tượng nhà, dùng icon nét 21px màu xám, dấu / và chữ 1rem cùng kiểu Tổng quan; giữ liên kết và responsive.

### 17/10/2026 – Đồng bộ dấu breadcrumb Chi tiết tòa nhà
- Đổi dấu › thành / giữa Quản lý tòa nhà và Chi tiết tòa nhà, thống nhất các dấu phân cách.

### 17/10/2026 – Căn giãn dòng Chi tiết tòa nhà
- Thu khoảng cách tiêu đề, thẻ thông tin/thống kê, tab, toolbar và hàng bảng phòng; đồng bộ khoảng cách dòng phụ. Chỉ CSS, giữ dữ liệu và chức năng.

### 17/10/2026 – Làm mới Chi tiết tòa nhà bằng Bootstrap
- Đồng bộ thẻ thông tin/thống kê, progress, trạng thái, toolbar lọc 42px, tab, bảng và phân trang; CSS riêng building-detail-bootstrap, giữ quyền và logic dữ liệu.
- Build đạt; Edge 360/768/1440px không tràn ngang, thống kê/tìm kiếm rỗng/lọc tầng/danh sách-lưới/link chỉnh sửa đạt. Kiểm tra chỉ đọc, chưa push.

### 17/10/2026 – Căn giãn dòng Thêm tòa nhà
- Thu padding thẻ, khoảng cách heading/trường nhập và lựa chọn loại hình; đồng bộ line-height mô tả, nhãn và hướng dẫn. Chỉ CSS, giữ chức năng form.

### 17/10/2026 – Làm mới Thêm tòa nhà bằng Bootstrap
- Đồng bộ thẻ thông tin, ô nhập 44px, lựa chọn loại hình, khung tóm tắt và nút thao tác; căn khoảng cách và bố cục responsive, giữ validation và logic lưu.
- Build đạt; Edge 360/768/1440px không tràn ngang, xem trước tên/số tầng/địa chỉ/loại hình, CSRF và Hủy đạt. Không lưu tòa nhà thử. Đã xem ảnh desktop; chưa push.

### 17/10/2026 – Căn hàng tìm kiếm/lọc tòa nhà
- Ô tìm và dropdown khu vực/tình trạng cùng 42px, icon/chữ căn giữa và gap đồng đều; mobile ô tìm full-width, hai lọc co đều. Chỉ CSS.

### 17/10/2026 – Thu giãn cách quản lý tòa nhà
- Đồng bộ khoảng cách thẻ thống kê, panel, toolbar và padding dòng bảng; giữ nội dung và chức năng. Chỉ CSS building-bootstrap.

### 17/10/2026 – Làm mới quản lý tòa nhà Bootstrap
- Thẻ thống kê/tổng quan/việc chú ý, toolbar Bootstrap và bảng responsive gọn đồng bộ; CSS giới hạn building-bootstrap. Giữ tìm kiếm, lọc, danh sách/lưới, sắp xếp/tháng và quyền thao tác.
- Build đạt; Edge owner 360/768/1440px không tràn ngang, 4 stats/search/empty/grid-list/sort/month/add link đạt; không sửa dữ liệu. Đã xem ảnh data/building-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Thu giãn dòng sidebar
- Giảm khoảng cách nhóm menu, heading, profile/logo và padding từng link; desktop link 36px, mobile 40px. Chỉ CSS sidebar, giữ quyền và điều hướng.

### 17/10/2026 – Làm mới sidebar workspace bằng Bootstrap
- Bootstrap nav/nav-link với icon, khoảng cách và nhóm menu đồng đều; profile, logo và trạng thái active champagne, badge gọn. CSS riêng bootstrap-sidebar, không đổi lọc quyền/MenuAsync hay tenant drawer.
- Build đạt; Edge owner desktop/mobile giữ menu/badge/active, điều hướng tòa nhà, mở sidebar/đóng Escape đạt, không tràn ngang. Đã xem ảnh data/sidebar-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Đổi dấu phân cách breadcrumb hồ sơ
- Đổi dấu › thành / theo ảnh; giữ bố cục còn lại.

### 17/10/2026 – Breadcrumb Bootstrap Hồ sơ của tôi
- Thay ký tự nhà bằng SVG trong nút nền nhạt, breadcrumb Bootstrap với dấu › và active rõ, căn giữa dọc/giảm gap mobile. Chỉ áp dụng HoSo/CuaToi.
- Build đạt; Edge 360/768/1440px không tràn ngang, trang hồ sơ và liên kết bảo mật vẫn hoạt động. Demo đã chạy lại.

### 17/10/2026 – Thu khoảng cách khối nhận chuyển khoản
- Căn gọn heading/mô tả, trường ngân hàng/số tài khoản, hướng dẫn QR và nhóm nút; ẩn dòng lỗi rỗng để không chiếm khoảng trắng. Chỉ CSS bank-settings, không sửa dữ liệu hay logic lưu.

### 17/10/2026 – Thu giãn dòng hồ sơ cá nhân
- Giảm khoảng đệm thẻ và dòng tài khoản, căn đều heading, avatar và bảo mật; chỉ CSS personal-bootstrap, không đổi dữ liệu.

### 17/10/2026 – Làm mới Hồ sơ của tôi bằng Bootstrap
- Grid profile/bảo mật và thông tin tài khoản, thẻ Bootstrap, avatar viết hoa và badge xác thực, dòng thông tin compact responsive. Giữ dữ liệu model, đổi mật khẩu, logout POST/CSRF và khối ngân hàng hiện có.
- Build đạt; Edge owner 360/768/1440px không tràn ngang, 6 dòng facts + ID header, bank section, CSRF logout và link đổi mật khẩu đạt. Không sửa dữ liệu tài khoản/ngân hàng. Đã xem ảnh data/personal-profile-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Làm mới tổng quan chủ nhà Bootstrap
- Cân heading/bộ lọc, ba thẻ chỉ số, thu tiền/tình trạng phòng và thẻ việc cần xử lý, dùng nút Bootstrap và spacing/type đồng bộ. CSS chỉ nạp cho Home chủ nhà và scope owner-bootstrap; không thay dữ liệu/logic tài chính.
- Build đạt; Edge owner 360/768/1440px không tràn ngang, đủ 3 chỉ số; chọn khoảng biểu đồ 3 tháng, cơ sở và tháng đạt; liên kết tạo hóa đơn còn nguyên. Không tạo/sửa nghiệp vụ. Đã xem ảnh data/owner-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Làm mới Đổi mật khẩu bằng Bootstrap
- Đồng bộ thẻ trắng, nhãn bảo mật và ba ô mật khẩu với nhóm auth; spacing gọn, nút rõ. Giữ trường, validation, CSRF và quy trình đổi mật khẩu.
- Build đạt; Edge sau đăng nhập tenant3 tại 360/768/1440px không tràn ngang; ba trường required và hiện/ẩn độc lập đạt. Không gửi đổi mật khẩu. Đã xem ảnh data/change-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Thu giãn cách dòng form Xác nhận email
- Căn đều và giảm khoảng cách biểu tượng/tiêu đề/mô tả, email/mã và hai nút; chỉ đổi CSS confirm-bootstrap, giữ nghiệp vụ.

### 17/10/2026 – Làm mới Xác nhận email bằng Bootstrap
- Đồng bộ form với Login/Register/Forgot: thẻ trắng, biểu tượng email, mã 6 số dễ đọc, nút xác nhận/gửi lại; chi tiết đường dẫn email thử nghiệm gói trong disclosure. Giữ thông báo và endpoint nghiệp vụ.
- Build đạt; Edge 360/768/1440px không tràn ngang, required/6-digit pattern/CSRF/resend formnovalidate/link Login đạt; không xác nhận mã hay gửi email. Đã xem ảnh data/confirm-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Căn giãn dòng form Quên mật khẩu
- Thu padding thẻ, khoảng cách biểu tượng/nhãn/tiêu đề/mô tả/email/nút và quay lại. Chỉ CSS riêng forgot-bootstrap; giữ chức năng.

### 17/10/2026 – Làm mới Quên mật khẩu bằng Bootstrap
- Đồng bộ thẻ trắng với Login/Register, thêm biểu tượng khôi phục, nhãn, email và nút rõ ràng, spacing gọn. Chỉ sửa giao diện, giữ logic gửi/CSRF và thông báo.
- Build đạt; Edge 360/768/1440px không tràn ngang, required/email validation và liên kết Login đạt. Không gửi email reset. Đã xem ảnh data/forgot-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Thu giãn cách dòng form đăng ký
- Giảm khoảng cách tiêu đề/mô tả, các trường, hướng dẫn mật khẩu và footer form; input/nút 44px. Chỉ CSS riêng register-page.

### 17/10/2026 – Làm mới Đăng ký bằng Bootstrap
- Đồng bộ với Login: thẻ trắng, nhãn chào, form/nút và khoảng cách gọn, dùng chung login-bootstrap.css với override riêng register-page. Giữ các trường và logic đăng ký.
- Build đạt; Edge 360/768/1440px không tràn ngang, required/phone pattern, password toggle, CSRF và liên kết Login đạt; không tạo tài khoản. Đã xem ảnh data/register-bootstrap-1440.png; demo đã chạy lại, chưa push.

### 17/10/2026 – Làm mới đăng nhập bằng Bootstrap
- Cân ảnh/form, thẻ trắng Bootstrap, nhãn chào, ô nhập/checkbox/nút đồng bộ và mobile gọn. CSS riêng chỉ nạp ở Login qua section Styles của LoginLayout; giữ logic đăng nhập/CSRF/khóa tài khoản/token.
- Build đạt; Edge 360/768/1440px không tràn ngang, báo trống form và hiện/ẩn mật khẩu đạt, đăng nhập tenant3 demo thành công. Đã xem ảnh data/login-bootstrap-1440.png và data/login-bootstrap-360.png. Demo đã chạy lại, chưa push.

### 17/10/2026 – Bỏ breadcrumb chi tiết tin
- Gỡ riêng dòng Trang chủ / Tìm phòng / Chi tiết tin. Build/HTTP đạt; demo đã chạy lại.

### 17/10/2026 – Bỏ footer riêng trang chi tiết phòng
- Không render footer công khai ở TinDang/ChiTiet theo ảnh; giữ footer các trang khác. Build/HTTP đạt, kiểm tra trang chủ vẫn có footer. Demo đã chạy lại.

### 17/10/2026 – Làm mới trang chi tiết phòng bằng Bootstrap
- Cân cột ảnh/nội dung và cột giá, làm rõ giá thuê/cọc/chi phí, thông tin phòng và chủ nhà. Bootstrap button/table-responsive; CSS giới hạn ld-page. Breadcrumb về TimTin, giữ lưu/chia sẻ/gallery và form nghiệp vụ.
- Build đạt; Edge 360/768/1440px không tràn ngang, giá mẫu đúng, lưu tin và gallery mở/đóng đạt; form tenant3 ở 360px vẫn có giới hạn 3 người, không gửi yêu cầu. Đã xem ảnh data/listing-detail-1440.png; ảnh public/mobile/tenant cùng thư mục. Demo đã chạy lại; chưa push.

### 17/10/2026 – Làm mới trang Giới thiệu bằng Bootstrap
- Banner chia chữ/ảnh, thẻ sứ mệnh/chỉ số, 3 cam kết và quy trình 4 bước dùng Bootstrap grid/card/button. Thay CTA tìm tin trùng bằng liên kết hướng dẫn trong trang, CSS giới hạn gioithieu-page; giữ header/footer chung.
- Build đạt; Edge 360/768/1440px không tràn ngang; liên kết hướng dẫn và tìm phòng hoạt động, 3 cam kết/4 bước đủ. Đã xem ảnh data/about-bootstrap-1440.png, ảnh mobile/tablet cùng thư mục. Demo đã chạy lại; chưa push lần chỉnh này.

### 17/10/2026 – Thu giãn dòng thẻ phòng trang chủ và footer
- Giảm khoảng cách tiêu đề/giá/khu vực/metadata, gutter hàng thẻ và padding section; thu dòng liên kết/liên hệ cùng padding bản quyền footer công khai.
- Edge 360/768/1440px không tràn ngang; form tìm kiếm vẫn đạt. Chỉ đổi CSS, demo có hiệu lực ngay.

### 17/10/2026 – Giảm khoảng trắng cuối trang Tìm phòng
- Bỏ padding đáy cộng dồn của public-main-timtin, giảm khoảng đệm cuối danh sách còn 20px và khoảng cách phân trang. Không đổi nội dung.

### 17/10/2026 – Thu khoảng cách dọc header công khai
- Header desktop 68px, logo 40px, mục menu và hai nút 40px; mobile giảm khoảng cách hai hàng. Căn giữa các nhóm, giữ chiều rộng hai mép.

### 17/10/2026 – Căn giãn dòng thông tin thẻ phòng
- Thu khoảng cách dòng giá/tiêu đề/địa chỉ/diện tích/footer, giảm gap giữa các thẻ và hàng kết quả. Giữ chiều cao tiêu đề hai dòng trên desktop để thẻ thẳng hàng; mobile co theo nội dung.

### 17/10/2026 – Thu khoảng cách hàng lọc nhanh và kết quả
- Bỏ padding dọc bị cộng dồn giữa timtin-top và kết quả; giảm margin dòng lọc nhanh/chip, còn 16px trước kết quả. Chỉ đổi CSS.

### 17/10/2026 – Căn khoảng cách dọc khung bộ lọc
- Đồng bộ khoảng cách nhóm lọc, giảm khoảng đệm tiêu đề/input/nút; input 40px, nút 42px. Chỉ đổi CSS, giữ chức năng lọc.

### 17/10/2026 – Đồng bộ dropdown sắp xếp, phường/xã và mức giá
- Bootstrap dropdown thay giao diện select native của ba ô; bo góc, giãn dòng gọn, nền champagne và dấu tích. Giữ select làm nguồn giá trị gửi form và phát sự kiện change để sắp xếp vẫn hoạt động; không đổi backend.
- JS syntax và Edge 360/1440px đạt: ba menu mở/đóng Escape, chọn phường/giá gửi đúng tham số, sắp xếp gia-tang hoạt động; không tràn ngang. CSS/JS cập nhật trực tiếp trên demo, không cần build.

### 17/10/2026 – Hoàn thiện khung bộ lọc và dropdown sức chứa
- Căn đều input, thu khoảng cách nhóm lọc, ghi đơn vị giá; dropdown Bootstrap sức chứa có trạng thái chọn champagne thay select native. Giữ giá trị SoNguoiToiDa khi gửi/tải lại, không đổi logic lọc.
- Build/JS syntax đạt; Edge 360/1440px chọn 2 người, Escape, gửi và giữ lựa chọn đạt, không tràn ngang. Đã xem ảnh data/filter-capacity-1440.png. Demo đã chạy lại.

### 17/10/2026 – Thu gọn giãn cách dòng dropdown khu vực Tìm phòng
- Giảm padding, line-height và chiều cao tối thiểu mỗi mục còn 30px; giữ nguyên chức năng chọn khu vực.

### 17/10/2026 – Đồng bộ thanh tìm kiếm với trang chủ
- Dropdown Bootstrap khu vực có dấu chọn, nhãn từ khóa và ô/nút 60px giống trang chủ; mobile xếp dọc. Tái sử dụng home-district.js, giữ QuanHuyen và các bộ lọc hiện có.
- Build đạt; Edge 360/768/1440px không tràn ngang; dropdown, lọc nâng cao mobile, sắp xếp giá và phân trang đạt. Demo đã chạy lại.

### 17/10/2026 – Đồng bộ header công khai với trang chủ
- Tách CSS header dùng chung cho layout công khai: logo/menu/nút và gutter hai mép giống trang chủ, giữ active đúng trang. Không đổi tenant header.
- Build đạt; Edge đối chiếu tọa độ/kích thước header, logo, menu và hai nút trên trang chủ/Tìm phòng giống nhau tại 360/1440px, không tràn ngang. Ảnh data/shared-header-360.png và data/shared-header-1440.png. Demo đã chạy lại.

### 17/10/2026 – Bỏ breadcrumb trang Tìm phòng
- Gỡ riêng dòng Trang chủ › Tìm phòng theo ảnh. Build/HTTP đạt, demo đã chạy lại.

### 17/10/2026 – Làm mới trang Tìm phòng bằng Bootstrap
- Chỉnh tiêu đề/mô tả, form tìm kiếm, lọc nhanh/nâng cao, sidebar, card-body, nút Bootstrap và phân trang. CSS giới hạn trong timtin-page, bố cục 3/2/1 cột; bỏ biểu tượng tim trang trí không có chức năng.
- Build đạt; Edge 360/768/1440px không tràn ngang, lọc nâng cao mobile, chọn quận, sắp xếp giá và phân trang đạt. Đã xem ảnh data/room-search-1440.png và data/room-search-360.png; demo đã chạy lại. Chưa push lần chỉnh này.

### 17/10/2026 – Căn đều hai nút đăng nhập/đăng ký
- Đặt cùng kích thước 108×44px desktop và 84×38px mobile, căn giữa chữ bằng inline-flex. Edge 360/768/1440px không tràn ngang; demo CSS đã cập nhật.

### 17/10/2026 – Giảm khoảng cách mục khu vực
- Thu padding danh sách và từng mục dropdown khu vực, chiều cao mục khoảng 34px. Edge desktop/mobile: chọn, Escape và gửi bộ lọc đạt.

### 17/10/2026 – Thu gọn chiều cao thanh tìm kiếm
- Giảm padding khung còn 12px, ô nhập và nút desktop 60px thay cho 74px; nút mobile 48px. Giữ nhãn và chức năng.
- Edge 360/768/1440px không tràn ngang; dropdown/tìm kiếm đạt. CSS có hiệu lực trên demo đang chạy.

### 17/10/2026 – Giãn header về hai mép màn hình
- Bỏ giới hạn 1320px của header trang chủ, dùng chiều rộng 100% và khoảng đệm hai bên bằng nhau; logo trái, menu giữa, tài khoản phải.
- Edge 360/768/1440px không tràn ngang; CSS phục vụ HTTP 200. Không cần build lại cho thay đổi CSS.

### 17/10/2026 – Bỏ khối nhận tin trong footer
- Gỡ lời mời nhận tin tuần và nút liên hệ email theo ảnh; giữ liên hệ, các nhóm liên kết và bản quyền. Build/HTTP đạt, demo đã chạy lại.

### 17/10/2026 – Căn chỉnh thanh header trang chủ bằng Bootstrap
- Bootstrap navbar/container/nav/button; căn ba nhóm logo/menu/tài khoản, đồng bộ kích thước nút và nền active nhẹ. Mobile hai hàng gọn. Không đổi nội dung banner, thẻ phòng hay footer.
- Build đạt; Edge 360/768/1440px không tràn ngang, đã xem ảnh header desktop; tìm kiếm vẫn hoạt động. Demo đã chạy lại.

### 17/10/2026 – Đổi nội dung tiêu đề banner
- Nội dung mới: Chọn nơi bạn thích. Sống theo cách bạn muốn. Thêm dòng dẫn ngắn, nhấn champagne và căn kiểu chữ/khoảng cách bằng Bootstrap; chỉ sửa khối tiêu đề/mô tả, giữ form và phần dưới.
- Build đạt; Edge 360/768/1440px không tràn ngang, tìm kiếm hoạt động; đã xem ảnh data/hero-refined-1440.png. Demo đã chạy lại.

### 17/10/2026 – Khôi phục phiên bản theo ảnh 02:52
- Hoàn tác lần làm mới thẻ phòng gần nhất; khôi phục banner và form hai ô theo ảnh, chiều cao desktop 650px. Giữ dropdown, footer và trạng thái không có lối tắt khu vực.
- Build/HTTP đạt, demo đã chạy lại.

### 17/10/2026 – Làm mới đồng bộ banner, tìm kiếm và thẻ phòng Bootstrap
- Banner giảm chiều cao, form nhãn rõ; thẻ phòng gọn có nhãn khu vực trên ảnh, địa chỉ, giá thuê và metadata. Grid 1/2/3 cột theo màn hình; giữ footer và không thêm lại lối tắt khu vực.
- Build đạt; Edge 360/768/1440px không tràn ngang, chọn khu vực/từ khóa và tìm kiếm đạt. Ảnh QA data/home-refresh-360.png, data/home-refresh-768.png, data/home-refresh-1440.png. Demo đã chạy lại.

### 17/10/2026 – Tinh chỉnh riêng banner/tìm kiếm Bootstrap
- Cân đối chữ và nền ảnh, form có hai ô cùng chiều cao/nhãn, nút và trạng thái focus/hover rõ hơn. CSS giới hạn trong home-reference-hero; không sửa thẻ phòng/footer, không đưa lại lối tắt khu vực.
- Build đạt; Edge 360/768/1440px không tràn ngang; dropdown và gửi khu vực/từ khóa đạt. Đã xem ảnh data/hero-refined-360.png và data/hero-refined-1440.png. Demo đã chạy lại.

### 17/10/2026 – Bỏ riêng dòng Khám phá khu vực
- Gỡ nhãn và các nút lối tắt theo ảnh; giữ bộ chọn khu vực trong tìm kiếm, danh sách phòng và footer. Build/HTTP đạt; demo đã chạy lại.

### 17/10/2026 – Banner trang chủ trở về bố cục ảnh tham chiếu
- Chỉ chỉnh khối đầu: ảnh nền tràn ngang, tiêu đề/mô tả gốc, thanh tìm kiếm Bootstrap gần chân ảnh. Giữ dropdown Bootstrap và lối tắt khu vực ở ngay dưới banner. Đối chiếu mã danh sách phòng không đổi; footer giữ nguyên.
- Build đạt; Edge 360/768/1440px không tràn ngang, dropdown/tìm kiếm/liên kết hoạt động. Đã xem ảnh data/home-final-1440.png; demo chạy tại localhost:5247.

### 17/10/2026 – Khôi phục giao diện trước lần xóa vừa rồi
- Khôi phục lối tắt khu vực và danh sách thẻ phòng theo yêu cầu; giữ nguyên các thay đổi giao diện trước đó. Build/HTTP đạt, demo đã chạy lại.

### 17/10/2026 – Bỏ lối tắt khu vực và danh sách thẻ phòng trang chủ
- Gỡ hai phần theo yêu cầu; giữ banner, tìm kiếm và footer. Build/HTTP đạt, demo đã chạy lại.

### 17/10/2026 – Hoàn thiện trang chủ Bootstrap lần hai
- Thống nhất khoảng cách, header responsive hai hàng trên mobile, banner bo góc, form Bootstrap, khu vực phổ biến lấy từ dữ liệu thật, thẻ phòng có giá/diện tích/sức chứa/liên kết và trạng thái rỗng. Căn footer và cải thiện độ tương phản. Giữ phần lợi ích đã gỡ, không đổi dashboard sau đăng nhập hay dữ liệu.
- Build đạt (cảnh báo NU1900/ImageSharp/CS8601 có sẵn). Edge 360px/768px/1440px không tràn ngang; dropdown, tìm kiếm hai tham số, lối tắt khu vực và chi tiết phòng đạt. Đã xem ảnh data/home-final-360.png và data/home-final-1440.png; ảnh tablet data/home-final-768.png. Demo đã chạy lại tại localhost:5247. Chưa thử thiết bị cảm ứng vật lý.
### 17/10/2026 – Dropdown khu vực Bootstrap trên trang chủ
- Thay select native bằng Bootstrap dropdown, bo góc, nền chọn champagne và dấu tích; giữ tham số QuanHuyen qua hidden input, hỗ trợ bàn phím Bootstrap.
- Build và JS syntax đạt; Edge 360px/1440px chọn Quận 1, đóng bằng Escape, gửi lọc đúng và không tràn ngang. Ảnh data/district-360.png và data/district-1440.png. Demo đã chạy lại.

### 17/10/2026 – Bỏ mục Tại sao nên chọn NhàTốt?
- Gỡ khối ba lợi ích và CSS riêng khỏi trang chủ theo yêu cầu. Build/HTTP đạt, footer vẫn hiển thị; demo đã chạy lại.

### 17/10/2026 – Hiển thị footer ở cuối trang chủ công khai
- Tái sử dụng footer hiện có đúng nội dung mẫu: thông tin NhàTốt, liên hệ, ba nhóm liên kết, email và bản quyền. Gỡ điều kiện ẩn footer ở trang chủ; không thêm đăng ký email giả.
- Build đạt; Edge 1440px/360px footer hiển thị, không tràn ngang; ảnh data/home-footer-1440.png và data/home-footer-360.png. Demo đã chạy lại tại localhost:5247.
### 17/10/2026 – Làm mới trang chủ công khai bằng Bootstrap
- Dùng Bootstrap container/row/col, card, form-select/form-control và button cho hero, tìm phòng và danh sách phòng. Tông than/champagne; banner gọn, form có nhãn rõ, thẻ phòng responsive. Bỏ khóa chiều cao/cuộn trang chủ để nội dung bên dưới truy cập được; CSS chỉ tải ở trang chủ chưa đăng nhập.
- Build đạt; Edge 1440px/360px không tràn ngang, đã xem ảnh data/home-bootstrap-1440.png và data/home-bootstrap-360.png. Form chuyển đúng khu vực/từ khóa sang TimTin. Demo đang chạy tại localhost:5247. Không đổi schema hay dữ liệu.
### 17/10/2026 – Bỏ trang danh sách tin cho thuê trùng Tìm phòng
- Gỡ mục Tin đăng cho thuê khỏi header công khai và header/sidebar khách thuê. Liên kết tìm tin ở giới thiệu, yêu cầu và footer chuyển sang TimTin. URL TinDang/Index chuyển hướng TimTin, giữ query string; không đổi quản lý, chi tiết tin hay dữ liệu.
- Bổ sung script sidebar cho layout công khai khi khách thuê đăng nhập.
- Build đạt; HTTP xác minh /TinDang?SapXep=gia-tang chuyển sang /TimTin?SapXep=gia-tang (200), chi tiết tin 1 trả 200, menu công khai không còn liên kết trang cũ. Demo đang chạy tại localhost:5247.
### 17/10/2026 – Menu khách thuê dạng sidebar ẩn
- Đổi menu nổi thành sidebar modal sát trái, cao toàn màn hình; nút ba gạch mở, nút đóng/Escape/bấm nền/chọn liên kết đóng. Giữ nguyên các mục và điều kiện phân quyền; hỗ trợ focus bàn phím, aria-expanded và khóa cuộn nền.
- Build đạt, JS syntax đạt; Edge 360px/1440px kiểm tra trạng thái ẩn, mở và đóng bằng nút/Escape/nền đạt. Ảnh QA: data/tenant-sidebar-360.png và data/tenant-sidebar-1440.png. Trang tổng quan khách có phần hóa đơn tràn ngang sẵn ở 360px (scrollWidth 450); ngoài phạm vi sửa menu. Demo đã khởi động lại tại localhost:5247.
### 16/10/2026 – Sửa chuông thông báo quản lý dẫn tới trang giữ chỗ
- Bổ sung hộp xổ ngay tại chuông giống các vai trò khác: tối đa 6 báo hỏng trong phạm vi quyền, liên kết chi tiết/xem tất cả, đóng bằng Escape hoặc bấm ngoài, tự làm mới 30 giây. Số trên chuông là số cần tiếp nhận (có nhãn trợ năng), không giả lập số chưa đọc; không đổi schema. Endpoint tóm tắt no-store, có kiểm thử chặn chéo vai trò và thu hồi quyền. Build/test liên quan đạt; Edge desktop/360px mở/đóng hộp đạt, đã xem ảnh `data/repair-upgrade-qa/browser-check-eca68ce3`. Server QA đã dừng.
- Chuông quản lý mở `/ThongBaoQuanLy`, đường dẫn cũ chuyển hướng về đây. Trang hiển thị báo hỏng thực tế trong phạm vi tòa được giao, tab cần tiếp nhận/Gấp quá 24 giờ, thời gian gửi/cập nhật và liên kết chi tiết xử lý. Kiểm tra quyền BAO_HONG ở backend và khi hiện chuông; không cấp TAI_CHINH cho quản lý.
- Không thêm trạng thái đã đọc hoặc ghi thông báo giả; nhãn cần tiếp nhận là trạng thái nghiệp vụ. Không đổi schema/DB đang dùng. Build và test HTTP mới đạt: chuông, chuyển hướng, lọc, rỗng, sai bộ lọc, chặn chéo tòa/vai trò/thu hồi quyền và chặn tài chính. Edge desktop/360px không tràn ngang, đã xem ảnh tại `data/repair-upgrade-qa/browser-check-75e2a191`; bộ chạy `verification/ManagerNotificationBrowser`. Server chính/QA đã dừng; chưa push.

### 16/10/2026 – Nâng cấp giao diện báo hỏng khách thuê và quản lý theo mẫu
- Làm lại danh sách/gửi/chi tiết khách thuê và danh sách/chi tiết quản lý; thêm tìm kiếm toàn bộ dữ liệu trong phạm vi quyền, tóm tắt biểu mẫu, kéo thả/bỏ ảnh, lịch sử và thao tác xử lý rõ hơn. Giữ quy tắc trạng thái, chi phí, ảnh riêng tư và cảnh báo Gấp chưa tiếp nhận quá 24 giờ.
- Build đạt; nhóm báo hỏng 135/135 đạt. Edge trên bản sao kiểm tra 5 màn ở 1440px/360px không tràn ngang, tóm tắt trực tiếp, giới hạn ảnh và nút chọn hành động đạt. Chi tiết/log/ảnh: `docs/giao-dien-bao-hong.md`, `data/repair-upgrade-*`.
- Không đổi schema/DB đang dùng/tài khoản; server chính và QA đều đã dừng. Chưa kiểm tra Safari/thiết bị thật/full suite. Chưa commit/push.

### 16/10/2026 – Khách đề nghị trả phòng trước hạn, chủ nhà tiếp nhận lịch bàn giao
- Thêm `/YeuCauTraPhong`, liên kết từ danh sách/chi tiết hợp đồng khách và chủ nhà. Chỉ người đứng tên đang thuê được gửi ngày/lý do; chủ nhà chọn ngày đã thống nhất hoặc từ chối có phản hồi; khách chỉ hủy khi còn mới. Giữ lịch sử, phân trang/lọc hợp đồng; mới/tiếp nhận/từ chối/hủy/hoàn tất có thông báo in-app đúng người, không gửi email thật.
- Hợp đồng/phòng/hóa đơn/cọc không đổi khi gửi/tiếp nhận; ngày tương lai chưa mở nút trả thực tế. Đến ngày, chuyển sang TraPhong/Nhap với ngày gợi ý; quy trình cũ vẫn nhập chỉ số, điều khoản/phạt và tất toán. Chốt tất toán đồng bộ yêu cầu HOAN_TAT cùng transaction; không tự hoàn tiền thật.
- Schema40 thêm yêu cầu/thông báo, unique có điều kiện chống hai yêu cầu mở, trigger giữ lịch sử, phien_ban và transaction immediate. Quyền HOP_DONG và sở hữu kiểm tra backend, POST có CSRF; nhật ký không chứa lý do/phản hồi. Các kỳ vọng schema test cập nhật40, không sửa bước nâng cấp cũ.
- Build đạt; nhóm mới12/12, hồi quy trả phòng/tất toán/phạt/schema98/98. Edge trên bản sao đạt luồng gửi → chủ nhà nhận chuông → tiếp nhận lịch tương lai → từ chối → gửi lại → tiếp nhận hôm nay → mở form trả phòng; desktop/360px không tràn, đã xem ảnh. Log/TRX/ảnh trong `data/checkout-request-*`; bộ browser `verification/CheckoutRequestBrowser`. Chi tiết `docs/yeu-cau-tra-phong-truoc-han.md`.
- Bản sao nâng39→40 giữ57bảng/25.123dòng cũ, integrity=ok/FK=0, chạy lặp byte-identical. Hash sau thử browser: hợp đồng/phòng/hóa đơn/chi tiết/thanh toán/cọc không đổi. Bản build cuối thử lại browser đạt (`data/checkout-request-browser-final.log`); server QA đã dừng. Sau người dùng cho phép, DB đang dùng đã sao lưu ngoài repo tại `%TEMP%/NhaTot-database-backups/checkout-v40-20261016-222912`, nâng40, giữ nguyên hash dữ liệu cũ, integrity/FK đạt, updater chạy lặp byte-identical và `run.bat --check-only` đạt. Hai bảng mới rỗng; server chính để tắt. Chưa kiểm tra Safari/thiết bị thật/full suite. Chưa push phần này.

### 16/10/2026 – Sửa tiếng Việt lỗi mã hóa trên giao diện dùng chung
- Khôi phục chuỗi tiếng Việt trong `Views/Shared/_Layout.cshtml`: tiêu đề tab (thống nhất NhàTốt), ô tìm kiếm, breadcrumb, nút menu, nhãn trợ năng và thông báo. Sửa hai thông báo lỗi phiên đăng nhập trong `Program.cs`. Chỉ thay chuỗi hiển thị, giữ nguyên markup, route và kiểm tra quyền.
- Quét 551 tệp mã nguồn/giao diện/script/style/cấu hình: không còn ký tự thay thế hoặc C1 control do lỗi mã hóa; các chuỗi nghi ngờ còn lại là chữ đúng “ĐÃ”. File `GiayToImageStore.cs` là UTF-16 có BOM hợp lệ, giữ nguyên. Không chuyển mã hàng loạt, không thay DB.
- Build đạt (cảnh báo ImageSharp/CS8601 có sẵn). Kiểm tra Edge trên DB sao chép: công nợ, danh sách/chi tiết/thêm tòa, danh sách phòng, hóa đơn khách và trang chủ quản lý/admin, tổng8màn đạt; đã xem ảnh ô tìm kiếm. Log `data/vietnamese-encoding-browser.log`, ảnh `data/debt-dashboard-qa/b8df171c98/browser-check-fe85c447`. Server QA đã dừng; chưa push thay đổi này.

### 16/10/2026 – Hoàn thiện giao diện công nợ S4-03
- `/CongNo` có tổng quan theo kết quả lọc, tổng hợp theo tòa cùng màn, ưu tiên thu, danh sách phòng và hộp chi tiết từng hóa đơn qua `/CongNo/Phong/{id}`. Hiển thị tổng/đã thu/cấn cọc/còn thiếu/hạn/ngày quá hạn/chờ xác nhận; kiểm tra sở hữu ở backend, khách/quản lý bị chặn. Nhãn nhiều kỳ đòi hỏi hai tháng/năm quá hạn khác nhau.
- Chặn số âm, khoảng đảo, tiền/ngày sai định dạng hoặc tràn số cả UI/server; bộ lọc sai trả400 và không hiển thị/xuất kết quả. Có trạng thái rỗng, tìm kiếm, bốn cách sắp xếp tiền/quá hạn và phân trang gọn giữ bộ lọc. CSV UTF-8 BOM khớp toàn bộ kết quả lọc, giữ escape/chống công thức, tên chứa ngày và bộ lọc; có phản hồi đang xuất/thành công/thất bại. Khoản chờ không tính đã thu; cấn cọc vẫn tách riêng.
- Build đạt, nhóm công nợ cuối đạt12/12 (`data/debt-dashboard-tests/debt-focused.trx`). Nhóm rộng17ca từng có một lỗi login500 không ổn định ở ca PaymentWorkflow ngoài thay đổi công nợ; chạy riêng ca đó đạt1/1 (`debt-login-recheck.trx`), không coi đó là toàn bộ suite xanh. Chưa chạy full suite. Mười ca lọc sai lúc đầu thiếu fixture schema test đã sửa setup; không thay schema ứng dụng.
- Edge trên DB sao chép đạt desktop/360px không tràn ngang, hộp phòng nhiều kỳ, trạng thái rỗng/lỗi và CSV thành công/thất bại; đã xem ảnh. Dữ liệu thử6phòng trả thiếu, TC003/TC004 quá hạn2kỳ, tổng21.500.000đ khớp CSV; script `verification/Verify-S403Data.py` kiểm tra chỉ đọc. Log/ảnh/CSV trong `data/debt-dashboard-*`; bộ chạy `verification/DebtDashboardBrowser`. Tài liệu `docs/S4-03-cong-no.md`.
- Không đổi schema/DB đang dùng hoặc tài khoản; không thêm trang riêng theo tòa. Server được để tắt sau QA; chưa kiểm tra Safari/thiết bị thật. Chưa commit/push phần này.

### 16/10/2026 – Thiết kế lại bản in hóa đơn A4
- Tách `_InvoicePrint.cshtml` dùng chung cho khách/chủ nhà và `invoice-print.css`: khổ A4, thương hiệu, khách/phòng/kỳ/ngày, bảng khoản thu với chỉ số/cách tính, tổng hóa đơn, tiền đã thu, cấn cọc và dư nợ. Mẫu chỉ xuất hiện khi in; menu, form, thông báo và các khung thao tác không vào bản in. Không đổi DB, công thức hay dữ liệu.
- Mỗi dòng tránh ngắt giữa trang, đầu bảng lặp nếu hóa đơn dài. Không đưa ảnh chứng từ/ghi chú báo trả riêng tư vào bản in. Khoản chờ xác nhận được ghi riêng, không trừ nợ. Nháp/xem trước/hủy có nhãn rõ. Nhóm kiểm thử hóa đơn khách liên quan đạt 5/5 (`data/invoice-print-tests/invoice-print.trx`).
- Build đạt. Edge xuất ba PDF thật từ DB sao chép: hóa đơn thường của khách, kỳ cuối của khách và cùng kỳ cuối của chủ nhà; đều một trang A4. Render PDF thành PNG đã rà trực quan. Hóa đơn kỳ cuối giữ tổng 5.813.742 đ, cấn cọc 5.800.000 đ, còn phải trả 13.742 đ. Log/ảnh/PDF trong `data/invoice-print-*` và `data/payment-workflow-qa/aa57372d90/browser-check-ede49fa6`. Bộ kiểm tra `verification/InvoicePrintBrowser`; không ghi nghiệp vụ trong DB QA hoặc DB thật. Để server tắt sau kiểm tra.

### 16/10/2026 – Luồng đối chiếu báo thanh toán theo mẫu
- Khách có form báo trả kèm ảnh/ghi chú, ngân hàng/QR, màn chờ xác nhận, hủy, lịch sử và lý do từ chối. Chủ nhà có thống kê/tab chờ, cột báo trả, chuông và màn đối chiếu với ba mục kiểm tra thủ công; từ chối bắt buộc lý do. Khoản chờ không giảm nợ; chỉ xác nhận mới ghi thu. Thông báo mới là in-app.
- Schema v39 thêm metadata và thông báo thanh toán; không suy đoán thời gian gửi cũ. Kiểm tra quyền/CSRF, token ảnh 15 phút, thông báo/nghiệp vụ/audit cùng transaction. Chi tiết: `docs/luong-bao-thanh-toan-va-thong-bao.md`.
- Build đạt; nhóm tích hợp liên quan đạt 31/31. Edge trên bản sao chạy hết gửi ảnh → chuông chủ nhà → từ chối → gửi lại → xác nhận; kiểm tra dư nợ/ảnh riêng tư, desktop/360px không tràn ngang. Log và ảnh trong `data/payment-workflow-*`. Chưa kiểm tra Safari/thiết bị thật/SMTP thật; chưa chạy lại toàn bộ suite.
- Bản sao nâng v38→39 giữ 54 bảng/25.067 dòng cũ, integrity/FK đạt, chạy lặp không đổi byte. Sau người dùng cho phép, sao lưu DB local tại `%TEMP%/NhaTot-database-backups/payment-v39-20261016-192112`, nâng v39, đối chiếu hash giữ nguyên 25.067 dòng nghiệp vụ. `run.bat --check-only` đạt. Server được để tắt.

### 16/10/2026 – Thu gọn phân trang hóa đơn chủ nhà
- Sửa `Views/HoaDonDichVu/Management.cshtml`: bỏ vòng lặp hiện toàn bộ hàng trăm trang, chỉ hiện trang đầu/cuối, trang hiện tại ±1 và dấu …; thêm trước/sau, trạng thái vô hiệu hóa đầu/cuối và aria-current. Giữ nguyên 10 hóa đơn/trang, toàn bộ bộ lọc và truy vấn dữ liệu.
- CSS trong `invoice-flow.css` chỉ áp dụng `.management-pagination`, giữ dòng tổng không bị ép xuống từng từ; phân trang gọn trên màn hình 360px. Không đổi schema, dữ liệu hoặc nghiệp vụ phát hành/thu tiền.
- Build đạt (cảnh báo ImageSharp/CS8601 có sẵn). Edge trên bản sao QA đạt: trang 42/574 chỉ có tối đa 6 liên kết, trước bị khóa ở trang đầu, liên kết giữ bộ lọc năm/tháng, thanh phân trang không tràn ở 360px; đã xem ảnh desktop. Log `data/owner-pagination-browser.log`. Đã dừng QA, server chính được để tắt theo yêu cầu người dùng tự chạy run.bat.

### 16/10/2026 – Thiết kế lại danh sách hóa đơn khách thuê, thống nhất trang chi tiết
- `/ThongBao/DanhSach` có bốn ô tổng quan, tìm theo mã, lọc kỳ/trạng thái/báo chờ, nhóm tất cả/cần thanh toán/đã thanh toán, chọn 10/20/50 dòng, phân trang số và nút Xem chi tiết. Giao diện theo ảnh người dùng, chỉ thay vùng nội dung; mobile 360px chuyển các dòng thành thẻ, không tràn ngang.
- Mã hóa đơn và nút Xem chi tiết mở thẳng `/ThongBao/HoaDon/{id}`; liên kết hóa đơn ở trang chủ cũng mở cùng route. Nút quay lại trong chi tiết về DanhSach. Xóa view `ThongBao/ChiTiet.cshtml`, JS `tenant-invoice-detail.js` và page model cũ; route ChiTiet theo mã chỉ còn bộ chuyển hướng có kiểm tra sở hữu. Giữ API dữ liệu ChiTietDuLieu tương thích, không còn dùng để dựng màn hình riêng.
- Trang HoaDon kiểm tra người đứng tên theo hóa đơn/hợp đồng, không phụ thuộc thông báo đã được tạo. Khách khác vẫn 403, giữ quyền module tài chính. Hủy báo cáo quay về thẳng route HoaDon. Không đổi nghiệp vụ tính tiền, báo trả, xác nhận hoặc schema/DB.
- API danh sách bổ sung ID/phòng/đã thu/cấn cọc/báo chờ và tổng quan toàn bộ hóa đơn được quyền xem. Bộ lọc mới `tinhTrang` tách trạng thái khoản phải trả khỏi báo chờ; vẫn giữ tham số `trangThai` cũ cho API tương thích. Báo chờ không giảm nợ; cấn cọc thể hiện riêng. Tổng quan không thay theo bộ lọc.
- Build/test và Edge desktop/360px đã chạy; nhóm liên quan đạt 12/12. Sau đổi liên kết trang chủ, nhóm cuối chạy tuần tự đạt 9/9, gồm test mới tổng quan/bộ lọc/quyền khi chưa có thông báo, điều hướng cũ và mới, cùng các ca trang chủ. Browser bản build cuối đạt danh sách, mở đúng route, đổi số dòng, tìm kiếm, nhóm đã thanh toán và không tràn ngang. Log/TRX/ảnh trong `data/tenant-list-*` và bản sao QA ignored. Bộ browser mới `verification/TenantInvoiceBrowser`; cập nhật bộ OwnerBankBrowser dùng form chuẩn.
- Lượt đầu 45/47 đạt: một kỳ vọng nút cũ đã cập nhật; một ca login 500. Một lượt kiểm tra bổ sung có ba lỗi login 500 khi chạy song song; chạy lại nhóm tuần tự đạt 9/9. Không sửa nghiệp vụ đăng nhập trong task này; lỗi không ổn định khi kiểm thử song song vẫn cần theo dõi. Chưa chạy lại full suite; chưa kiểm tra Safari/thiết bị thật.

### 16/10/2026 – Khắc phục run.bat: DB v37 chưa khớp mã v38
- Sau khi người dùng gửi lỗi khởi động, đã dừng/kiểm tra không còn tiến trình web, sao lưu DB đang dùng ra ngoài repository rồi nâng `data/s4-acceptance/20261016-115246-fb2642/s404.sqlite` lên v38.
- So sánh hash toàn bộ dữ liệu: giữ nguyên cả 53 bảng cũ / 25.036 bản ghi. Integrity=ok, FK=0; `run.bat --check-only` đạt. Không thay tài khoản/mật khẩu hoặc cấu hình ngân hàng hiện có; bảng ngân hàng mới được tạo rỗng.
- Backup ngoài repo trong `%TEMP%/NhaTot-database-backups/bank-v38-20261016-174620/`; updater có thêm backup riêng. Chi tiết đường dẫn và log kiểm tra ở `data/owner-bank-*` ignored.

### 16/10/2026 – Cấu hình ngân hàng chủ nhà và QR trong báo thanh toán
- Thêm khối nhận chuyển khoản ở `/HoSo/CuaToi`: xem/sửa ngân hàng, số tài khoản, xem trước/thay/xóa QR JPG/JPEG/PNG ≤ 5 MiB, hủy và lưu AJAX, lỗi theo trường, khóa lạc quan. Chủ nhà chỉ thao tác cấu hình của mình.
- API `/api/ngan-hang/cua-toi` (GET/POST), `/cua-toi/qr`, `/hoa-don/{id}` và `/hoa-don/{id}/qr`; kiểm tra vai trò/tài khoản hoạt động/quyền hóa đơn ở backend, CSRF cho POST, QR private/no-store. Khách đọc theo đúng chủ nhà sở hữu phòng; người ở ghép cần khoảng cư trú giao kỳ hóa đơn. QUAN_LY/ADMIN không được sửa.
- Tích hợp hai form thanh toán hiện có: chọn chuyển khoản hiện ngân hàng, số tài khoản, sao chép và QR trước ảnh chứng từ; chọn tiền mặt ẩn; chưa cấu hình hiển thị thông báo thay thế. Không đổi header/sidebar, không tích hợp ngân hàng, không tự xác nhận hoặc tính tiền từ QR; không mở rộng luồng gửi báo cáo của người ở ghép.
- Schema v38 thêm `tai_khoan_ngan_hang` (PK/FK chủ nhà, ngân hàng, số tài khoản, khóa ảnh/MIME, UTC tạo/cập nhật, phiên bản), giữ nguyên bước cũ. Upload/storage và DB lỗi giữ cấu hình/QR cũ. Loại thư mục `*.bank-qr/` khỏi Git.
- Nâng bản sao DB thực v37 → v38: cả 53 bảng cũ giữ nguyên toàn bộ dữ liệu, integrity=ok/FK=0; có backup và test chạy lặp. DB chính chưa nâng theo AGENTS.md §3.1; web chính đã dừng để build. Bản sao QA riêng chạy tại localhost:5251, không gửi email thật.
- Build đạt. Lần test toàn bộ: 1.149/1.151 đạt, hai lỗi chỉ là kỳ vọng chuỗi phiên bản cũ `37` trong LichHenMigrationTests. Sau sửa thành `38`, chạy lại cả nhóm migration đạt 4/4; tổng 1.151 ca duy nhất đã đạt qua các lần chạy, không gọi đây là một lần full suite xanh. Test riêng ban đầu 17/17 và bổ sung nâng cấp/luồng thanh toán 2/2 đều đạt.
- Edge desktop/360px đạt: lưu AJAX/upload/xem QR, đúng cấu hình chủ nhà, chuyển khoản/tiền mặt, hủy chỉnh sửa, không tràn ngang; đã xem ảnh và sửa CSS cục bộ tránh nhãn số tài khoản xuống từng ký tự. QR test là ảnh giả, không phải mã thanh toán thật. Chưa Safari/thiết bị cảm ứng thật/review của thành viên khác. Cảnh báo ImageSharp license/CS8601 có sẵn.
- Bàn giao, danh sách file, schema/API/quyền và lệnh kiểm thử: [docs/tai-khoan-ngan-hang-chu-nha.md](docs/tai-khoan-ngan-hang-chu-nha.md). Log/TRX/ảnh/credential QA ở `data/owner-bank-*` ignored; không đưa dữ liệu riêng lên Git.

### 14/10/2026 – S4-04 task 2: bảng tất toán cọc và khấu trừ
- PO xác nhận dùng cọc thực giữ, trừ thanh toán/cấn cọc đã xác nhận khỏi nợ; kết quả = cọc − dư nợ kỳ cuối − nợ kỳ trước − hư hỏng. Làm tròn từng khoản AwayFromZero; hư hỏng cho phép bằng 0, không cho âm/tên rỗng.
- Theo yêu cầu người dùng, khôi phục task 1 đang thiếu ở HEAD 29d4f02, nối hóa đơn kỳ cuối sang bảng tất toán. Chủ nhà thêm/sửa/xóa hư hỏng, xem dự tính từ máy chủ, lưu/sửa NHAP; khách xem cùng snapshot, không sửa. ADMIN chỉ xem, QUAN_LY không truy cập tài chính.
- Schema v31 bổ sung sổ cọc, bảng tất toán và chi tiết khấu trừ; phiên bản/dấu vân tay nguồn, BEGIN IMMEDIATE, FK RESTRICT, audit và rollback. Không thu/hoàn/cấn cọc trên UI, không phạt, chốt/kết thúc hợp đồng/đổi trạng thái phòng/PDF.
- Nâng trên bản sao DB giả v30: bảo toàn hash tất cả dòng/cột cũ, backup, integrity/FK và rerun đạt. DB local vẫn giữ nguyên v5/chưa có module hợp đồng/hóa đơn.
- Demo riêng localhost:5252; New-S404Demo.ps1/Start-S404Demo.ps1, tài khoản ngẫu nhiên trong access.json ignored. Edge desktop/360px: thêm/sửa/xóa, dự tính/lưu, khách xem cùng số liệu, sửa/khách lạ 403; không tràn ngang ở 360px. Chưa Safari/thiết bị thật, review/hợp nhất nhánh.
- Build cuối đạt; 121/121 test liên quan đạt (38 S4-04/khôi phục task 1 + 83 hồi quy). HTTP danh sách chủ nhà/khách 200; Edge lượt cuối đạt trên data/s404-task2-final, ảnh/report ignored browser-check-c27c22b6. Demo integrity=ok/FK=0. Cảnh báo ImageSharp/CS8601 có sẵn.
- Bàn giao và lệnh kiểm thử: [docs/S4-04-tat-toan-task2.md](docs/S4-04-tat-toan-task2.md).

### 14/10/2026 – S4-07:2 giai đoạn 2/6: schema v31
- Thêm bảng chi phí/lịch sử riêng, view trạng thái, CHECK/FK/UNIQUE/index/trigger: một tổng phí, chỉ HOAN_THANH/Quản lý đúng phân công, hợp đồng gốc, tiền nguyên dương, nguồn hóa đơn NHAP giữ chỗ, chống hai khoản hiệu lực và giữ snapshot hủy/thay thế. Không tự tạo khoản thu/hóa đơn.
- Chỉ nâng data/s407-cost/stage1-20261014-01/s407-cost-v30.sqlite: v31, integrity/FK đạt;45 bảng/299 bản ghi cũ giữ nguyên; thêm2 bảng rỗng và một dòng phiên bản. Rerun không đổi byte/schema/dữ liệu. Hash nguồn S407 và backup v30 giữ nguyên.
- Build0 lỗi,22/22 test schema trên bản sao tạm đạt; không seed/reset và không có dữ liệu test vĩnh viễn trong bản thử nghiệm. Hai lỗi công cụ kiểm chứng (handle khi đọc hash/tuple-list) được xử lý ở test hoặc đối chiếu chỉ đọc, không sửa DB nghiệp vụ.
- Không sửa service/controller/ViewModel/Razor; chưa chạy full suite/HTTP/UI, chưa commit/push/merge. Tích hợp giai đoạn3–5 và giới hạn fixture/optional invoice module ghi trong docs/S4-07-02-schema-v31.md. Dừng ở giai đoạn2.

### 14/10/2026 – S4-07:1: tiếp nhận và tiến độ báo hỏng
- UI-01: thông báo thành công chuyển sang `_Toast` dùng chung, giữ tới khi đóng/rời trang; toast mặc định khác/lỗi vẫn tự ẩn3 giây. Chỉ sửa Details/_Toast/site.js, không đổi nghiệp vụ/DB. Build0 lỗi, 50/50 test báo hỏng đạt. Trình duyệt desktop1280/mobile360 xác minh thông báo, nút đóng, không lặp sau reload, lỗi không hiện success. Thử trên bản sao data/s407-ui01/demo/ui01.sqlite, SHA256 nguồn demo không đổi; hướng dẫn cập nhật runtime ở docs/S4-07-tiep-nhan-tien-do.md.
- PO chốt Chủ nhà chỉ xem nội dung/ảnh/trạng thái/lịch sử thuộc tòa hiện sở hữu, kể cả hợp đồng kết thúc; không cập nhật/phản hồi. Giữ chuyển trạng thái/ghi chú bắt buộc của S4-06.
- Thêm service/controller GET Chủ nhà với quyền backend ở list/detail/image, nối menu tới dữ liệu thật; dùng chung view, thêm tên người gửi và mô tả sự cố trong danh sách. CSS màn hình nhỏ chỉ trong trang báo hỏng.
- Không đổi schema/migration/updater. Demo riêng data/s407/demo-20261014-164333-49008c, localhost5271; SHA256 nguồn Sprint2 trước/sau không đổi, integrity/FK đạt.
- Build đạt0 lỗi; 50/50 test báo hỏng đạt (40 hồi quy +10 mới), HTTP403/CSRF400/ghi chú/chuyển trạng thái/stale409/retry/ảnh/lịch sử/hợp đồng kết thúc đạt. Lần test đầu bị sandbox chặn testhost; chạy lại ngoài sandbox đạt.
- Trình duyệt Chủ nhà có tên/ảnh/lịch sử, không form ghi; Quản lý có form đúng. DOM360px Chủ nhà354/Quản lý345, ảnh tải được. Chưa nghiệm thu Safari/thiết bị thật/review thành viên khác. Chưa commit/push/merge.
- Hướng dẫn, TC01–TC16 và giới hạn: docs/S4-07-tiep-nhan-tien-do.md. Chạy verification/Start-S407Demo.ps1, ID2 giữ trạng thái Mới để nghiệm thu.

### 13/10/2026 – Dữ liệu kiểm thử Sprint 3 bổ sung
- Thêm vào DB local tòa kiểm thử ID 7 cho tài khoản chủ nhà được yêu cầu: 60 phòng, 54 hợp đồng, 54 tài khoản khách mẫu, 6 yêu cầu, 6 hóa đơn. Giữ dữ liệu và tài khoản cũ.
- Đã nạp thử bản sao; backup, integrity_check=ok, foreign_key_check=0; kiểm tra bảo toàn mọi bản ghi cũ và schema. Không build/test ứng dụng vì không đổi mã.
- Hướng dẫn và thông tin đăng nhập mẫu nằm trong thư mục ignored data/sprint3-extra-20261013/live-verified/. Chưa nghiệm thu AC trên UI/thiết bị hay đo hiệu năng. Ba hóa đơn phát hành nhập snapshot còn tham chiếu kỳ/giá gốc; dùng nháp/kỳ mới để kiểm thử truy vết cùng hợp đồng.


- Bổ sung phân tầng S3-05: tòa mẫu ID 7 có 6 tầng × 10 phòng, giữ mã phòng. T051 tầng 2 thiếu điện, T052 tầng 4 thiếu nước kỳ 10/2026. Đã thử trên bản sao, backup, kiểm tra integrity/FK và đối chiếu các trường còn lại giữ nguyên. Mapping trong data/sprint3-extra-20261013/floors/mapping.json.


### 14/10/2026 – S4-06 task 2: ảnh báo hỏng
- PO chốt JPG/JPEG, PNG; tối đa 3 ảnh × 5 MiB (5.242.880 byte), nhận đúng giới hạn.
- Đã thêm chọn/xem trước/bỏ ảnh, validation giao diện và nội dung máy chủ, kho riêng ngoài wwwroot, endpoint ảnh kiểm tra quyền người gửi/no-store/nosniff, mở ảnh từ chi tiết.
- Schema v29 thêm anh_bao_hong, giữ bước v28 và dữ liệu cũ. Ghi báo hỏng/metadata/mã/audit cùng transaction; lỗi ảnh rollback, retry so hash/thứ tự chống trùng.
- Nâng bản sao v28 giữ 42 bảng/251 dòng, integrity/FK đạt, rerun an toàn. DB local/Sprint2/task1 giữ nguyên.
- HTTP upload/biên/ảnh giả/storage failure/403 đạt; trình duyệt bỏ/chọn lại/gửi 3 ảnh BH-202610-0013/Mới; DOM 360px không tràn, không chụp màn hình theo yêu cầu.
- 24 test báo hỏng/ảnh và 36 hồi quy thu/hủy thu đạt. Bộ rộng RoomServicesTests có 3 lỗi: 2 lỗi text tái hiện ở baseline c4b6fc6; ca login500 đạt khi chạy riêng và trên baseline. Cảnh báo ImageSharp/CS8601 hiện hữu.
- Demo localhost:5249; data/s406/images-latest.txt chứa đường dẫn ignored. Hướng dẫn/lệnh/test/giới hạn tại docs/S4-06-anh-bao-hong.md. Chưa có cập nhật trạng thái, phản hồi quản lý, dọn tệp mồ côi sau crash; chưa nghiệm thu trực quan/Safari/review thành viên khác. Không push task2.

### 14/10/2026 – S4-06 task 3: trạng thái và phản hồi quản lý
- PO chốt Mới→Đang xử lý/Từ chối, Đang xử lý→Hoàn thành; trạng thái cuối không mở lại; mỗi lần đổi có ghi chú, phản hồi ở mọi trạng thái. Chỉ QUAN_LY được gán tại tòa xử lý; khách gửi giữ quyền xem sau hết hợp đồng.
- Đã thêm danh sách/chi tiết quản lý trong phạm vi, xem ảnh riêng tư, đổi trạng thái, gửi phản hồi, lưu người/snapshot tên/nội dung/thời điểm và lịch sử trên cùng chi tiết của khách.
- v30 thêm bao_hong.phien_ban và bao_hong_trao_doi bất biến; chống stale409/retry trùng; trạng thái/lịch sử/audit cùng transaction, rollback khi lỗi; audit không lưu nội dung tự do.
- 76/76 test liên quan đạt (40 báo hỏng/ảnh/theo dõi, 36 thu/hủy thu); HTTP CSRF/403/chuyển trạng thái/phản hồi/stale/reload/hết hợp đồng/ảnh/integrity/FK đạt.
- Nâng bản sao v29 giữ 43 bảng/280 dòng và ảnh, integrity/FK/rerun đạt. DB dùng thật và demo cũ không nâng. Demo mới localhost:5250, data/s406/tracking-latest.txt ignored.
- Trình duyệt ID13: quản lý cập nhật Đang xử lý/gửi phản hồi, khách reload thấy cùng trang và 3 ảnh. DOM 360px không tràn; không chụp màn hình. Chưa nghiệm thu trực quan/Safari/review thành viên khác.
- Phạm vi S4-06 đủ AC1–AC4 về chức năng; chưa triển khai thông báo tự động, khách trả lời, chi phí và phần khác của S4-07. Hướng dẫn tại docs/S4-06-trang-thai-phan-hoi.md. Nhánh bàn giao Task3: `feature/S4-06/3.maintenance-request-status-feedback-tracking`.

### 15/10/2026 – S4-04 task 3: chấm dứt trước hạn và phạt cọc
- Khôi phục task 1–2 từ commit 79d428a trên nhánh task 3 bắt đầu ở 29d4f02. Chưa commit/push.
- PO chốt: ngày trả < ngày kết thúc kỳ cuối (kể cả gia hạn); 4 lý do, Khác cần ghi rõ; Có phạt thì số nguyên đồng >0 và ≤ cọc thỏa thuận, Không phạt =0.
- Đã thêm chọn lý do/Có–Không/mức phạt ở trả phòng và tất toán, lưu cùng transaction, hiển thị cho chủ nhà/khách thuê; PHAT_COC riêng tối đa một dòng, không cộng lặp hư hỏng, cập nhật tổng khấu trừ và hoàn/trả thêm.
- Schema v32 thêm cham_dut_hop_dong, tien_phat và loai_khoan; unique/ CHECK/FK RESTRICT/trigger, snapshot/phiên bản/fingerprint chống stale, audit không ghi nội dung lý do khác. Giữ bước v31; nâng bản sao v31 có dữ liệu, bảo toàn mọi cột cũ, backup/integrity/FK/rerun đạt. Không nâng DB local v5 hoặc demo task 2 v31.
- Build đạt; 145/145 test liên quan đạt (24 task 3 + 121 hồi quy). Edge kiểm tra trả phòng, đổi Có/Không, thêm/sửa/xóa hư hỏng, lưu một dòng phạt, khách xem cùng số liệu, 403 khi sửa/trái sở hữu; DOM 360px không tràn. Ảnh/report ở browser-check-* trong bộ demo ignored.
- Demo riêng localhost:5253; data/s404-task3-demo/latest.txt trỏ bộ mới. Cọc 7m − cuối 1.863.742 − cũ 1.2m − hư hỏng450k − phạt3.5m => khách trả thêm13.742đ. New-S404Demo.ps1/Start-S404Demo.ps1 có -Task3; không ghi đè demo cũ.
- Chưa xác nhận/chốt tất toán, kết thúc hợp đồng/phòng/tin, ghi CAN_TRU/hoàn cọc hay PDF. Khi làm bước chốt phải chuyển phạt/hư hỏng thành PHAT_SINH và tránh cộng hai lần theo AGENTS 7.6. Chưa nghiệm thu Safari/thiết bị thật/review thành viên khác. Hướng dẫn: docs/S4-04-cham-dut-truoc-han-task3.md.

### 15/10/2026 – S4-04 task 4: phần nền xác nhận tất toán (đang làm, chờ PO)
- Nhánh task 4 bắt đầu từ nền v30, chưa có task 1–3; đã khôi phục worktree từ commit 3f3c477 (task 3). Không commit/push, không sửa CSDL local hoặc demo task 2–3.
- Đã chuẩn bị schema v33: xac_nhan_tat_toan (người/thời điểm/số tiền, unique, bất biến) và lich_su_trang_thai_phong; trigger khóa tạo/phát hành hóa đơn khi hợp đồng DA_KET_THUC, chặn hủy hóa đơn của tất toán đã chốt. Updater nối thêm, giữ bước cũ, có backup/integrity/FK; đã thử bảo toàn toàn bộ dữ liệu cũ trên bản sao v32.
- Đã chuẩn bị Razor kiểm tra/xác nhận/modal/kết quả và DTO; chưa nối action HoanTat hoặc thực thi transaction chốt, vì đang chờ quy tắc thanh toán do PO chốt. Không coi task 4 đã hoàn tất; UI mới chưa kiểm chứng qua trình duyệt.
- Câu hỏi đang chờ: chốt nghĩa vụ và tiếp tục thu/hoàn sau (CAN_TRU được ghi khi chốt), hay chỉ kết thúc sau khi đã thu/hoàn đủ. Đây là điều kiện nghiệp vụ bắt buộc của task 4, không tự suy đoán.
- Build đạt; 167/167 ca kiểm tra phần chung/hồi quy đạt, gồm chặn tạo/phát hành sau kết thúc tại service và SQLite, nâng bản sao v32/rerun/bảo toàn dữ liệu. Kết quả ignored: data/s404-task4-test-results/s404-task4-common.trx. Cảnh báo ImageSharp/CS8601 có sẵn. Chưa có demo task 4, chưa triển khai PDF.

### 15/10/2026 – S4-04 task 4: hoàn tất xác nhận tất toán và kết thúc hợp đồng
- PO đã xác nhận phương án chốt nghĩa vụ: chỉ Chủ nhà sở hữu có quyền ghi HOP_DONG + TAI_CHINH; phát hành kỳ cuối có hư hỏng/phạt, CAN_TRU đúng một lần; thu/hoàn thực tế ghi sau. Mục này thay trạng thái chờ PO ở ghi chú phần nền trước đó.
- Đã nối kiểm tra bản đã lưu → 2 bước đồng ý/modal → Hoàn tất → kết quả theo ảnh tham chiếu. Kiểm tra thiếu bảng/hóa đơn/dữ liệu, pending thanh toán, nháp khác, nguồn thay đổi, trạng thái phòng và phiên bản/fingerprint. Chủ nhà/khách thuê xem cùng bản bất biến; gửi trùng/đồng thời chỉ một lần thành công.
- Cùng transaction: PHAT_SINH vào hóa đơn cuối, phát hành/khóa snapshot và nguồn chỉ số, thông báo, CAN_TRU nợ cũ trước rồi kỳ cuối, DA_CHOT, dữ liệu người/tên/UTC/số tiền, hợp đồng DA_KET_THUC, phòng TRONG/lịch sử, tin NHAP và audit. Lỗi audit rollback tất cả. Không cộng hư hỏng/phạt hai lần sau khi đã gộp vào hóa đơn.
- Đã có ghi nhận hoàn cọc thực tế sau chốt (từng phần, chống trùng/vượt cọc), liên kết ghi thu hóa đơn còn nợ; cập nhật dư nợ/trạng thái của khách và quản lý hóa đơn để tính CAN_TRU mà không tính nó là tiền mặt đã thu. Bảng chốt giữ nguyên khi thu/hoàn sau đó.
- Schema v33 thêm xac_nhan_tat_toan và lich_su_trang_thai_phong; khóa tạo/phát hành sau DA_KET_THUC và hủy hóa đơn thuộc bảng chốt; giữ bước v31/v32, backup/integrity/FK/rerun, bảo toàn toàn bộ dòng v32 trên bản sao.
- Phát hiện bảng chi_so_dien_nuoc thực tế chỉ nhận kỳ trọn tháng. Không sửa CHECK/không tạo kỳ giả: chỉ số cuối kỳ lẻ lưu trong snapshot hóa đơn bất biến và khóa nguồn. Lần demo phát hiện lỗi đã rollback, bổ sung test task1→4 giữa tháng đạt.
- Build đạt, 189/189 test đạt (21 task4). Edge desktop/360px đạt toàn luồng, modal/kết quả không tràn, replay409, khách xác nhận/khách ngoài hợp đồng403, xem trạng thái Đã kết thúc và một dòng phạt. Ảnh/report: data/s404-task4-demo/20261015-task4-final/browser-check-d64d6978/ ignored.
- Demo cuối localhost:5254; data/s404-task4-demo/latest.txt trỏ bộ cuối. Hợp đồng1 đã chốt: hóa đơn cuối5.813.742đ gồm hư hỏng450k/phạt3.5m; nợ cũ1.2m; hai CAN_TRU tổng7m; còn thu13.742đ, một biên bản/tin nháp/lịch sử phòng, không tự thêm thanh_toan. Integrity/FK đạt. Hợp đồng2 còn hiệu lực để demo nhập từ đầu.
- DB local v5, demo task2 v31/task3 v32 giữ nguyên. Chưa PDF, chưa nghiệm thu Safari/thiết bị thật/review thành viên khác/hợp nhất. Hướng dẫn: docs/S4-04-xac-nhan-tat-toan-task4.md. Chưa commit/push.

### 15/10/2026 — S4-04 task 5: Xuất và tải bảng tất toán PDF
- Đã áp dụng mẫu A4 NhàTốt do PO xác nhận: hai bên/hợp đồng/phòng/ngày trả, toàn bộ dòng hóa đơn cuối, cọc, công nợ cũ, từng hư hỏng, lý do/phạt, kết quả, người/thời điểm xác nhận và trạng thái hợp đồng.
- GET /TatToan/TaiPdf/{hopDongId}: kiểm tra quyền module và sở hữu ở backend, chỉ xuất bảng DA_CHOT của hợp đồng đã kết thúc; attachment/no-store/nosniff. Nút tải cho Chủ nhà và Khách thuê ở banner và Tài liệu liên quan, theo theme ảnh mẫu và responsive 360px.
- Số tiền dùng snapshot đã chốt/chi tiết hóa đơn đã phát hành, không trừ hư hỏng/phạt hai lần; PDF giữ nguyên nội dung sau thu/hoàn tiếp theo. QuestPDF và font Lato sẵn có; không thêm thư viện/schema, không nâng cấp DB đang dùng.
- Build đạt; hồi quy 192/192, nhóm PDF đầy đủ 4/4 (bao gồm ca bổ sung tiền phòng theo ngày + điện nước). PdfPig mở/trích tiếng Việt có dấu và kiểm tra PDF nhiều trang. Edge desktop/360px: hai bên tải được, hai tài khoản ngoài hợp đồng bị 403. Chưa kiểm tra Safari/thiết bị thật.
- Demo mới: http://localhost:5255/TatToan/ChiTiet/1; tài khoản riêng trong data/s404-task5-demo/20261015-task5-verified/access.json. Tạo/chạy lại bằng verification/New-S404Demo.ps1 -Task5 và Start-S404Demo.ps1 -Task5 -Port 5255. Giữ nguyên các demo cũ và DB local.
- Chi tiết và lệnh kiểm thử: docs/S4-04-xuat-pdf-task5.md. Chưa commit/push/merge hoặc review bởi thành viên khác.
# 14/10/2026 — S4-07:2 chi phí sửa chữa

Theo phê duyệt mới, database demo chính S4-07 đã nâng v30→v31; dữ liệu cũ và backup v30 bảo toàn, integrity/FK và rerun đạt. Hoàn thiện service/POST/CSRF/quyền/phân công/transaction/chống trùng, chi tiết dùng chung ba vai trò và toast. Tích hợp bảo vệ snapshot hóa đơn nháp/hủy/thay thế, không tự đưa phí vào hóa đơn hay phát hành trên demo. Runtime launcher đã build mới tại data/s407/runtime. Báo cáo và AC/test: [docs/S4-07-02-chi-phi.md](docs/S4-07-02-chi-phi.md). Test ghi chỉ trên bản sao; chi phí demo chính vẫn0 dòng. Chờ nghiệm thu; không commit/push/merge.
# 14/10/2026 — S4-07:2 xử lý lỗi nghiệm thu

Đã sửa validation cập nhật ngay khi sửa tiền hợp lệ và hướng dẫn động theo bên chịu phí; server validation/phân quyền/schema giữ nguyên. 95/95 báo hỏng/chi phí PASS; hóa đơn162/164 PASS, hai lỗi nhãn được chạy lại trên baseline4364280 và cũng FAIL, không tự sửa ngoài phạm vi. Kiểm tra chống trùng/vòng đời hóa đơn PASS. SHA256 và snapshot mọi bảng database demo chính đầu/cuối khớp, dữ liệu nghiệm thu giữ nguyên. Runtime launcher đã build lại. Báo cáo: [docs/S4-07-02-xu-ly-loi.md](docs/S4-07-02-xu-ly-loi.md). Chờ nghiệm thu và quyết định hai lỗi hóa đơn cũ; không commit/push/merge.
# 14/10/2026 — S4-07:2 RC-01 trước commit

Sửa service chi phí để hỗ trợ database v31 chưa cài module hóa đơn: kiểm tra module trong transaction, bỏ truy vấn bảng không tồn tại khi module chưa cài, giữ khóa/chống trùng/kỳ phát hành khi có module; không tạo bảng hóa đơn. Thêm hai case tự tạo DB giả tái hiện FAIL trước sửa và kiểm tra lưu/sửa/lịch sử/replay/ba vai trò. Báo cáo tại docs/S4-07-02-chi-phi.md. Không tác động database demo gốc/backup, không sửa20FAIL cũ, không commit/push/merge; chờ duyệt.

# 15/10/2026 — S4-07:02 rà soát để người dùng tự commit

Sửa dấu ngoặc thừa khiến cài trigger hóa đơn v31 lỗi SQL; chuyển fixture schema chi phí sang dữ liệu giả tự tạo, không phụ thuộc backup ignored; bổ sung cách tính/đơn vị snapshot trên chi tiết hóa đơn. Thêm test lưu chi phí trước rồi cài module hóa đơn, giữ lịch sử và khóa nguồn đã liên kết. Không đổi công thức, không tự đưa phí vào hóa đơn, không chạy updater/seed trên demo.

Rebuild 0 lỗi; smoke4/4 PASS; full suite871ca: **853PASS/18FAIL/0SKIP**, gồm **98/98 báo hỏng/chi phí và164/164 hóa đơn/dịch vụ PASS**. Hai lỗi nhãn hóa đơn cũ đã hết; 18FAIL còn lại trùng danh sách lỗi lịch sử ở module khác, không sửa assertion để che lỗi. Chưa nghiệm thu lại UI/mobile/Safari/SMTP. Chi tiết lỗi, lệnh chạy, bằng chứng và giới hạn: [docs/S4-07-02-ra-soat-20261015.md](docs/S4-07-02-ra-soat-20261015.md). Giữ thay đổi có sẵn và project test; không stage/commit/push.

# 15/10/2026 — S4-07:03 cảnh báo báo hỏng Gấp quá 24 giờ

PO chốt tính từ lúc gửi, chỉ GAP + MOI + chờ >24 giờ; đúng24giờ chưa cảnh báo, dừng khi DANG_XU_LY/HOAN_THANH/TU_CHOI. Phản hồi không đặt lại giờ chờ. Danh sách và chi tiết Quản lý dùng cùng bộ tính theo ITimeProvider, nhãn “Gấp — Quá 24 giờ”, tổng giờ/phút chờ và viền/nền nổi bật; tải lại tự đánh giá theo giờ hiện tại. Giữ quyền/phân công, thứ tự và phân trang, không đổi schema hoặc ghi audit khi đọc.

Build/Razor đạt0lỗi; **114/114 test báo hỏng/chi phí PASS**, gồm16ca mới: mốc24giờ±1tick,26giờ, các trạng thái loại trừ, cập nhật theo giờ/đổi trạng thái, quyền403/CSRF400 và không ghi dữ liệu khi đọc. HTTP demo riêng5291 sáu ca đạt, DB nguồn/backup giữ hash. Không chạy lại full suite trong lát này. Có script tạo/chạy/kiểm tra demo độc lập; dữ liệu và tài khoản ignored. Chưa nghiệm thu UI desktop/360px/Safari vì automatic approval review chặn mở trình duyệt do hết hạn mức; không dùng công cụ khác vượt chặn. Bàn giao: [docs/S4-07-03-canh-bao-qua-24-gio.md](docs/S4-07-03-canh-bao-qua-24-gio.md). Nhánh kế thừa01+02; chưa stage/commit/push.

Cập nhật nghiệm thu UI15/10/2026: cùng công cụ trình duyệt đã hoạt động lại. Bản demo mới riêng `demo-20261015-044703-a7e78c`, desktop1366×900 và360×800: danh sách/chi tiết hiển thị cảnh báo đúng, scrollWidth1351≤1366 và345≤360. Nhận xử lý trên360px lưu thành công, có toast/lịch sử và cảnh báo biến mất ở cả hai trang. Ảnh bằng chứng ignored được ghi trong tài liệu bàn giao. Không đổi mã trong lượt UI, không chạm DB nguồn. Chưa kiểm tra Safari vì không có Safari kết nối; không coi giả lập360px là Safari thật. Chưa commit/push.

# 15/10/2026 — S4-07:04 lọc danh sách báo hỏng

Nhánh `feature/S4-07/04-repair-request-filters` tạo từ `a9c69a4`, chứa đủ01–03. Thêm bộ lọc GET tòa/trạng thái/mức độ kết hợpAND trước phân trang; chỉ tòa được giao, tòa ngoài phạm vi403, tham số sai400. Nhãn Đã xong ánh xạHOAN_THANH. Giữ cảnh báo quá24giờ, điều kiện/trang khi mở chi tiết/quay lại/phân trang/validation/lưu trạng thái/phản hồi/chi phí. Có thông báo rỗng và Xóa bộ lọc về mặc định. Không đổi schema hoặc nghiệp vụ tiền/trạng thái/CSRF.

Build/Razor đạt0lỗi; **125/125testPASS** (114hồi quy+11mới), không skip. HTTP demo5292 với6truy vấn lọc đạt, GET không đổi snapshot nghiệp vụ/audit. Đã nghiệm thu trực quan desktop1366×900 và360×800: lọcTòaA/Mới/Gấp→đúng1kết quả+cảnh báo, detail/back giữ lựa chọn, tòa trống hiện thông báo, xóa lọc về10thẻ mặc định; không tràn ngang. Chạy lại full suite15/10/2026: **898ca,880PASS/18FAIL/0SKIP**,125ca báo hỏng đềuPASS; đối chiếu TRX với baseline853/871 xác nhận18FAIL trùng hoàn toàn, không có lỗi mới. Chưa Safari vì danh sách trình duyệt khả dụng không có kết nối Safari; cần macOS/iOS, không coi360px là Safari. Lượt cuối không tạo/chụp thêm ảnh theo yêu cầu. Demo riêng/ảnh cũ/TRX/credential ignored; DB nguồn vàbackup giữ hash, không chạm local. Chưa đủ toàn bộ DoD/Safari, chưa stage/commit/push. Chi tiết/lệnh chạy/test: [docs/S4-07-04-loc-bao-hong.md](docs/S4-07-04-loc-bao-hong.md).

# 15/10/2026 — Hòa giải S4-07 với dev S4-04

Nhánh tích hợp riêng `codex/s407-dev-reconcile` gộp devedef5ea và feature43f8998, giữ mapping/nghiệp vụ cả tất toán và báo hỏng. Giải quyết7file conflict; v34 nhận diện hai lịch sử v31 và cài module còn thiếu mà không đổi marker/dữ liệu cũ. Fixture giữ cả hai nhóm helper; các test nâng cấp liên quan kỳ vọng34. Build đạt0lỗi,6/6test hòa giải đạt,382hồi quy có381PASS/1FAIL Login500 (chạy riêng lại1/1PASS); nhóm còn lại609ca có589PASS/20FAIL, gồm18baseline+2fixture tạo hóa đơn sau kết thúc hợp đồng. Đã sửa thứ tự dữ liệu hai fixture, giữ guard/assertion, nhóm kiểm tra cuối52/52PASS. Phủ991ca khác nhau; tổng hợp kết quả cuối từng ca973PASS/18FAIL trùng baseline, không phải một full suite xanh; Login500 cần theo dõi. Không sửa18lỗi cũ ngoài phạm vi, không đổi DB local/demo thật, không tạo ảnh/không nghiệm thuSafari, chưa commit/push/mergedev. Chi tiết: [docs/S4-07-dev-schema-v34.md](docs/S4-07-dev-schema-v34.md).

# 15/10/2026 — Chuyển tích hợp về S4-07:04 và xử lý18lỗi baseline

Theo yêu cầu người dùng, toàn bộ phần hòa giải đã kiểm thử được chuyển từ worktree thử về `feature/S4-07/04-repair-request-filters`, gộp devedef5ea, giữ cảS4-04/S4-07. Người dùng yêu cầu sửa cả18lỗi cũ: sửa lưu lại tài liệu bản nháp (giữ tệp/backup/rollback/bất biến sau hiệu lực), phục hồi tin mới nhất trang chủ, badge yêu cầu/menu tài khoản và nhãn quá hạn từng dòng; sửa fixture/schema/selector theo chức năng hiện hành, giữ các assertion nghiệp vụ/quyền. Thêm2test bảo toàn tài liệu/rollback. Các ca lỗi nhỏ đã đạt khi kiểm tra lại, fullsuite đang chạy. Không tạo/chụp ảnh hoặc sửa DB thật, chỉ đẩy nhánhS4-07:04 sau khi kiểm tra hoàn tất. Báo cáo: [docs/S4-07-04-tich-hop-va-sua-18-loi.md](docs/S4-07-04-tich-hop-va-sua-18-loi.md).

Cập nhật kiểm thử cuối15/10/2026: fullsuite trên S4-07:04 tích hợp devedef5ea đạt **993/993PASS,0FAIL/0SKIP**, toàn bộ18ca baseline đãPASS. Không sửa mã khi chạy; kết quả tại data/s407-final-fixes/full-final.trx ignored. Theo yêu cầu tiếp theo, lưu phần sửa vào commit cục bộ S4-07:04 rồi chuyển thư mục chính sang dev mới nhất a934630; không coi test993ca là test bản S4-05 mới.

# 15/10/2026 — Người dùng yêu cầu tích hợp và push dev

Merge S4-07 commit903fe8a vào deva934630 có S4-05 task1–4. Một conflict test tin đăng được giải bằng selector/form/assertion cụ thể từ dev. Build đạt, hồi quy tích hợp **575/575PASS,0FAIL/0SKIP**; đầy đủ báo hỏng, nhóm hóa đơn/tất toán/thu-hủy/giải phóng phòng/tạo Nháp, hợp đồng và đăng Nháp/danh sách phòng trống/dashboard. Không thay DB thật hoặc tạo ảnh; chưaSafari, không coi575ca là fullsuite devmới. Báo cáo: [docs/S4-07-dev-push-20261015.md](docs/S4-07-dev-push-20261015.md). Push duy nhấtdev theo yêu cầu, khôngforce/tag.

# 16/10/2026 — Hoàn thiện tiêu chí S4-01 đến S4-09

Theo yêu cầu tự sửa và hoàn thiện: thêm bước schema **v37** hòa giải các nhánh v35/v36, giữ bước cũ và dữ liệu; nối báo trả của khách với xác nhận thu đúng bản ghi, có replay/kiểm tra dư nợ/transaction/audit; bổ sung ảnh chứng từ cho form hóa đơn cũ. Sửa công nợ theo phạm vi chủ nhà, tên hồ sơ, cấn cọc, ngày Việt Nam và CSV dùng chung truy vấn. Bổ sung kỳ/tổng/còn trả trong email và khóa tác vụ nhắc hạn chống chạy trùng. Sửa header nhân viên không tràn ở 360px. Không sửa project test, không stage/commit/push.

Nâng bản sao mới của DB local **v27 → v37**, giữ nguyên **3.922 dòng / 40 bảng cũ**, integrity/FK đạt; chạy lặp không đổi bytes và hash DB nguồn không đổi. Script tái lập: `python verification/Verify-Sprint4Upgrade.py QL_PhongTro/Data/local-dev.sqlite data/<thu-muc-moi>/copy.sqlite` sau build Debug. **DB local thật vẫn v27**, không chạy updater trên file đó.

Build/Razor đạt. Nhóm kiểm tra mới và migration liên quan **13/13 PASS**; bổ sung ca HTTP quyền xác nhận của chủ nhà/khách/quản lý/admin và quyền bị thu hồi **1/1 PASS**. Fixture công nợ chạy lại **1/1 PASS**, có 5 trả thiếu + 2 phòng quá hạn hai kỳ, tổng nợ 2.450.000 đ, kiểm tra lọc/tổng/CSV/quyền. Báo cáo doanh thu **14/14 PASS**, gồm benchmark 500 phòng ×12 tháng dưới 3 giây cho service và HTTP đã warm-up. Hồi quy toàn dự án **1.127PASS/2FAIL/0SKIP trên 1.129ca**, 18 phút 42 giây; hai FAIL là assertion phiên bản cũ trong LichHenMigration, đã sửa theo v37 và chạy lại PASS. Gộp kết quả cuối từng ca của full run + chạy lại/bổ sung: **1.130 ca khác nhau đều PASS**, không coi đây là một full suite xanh duy nhất. Bốn TRX và `final-test-summary.json` trong `data/s4-fix/` (ignored). Sau hồi quy, kiểm tra toàn bộ schema/dòng local vẫn khớp backup trước nâng cấp bản sao, phiên bản local vẫn v27.

Edge headless đã chạy thật luồng gửi ảnh → chủ nhà xem/xác nhận → cập nhật nợ; báo hỏng → quản lý Hoàn thành → phí khách kỳ sau; trả phòng → chốt → phòng trống → tin Nháp → đăng → dashboard cập nhật. Đăng tin bằng một nút **0,63 giây**; ảnh/mô tả giữ nguyên, giá phòng hiện tại. Tải PDF tất toán cho chủ nhà/khách đạt; người ngoài 403, chốt lặp 409. Desktop/360px đạt, có ảnh/JSON trong các demo ignored ở `data/s4-fix/`. Các server demo đã dừng. Chưa Safari/iOS, thiết bị vật lý, SMTP/inbox thật, 3G; chưa review/merge bởi thành viên khác. Chi tiết AC, lệnh và giới hạn: [docs/hoan-thien-tieu-chi-S4-20261016.md](docs/hoan-thien-tieu-chi-S4-20261016.md).

## 16/10/2026 — Khắc phục run.bat báo local v27, yêu cầu v37

Sau khi người dùng báo lỗi chạy, kiểm tra không có app/cổng 5247 đang chạy, sao lưu DB ra `%TEMP%/NhaTot-database-backups/20261016-114109/local-dev.before-v37.sqlite`, rồi chạy updater trên đúng DB local. Local hiện **v37**, giữ nguyên toàn bộ giá trị của **3.922 dòng / 40 bảng cũ**, integrity_check=ok, foreign_key_check không lỗi. `run.bat --check-only` exit 0, báo Database schema is ready. Không đổi tài khoản/mật khẩu, không tự mở server hoặc gửi email. Biên bản/log trong `data/local-upgrade-20261016-114109/` (ignored). Ghi chú local vẫn v27 ở mục trước đã được thay thế bởi cập nhật này.

## 16/10/2026 — Chuyển sang DB nghiệm thu mới theo yêu cầu

Tạo DB giả mới bằng wrapper `verification/New-Sprint4AcceptanceDemo.py`, nhận credential từ JSON private, không ghi email/mật khẩu người dùng vào mã. Dùng hai tài khoản chủ nhà/khách thuê được cung cấp, áp dụng email khách thuê đã đính chính; thêm quản lý/ADMIN và tài khoản ngoài phạm vi để thử phân quyền. Bộ dữ liệu có 500 phòng, 480 hợp đồng, 5.738 hóa đơn trong 12 tháng, 5 ca trả thiếu/2 ca quá hạn nhiều kỳ, công nợ ban đầu 24.700.000 đ, 5 báo hỏng đủ trạng thái, hai hợp đồng cho luồng trả phòng/tất toán và các tin Nháp/phòng trống.

`.env.local` (ignored) đã trỏ DatabasePath sang `data/s4-acceptance/20261016-115246-fb2642/s404.sqlite`, cùng key riêng và email pickup để không gửi inbox thật. DB cũ giữ nguyên; backup DB/cấu hình ngoài repo tại `%TEMP%/NhaTot-database-backups/switch-s4-20261016-115334/`. `run.bat --check-only` PASS, integrity/FK đạt. Đăng nhập/HTTP các trang chính 4 vai trò PASS; báo cáo 12 tháng/500 phòng đo sau làm nóng 0,428 giây. Web chạy localhost:5247; thông tin tài khoản bổ sung, hướng dẫn từng tiêu chí và số liệu mẫu trong thư mục dữ liệu ignored. Không commit DB/credential, không sửa tài khoản trong DB cũ.
# 15/10/2026 — S4-07 giao diện Quản lý theo ảnh tham chiếu

Nhánh `feature/S4-07/ui-improvements` từ dev8e4eca4. Thêm bốn thẻ thống kê toàn phạm vi, banner ưu tiên trong kết quả lọc, panel bộ lọc/tab trạng thái/bảng sáu cột/badge/phân trang. Giữ query, quyền backend, quy tắc GAP + MOI + chờ >24 giờ và luồng xử lý; không đổi schema hoặc giao diện Chủ nhà. CSS riêng, ở760px chuyển bảng thành thẻ;360px không tràn ngang (scrollWidth345). Build đạt, **133/133 test báo hỏng PASS**, gồm hai test mới cho thống kê/tab/phạm vi/phân trang. Trình duyệt desktop1366px và360px: lọc đúng một dòng, detail/back giữ query, tab rỗng và xóa lọc đạt. Không tạo/chụp ảnh; chưa Safari hoặc chạy lại full suite. Demo5247 đã restart bằng runtime UI riêng, giữ dữ liệu. Chưa stage/commit/push. Chi tiết: [docs/S4-07-ui-improvements.md](docs/S4-07-ui-improvements.md).

# 15/10/2026 — Giao diện chi tiết báo hỏng cho Quản lý

Theo ảnh tham chiếu của người dùng, thêm partial `_ManagerDetail.cshtml` cho vai trò Quản lý, CSS/JS riêng chỉ nạp tại chi tiết quản lý. Bố cục desktop hai cột: sự cố/lịch sử/phản hồi bên trái, chi phí/cập nhật trạng thái bên phải; màn hình nhỏ chuyển một cột. Giữ dữ liệu động, các action POST, tên trường, CSRF, phiên bản, khóa chống trùng, query quay lại danh sách, điều kiện khóa chi phí/quyền ghi và trạng thái cuối. Ảnh tiếp tục dùng endpoint có quyền; khi tải lỗi hiện ô dự phòng. Không sửa controller/service/model/schema trong lượt này; các thay đổi backend có sẵn từ task danh sách được giữ nguyên. Điều chỉnh selector test liên kết quay lại theo nhãn mới, giữ các assertion quyền/bộ lọc.

Build Debug/Razor cuối đạt 0 lỗi (cảnh báo ImageSharp/CS8601 có sẵn). Kiểm tra trình duyệt trên CSDL demo sao chép riêng cổng 5296: desktop 1280px hiển thị hai cột, scrollWidth 1265; 360px hiển thị một cột, scrollWidth 345; mở lịch sử chi phí và xem form chi phí trên mobile đạt. Chưa Safari/thiết bị thật, không chạy full suite. Demo nguồn v31 được nâng v34 chỉ trên bản sao mới `data/manager-detail-ui/demo/detail.sqlite`, không nâng DB local hoặc DB demo nguồn. Lệnh build: `dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore -o data/manager-detail-ui/runtime`. Hồi quy báo hỏng đạt **133/133 PASS, 0 FAIL/0 SKIP** (`dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore -o data/manager-detail-ui/tests --filter FullyQualifiedName~BaoHong`); lần đầu sandbox chặn socket testhost, chạy lại ngoài sandbox đạt. Build cuối sau chỉnh icon lịch sử đạt; trình duyệt xác minh cả bản Hoàn thành có chi phí và bản Đang xử lý có form cập nhật, nút quay lại danh sách hoạt động. Chưa stage/commit/push.

## 15/10/2026 — Chỉnh lại riêng thẻ Thông tin sự cố theo ảnh mới

Tăng phân cấp chữ/biểu tượng, padding và khoảng cách của thẻ; người gửi thẳng cột thứ hai (cột 1/3), hàng thiết bị/mức độ/thời điểm ba cột; khung phòng nền xám và hàng ảnh cao 108px với biểu tượng mở ảnh SVG. CSS container query điều chỉnh theo chiều rộng thẻ, trên 360px tự chia hai cột và dành hàng riêng cho thời điểm/hành động ảnh. Chỉ sửa partial giao diện và CSS, không sửa backend/DB. Ảnh tải được vẫn hiển thị; lỗi tải dùng ô dự phòng sẵn có, không giả trạng thái lỗi theo ảnh mẫu.

Build Debug/Razor đạt 0 lỗi, giữ cảnh báo ImageSharp/CS8601 cũ. Kiểm tra trình duyệt desktop 1440px và mobile 360px: không tràn ngang (scrollWidth 1425 và 345). Demo cổng 5296 đã khởi động lại bằng runtime mới. Không chạy lại bộ 133 test vì thay đổi chỉ markup/icon/CSS; kết quả 133/133 ở lượt trước không được tính là lần chạy mới. Chưa Safari/thiết bị thật. Chưa commit/push.

## 15/10/2026 — Thêm đường phân cách và thu gọn đầu thẻ sự cố

Chỉ chỉnh `wwwroot/css/manager-repair-detail.css`: thêm đường dọc giữa các ô metadata, đường ngang giữa hai hàng; trên thẻ hẹp thời điểm xuống hàng riêng với đường phân cách phù hợp. Thu tiêu đề Thông tin sự cố 24→18px, biểu tượng 48→32px, khung phòng tối thiểu 70→44px và giảm padding/khoảng cách đầu thẻ. Giữ nguyên toàn bộ nội dung động và backend, không sao chép nội dung tài chính từ ảnh tham chiếu.

Kiểm tra trực tiếp demo 5296 trên desktop 1440px và mobile 360px đạt; đường dọc 1px, tiêu đề 18px, scrollWidth 1425/345, không tràn ngang. CSS phục vụ trực tiếp từ wwwroot, đã tải lại trang; không cần build/restart hoặc chạy lại test nghiệp vụ cho chỉnh CSS này. Chưa Safari/thiết bị thật. Chưa commit/push.

## 15/10/2026 — Sửa thiếu Báo hỏng của tôi trong menu khách thuê

Thêm liên kết BaoHong/Index ngay sau Hợp đồng của tôi trong `_TenantHeader.cshtml`, chỉ hiển thị khi `PermissionService.AllowsAsync("BAO_HONG")` cho phép; dùng Active("BaoHong") để đánh dấu tại danh sách/chi tiết/tạo mới. Giữ nguyên controller/service/phân quyền backend/schema và dữ liệu. Không thêm mục vào thanh điều hướng ngang để tránh chật header.

Build Debug/Razor đạt 0 lỗi (cảnh báo ImageSharp/CS8601 cũ). Demo 5296 khởi động lại bằng runtime mới. Kiểm tra bằng khách thuê demo: mở menu từ trang chủ thấy liên kết; bấm vào tới /BaoHong, mục menu có is-active; bấm Gửi báo hỏng mở /BaoHong/Create đủ biểu mẫu. Desktop 1440px và mobile 360px hiển thị đúng, menu nằm trong viewport (cạnh phải 252px trên 360px), scrollWidth 345px. Không gửi báo hỏng thử/không đổi quyền DB để kiểm tra; nhánh ẩn khi không có quyền dùng predicate hiện hữu đã được đối chiếu mã. Không chạy lại full suite hoặc 133 test cho thay đổi view nhỏ này; chưa Safari. Chưa commit/push.

## 15/10/2026 — Giao diện danh sách báo hỏng khách thuê theo ảnh

Thay `Views/BaoHong/Index.cshtml` bằng breadcrumb/tiêu đề/nút Gửi báo hỏng, thanh thống kê bốn trạng thái, bảng sáu cột (thiết bị+mã, phòng+tòa, trạng thái, mức độ, thời điểm Việt Nam, xem chi tiết), footer. CSS/JS riêng `tenant-repair-list.*` chỉ nạp cho trang danh sách khách thuê. Dữ liệu/nhãn/ngày giờ động, không dùng số liệu hardcode. Mobile <=760px chuyển bảng thành thẻ. Giữ nguyên backend, quyền/sở hữu/CSRF, schema và dữ liệu, endpoint tạo/chi tiết và phân trang 20+1 hiện hữu. Bộ lọc JS theo mã/thiết bị/phòng/tòa (không phân biệt dấu/hoa thường), trạng thái và mức độ áp dụng kết hợp trên trang hiện tại. Khi có nhiều trang, nhãn thống kê/tổng và ghi chú nêu rõ phạm vi tối đa20 bản ghi; không trình bày tổng trang thành tổng toàn hệ thống. Không JS vẫn xem danh sách/phân trang; điều khiển lọc chỉ hiện khi JS chạy.

Build cuối đạt0lỗi, cảnh báo ImageSharp/CS8601 cũ. Demo5296 restart runtime mới. Browser desktop1440px:11bản ghi, thống kê Mới4/Đang xử lý2/Hoàn thành3/Từ chối2; lọc Hoàn thành ra3, thêmGấp+từ khóa không dấu VOI NUOC ra2, từ khóa không tồn tại ra0+thông báo rỗng, Xóa bộ lọc về11, mã0002 ra1 và mở chi tiết đúng. Mobile360px: thẻ đầy đủ trường, scrollWidth345; desktop scrollWidth1425. Trang2rỗng có thông báo+quay lại trang trước; nút Gửi báo hỏng mở biểu mẫu hiện hữu. Đã sửa lỗi Razor @page và ID biểu tượng tìm kiếm trùng ô nhập trong quá trình kiểm tra. Không tạo báo hỏng thử, không đổi dữ liệu. Không chạy lại full suite/133test vì thay đổi giao diện; chưa Safari/thiết bị thật và chưa kiểm tra tập >20 bản ghi bằng browser. Chưa commit/push.

# 16/10/2026 — Đồng bộ giao diện báo hỏng Chủ nhà theo mẫu trước

Danh sách Chủ nhà dùng partial `_ReportList.cshtml` chung với khách thuê: thống kê động, bảng/badge, tìm kiếm/lọc trên trang, phân trang giữ nguyên. Nội dung dành cho Chủ nhà, không có nút tạo báo hỏng. Chi tiết dùng bố cục thông tin sự cố/đường phân cách/ảnh/lịch sử và chi phí theo mẫu Quản lý, liên kết danh sách/ảnh qua ChuNhaBaoHong. Form phản hồi/cập nhật trạng thái chỉ render cho LaQuanLy; Chủ nhà không có form ghi. Chỉ thay view/CSS, không sửa controller/service/schema trong lượt này. Thêm thu gọn header tài khoản trên mobile chỉ tại danh sách Chủ nhà để tránh min-width gây tràn.

Build Razor đạt0lỗi (ImageSharp/CS8601 cũ). Demo5296 restart. Browser Chủ nhà desktop1440:11bản ghi, lọc Hoàn thành ra3, mở chi tiết0002 có thông tin/lịch sử/chi phí,0form, ảnh URL ChuNhaBaoHong/Image; quay lại danh sách đúng. Mobile360: chi tiết scrollWidth354 và danh sách sau sửa header355 <=360. Không gửi form/đổi DB; chưa Safari/full suite. Khách thuê dùng partial chung giữ luồng hiện hữu, chưa kiểm tra lại browser khách sau tách partial. Chưa commit/push.

# 16/10/2026 — Kiểm thử lại toàn bộ phạm vi S4-07 sau chỉnh giao diện

Theo yêu cầu người dùng, build và chạy toàn bộ nhóm BaoHong trên mã hiện tại: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s407-ui-retest/runtime --filter FullyQualifiedName~BaoHong --logger "trx;LogFileName=s407.trx" --results-directory data/s407-ui-retest/results`. Kết quả mới **133/133 PASS,0FAIL,0SKIP**, thời gian test1phút50giây. Build/Razor0lỗi; cảnh báo có sẵn NU1900 (không truy cập nguồn cảnh báo NuGet), ImageSharp license, CS8601. Phạm vi toàn bộ12file test BaoHong: tạo/ảnh/đọc, trạng thái/ghi chú/phản hồi/lịch sử, chi phí/hóa đơn/schema/hòa giải,24giờ,bộ lọc/phân trang/tổng quan, quyền/CSRF/chống trùng/đồng thời/rollback. Đây là toàn bộ nhóm báo hỏng, không phải full suite cả dự án.

Smoke HTTP và browser dùng bản sao mới `data/s407-ui-retest/demo/retest.sqlite`, cổng5297, không tác động DB nguồn. Browser Quản lý: lọc tòaA+MOI+GAP ra1, nhận xử lý có ghi chú thành công và cảnh báo biến mất; gửi phản hồi, hoàn thành có ghi chú, lưu250.000đ KHACH_THUE thành công, trạng thái chờ hóa đơn kỳ11/2026. Quay lại danh sách giữ ba điều kiện lọc và hiện rỗng vì báo đã hoàn thành. Mobile360px danh sách/chi tiết scrollWidth345. HTTP mới xác minh cả3vai trò thấy cùng lịch sử/chi phí, chủ nhà/khách không có form ghi; quản lý ngoài phạm vi403, filter sai400, bản đã xử lý không còn trong lọcMOI+GAP. DB bản sao integrity_check=ok,foreign_key_check rỗng; cờ chờ thu=1,tiền250000. SHA256 DB demo nguồn không thay đổi. Chứng cứ ignored: `data/s407-ui-retest/results/s407.trx`, `data/s407-ui-retest/demo/http-smoke.json`, script `data/s407-ui-retest/http-smoke.py`.

Không phát hiện lỗi trong phạm vi kiểm tra; không sửa backend/giao diện trong lượt test này. Chưa Safari/thiết bị vật lý/tải đồng thời thực tế/full suite toàn dự án. Không stage/commit/push. Web kiểm thử tách riêng; cổng5296 được mở lại để người dùng dùng dữ liệu demo cũ.

## 16/10/2026 — Bổ sung nút Áp dụng cho bộ lọc báo hỏng Chủ nhà/Khách thuê

Partial danh sách chung có form lọc với nút Áp dụng; JS chặn submit mặc định và lọc tại trình duyệt khi bấm nút hoặc Enter. Bỏ tự lọc lúc nhập/chọn để thao tác nhất quán, giữ Xóa bộ lọc khi rỗng và phạm vi trang hiện tại. Chỉ sửa view/CSS/JS, không thay backend. Build0lỗi; browser Chủ nhà chọn Hoàn thành vẫn11dòng trước áp dụng, bấm nút ra3, mã0002+Enter ra1. Mobile360px scrollWidth355; nút hiển thị đầy đủ. Web5296 restart bản mới. Không chạy lại133test vì thay đổi UI cục bộ; chưaSafari. Chưacommit/push.

## 16/10/2026 — Thu gọn tổng quan báo hỏng Quản lý

Theo ảnh người dùng, chỉ chỉnh CSS manager-repairs.css: tiêu đề23px, số thống kê28px, biểu tượng28px, giảm padding/gap/margin và cảnh báo. Browser1440px: thẻ cao98px, banner62px; mobile360px scrollWidth345 không tràn ngang. CSS áp dụng trực tiếp trên web5296, không đổi backend/nội dung/bộ lọc hoặc build lại cho thay đổi CSS. Không chạy lại test nghiệp vụ, chưaSafari. Chưacommit/push.

## 16/10/2026 — Bộ lọc báo hỏng Quản lý theo kiểu danh sách phòng

Chỉ sửa `_ManagerList.cshtml` và manager-repairs.css: thứ tự tiêu đề→tab→bộ lọc→bảng, nhãn inline trong ô chọn, viền/bo góc/chữ/nút trắng tương tự danh sách phòng. Giữ nguyên ba name/query, formGET, nút Áp dụng và Xóa, không thêm search/sort chưa có backend. Build0lỗi, browser1440px hiển thị đúng; lọc tòaA+MOI+GAP ra1;360px scrollWidth345. Web5296 restart. Không sửa backend/DB. Không chạy lại toàn bộ133test cho đổi markup/CSS này; chưaSafari. Chưacommit/push.

## 16/10/2026 — Đồng bộ chi tiết báo hỏng Khách thuê

Theo yêu cầu chỉ giao diện: Details.cshtml khách thuê dùng chung partial chi tiết với Chủ nhà/Quản lý, nạp CSS và xử lý fallback ảnh hiện có. Bố cục hai cột thông tin/ảnh/lịch sử và chi phí, tiêu đề gọn, đường phân cách metadata; mobile xếp một cột. Partial chọn đường dẫn BaoHong cho khách thuê, giữ nguyên điều kiện hiển thị form theo vai trò. Không sửa backend/schema/DB nghiệp vụ.

Build Razor thành công0lỗi (cảnh báo ImageSharp có sẵn). Browser khách thuê demo5296 chi tiết0002: ảnh tải đúng qua BaoHong/Image, thông tin/lịch sử/350.000đ và kỳ dự kiến hiển thị;0form thao tác quản lý, liên kết danh sách đúng BaoHong. Desktop1440 và mobile360: scrollWidth1425/345, không tràn ngang. Web demo đã khởi động lại bản mới. Không chạy lại full test nghiệp vụ vì đổi view/CSS; chưaSafari. Chưa commit/push.

## 16/10/2026 — Kiểm tra trước khi đẩy nhánh giao diện S4-07

Theo yêu cầu đẩy riêng nhánh feature/S4-07/ui-improvements lên origin (Anh49132/TTCS_T926_K12C3_N1). Fetch xác nhận dev đã có cập nhật mới; giữ nguyên nền nhánh hiện tại, không gộp vào dev. Rà soát 21file của thay đổi báo hỏng, không đưa DB/data/credentials hoặc project test csproj vào commit. diff --check đã xử lý dòng trống cuối partial.

Chạy `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s407-prepush/runtime --filter FullyQualifiedName~BaoHong --logger "trx;LogFileName=s407-prepush.trx" --results-directory data/s407-prepush/results`: build thành công, **133/133 PASS,0FAIL,0SKIP**,2phút04giây. Cảnh báo NU1900/ImageSharp có sẵn. Không chạy full suite dự án hoặc kiểm tra tương thích tích hợp với dev mới. Tài liệu phạm vi cuối đã cập nhật docs/S4-07-ui-improvements.md.

## 16/10/2026 — Gộp giao diện S4-07 vào dev theo yêu cầu

Gộp origin/feature/S4-07/ui-improvements (0784cf5) vào dev928b2e2. Chỉ xung đột tien-do.md, giải bằng giữ đủ nội dung hai nhánh; mã nguồn merge tự động, không đổi schema/DB. Build Razor đạt. Test filter BaoHong|InvoiceNotification|NhacHan trên output data/s407-dev-ui-merge/runtime:133test BaoHong và11test khácPASS;5test NhacHanIntegration lỗi tìm seed vì giả định output cách repo5cấp. Chạy lại toàn bộ11test NhacHan bằng output data/s407-dev-ui-merge/bin/Debug/net10.0:11/11PASS, gồm đủ5ca trước lỗi. Tổng149ca phân biệt đãPASS sau chạy lại đúng đường dẫn; không sửa mã test/production để lách lỗi. TRX ignored tại data/s407-dev-ui-merge/results. Chưa fullsuite/Safari, không restart web hoặc nâng DB trong lượt merge. Commit merge và push dev theo yêu cầu, giữ nhánh feature riêng.
### 17/10/2026 – Thiết kế giao diện báo cáo S4-09 theo mẫu
- `/BaoCao` có bộ lọc toàn chiều ngang, đặt lại, giới hạn 12 tháng, 5 thẻ tổng hợp, biểu đồ 6 tháng gồm phát hành/đã thu/còn phải thu và chú giải số tiền chính xác khi trỏ/đặt tiêu điểm; biểu đồ vòng lấp đầy cuối kỳ, bảng tháng mới nhất trước và bảng theo tòa đặt cạnh nhau trên màn lớn. Responsive 360px; bảng rộng cuộn trong khung.
- Bổ sung số đã thu/còn phải thu vào dữ liệu biểu đồ bằng cùng hàm tổng hợp bảng tháng; giữ thanh toán xác nhận/cấn cọc tách biệt. Chặn tháng kết thúc sai trước khi tính khoảng mặc định, sửa nhãn chọn tháng hiển thị biểu thức Razor. Không đổi schema/DB đang dùng hoặc quyền.
- Build và 14/14 kiểm thử báo cáo đạt, gồm đối chiếu ba dãy biểu đồ, quyền, lọc và benchmark HTTP 500 phòng/12 tháng dưới 3 giây. TRX `data/revenue-dashboard-tests/revenue-dashboard-final.trx`. Edge trên bản sao kiểm tra desktop1672/360px không tràn ngang; tải báo cáo 12 tháng khoảng734/325ms tại máy kiểm thử, không phải cam kết mạng thực. Ảnh mặc định6tháng đã rà: `data/repair-upgrade-qa/browser-check-2a295caa`; bộ chạy `verification/RevenueDashboardBrowser`. Server QA đã dừng. Chưa kiểm tra Safari/thiết bị thật/full suite, chưa push.
### 17/10/2026 – Popup chatbot Gemini trên nhánh feature/plus/chatbot
- Thêm Trợ lý AI cho tài khoản đã đăng nhập: câu hỏi tự do, ngữ cảnh tạm server, gợi ý theo vai trò, liên kết nghiệp vụ, responsive360px. Gemini tự gọi công cụ đọc hóa đơn/công nợ, báo hỏng, phòng và kỳ hợp đồng; backend kiểm tra tài khoản/quyền/module/sở hữu, chặn quản lý xem tài chính. Không cho AI chạy SQL hoặc ghi nghiệp vụ.
- `run.bat` nhận Gemini__ApiKey/Gemini__Model từ `.env.local` bị ignore; khóa chỉ ở header server→Google, không xuất hiện frontend. Không đổi schema/DB/tài khoản. CSRF/no-store, 2.000 ký tự/câu, 10 câu/phút/tài khoản, hội thoại30phút, xử lý quota/timeout và giữ câu hỏi để thử lại.
- Build/10kiểm thử mới đạt (`data/chatbot-tests/chatbot-final.trx`). Edge gọi Gemini thật trên bản sao đạt hội thoại khách/nối tiếp, chặn quản lý tài chính, chủ nhà tổng hợp phòng theo tòa; desktop/360px, reset/Escape đã kiểm tra và xem ảnh (`data/chatbot-qa/browser-check-562c7346`). Có timeout thử nghiệm ban đầu, không cam kết độ ổn định API. Server QA đã dừng. Chưa full suite/Safari/thiết bị thật/push. Hướng dẫn và giới hạn: `docs/chatbot-gemini.md`.

### 18/10/2026 – Đồng bộ thanh đầu trang trên toàn bộ giao diện Admin
- Nạp CSS Admin tại layout chung thay vì riêng Tổng quan; dùng chung nút 3 gạch, tiêu đề, ô tìm kiếm bên cạnh chuông và tài khoản trên mọi trang Admin. Giữ kích thước/căn hàng của Tổng quan và tên trang hiện tại; không tác động bố cục vai trò khác.
- Build Razor thành công, 0 lỗi (cảnh báo NU1900/ImageSharp/nullable có sẵn). HTTP 200 và kiểm tra header/CSS duy nhất đạt ở Tổng quan, tài khoản, phân quyền, nhật ký, tòa nhà và báo cáo; trang chủ nhà không nạp CSS Admin. `/BaoHong` trả 403 theo quyền hiện tại. `git diff --check` đạt. Chưa kiểm tra trực quan trình duyệt/mobile/Safari; không đổi nghiệp vụ, schema hoặc DB. Chưa commit/push thay đổi mới.

### 18/10/2026 – Dropdown Bootstrap bộ lọc tài khoản Admin
- Hai bộ lọc Vai trò/Trạng thái dùng Bootstrap Dropdown: cao 44px, bo góc, viền và focus vàng nhẹ, menu có bóng và tô mục đang chọn. Đồng bộ select gốc để giữ query lọc và lựa chọn từ server; khi JavaScript không chạy vẫn dùng select gốc.
- Build đạt 0 lỗi (cảnh báo cũ); node --check và git diff --check đạt. Chưa kiểm tra trực quan/mobile. Không đổi nghiệp vụ hoặc schema/DB; chưa push.

### 18/10/2026 – Bootstrap trang Đổi vai trò tài khoản
- Thẻ Bootstrap rộng tối đa 600px, biểu tượng và tiêu đề, vùng tên tài khoản, khung lưu ý, dropdown vai trò và nhóm nút cao 44px; khoảng cách thống nhất, nút xếp dọc trên màn nhỏ. Giữ nguyên POST/CSRF/Id/OriginalRole và nghiệp vụ.
- Build đạt 0 lỗi (cảnh báo cũ), node --check và git diff --check đạt. HTTP GET trang đổi vai trò đạt 200, kiểm tra trường form/CSRF và tài nguyên giao diện; không gửi đổi vai trò thật. Chưa xác minh trực quan/mobile/Safari. Không đổi schema/DB, chưa push.

### 18/10/2026 – Căn khoảng cách trang Đổi vai trò
- Giảm padding thẻ về 20px, giữ 16px trên mobile; khoảng cách các khối 16px, dòng nội dung 1.5, tên tài khoản cách nhãn 4px. Nhóm nút cách đường phân cách 16px, giữ chiều cao 44px.
- Chỉ sửa CSS, kiểm tra git diff --check; chưa kiểm tra trực quan trình duyệt. Không đổi chức năng hoặc DB.

### 18/10/2026 – Bootstrap trang Tạo tài khoản Admin
- Đồng bộ thẻ 600px, biểu tượng/tiêu đề và khung hướng dẫn với Đổi vai trò; ô nhập cao 44px, placeholder/autocomplete, dropdown Bootstrap vai trò, nhóm nút và khoảng cách 16px. CSS riêng trang, nút toàn chiều ngang trên mobile.
- Build đạt 0 lỗi (cảnh báo cũ), git diff --check đạt. GET /ManagedAccounts/Create 200; kiểm tra trường dữ liệu, CSRF, CSS/JS; không tạo tài khoản hoặc gửi email. Chưa kiểm tra trực quan/mobile/Safari. Không đổi backend/schema/DB; chưa push.

### 18/10/2026 – Căn giãn dòng trang Tạo tài khoản
- Dùng grid gap 16px cho form, bỏ margin Bootstrap bị cộng dồn; nhãn cách ô 6px, nhóm nút cách trường cuối 16px và cách đường phân cách 16px. Ẩn vùng validation rỗng, thông báo/lỗi giãn dòng 1.5. Phạm vi CSS chỉ trang Tạo tài khoản.
- git diff --check đạt; chỉ thay CSS, không build lại hoặc gửi tạo tài khoản. Chưa kiểm tra trực quan/mobile.

### 18/10/2026 – Breadcrumb Đổi vai trò tài khoản
- Thêm liên kết Quản lý tài khoản giữa nút menu và Đổi vai trò tài khoản. Đồng bộ font 16px, gap 12px, màu liên kết và dấu / theo breadcrumb chi tiết phòng của chủ nhà; có aria-current và co dòng màn nhỏ.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP GET trang 200 và liên kết /ManagedAccounts đúng. Chưa kiểm tra trực quan/mobile. Không đổi nghiệp vụ/DB; chưa push.

### 18/10/2026 – Đồng bộ breadcrumb Tạo tài khoản
- Trang Tạo tài khoản dùng chung breadcrumb với Đổi vai trò: nút menu, liên kết Quản lý tài khoản, dấu / và tên trang hiện tại; dùng cùng CSS và responsive.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt. HTTP 200 và breadcrumb/liên kết đúng trên cả hai trang; chưa kiểm tra trực quan. Không đổi nghiệp vụ/DB; chưa push.

### 18/10/2026 – Đồng bộ Bootstrap Hồ sơ Admin
- Hồ sơ Admin dùng toàn chiều ngang khung dashboard, lưới Bootstrap gutter 16px, thẻ viền/bo góc 12px, avatar 72px, hàng thông tin căn cột và giãn dòng 1.5. Nút bảo mật cao 44px, đổi mật khẩu màu chính, đăng xuất viền; bố cục dọc trên mobile. Chỉ gắn class riêng Admin.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt. GET /HoSo/CuaToi 200 ở Admin/chủ nhà; kiểm tra class riêng Admin, phần bảo mật và CSRF còn đủ. Chưa kiểm tra trực quan/mobile/Safari; không đổi backend/schema/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Hồ sơ Admin
- Hàng thông tin padding dọc 12px, tiêu đề thẻ cách đường phân cách 12px; mô tả bảo mật và tên/vai trò có khoảng cách gọn, bỏ margin cuối form đăng xuất. Giữ line-height 1.5 và khoảng cách các thẻ 16px; chỉ áp dụng Admin.
- git diff --check đạt; thay CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ hoặc DB.

### 18/10/2026 – Bootstrap Quản lý tòa nhà Admin
- Đồng bộ khung toàn chiều ngang, tiêu đề 28px, thẻ thống kê viền nhấn vàng, icon 36px, lưới/khoảng cách 16px, panel bo góc 12px; bộ lọc cao 44px, bảng Bootstrap căn giữa dọc và nút Chi tiết dạng viền. Responsive cột tổng hợp xếp dọc, thống kê 2 cột và bộ lọc xuống hàng ở màn nhỏ. CSS chỉ Admin, giữ dữ liệu và thao tác cũ.
- git diff --check đạt; GET trang 200 và CSS cập nhật được phục vụ. Chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile/Safari; không đổi nghiệp vụ/schema/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Quản lý tòa nhà Admin
- Thu gọn khoảng cách nhãn/số/mô tả thống kê, tiêu đề panel cách nội dung 14px; hàng bảng padding dọc 10px, ghi chú phụ cách 4px và line-height 1.5. Giữ khoảng cách các thẻ 16px; CSS chỉ Admin.
- git diff --check đạt. Chỉ CSS, không build lại; chưa xác minh trực quan/mobile. Không đổi dữ liệu/nghiệp vụ.

### 18/10/2026 – Bootstrap Thêm tòa nhà Admin
- Đồng bộ layout hai cột với khung Admin, thẻ bo góc 12px, tiêu đề/biểu tượng, ô nhập nền trắng cao 44px, nhãn cách 6px và lưới gap 16px. Thẻ tóm tắt rõ cột, nhóm nút lưu/hủy đồng chiều cao; dưới 1000px xếp một cột, mobile ô nhập/nút tự co.
- diff --check đạt; GET /PhongTro/TaoToaNha 200, CSRF và CSS mới được phục vụ. Chỉ CSS nên không build lại, không gửi tạo tòa. Chưa kiểm tra trực quan/mobile/Safari. Không đổi nghiệp vụ/schema/DB; chưa push.

### 18/10/2026 – Căn giãn dòng Thêm tòa nhà Admin
- Padding thẻ 16px; tiêu đề khối cách nội dung 14px, khoảng cách dọc các trường 14px, dòng trợ giúp cách 4px và line-height 1.5. Thu gọn lựa chọn loại hình, căn lại tên/mô tả và các hàng tóm tắt; ẩn validation rỗng. CSS chỉ Admin.
- git diff --check đạt; chỉ CSS, không build hoặc gửi tạo tòa. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB.

### 18/10/2026 – Breadcrumb Thêm tòa nhà Admin
- Thêm Quản lý tòa nhà / Thêm tòa nhà sau nút menu, dùng chung kiểu breadcrumb của Tạo tài khoản và liên kết về PhongTro/ToaNha.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang và đích liên kết 200, breadcrumb đúng. Chưa kiểm tra trực quan/mobile. Không đổi nghiệp vụ/DB; chưa push.

### 18/10/2026 – Bootstrap Chi tiết tòa nhà Admin
- Đồng bộ tiêu đề, nút 44px, tổng quan, thống kê viền vàng và danh sách phòng với dashboard Admin; thẻ bo góc 12px, khoảng cách 16px, nhãn/giá trị rõ cột, bộ lọc cao đều và bảng căn giữa dọc. Responsive giữ thống kê 2 cột, nút/bộ lọc xuống hàng; chỉ CSS Admin.
- diff --check đạt; HTTP chi tiết tòa nhà 200 và CSS mới phục vụ đúng. Không build lại vì chỉ CSS; chưa kiểm tra trực quan/mobile/Safari. Không đổi nghiệp vụ/schema/DB; chưa push.

### 18/10/2026 – Căn giãn dòng Chi tiết tòa nhà Admin
- Tổng quan padding/gap 16px, địa chỉ cách dữ kiện 12px; thống kê và mô tả giãn dòng 1.5. Bỏ margin thừa ở tiêu đề danh sách/form lọc, hàng bảng padding dọc 10px, dòng phụ cách 4px. Giữ khoảng cách các khối 16px; CSS chỉ Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ hoặc DB.

### 18/10/2026 – Breadcrumb Chi tiết tòa nhà Admin
- Thêm Quản lý tòa nhà / Chi tiết tòa nhà sau nút menu, dùng chung breadcrumb với Tạo tài khoản và Thêm tòa nhà. Liên kết về danh sách tòa nhà.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP chi tiết 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Chỉnh sửa tòa nhà Admin
- Đồng bộ khung editor với trang Thêm tòa nhà: thẻ 12px, khoảng cách 16px, tiêu đề 28px, input nền trắng cao 44px, focus vàng nhẹ, sidebar 300px và tóm tắt viền nhấn. Tab và nhóm nút lưu/hủy đồng bộ; responsive sidebar xuống dưới. CSS chỉ Admin, giữ form/validation/POST cũ.
- git diff --check đạt; GET trang sửa 200, CSRF và CSS mới được phục vụ; không gửi cập nhật tòa. Chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile/Safari; không đổi nghiệp vụ/schema/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Chỉnh sửa tòa nhà Admin
- Khoảng cách dọc các trường và sau tiêu đề khối 14px; tóm tắt/mô tả/ghi chú line-height 1.5, nhãn phụ cách 4px. Căn margin phần ảnh, số liệu và lưu ý; ẩn validation rỗng. Giữ CSS trong phạm vi Admin.
- git diff --check đạt; chỉ CSS, không build lại hoặc gửi cập nhật. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB.

### 18/10/2026 – Breadcrumb Chỉnh sửa tòa nhà Admin
- Thêm Quản lý tòa nhà / Chỉnh sửa tòa nhà sau nút menu, dùng chung CSS với Tạo tài khoản; liên kết về danh sách tòa nhà.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang sửa 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Thêm cấp Chi tiết tòa nhà trong breadcrumb sửa tòa Admin
- Breadcrumb: Quản lý tòa nhà / Chi tiết tòa nhà / Chỉnh sửa tòa nhà; liên kết chi tiết giữ đúng id tòa đang sửa, dùng CSS chung như Tạo tài khoản.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP xác minh thứ tự ba cấp, id tòa và liên kết chi tiết 200. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Chuẩn bị đẩy giao diện Admin
- Theo yêu cầu push nhánh feature/fix/UI-hoan-thien-du-an. Fetch xác nhận HEAD và origin cùng nền (0 ahead/0 behind). Phạm vi gồm dashboard/header/sidebar, tài khoản, hồ sơ, tòa nhà và tài liệu tiến độ; không stage DB/data/credential hoặc test csproj.
- Build gần nhất đạt 0 lỗi; HTTP các trang/breadcrumb đã kiểm tra theo từng mục; node --check cho site.js và account-filter-dropdowns.js đạt, diff --check đạt. Chưa kiểm tra trực quan/mobile/Safari hoặc full test nghiệp vụ.

### 18/10/2026 – Bootstrap Cấu hình điện nước Admin
- Đồng bộ tiêu đề, tab, thẻ 12px, nhãn/ô nhập 44px nền trắng, focus vàng, lựa chọn cách tính và thẻ tóm tắt với giao diện tòa nhà Admin. Nhóm lưu/hủy cao đều; responsive sidebar xuống dưới và lựa chọn xếp dọc trên mobile. Giữ nguyên giá, kỳ áp dụng, form và backend.
- git diff --check đạt; HTTP trang 200, CSRF và CSS cập nhật được phục vụ. Chỉ CSS nên không build lại; không gửi thay đổi cấu hình. Chưa kiểm tra trực quan/mobile/Safari; không đổi schema/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Cấu hình điện nước Admin
- Căn khoảng cách sau tiêu đề và giữa trường 14px, lựa chọn cách tính cách dọc 8px; bỏ margin dư của lựa chọn phạm vi, đồng bộ mô tả/tóm tắt/ví dụ line-height 1.5 và ẩn validation rỗng. Chỉ CSS Admin.
- git diff --check đạt; không build lại vì chỉ CSS, không gửi thay đổi cấu hình. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB.

### 18/10/2026 – Breadcrumb Cấu hình điện nước Admin
- Thêm Quản lý tòa nhà / Cấu hình điện nước sau nút menu, dùng chung CSS với Tạo tài khoản và liên kết về danh sách tòa nhà.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang cấu hình 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Nhật ký hoạt động Admin
- Gom bộ lọc vào thẻ trắng, ô nhập/select cao 44px, nhóm nút và tiêu đề đồng bộ Admin. Bảng Bootstrap hover nằm trong khung bo góc, cột có kích thước rõ, nội dung dài tự ngắt, chi tiết thay đổi giữ nguyên; cuộn ngang trong khung trên mobile.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP nhật ký và GET có bộ lọc 200, CSS mới tải đúng. Không sửa nội dung nhật ký hoặc backend/DB. Chưa kiểm tra trực quan/mobile/Safari; chưa push.

### 18/10/2026 – Căn giãn dòng Nhật ký hoạt động
- Hàng nhật ký padding 12px, line-height 1.5; câu mô tả và tóm tắt cách 6px, phần xem chi tiết và thông báo có nhịp cách đều. Giữ bộ lọc cách 16px và ô nhập cao 44px.
- git diff --check đạt; chỉ CSS, không build lại. Chưa kiểm tra trực quan/mobile; không sửa dữ liệu nhật ký hoặc nghiệp vụ.

### 18/10/2026 – Dropdown Bootstrap bộ lọc nhật ký
- Người thực hiện/Nhóm thông tin dùng Bootstrap Dropdown cao 44px, menu bo góc/bóng nhẹ, tô mục được chọn và focus vàng. Đồng bộ select gốc để giữ query lọc và lựa chọn từ server; không có JS vẫn dùng select gốc.
- Build đạt 0 lỗi (cảnh báo cũ), node --check và diff --check đạt; GET trang/tài nguyên JS 200, trường gốc và asset đúng. Chưa kiểm tra tương tác trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Đồng bộ giao diện Báo hỏng & bảo trì Admin
- Đồng bộ kích thước Bootstrap dashboard: tiêu đề 28px, control 44px, thống kê viền vàng/thẻ 12px, khoảng cách 16px, bảng chữ 13px và khung chi tiết 340px. Màn nhỏ xếp khung chi tiết dưới danh sách, bảng cuộn trong khung. CSS riêng Admin.
- Mã thực tế trang /Modules/BAO_HONG là dữ liệu minh họa với nút disabled; giữ nguyên trạng thái và nhãn minh họa, không nối CRUD hoặc thay dữ liệu. diff --check đạt, HTTP trang/CSS 200. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, chưa push.

### 18/10/2026 – Căn giãn dòng Báo hỏng & bảo trì Admin
- Thu gọn hàng danh sách padding dọc 10px, đồng bộ line-height 1.5; căn khoảng cách tiêu đề/mô tả, liên hệ, lịch hẹn và lịch sử xử lý trong khung chi tiết theo nhịp 4/8/12/16px. Chỉ CSS Admin, giữ trạng thái dữ liệu minh họa/nút disabled.
- git diff --check đạt; không build lại vì chỉ CSS. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB.

### 18/10/2026 – Đồng bộ Bootstrap Báo cáo Admin
- Bộ lọc dùng CSS grid căn hàng, tự chia 3/2/1 cột theo màn hình; thẻ KPI nhấn vàng, số liệu 26px và mô tả 12px. Đồng bộ khoảng cách 16px, header biểu đồ và bảng theo dashboard. CSS chỉ Admin, giữ nguyên dữ liệu/công thức báo cáo và dropdown Bootstrap có sẵn.
- diff --check đạt, GET /BaoCao và CSS 200. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Báo cáo Admin
- Thu gọn khoảng cách nhãn/số/mô tả KPI theo nhịp 8/6px; header biểu đồ padding dọc 14px, dòng phụ cách 4px; hàng bảng padding dọc 10px và line-height 1.5. Bộ lọc giữ nhãn cách ô 6px, các khối cách 16px; chỉ CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi dữ liệu/công thức hoặc DB.

### 18/10/2026 – Bootstrap Công nợ Admin
- Đồng bộ tiêu đề 28px, thẻ 12px, bộ lọc/nút cao 44px, khoảng cách 16px; thẻ thống kê nhấn vàng, bảng Bootstrap chữ rõ và badge gọn. Tổng hợp xuống một cột trên màn nhỏ, thống kê 2 cột mobile. Giữ dropdown Bootstrap và chức năng lọc/xuất/chi tiết có sẵn; CSS chỉ Admin.
- diff --check đạt; GET /CongNo và CSS 200. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không đổi tính nợ/backend/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Công nợ Admin
- Căn tiêu đề thẻ padding dọc 14px, hàng bảng 10px; thu gọn nhịp nhãn/số/mô tả KPI, dòng phụ cách 4px. Bộ lọc gap dọc 14px, nội dung trợ giúp/ưu tiên/phân trang line-height 1.5. CSS chỉ Admin.
- git diff --check đạt; chỉ CSS nên không build lại, chưa kiểm tra trực quan/mobile. Không đổi dữ liệu/công thức công nợ hoặc DB.

### 18/10/2026 – Tinh chỉnh Bootstrap Quản lý tòa nhà Admin
- Cân lại chiều cao/margin thống kê, đồng bộ hai panel tổng hợp và thu gọn vùng biểu đồ trống. Các dropdown bộ lọc thực tế (details/summary) cùng cao 44px với ô tìm kiếm; nút đổi kiểu hiển thị rõ trạng thái chọn. CSS chỉ Admin, không đổi dữ liệu hoặc thao tác.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile/Safari; chưa push.

### 18/10/2026 – Căn lại giãn dòng tổng quan tòa nhà Admin
- Cân nhịp nhãn/số/mô tả thống kê 4–6px; tiêu đề panel, chú giải biểu đồ và số tổng hợp có khoảng cách gọn; ghi chú hàng bảng line-height 1.5 và cách dòng chính 4px. Giữ khoảng cách khối 16px, không đổi bố cục mobile.
- git diff --check đạt; chỉ CSS, chưa kiểm tra trực quan/mobile. Không đổi chức năng hoặc DB; chưa push.

### 18/10/2026 – Bootstrap Quản lý phòng Admin
- Đồng bộ thống kê nhấn vàng, thẻ 12px, tiêu đề 28px, khoảng cách 16px; bộ lọc/nút 44px, bảng và badge dễ đọc, Chi tiết dạng nút viền. Responsive giữ thống kê 2 cột, nhóm nút/bộ lọc xuống hàng. CSS chỉ Admin, giữ form và nghiệp vụ phòng.
- GET /PhongTro và CSS 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không đổi backend/DB; chưa push.

### 18/10/2026 – Căn giãn dòng Quản lý phòng Admin
- Thu gọn khoảng cách số/mô tả thống kê, header và bộ lọc padding dọc 14px, hàng bảng 10px; dòng phụ phòng/khách thuê cách 4px và line-height 1.5. Chỉ CSS Admin, giữ chức năng và dữ liệu.
- git diff --check đạt; không build lại vì chỉ CSS. Chưa kiểm tra trực quan/mobile; chưa push.

### 18/10/2026 – Bootstrap Thêm phòng Admin
- Đồng bộ thẻ 12px, tiêu đề 28px, khoảng cách 16px, ô nhập 44px nền trắng/focus vàng, lựa chọn trạng thái/tiện nghi cao đều và khung tải ảnh rộng rõ. Responsive một cột dưới 1000px, trường và tiện nghi xếp dọc trên mobile. CSS chỉ Admin, giữ validation/POST và tải ảnh hiện có.
- diff --check đạt; GET /PhongTro/Create 200, CSRF và CSS tải đúng; không tạo phòng. Chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile/Safari; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Thêm phòng Admin
- Khoảng cách dọc trường/tiêu đề khối 14px, dòng trợ giúp cách 4px, hướng dẫn trạng thái cách 8px và line-height 1.5. Cân nhịp tiện nghi, số đã chọn, khung tải ảnh; ẩn validation rỗng. Chỉ CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi chức năng/DB, chưa push.

### 18/10/2026 – Breadcrumb Thêm phòng Admin
- Thêm Quản lý phòng / Thêm phòng cạnh nút menu, dùng chung CSS với Tạo tài khoản và liên kết về danh sách phòng.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang thêm/đích danh sách 200 và breadcrumb đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Tạo nhanh phòng Admin
- Đồng bộ thẻ 12px, tiêu đề 28px, lưới gap 16px, ô nhập 44px nền trắng và focus vàng; thẻ xem trước dễ đọc, nút cùng kích thước. Responsive một cột dưới 1000px, trường xếp dọc mobile. CSS chỉ Admin, giữ logic xem trước/tạo loạt và validation.
- GET /PhongTro/CreateBulk 200 và CSRF còn đủ; git diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không gửi tạo phòng hoặc đổi DB; chưa push.

### 18/10/2026 – Căn giãn dòng Tạo nhanh phòng Admin
- Khoảng cách dọc trường/tiêu đề khối 14px; hướng dẫn cách ô 4px, nội dung xem trước line-height 1.5 và hàng tóm tắt đều nhịp. Ẩn validation rỗng; chỉ CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi logic hoặc DB, chưa push.

### 18/10/2026 – Breadcrumb Tạo nhanh phòng Admin
- Thêm Quản lý phòng / Tạo nhanh phòng cạnh nút menu, dùng chung CSS như Tạo tài khoản/Thêm phòng và liên kết về danh sách phòng.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP hai trang thêm/tạo nhanh 200, breadcrumb và liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Chi tiết phòng Admin
- Đồng bộ tiêu đề/tab, thẻ bo góc 12px, khoảng cách 16px, nhãn/giá trị rõ ràng; nút cao 44px, ảnh tỷ lệ 4:3, tiện nghi và khu hợp đồng/giá thuê đồng bộ. Responsive một cột dưới 1000px, ảnh/thông tin 2 cột mobile. CSS chỉ Admin, giữ dữ liệu/quyền/chức năng hiện có.
- GET chi tiết phòng 200, git diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Chi tiết phòng Admin
- Tiêu đề khối cách nội dung 14px, lưới thông tin và đường phân cách theo nhịp 14px; căn tên/mô tả khách thuê, giá thuê và phí dịch vụ, line-height 1.5. Giữ ảnh và các khối chính cách đều; chỉ CSS Admin.
- git diff --check đạt; không build lại vì chỉ CSS. Chưa kiểm tra trực quan/mobile; không đổi dữ liệu hoặc nghiệp vụ, chưa push.

### 18/10/2026 – Breadcrumb Chi tiết phòng Admin
- Thêm Quản lý phòng / Chi tiết phòng [mã phòng] cạnh nút menu, dùng chung CSS như Tạo tài khoản và liên kết về danh sách phòng; tên phòng lấy từ tiêu đề hiện tại.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP chi tiết 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Chỉnh sửa phòng Admin
- Đồng bộ thẻ 12px, khoảng cách 16px, tiêu đề 28px, cảnh báo hợp đồng và ô nhập 44px nền trắng/focus vàng với Thêm phòng. Nút đồng kích thước, responsive một cột dưới 1000px; CSS chỉ Admin, giữ form và cơ chế lưu dịch vụ riêng hiện có.
- GET trang sửa 200 và CSRF còn đủ; diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không gửi cập nhật hoặc đổi DB; chưa push.

### 18/10/2026 – Căn giãn dòng Chỉnh sửa phòng Admin
- Khoảng cách dọc trường và sau tiêu đề khối 14px, hướng dẫn cách ô 4px và line-height 1.5; thu gọn khung lưu ý hợp đồng, ẩn validation rỗng. CSS chỉ Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi chức năng hoặc DB, chưa push.

### 18/10/2026 – Breadcrumb Chỉnh sửa phòng Admin
- Thêm Quản lý phòng / Chỉnh sửa phòng [mã phòng] cạnh nút menu, dùng chung CSS với Tạo tài khoản và liên kết về danh sách phòng.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang sửa 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Thêm mã phòng vào breadcrumb Chỉnh sửa phòng Admin
- Breadcrumb gồm Quản lý phòng / [mã phòng] / Chỉnh sửa phòng [mã phòng]. Mã lấy từ model, liên kết chi tiết bằng RoomId hiện tại; dùng CSS chung như Tạo tài khoản.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP xác minh mã, thứ tự ba cấp và liên kết chi tiết đúng id trả 200. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bỏ mã phòng ở mục cuối breadcrumb sửa phòng Admin
- Mục hiện tại hiển thị Chỉnh sửa phòng, giữ mã phòng ở cấp liên kết giữa theo yêu cầu.
- Build đạt 0 lỗi (cảnh báo cũ), HTTP kiểm tra caption đúng; diff --check đạt. Không đổi tiêu đề nội dung/nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Nhật ký phòng Admin
- Đồng bộ tiêu đề 28px, thẻ 12px, khoảng cách 16px, thông tin phòng dễ đọc; bộ lọc/ô ghi chú/nút cao 44px và focus vàng. Giữ tab, lịch sử, ghi chú và quyền hiện tại; CSS chỉ Admin.
- HTTP nhật ký phòng 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không gửi ghi chú hoặc đổi DB; chưa push.

### 18/10/2026 – Căn giãn dòng Nhật ký phòng Admin
- Cân nhịp nhãn/giá trị tóm tắt 4px, thu gọn khoảng cách tab và sự kiện; nội dung lịch sử/ghi chú/tài liệu line-height 1.5, nhóm nút cách 12px. CSS chỉ Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi nhật ký hoặc nghiệp vụ/DB, chưa push.

### 18/10/2026 – Breadcrumb Nhật ký phòng Admin
- Thêm Quản lý phòng / Nhật ký phòng cạnh nút menu, dùng chung CSS với Tạo tài khoản; liên kết về danh sách phòng.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP nhật ký 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Tin đăng cho thuê Admin
- Đồng bộ thẻ thống kê nhấn vàng, tiêu đề 28px, khoảng cách 16px và panel bo góc 12px; bộ lọc/control 44px, bảng chữ rõ, tab cuộn trên màn nhỏ. CSS riêng Admin, giữ bộ lọc/thao tác/quyền tin đăng hiện có.
- GET /TinDang/QuanLy và CSS 200, git diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Tin đăng Admin
- Thu gọn nhịp số/mô tả thống kê, tiêu đề danh sách và bộ lọc padding dọc 14px, hàng bảng 10px; dòng phụ cách 4px và line-height 1.5. Chỉ CSS Admin, giữ chức năng/dữ liệu.
- git diff --check đạt; không build lại vì chỉ CSS. Chưa kiểm tra trực quan/mobile; chưa push.

### 18/10/2026 – Bootstrap Yêu cầu thuê & lịch xem Admin
- Đồng bộ tiêu đề 28px, thẻ thống kê nhấn vàng/bo góc 12px, control 44px; bảng và khung chi tiết chữ rõ, khoảng cách 16px. Giữ responsive, bộ lọc, dữ liệu và quyền nghiệp vụ; CSS chỉ Admin.
- GET /YeuCau 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không đổi backend/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Yêu cầu thuê Admin
- Cân nhịp số/mô tả thống kê; hàng bảng padding dọc 10px, dòng phụ cách 4px, header chi tiết 14px. Tên/mô tả người thuê, dữ kiện và lời nhắn line-height 1.5, lưới chi tiết gap dọc 12px. CSS chỉ Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Chi tiết yêu cầu thuê Admin
- Đồng bộ tiêu đề 28px, thẻ 12px, thanh tiến trình chữ rõ, thông tin khách/phòng và lịch hẹn; input/nút 44px nền trắng, focus vàng và khoảng cách 16px. Responsive khung lịch hẹn xuống dưới dưới 1000px; CSS chỉ Admin, giữ dữ liệu/POST/quyền hiện tại.
- GET chi tiết yêu cầu 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari, không gửi thao tác nghiệp vụ hoặc đổi DB; chưa push.

### 18/10/2026 – Căn giãn dòng Chi tiết yêu cầu thuê Admin
- Cân tiêu đề/nội dung thẻ, tên/mô tả khách và phòng cách 4px; contact margin 12px, lưới dữ kiện gap dọc 12px. Lời nhắn, lịch hẹn và lịch sử line-height 1.5, giữ phạm vi CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi chức năng/DB, chưa push.

### 18/10/2026 – Breadcrumb Chi tiết yêu cầu Admin
- Thêm Yêu cầu thuê & lịch xem / Chi tiết yêu cầu [mã] cạnh nút menu, dùng chung CSS với Tạo tài khoản; liên kết về /YeuCau.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP chi tiết 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Bootstrap Hợp đồng & người ở ghép Admin
- Đồng bộ thẻ thống kê viền vàng, icon 38px, panel 12px, bộ lọc/nút 44px; bảng có viền và khoảng cách dễ đọc. Mobile thống kê xếp dọc và tìm kiếm toàn chiều ngang. CSS chỉ Admin, giữ dữ liệu/quyền và chức năng hiện có.
- GET /HopDong 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Căn giãn dòng Hợp đồng Admin
- Cân nhịp thống kê, bộ lọc cách hàng tìm kiếm 12px; nhãn/ô cách 6px, hàng bảng padding dọc 10px và dòng phụ cách 4px. Tiêu đề/mô tả/tab line-height 1.5; chỉ CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi chức năng/DB, chưa push.

### 18/10/2026 – Bootstrap Lập hợp đồng Admin
- Đồng bộ thẻ và tiêu đề khối có đường phân cách, ô nhập/select và nút 44px, nhãn dễ đọc, phần tóm tắt rõ cột và phòng được chọn nổi nhẹ. Responsive một cột dưới 1000px. CSS chỉ Admin, giữ dữ liệu/validation/luồng lập hợp đồng.
- GET /HopDong/Create 200, diff --check đạt. Chỉ CSS nên không build lại; chưa kiểm tra trực quan/mobile/Safari. Không tạo hợp đồng hoặc đổi DB; chưa push.

### 18/10/2026 – Căn giãn dòng Lập hợp đồng Admin
- Khoảng cách sau tiêu đề và giữa trường 14px, nhãn cách ô 6px; hàng tóm tắt padding dọc 10px, dòng trợ giúp line-height 1.5. Cân khung phòng/chỉ số và ẩn validation rỗng; chỉ CSS Admin.
- git diff --check đạt; chỉ CSS nên không build lại. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Breadcrumb Lập hợp đồng Admin
- Thêm Hợp đồng & người ở ghép / Lập hợp đồng thuê cạnh nút menu, dùng chung CSS với Tạo tài khoản; liên kết về /HopDong.
- Build đạt 0 lỗi (cảnh báo cũ), diff --check đạt; HTTP trang tạo 200 và breadcrumb/liên kết đúng. Chưa kiểm tra trực quan/mobile; không đổi nghiệp vụ/DB, chưa push.

### 18/10/2026 – Đẩy bổ sung giao diện Admin theo yêu cầu
- Fetch xác nhận nhánh local/remote cùng nền. Đưa vào commit 6 file giao diện/tiến độ: CSS Admin, nhật ký và dropdown, layout breadcrumb, tài liệu; không gồm DB/data/credential/test project.
- Build gần nhất đạt 0 lỗi; các trang/breadcrumb đã kiểm tra HTTP theo từng mục. node --check audit-dropdowns.js và diff --check đạt. Chưa full test nghiệp vụ hoặc kiểm tra trực quan/mobile/Safari.

### 19/10/2026 — Đồng bộ thanh đầu trang Quản lý
- Đồng bộ chiều cao 66px, khoảng cách, ô tìm kiếm, menu tài khoản và tiêu đề Tổng quan theo Chủ nhà; CSS chỉ nạp cho QUAN_LY.
- Giữ nguyên quyền và chức năng thông báo. Kiểm chứng build và HTTP; chưa nghiệm thu trực quan trên thiết bị 360px.

### 19/10/2026 — Bootstrap chi tiết yêu cầu
- Trang chi tiết dành cho người xem ngoài Chủ nhà: lưới Bootstrap 8/4, thẻ thông tin/thao tác/lịch sử, nhãn trạng thái và bố cục một cột trên điện thoại.
- Giữ nguyên điều kiện hiển thị thao tác, form POST/CSRF và dữ liệu server. Kiểm tra build, HTTP quản lý/khách/chủ nhà; chưa nghiệm thu trực quan 360px.

- 19/10/2026: Căn nhịp dòng chi tiết yêu cầu: bảng line-height 1.5, đệm dòng 11px; tiêu đề cách 16px, lịch sử line-height 1.6 và các mốc cách đều 16px. Kiểm tra CSS phục vụ qua HTTP.
- 19/10/2026: Bỏ liên kết Quay lại danh sách ở đầu trang chi tiết yêu cầu dành cho người xem ngoài Chủ nhà theo yêu cầu.
- 19/10/2026: Breadcrumb chi tiết yêu cầu của Quản lý: biểu tượng trang chủ / Yêu cầu thuê & lịch xem / Chi tiết yêu cầu; đồng bộ cỡ chữ và khoảng cách trang chi tiết phòng Chủ nhà, co lại trên mobile.
- 19/10/2026: Trang hợp đồng Quản lý dùng thẻ Bootstrap, lưới bộ lọc responsive, tab trạng thái dạng nhóm, bảng cuộn ngang trong thẻ; bỏ thanh trả phòng trống đối với vai trò không có thao tác. Giữ logic tìm/lọc/xuất và quyền hiện hành.
- 19/10/2026: Căn giãn dòng trang hợp đồng Quản lý: line-height 1.5, đệm hàng bảng 13px, thống nhất khoảng cách nhãn/bộ lọc/thống kê và tiêu đề. Xác minh CSS qua HTTP.
- 19/10/2026: Bootstrap trang chỉ số điện nước Quản lý: bộ chọn kỳ dạng lưới, thẻ thống kê, thanh tìm/lọc, bảng nhập và nhãn trạng thái đồng bộ tông graphite/champagne. Giữ form lưu từng phòng, CSRF, khóa kỳ và cảnh báo. Có responsive; chưa nghiệm thu trực quan mobile.
- 19/10/2026: Căn giãn dòng trang chỉ số điện nước Quản lý: thống nhất line-height 1.5, khoảng cách nhãn/ô nhập 6px, hướng dẫn 4px và đệm hàng 13px; giảm khoảng trống thống kê/bộ chọn kỳ. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bootstrap báo cáo Quản lý: bố cục bộ lọc dạng lưới, thẻ KPI, khung biểu đồ/bảng bo góc, tông graphite/champagne và giãn dòng đồng bộ; responsive. Giữ nguyên dữ liệu/tính toán và quyền. Kiểm tra build/HTTP, chưa nghiệm thu trực quan mobile.
- 19/10/2026: Căn giãn dòng báo cáo Quản lý: line-height 1.5, khoảng cách tiêu đề/nhãn 4–6px, đệm hàng bảng 11px; thống nhất khoảng cách thẻ số liệu, biểu đồ và phần lấp đầy. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bootstrap danh sách báo hỏng Quản lý: thẻ thống kê, tab trạng thái, lưới bộ lọc 3/2/1 cột, nút áp dụng/reset, trạng thái trống có biểu tượng và phân trang. Giữ nguyên tìm/lọc/quyền/luồng xử lý. Kiểm chứng build/HTTP; chưa nghiệm thu trực quan mobile.
- 19/10/2026: Căn giãn dòng danh sách báo hỏng Quản lý: line-height 1.5, nhãn cách ô nhập 6px, đệm hàng 13px; thống nhất khoảng cách thống kê/tab/bộ lọc/trạng thái trống/phân trang. Kiểm tra CSS qua HTTP.
- 19/10/2026: Theo mẫu người dùng, giữ bộ lọc báo hỏng trên một hàng desktop (tìm kiếm rộng gấp đôi), áp dụng/xóa ở hàng dưới; tab gạch chân và trạng thái trống tối giản. Chỉ tinh chỉnh màu/viền/giãn cách, responsive thu cột khi thiếu chỗ.
- 19/10/2026: Tinh chỉnh bộ lọc báo hỏng: căn nhãn trái, chiều cao ô 42px, khoảng cách đều 14px; nút áp dụng gọn 174px và xóa cạnh bên, giữ một hàng desktop; responsive 3/1 cột. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bỏ mục Tạo tài khoản nội bộ khỏi sidebar ADMIN theo yêu cầu; giữ nút tạo trong trang Quản lý tài khoản.
- 19/10/2026: Bỏ mục Đổi mật khẩu khỏi sidebar ADMIN theo yêu cầu.
- 19/10/2026: Đồng bộ header ADMIN theo Chủ nhà: cao 66px, tìm kiếm tối đa 340px, căn chuông/tài khoản, menu/avatar/chevron cùng kiểu; CSS chỉ nạp ADMIN, giữ breadcrumb và quyền hiện tại. Kiểm tra build/HTTP.
- 19/10/2026: Bootstrap tra cứu gửi thông báo: nav-pills phân nhóm có trạng thái hiện tại, thẻ bộ lọc 3 cột, bảng nhật ký và trạng thái trống; giãn dòng 1.5, responsive. Reset giữ nhóm đang xem. Giữ nguyên truy vấn và quyền. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng tra cứu email: line-height 1.5, khoảng cách nhãn/tiêu đề 6px, đệm hàng bảng 11px; thống nhất khoảng cách tab, ghi chú, bộ lọc và bảng. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bỏ mũi tên CSS giả bị trùng với SVG trên menu tài khoản ADMIN; dùng một chevron SVG giống Chủ nhà, giữ hiệu ứng mở/đóng.
- 19/10/2026: Dropdown trạng thái tra cứu email dùng Bootstrap, nút/chevron và menu nổi có bóng nhẹ, mục chọn đánh dấu; giữ select gốc làm fallback và gửi form đúng giá trị. Không thêm thư viện.
- 19/10/2026: Bootstrap Dịch vụ & đơn giá ADMIN: thẻ thống kê đồng bộ, panel/bộ lọc responsive, bảng hover, trạng thái trống/phân trang và thẻ giải thích; giãn dòng 1.5. Giữ chức năng và quyền chỉ xem. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng Dịch vụ & đơn giá ADMIN: line-height 1.5, khoảng cách nhãn/số liệu 6–10px, đệm bảng 12px; thống nhất khoảng cách tab, bộ lọc, phân trang và giải thích. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bootstrap chi tiết tin đăng ADMIN: thẻ tóm tắt/giá thuê, đặc tính phòng, ảnh responsive khi có, mô tả và bảng dịch vụ 5/7 cột; giãn dòng thống nhất, không đổi quyền/dữ liệu. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng chi tiết tin ADMIN: thông tin/bảng line-height 1.5, mô tả 1.6; khoảng cách khối 16px, tiêu đề 6–14px, đệm hàng 12px. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bỏ nút Danh sách tin đăng khỏi chi tiết tin ADMIN theo yêu cầu.
- 19/10/2026: Breadcrumb chi tiết tin ADMIN: trang chủ / Tin đăng cho thuê / Chi tiết tin đăng, đồng bộ thông số trang chi tiết phòng Chủ nhà; liên kết về quản lý tin, responsive.
- 19/10/2026: Hoàn thiện Bootstrap danh sách hợp đồng ADMIN: thống kê 3 thẻ, bộ lọc 4/2/1 cột, tab nhóm, bảng hover/phân trang và giãn dòng 1.5; giữ nguyên lọc/xuất/quyền. Kiểm tra CSS và HTTP.
- 19/10/2026: Căn giãn dòng danh sách hợp đồng ADMIN: line-height 1.5, đệm hàng 12px, nhãn cách ô nhập 6px; thống nhất khoảng cách thẻ/tab/bộ lọc/phân trang. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bootstrap tra cứu chỉ số ADMIN: thẻ bộ lọc/kết quả, nhãn trạng thái, căn phải số công tơ/tiêu thụ, trạng thái trống và phân trang; giãn dòng 1.5, responsive. Giữ nguyên truy vấn và quyền chỉ xem. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng tra cứu chỉ số ADMIN: line-height 1.5, nhãn cách ô 6px, đệm hàng 12px; khoảng cách bộ lọc/kết quả 16px. Kiểm tra CSS qua HTTP.
- 19/10/2026: Đổi tiêu đề trang và header chỉ số ADMIN thành Chỉ số điện nước theo yêu cầu.
- 19/10/2026: Bootstrap quản lý hóa đơn ADMIN: thẻ thống kê, tab nhóm, bộ lọc responsive, bảng có vùng cuộn riêng và căn phải tiền, mã hóa đơn xuống dòng rõ, trạng thái/phân trang đồng bộ; giãn dòng 1.5. Giữ quyền và nghiệp vụ hiện hành. Kiểm tra CSS qua HTTP.
- 19/10/2026: Căn giãn dòng quản lý hóa đơn ADMIN: line-height 1.5, đệm hàng 12px, khoảng cách tab/bộ lọc/phân trang 14px; giảm khoảng trống thẻ thống kê. Kiểm tra CSS qua HTTP.
- 19/10/2026: Bootstrap chi tiết hóa đơn ADMIN: giữ hai cột, căn thẻ số liệu, bảng chi tiết/tổng tiền, lịch sử trống, tiến độ/thông tin; màu graphite/champagne, giãn dòng 1.5 và responsive. Không đổi quyền/tính tiền/thu tiền. Kiểm tra CSS qua HTTP.
- 19/10/2026: Căn giãn dòng chi tiết hóa đơn ADMIN: line-height 1.5, đệm khoản mục 12px, thông tin 10px; thống nhất nhịp thẻ, tiến độ và lịch sử thu. Kiểm tra CSS qua HTTP.
- 19/10/2026: Breadcrumb chi tiết hóa đơn ADMIN: trang chủ / Quản lý hóa đơn / Phòng; dùng cùng cỡ chữ/khoảng cách breadcrumb chi tiết phòng Chủ nhà.
- 19/10/2026: Bỏ cột Chi tiết khỏi bảng lịch sử cư trú khi ADMIN xem theo yêu cầu; giữ cột cho các vai trò còn lại.
- 19/10/2026: Khôi phục cột Chi tiết ở lịch sử cư trú ADMIN theo yêu cầu mới nhất.
- 19/10/2026: Bỏ đúng cột Chi tiết ở bảng Yêu cầu thuê & lịch xem khi ADMIN xem; giữ cột cho Chủ nhà/Quản lý và giữ nguyên lịch sử cư trú.
- 19/10/2026: Dropdown Tòa nhà ở Chỉ số điện nước ADMIN dùng Bootstrap, menu nổi có bóng nhẹ và đánh dấu mục chọn; giữ select gốc/fallback và giá trị form tra cứu.
- 19/10/2026: Breadcrumb chỉnh sửa tài khoản ADMIN: trang chủ / Hồ sơ của tôi / Chỉnh sửa tài khoản, đồng bộ cỡ chữ/khoảng cách chi tiết phòng Chủ nhà.
- 19/10/2026: Thêm breadcrumb chỉnh sửa tài khoản Chủ nhà: trang chủ / Hồ sơ của tôi / Chỉnh sửa tài khoản; đồng bộ chi tiết phòng, responsive, giữ nguyên luồng tài khoản.
- 19/10/2026: Đồng bộ breadcrumb chỉnh sửa tài khoản Quản lý tòa nhà: trang chủ / Hồ sơ của tôi / Chỉnh sửa tài khoản, cùng bố cục Chủ nhà/ADMIN; responsive.
- 19/10/2026: Bỏ mục Chỉnh sửa tài khoản khỏi menu tài khoản Quản lý tòa nhà theo yêu cầu; không đổi menu các vai trò khác.
- 19/10/2026: Bỏ mục Chỉnh sửa tài khoản khỏi menu tài khoản Chủ nhà theo yêu cầu, tương tự Quản lý.
- 19/10/2026: Bỏ mục Chỉnh sửa tài khoản khỏi menu tài khoản ADMIN theo yêu cầu, giữ mục này cho Khách thuê.
- 19/10/2026: Sidebar ADMIN Bootstrap: căn đều icon 18px, hàng menu 40px, nhóm/chữ/badge và trạng thái chọn graphite/champagne; thu khoảng cách giữa các nhóm nghiệp vụ liên tiếp. Giữ nguyên menu, URL và kiểm tra quyền. Kiểm tra CSS HTTP và diff.
- 19/10/2026: Căn giãn dòng sidebar ADMIN: hàng desktop 38px, khoảng cách mục 2px, nhóm 10px; nhóm nghiệp vụ liên tiếp không thêm khoảng trống. Mobile giữ vùng bấm 44px. Kiểm tra CSS HTTP.
- 19/10/2026: Sidebar Chủ nhà: tinh chỉnh nav Bootstrap, icon 18px, hàng 40px, nhóm champagne, badge và trạng thái chọn; giữ menu/URL/quyền, mobile vùng bấm 44px. Kiểm tra CSS HTTP và diff.
- 19/10/2026: Căn giãn dòng sidebar Chủ nhà: hàng desktop 38px, cách mục 2px, cách nhóm 10px; mobile giữ vùng bấm 44px. Kiểm tra CSS HTTP.
- 19/10/2026: Menu Khách thuê dùng nav Bootstrap, SVG thống nhất, nhãn/badge/active và nút đóng đồng bộ graphite/champagne; giữ dialog và điều khiển bàn phím/đóng menu hiện tại. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng menu Khách thuê: hàng tối thiểu 44px, cách mục 2px, đệm menu 14px; tiêu đề và nhãn line-height 1.5. Kiểm tra CSS qua HTTP.
- 19/10/2026: Đồng bộ menu Khách thuê theo sidebar Chủ nhà: nền graphite, chữ sáng, active/badge champagne, icon 18px và nhịp hàng tương ứng; giữ drawer, quyền/menu và vùng bấm mobile 44px. Kiểm tra CSS HTTP.
- 19/10/2026: Thêm logo NhàTốt / Sống đúng nơi ở đầu drawer Khách thuê theo mẫu Chủ nhà; giữ nút đóng và liên kết về tổng quan.
- 19/10/2026: Bỏ mục Hóa đơn trên thanh điều hướng đầu trang Khách thuê theo yêu cầu; giữ Hóa đơn của tôi trong menu.
- 19/10/2026: Đồng bộ header Khách thuê với Chủ nhà: cao 66px, menu 36px, căn brand/nav/chuông/tài khoản, avatar/menu tài khoản và responsive. Giữ nguyên điều hướng, quyền và drawer. Kiểm tra CSS HTTP.
- 19/10/2026: Đồng bộ ba liên kết điều hướng đầu trang Khách thuê với menu công khai: cỡ chữ .9rem, độ đậm 600, đệm/cách mục, active champagne và hover.
- 19/10/2026: Ẩn ảnh đại diện trong nút tài khoản đầu trang Khách thuê theo ảnh yêu cầu; giữ tên và menu tài khoản.
- 19/10/2026: Hoàn tác ẩn avatar Khách thuê theo yêu cầu; khôi phục hiển thị T1 như trước.
- 19/10/2026: Đồng bộ header Khách thuê trên layout công khai và workspace: thêm role-tenant cho trang công khai đã đăng nhập, bảo đảm avatar giữ đúng kích thước; dùng chung _TenantHeader và tenant-header.css.
- 19/10/2026: Bootstrap tổng quan Khách thuê: thẻ phòng nền trắng/nhấn champagne, thống kê cân đối, panel hóa đơn/yêu cầu và responsive; giữ nguyên dữ liệu và liên kết. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng tổng quan Khách thuê: line-height 1.5, thông tin phòng cách 4–6px, khoảng cách thẻ 14–16px; thống nhất thẻ số liệu và danh sách hóa đơn. Kiểm tra CSS HTTP.
- 19/10/2026: Tinh chỉnh Bootstrap Tìm phòng Khách thuê: thanh tìm kiếm gọn, sidebar/thẻ tin bo góc, hình ảnh đồng tỷ lệ, typography/giãn dòng và phân trang; responsive 3/2/1 cột. Chỉ scope role-tenant, giữ truy vấn và bộ lọc. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng Tìm phòng Khách thuê: line-height 1.5, khoảng cách thẻ 6–10px, nhóm lọc 12px; thống nhất nhịp thanh tìm kiếm và kết quả. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap-style hồ sơ Khách thuê: thẻ tài khoản 3 cột, avatar 68px, tab nhóm, thẻ thông tin 2 cột, nút và giãn dòng đồng bộ; responsive. Giữ dữ liệu nhạy cảm/che số và logic hiện tại. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng hồ sơ Khách thuê: line-height 1.5, đệm dòng thông tin 10px, khoảng cách tab/thẻ 14–16px; đồng bộ ghi chú và phần tài khoản. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap form hồ sơ cá nhân: lưới thông tin 2 cột, thẻ ảnh giấy tờ, hành động lưu/hủy và vùng xóa riêng; responsive, giữ validation/CSRF/upload và logic lưu. Kiểm tra build/HTTP.
- 19/10/2026: Căn giãn dòng form hồ sơ: khoảng cách trường 16px, nhãn–ô nhập 6px, hướng dẫn line-height 1.5; phần giấy tờ và thao tác cách 20px. Kiểm tra CSS HTTP.
- 19/10/2026: Tinh chỉnh Bootstrap Hợp đồng của tôi Khách thuê: toolbar dạng thẻ, tab nhóm, hợp đồng trắng bo góc, giá/ngày/nút cân đối và responsive. Giữ lọc, liên kết PDF/chi tiết và quyền. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng Hợp đồng của tôi: line-height 1.5, khoảng cách thông tin 4–6px, thẻ 14px và toolbar gọn. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap chi tiết hợp đồng Khách thuê: nút phụ cùng hàng, thẻ 2 cột, thông tin/ảnh/tài liệu/bảng dịch vụ cân đối và giãn dòng 1.5; responsive. Giữ liên kết và logic hợp đồng. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng chi tiết hợp đồng Khách thuê: line-height 1.5, khoảng cách nhãn 4px, thông tin 16px, đệm bảng/người ở 12px; đồng bộ các thẻ. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap yêu cầu trả phòng Khách thuê: form 2 cột, cảnh báo champagne, thẻ gửi/lịch sử, lọc và trạng thái trống gọn; responsive và giãn dòng 1.5. Giữ validation/CSRF/nghiệp vụ. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng yêu cầu trả phòng Khách thuê: nhãn–ô nhập 6px, trường cách 14px, line-height 1.5 và nhịp thẻ/lịch sử thống nhất. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap Báo hỏng của tôi: thống kê cân đối, tab trạng thái, lọc dạng lưới, bảng/trạng thái trống/phân trang; responsive và giãn dòng 1.5. Chỉ scope tenant, giữ nguyên truy vấn/quyền/luồng gửi. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng Báo hỏng của tôi: line-height 1.5, đệm bảng 12px, nhịp thống kê/tab/lọc/phân trang đồng nhất. Kiểm tra CSS HTTP.
- 19/10/2026: Bootstrap Yêu cầu của tôi: thẻ thống kê, lịch hẹn champagne, toolbar/tab/lọc và bảng thao tác đồng bộ; responsive, giãn dòng 1.5. Giữ tìm/lọc/hủy/chi tiết và quyền. Kiểm tra CSS HTTP.
- 19/10/2026: Căn giãn dòng Yêu cầu của tôi: line-height 1.5, đệm bảng 12px, khoảng cách phụ 4px; nhịp thống kê/lịch hẹn/toolbar gọn, đều. Kiểm tra CSS HTTP.
- 19/10/2026: Đồng bộ Tìm phòng đã đăng nhập với giao diện công khai: bỏ các override riêng tenant, dùng chung room-search-bootstrap và loại đệm workspace bao ngoài; giữ header tài khoản và toàn bộ lọc/tìm kiếm.
- 20/10/2026: Sửa xung đột grid ở tổng quan Khách thuê: cụm thông tin phòng xếp dọc cạnh icon, thống kê flex cột tránh nhãn vỡ dòng; giảm chiều cao thẻ dư, responsive. Kiểm tra CSS HTTP.
- 20/10/2026: Bỏ dòng Quay lại danh sách ở chi tiết hợp đồng Khách thuê theo yêu cầu; giữ breadcrumb.
- 20/10/2026: Dropdown Vai trò trong Hợp đồng của tôi dùng Bootstrap, menu nổi/active/check đồng bộ; giữ select và sự kiện change để bộ lọc hoạt động như cũ.
- 20/10/2026: Hai bộ lọc Trạng thái/Mức độ ở Báo hỏng của tôi dùng dropdown Bootstrap có menu nổi và dấu chọn; giữ select gốc và giá trị GET. Kiểm tra cú pháp JS/build.
- 20/10/2026: Bootstrap form Gửi báo hỏng Khách thuê: lưới 2 cột, thẻ mức độ, upload, tóm tắt gọn và responsive; nhãn/giãn dòng thống nhất. Giữ validation/CSRF/ảnh riêng và nghiệp vụ. Kiểm tra CSS HTTP.
- 20/10/2026: Căn giãn dòng form Gửi báo hỏng: nhãn–ô nhập 6px, nhóm trường 16px, căn ngang nhãn thiết bị/mức độ; tóm tắt và hướng dẫn có khoảng cách đều. Kiểm tra CSS HTTP.
- 20/10/2026: Sửa hai dropdown lọc Báo hỏng bị nền tối/ẩn chữ do theme: nền trắng, chữ rõ, mũi tên và trạng thái champagne; giữ chức năng lọc. Kiểm tra CSS HTTP.
- 20/10/2026: Bỏ dòng Quay lại danh sách trên trang Gửi báo hỏng theo yêu cầu; giữ breadcrumb và nút Hủy.
- 20/10/2026: Dropdown chọn phòng Gửi báo hỏng dùng Bootstrap, menu trắng bo góc, trạng thái chọn champagne; giữ select gốc, validation bắt buộc và cập nhật tóm tắt.
- 20/10/2026: Tinh chỉnh trang chi tiết Yêu cầu của tôi theo Bootstrap: thẻ 2 cột, thông tin/ảnh phòng, nút và lịch sử đồng bộ; giãn dòng 1.5, responsive. Giữ nghiệp vụ và quyền. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan trên thiết bị.
- 20/10/2026: Căn giãn dòng chi tiết yêu cầu thuê: thông tin line-height 1.6, đệm dòng 11px, khoảng cách thông tin phòng 6px và lịch sử 12px. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Bỏ dòng Quay lại danh sách ở chi tiết yêu cầu thuê theo yêu cầu; giữ breadcrumb. Kiểm tra mã nguồn.
- 20/10/2026: Bộ lọc trạng thái Yêu cầu của tôi dùng dropdown Bootstrap: menu bo góc, bóng nhẹ, dấu chọn champagne; giữ select và sự kiện lọc hiện có. Kiểm tra cú pháp JS và CSS HTTP.
- 20/10/2026: Tinh chỉnh Bootstrap Hóa đơn của tôi: thống kê, lưới lọc, tab/bảng/phân trang bo góc đồng bộ, giãn dòng 1.5 và responsive. Giữ truy vấn/dữ liệu và quyền. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan mobile.
- 20/10/2026: Căn giãn dòng Hóa đơn của tôi: nhãn/giá trị thống kê, nhãn–ô nhập 6px, dòng bảng 1.6 và đệm 12px; thông tin phụ cách 4px. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Bootstrap chi tiết hóa đơn Khách thuê: tiêu đề tách hàng thao tác, thống kê/thẻ/bảng và form báo thanh toán đồng bộ, nhãn 6px, giãn dòng 1.5–1.6; responsive. Giữ CSRF, validation và nghiệp vụ. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Căn giãn dòng chi tiết hóa đơn: bảng/tổng tiền line-height 1.6, đệm dòng 12px, nhãn–ô nhập 6px và nhóm form 16px. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Bỏ nút Quay lại danh sách hóa đơn ở chi tiết hóa đơn Khách thuê theo yêu cầu; giữ breadcrumb và nút in. Kiểm tra mã nguồn.
- 20/10/2026: Hai bộ lọc Trạng thái hóa đơn/Báo thanh toán dùng dropdown Bootstrap, menu bo góc và dấu chọn champagne; giữ select có name và FormData GET hiện có. Kiểm tra cú pháp JS, CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Bootstrap Thông báo hóa đơn Khách thuê: tiêu đề, thẻ thông báo, trạng thái trống có biểu tượng chuông, nút và phân trang đồng bộ; responsive. Giữ nội dung/quyền/POST đánh dấu đã đọc. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
- 20/10/2026: Căn giãn dòng Thông báo hóa đơn: nội dung line-height 1.6, tiêu đề–mô tả 6px, đoạn 12px, thao tác 10px và phân trang 16px. Kiểm tra CSS HTTP; chưa kiểm chứng trực quan.
