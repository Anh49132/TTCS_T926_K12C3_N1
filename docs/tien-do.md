## 12/10/2026 - Huy yeu cau da duyet

- Chủ nhà có mục **Khách đổi ý, không thuê**, bắt buộc lý do và xác nhận. Yêu cầu chuyển Đã hủy, phòng về Trống, tin nguồn tự đăng lại với hạn mới 30 ngày theo yêu cầu PO. Chặn nếu tòa ngừng hoạt động hoặc đã có tin công khai khác.
- Chặn hợp đồng đã lập, cọc xác nhận, giữ chỗ khác và phiên bản cũ; gửi trùng không ghi thêm. Trạng thái, nhật ký, lịch sử và thông báo cùng transaction. Không đổi schema/DB thật.
- [Hướng dẫn và kết quả kiểm thử](huy-yeu-cau-da-duyet.md): 25 test mới và 121 test hồi quy PASS. Chưa nghiệm thu trực quan form mới trên thiết bị thật.

## 11/10/2026 — Duyệt thuê sau khi xem phòng

- Chủ nhà có nút Duyệt thuê sau khi xem cho yêu cầu XEM_PHONG/DA_HEN_LICH khi phòng còn trống. Phải xác nhận khách đồng ý thuê ở form và backend; loại/trạng thái khác bị từ chối. Giữ nguyên loại yêu cầu, lịch hẹn và lịch sử.
- Dùng cùng transaction giữ phòng như thuê ngay: yêu cầu DA_DUYET, phòng DA_DAT_COC, tin DA_CHO_THUE, lịch sử/nhật ký/thông báo. Kiểm tra sức chứa hiện tại, hợp đồng đang/chờ hiệu lực và yêu cầu đã duyệt khác trong khóa SQLite; lỗi rollback. Lập hợp đồng dùng luồng hiện có từ yêu cầu đã duyệt. Không tự ghi nhận cọc.
- Thống kê đã duyệt chưa lập hợp đồng bao gồm cả hai loại yêu cầu. Không đổi schema/DB thật. Build PASS; 28 test duyệt cũ/mới và HTTP đến form hợp đồng PASS, gồm CSRF, thiếu xác nhận, sai loại/trạng thái, trái quyền, sức chứa giảm, gửi lại và rollback lịch sử. Chưa kiểm tra trực quan trình duyệt.

## 11/10/2026 — Email yêu cầu mới và thay đổi lịch hẹn

- Khách gửi yêu cầu thành công: gửi email tới tài khoản chủ sở hữu tòa của phòng trong tin, chứa mã yêu cầu, khách, phòng, loại, ngày mong muốn và link xử lý. Yêu cầu trùng hoặc không được lưu không gửi email.
- Chủ nhà đổi lịch thành công: gửi email cho tài khoản khách của yêu cầu với giờ mới theo Việt Nam và link chi tiết. Giữ email xác nhận lịch hiện có; thiếu người nhận/dịch vụ gửi cũng báo lỗi thay vì im lặng.
- Email gửi sau khi nghiệp vụ được lưu; lỗi gửi không rollback dữ liệu và được báo qua giao diện. Dùng cấu hình PasswordReset SMTP/pickup hiện có, không đổi cấu hình/bí mật/DB. Chưa có hàng đợi tự gửi lại email lỗi.
- Build PASS; kiểm thử HTTP email thành công và lỗi SMTP, đúng chủ/khách, yêu cầu trùng và thao tác trái quyền không gửi email. Chưa gửi thử SMTP tới hộp thư thật.

## 11/10/2026 — Giao diện chủ nhà quản lý yêu cầu

- Danh sách có bốn thống kê từ dữ liệu thật, tab trạng thái, tìm kiếm, lọc loại/tòa, phân trang 10 dòng, xuất CSV UTF-8 có chống công thức, lịch hẹn hôm nay và cảnh báo quá 24 giờ. Thống kê trong phạm vi tòa đang chọn và không bị lọc theo trạng thái. Thuê ngay đã duyệt chỉ đếm yêu cầu chưa có hợp đồng chưa hủy.
- Trang chi tiết chủ nhà chia thông tin/xử lý/lịch sử và cột khách/phòng có ảnh, giá, diện tích, sức chứa. Giữ form/CSRF và quy tắc hẹn, đổi lịch, từ chối, duyệt thuê ngay, hủy, lập hợp đồng. Chỉ hiện form xác nhận lịch cho loại xem phòng ở giao diện chủ nhà. Không thêm ghi chú hẹn hay xác thực khách giả khi chưa có cơ chế dữ liệu.
- Không sửa CSDL. Build PASS; 57 test hẹn/duyệt/từ chối PASS; test HTTP danh sách và hai loại chi tiết PASS sau khi giữ nhãn số yêu cầu chưa xử lý. Chưa kiểm tra trực quan trình duyệt/360px và thao tác CSV thực tế.

## 11/10/2026 — Giao diện sửa đơn giá dịch vụ của tòa

- Trang DichVu/Manage chia thành thống kê, form cập nhật giá, thông tin dịch vụ, trạng thái áp dụng và lịch sử. Hiển thị giá đang hiệu lực thay vì lấy phiên bản tương lai; lịch sử phân biệt sắp áp dụng, đang áp dụng và đã kết thúc.
- Số phòng lấy từ dịch vụ đã chọn còn áp dụng; tách phòng dùng giá riêng. Công tắc mặc định dùng SetDefault hiện có và quay lại trang Manage. Giữ CSRF, kiểm tra sở hữu, lịch giá và thao tác ngừng/kích hoạt từ kỳ sau. Không đổi schema; không thêm trường lý do chưa có cơ chế lưu.
- Build PASS; chưa kiểm tra trực quan trình duyệt/360px.

## 11/10/2026 — Doanh thu dự kiến trên danh sách phòng

- Chỉ cộng giá thuê tháng của phòng DANG_THUE trong phạm vi tòa đang xem; loại phòng trống, đặt cọc và ngừng cho thuê. Nhãn ghi rõ doanh thu tiền phòng dự kiến; liên kết mở danh sách phòng đang thuê. Không cộng phí dịch vụ hoặc coi là tiền thực thu.
- Build PASS; không đổi CSDL. Chưa kiểm tra trực quan trình duyệt.

## 11/10/2026 — Ảnh trên trang chi tiết phòng

- Trang /PhongTro/Details tải cùng danh sách ảnh đã lưu như trang sửa phòng, giữ thứ tự và đánh dấu ảnh đại diện. Bấm ảnh mở ảnh gốc; phòng chưa có ảnh có thông báo riêng. Lưới ảnh tự co theo chiều rộng màn hình.
- Giữ kiểm tra quyền module và phạm vi tòa trước khi tải ảnh. Không đổi schema hoặc dữ liệu.
- Build PASS; chưa kiểm tra trực quan trình duyệt/360px.

## 10/10/2026 — Chủ nhà chốt chỉ số điện nước như quản lý

- Theo yêu cầu mới: ChiSoDienNuoc cho CHU_NHA và QUAN_LY dùng cùng màn hình/form chốt từng phòng. Service đọc vai trò đang hoạt động từ DB: chủ nhà chỉ tòa có chu_nha_id là mình; quản lý chỉ tòa có quan_ly_id là mình. Tòa/phòng/hợp đồng bị giả mạo vẫn bị chặn khi GET và POST.
- Menu chủ nhà dẫn trực tiếp /ChiSoDienNuoc; đường dẫn /Modules/DIEN_NUOC cũ chuyển tới màn hình nghiệp vụ. Danh sách phòng thiếu thêm Nhập chỉ số cho kỳ hiện tại (ngày Việt Nam qua ITimeProvider), giữ Xem phòng cho kỳ cũ. Ghi nhận nguoi_nhap_id là tài khoản thao tác.
- Giữ quyền module DIEN_NUOC (WRITE khi lưu), CSRF, chỉ số không giảm, kiểm tra phiên bản/chống ghi đè giữa chủ nhà và quản lý, cảnh báo tiêu thụ bất thường và khóa kỳ sau phát hành. Không sửa DB thật, schema, dữ liệu phân quyền hoặc mở quyền đã bị thu hồi.
- Kiểm thử mới: chủ nhà lưu và ghi đúng người nhập; GET/POST tòa khác bị chặn cả khi được gán là quản lý; READ/NONE và khóa hóa đơn vẫn chặn lưu; quản lý dùng form cũ sau chủ nhà lưu không ghi đè được. Cập nhật hai kỳ vọng test cũ vốn chặn chủ nhà theo yêu cầu trước đây.
- Build PASS; 104/104 kiểm thử nhóm MeterReadingListTests, MeterUsageWarningTests, tiến độ và phòng thiếu PASS; git diff --check PASS. Chưa kiểm tra trực quan trình duyệt/360px. Khởi động lại ứng dụng để nhận thay đổi.

## 10/10/2026 — Thiết kế lại tiến độ chốt chỉ số S3-10

- Màn hình theo kỳ: bốn ô thống kê, cảnh báo đến ngày chốt, bảng theo tòa có người quản lý phụ trách, tổng/đã chốt/còn thiếu, thanh tiến độ và trạng thái khóa riêng. Tìm theo tên tòa, lọc trạng thái, xuất CSV các tòa đang hiển thị với UTF-8 BOM và xử lý ô có tiền tố công thức. Dữ liệu hiển thị lấy từ service hiện hành, không dùng số hoặc ảnh mẫu.
- Bấm số phòng thiếu/Xem chi tiết mở dialog bên phải qua partial của endpoint PhongConThieu, vẫn kiểm tra vai trò/quyền sở hữu; mở liên kết bình thường vẫn có trang đầy đủ. Danh sách nêu phòng/tầng/người quản lý, hỗ trợ trạng thái trống và liên kết xem phòng. Không thêm nút nhập chỉ số cho chủ nhà vì controller nhập hiện chỉ cho QUAN_LY. Không suy đoán phòng thiếu điện hay nước khi dữ liệu service hiện chỉ trả kết quả hoàn tất chung; thiếu cấu hình giá cũng có thể khiến phòng chưa hoàn tất.
- Giữ nguyên cơ chế khóa kỳ sau phát hành và cảnh báo trang chủ; không đổi quyền, schema hoặc DB thật. CSS/JS riêng cho màn hình tiến độ; bảng cuộn trong khung trên điện thoại, dialog rộng tối đa màn hình.
- Build PASS; 11/11 kiểm thử tiến độ/phòng thiếu/quyền/cảnh báo và khóa sau phát hành PASS. HTTP demo: tiến độ và hai tòa ở cả chế độ trang đầy đủ/partial trả 200, CSS/JS 200. git diff --check PASS. Chưa kiểm tra trực quan 360px/Safari hoặc thao tác dialog/lọc/xuất trong trình duyệt do không có trình duyệt kết nối. Khởi động lại app để nhận Razor mới.

## 10/10/2026 — Yêu cầu của tôi và chi tiết yêu cầu theo ảnh mẫu

- Khách thuê xem danh sách với bốn thống kê, lịch hẹn sắp tới, tab loại yêu cầu, tìm theo mã/phòng/tòa và lọc trạng thái trên dữ liệu chỉ thuộc chính mình. Danh sách có liên kết chi tiết, menu mở tin/hủy; giữ giao diện chủ nhà/quản lý/admin. Thống kê tính toàn bộ yêu cầu, lọc chỉ tác động các hàng hiển thị.
- Chi tiết gồm thông tin/lời nhắn, lịch hẹn hoặc trạng thái trống, lịch sử nghiệp vụ, thẻ phòng từ dữ liệu hiện có, trạng thái xử lý và liên kết hỗ trợ. Lịch đã hủy/từ chối được đánh dấu hết hiệu lực. Hủy chỉ cho MOI/DA_HEN_LICH, xác nhận dialog trước POST có CSRF; backend hủy và kiểm tra sở hữu giữ nguyên. Lịch sử chỉ hiển thị trạng thái/thời gian/lịch hẹn/lý do nghiệp vụ, không đưa snapshot người thực hiện lên trang khách.
- CSS tenant-requests.css chỉ nạp hai trang khách thuê; mobile chuyển hàng bảng thành thẻ, chi tiết một cột. Không đổi schema, không sửa DB thật hoặc thêm thư viện.
- Build PASS; bộ kiểm thử quyền/hủy/lịch sử và liên kết tin: 34/34 PASS. HTTP demo xác nhận danh sách và sáu chi tiết trả 200, có các thẻ thông tin/lịch sử, form hủy và tài nguyên CSS/JS. Chưa nghiệm thu trực quan 360px/Safari hoặc thao tác bộ lọc/dialog trong trình duyệt do không có trình duyệt kết nối. Cần khởi động lại app để nhận Razor mới.

## 10/10/2026 — Chi tiết tin đăng cho khách thuê theo ảnh mẫu

- Trang `TinDang/ChiTiet`: bố cục hai cột, cụm ảnh lớn và ảnh phụ, bộ ảnh mở trong dialog, thông tin phòng, mô tả mở rộng, tiện nghi, bảng dịch vụ; cột phải gồm giá thuê/cọc/chi phí cố định, form xem phòng hoặc thuê ngay và chủ nhà. Giữ dữ liệu giá thật, kiểm tra quyền, tin lịch sử và các ràng buộc gửi yêu cầu + CSRF.
- Tiện nghi và tên chủ nhà lấy từ dữ liệu hiện có; thiếu dữ liệu hiển thị chưa cập nhật, không dựng tiện nghi hoặc dấu xác minh. Lưu tin dùng localStorage trên thiết bị; chia sẻ dùng chức năng trình duyệt hoặc sao chép/hiển thị liên kết. Không thêm thư viện hoặc đổi schema/DB thật.
- CSS riêng `listing-detail.css`, JS `listing-detail.js`, SVG nội bộ; chuyển một cột trên màn hình nhỏ, bảng dịch vụ cuộn trong khung. Public layout hỗ trợ section Styles để trang chưa đăng nhập cũng nạp đúng CSS.
- Build PASS; 14/14 kiểm thử hiện có về chi tiết/giá/ảnh và quyền xem tin qua yêu cầu PASS. HTTP demo riêng PASS cho trang khách chưa đăng nhập/khách thuê, CSS/JS và trường form + CSRF. `git diff --check` PASS. Chưa kiểm tra trực quan 360px/Safari và thao tác JavaScript trên trình duyệt do không có trình duyệt kết nối. Cần khởi động lại app để nhận Razor mới.

## 10/10/2026 — Thanh đầu trang khách thuê theo ảnh mẫu

- Sửa `_TenantHeader`: nền trắng, nút menu và biểu tượng nhà màu than, chữ NhàTốt, liên kết giữa thanh, chuông vàng/số chưa đọc, đường phân cách và avatar/tên tài khoản bên phải. Dùng SVG nội bộ; gom chuông + tài khoản để khớp bốn cột và tránh xuống hàng.
- CSS `tenant-header.css` chỉ nạp cho KHACH_THUE, sau theme chung. Khi khách thuê đã đăng nhập, trang tìm phòng/tin đăng/giới thiệu dùng cùng header; khách chưa đăng nhập và vai trò khác giữ header công khai. Màn hình nhỏ ẩn liên kết giữa/tên dài, giữ menu, thương hiệu, chuông và tài khoản.
- Giữ kiểm tra quyền và tính số thông báo từ DB, menu hồ sơ/đổi mật khẩu, đăng xuất POST có CSRF. Sửa trạng thái liên kết Trang chủ/Giới thiệu để không cùng được đánh dấu.
- Build PASS, 0 lỗi; kiểm tra HTTP trên demo riêng: `/HopDongCuaToi`, `/TimTin`, `/TinDang`, `/Home/GioiThieu` và stylesheet đều 200, đúng một header, có chuông/tài khoản/form đăng xuất + CSRF. Header công khai trước đăng nhập được giữ nguyên. `git diff --check` PASS. Chưa nghiệm thu trực quan 360px/Safari vì không có trình duyệt kết nối. Không đổi DB thật; cần khởi động lại app để nhận Razor mới.

## 10/10/2026 — Giao diện khách thuê xem chi tiết hợp đồng theo ảnh mẫu

- Trang `HopDongCuaToi/Details`: bố cục hai cột, tiêu đề/trạng thái và nút tải PDF; sáu thông tin chính, thanh thời hạn, người ở, bảng/khung trống dịch vụ bên trái; thẻ phòng thuê, xem trước PDF, tóm tắt thanh toán và ba mục mở rộng bên phải. Giữ lịch sử kỳ thuê và ngày trả phòng khi có.
- Dùng ngày chốt, sức chứa, trạng thái/ảnh phòng, tin đăng và thời hạn từ dữ liệu hiện có sau kiểm tra quyền hợp đồng. Ngày chốt tiếp theo theo ngày Việt Nam qua `ITimeProvider`, kẹp ngày cuối tháng và nằm trong kỳ hợp đồng. Không nhầm ngày chốt với hạn thanh toán. Không điền số trang/dung lượng PDF mẫu; ảnh/điều khoản chưa có hiển thị rõ trạng thái trống.
- CSS riêng `tenant-contract-detail.css`, icon SVG nội bộ, không thêm thư viện, không đổi header chung hoặc schema. Mobile chuyển một cột, thông tin chính hai cột; bảng dịch vụ cuộn trong khung, điều khoản dùng `details/summary` hỗ trợ bàn phím.
- Build Razor + 18/18 `HopDongCuaToiTests` PASS (quyền đứng tên/ở ghép/người ngoài và PDF). Demo mới riêng cổng 5256: chi tiết, CSS, tải PDF HTTP 200; xác nhận liên kết xem trước/tải và 27 icon SVG đã render. `git diff --check` PASS. Công cụ computer-use không có trình duyệt kết nối nên chưa nghiệm thu trực quan 360px/Safari; không khẳng định đã kiểm tra hình ảnh trên thiết bị.
- Không cập nhật DB thật. App đang chạy cần khởi động lại để nhận giao diện mới.

## 10/10/2026 — Sửa tự động các khoảng trống Sprint 3

- S3-01: nhập giá thuê/cọc thỏa thuận nguyên đồng; backend chặn giá dưới 500.000 đ và cọc ngoài 0…3 tháng giá chốt. Lưu giá kỳ hợp đồng, giữ giá niêm yết phòng; mở lại Nháp giữ giá/cọc đã lưu.
- S3-03: cảnh báo 0…29 ngày, hiển thị riêng ngày hết hạn hôm nay.
- S3-06: chọn tối đa 50 Nháp đã kiểm tra và xác nhận phát hành một lần. Kiểm tra quyền tòa/kỳ, phiên bản từng bản, dữ liệu và ngày/hạn; cùng transaction cho phát hành, khóa chỉ số, audit và thông báo. Gửi lại/đồng thời không tạo trùng; lỗi một bản rollback cả lần.
- S3-10: tiến độ lấy giá/cách tính theo phòng và ngày chốt; không đòi công tơ cho điện/nước THEO_NGUOI. Thiếu cấu hình hoặc giá không hợp lệ vẫn chưa sẵn sàng.
- Đồng bộ test schema 23, fixture hồ sơ dùng hợp đồng thật + module yêu cầu thuê; fixture nghiệm thu kích hoạt truyền giá/cọc theo form mới. Thêm test thương lượng, Nháp sau đổi giá niêm yết, khoán hỗn hợp/toàn bộ, 50 hóa đơn qua HTTP, gửi lặp/đồng thời, rollback phiên bản/audit và giới hạn 50.
- Xác minh cuối: **365 test liên quan đã PASS** (lượt `autofix-final.trx`: 362 PASS/3 lỗi fixture chưa tạo bảng hóa đơn; sửa fixture và chạy lại đúng ba trường hợp đạt 3/3 trong `autofix-limits-final.trx`). HTTP 50 hóa đơn + kiểm tra gửi lặp/quyền mất khoảng 2,18 giây, đạt ngưỡng 30 giây. Build và `git diff --check` PASS.
- Lượt đầy đủ trước các chỉnh fixture cuối: 617 test, 600 PASS/17 FAIL. Tám lỗi fixture hợp đồng/hồ sơ đã qua ở lượt cuối; chín lỗi giao diện/form/phân quyền Sprint 1–2 đã có trước lượt sửa vẫn chưa xử lý. Không khẳng định toàn bộ suite đã xanh. Chi tiết trong [báo cáo Sprint 3](kiem-tra-sprint3-sau-sua-20261010.md).
- Không đổi schema, không cập nhật DB thật; build riêng, test trên SQLite tạm. Chưa nghiệm thu 360px/Safari/PDF/SMTP thật. App đang chạy cần khởi động lại để nhận mã mới.

## 10/10/2026 — Kiểm tra lại 10 story Sprint 3 sau sửa phí theo người

- Báo cáo [kiem-tra-sprint3-sau-sua-20261010.md](kiem-tra-sprint3-sau-sua-20261010.md): S3-02 giảm phí kỳ sau đã được kiểm thử; S3-01 vẫn thiếu giá chốt/cọc tùy ý; S3-06 web tạo Nháp hàng loạt rồi phát hành từng bản, chưa có một thao tác web phát hành toàn tòa; S3-10 vẫn đòi chỉ số điện+nước khi tính tiến độ dù nước có thể khoán theo người. S3-03 cảnh báo chỉ 1…29 ngày, cần chốt ngày hết hạn 0 ngày.
- Kiểm thử bổ sung hợp đồng/chỉ số/lịch sử: 245 test, 241 PASS, 4 FAIL (3 kỳ vọng schema cũ, 1 fixture tạo lại bảng), 0 SKIP. Không cộng với bộ 140 test trước vì có thể trùng. Không sửa mã nghiệp vụ hay DB trong lượt kiểm tra; chưa nghiệm thu UX 360px/PDF/Safari/SMTP thật.

## 10/10/2026 — Khôi phục phí dịch vụ theo người (S3-02/S3-06)

- Theo yêu cầu trực tiếp của người dùng, thay thế quyết định cũ chặn THEO_NGUOI ngày 09/10. Bỏ chặn hóa đơn riêng lẻ, xem trước/thao tác hàng loạt và phát hành Nháp; dùng số người tính phí phía server, không nhận số người giả từ form. Tháng chuyển đi tính đủ, kỳ sau giảm; người mới bắt đầu tính phí từ tháng kế tiếp theo quy tắc hiện có.
- Điện/nước khoán theo người tính số người × đơn giá ngày chốt, không yêu cầu chỉ số; dịch vụ theo công tơ vẫn bắt buộc chốt đầy đủ. Snapshot số người/giá/dòng đã lưu không tự thay đổi khi sửa hồ sơ hoặc bảng giá. Giữ kiểm tra quyền, phiên bản, chống trùng và rollback nhật ký.
- Cập nhật bộ test số người/chuyển đi để kiểm tra số tiền thực tế, thêm kiểm thử Nháp → phát hành + thông báo, nước khoán/điện công tơ, cả hai khoán, phí tháng kế tiếp và snapshot. Test chuyển đi mở đúng tab `people` của giao diện hiện hành.
- Không đổi schema, không nâng cấp hay chỉnh DB đang dùng. Build/test dùng output riêng và SQLite tạm. Hồi quy `RoomServicesTests|Roommate|Invoice`: **140/140 PASS**, 0 SKIP, 59 giây; build và `git diff --check` PASS. TRX local: `data/criteria-audit-results/per-person-final.trx`. Lượt đầu 112/113 PASS, còn một assertion cũ kỳ vọng chặn theo người; đã cập nhật sang kiểm tra phí thực tế và chạy lại cả nhóm. Chưa nghiệm thu lại trình duyệt/360px/Safari/SMTP thật; tiến trình web đang chạy cần khởi động lại để nhận mã mới.

## 10/10/2026 — Rà soát tiêu chí Sprint 1–3 theo yêu cầu người dùng

- Đối chiếu 30 story từ ba tệp tiêu chí với mã `dev` tại `265dd83`; báo cáo: [kiem-tra-tieu-chi-sprint1-3-20261010.md](kiem-tra-tieu-chi-sprint1-3-20261010.md). Chưa đáp ứng toàn bộ tiêu chí gốc: thiếu cửa sổ đếm đăng nhập sai 15 phút, tìm tin 6 thay vì 12/trang, giá hợp đồng chỉ lấy giá phòng/cọc chỉ số tháng nguyên, chặn dịch vụ theo người theo quyết định PO mới, chưa đủ nhật ký thanh toán. Phân biệt các quyết định hiện hành khác bản gốc về xác nhận email, MVC 403 và quyền xem hồ sơ hợp đồng đã kết thúc.
- Chạy toàn bộ xUnit bằng output riêng: 604 test, 587 PASS, 17 FAIL, 0 SKIP; 6 lỗi assertion còn kỳ vọng schema v22, 1 fixture tạo lại bảng, 10 lỗi giao diện/luồng cần đối chiếu chi tiết trong báo cáo. Không sửa test/mã nghiệp vụ trong audit. TRX local: `data/criteria-audit-results/criteria.trx`.
- Chỉ đọc DB theo đường dẫn log người dùng bằng SQLite mode=ro: hiện v23, integrity ok, FK 0 lỗi. Không chạy updater/initializer hoặc thay đổi dữ liệu DB đang dùng. Chưa nghiệm thu UI 360px/Safari/SMTP thật/3G/tải production.

## 10/10/2026 — Merge S3-07 vào dev

- Merge `feature/S3-07/tenant-invoice-list-and-filter` vào `dev`; giữ các thay đổi mới hơn của dev và toàn bộ chức năng hóa đơn khách thuê của nhánh feature.
- Xử lý xung đột fixture `BillingWeb`: giữ tham số `testClock` của dev; tài liệu tiến độ giữ nội dung cả hai nhánh.
- Xác minh: build Debug thành công; 22/22 test mục tiêu hóa đơn/hợp đồng khách thuê PASS; 17/17 test lọc theo Invoice (gồm schema thanh toán) PASS. Hai lượt có test trùng nhau. Build ra thư mục riêng, test dùng SQLite tạm; không chạy updater trên DB người dùng.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -p:BaseOutputPath=verification/merge-s307-output/ --filter "FullyQualifiedName~TenantInvoice|FullyQualifiedName~TenantCanOpenOnlyTheirPublishedInvoice|FullyQualifiedName~HopDongCuaToiTests"`; lượt hồi quy dùng `--no-build --filter "FullyQualifiedName~InvoicePaymentSchemaTests|FullyQualifiedName~Invoice"` với cùng output path.
- Còn cảnh báo ImageSharp license, CS8601 và xUnit2013 có sẵn. Chưa xác minh UI trực quan/360px/Safari hoặc SMTP thật trong lần merge này.

## 09/10/2026 — Khởi chạy và kiểm tra tài khoản demo hợp đồng

- Tạo DB demo biệt lập dưới `data/sprint2-demo/20261009-135112-532c7a`, tài khoản khách thuê giả `tenant1.demo@demo.local`; thông tin đăng nhập ngẫu nhiên chỉ lưu trong `access.json` bị ignore. Không sửa DB local `QL_PhongTro/Data/local-dev.sqlite`.
- Sửa đăng ký DI cho `HopDongPdfService` và bổ sung `Views/HopDongCuaToi/Index.cshtml` theo màn danh sách mẫu: thẻ hợp đồng, trạng thái, tìm kiếm/lọc theo vai trò, truy cập chi tiết/PDF và menu khách thuê. Giao diện responsive; không thay schema hay dữ liệu.
- Xác minh: build Debug thành công; 5/5 `HopDongCuaToiTests` PASS. Lần build song song test bị khóa output, chạy lại riêng sau đó thành công. HTTP login trả 200; sau đăng nhập, trang `/HopDongCuaToi` hiển thị 2 hợp đồng demo `HD-DEMO-C001` và `HD-DEMO-C002`.
- Máy chủ tại `http://localhost:5249`; nếu dừng, chạy `verification/Start-Sprint2Demo.ps1 -Directory 'data/sprint2-demo/20261009-135112-532c7a'` từ thư mục dự án. Database demo riêng, không dùng database gốc. Bước upload ảnh mẫu lúc tạo demo vẫn lỗi, không ảnh hưởng màn hợp đồng; chưa xác minh thao tác tải PDF bằng trình duyệt.

## 09/10/2026 — Sửa fixture test hợp đồng khách thuê

- Đổi khởi tạo hợp đồng trong `Tests/QL_PhongTro.Tests/HopDongCuaToiTests.cs` từ thuộc tính không còn tồn tại `TienCocThoaThuan` sang `TienCoc`. Đồng bộ fixture với schema hiện tại bằng cách khởi tạo cờ email/xóa mềm, tiện ích tòa nhà và principal hợp lệ cho audit.
- Xác minh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --filter FullyQualifiedName~HopDongCuaToiTests` đạt 5/5. Không thao tác database local; test dùng SQLite in-memory. Còn cảnh báo license ImageSharp và analyzer xUnit có sẵn ngoài phạm vi.

## 09/10/2026 — Sửa lỗi ánh xạ chi tiết hợp đồng khách thuê

- Sửa gán ngày chuyển đi từ `NguoiOGhep.NgayRa` vào `NguoiOViewModel.NgayChuyenDi`; dùng `HopDongThamChieu.TienCoc` thay thuộc tính không tồn tại `TienCocThoaThuan`; khai báo `HopDongCardViewModel.KhachDungTenId` nullable khớp entity.
- Thành phần: `QL_PhongTro/Controllers/HopDongCuaToiController.cs`, `QL_PhongTro/ViewModels/HopDongCuaToiViewModels.cs`.
- Xác minh: `dotnet build QL_PhongTro/QL_PhongTro.csproj --configuration Debug` thành công, 0 lỗi. Còn 3 cảnh báo đã có: license ImageSharp và 2 nullable warnings trong `AuthController`; không chạy test vì không có test riêng cho controller này trong nhánh hiện tại. Không chạy ứng dụng hoặc thao tác CSDL.
# 10/10/2026 — S3-07 AC4: danh sách và lọc hóa đơn khách thuê

- PO chốt: kỳ mới nhất lên trước; 10 hóa đơn/trang; chọn một tháng; chọn một trạng thái hoặc “Tất cả”; trạng thái “Quá hạn” thay cho chưa thanh toán/trả một phần sau ngày hạn, còn đã thanh toán giữ nguyên. Kết hợp kỳ + trạng thái bằng AND; phân biệt chưa có hóa đơn với bộ lọc không khớp.
- Thêm danh sách và endpoint chỉ lấy hóa đơn đã phát hành của khách đang đăng nhập theo hồ sơ đứng tên hợp đồng; tính tổng từ snapshot dòng, chỉ trừ thanh toán đã xác nhận, áp dụng ngày Việt Nam. Lọc/sắp xếp/phân trang ở backend; kỳ `yyyy-MM` và trạng thái ngoài danh mục bị từ chối. Không đổi schema.
- Màn hình có lọc tháng/trạng thái, xóa lọc, 6 cột, nhãn quá hạn/số ngày trễ, tải/lỗi/rỗng/không khớp; chọn dòng mở chi tiết và giữ bộ lọc/trang khi quay lại. Bổ sung hóa đơn của khách thứ hai trong demo S3-08 để kiểm tra không lộ chéo; giữ 8 tình huống thanh toán qua nhiều kỳ.
- S3-07 đã triển khai đủ AC1–AC4. Kiểm thử mới `InvoiceTenantListTests` bao gồm phân quyền, 12 hóa đơn/phân trang/thứ tự, đủ trạng thái, lọc đơn/kết hợp/rỗng, kỳ/trạng thái lỗi, nhãn quá hạn đồng nhất với chi tiết và giữ query bộ lọc.
- Build web + test và build tiện ích demo S3-08: PASS, 0 lỗi. Cảnh báo ImageSharp license, `CS8601` trong `AuthController` và `xUnit2013` hiện có. Chạy test mục tiêu ban đầu phát hiện ngày hạn sai trong dữ liệu test; đã sửa. Lượt chạy lại không khởi chạy được vì Windows Application Control chặn load DLL kiểm thử (`0x800711C7`), nên chưa xác nhận pass/fail runtime cho bộ test mới. Chưa chạy trình duyệt/Safari/thiết bị 360px.

## 10/10/2026 — S3-07 AC2–AC3: tổng kết và hạn thanh toán hóa đơn khách thuê

- PO chốt: tổng cộng lấy các dòng chi tiết, phụ phí cộng và giảm trừ trừ; tiền cọc/công nợ kỳ trước không gộp. Hạn mặc định là ngày phát hành + 7 ngày và được lưu trên hóa đơn. Chỉ khoản thanh toán `DA_XAC_NHAN` được cộng; trả dư vẫn giữ số đã trả thực tế nhưng công nợ tối thiểu bằng 0.
- Quá hạn khi hóa đơn đã phát hành, còn phải trả > 0 và ngày Việt Nam đã qua hạn (ngày hạn chưa quá hạn). Số ngày trễ là hiệu ngày lịch; nhãn đỏ “Quá hạn N ngày” không hiện khi chưa quá hạn hoặc đã trả đủ.
- Thêm bảng lưu các khoản thanh toán với số tiền, ngày và trạng thái; updater cộng thêm schema v23, giữ nguyên dữ liệu cũ và kiểm tra FK. Endpoint khách thuê trả tổng cộng, đã thanh toán, còn phải trả, hạn `dd/MM/yyyy`, cờ quá hạn/số ngày trễ. Cả hai đường chi tiết hóa đơn của khách hiển thị đủ bốn dòng tổng kết; trang dòng chi tiết đặt nhãn cạnh hạn.
- Demo S3-08 có các hóa đơn mẫu chưa trả, trả một phần, trả đủ, quá hạn nhiều ngày, quá hạn một ngày, đúng hạn, chưa đến hạn và trả dư; mã ghi tại `tenant-invoice-samples.txt`.
- Xác minh: 12/12 test có tên `Invoice` PASS (bao gồm tổng kết, nhiều khoản thanh toán, trả dư, biên ngày/múi giờ Việt Nam, quyền và render chi tiết); 3/3 test chi tiết tổng kết/schema PASS; build ứng dụng và `verification/S308` PASS; demo trên bản sao mới tạo đủ 8 kịch bản, cùng Nháp tháng cho bản chuẩn bị. `git diff --check` PASS. Build có cảnh báo ImageSharp thiếu license và CS8601 có sẵn trong `AuthController`.
- Chưa kiểm tra trực quan bằng trình duyệt, Safari/thiết bị thật, SMTP thật hoặc database người dùng thật. Bảng thanh toán v23 hiện chỉ lưu phần tối thiểu phục vụ tổng kết AC2/AC3, chưa triển khai đầy đủ luồng thu tiền S4.

## 09/10/2026 — S3-07 AC1: chi tiết dòng hóa đơn khách thuê

- PO chốt cho AC1: tiền phòng cố định; điện/nước theo chỉ số; dịch vụ cấu hình, Internet và phí dịch vụ theo giá cố định; không dùng khoán theo người trong danh sách AC này; tiêu thụ giữ tối đa 3 chữ số thập phân; làm tròn thành tiền từng dòng tới đồng `AwayFromZero`; tiền hiển thị VND, dấu chấm phân tách nghìn và hậu tố `đ`; khoản không có chỉ số hiển thị `—`; điện không tính bậc thang.
- Trang mở trực tiếp theo mã hóa đơn và endpoint dữ liệu giới hạn hóa đơn đã phát hành/đã hủy cho `KHACH_THUE` đứng tên hợp đồng; các trường hợp không có quyền trả cùng 404. Giữ snapshot các dòng, không trả thông tin cá nhân khác.
- AC1 trước đó đã kiểm thử trên SQLite tạm; demo sử dụng bản sao S3-08, không thao tác database local.

## Bổ sung quyết định PO — chặn dịch vụ khoán theo người

- Không cho tạo mới hoặc phát hành hóa đơn khi dịch vụ áp dụng cho phòng đang tính `THEO_NGUOI`; áp dụng cho xem trước, tạo Nháp tháng, phát hành trực tiếp/hàng loạt và phát hành Nháp cũ. Báo tên dịch vụ và yêu cầu đổi sang cố định (`CO_DINH`); xem trước bỏ qua phòng đó, không lập hóa đơn một phần.
- Thêm kiểm thử dịch vụ cho từ chối phát hành trực tiếp, bỏ qua khi xem trước/phát hành hàng loạt và chặn phát hành Nháp đã tạo trước khi cấu hình đổi. Test hồi quy hóa đơn theo đầu người cũ được chuyển sang kiểm tra snapshot số người hoặc phí cố định tương ứng.
- Xác minh: 96/96 test `RoomServicesTests` (bỏ qua một kiểm thử luồng giao diện người ở ghép) và 1/1 test chi tiết hóa đơn khách thuê PASS; `git diff --check` PASS. Kiểm thử bị bỏ qua `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` hiện dừng ở assertion form “Ghi nhận chuyển đi” trên trang hợp đồng; chưa sửa vì không thuộc thay đổi phát hành hóa đơn. Build có cảnh báo ImageSharp license và CS8601 đã biết.

## 09/10/2026 — S3-08 phần 4: lịch sử sửa Nháp

- Nhánh feature/S3-08/04-invoice-draft-history kế thừa phần 3. Ghi một bản trước/sau cho mỗi lần lưu có thay đổi, cùng transaction; có tên/người sửa, giờ UTC, chỉ số, tiền và ghi chú phát sinh/giảm trừ. Thao tác rỗng/sai/stale không thêm lịch sử. Không đổi công thức hoặc schema v22; giữ audit cũ, không backfill thông tin không tồn tại.
- Lịch sử mới nhất ở trên, giữ nguyên sau phát hành/hủy, bản thay thế có lịch sử riêng. Chủ nhà trong phạm vi sở hữu xem được; khách không nhận lịch sử nội bộ. 193 kiểm tra S3-08 PASS, gồm rollback riêng khi ghi lịch sử lỗi, race, quyền, append-only, mã hóa HTML, 360px và luồng phát hành/hủy/thay thế. CSDL gốc và demo trước giữ nguyên; web 5249 chạy trên bản sao mới. Chưa commit/push phần 4.
- Hồi quy toàn bộ: 560/569 PASS, 9 FAIL trùng lỗi cũ, 0 test lỗi mới so với phần 3. Bộ nhật ký hiện có 10/10 PASS. Hướng dẫn test/kết quả/giới hạn: [s3084-lich-su-sua-nhap.md](s3084-lich-su-sua-nhap.md).

## 09/10/2026 — S3-08 lát 3: hủy có lý do và phát hành lại

- Nhánh feature/S3-08/03-invoice-cancel-reissue kế thừa phần 1/2. Chủ nhà hủy Đã phát hành có lý do/xác nhận/phiên bản; giữ tiền, dòng, ghi chú, mã và lần phát hành của bản cũ. Tạo Nháp thay thế có liên kết hai phía, sao chép đủ snapshot; bấm lặp/đồng thời không sinh trùng. Sửa/phát hành qua luồng hiện có, khách nhận thông báo mới, bản hủy vẫn tra cứu được.
- Chặn hủy khi có khoản thu/cấn cọc đang hiệu lực hoặc chờ xử lý, schema tiền chưa tương thích hoặc email đang gửi. Hủy dừng email chưa gửi, không mở khóa/ghi lại chỉ số nguồn. Khách không được xem bản thay thế khi còn Nháp. Danh sách tháng giữ bản hủy và nhãn trạng thái chính xác.
- Schema v22 thay guard, không thêm bảng/cột nghiệp vụ và không sửa bước migration cũ. Nâng cấp trên bản sao v21, kiểm tra dữ liệu trước/sau và chạy lặp, integrity/FK PASS. CSDL gốc và demo trước đó giữ nguyên; demo mới dùng thư mục riêng dưới data/s308-demo.
- 174 kiểm tra service/HTTP/Chrome PASS, gồm rollback audit/ghi dòng, race tạo thay thế, chống sửa bản hủy, nguồn chỉ số đã khóa, quyền, CSRF, trạng thái/thông báo/link, tính lại tiền chính xác, giao diện 360px và email pickup. Sửa lỗi partial khi mở từ ThongBao, nguồn đã khóa chặn phát hành bản thay thế, ẩn bản hủy và nhãn phát hành sai. Hướng dẫn/kết quả hồi quy: [s3083-huy-phat-hanh-lai.md](s3083-huy-phat-hanh-lai.md).
- Web demo cổng 5249, CSDL bản sao v22; email local. Chưa kiểm tra SMTP thật/Safari/tải production; chưa làm giao diện nhật ký chi tiết trước/sau các lần sửa. Chưa commit/push phần 3.
- Hồi quy toàn bộ và chạy lại test sau sửa fixture: tổng hợp 560/569 PASS, 9 FAIL đã có trên baseline trước phần 2. Cả 7 test schema liên quan PASS; chi tiết và giới hạn nằm trong báo cáo phần 3.

## 09/10/2026 — Rà soát tương thích S3-08 phần 2

- Sửa lỗi 500 trên trang khách thuê khi CSDL chưa cài module hóa đơn; kiểm tra module và quyền trước chuông, trang thông báo và hàng đợi email. 105 kiểm tra S3-08 PASS; 4 test schema và 3 test HTTP Nháp hàng loạt PASS sau cập nhật fixture/kỳ vọng cho luồng mới. Hồi quy tổng hợp 557/569 PASS sau chạy lại 3 test; 12 FAIL còn lại cũng FAIL trên commit trước phần 2 (xem báo cáo). CSDL gốc không đổi. Thêm project verification/S308Regression để chạy test có sẵn; kết quả và giới hạn: [s3082-ra-soat-loi.md](s3082-ra-soat-loi.md).

## 09/10/2026 — S3-08 lát 2: phát hành và thông báo

- Nhánh feature/S3-08/02-invoice-publish-notify kế thừa lát 1. Thêm xác nhận phát hành, kiểm tra phiên bản/quyền/CSRF, lưu trạng thái và thông báo trong cùng transaction; gửi email từ hàng đợi sau commit. Đã phát hành khóa sửa, hiển thị thời điểm; khách đứng tên xem thông báo và hóa đơn chỉ đọc.
- Schema v21 bổ sung thong_bao; chỉ nâng cấp bản sao/demo, giữ nguyên CSDL gốc. 97 kiểm tra service/HTTP/Chrome PASS, kiểm tra giao diện 360px và email pickup. Chưa kiểm tra SMTP thật/Safari/regression toàn dự án; chưa làm hủy/thay thế/nhật ký riêng. Chi tiết: [s3082-phat-hanh-thong-bao.md](s3082-phat-hanh-thong-bao.md).

## 09/10/2026 — S3-08 lát 1: hóa đơn Nháp

- Nhánh `feature/S3-08/01-invoice-draft-edit`: các action tạo hóa đơn trên web lưu `NHAP`; tạo từng hợp đồng hoặc danh sách tháng không phát hành, không khóa chỉ số gốc, không gửi thông báo.
- Chủ nhà có quyền TAI_CHINH và sở hữu tòa được sửa snapshot chỉ số, thêm PHAT_SINH/GIAM_TRU kèm tên, số tiền nguyên đồng dương và ghi chú bắt buộc. Giữ đơn giá snapshot; decimal, làm tròn từng dòng AwayFromZero; giảm trừ lưu dương và trừ khi cộng tổng. Chặn tổng âm/tràn số, chỉ số sai, dòng chỉ số giả, bản đã phát hành và phiên bản cũ. Lưu trong transaction, CSRF trên POST; danh sách tháng hiển thị Nháp để mở lại.
- Không đổi schema; dùng cột ghi_chu có sẵn. Không chạy updater hoặc chỉnh dữ liệu gốc. Mã SHA-256 của QL_PhongTro/Data/local-dev.sqlite giữ nguyên trước/sau kiểm thử. Bộ demo S3-08 dùng bản sao riêng dưới data/s308-demo; bổ sung chỉ số giả và kỳ giá nước theo chỉ số trên bản sao (không dùng giá này cho môi trường thật).
- Build PASS, 46 kiểm tra service/HTTP PASS trên SQLite giả mới: 10 ví dụ làm tròn, tạo Nháp đơn/hàng loạt/chạy lại, sửa chỉ số giữ nguồn gốc, phát sinh/giảm trừ, ghi chú, tổng âm/tràn, lưu cũ, bất biến sau phát hành, integrity/FK, form/binding, CSRF, chủ nhà khác và quyền ghi 4 vai trò, không tạo email. Lệnh: `dotnet run --project verification/S308/S308.csproj -p:OutputPath="$pwd/data/s308-check/" -- .`.
- Bộ xUnit hiện thiếu Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj trong checkout nên dùng verification/S308 riêng. Giữ UnitTest1.cs sẵn có. Cảnh báo ImageSharp/CS8601 cũ còn tồn tại. Chưa kiểm trực quan mobile/Safari hoặc regression toàn dự án; không bổ sung phát hành/hủy/thay thế/nhật ký riêng trong lát này. Audit nền hiện có tiếp tục hoạt động.
- Chờ PO xác nhận danh mục loại chi tiết; hiện dùng hai nhóm và tên khoản tự nhập. Quy tắc làm tròn kế thừa AGENTS.md. Hướng dẫn và giới hạn: [s3081-hoa-don-nhap.md](s3081-hoa-don-nhap.md). Chạy demo: `./verification/Start-S308Demo.ps1`, cổng 5249. Chưa commit/push.


## 08/10/2026 — Admin tạo tài khoản Admin và đổi vai trò

- Form tạo tài khoản thêm Quản trị viên (Admin), backend cho phép ADMIN cùng CHU_NHA/QUAN_LY. Giữ mật khẩu tạm BCrypt, gửi email và bắt buộc đổi mật khẩu lần đầu; hỗ trợ gửi lại mật khẩu tạm cho Admin mới.
- Danh sách tài khoản thêm nút Đổi vai trò tới form chọn KHACH_THUE/CHU_NHA/QUAN_LY/ADMIN. GET/POST chỉ ADMIN, POST có CSRF. Không tự đổi vai trò của tài khoản đang đăng nhập; không đổi khỏi vai trò chủ nhà/quản lý khi còn sở hữu/được phân công tòa nhà. Giữ hồ sơ thuê và lịch sử hiện có; không chuyển hoặc xóa dữ liệu theo vai trò.
- So vai trò gốc để từ chối form đã cũ. Đổi vai trò, nhật ký allowlist và thu hồi cookie/JWT/refresh cùng transaction; lỗi audit rollback toàn bộ. Không đổi schema, không cập nhật DB thật, không stage/commit; giữ file import ảnh chưa theo dõi và project test hiện có.
- Build Debug PASS; 12/12 test HTTP trên SQLite tạm PASS: tạo Admin/bắt đổi mật khẩu/gửi lại, 403 ba vai trò khác, CSRF, vai trò sai/form cũ/tự đổi, ràng buộc tòa nhà, nhật ký/rollback, thu hồi cookie/JWT/refresh. Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter "FullyQualifiedName~ManagedRole|FullyQualifiedName~ManagedAccount_CreateAdmin"`. `git diff --check` PASS. Cảnh báo NU1900/ImageSharp/CS8601 và analyzer test cũ còn tồn tại.
- Chưa kiểm trực quan 360px/Safari, SMTP thật hoặc bộ regression toàn dự án. Giao diện dùng auth-card/form responsive và bảng cuộn hiện có.

## 08/10/2026 — S3-06 chống tạo trùng và hoàn thiện phát hành hàng loạt

- PO xác nhận giữ một hóa đơn chưa hủy/hợp đồng/tháng; phòng đã có hóa đơn nằm trong nhóm riêng và có liên kết xem. Kiểm tra lại trong transaction SQLite immediate + unique index hiện có; gửi lại POST/chạy lại kỳ/hai lượt đồng thời không tạo trùng, phòng vừa đủ dữ liệu vẫn được phát hành sau khi kiểm tra lại. Không đổi schema hoặc DB local.
- Thêm màn hình hoàn tất theo mẫu, bỏ thanh ba bước: banner/thời gian thực tế, số mới/thiếu chỉ số/đã có/bỏ qua khác, tổng tiền mới/hạn, tab kết quả từng phòng, tìm/lọc tầng/phân trang, CSV toàn bộ kết quả, thông tin lần chạy và nút chạy lại/kỳ khác. Cho chạy lại kể cả 0 ứng viên; bảo vệ danh sách/ngày/fingerprint/quyền/CSRF tiếp tục giữ nguyên. Báo cáo server-side tối đa 24 giờ khi app còn chạy, hóa đơn/audit lưu SQLite lâu dài.
- Test mới MonthlyBulk 4/4 PASS; hồi quy 188/189 PASS, lỗi cũ DepartureHttpWarningSnapshotsAndFutureUnissuedBill thiếu “Ghi nhận chuyển đi” ở người ở ghép. Build 0 lỗi. Test đồng thời hai lượt 50 phòng đúng 50 hóa đơn/100 chỉ số khóa; test HTTP xác nhận tới trang kết quả dưới 30 giây, lặp POST an toàn, CSV đầy đủ, thu hồi sở hữu chặn 403.
- Chrome demo tổng hợp mới: 50 phòng đủ tiền phòng/điện/nước/Internet/rác/khoán người phát hành trong **1,426 giây** từ xác nhận tới kết quả (dịch vụ 0,80 giây), tổng 203.000.000 đ; chạy lại 0 mới/50 đã có trong 0,690 giây. A105 được quản lý chốt nước qua UI rồi chạy lại chỉ tạo 1 mới, giữ 4 bản trước. Desktop/360px, tab/lọc/phân trang/CSV/liên kết/403 và integrity/FK PASS; chưa Safari/thiết bị thật/production load. Chi tiết và lệnh tái hiện: [s306-bulk-issuance.md](s306-bulk-issuance.md).

## 08/10/2026 — S3-06 kiểm tra trước phát hành và quản lý ngày thanh toán

- PO xác nhận cho phép chọn ngày phát hành quá khứ/tương lai và phát hành ngay sau xác nhận. Ngày nghiệp vụ tách khỏi thời điểm thao tác: thêm `hoa_don.ngay_phat_hanh_nghiep_vu` (date ánh xạ DateOnly, SQLite TEXT nullable); `ngay_lap`, `ngay_phat_hanh` và audit vẫn là UTC thực tế. Kỳ hóa đơn/giá theo ngày chốt hợp đồng không phụ thuộc ngày phát hành đã chọn. Hạn mặc định = ngày phát hành +7 ngày, chủ nhà sửa được trước xác nhận; backend chặn hạn trước ngày phát hành, ngày ngoài 1900–9998. Hóa đơn cũ giữ NULL, hiển thị ngày cũ từ timestamp UTC+7, không backfill/suy đoán.
- Trang `/HoaDonDichVu/Monthly` theo ảnh mẫu và không có thanh ba bước. Có năm thẻ tổng hợp, chọn tòa/kỳ/ngày/hạn, bảng sẵn sàng với từng khoản/khách thuê, tab bị bỏ qua kèm lý do; bên phải là kết quả kiểm tra/phòng bỏ qua/thông tin kỳ. Có chọn phòng, lọc tên/phòng/tầng, sắp xếp tổng/mã, phân trang 5 dòng và xuất CSV bảng kiểm tra thực tế. Ô ngày dùng dd/MM/yyyy thống nhất và có lịch chọn; sửa ngày phát hành tự cập nhật +7 nếu hạn còn mặc định, giữ hạn khi đã sửa riêng. Thay đổi thông tin yêu cầu kiểm tra lại trước khi xác nhận.
- Xem trước từng phòng cũng sửa được ngày/hạn và giữ thông tin khi điều hướng từ danh sách. “Tiếp tục phát hành” mở dialog nêu số hóa đơn/tổng/ngày/hạn; bắt buộc xác nhận checkbox. Backend yêu cầu `xacNhan`, CSRF, quyền TAI_CHINH và sở hữu. Bản kiểm tra được bảo vệ bằng Data Protection, gắn actor/tòa/kỳ/ngày/hạn/danh sách sẵn sàng và fingerprint giá/chỉ số/phiên bản/số người trong 30 phút. Không thể đưa phòng bị bỏ qua vào POST; dịch vụ kiểm lại trong transaction, báo bỏ qua khi dữ liệu thay đổi, phòng không còn thuê hoặc thiếu chỉ số. Phòng mới được chốt sau bản xem trước không tự được phát hành ngoài danh sách đã xác nhận.
- Liệt kê lý do: thiếu điện/nước/cả hai, chỉ số không hợp lệ/khóa, thiếu giá/dịch vụ theo chỉ số, dịch vụ ngoài hợp đồng, không có hợp đồng hiệu lực/kỳ trọn tháng/ngày chốt hợp lệ hoặc đã có hóa đơn. Sau thao tác hiển thị kết quả từng phòng: đã phát hành/bị bỏ qua/không chọn và liên kết hóa đơn. Hóa đơn và khóa chỉ số + audit cùng transaction; bản đã phát hành bất biến.
- **Schema v20**: `InvoiceIssueDateSchema` thêm cột nullable, giữ nguyên các bước v1–v19 và dữ liệu; updater có backup sẵn và bước v20 transaction. Initializer module hóa đơn áp dụng cột bổ sung trên DB mới; AccountReuseSchema chỉ mở tương thích tới v20. Không nâng cấp DB local hoặc DB demo cũ đang dùng. Kiểm trên bản sao mới từ demo v19: mọi bản ghi cũ không đổi, backup/integrity/FK PASS, rerun không đổi, check-database PASS. Có test tự động bảo toàn hóa đơn/dòng cũ, cột mới NULL và rerun. Lệnh kiểm copy: `python verification/s306_issue_date_upgrade.py <demo-v19-đã-dừng> <thư-mục-copy-mới-trong-data/s306-demo>`.
- Khi áp dụng cho DB hiện có: dừng web, tạo bản sao mới/backup, trỏ `DatabasePath` tới bản sao, chạy `dotnet run --project QL_PhongTro -- --update-database` rồi `--check-database`, xác minh integrity/FK và dữ liệu trước khi áp dụng theo quy trình nhóm. Không có nâng cấp ngầm khi mở trang. Tạo DB rỗng bằng LocalDatabaseInitializer đã kiểm qua fixture và demo mới v20; cài module hóa đơn bằng CLI `--initialize-service-invoices` khi cần.
- Build C#/Razor PASS. Kiểm cuối `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~Monthly|FullyQualifiedName~InvoiceDateSchema|FullyQualifiedName~MeterAnomalySchema|FullyQualifiedName~SchemaBranchReconciliation'`: **34/34 PASS**. Có ca hạn +7 qua tháng/năm/năm nhuận, ngày phát hành trước/sau hôm nay, hạn riêng, UTC thao tác độc lập, danh sách/lý do/phòng thiếu chỉ số không tạo hóa đơn, ngày hoặc phòng bị giả mạo, bắt buộc xác nhận, dữ liệu thay đổi/phòng không còn thuê và bảo toàn schema. Bộ liên quan rộng đã chạy: **183/184 PASS**, còn cùng lỗi `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` ngoài phạm vi ở màn hình người ở ghép.
- Chrome desktop/360px PASS: ô ngày dd/MM/yyyy, ngày 07/11 → hạn 14/11, sửa hạn 25/11, chặn ngày sai/hạn trước phát hành, lọc/sắp xếp/tab bỏ qua/CSV, dialog xác nhận không POST nếu chưa tick, giao diện không tràn ngang, lưu đúng ngày nghiệp vụ/hạn và kết quả từng phòng. Demo mới v20 trong đường dẫn `data/s306-demo/review-latest.txt` (ignored), tòa **Demo hóa đơn đầy đủ** ID 5, kỳ 10/2026: A101/A102/A103 còn sẵn sàng, A104 đã phát hành thử 4.220.000 đ với ngày 07/11/2026 và hạn 25/11/2026, A105 thiếu nước không có hóa đơn. Ảnh/check JSON/CSV trong thư mục demo. Kiểm browser: `python verification/s306_review_browser.py <demo-v20>` (cần ≥2 phòng chưa phát hành, chạy trên demo riêng; script phát hành một phòng). Chạy lại bằng `./verification/Start-Sprint2Demo.ps1 -Directory <thư-mục-demo>`.
- Chưa nghiệm thu Safari/thiết bị thật, chưa kiểm hiệu năng 50 phòng hoặc mở story chống trùng riêng (giữ cơ chế cũ). Cảnh báo ImageSharp license/CS8601/xUnit2013 hiện có còn tồn tại; không sửa module ngoài phạm vi.

## 08/10/2026 — S3-06 bổ sung dịch vụ cố định và khoán theo đầu người

- Luồng tháng lấy tất cả dịch vụ CO_DINH/THEO_NGUOI đang gán cho phòng và thuộc danh sách hợp đồng (hợp đồng cũ chưa có snapshot dịch vụ dùng danh sách phòng). Dùng `LayGiaHoaDonAsync` tại ngày chốt: giá riêng ưu tiên, dịch vụ ngừng từ kỳ sau/không được chọn/ngoài hợp đồng không tính. Cố định = 1 × giá; theo người = số người tính phí × giá; từng dòng decimal/AwayFromZero rồi cộng tổng tiền phòng + điện + nước + các dịch vụ.
- PO xác nhận giữ cách tính người hiện có: người đứng tên + người ở ghép từ tháng trước; mới vào tính từ tháng sau, rời phòng tính hết tháng rời phòng. Số người không lấy từ form. Lưu snapshot số người, tên, cách tính, đơn vị, đơn giá, số lượng, thành tiền; sửa giá/số người sau phát hành không đổi hóa đơn đã lưu.
- Bỏ thanh “Cấu hình kỳ – Kiểm tra hóa đơn – Phát hành” theo yêu cầu. Trang tháng có cột dịch vụ bổ sung và liên kết `/HoaDonDichVu/Preview` tới trang xem trước đầy đủ từng khoản. Trang xem trước/chi tiết dùng bố cục ảnh: bảng khoản thu, chỉ số sử dụng, số người, thông tin hóa đơn, bảng đơn giá, tổng cuối và bản in thực qua browser print. Xem trước không tạo chứng từ trong DB. Có thể phát hành một phòng từ trang xem trước hoặc cả tòa từ danh sách; backend kiểm lại dữ liệu và quyền trong transaction, ghi audit và khóa chỉ số. Không thêm sửa nháp/điều chỉnh/hủy hóa đơn ngoài story.
- Không đổi schema, không ghi vào DB local, không thay file csproj test. Ngày phát hành tự động và hạn +7 ngày; chưa mở chỉnh ngày/hạn, tổng hợp phòng bỏ qua hoặc cam kết hiệu năng 50 phòng. Giữ cơ chế chống trùng đã có, không triển khai thêm story riêng.
- Build C#/Razor PASS; `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~Monthly`: **23/23 PASS**. Có ca không có bổ sung, nhiều khoản cố định, 1/2/4 người, giá riêng, thời điểm vào/rời phòng, dịch vụ ngừng/ngoài hợp đồng, tổng đầy đủ, snapshot bất biến, HTTP xem trước/phát hành một phòng, 403, CSRF và rollback audit. Bộ liên quan `--no-build --no-restore --filter 'FullyQualifiedName~RoomServicesTests|FullyQualifiedName~MeterReading'`: **173/174 PASS**, cùng ca người ở ghép `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` không tìm thấy nút “Ghi nhận chuyển đi” đã ghi ở lần trước; ngoài phạm vi sửa.
- Chrome desktop 1672px và mobile 360px PASS: kiểm từng khoản/tổng, trang không tràn ngang, tiêu đề vừa màn hình, print hoạt động/ẩn sidebar, bỏ thanh bước, phát hành A104 qua browser, lưu 6 khoản/3 người/tổng 4.220.000 đ/khóa đủ chỉ số. integrity_check/FK PASS; không có lỗi JavaScript. Chưa nghiệm thu Safari/thiết bị thật. Cảnh báo ImageSharp license/CS8601/xUnit2013 hiện có còn tồn tại.
- Demo mới nằm tại đường dẫn trong `data/s306-demo/full-latest.txt` (ignored); tòa **Demo hóa đơn đầy đủ**, ID lấy từ `access.json.monthlyBuildingId`, kỳ 10/2026. A101 chỉ phòng/điện/nước = 3.780.000 đ; A102 thêm Internet/Rác = 3.980.000 đ; A103 thêm khoán 1 người = 4.060.000 đ; A104 khoán 3 người = 4.220.000 đ (đã phát hành khi kiểm browser); A105 thiếu nước bị bỏ qua. Tài khoản/mật khẩu chỉ lưu tại access.json. Tạo mới bằng CLI `--create-sprint2-demo` của ứng dụng với thư mục mới trong data/s306-demo, rồi `python verification/s306_prepare_demo.py <thư-mục-demo>`. Kiểm browser: `python verification/s306_browser.py <thư-mục-demo>` (cần Playwright local có sẵn trong data/task1-build/browser-packages và Chrome). Chạy lại demo: `./verification/Start-Sprint2Demo.ps1 -Directory <thư-mục-demo>`. Ảnh desktop/mobile/print và browser-checks.json nằm trong thư mục demo.

## 08/10/2026 — S3-06 phát hành hóa đơn tháng: tiền phòng, điện, nước

- Trang `/HoaDonDichVu/Monthly` dùng bố cục ảnh tham chiếu, chọn tòa/tháng/năm, kiểm tra từng khoản và chỉ số/đơn giá, phát hành cả tòa, xem lịch sử theo kỳ và chi tiết hóa đơn. Menu Hóa đơn, thanh toán của chủ nhà dẫn tới trang mới; luồng hóa đơn dịch vụ cũ giữ nguyên.
- PO xác nhận kỳ tháng dương lịch; lấy giá tại ngày chốt **từng hợp đồng**, ngày chốt vượt số ngày tháng được giới hạn tới cuối tháng. Tiền phòng giữ quy tắc hiện có: một kỳ hợp đồng bao phủ trọn tháng với một mức giá; chưa xử lý kỳ lẻ/gia hạn giữa tháng. Điện/nước dùng bản chỉ số đã lưu đúng hợp đồng và đủ hai đầu ngày của tháng; thiếu một chỉ số, giá hợp lệ hoặc dịch vụ theo chỉ số thì bỏ qua phòng.
- Tiền tính bằng decimal, làm tròn từng dòng AwayFromZero. Snapshot chỉ số/giá/tiền, hạn tự động hôm nay Việt Nam +7 ngày; khóa bản chỉ số, phát hành và audit trong một transaction SQLite khóa ghi. Giữ ràng buộc chống trùng hiện có, không thêm module chống trùng riêng.
- Backend chỉ CHU_NHA có quyền TAI_CHINH và sở hữu tòa được phát hành; POST có CSRF. Không đổi schema, không chạy updater/initializer hoặc ghi dữ liệu vào DB local. DB local thực tế hiện v5, chưa có bảng hợp đồng/chỉ số/hóa đơn; khác tài liệu AGENTS mô tả mã sprint 3 chưa có nhưng checkout đã có hợp đồng/chỉ số. Nếu DB triển khai thiếu module, trang báo chưa sẵn sàng; cần người vận hành thiết lập bằng công cụ hiện có trên bản sao trước.
- Ngoài phạm vi theo yêu cầu: dịch vụ cố định/khoán người trong luồng mới, chỉnh hạn thanh toán, tổng hợp phòng bị bỏ qua, kiểm thử hiệu năng 50 phòng; chưa có email/thông báo chung. Không hiển thị các chức năng này thành nút giả.
- Kiểm thử mới trên DB tạm: 10 ví dụ tính tiền; nhiều phòng; thiếu nước; đổi giá đúng ngày chốt; khóa chỉ số; chạy lại không trùng theo cơ chế cũ; sai chủ nhà; rollback khi audit lỗi; HTTP render, 403 và CSRF. Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RoomServicesTests|FullyQualifiedName~MeterReading'`. Chưa kiểm trực quan bằng trình duyệt/360px/Safari; CSS có breakpoint mobile và bảng cuộn riêng. Cảnh báo ImageSharp license/CS8601/xUnit2013 có sẵn còn tồn tại.
- Kết quả build C#/Razor PASS. Chạy lại riêng `--filter FullyQualifiedName~Monthly`: 15/15 PASS. Bộ liên quan: 165/166 PASS; ca `DepartureHttpWarningSnapshotsAndFutureUnissuedBill` thất bại tại trang người ở ghép do không tìm thấy nút “Ghi nhận chuyển đi”, ngoài phạm vi thay đổi này. Không sửa module người ở ghép trong S3-06.


## 07/10/2026 — Giao diện chi tiết tòa nhà theo ảnh tham chiếu

- Thêm `/PhongTro/ChiTietToaNha/{id}`: thông tin tòa/địa chỉ/quản lý/ngày chốt, ảnh phòng đại diện, quy mô/diện tích, thống kê thuê/trống/đặt cọc và danh sách phòng. Bấm tên tòa tại danh sách mở trang mới; giữ liên kết tới quản lý phòng, chỉnh sửa tòa, thêm phòng và cấu hình điện nước theo quyền.
- Danh sách lọc mã phòng, tầng, trạng thái; sắp xếp tầng/mã phòng, 8 phòng/trang, phân trang giữ bộ lọc, trạng thái rỗng. Khách đứng tên và hạn hợp đồng lấy từ hợp đồng đang hiệu lực tại hôm nay Việt Nam qua ITimeProvider; không đưa SĐT/căn cước lên trang.
- Giữ nguyên sidebar, `_Layout.cshtml`, CSS chung và dữ liệu/schema. CSS mới chỉ nạp tại trang chi tiết và dùng class `bd-*`/`building-detail`. Không thêm nút xuất báo cáo/nhập danh sách giả. Chỉ số thu tiền/còn phải thu hiển thị chưa có dữ liệu, không dựng số mẫu; quản lý không thấy các thẻ tài chính/nút sửa/cấu hình.
- Backend kiểm quyền PHONG_TRO, sở hữu chủ nhà hoặc phân công quản lý; chủ nhà ngoài phạm vi 403, tòa không tồn tại 404. Build C#/Razor PASS; 2/2 test HTTP mục tiêu PASS (dữ liệu thực, hợp đồng, lọc, rỗng, phân trang, link, 403 và quản lý chỉ đọc). Kiểm trực quan desktop/360px qua browser: nội dung mới vừa màn hình, bảng cuộn riêng; không sửa sidebar. Ảnh kiểm chứng trong `data/building-detail-preview.jpg` (ignored). Chưa nghiệm thu Safari/thiết bị thật. Cảnh báo ImageSharp license/CS8601/NU1900 có sẵn còn tồn tại.
- Lệnh test: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~BuildingDetail`. Demo đã khởi động lại; mở `/PhongTro/ToaNha` rồi bấm tên tòa, hoặc `/PhongTro/ChiTietToaNha/1`.

## 08/10/2026 — S3-05:5 hoàn thiện ghi chỉ số trên mobile

- Cùng DOM/form theo phòng: bảng responsive thành thẻ 360px, tham chiếu sát ô, inputmode decimal, input/nút >=48px, status/progress rõ. Validate tức thời và focus/cuộn đúng field/room/warning; redirect success thêm fragment phòng. Chỉ presentation và một dòng redirect, giữ mọi nghiệp vụ lát 1–4, không thêm schema/S3-06/hóa đơn.
- Build cách ly PASS, 0 lỗi; Meter/schema 98/98 PASS (95 cũ + 3 HTTP mobile mới), bộ cuối kèm ba test nâng schema 101/101 PASS; JS harness 8/8 PASS. Regression cùng bốn nhóm lát 4 306/314 PASS, đúng 8 baseline FAIL, 0 skipped. So tập tên với TRX cuối lát 4 khớp hoàn toàn, không lỗi mới.
- Browser in-app 360x800 trên DB COPY riêng v18: không overflow cấp trang; lỗi điện/nước đúng ô, sửa/lưu từng phòng, warning điện và cả hai, không confirm vẫn không save, confirm save, progress tới 2/2, focus/vị trí/reload đạt. Desktop 1280x900 giữ bảng và lưu được. THEO_NGUOI không tạo meter nước giả; copy integrity/FK đạt, nguồn copy SHA256 không đổi; app đã dừng.
- Chưa kiểm thiết bị/bàn phím Android/iPhone thật, Safari/3G/stress. DB demo chính không bị ghi test hoặc nâng schema; demo S3-05 đầy đủ dùng fixture v18. Không stage/commit/push/merge/PR. Chi tiết AC5, test/filter, baseline và bằng chứng: [s3055-mobile-meter-entry.md](s3055-mobile-meter-entry.md).

## 08/10/2026 — S3-05:3: tiến độ ghi chỉ số theo tòa/kỳ

- PO duyệt mẫu số là các phòng trong danh sách hiện tại có `DichVu.Count > 0`; đếm phòng, không đếm dịch vụ/hợp đồng. ViewModel thêm computed properties `TongPhongCanChot`, `SoPhongDaChot`, `SoPhongConLai`; số đã chốt chỉ đếm `DaChot` trong cùng tập cần meter. Phòng không cần meter giữ nguyên list/status legacy nhưng không góp vào progress. Thiếu reference vẫn nằm trong tổng/còn lại, không giả 0 hay đổi validation.
- Razor hiển thị `Đã chốt: X/Y · Còn lại: Z phòng` sau bộ chọn tòa và trước danh sách tầng. Khi tổng bằng 0, cả ba values bằng 0 và UI ghi `Không có phòng cần ghi chỉ số trong kỳ này.` Không có tòa được phân công thì giữ thông báo hiện có. Kế thừa nguyên trạng cách chọn tập DichVu của lát 2, không phân loại thêm cấu hình lỗi/thiếu.
- Không đổi service/controller/save/schema/DB/updater, không JS/AJAX/CSS mới. Redirect GET sau save hoặc render khi POST lỗi đọc dữ liệu DB hiện có; input chưa lưu không tăng progress. Không triển khai lát 4/5, S3-06/S3-10 hoặc sửa baseline.
- Thêm 8 test progress dùng SQLite temp theo fixture hiện có: 0/N, một phần, N/N, cập nhật lại không tăng, phòng khác không đổi, nước theo người, chỉ một meter/chỉ đủ hai meter, kỳ/hợp đồng khác, thiếu reference/không fake 0, không-meter giữ legacy status/0 tổng, không trộn tòa/không lặp phòng, concurrency/stale/audit rollback, HTTP invalid/redirect/GET mới/zero message. Bộ S3-05/schema gồm 55 ca cũ + 8 mới: **63/63 PASS**. Test quyền/module/phân công và CSRF của lát 1/2 chạy lại cùng bộ. Build C#/Razor PASS, 0 lỗi; cảnh báo ImageSharp/CS8601 và analyzer của test lát 2 có sẵn.
- Browser in-app thật trên DB copy disposable `data/s3053-acceptance/copy.sqlite` (ignored): bản sao nguồn v17, thêm một bàn giao hợp đồng 1/ngày 01/08/2026/điện 100/nước 20/người nhập 1. Ban đầu 0/2/còn 2; điện 99 báo lỗi và không tăng; lưu 110 rồi reload 1/2/còn 1, C002 thiếu reference vẫn Chưa chốt/còn lại. Nước THEO_NGUOI không input/record. Bản sao sau save đúng một dòng DIEN/hợp đồng 1/kỳ 01–31/10/2026/100→110/người nhập 6/da_khoa=0/phien_ban=0; integrity_check=ok, foreign_key_check rỗng. Ảnh `data/s3053-acceptance/progress-after-save.png` ignored. Fixture hai meter và N/N được chứng minh bằng automated tests; không chạy browser hai-meter lần này. App nghiệm thu đã dừng.
- Regression bốn nhóm: **278/286 PASS, 8 FAIL, 0 skipped**, log `data/test-results/s3053-regression.trx`. Tập 8 tên lỗi khớp chính xác TRX nghiệm thu lát 2 `s3052-acceptance-regression.trx`, không có failure mới; không sửa baseline. Đây không phải full suite. Review diff và git diff --check PASS; branch feature/S3-05/03-reading-progress, HEAD 5208a65 giữ nguyên; 3 modified + 1 untracked, không staged/commit/push/merge/switch.
- DB demo chính/backup không bị ghi hoặc sửa; SHA-256 nguồn trước/sau `6c86a6e2022ec7b96a12a7e296b8f22bfe7769bb8b952b7b5bda29b4744284d9`. Không sửa csproj test. Chưa nghiệm thu Chrome/Safari/thiết bị vật lý hoặc tối ưu mobile thuộc lát 5.
- Lệnh: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --output data/s3053-build`; `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s3053-tests --filter FullyQualifiedName~MeterReadingListTests --logger 'trx;LogFileName=s3053-tests.trx' --results-directory data/test-results`. Regression dùng `--no-build --no-restore --output data/s3053-tests --filter 'FullyQualifiedName~MeterReadingListTests|FullyQualifiedName~PermissionTests|FullyQualifiedName~ContractCreationTests|FullyQualifiedName~RoomServicesTests' --logger 'trx;LogFileName=s3053-regression.trx' --results-directory data/test-results`.

## 07/10/2026 — Nghiệm thu kỹ thuật S3-05:2 (trước commit)

- PASS phạm vi lát 2. Điều khiển trình duyệt in-app thật trên hai bản sao riêng ở `data/s3052-acceptance/`, local ignored. DB chính `QL_PhongTro/Data/s3-demo/sprint2.sqlite` đã được người dùng nâng lên v17 trước lượt này; agent không nâng/ghi DB chính và không sửa/xóa backup. SHA-256 nguồn trước/sau bằng nhau: `6c86a6e2022ec7b96a12a7e296b8f22bfe7769bb8b952b7b5bda29b4744284d9`.
- Sửa lỗi thông báo thiếu tham chiếu lặp: Razor trước đây có lời nhắc riêng ngoài span lỗi, trong khi JS/POST dùng span với cùng nội dung. Nay GET/POST/JS cùng dùng một span mỗi dịch vụ; giữ lỗi server khi đã có. Test HTTP kiểm đúng một thông báo mỗi form ở GET và POST cố chốt thiếu tham chiếu, DB không có bản giả.
- `copy.sqlite`: thêm đúng một dòng `hop_dong_chi_so_dau_ky` cho hợp đồng 1, ngày 01/08/2026, điện 100, nước 20, người nhập 1; ngày nằm trong kỳ hợp đồng. C001/phòng 6 có tham chiếu, C002/phòng 7 giữ thiếu. Nước THEO_NGUOI không có input/record meter. Trình duyệt: 99 lỗi ngay tại điện và chặn lưu; 100 bằng tham chiếu xóa lỗi/lưu được; cập nhật 125 và reload giữ số/trạng thái. C002 vẫn Chưa chốt, lời nhắc không lặp khi nhập. POST HTTP trực tiếp gửi 99 kèm previousReading=0 bị từ chối; cố chốt C002 cũng bị từ chối, không đổi DB.
- `both-meters.sqlite`: bản sao riêng khác từ nguồn, cùng dòng bàn giao; thêm cấu hình giá riêng phòng 6/id 7 cho NUOC/id 2, THEO_CHI_SO, 18.000 đ/m3, 01–31/10/2026, người tạo 1/ngày tạo 01/09/2026. Đây là fixture kiểm thử cấu hình từ kỳ sau, không đổi cấu hình nguồn/hóa đơn lịch sử. Trình duyệt xác minh riêng điện 99/nước 20, điện 100/nước 19, cả hai 99/19, chặn lưu; sửa 110/25 xóa cả hai lỗi, lưu/reload Đã chốt. C002 vẫn Chưa chốt.
- Kiểm SQLite trực tiếp: bản sao đầu đúng một dòng (hợp đồng 1, DIEN/id 1, đầu 100/cuối 125, người nhập 6, da_khoa=0, phien_ban=1); bản sao hai đúng hai dòng (hợp đồng 1, DIEN 100→110, NUOC 20→25, người nhập 6, da_khoa=0, phien_ban=0). Tất cả kỳ 01–31/10/2026; không bản của hợp đồng 2, không bản meter cho THEO_NGUOI. Cả hai integrity_check=ok, foreign_key_check rỗng. Không tạo invoice giả. Lưu có nhật ký như implementation hiện có; app cũng có các tác vụ hạ tầng thông thường trên COPY.
- Build riêng PASS (0 lỗi, cảnh báo license ImageSharp có sẵn). S3-05/schema 55/55 PASS; concurrency riêng 1/1 PASS; JS harness 8/8 PASS. Regression bốn nhóm 270/278 PASS, đúng 8 baseline cũ, không lỗi mới. So TRX với lượt 269/277: thêm đúng `StaleHttpFormKeepsOldVersionUntilExplicitReload` đã có trong source trước lượt này. Không khẳng định full suite hoặc Chrome/Safari/thiết bị vật lý đã nghiệm thu.
- Lệnh thực tế:
  - `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --output data/s3052-acceptance-build`
  - `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s3052-acceptance-runtime --filter FullyQualifiedName~MeterReadingListTests --logger 'trx;LogFileName=s3052-acceptance.trx' --results-directory data/test-results`
  - `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --output data/s3052-acceptance-runtime --filter 'FullyQualifiedName~MeterReadingListTests|FullyQualifiedName~PermissionTests|FullyQualifiedName~ContractCreationTests|FullyQualifiedName~RoomServicesTests' --logger 'trx;LogFileName=s3052-acceptance-regression.trx' --results-directory data/test-results`
  - `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --output data/s3052-acceptance-runtime --filter FullyQualifiedName~DuplicateConcurrentSaveAndOptimisticUpdate --logger 'trx;LogFileName=s3052-acceptance-concurrency.trx' --results-directory data/test-results`
- JS harness chạy Node stdin với JS production, mock DOM, 8 trường hợp điện/nước/cả hai/sửa hợp lệ/bằng/lớn hơn/precision/rỗng-thiếu tham chiếu và chặn submit. HTTP Python stdin dùng cookie/CSRF thật và đọc SQLite bản sao để đối chiếu không ghi khi lỗi. Ảnh bằng chứng nằm local ignored `data/s3052-acceptance/browser-after-save.png` và `browser-both-after-save.png`.
- App nghiệm thu đã dừng. Review phạm vi/diff và git diff --check PASS; chỉ 10 modified + 7 untracked source/test/docs thuộc lát 2, không staged, DB/backup/WAL/SHM/secret/temp không xuất hiện Git. Không commit/push/merge/switch; không triển khai các lát 3/4/5, S3-06/S3-10, không sửa baseline.

## S3-05, lát 2: nhập, kiểm tra và lưu từng phòng — schema v17

- Triển khai MeterReadingSchema v17 và entity/DbSet/audit cho chi_so_dien_nuoc; FK RESTRICT tới hợp đồng/dịch vụ/người nhập, unique hợp đồng/dịch vụ/khoảng tháng, CHECK tháng/decimal/đầu-cuối. Giữ da_khoa và phien_ban (concurrency token); guard bản đã khóa nhưng không có action khóa/mở khóa. Bước mới thêm dữ liệu rỗng, không backfill hay đổi bảng/dòng cũ. Updater chạy lặp nhận lại bảng chỉ khi định nghĩa bảng và mọi trigger khớp chính xác; schema lạ bị từ chối. AccountReuseSchema mở rộng phạm vi kiểm tra tới v17, giữ nguyên logic nâng/index cũ.
- GET/POST dùng cookie, ModuleAccess DIEN_NUOC (WRITE trên Save), phân công tòa và CSRF. Kỳ Việt Nam, phòng/hợp đồng/giao kỳ/dịch vụ/chỉ số đầu được kiểm lại server-side. Chỉ DIEN/NUOC được gán cho phòng, còn áp dụng và có cấu hình hiệu lực THEO_CHI_SO cần nhập; cấu hình riêng thắng mặc định, snapshot dịch vụ hợp đồng được tôn trọng khi đã có. THEO_NGUOI không tạo bản giả. Nguồn đầu: bản chốt kỳ trước → hóa đơn legacy hợp lệ → bàn giao. Thiếu tham chiếu chặn lưu, không giả 0.
- Form riêng từng phòng; client/server kiểm số hợp lệ, tối đa 3 số lẻ, giới hạn và mới >= trước. Lỗi tại ô/dòng tương ứng, sửa hợp lệ xóa lỗi; số input dùng binder bất biến. BEGIN IMMEDIATE + unique + phien_ban kiểm insert/update; stale báo tải lại và giữ token cũ trên form lỗi. Hai dịch vụ của phòng lưu cùng transaction với nhật ký; lỗi ghi/audit rollback, phòng khác không đổi. Reload đọc dữ liệu đã lưu; Đã chốt khi đầy đủ dịch vụ meter cần nhập của kỳ. Bản chưa khóa có thể lưu lại với version đúng.
- Build C#/Razor PASS. Toàn bộ 28 test lát 1 giữ nguyên; bộ S3-05/schema cuối 55/55 PASS (`data/test-results/s3052-complete.trx`), bổ sung test concurrency hai Task.Run/two connections 1/1 PASS (`s3052-concurrency.trx`). JavaScript harness Node PASS equal/greater/both wrong/correction/precision/missing reference/submit blocking. Chưa nghiệm thu trực quan trình duyệt, Chrome/Safari hay thiết bị 360px cho lát 2; harness không thay cho nghiệm thu browser.
- Updater: test SQLite temp v16→v17, bảo toàn mọi dòng cũ, idempotency, từ chối schema lạ/thiếu protections, constraints/FK/unique/bản khóa PASS. Thử bản sao read-only backup từ DB demo v16 với build cuối: hai lượt update PASS, integrity/FK PASS, mọi dòng cũ giữ nguyên, hash nguồn không đổi. Không chạy updater/seed hoặc sửa trực tiếp DB demo; nguồn còn v16. App repository đã dừng trước build, chưa tự khởi động lại.
- Regression filter MeterReadingListTests + PermissionTests + ContractCreationTests + RoomServicesTests: 269/277 PASS, đúng 8 baseline FAIL đã được đối chứng trên dev trước S3-05 (log `s3052-regression-final.trx`). UpdateV13 vẫn kỳ vọng 15, thực tế nay 17; bảy lỗi assertion/fixture còn lại như báo cáo baseline. Không sửa 8 test đó. Lượt trung gian còn lỗi của code mới (AccountReuse giới hạn version, bước v17 từ chối bảng giữ lại khi test xóa marker) và một tiêu đề UI làm test lát 1 lỗi; đã sửa và test lại đạt. Hai test nâng cấp V7/V5 từng lỗi đạt trong bộ mục tiêu 56/56 và trong regression cuối. Không khẳng định toàn suite PASS.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s3052-complete --filter FullyQualifiedName~MeterReadingListTests --logger 'trx;LogFileName=s3052-complete.trx' --results-directory data/test-results`; concurrency tương tự với filter DuplicateConcurrentSaveAndOptimisticUpdate và output data/s3052-concurrency. Regression cuối dùng --no-build --no-restore --output data/s3052-final-tests, filter bốn nhóm trên, log s3052-regression-final.trx. Build cuối `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --output data/s3052-ui-final`. Cảnh báo ImageSharp license/CS8601 có sẵn. Không sửa csproj test.
- Chi tiết thiết kế/vận hành: [s305-chi-so-theo-ky.md](s305-chi-so-theo-ky.md). Không triển khai progress S3-05:3, bất thường S3-05:4, tối ưu S3-05:5, phát hành S3-06 hay khóa kỳ S3-10. Không sửa chi_so_id/dòng hóa đơn để chứa số mới. Branch feature/S3-05/02-meter-reading-input-save; chưa commit/push/merge/switch/reset.

## 07/10/2026 — S3-05, lát 1: danh sách phòng cần ghi chỉ số (AC 1)

- Thêm GET `/ChiSoDienNuoc` cho QUAN_LY; menu Chỉ số điện nước dẫn tới màn thật, `/Modules/DIEN_NUOC` chuyển hướng riêng cho quản lý. Vai trò khác giữ trang module hiện có. Backend dùng cookie, ModuleAccess DIEN_NUOC, account đang hoạt động và `toa_nha.quan_ly_id`; chọn trực tiếp tòa ngoài phân công/ngừng hoạt động bị 403. Không được phân công có thông báo rỗng; tham số sai định dạng trả 400.
- Kỳ hiện tại là tháng dương lịch theo ngày Việt Nam qua ITimeProvider, không chuyển tháng theo ngày chốt. Chỉ phòng DANG_THUE có hợp đồng DANG_HIEU_LUC và kỳ thuê giao tháng (bao gồm hai đầu); ngày trả thực tế giới hạn khoảng thuê. Loại nháp/hủy/kết thúc/chờ hiệu lực, kỳ ngoài tháng và phòng trống. Nhiều kỳ không nhân đôi phòng; nếu nhiều hợp đồng cùng giao tháng, chọn hợp đồng có ngày bắt đầu kỳ giao tháng mới nhất, rồi ID giảm dần để ổn định; không gộp chỉ số giữa các hợp đồng.
- Chỉ số điện/nước tham chiếu: chi_tiet_hoa_don.chi_so_cuoi của đúng hợp đồng, mã dịch vụ DIEN/NUOC, dòng DICH_VU, snapshot THEO_CHI_SO, giá trị khác NULL, hóa đơn DA_PHAT_HANH kết thúc trước đầu kỳ. Lấy gần nhất theo den_ngay rồi ID hóa đơn/dòng. Fallback bảng hop_dong_chi_so_dau_ky của chính hợp đồng, ngày bàn giao không sau cuối kỳ; UI ghi rõ Chỉ số bàn giao. Thiếu nguồn giữ nullable và hiển thị Chưa có dữ liệu; 0 thật vẫn hiển thị 0. Module hóa đơn chưa cài vẫn đọc bàn giao, không tự cài schema.
- UI tiếng Việt dùng layout/theme hiện có, chọn tòa, kỳ, nhóm theo tầng, mã phòng, chỉ số và trạng thái; sắp tầng rồi mã phòng. Đã chốt chỉ khi có snapshot chỉ số thật cho cả DIEN và NUOC trên hóa đơn phát hành trong khoảng kỳ; thiếu một hoặc cả hai giữ Chưa chốt. Bàn giao hoặc hóa đơn khoán/không chỉ số không chứng minh đã chốt. Không thêm nơi lưu trạng thái, nhập/lưu chỉ số, tiến độ, cảnh báo >200% hay popup. Không đổi schema, chạy updater/seed hoặc sửa DB demo; không hard-code dữ liệu/DatabasePath.
- Kiểm chứng bản cuối: build C#/Razor thành công; 28/28 MeterReadingListTests PASS, gồm HTTP menu/route, Login/403, thu hồi/thiếu quyền, phân công, chỉ số, fallback/0/thiếu dữ liệu, kỳ giao/không giao, thứ tự và chống lặp, UTC+7/đổi năm, module hóa đơn tùy chọn, GET không đổi schema/dữ liệu và HTML không có nhập/lưu chỉ số mới. Test dùng SQLite tạm; GET được so sánh toàn bộ schema/dữ liệu sau bước login. Chưa nghiệm thu trực quan Chrome/Safari, 360px hoặc thiết bị thật. App repository đã dừng trước build và không tự khởi động lại.
- Lệnh bản cuối: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --output data/s305-final-tests --filter FullyQualifiedName~MeterReadingListTests --logger 'console;verbosity=minimal'`. Build project: `dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore`. Restore test đã thực hiện vì thiếu project.assets.json. Build thường ban đầu lỗi quyền ghi obj; chạy lại với quyền cần thiết đạt. Cảnh báo ImageSharp license/CS8601 có sẵn vẫn còn.
- Lượt mở rộng filter MeterReadingListTests + PermissionTests + ContractCreationTests + RoomServicesTests: 242/251 PASS, 9 FAIL. Một FAIL là fixture mới thiếu module yêu cầu thuê trong ca không có hóa đơn, đã sửa và kiểm chứng lại bằng bộ 28/28 phía trên. Tám FAIL còn lại thuộc test hồi quy ngoài phần thay đổi: MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio (badge); UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi (kỳ vọng 15, thực tế 16); MenuAndEveryModuleRouteMatchMatrixForAllRoles (menu); GuestPricingUsesRoomOverrideAndOmitsInvalidOrInactivePrices (nội dung phí); RegisterHasPasswordToggleAndConfirmationEmailIsEditable (class password-field); ProfileReadIsScopedToSelfOrSignedContractAndOwnerCannotEdit (fixture tạo lại hop_dong); AnonymousGuestViewsNhaTotBrandedHomePage (link tin trang chủ); DepartureHttpWarningSnapshotsAndFutureUnissuedBill (chữ Ghi nhận chuyển đi). Không sửa các phần này và không khẳng định toàn suite PASS; chưa chạy baseline độc lập để chứng minh tất cả lỗi có trước. Các lượt fixture mới ban đầu còn thất bại do thiếu người đứng tên/module hóa đơn, đã sửa fixture; không che giấu các lượt thất bại.
- Đang làm trên feature/S3-05/01-meter-reading-list; chưa commit/push/merge/rebase/reset/switch. Database demo hiện thiếu chỉ số cho hai hợp đồng legacy: UI phản ánh Chưa có dữ liệu, không seed để giả lập. Lát 2–5 chưa triển khai.

## 07/10/2026 — S3-02, lát 4: lịch sử người ở theo phòng/khoảng ngày (AC 5)

- Thêm `/HopDong/LichSuNguoiO?phongId=<id>&tuNgay=yyyy-MM-dd&denNgay=yyyy-MM-dd`, liên kết tại trang sửa phòng và chi tiết hợp đồng. Chủ nhà chọn từ ngày–đến ngày; mặc định đầu tháng hiện tại tới hôm nay Việt Nam qua ITimeProvider. Khoảng sai thứ tự hoặc ngày sai không truy vấn lịch sử và có lỗi; kết quả rỗng có thông báo.
- Backend kiểm quyền module HOP_DONG và chủ nhà đang hoạt động sở hữu tòa của phòng; chủ nhà khác/quản lý bị 403, chưa đăng nhập chuyển Login, phòng không tồn tại 404. Service kiểm lại sở hữu trước khi đọc; các truy vấn lịch sử dùng cùng transaction đọc SQLite deferred để nhất quán khi ngày chuyển đi/hợp đồng được cập nhật đồng thời. Chỉ trả họ tên, vai trò, mã hợp đồng và ngày ở; không trả SĐT/căn cước/ảnh giấy tờ.
- Lấy người đứng tên từ kỳ hợp đồng của phòng; lấy người ở ghép từ ngày vào/ngày ra và giới hạn trong khoảng hợp đồng. Gộp các kỳ gia hạn liền kề/chồng lấn của cùng hợp đồng, giữ khoảng gián đoạn thật và các lần ở riêng. Ngày trả phòng thực tế chặn khoảng ở của cả người đứng tên lẫn người ở ghép. Chỉ hợp đồng CHO_HIEU_LUC/DANG_HIEU_LUC/DA_KET_THUC; nháp và đã hủy không tính.
- Giao khoảng ngày **bao gồm hai đầu**, không cắt ngày hiển thị theo bộ lọc. Ngày chuyển đi là ngày còn ở. Đánh dấu Đang ở khi hợp đồng đang hiệu lực và hôm nay nằm trong khoảng; các khoảng tương lai/chờ hiệu lực không mang nhãn này. Nếu chưa ghi ngày chuyển đi/trả phòng, kết thúc lấy theo hạn hợp đồng và ghi rõ đây không phải xác nhận đã rời. Legacy thiếu người đứng tên/kỳ có cảnh báo, không tự suy đoán hoặc bổ sung dữ liệu.
- Không đổi schema, không updater, không ghi/xóa dữ liệu nguồn; không xuất lịch sử ra tệp. Phần chức năng AC 5 được triển khai; các phần trước AC 1–4 giữ nguyên. Chưa nghiệm thu trực quan Chrome/Safari 360px, review/hợp nhất nhánh hoặc toàn bộ test suite.
- Kiểm chứng: build C#/Razor đạt; **156/156 test mục tiêu PASS**, gồm HTTP bộ lọc/biên ngày, trạng thái rỗng, ngày sai, link trang phòng/hợp đồng, Login/403. Các ca: lọc trước/sau/giao một phần/bao trọn, trùng ngày vào/ngày ra, đang ở, nhiều hợp đồng kế tiếp, cùng người nhiều lần ở, gia hạn liên tiếp/khoảng gián đoạn, ngày trả thực tế, nháp/hủy, dữ liệu legacy thiếu, khoảng sai, chủ nhà khác/quản lý. Cảnh báo cũ NU1900, ImageSharp license và CS8601 vẫn còn.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RoomServicesTests|FullyQualifiedName~ContractCreationTests' --logger 'trx;LogFileName=s302-history-final.trx' --results-directory data/test-results`.
- Demo: đăng nhập chủ nhà, mở sửa phòng → Lịch sử người ở (hoặc nút trên chi tiết hợp đồng), chọn ngày từ hợp đồng cũ tới hiện tại. Xem từng vai trò/khoảng ở, đổi bộ lọc sang ngày đúng ngày chuyển đi, sang khoảng không có người và thử đến ngày trước từ ngày. Bộ demo Sprint 2 có hai hợp đồng legacy thiếu người đứng tên: cảnh báo phản ánh đúng nguồn; tạo hợp đồng hợp lệ/người ở ghép theo luồng S3-01/S3-02 để demo đầy đủ, không tự sửa hồ sơ legacy.

## 07/10/2026 — S3-02, lát 3: ghi nhận người ở ghép chuyển đi (AC 4)

- PO chốt trong phiên: **ngày chuyển đi vẫn là ngày còn ở**, cho nhập ngày tương lai; tháng chuyển đi vẫn tính đủ khoản khoán và giảm từ tháng kế tiếp. Nếu hóa đơn từ kỳ giảm trở đi đã lập (chưa hủy), giữ nguyên snapshot, cảnh báo các kỳ đã lập và áp dụng số người mới cho hóa đơn chưa lập. Quy tắc tháng chuyển đi này thay cho cách lọc theo ngày ra/ngày chốt của lát 2.
- Chủ nhà sở hữu hợp đồng dùng nút Ghi nhận chuyển đi tại từng người đang ở/sắp vào. Backend kiểm quyền HOP_DONG WRITE, vai trò chủ nhà, sở hữu, người ở ghép thuộc đúng hợp đồng và CSRF. Chặn ngày thiếu/sai, trước ngày vào, phiên bản phòng cũ, hợp đồng không còn đang/chờ hiệu lực và ghi ngày ra lần thứ hai. Không có thao tác sửa ngày đã ghi hay mở lại khoảng ở trong phạm vi này.
- Lưu `nguoi_o_ghep.ngay_ra`, tăng phiên bản phòng và ghi nhật ký cùng transaction `BEGIN IMMEDIATE`; lỗi bất kỳ rollback. Không xóa người/hồ sơ, không đổi ngày vào/căn cước/SĐT; nhật ký chỉ ghi ID/ngày. Hợp đồng vẫn ghi được ngày ra khi chưa cài module hóa đơn tùy chọn.
- Danh sách hiện tại giữ người tới hết ngày ra, người đã rời nằm ở danh sách riêng từ ngày kế tiếp; người sắp vào được hiển thị riêng. Ngày tương lai hiển thị như lịch chuyển đi, không loại người sớm. Sức chứa được giải phóng từ ngày sau ngày ra; thêm người mới vẫn kiểm tất cả mốc chồng lấn và phiên bản phòng.
- Khoán theo người: người mới có ngày vào trước ngày đầu kỳ mới bắt đầu tính; người có ngày ra trước ngày đầu tháng của kỳ được loại. Người rời bất kỳ ngày nào trong tháng vẫn tính đủ tháng đó, giảm tháng sau. Khoản cố định không đổi; số người, đơn giá/thành tiền của hóa đơn đã lập không bị sửa. Ngày chốt và cách chọn giá theo hợp đồng vẫn giữ nguyên.
- Schema hiện có **v16** đã có cột ngày ra và CHECK ngày ra >= ngày vào: không đổi schema hoặc chạy updater trên DB đang dùng. Không thêm UI tra cứu lịch sử theo khoảng thời gian tùy chọn.
- Kiểm thử mục tiêu: nhóm `RoomServicesTests` + `ContractCreationTests`, log `data/test-results/s302-departure-final.trx`. Có ca lưu hợp lệ/thiếu/sai ngày, cùng ngày vào, ngày hôm nay/tương lai, ngày ra trong tháng và ranh giới 30/09–01/10–31/10, kỳ sau giảm, cố định giữ nguyên, hóa đơn đã lập giữ snapshot/cảnh báo, danh sách hiện tại/đã rời, sức chứa ngày ra/ngày kế tiếp, đồng thời, stale version, ghi lại, 403, CSRF và rollback nhật ký. **139/139 test mục tiêu PASS**, build C#/Razor PASS. Cảnh báo cũ NU1900, ImageSharp license và CS8601 vẫn còn. Chưa nghiệm thu trực quan Chrome/Safari 360px hoặc review/hợp nhất nhánh.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RoomServicesTests|FullyQualifiedName~ContractCreationTests' --logger 'trx;LogFileName=s302-departure-final.trx' --results-directory data/test-results`.
- Demo: mở hợp đồng có người ở ghép, ghi ngày ra hôm qua để thấy người đó ở danh sách đã rời ngay; nếu chọn hôm nay thì người đó vẫn hiện tại đến hết ngày. So sánh hóa đơn tháng có ngày ra với tháng kế tiếp: khoản theo người giảm một đơn giá, khoản theo phòng không đổi. Nếu kỳ sau đã lập, lưu vẫn thành công và có cảnh báo; mở hóa đơn đã lập thấy snapshot giữ nguyên, chọn kỳ chưa lập tiếp theo để thấy số người giảm. Có thể thêm người mới từ ngày sau ngày ra trong giới hạn phòng.

## 07/10/2026 — S3-02, lát 2: khoán theo đầu người từ danh sách người ở

- PO chốt trong phiên: người ở ghép mới tính **từ tháng sau ngày vào, trọn tháng**, không chia ngày. Số người chốt theo `hop_dong.ngay_chot_hang_thang`; tháng thiếu ngày đó lấy ngày cuối tháng. Người đứng tên luôn tính 1 trong kỳ thuê trọn tháng; người ở ghép phải vào trước ngày đầu tháng của kỳ và còn ở tại ngày chốt (ngày ra bao gồm cả ngày cuối). Chưa có thao tác ghi chuyển đi, tra cứu lịch sử khoảng thời gian; **AC 4 của toàn story chưa hoàn tất**.
- Bỏ ô nhập số người thủ công: `LaySoNguoiAsync` dùng chung cho xem trước/phát hành, không lấy số người từ POST. Dòng `THEO_NGUOI` = đơn giá × số người, làm tròn AwayFromZero từng dòng; `CO_DINH` vẫn 1 phòng/tháng. Ngày trên form chọn kỳ tháng; ngày chốt hợp đồng quyết định số người và ngày chọn giá.
- Dịch vụ lấy theo danh sách `hop_dong_dich_vu` khi đã có; hợp đồng cũ chưa có snapshot tiếp tục dùng các dịch vụ gán cho phòng, không tự thêm dịch vụ mặc định tòa. Kiểm giá còn áp dụng của phòng và giá đã xem trước; kiểm phiên bản phòng nếu form gửi phiên bản. Không mở rộng luồng tạo/sửa danh sách dịch vụ hợp đồng.
- `BEGIN IMMEDIATE` giữ nhất quán khi đếm người/lấy giá/phát hành. Số người lưu ở `hoa_don.so_nguoi_tinh_phi`; ngày chốt, đơn giá, số lượng và thành tiền lưu vào snapshot sẵn có. Không đổi schema, không updater trên DB hiện tại. Hóa đơn cũ chỉ đọc từ snapshot, thêm người hoặc đổi giá không làm thay đổi nội dung cũ. Nhật ký và hóa đơn/dòng cùng transaction; dùng ITimeProvider cho thời điểm phát hành/hạn thanh toán.
- Xem trước có ngày chốt, số người, đơn giá, thành tiền từng khoản khoán/cố định; chi tiết hiển thị số người đã lưu và đầy đủ snapshot. Bảng responsive dùng vùng cuộn riêng; chưa nghiệm thu trực quan Chrome/Safari 360px.
- Kiểm chứng: build C#/Razor đạt; **119/119 test mục tiêu đạt** (RoomServicesTests + ContractCreationTests), gồm 10 ví dụ tính tay chuyển thành test, người đứng tên duy nhất, nhiều người, ngày vào trước/đúng/sau ngày chốt, giữa tháng, tháng kế tiếp, ngày cuối tháng/năm nhuận, khoảng ra bao gồm ngày chốt, cố định không tăng, POST giả số người bị bỏ qua, snapshot bất biến, stale version, concurrent issue, rollback nhật ký, HTTP xem trước/chi tiết/CSRF/403. Fixture hóa đơn cũ được cập nhật dùng schema hợp đồng phiên bản + module yêu cầu thuê; fixture nâng cấp giữ đúng dấu phiên bản khi giả lập v5/v7. Không sửa csproj test.
- Lệnh: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter 'FullyQualifiedName~RoomServicesTests|FullyQualifiedName~ContractCreationTests' --logger 'trx;LogFileName=s302-billing-final.trx' --results-directory data/test-results`. Cảnh báo cũ NU1900, ImageSharp license, CS8601 vẫn còn. Không khẳng định toàn bộ suite hoặc review/hợp nhất nhánh đã xong.
- Demo trên bộ Sprint 2: dùng chủ nhà A, duyệt yêu cầu 1 rồi lập hợp đồng A101 bắt đầu trong tháng 10/2026, ngày chốt 5. Thêm người ở ghép ngày trong tháng 10. Mở `/HoaDonDichVu?toaNhaId=1`, chọn hợp đồng mới, ngày 01/11/2026: ngày chốt hiển thị 05/11, số người 2, Nước 90.000 đ/người × 2 = 180.000 đ. Rác/Internet/Gửi xe vẫn tính cố định như trước. Nhập điện để phát hành hoặc bỏ chọn điện; sau phát hành thêm người nữa rồi mở chi tiết hóa đơn cũ: vẫn 2 người/180.000 đ. Hai hợp đồng legacy C001/C002 của bộ Sprint 2 chưa có người đứng tên: không dùng chúng để demo phần số người, không tự sửa dữ liệu legacy.

## 07/10/2026 — Tạo bộ demo theo huong-dan-test-sprint2

- Tạo bộ mới `data/sprint2-demo/20261007-064359-979c32`, build riêng; 7 tài khoản, 4 tòa, 45 phòng, 41 tin (35 công khai), 8 yêu cầu và 5 ảnh upload qua HTTP. DB cá nhân không bị ghi đè. Server demo chạy tại `http://localhost:5247`.
- Smoke test xác minh 7 đăng nhập, trang nghiệp vụ, quyền sở hữu, ảnh, integrity/FK PASS; tìm kiếm 5 lượt 16–30 ms. `verification.json` ghi kết quả và `latest.txt` đã chọn bản mới sau khi xác minh đạt.
- Sửa selector của `verification/verify_sprint2_demo.py` từ class card cũ sang class giao diện hiện tại, kỳ vọng 6 tin/trang theo `TimTinViewModel.KichThuocTrang`; ghi rõ lệch với hướng dẫn cũ 12 tin/trang. Không thay đổi chức năng phân trang.

## 07/10/2026 — S3-02, lát 1: người đứng tên và thêm người ở ghép (AC 1–3)

- Chủ nhà mở liên kết mã hợp đồng tại `/HopDong`, xem người đứng tên duy nhất, trách nhiệm thanh toán/nhận lại cọc, người ở ghép hiện tại và tổng số người theo ngày Việt Nam. Chỉ chủ nhà sở hữu tòa được xem màn này/thêm người; POST kiểm quyền module, quyền sở hữu và CSRF ở backend.
- PO đã chốt trong phiên: bắt buộc họ tên (2–100 ký tự), SĐT 10 chữ số bắt đầu 0, căn cước 9/12 chữ số và ngày vào trong kỳ hợp đồng. Căn cước trùng người đứng tên hoặc người ở ghép có khoảng ở chồng lấn trong cùng hợp đồng bị chặn. Ngày ra lưu NULL; không tự gắn/sửa hồ sơ thuộc tài khoản khác.
- `BEGIN IMMEDIATE` khóa ghi trước khi kiểm tra sức chứa; đếm người đứng tên + người ở ghép tại ngày vào và mọi mốc vào tương lai. Kiểm phiên bản phòng, tăng phiên bản sau khi thêm. Hồ sơ, người ở ghép và nhật ký cùng transaction; nhật ký chỉ ghi ID/ngày, không ghi SĐT/căn cước. Dữ liệu nhạy cảm hiển thị đủ chỉ khi hợp đồng đang hiệu lực và hôm nay nằm trong kỳ; trường hợp còn lại che chỉ giữ 4 số cuối.
- Schema **v16** thêm `nguoi_o_ghep`, FK RESTRICT, unique hợp đồng/khách/ngày vào, trigger yêu cầu người đứng tên khi chốt và khóa người đứng tên sau khi chốt; cấm người đứng tên xuất hiện ở bảng ở ghép. Nguồn thực tế v15 chưa có bảng này, khác bản đồ AGENTS.md. Giữ nguyên bước nâng cấp v1–v15; mở kiểm tra AccountReuseSchema tới v16. Không nâng cấp DB local đang dùng; đã chạy updater trên bản sao mới trong `data/s302-verification`, có backup tự động, đối chiếu tất cả dòng bảng cũ, integrity/FK và chạy lặp.
- Không triển khai tính khoản khoán theo số người, ghi nhận chuyển đi, tra cứu lịch sử khoảng thời gian hay thanh toán/hoàn cọc mới. Trách nhiệm tài chính tiếp tục gắn với `hop_dong.khach_dung_ten_id`, không có trường người đứng tên thứ hai hoặc lựa chọn chuyển trách nhiệm sang người ở ghép.
- Kiểm chứng: build C#/Razor đạt; 71/71 test hợp đồng/người ở ghép đạt, gồm dữ liệu sai/thiếu, giới hạn, mốc vào tương lai, trùng căn cước, đồng thời, quyền chủ nhà khác, rollback nhật ký và nâng cấp lặp. Test HTTP demo đạt: hiển thị 1/2 → 2/2 người, vượt giới hạn có thông báo cụ thể, thiếu CSRF trả 400 và chủ nhà khác nhận 403 cho cả GET/POST. Chưa kiểm trực quan Chrome/Safari 360px, thiết bị thật, review/hợp nhất nhánh. Cảnh báo cũ ImageSharp license, CS8601 và NU1900 còn tồn tại.
- Lệnh test: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~ContractCreationTests`.
- Chạy demo: dừng app; đặt đúng `DatabasePath`; chạy `dotnet run --project QL_PhongTro -- --update-database` (backup tự động), rồi `--check-database`, khởi động app. Mở `/HopDong`, nhấn mã hợp đồng của chủ nhà, thêm đủ thông tin trong kỳ; thử thêm đến giới hạn rồi vượt giới hạn. Ngày vào tương lai được lưu nhưng chỉ xuất hiện trong danh sách hiện tại từ ngày đó.

# Tiến độ và bàn giao dự án

- 07/10/2026: dựng lại giao diện Chủ nhà cho danh sách hợp đồng, lập hợp đồng, Tổng quan chi tiết và tab Người ở cùng theo bố cục mẫu graphite/champagne (`#30343B`, `#BFA77A`, `#F5F5F4`, `#FFFFFF`, `#E9EAEC`, `#25282D`). Danh sách có thống kê thật, lọc tìm kiếm/tòa/trạng thái, thời hạn và cảnh báo trong 30 ngày; tiền cọc ghi rõ là mức thỏa thuận. Form có bước hướng dẫn, hai cột thông tin, tóm tắt và cọc cập nhật trực tiếp; giữ mọi ràng buộc server. Chi tiết lấy kỳ thuê/chỉ số bàn giao thật; tab người ở giữ form thêm/chuyển đi, sức chứa và lịch sử, POST thành công quay lại tab people. Không thêm chức năng giả cho PDF/gia hạn/thanh toán/điều khoản chưa lưu. Build Razor/C# cách ly PASS, 100 test hợp đồng/người ở/lịch sử PASS (HTTP kiểm tra tổng quan và tab people, CSRF/sở hữu). Không đổi schema/database; chưa kiểm tra trực quan trình duyệt và mobile 360px.

- 07/10/2026: bổ sung ngẫu nhiên một ảnh mẫu kèm thumbnail vào 33 phòng còn chưa có ảnh (id 13–45), sau khi ứng dụng đã dừng. Tất cả 45/45 phòng hiện có ảnh; giữ nguyên ảnh cũ và toàn bộ bảng khác, gồm hồ sơ CCCD. Có backup trước cập nhật, kiểm tra đối chiếu dữ liệu/tệp/giới hạn 8 ảnh/integrity/FK PASS. Script hỗ trợ `--all-empty-rooms`, chạy lại không thêm ảnh vào phòng đã có; chưa kiểm chứng hiển thị trình duyệt.

- 07/10/2026: theo xác nhận người dùng, bổ sung 10 ảnh phòng mẫu vào 10 phòng đầu tiên chưa có ảnh (id 3–12), mỗi phòng một ảnh và thumbnail; thêm hai mặt CCCD mẫu cho Khách thuê demo 2, 3 trong kho private. Sao lưu SQLite bằng backup API trước khi ghi; không đổi schema, ảnh/hồ sơ hiện có hoặc dữ liệu nghiệp vụ khác. Đối chiếu tất cả bảng với backup PASS, 24 tệp mới tồn tại, integrity/FK PASS. Không đưa ảnh/database/backup vào Git; chưa kiểm tra hiển thị qua trình duyệt. Script import có kiểm tra hồ sơ demo và từ chối nếu mục tiêu không còn trống, không chạy lại để nhân đôi.

- 07/10/2026: thêm ô Số điện thoại khách thuê ngay dưới tên khách trong thẻ Thông tin hợp đồng của màn lập hợp đồng. Lấy từ hồ sơ khách thuộc yêu cầu đã chọn, readonly, không gửi lên để sửa hồ sơ; thiếu số hiển thị Chưa cập nhật. Không đổi schema/database. Build Razor/C# và test HTTP render/lưu hợp đồng PASS (xác nhận số điện thoại đúng, readonly và không có name). Chưa kiểm tra trực quan trên trình duyệt.

## S3-01 AC3–AC5 — hoàn tất luồng lập hợp đồng (07/10/2026)

- PO đã chốt: nhập trực tiếp **cả hai** chỉ số điện/nước bàn giao tại ngày bắt đầu thuê, bắt buộc không âm. Lưu tối đa 3 chữ số thập phân, không tự suy đoán/điền 0. Áp dụng cả khi nước tính theo đầu người; đây là snapshot bàn giao, không phải bản tiêu thụ để tính tiền. Database thực tế v14 chưa có `chi_so_dien_nuoc`; v15 bổ sung `hop_dong_chi_so_dau_ky` có FK/unique hợp đồng, không tự dựng module chốt chỉ số hàng tháng S3-05.
- Cọc mặc định bằng 1 tháng giá thuê chốt; JS cập nhật mặc định khi đổi giá nếu chưa sửa cọc. Cọc được phép 0..3 tháng, kiểm tra lại bằng decimal ở server; cọc thiếu/âm/vượt giới hạn bị chặn. Các trường chỉ số dùng binder số thập phân bất biến để không phụ thuộc vùng Windows.
- Chặn chồng lấn với mọi kỳ của hợp đồng `DANG_HIEU_LUC`/`CHO_HIEU_LUC` trong cùng phòng, bao gồm ngày chung hai đầu. Hiển thị mã và khoảng thời gian các hợp đồng đang vướng; kiểm tra AJAX khi đổi ngày/kỳ hạn và kiểm tra lại lúc lưu. Nháp/hủy/kết thúc không chặn hợp đồng mới.
- Sinh `HD-yyyy-xxxx` theo năm thời điểm lưu tại Việt Nam, dải 0001..9999/năm; bảng `hop_dong_so_ma` cùng transaction và unique mã hiện có bảo đảm không trùng. Lấy tiếp số lớn nhất đã có, không dùng mã dự đoán trên UI. Khi hết dải báo lỗi, không tạo hợp đồng. Khóa ghi `BEGIN IMMEDIATE` trước đọc/kiểm tra và kiểm tra phiên bản phòng chống hai lần lưu cạnh tranh.
- Lưu thành công: hợp đồng `DANG_HIEU_LUC`, kỳ thuê, chỉ số bàn giao và phòng `DANG_THUE` cùng transaction với nhật ký; tăng phiên bản phòng. Theo yêu cầu trực tiếp, phòng đổi Đang thuê ngay khi lưu kể cả ngày bắt đầu ở tương lai; chưa thêm lịch kích hoạt hay tự đổi tin đăng (luồng tự động tin thuộc S3-04). Hiển thị mã và trạng thái phòng bằng thông báo thành công, danh sách hiển thị mã thật. Các bản NHAP từ AC1–AC2 mở lại qua chọn yêu cầu để hoàn tất trên chính bản cũ, không tạo bản trùng.
- Giao diện form Latte/Warm Brown có thẻ chỉ số đầu kỳ, kiểm tra hiệu lực và thông tin tự sinh như ảnh mẫu; không còn các placeholder ngoài phạm vi AC1–AC2. Không thay đổi dữ liệu/schema database đang dùng của người dùng; chỉ đọc schema nguồn.
- Kiểm chứng: build C#/Razor PASS; **60/60 test mục tiêu PASS** (`ContractCreationTests`, `LichHenLapHopDongTests`, nâng cấp quyền v13). Có ca HTTP/CSRF, POST giả ngày kết thúc, số thập phân, cọc biên, chồng lấn/ngày liền kề, trạng thái hợp đồng, chỉ số, mã/năm VN, hoàn tất nháp, sở hữu chéo 403, phiên bản phòng, lưu đồng thời cùng/khác phòng và trigger lỗi ghi chỉ số rollback cả mã/phòng/nhật ký. Test nâng cấp legacy/cập nhật lặp/integrity/FK PASS. `verification/s301_copy_upgrade.py` nâng cấp **bản sao** database demo v14 → v15, xác nhận mọi dòng ở mọi bảng cũ giữ nguyên và updater chạy lại không đổi dữ liệu; nguồn giữ nguyên. Chưa nghiệm thu trực quan từng pixel, Chrome/Safari 360px, tải 30 người hay review/hợp nhất nhánh; không khẳng định toàn bộ test suite PASS.
- Chạy demo: dừng app, đặt đúng `DatabasePath` đang dùng, chạy `dotnet run --project QL_PhongTro -- --update-database` (tạo backup), rồi `--check-database` và `.\run.bat`. Màn `/HopDong/Create`: chọn yêu cầu đã duyệt/nháp, nhập hai chỉ số, nhập thông tin thuê, thử cọc vượt giới hạn/chồng lấn rồi lưu hợp lệ. Phiên bản yêu cầu hiện tại **v15**.

## Đồng bộ màu Tin đăng cho thuê (06/10/2026)

- Trang TinDang dùng listing-theme.css theo bảng màu trang chủ: xanh #008060, hover #006e52, nền trắng/xám nhạt, viền xám; cập nhật nút tìm/lọc, biểu mẫu, liên kết, phân trang, giá thuê và chi phí tháng đầu ở chi tiết công khai. Chỉ nạp CSS cho controller TinDang trong public layout.
- Build thành công; HTTP danh sách/chi tiết/CSS và phạm vi trang chủ đạt. Quản lý tin của chủ nhà tiếp tục dùng theme dashboard chung. Đã chạy lại localhost:5247 và kiểm tra đăng nhập 7 tài khoản demo đạt; không chụp ảnh.

## Đồng bộ màu giao diện Admin (06/10/2026)

- ADMIN dùng homepage-dashboard-theme.css cùng chủ nhà, quản lý và khách thuê: xanh #008060, nền trắng/xám nhạt, đồng bộ menu, nút, biểu mẫu với trang chủ công khai.
- Build thành công; đăng nhập AJAX và phiên Admin đạt; trang phân quyền, phòng trọ, tòa nhà, yêu cầu thuê HTTP 200 và nạp đúng theme. Trang chủ công khai kiểm tra đạt. Đã chạy lại localhost:5247, 7 tài khoản demo đăng nhập đạt; không chụp ảnh.

## Đồng bộ màu giao diện quản lý (06/10/2026)

- Vai trò QUAN_LY dùng chung homepage-dashboard-theme.css với chủ nhà và khách thuê: xanh #008060, nền trắng/xám nhạt, menu/nút/biểu mẫu theo trang chủ công khai.
- Build thành công; đăng nhập AJAX và phiên quản lý đạt; các trang phòng trọ, tòa nhà, quản lý tin, yêu cầu thuê HTTP 200 và nạp đúng theme. Kiểm tra phạm vi theme chủ nhà/khách thuê/admin đạt. Đã chạy lại localhost:5247, 7 tài khoản demo đăng nhập đạt; không chụp ảnh.

## Đồng bộ màu giao diện chủ nhà (06/10/2026)

- Chủ nhà và khách thuê dùng chung homepage-dashboard-theme.css: xanh #008060, nền trắng/xám nhạt, viền xám; đồng bộ menu, nút, biểu mẫu và nhãn ảnh chính. Trạng thái giữ chỗ dùng vàng nhạt, lỗi dùng đỏ để phân biệt rõ. Các vai trò khác giữ theme hiện có.
- Build thành công; kiểm tra HTTP 200 và theme trên các trang phòng trọ, tòa nhà, dịch vụ, yêu cầu thuê, quản lý tin của chủ nhà và hồ sơ/yêu cầu của khách thuê. Trang công khai/admin không nạp theme mới. Server localhost:5247 đã chạy lại và 7 tài khoản demo đăng nhập đạt; không chụp ảnh.

## Đồng bộ màu giao diện khách thuê (06/10/2026)

- Dashboard KHACH_THUE dùng xanh #008060, hover #006e52, nền trắng/xám nhạt và viền xám theo trang chủ công khai. Đồng bộ menu, nút, biểu mẫu, trạng thái thành công và phân trang qua tenant-theme.css, chỉ nạp cho vai trò khách thuê.
- Build thành công; HTTP trang Yêu cầu thuê và Hồ sơ cá nhân 200, có theme khách thuê; trang chủ công khai và giao diện chủ nhà không nạp theme này. Đã chạy lại tại localhost:5247, kiểm tra đăng nhập 7 tài khoản demo đạt. Không chụp ảnh.

## Đồng bộ giao diện xác nhận email (06/10/2026)

- ConfirmEmail dùng layout/CSS đăng nhập, hai cột ảnh nội thất và form nền kem, logo NhàTốt, nút xác nhận xanh và gửi lại mã dạng viền. Giữ Email/Code, CSRF, validation, thông báo lỗi/trạng thái, đường dẫn email local và formnovalidate của Gửi lại mã; mã có inputmode numeric/one-time-code và giới hạn 6 số. Có liên kết về đăng nhập.
- Build và HTTP GET PASS, đủ trường/CSRF/route gửi lại; server 5247 đã nạp bản mới, 7 tài khoản demo đăng nhập đạt. Không chụp ảnh, không gửi email hoặc xác nhận mã thử trong lượt chỉnh giao diện.

## Đồng bộ giao diện quên mật khẩu (06/10/2026)

- ForgotPassword dùng layout/CSS đăng nhập, ảnh nội thất bên trái, logo NhàTốt và form email nền kem/nút xanh bên phải; dùng cùng responsive mobile. Giữ thông báo ResetRequestMessage, validation, CSRF và liên kết về đăng nhập; không đổi nghiệp vụ gửi email.
- Build PASS; HTTP 200 và kiểm tra trường email/CSRF/branding đạt. Server 5247 đã nạp bản mới; 7 tài khoản demo đăng nhập đạt. Không chụp ảnh, không gửi email thử trong lượt chỉnh giao diện.

## Đồng bộ trang đăng ký với đăng nhập (06/10/2026)

- Trang Register dùng layout/CSS đăng nhập, hai cột ảnh nội thất và form nền kem, logo NhàTốt trên ảnh dùng chữ sáng, nút xanh; mobile dùng một cột. Giữ họ tên/email/điện thoại/mật khẩu, validation, CSRF và hiện/ẩn mật khẩu. Điều chỉnh khoảng cách cho form dài.
- Build PASS; HTTP Register 200, đủ trường/CSRF/logo; server 5247 đã nạp bản mới, đăng nhập 7 tài khoản demo đạt. Không chụp ảnh, không gửi đăng ký mới/email, không đổi mã nghiệp vụ đăng ký.

## Đồng nhất thương hiệu trang đăng nhập (06/10/2026)

- Hai vị trí logo trên trang đăng nhập dùng cùng `images/nhatot-logo.svg` với trang chủ: NhàTốt - SỐNG ĐÚNG NƠI. Tiêu đề tab và nhãn trợ năng đổi từ Nhà Mộc sang NhàTốt; logo trên ảnh có nền kem để dễ đọc.
- Build và xác minh HTML qua HTTP PASS; server 5247 đã nạp bản mới, đăng nhập 7 tài khoản demo đạt. Không chụp ảnh, không đổi dữ liệu/mật khẩu.

## Trang đăng nhập theo ảnh Nhà Mộc (06/10/2026)

- Layout đăng nhập riêng hai cột: ảnh nội thất hiện có bên trái, logo/nội dung theo ảnh tham chiếu, form nền kem và nút xanh bên phải. Mobile hiển thị form một cột. Không chụp ảnh.
- Giữ validation, CSRF, AJAX nhận token, ReturnUrl, khóa tài khoản/countdown, quên mật khẩu, đăng ký và nút hiện/ẩn mật khẩu. Thêm RememberMe gửi qua form/fetch và điều khiển cookie persistent trong Login.
- Build PASS; HTTP kiểm tra trang/assets, đăng nhập 7 tài khoản demo và cookie phiên/persistent PASS. Server 5247 đã nạp runtime mới; mật khẩu demo vẫn Demo@2026. Chưa xác minh bố cục trực quan trong browser; ảnh nền dùng asset có sẵn nên không giống hoàn toàn ảnh tham chiếu. Chưa commit/push.

## Bộ dữ liệu sạch nghiệm thu S2-01–S2-10 trong bản - test (05/10/2026)

- Đọc hướng dẫn, yêu cầu, mô hình SQLite/DBML và tiến độ; đối chiếu bộ demo cũ ở dự án gốc. Bộ cũ có 8 tài khoản và yêu cầu 8 đã bị đổi trạng thái, nên tạo bộ mới riêng bằng Sprint2DemoSeeder, không ghi hoặc xóa database cũ.
- Bộ mới trong `data/sprint2-demo/20261005-172234-428e1c`, được chọn qua `latest.txt`; server riêng `http://localhost:5271`. Có 7 tài khoản đủ 4 vai trò, 4 tòa (A sở hữu A1/A2, B sở hữu B1/B2), 45 phòng, 35 tin công khai và 6 tin bị loại. Chọn 35 thay vì 36 để trang cuối lẻ: 12/12/11.
- A1 có 5 dịch vụ mặc định có giá, gửi xe chỉ ở các phòng mẫu tầng trệt, có giá riêng và 2 hóa đơn tháng trước. A2 chưa cấu hình. Điện A1 theo chỉ số, nước theo người; cấu hình nước chờ kỳ 11/2026.
- 8 yêu cầu đủ 5 trạng thái ở 2 tòa; có yêu cầu quá 24 giờ và vừa tạo, 2 lịch cùng phòng cách 15 phút, Thuê ngay chờ duyệt; khách 1 chưa gửi trên A108, khách 2 có yêu cầu mở trên tin đó.
- Nạp 5 tệp ảnh nội thất JPG/PNG qua HTTP từ các asset hero hiện có (3 asset, có ảnh lặp). Kiểm tra thumbnail rộng 400px, không thiếu tệp/mồ côi. Thử tệp quá 5MB, JPG giả và ảnh thứ 9 đều bị từ chối; phục hồi phòng placeholder về 0 ảnh.
- Xác minh HTTP đăng nhập 7 tài khoản, các trang dịch vụ/tin/yêu cầu/hóa đơn, Chủ nhà B nhận đúng 403, phân trang 12/12/11 và áp dụng gợi ý giá tìm được tin. SQLite integrity/FK đạt. Tài khoản, mật khẩu chung và bản đồ ID ghi trong `access.json`/`report.md`, kết quả trong `verification.json`; tất cả thuộc `data/` đã ignore.
- Không đổi schema/mã nghiệp vụ, không thay `.env.local` đang chọn database cũ và không dừng server 5247. Chạy bộ mới bằng `verification/Start-Sprint2Demo.ps1` (đọc latest mới). S2-08 chỉ chuẩn bị lập hợp đồng; S3-01 chưa triển khai.

## Giao diện trang chủ NhàTốt khi chưa đăng nhập và đồng bộ điều hướng (05/10/2026)

- Thiết kế trang chủ theo nhận diện NhàTốt khi chưa đăng nhập: Logo NhàTốt - SỐNG ĐÚNG NƠI, thanh điều hướng (Trang Chủ, Tìm Phòng, Tin Đăng Cho Thuê, Giới Thiệu, Đăng nhập, Đăng ký), hero banner tràn viền với ảnh phòng khách hiện đại và slogan "Tìm chốn ở phù hợp, bắt đầu cuộc sống mới."
- Trang chủ hiện chỉ hiển thị hero banner; form tìm nhanh tại trang chủ chưa có trong phiên bản hiện tại.
- Controller chuẩn bị danh sách tin mới nhất, nhưng view trang chủ hiện chưa hiển thị danh sách này.
- Thêm trang Giới thiệu tại `/Home/GioiThieu` với 3 cam kết cốt lõi (Giá thật minh bạch, Ảnh thật xác thực, Liên hệ trực tiếp) và quy trình 4 bước thuê phòng.
- Khi đã đăng nhập, người dùng tiếp tục sử dụng không gian quản lý bình thường; layout công khai `_PublicLayout.cshtml` được đồng bộ cho cả `/`, `/TimTin`, `/TinDang` và `/Home/GioiThieu`.
- Kiểm thử ngày 05/10/2026 trước khi hợp nhất bản sửa email: Build PASS; 262/263 ca đạt, 1 ca trang chủ thất bại vì thiếu form tìm nhanh. Test còn kỳ vọng tin nổi bật và cam kết trên trang chủ. Không đổi schema CSDL.

## Đồng bộ nhánh dev mới nhất (05/10/2026)

- Trước khi pull đã dừng ứng dụng, xác nhận working tree sạch và sao lưu nguyên fixture local ra `D:\DevSprint2\database-backups\sprint2-demo-before-pull-20261005-ca363ac2`; SHA-256 database nguồn/bản sao khớp nhau.
- `git pull --ff-only origin dev` thành công, fast-forward từ `66ec95e` lên `66376d2`, không xung đột. `.env.local`, database và kho ảnh local vẫn tồn tại, vẫn được Git ignore; hash database không đổi sau pull.
- Xác minh sau pull: `run.bat --check-only` PASS, dùng đúng fixture `data/sprint2-demo/20261004-185115-e98174/sprint2.sqlite`; schema sẵn sàng. Còn cảnh báo NU1900/ImageSharp/nullable có sẵn, không có lỗi build hoặc schema. Không chạy updater và không ghi đè database.

## Thu gọn từ chối và chuẩn hóa giờ hẹn (05/10/2026)

- Đặt nút Từ chối ngay cạnh ô lý do; ghi chú Lý do khác nằm ở hàng dưới và ẩn cả cột khi không cần, bỏ khoảng trống đẩy nút ra xa.
- Ô xác nhận/đổi lịch dùng định dạng 24 giờ HH:mm, ví dụ 19:30, tối đa 5 ký tự và bàn phím cho phép nhập dấu hai chấm. Chuẩn hóa 1930 thành 19:30 và 9:30 thành 09:30; giờ ngoài 00:00–23:59 báo lỗi. Razor định dạng giờ với dấu hai chấm cố định và invariant culture. Giữ giá trị ngày giờ ISO gửi backend và cảnh báo trùng lịch.
- Không chụp ảnh. Kiểm tra cú pháp JavaScript PASS; build Razor để xác minh. Cần khởi động lại app tại terminal giữ cấu hình SMTP để nạp giao diện mới. Chưa push.

## Sắp xếp giá và phân trang danh sách tin (05/10/2026)

- `/TinDang` thêm Mới đăng nhất/Giá tăng dần/Giá giảm dần, phân trang ở database 12 tin/trang, hiển thị giá thuê và tổng tin/trang. Link Trang trước/Trang sau giữ cách sắp xếp; áp dụng sắp xếp về trang đầu. Form lọc sang `/TimTin` truyền cách sắp xếp đang chọn; `/TimTin` đã có lọc và phân trang 12 từ trước.
- Chỉ lấy tin công khai hợp lệ, giá trùng dùng ID giảm dần; chuẩn hóa cách sắp xếp lạ và giới hạn trang vào phạm vi hợp lệ. Không sửa schema/dữ liệu thật, không chụp ảnh.
- Build PASS; HTTP integration test PASS trên SQLite tạm với 25 tin hợp lệ và 1 nháp: thứ tự tăng/giảm đúng, trang 12/12/1, trang quá lớn về trang cuối, link giữ cách sắp xếp. Cần khởi động lại app trong terminal đã cấu hình SMTP để nạp thay đổi. Chưa push.

## Hiển thị trạng thái trong màn sửa tin (05/10/2026)

- Thêm trường chỉ đọc “Trạng thái tin đăng” tại phần Nội dung tin của màn chỉnh sửa; DANG_HIEN_THI hiển thị “Đã hiển thị” theo yêu cầu. Các trạng thái còn lại hiển thị Nháp, Tạm ẩn, Đã cho thuê. Trạng thái lấy từ tin đã lưu, giữ luồng đăng/lưu/gỡ hiện có. Không chụp ảnh; cần khởi động lại ứng dụng để nạp Razor mới.

## Bỏ nút mũi tên sắp xếp ảnh (05/10/2026)

- Theo yêu cầu người dùng, bỏ hai nút lên/xuống khỏi thẻ ảnh có sẵn và thẻ ảnh tạo sau upload; bỏ handler JavaScript tương ứng. Giữ kéo thả và nút Xóa. Kiểm tra cú pháp JavaScript và diff đạt; không chụp ảnh. Razor cần build/khởi động lại ứng dụng để nạp thay đổi.

## Sửa kéo thả thứ tự ảnh phòng (05/10/2026)

- `room-images.js` luôn chấp nhận drop trong grid khi đang kéo, gồm trên chính thẻ vừa di chuyển và khoảng trống; trước đây các vị trí này không preventDefault nên drop bị hủy và dragend phục hồi thứ tự cũ. Tắt native drag của img để trình duyệt kéo thẻ, không kéo riêng tệp ảnh; không khóa chuột dựa vào pointer coarse; nút thao tác không bắt đầu drag. Vị trí trước/sau dựa vào nửa trái/phải của thẻ trong grid.
- Kiểm tra cú pháp JS PASS. Kiểm tra trực tiếp browser localhost:5247: kéo ảnh ID 3 lên đầu nhận “Đã lưu thứ tự ảnh”, reload giữ thứ tự 3/1/2/4/5; dùng mũi tên đưa về 1/2/3/4/5. Ảnh xác minh tại data/room-drag-fixed.png đã ignore. Chỉ sửa static JS, không cần restart web; người dùng reload trang để nạp bản mới. Chưa push trong lượt này.

## Giới hạn quyền hồ sơ khách thuê (05/10/2026)

- Khách thuê chỉ xem/sửa hồ sơ gắn với tài khoản đang đăng nhập; endpoint xem chi tiết chặn hồ sơ khác ở backend và không còn form nhập mã hồ sơ bất kỳ. Ảnh giấy tờ áp dụng cùng phạm vi quyền.
- Chủ nhà có menu Hồ sơ khách thuê và danh sách `/HoSo/DanhSach`, chỉ gồm người đứng tên/người ở ghép của hợp đồng tại tòa thuộc mình. Chủ nhà xem chi tiết/ảnh nhưng GET/POST sửa và xóa đều bị chặn. Danh sách không trùng khi khách có nhiều hợp đồng.
- Diễn giải yêu cầu “đã có hợp đồng”: hợp đồng CHO_HIEU_LUC, DANG_HIEU_LUC hoặc DA_KET_THUC cấp quyền; NHAP/DA_HUY và yêu cầu thuê không cấp quyền. Giữ quyền ADMIN hiện có. Schema hợp đồng chưa cài/không tương thích không cấp quyền; bảng người ở ghép là tùy chọn.
- Build riêng tại data/profile-check PASS; 4/4 kiểm thử hồ sơ PASS, gồm truy cập hồ sơ/ảnh người khác, hợp đồng nháp/kết thúc, đổi chủ tòa, danh sách chỉ đúng khách, chủ nhà không sửa được và hồi quy tạo/sửa/xóa hồ sơ cá nhân. Không đổi schema/database thật.
- Phiên web của người dùng vẫn chạy mã cũ để giữ cấu hình SMTP trong terminal. Cần Ctrl+C rồi chạy lại run.bat tại chính terminal đó để nạp bản sửa. Chưa nghiệm thu UI trực quan.

## Thống nhất cổng localhost (05/10/2026)

- Theo yêu cầu người dùng, chuyển bộ demo `20261005-012411-0d3b1e` sang http://localhost:5247; cập nhật access.json, report.md và hướng dẫn test local. Giữ database, kho ảnh và mật khẩu demo hiện tại.
- `.env.local` đã ignore trỏ run.bat tới đúng database/kho ảnh/khóa/email pickup của bộ demo, cùng PublicBaseUrl http://localhost:5247. run.bat đọc thêm PublicBaseUrl và truyền URL 5247 tường minh; New-Sprint2Demo mặc định dùng 5247. Hướng dẫn Sprint 2 dùng cổng 5247.
- Dừng đúng hai tiến trình của dự án từng chạy ở 5248 và 5270 để tập trung sử dụng cổng 5247. Những lần sau chạy run.bat ở gốc repository; server cần đang chạy để truy cập website.

## Tạo bộ nghiệm thu Sprint 2 mới (05/10/2026)

- Đọc hướng dẫn, yêu cầu Sprint 2, mô hình DBML và tiến độ; dùng seeder hiện có tạo fixture mới `data/sprint2-demo/20261005-012411-0d3b1e`, runtime riêng, chạy tại http://localhost:5270. Không ghi database cá nhân. Credential chung của 7 tài khoản nằm trong access.json và report.md đã ignore.
- 35 tin công khai ở 4 quận/huyện, 6 tin bị loại; chọn 35 để trang cuối lẻ (12/12/11). Khách 1 chưa có yêu cầu trên tin A108 nhưng có yêu cầu khác phục vụ tự hủy. Hóa đơn cũ kỳ 09/2026, cấu hình hiện tại 10/2026 và nước chờ 11/2026.
- Build PASS; upload 5 ảnh PNG qua HTTP PASS; integrity/FK, đăng nhập 7 tài khoản và smoke test PASS. Xác minh riêng 3 trang HTTP 12/12/11, thumbnail rộng 400px, Chủ nhà B mở dịch vụ phòng A1 trả 403. Hướng dẫn từng AC theo cổng mới nằm ở huong-dan-test.md cạnh report.md.
- Python mặc định trỏ Microsoft Store; hoàn tất upload/xác minh bằng Python runtime có sẵn của Codex. latest.txt chỉ được cập nhật sau kiểm tra thành công. Hai thư mục chuẩn bị chưa hoàn tất trước đó được giữ, không chọn làm mẫu bàn giao.
- Chưa chạy full xUnit, nghiệm thu UI/responsive/3G, SMTP thật hoặc hiệu năng 500 tin trong phiên này. Email demo dùng pickup; các tài khoản đã xác nhận email. Server đang chạy để người dùng tự nghiệm thu các thao tác làm thay đổi dữ liệu.

## Dùng fixture Sprint 2 làm database local (04/10/2026)

- Không chép đè `QL_PhongTro/Data/local-dev.sqlite` vì file này đã tồn tại. Tạo `.env.local` đã Git ignore để `run.bat` trỏ `DatabasePath`, `RoomImagesPath`, Data Protection keys và thư mục email pickup tới fixture `data/sprint2-demo/20261004-185115-e98174` mà người dùng vừa kiểm thử.
- `run.bat` nay đọc bốn cấu hình local trên (và cấu hình Event Log cần cho môi trường hạn chế); biến môi trường đặt sẵn trong terminal vẫn được ưu tiên. README ghi cách dùng cho máy mới và máy có DB cần giữ.
- Xác minh thực tế: `run.bat --check-only` PASS và báo đúng đường dẫn fixture; `run.bat` mở web tại `http://localhost:5247`; `/TimTin` trả HTTP 200 và một thumbnail từ kho ảnh cấu hình trả `200 image/png`. Đã dừng server sau kiểm tra. Dữ liệu đã thay đổi do lượt test thủ công trước đó được giữ nguyên, không reset fixture.

## Dữ liệu mẫu nghiệm thu S2-01 đến S2-10 (04/10/2026)

- Hoàn thiện `Sprint2DemoSeeder` và bộ script `New/Start-Sprint2Demo`: mỗi lần tạo database/schema v13 và runtime riêng dưới `data/` đã ignore, không đọc/ghi `local-dev.sqlite`, không ghi đè bản cũ. Mật khẩu chung được sinh ngẫu nhiên; email/mật khẩu/role của 7 tài khoản chỉ nằm trong `access.json` và `report.md` local.
- Fixture mới có: ADMIN; CHU_NHA A sở hữu A1/A2; CHU_NHA B và tòa riêng để thử 403; QUAN_LY được gán A1; 3 KHACH_THUE. A1 có 5 dịch vụ/giá, khác biệt gửi xe theo tầng, giá riêng, hóa đơn cũ và cấu hình nước chờ kỳ sau; A2 chưa cấu hình. Có 35 tin công khai ở 4 quận (12/12/11), 6 tin bị loại, đủ trạng thái phòng/tin/yêu cầu, hai lịch cùng phòng cách 15 phút và ca gửi trùng.
- Thêm `upload_sprint2_demo_images.py`: server tạm upload 5 PNG vào A101 qua endpoint thật, sinh thumbnail 400px và xác minh đường dẫn DB/tệp; A104 giữ 0 ảnh. Thư mục mẫu có PNG/JPG hợp lệ, file >5 MiB, JPG nội dung giả và ảnh thứ 9.
- Cách dùng: `powershell -ExecutionPolicy Bypass -File .\verification\New-Sprint2Demo.ps1`, sau đó `.\verification\Start-Sprint2Demo.ps1`. Đọc credential/bản đồ ID trong đường dẫn `data/sprint2-demo/latest.txt` → `access.json`/`report.md`; hướng dẫn từng AC ở `docs/huong-dan-test-sprint2.md`.
- Xác minh thực tế trên bản `data/sprint2-demo/20261004-185115-e98174`: tạo mới + upload HTTP + smoke test PASS; 7/7 đăng nhập, integrity/FK PASS, 35 tin public/4 quận, đủ 8 yêu cầu/5 trạng thái, quyền CHU_NHA B bị chặn, tìm kiếm 5 lần 7,279–29,965 ms. `--check-database` PASS; kho ảnh expected 10, missing 0, orphan 0; chạy lại `Start-Sprint2Demo.ps1` và GET `/TimTin` trả HTTP 200. Full xUnit **259/259 PASS**; đã cập nhật fixture migration test từ v12 lên schema hiện hành v13. Còn cảnh báo NU1900 do nguồn NuGet và cảnh báo license ImageSharp; chưa thao tác thủ công toàn bộ UI từng AC.
- File chính: `Data/Sprint2DemoSeeder.cs`, `Data/RequestDemoSeeder.cs`, `Program.cs`, hai PowerShell demo, hai Python verifier/uploader, test migration, README và hướng dẫn Sprint 2. Không đổi schema/quy trình DB sản xuất, không commit/push.

## Chỉnh form tìm kiếm tin công khai (04/10/2026)

- Đổi form tại `/TinDang` sang card tìm kiếm dùng CSS riêng thay cho các lớp Bootstrap không được nạp trong public layout: ô nhập đồng nhất, bố cục 4/2/1 cột theo desktop/tablet/mobile, nhãn có đơn vị, focus rõ và nút tìm kiếm dễ nhận biết. Giữ nguyên action `/TimTin`, method GET, tên và ràng buộc của đủ tám tham số lọc.
- File thay đổi: `Views/TinDang/Index.cshtml`, `wwwroot/css/public-listing.css`, kiểm thử HTML trong `Sprint2UsabilityTests.cs`. Build và test `PublicListingIndexOffersKeywordAndRequestedFilters` PASS qua output riêng; còn cảnh báo ImageSharp/nullable có sẵn. Chưa kiểm tra trực quan trên trình duyệt vì tiến trình web hiện tại đang chạy bản cũ và khóa output; cần khởi động lại app rồi tải lại `/TinDang`. Không thay đổi database.

## Chỉ Chủ nhà/Quản lý được đăng tin (04/10/2026)

- `TinDangController` cho `CHU_NHA` và `QUAN_LY` mở quản lý/tạo/sửa/gỡ tin; Chủ nhà chỉ thao tác tòa mình sở hữu, Quản lý chỉ thao tác tòa có `quan_ly_id` là tài khoản hiện tại. Khách thuê, ADMIN và quản lý không được phân công bị chặn ở backend. Menu Tin đăng của Chủ nhà/Quản lý dẫn thẳng tới `/TinDang/QuanLy`.
- Quyền mặc định `TIN_DANG` của `QUAN_LY` đổi từ `READ` sang `WRITE`. Updater v13 sao lưu DB rồi chỉ nâng giá trị `READ` mặc định; quyền đã thu hồi thành `NONE` không bị mở lại. Máy có DB v12 cần dừng app, sao lưu DB ra ngoài repository, chạy `--update-database`, `--check-database`, rồi khởi động lại. Không sửa database local trong task này.
- File chính: `Controllers/TinDangController.cs`, `_Sidebar.cshtml`, `permissions.seed.json`, `DatabaseUpdates.cs`, `AccountReuseSchema.cs`, hai bộ test quyền/tin đăng, README và `docs/cap-nhat-csdl.md`. Kiểm thử trên database tạm: 6/6 ca quản lý/chủ nhà/phạm vi vai trò PASS và 4/4 ca ma trận quyền/updater v13 PASS; build qua output riêng vì web đang chạy khóa output mặc định. Còn cảnh báo giấy phép ImageSharp có sẵn; chưa nghiệm thu trực quan bằng trình duyệt. Giả định Quản lý chỉ phụ trách tòa được gán qua `toa_nha.quan_ly_id`.

## Thêm `run.bat` khởi động dự án trên Windows (04/10/2026)

- Thêm `run.bat` ở gốc repository: kiểm tra .NET SDK/project, restore, dùng `DatabasePath` đã cấu hình hoặc database mặc định, tự khởi tạo bằng quy trình của dự án khi file chưa tồn tại, kiểm tra schema rồi chạy profile HTTP tại `http://localhost:5247`.
- Với database đã tồn tại, script không ghi đè và không tự chạy updater; schema chưa sẵn sàng làm script dừng và hướng dẫn sao lưu/chạy `--update-database`. Không tạo ADMIN, không chứa credential. Có `run.bat --check-only` để xác minh mà không giữ web chạy.
- README đã bổ sung cách dùng cho cả máy mới và máy có database cần giữ dữ liệu. Xác minh thực tế `run.bat --check-only` PASS trên database thử mới: restore, khởi tạo schema hiện hành tại thời điểm kiểm thử và check thành công. Chạy lại trên chính file thử PASS, đi qua nhánh giữ database hiện có và SHA-256 trước/sau không đổi. Không dùng database cá nhân; database mặc định vô tình sinh trong lần gọi thử sai đã được dọn cùng toàn bộ backup liên quan sau khi xác nhận trước đó chưa tồn tại.

## Hoàn thiện sau rà soát S2-01 đến S2-10 (04/10/2026)

- S2-03: thêm `TinDangExpirationService` và tác vụ nền chạy mỗi phút; các lối vào danh sách công khai, tìm kiếm và quản lý cũng đồng bộ ngay. Tin `DANG_HIEN_THI` quá hạn chuyển `TAM_AN`; Chủ nhà thấy cảnh báo/nhãn **Đã hết hạn** và có thể đăng lại. Không đổi schema.
- S2-07: thống nhất `MOI` và `DA_HEN_LICH` là chưa xử lý cho cả đánh dấu quá 24 giờ và badge menu; badge 0 được ẩn. Bổ sung test xác nhận hai trạng thái được đếm/đánh dấu, trạng thái đã duyệt không bị tính.
- S2-09: mọi dòng yêu cầu của Khách thuê đều mở đúng tin liên kết. Tin còn public hoạt động như cũ; tin ẩn/hết hạn/phòng không còn trống chỉ chính khách đã gửi yêu cầu được xem lại ở chế độ chỉ đọc, có cảnh báo và không có form gửi mới. API/khách khác vẫn không truy cập được.
- Cập nhật `verification/s206_http.py` theo public layout và hành vi xem lại tin; script PASS toàn bộ trên database mới, gồm validation, quyền/CSRF, chống trùng, mã yêu cầu, gửi đồng thời, rollback, integrity/FK và tin hết hạn chỉ đọc. Không đọc/ghi database cá nhân.
- Xác minh: build test project PASS; `PermissionTests` 50/50 PASS; `RoomServices|RoomImage` 31/31 PASS; `LichHen|YeuCauTenant|DesiredDate` 152/152 PASS; nhóm hồi quy mới 6/6 PASS; `git diff --check` PASS. Đo lại S2-04 với 500 tin PASS dưới 2 giây: request đầu 140,808 ms, mẫu chậm nhất 36,82 ms. Cảnh báo còn lại: NU1900 do không lấy được dữ liệu lỗ hổng NuGet, thiếu license ImageSharp và CS8601 có sẵn trong `AuthController`.
- Database mặc định không tồn tại; mọi xác minh dùng database mới trong `data/` hoặc thư mục tạm. Không thay đổi quy trình database, không commit/push. Chưa nghiệm thu trực quan ở viewport 360 px; form lập hợp đồng vẫn thuộc S3-01.

## Chủ nhà quản lý tin đăng và gửi lại sau khi hủy yêu cầu (04/10/2026)

- Bổ sung `/TinDang/QuanLy` cho vai trò Chủ nhà: liệt kê phòng thuộc các tòa nhà do tài khoản sở hữu, cho đăng/đăng lại tin khi phòng `TRONG` và gỡ tin đang hiển thị. Gỡ tin chuyển trạng thái sang `TAM_AN`, không xóa bản ghi; mọi thao tác kiểm tra lại quyền sở hữu và quyền ghi `TIN_DANG` ở backend. Sidebar Chủ nhà dẫn vào màn quản lý; danh sách phòng có nút **Đăng tin** cho phòng trống.
- Form đăng tin lấy sẵn phòng, tòa nhà, diện tích, giá thuê và mô tả phòng; Chủ nhà nhập/chỉnh tiêu đề, mô tả. Tin được hiển thị 30 ngày và unique index hiện có tiếp tục bảo đảm mỗi phòng chỉ có tối đa một tin `DANG_HIEN_THI`.
- Xác nhận truy vấn nghiệp vụ đã loại `DA_HUY`; lỗi demo trước đây do cùng Khách Demo còn một yêu cầu `MOI` khác trên chính tin đó. Sửa `RequestDemoSeeder` để mỗi khách/tin chỉ có một yêu cầu mở, vẫn giữ khách phụ riêng cho ca cảnh báo trùng lịch. Tạo mới `data/s2-08-demo/demo-v2.sqlite`; file demo cũ được giữ nguyên, không ghi đè. Demo v2 có 3 tài khoản giả, 3 phòng, 3 tin, 4 yêu cầu và không có cặp khách/tin mở trùng.
- File chính: `TinDangController`, `TinDangViewModels`, Razor `TinDang/QuanLy`, `TinDang/Tao`, sidebar, danh sách phòng, `RequestDemoSeeder`, `OwnerListingManagementTests`. Không đổi schema hoặc quy trình database.
- Xác minh thực tế: build PASS; nhóm tin công khai + quản lý tin + lịch hẹn **150/150 PASS**; kiểm thử riêng xác nhận yêu cầu `DA_HUY` không chặn `Send`; HTTP đăng nhập thật xác nhận màn quản lý tin và toàn bộ tiêu chí lịch hẹn hiện có PASS; `git diff --check` PASS. Web demo v2 đang chạy tại `http://127.0.0.1:5268`.

## Hủy yêu cầu bởi Chủ nhà và Khách thuê (04/10/2026)

- Bổ sung nút **Hủy yêu cầu** trên trang chi tiết cho cả đúng Chủ nhà của phòng và đúng Khách thuê đã gửi yêu cầu. Chỉ hiện và chỉ cho phép xử lý ở trạng thái `MOI` hoặc `DA_HEN_LICH`; endpoint kiểm tra lại quyền/trạng thái ở backend và có hộp thoại xác nhận trước khi gửi.
- Khi hủy, yêu cầu chuyển `DA_HUY` bằng cập nhật có kiểm tra phiên bản, ghi lịch sử gồm người thực hiện/vai trò/thời điểm và gửi thông báo cho bên còn lại trong cùng transaction. Chủ nhà vẫn có luồng **Từ chối** riêng với danh sách lý do bắt buộc. Không thay đổi schema hoặc quy trình database.
- File chính: `Services/LichHenService.cs`, `Controllers/LichHenController.cs`, `Views/LichHen/ChiTiet.cshtml`, các model/view model lịch hẹn và test `LichHenTuChoiTests.cs`, `LichHenLichSuTests.cs`.
- Xác minh thực tế: build PASS; nhóm `LichHen` **148/148 PASS**; `git diff --check` PASS. HTTP đăng nhập thật xác nhận cả Chủ nhà và Khách thuê đều thấy form **Hủy yêu cầu** trỏ đúng endpoint trên yêu cầu còn mở; không bấm hủy để giữ nguyên fixture demo. Cảnh báo NuGet/ImageSharp có sẵn, không có test lỗi. Web demo đã chạy lại tại `http://127.0.0.1:5268`.

## Giờ hẹn 24 giờ và danh sách yêu cầu của Khách thuê (04/10/2026)

- Thay control `datetime-local` bằng ô ngày và ô giờ 24 giờ cho màn hình xác nhận/đổi lịch; chấp nhận `0:00` hoặc `00:00`, chuẩn hóa thành `00:00` trước khi gửi. Đồng thời sửa tên trường POST của luồng đổi lịch để bind đúng `LichHenNhap`.
- `/YeuCau` cho phép vai trò Khách thuê và chỉ truy vấn yêu cầu gắn với tài khoản khách hiện tại. Giao diện đổi thành **Yêu cầu của tôi**, ẩn bộ lọc tòa nhà và giữ nút **Chi tiết**; trang chi tiết tiếp tục chặn chỉnh sửa đối với khách.
- Không thay đổi schema/database. Xác minh: build PASS; nhóm `LichHen` **144/144 PASS** gồm ca `00:00`; HTTP đăng nhập thật xác nhận Khách thuê mở `/YeuCau`, thấy đúng 4 liên kết chi tiết và không có control xử lý; Chủ nhà thấy ô giờ 24 giờ `00:00`, cảnh báo trùng lịch và các thao tác cũ vẫn còn. Web demo đã chạy lại tại `http://127.0.0.1:5268`.

## Bổ sung lối vào chi tiết lịch hẹn từ danh sách (04/10/2026)

- Danh sách **Yêu cầu của khách** đã có cột **Thao tác** và nút **Chi tiết** trên từng dòng, dẫn tới `/LichHen/ChiTiet/{id}`. View model/projection được bổ sung ID thật của `yeu_cau_thue`; trang đích tiếp tục kiểm tra quyền chủ nhà/khách ở backend.
- Không đổi schema hoặc dữ liệu demo. Xác minh: build PASS; nhóm `LichHen` **143/143 PASS**; đăng nhập Chủ nhà qua HTTP, `/YeuCau` trả đủ bốn nút với đích `/LichHen/ChiTiet/1..4`. Web demo đã chạy lại tại `http://127.0.0.1:5268`.

## Dữ liệu demo và hướng dẫn nghiệm thu S2-08 (04/10/2026)

- Đã tạo database demo mới, schema v12 tại `data/s2-08-demo/demo.sqlite` (đang được Git ignore), gồm hai tài khoản giả Chủ nhà/Khách thuê, hai phòng và bốn yêu cầu tách riêng cho xác nhận/cảnh báo trùng lịch, từ chối, xem lịch sử và duyệt Thuê ngay. Không đọc, sao chép hoặc ghi database cá nhân.
- Sửa `RequestDemoSeeder` để lấy mật khẩu từ `RequestDemo:Password`, không nhúng mật khẩu và không tạo ADMIN. Seeder chỉ chấp nhận database mới không có tài khoản/dữ liệu nghiệp vụ, kiểm tra schema trước ghi và tạo backup; console in các URL demo cùng giờ lịch xung đột. `Program.cs` truyền cấu hình này; README có quy trình máy mới và nhắc máy có DB cần giữ dữ liệu không được chạy seeder.
- Web demo đang chạy tại `http://127.0.0.1:5268` với Data Protection keys riêng trong thư mục demo. Credential và hướng dẫn chi tiết nằm trong `data/s2-08-demo/access.txt`, không đưa vào Git.
- Xác minh thực tế: build PASS; `--check-database` PASS; truy vấn SQLite `mode=ro` xác nhận schema v12, không lỗi FK, 2 tài khoản có BCrypt hash dài 60, 4 yêu cầu và 2 phòng đều đúng trạng thái đầu; nhóm `LichHen` **143/143 PASS**; HTTP đăng nhập thật cho cả hai vai trò và kiểm tra form/cảnh báo/4 lý do/nút duyệt/quyền xem lịch sử đều PASS. Chưa bấm các thao tác ghi trong browser để giữ fixture nguyên trạng cho người nghiệm thu.

## Kiểm tra tiêu chí xử lý lịch hẹn S2-08 (03/10/2026)

- Đã đối chiếu controller, service, Razor/JavaScript, migration v10 và kiểm thử hiện có. Ba tiêu chí đầu đạt: xác nhận bắt buộc ngày giờ tương lai và cảnh báo lịch cùng phòng trong khoảng ±30 phút; từ chối chỉ nhận đúng 4 lý do và `LY_DO_KHAC` bắt buộc ghi chú 5–500 ký tự; các thao tác xác nhận, đổi lịch, từ chối, duyệt thuê ngay ghi lịch sử cùng người thực hiện/thời điểm, khách của yêu cầu xem được lịch sử.
- Tiêu chí duyệt **Thuê ngay** đạt phần cập nhật nghiệp vụ: yêu cầu chuyển `DA_DUYET`, phòng chuyển `DA_DAT_COC` trong cùng transaction và nút **Lập hợp đồng** chỉ hiện cho chủ nhà khi hai trạng thái khớp. Chưa đạt nếu hiểu nút phải mở form lập hợp đồng hoàn chỉnh: route hiện chỉ dẫn đến trang chờ S3-01, chưa tạo hợp đồng.
- Xác minh thực tế: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --filter "FullyQualifiedName~LichHen"` **143/143 PASS**. Có cảnh báo không tải được dữ liệu lỗ hổng NuGet, ImageSharp license và CS8601 sẵn có; không có test lỗi. Database mặc định `QL_PhongTro/Data/local-dev.sqlite` không tồn tại và cấu hình không chỉ định đường dẫn khác, nên không tạo/ghi DB local và chưa kiểm thử trình duyệt thật.
- Không thay đổi mã nguồn, schema hay quy trình database. Bước tiếp theo phù hợp là S3-01: thay trang chờ `Views/LichHen/LapHopDong.cshtml` bằng luồng lập hợp đồng thật và giữ điều kiện quyền/trạng thái hiện có.

## Merge S2-02 xoá ảnh phòng vào dev (03/10/2026)

- Đã merge `feature/S2-2/owner-delete-room-photo` vào nhánh local `dev` tracking `origin/dev`. Tự xử lý conflict ở `AccountReuseSchema.cs`, `DatabaseUpdates.cs`, `README.md` và `docs/tien-do.md`.
- Conflict schema được hợp nhất bằng cách giữ v11 của `dev` cho bảng `yeu_cau` và nâng thay đổi xoá ảnh sang v12 (`anh_phong.dang_cho_xoa`, `loi_xoa_gan_nhat`, `lan_thu_xoa_gan_nhat`). `AccountReuseSchema` chấp nhận dải v4-v12; tài liệu DB/README cập nhật schema hiện hành v12.
- Xác minh sau merge: build Debug project chính PASS; nhóm ảnh + migration/schema **16/16 PASS**; full xUnit **101/101 PASS**. `git diff --check` PASS; không có DB/SQLite/sidecar được Git theo dõi. Còn cảnh báo ImageSharp license và CS8601 trong `AuthController` là cảnh báo có sẵn.
- Chưa push `dev` lên remote trong lượt này. DB local thật không được ghi/nâng cấp; chỉ có artifact demo/build trong `data/` đang ignore.

## Kiểm tra tiêu chí ảnh phòng và demo nghiệm thu (03/10/2026)

- Đã đối chiếu mã hiện tại và chạy lại kiểm thử: hệ thống thỏa 4 tiêu chí ảnh phòng. `RoomImageStore` giới hạn tối đa 8 ảnh/phòng, 5 MiB/tệp, chỉ JPG/PNG hợp lệ và sinh thumbnail rộng 400px; form chủ nhà có kéo-thả/sắp xếp, ảnh `thu_tu=1` là ảnh đại diện trên `/TinDang`; xoá ảnh có xác nhận, xoá bản ghi + tệp gốc + thumbnail, và lệnh đối chiếu kho báo mồ côi/missing. Sau khi demo phát hiện ảnh vỡ khi `RoomImagesPath` nằm ngoài `wwwroot`, đã sửa `Program.cs` để phục vụ storage cấu hình riêng tại `/uploads/rooms`.
- Thêm script demo `verification/prepare_room_images_demo.py`. Script tạo DB và kho ảnh mới trong `data/room-images-demo`, chạy server riêng, upload 5 ảnh PNG qua HTTP thật, tạo tin public, xác minh danh sách tin dùng thumbnail ảnh đầu, chi tiết dùng ảnh gốc ảnh đầu và `--check-room-image-storage` báo `missing: 0; orphan: 0`. Không đọc/ghi DB local thật.
- Demo mới đang chạy: `http://localhost:5259`, PID `27972`, DB `data/room-images-demo/20261003-174320-e7c11c/demo.sqlite`, kho ảnh `data/room-images-demo/20261003-174320-e7c11c/uploads/rooms`. Chủ nhà: `demo-chu_nha-f4f04988@example.test`; mật khẩu chung: `42662CAC1411087053DEa1!`; phòng demo: `/PhongTro/Edit/1`; danh sách tin: `/TinDang`; chi tiết tin: `/TinDang/ChiTiet/1`. Đã kiểm tra trực tiếp thumbnail `/uploads/rooms/...-thumb.png` trả `200 image/png`.
- Hướng dẫn test nhanh: đăng nhập Chủ nhà, mở `/PhongTro/Edit/1`, kiểm tra bộ đếm `5/8`; kéo-thả ảnh khác lên đầu rồi mở `/TinDang` để thấy ảnh đại diện đổi; tải thêm 3 ảnh JPG/PNG hợp lệ để đạt `8/8`, ảnh thứ 9 bị từ chối; thử ảnh >5 MB hoặc định dạng khác để thấy lỗi; bấm **Xóa**, chọn **Huỷ** để ảnh còn nguyên, bấm lại và **Xoá** để ảnh biến mất, bộ đếm giảm và tệp gốc/thumbnail mất khỏi thư mục kho.
- Xác minh đã chạy trong lượt này: build runtime demo PASS; script demo PASS; `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter "FullyQualifiedName~RoomImage"` **10/10 PASS**. Cảnh báo ImageSharp license và CS8601 trong `AuthController` là cảnh báo có sẵn. Chưa thao tác browser thủ công thay người dùng; demo đã sẵn để nghiệm thu trực quan.

## S2-02 — ảnh phòng, thumbnail và xoá ảnh (03/10/2026)

- PO chốt: trang chi tiết dùng ảnh lớn + dải thumbnail; phòng chưa ảnh dùng placeholder chung; chủ nhà tải trong form sửa phòng; thumbnail giữ tỉ lệ, không phóng ảnh nhỏ; JPEG quality 82, PNG nén lossless. AC2: không thêm nút “Đặt làm ảnh đại diện”; desktop kéo-thả, màn hình cảm ứng dùng mũi tên lên/xuống; nhãn **Ảnh đại diện** nổi bật trên ảnh đầu.
- Hoàn tất upload tối đa 8 ảnh/phòng, 5 MiB/tệp, JPG/PNG kiểm tra cả nội dung; xác minh ảnh hỏng, ảnh vượt cỡ, MIME/đuôi giả và quyền sở hữu phía server. Ghi ảnh gốc + thumbnail đã auto-orient trong `wwwroot/uploads/rooms/{roomId}`, lưu đường dẫn/thứ tự/thời điểm vào `anh_phong`; transaction SQLite immediate cấp thứ tự, dọn cặp tệp nếu lỗi/hủy. Upload nhiều tệp có tiến trình/lỗi riêng và lưới xem trước `x/8`.
- Endpoint reorder kiểm tra chủ sở hữu và tập ID gửi lên phải khớp chính xác (không thiếu/thừa/trùng). Cập nhật thứ tự trong một transaction, giữ tập ảnh hiện có; tab lưu sau được phép thay thứ tự mới nhất. Ảnh tải mới nhận thứ tự cuối. List tin lấy thumbnail của `thu_tu=1`; API/chi tiết trả ảnh theo cùng thứ tự.
- UI dùng HTML drag/drop trên desktop, mũi tên lên/xuống cho cảm ứng, cập nhật badge/bộ đếm ngay; lỗi/mất mạng khôi phục thứ tự cũ và báo lỗi. AC4 đã bổ sung nút **Xóa** trên từng ảnh, hộp thoại xác nhận “Xoá ảnh này? Ảnh sẽ bị xoá vĩnh viễn và không thể khôi phục.” với **Huỷ/Xoá**, không có hoàn tác.
- Xoá ảnh kiểm tra chủ sở hữu phía server, xoá bản ghi + tệp gốc + thumbnail, đánh lại `thu_tu` liên tục. Xoá ảnh đại diện làm ảnh kế tiếp lên đại diện; xoá hết ảnh thì list/detail dùng placeholder. Xác nhận hai lần liên tiếp không gây lỗi; chủ nhà khác không xoá được ảnh không thuộc phòng của mình.
- Schema nền sau merge lên v12: v11 giữ bảng `yeu_cau` của dev, v12 thêm `dang_cho_xoa`, `loi_xoa_gan_nhat`, `lan_thu_xoa_gan_nhat` vào `anh_phong`. Nếu xoá tệp kho lưu trữ lỗi, giữ bản ghi ở trạng thái chờ xoá, lưu lỗi gần nhất, UI giữ nguyên ảnh và background service retry định kỳ. Có lệnh thủ công `--retry-pending-room-image-deletes` và `--check-room-image-storage`; README và `docs/cap-nhat-csdl.md` đã cập nhật quy trình máy mới/máy có DB.
- File/thành phần chính: `Services/RoomImageStore.cs`, `Services/RoomImageDeletionService.cs`, `Services/RoomImageCleanupHostedService.cs`, `Controllers/PhongTroController.cs`, `Data/DatabaseUpdates.cs`, `Models/AnhPhong.cs`, Razor `PhongTro/Create`, `wwwroot/js/room-images.js`, `wwwroot/css/room-images.css`, README và tài liệu DB. Test ở `RoomImageStoreTests.cs`, `RoomImageUploadTests.cs`, migration tests.
- Xác minh: build Debug PASS; nhóm ảnh focused **9/9 PASS**; nhóm migration/schema focused **6/6 PASS**; full xUnit **100/100 PASS**. Test xóa ảnh đại diện cập nhật thumbnail public/detail, xóa ảnh giữa giữ thứ tự liên tục, xóa hết về placeholder, upload lại đủ 8, idempotent, quyền sở hữu và đối chiếu kho 0 orphan/0 missing. Chưa chạy browser thật cho dialog; xác minh UI hiện tại qua markup/JS và HTTP.
- Browser demo: kéo ảnh thứ ba lên đầu, reload giữ thứ tự; mũi tên lên/xuống đổi vị trí; chặn POST reorder xác nhận rollback và thông báo lỗi; API/detail/list xác nhận ảnh đầu và thứ tự thumbnail/gốc khớp. Chọn 10 ảnh khi đã có 5 chỉ thêm 3 để đạt `8/8`, 7 ảnh còn lại báo đủ ảnh. Đã phục hồi 8 cặp ảnh giả cho demo, tất cả ảnh gốc/thumbnail tải được; list dùng thumbnail 400px. Server demo chạy tại `http://localhost:5259`, fixture hiện có 8 ảnh; manifest `data/room-images-demo-d51197c4/access.json` chỉ dành cho máy local, không chia sẻ/commit.
- Giới hạn xác minh: chưa throttling mạng 3G thật hoặc thử thiết bị cảm ứng vật lý; đã thử mũi tên ở viewport browser nhỏ và chặn request để xác nhận rollback. ImageSharp license warning và CS8601 trong `AuthController` là cảnh báo có sẵn.
- Bước tiếp theo: chạy nghiệm thu trình duyệt thật nếu cần quan sát dialog; DB thành viên cần dừng app, sao lưu ngoài repo, chạy `--update-database` rồi `--check-database` để lên v12 trước khi chạy web.

## S2-07 AC4 — số lượng yêu cầu chưa xử lý trên menu (03/10/2026)

- PO chốt trạng thái chưa xử lý là `MOI` (Mới) và `DA_HEN_LICH` (Đã hẹn lịch); các trạng thái còn lại đã xử lý. Số 0 được ẩn trên menu.
- Thêm `PermissionService.UnprocessedRequestCountAsync`: đếm độc lập với bộ lọc `/YeuCau`, chỉ lấy yêu cầu thuộc các tòa đang hoạt động do chủ nhà hiện tại quản lý. Menu yêu cầu trỏ trực tiếp `/YeuCau` và hiển thị badge khi số lượng lớn hơn 0; mỗi lần render menu đọc dữ liệu hiện tại nên phản ánh yêu cầu mới hoặc thay đổi trạng thái sau khi tải lại/chuyển trang.
- Không thay đổi schema, database local hoặc quy trình khởi tạo/nâng cấp. File thay đổi: `Authorization/PermissionService.cs`, `Views/Shared/_Sidebar.cshtml`.
- Xác minh: build project chính với output riêng `obj/S207AC4Check`/`bin/S207AC4Check` PASS, 0 lỗi; có cảnh báo license ImageSharp và CS8601 đã tồn tại. Chưa chạy test HTTP/browser hoặc ghi database local.

## Demo nghiệm thu S2-10 (02/10/2026)

- Thêm `verification/prepare_s210_demo.py` và hướng dẫn README. Script chỉ tạo database mới dưới `data/s210-demo/`, dùng initializer + permission demo của dự án, dữ liệu tòa/cấu hình giả; không đọc/ghi database local. Artifact và credential thuộc `data/` đã ignore. Bản thành công mới nhất chạy ở cổng 5251; `latest.txt` trỏ tới `access.json` chứa URL, PID, database và bốn tài khoản chung mật khẩu ngẫu nhiên.
- Dữ liệu nghiệm thu 10/2026: điện theo chỉ số 4.000đ/kWh, nước theo người 80.000đ/người/tháng. Cấu hình chờ 11/2026 được lưu qua HTTP thật: điện theo người 95.000đ/người/tháng, nước theo chỉ số 18.000đ/m³. HTTP form có đủ hai lựa chọn và hiển thị kỳ; POST giá điện 0 trả lại form có lỗi, bốn version trong DB không đổi. `--check-database`, integrity và FK PASS.
- `Program.cs` nhận cấu hình tùy chọn `DataProtectionKeysPath` để demo dùng kho khóa cookie riêng trong workspace. Mặc định không đặt biến này nên cách chạy và kho khóa hiện tại không đổi; không đổi schema/updater. Lần đầu trong sandbox thất bại do kho DPAPI người dùng không ghi được, các server lỗi đã dừng; bản sau dùng kho riêng thành công.
- Build runtime demo PASS, không lỗi; toàn suite sau thay đổi PASS 46/46 trong 1 phút 15 giây; còn cảnh báo ImageSharp và CS8601 có sẵn. Computer Use không có browser khả dụng nên chưa kiểm tra trực quan; xác minh UI dùng HTML MVC thật qua HTTP. Server demo thành công được giữ chạy để người dùng mở bằng trình duyệt trên máy.

## S2-10 — hoàn thiện cấu hình điện/nước lần đầu (02/10/2026)

- Đã bỏ điểm chặn còn lại của S2-10: chủ nhà có thể cấu hình điện/nước ngay cả khi tòa chưa khởi tạo danh mục gợi ý hoặc các dòng mặc định chưa chốt giá. Cấu hình hợp lệ đầu tiên cũng bắt đầu từ ngày 01 tháng kế tiếp; dòng chưa chốt của kỳ hiện tại, nếu có, chỉ được đóng `den_ngay` và không bị ghi đè. Nếu thiếu danh mục DIEN/NUOC hoặc liên kết tòa, service tạo đúng phần tối thiểu và không tự bật áp dụng mặc định cho phòng.
- Giữ nguyên giao diện hai lựa chọn độc lập, trường giá theo phương thức, thông báo “Áp dụng từ kỳ MM/yyyy”, validation giá bắt buộc lớn hơn 0, kiểm tra sở hữu/CSRF/stale form và transaction hiện có. Không đổi schema, updater, README hay database local.
- File thay đổi: `Services/DichVuService.DienNuoc.cs`; thêm `Tests/QL_PhongTro.Tests/RoomServicesTests.ElectricWater.cs`; cập nhật tài liệu này. Giữ nguyên thay đổi có sẵn trong `Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj`.
- Kiểm thử mới dùng SQLite tạm do fixture tạo: tạo cấu hình trước bước khởi tạo danh mục; chốt giá lần đầu từ dòng chưa có giá; điện theo người/nước theo chỉ số và đúng đơn vị; đổi cách tính giữ nguyên kỳ hiện tại; tra giá trước/sau ranh giới kỳ; từ chối giá trống/0/âm và không ghi dở. Các test mới PASS 6/6; toàn suite PASS 46/46 trong 1 phút 14 giây. Build đi kèm test không có lỗi; còn cảnh báo license ImageSharp có sẵn. Chưa chạy lại kiểm tra trực quan trên trình duyệt trong lần sửa này.
- Cách dùng không đổi: Chủ nhà mở **Cấu hình điện nước** từ danh sách tòa hoặc trang dịch vụ, chọn cách tính và nhập giá dương. Màn hình hiển thị kỳ áp dụng trước khi lưu; tải lại sẽ thấy cấu hình đang chờ. Không cần cập nhật database cho thay đổi này.

## S2-10:4 — lưu cấu hình điện/nước cho kỳ kế tiếp (02/10/2026)

- Hoàn thành luồng chính trên branch feature/S2-10/save-next-period-config. Giữ schema/version SQLite hiện có, không migration/bảng mới. Dùng cau_hinh_dich_vu.tu_ngay/den_ngay: cấu hình mới bắt đầu ngày 1 tháng kế tiếp UTC+7, chỉ đóng khoảng cũ, không ghi đè cách tính/giá hiện tại. Thời gian trong từng GET/lần lưu lấy một lần từ ITimeProvider (fallback UTC hệ thống).
- Điện/nước so sánh độc lập với baseline chờ nếu có, hiện tại nếu chưa có. Không thay đổi thì không tạo/sửa version. Có chờ đúng tòa + dịch vụ + phong_id NULL + ngày đầu kỳ sau thì UPDATE giữ ID; chưa có thì đóng khoảng trước INSERT trong cùng transaction. Unique index và trigger chống chồng lấn vẫn bảo vệ DB. Hai dịch vụ cùng transaction; lỗi bên sau rollback cả version và ngày kết thúc bên trước, gồm audit.
- Giao diện phân biệt cấu hình đang áp dụng/chờ, kỳ bắt đầu chờ, form sửa chờ; mở lại không báo thay đổi. Giữ JavaScript S2-10:3 hiện có vì baseline data-saved-* đã được cấp đúng. Giữ validation S2-10:2: giá trống/0/âm bị chặn, lỗi giữ dữ liệu nhập, ô không chọn bỏ qua kể cả lỗi binding. Quyền CHU_NHA/PHONG_TRO, sở hữu tòa và antiforgery giữ nguyên.
- POST và service tính lại kỳ; form qua tháng hoặc snapshot trạng thái không còn khớp bị từ chối trước khi ghi. Snapshot SHA256 so sánh toàn bộ phiên bản điện/nước, nhận biết UPDATE cùng ID. Không coi kỳ browser là nguồn quyết định hiệu lực. Các đường SuaGiaBanDau/DoiGia chung với DIEN/NUOC được chặn và hướng về màn hình cấu hình để không vượt quy tắc kỳ kế tiếp; dịch vụ khác giữ nguyên.
- Chưa triển khai nghiệp vụ chưa chốt: thiết lập lần đầu/giá chưa chốt; đổi chờ về đúng hiện tại (hủy); đổi cách tính khi có giá riêng phòng; có phiên bản sau kỳ kế tiếp; sửa chờ đã tham chiếu hóa đơn/hợp đồng. Service báo rõ và không ghi; không xóa chờ/không tự suy diễn quy tắc. Nguồn số người vẫn để lát hóa đơn sau. Dịch vụ ngừng áp dụng cũng dừng thay đổi để đối chiếu lịch.
- File thay đổi: Controllers/DichVuController.cs; Services/DichVuService.DienNuoc.cs; Services/DichVuService.Management.cs; ViewModels/CauHinhDienNuocViewModel.cs; Views/DichVu/DienNuoc.cshtml; docs/tien-do.md. Không sửa model/AppDbContext/schemaSQL/updater/HoaDonDichVuService/electric-water-config.js, không sửa hoặc build/run Tests/tests.
- Xác minh console tạm (obj/S2104Check, ignore; không dùng project Tests/tests) trên bản sao DB giả S2103: PASS đổi điện chỉ số→người, người→chỉ số; giá điện/nước riêng và cả hai; chỉ một dịch vụ thay đổi tăng 1 dòng, cả hai tăng 2; kỳ hiện tại giữ ID/cách tính/giá cũ, kỳ kế tiếp đúng mới; sửa chờ giữ ID/count; mở lại baseline sạch; POST không đổi không thêm dòng; UTC+7 31/12 16:59:59→17:00 chuyển năm, form cũ bị từ chối; trigger lỗi INSERT nước chứng minh rollback cả hai và các khoảng cũ; stale form bị từ chối; 12 tổ hợp giá sai ở service, ô không chọn bỏ qua; quyền chủ khác bị chặn. PASS guard route giá cũ, đổi chờ về hiện tại, lịch xa hơn và giá ban đầu chưa chốt đều không ghi dữ liệu. Chưa stress test nhiều writer đồng thời.
- HTTP thực tế assembly cuối trên DB giả riêng: PASS lưu/mở lại có hiện tại + chờ + kỳ 11/2026; số dòng 4→5 khi đổi điện, UPDATE điện 5→5 giữ ID; 24 tổ hợp validation trống/0/âm (hai dịch vụ × hai phương thức × tổ hợp của dịch vụ còn lại), giữ giá/phương thức, không ghi dở; ô không chọn trống/0/âm/text/overflow không chặn; thông báo trước lưu vẫn hiện khi có thay đổi hợp lệ dù bên còn lại sai; stale period/tab không ghi; tòa chủ khác GET/POST 403, thiếu CSRF 400; integrity/FK PASS. Bản chốt hóa đơn đã phát hành/chi tiết trên DB giả không đổi.
- Browser thật bằng skill computer-use: PASS baseline chờ sạch, sửa giá điện hiện kỳ, hoàn tác về baseline chờ ẩn kỳ, đổi phương thức bật kỳ và disabled ô cũ, giá trống/0/âm không lưu, sửa chờ qua nút lưu và reload đúng, thay đổi nước độc lập. Sau đổi riêng nước DB có đúng 6 dòng (4 gốc + 2 chờ), giá gốc điện 120000/nước 15000 giữ nguyên; chờ điện 140000/nước 16000 ngày 2026-11-01. Ảnh obj/S2104Check/pending-proof.png. Chưa nghiệm thu Safari/Firefox hoặc các viewport khác.
- Build project chính PASS: dotnet build QL_PhongTro.csproj --no-restore -c Debug -p:IntermediateOutputPath=obj/S2104Final/ -p:OutputPath=bin/S2104Final/ (từ thư mục app). Còn warning ImageSharp license/CS8601 AuthController có sẵn. Console xác minh tạm có NU1900 do feed vulnerability không truy cập được; đã chạy thành công bằng DLL, không dùng apphost bị khóa. git diff --check PASS.
- Không ghi local-dev.sqlite hoặc backup có sẵn; SHA256 local DB trước/sau bằng nhau: cea2190c068f4bb35eda4f28130258ce263b2347b6894d409b67de37b08ac2ac. Chỉ ghi các DB giả trong obj/S2104Check; không kill process người dùng. Không stage/commit/push, không bắt đầu task khác; giữ nguyên tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj modified có sẵn.


## S2-10:3 — xác định và hiển thị kỳ bắt đầu áp dụng (02/10/2026)

- Quy chiếu nghiệp vụ: 01-yeu-cau-he-thong.md:690 và 02-csdl.dbml:105 xác định kỳ tháng dương lịch, ngày chốt chung của tòa không đổi ranh giới kỳ. HoaDonDichVuService.cs:39–40 dùng đầu/cuối tháng; DichVuService.Management.cs:8 (KySau) và DichVuPhongService.cs:13 (KyNgung) lấy tháng kế tiếp theo UTC+7, độc lập hóa đơn đã phát hành. Kỳ hiện tại là tháng lịch tại thời điểm mở/trả form; kỳ áp dụng là ngày 01 tháng kế tiếp. Tòa chưa có hóa đơn dùng cùng quy tắc, không cần hóa đơn đầu tiên hoặc lấy hóa đơn gần nhất +1. Không có điểm PO còn thiếu cho cách xác định kỳ của lát này.
- Tái sử dụng KyNgung và ITimeProvider hiện có trong DichVuService để lấy kỳ kế tiếp với một lần đọc thời gian; không tạo service/abstraction mới. GET lấy cấu hình và bản gốc riêng; POST lỗi đọc lại baseline/kỳ từ server, giữ ModelState/phương thức/giá đã nhập. Các trường baseline/kỳ có BindNever/ValidateNever, không tin giá trị client gửi.
- Phát hiện khác biệt chỉ xét phương thức và giá đang chọn của từng dịch vụ. Có ít nhất một thay đổi hợp lệ thì hiện “Áp dụng từ kỳ MM/yyyy” trước nút lưu; mở lần đầu/hoàn tác về giá cũ thì ẩn. Không xét ô giá ẩn; JS dùng BigInt so sánh nguyên VND chính xác. Giá trống/0/âm/ngoài long không được coi là thay đổi hợp lệ. Validation S2-10:2, quyền CHU_NHA/PHONG_TRO/sở hữu và CSRF được giữ.
- File sửa: Controllers/DichVuController.cs; Services/DichVuService.cs; Services/DichVuService.DienNuoc.cs; ViewModels/CauHinhDienNuocViewModel.cs; Views/DichVu/DienNuoc.cshtml; wwwroot/js/electric-water-config.js; docs/tien-do.md. Truy cập /DichVu/DienNuoc?toaNhaId=<id> hoặc nút Cấu hình điện nước hiện có.
- Không đổi schema/migration/quy trình DB hoặc cơ chế lưu S2-10:1/2. Thông tin kỳ trong lát này chỉ phục vụ xem trước; chưa lưu lịch/version hoặc trì hoãn hiệu lực thực tế. Chưa áp dụng cấu hình mới vào hóa đơn, không sửa hóa đơn cũ; S2-10:4 chưa thực hiện. Nguồn số người tính khoán vẫn để PO chốt ở lát hóa đơn sau.
- Xác minh thực tế trên DB mới chứa dữ liệu giả (obj/S2103Check, ignore), khởi tạo qua initializer/updater và script/installer hóa đơn hiện có: console với assembly cuối PASS 5 thời điểm UTC × 2 tòa (có hóa đơn/chưa có hóa đơn), gồm 16:59:59→17:00 cuối tháng theo UTC+7, chuyển năm, tháng 2 nhuận; baseline độc lập với dữ liệu đang chỉnh, đổi/hoàn tác PASS. Không dùng/sửa/build/run project Tests/tests.
- Browser thật PASS 12 trạng thái: ban đầu, đổi phương thức điện/nước, đổi giá từng dịch vụ/cả hai, hoàn tác, ô ẩn có giá, thay đổi sai duy nhất, một thay đổi hợp lệ khi dịch vụ còn lại sai; tòa chưa có hóa đơn ban đầu ẩn rồi đổi giá hiện kỳ 11/2026 trước nút lưu. Ảnh obj/S2103Check/period-proof.png (ignore). HTTP assembly cuối PASS 24 trường hợp validation giá và bốn tổ hợp hợp lệ/lưu/mở lại, giữ form lỗi/không ghi một phần, bỏ qua ô ẩn lỗi binding; baseline/kỳ giả gửi trong POST bị bỏ qua; GET/POST tòa chủ khác 403, thiếu CSRF 400, integrity/FK PASS. Hóa đơn giả đã phát hành và chi tiết không đổi qua các lần lưu cấu hình.
- Build project chính PASS: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -c Debug -p:IntermediateOutputPath=obj/S2103Final/ -p:OutputPath=bin/S2103Final/` từ gốc repo. Còn cảnh báo ImageSharp license/CS8601 cũ. Chỉ dùng thư mục output/cache riêng; không kill process của người dùng. Server xác minh dùng biến môi trường tắt EventLog provider như phiên trước, không sửa cấu hình app.
- Chưa nghiệm thu mobile/Safari/Firefox, trang mở xuyên ranh giới tháng hoặc người dùng ghi đồng thời. Kỳ được server tính lúc GET/POST; chưa tự cập nhật thông báo trong trang đang mở khi tháng đổi. Database local chỉ đọc schema/đối chiếu hash; không sửa tài khoản/dữ liệu cá nhân. Không stage/commit/push; giữ nguyên project test modified có sẵn. Dừng sau S2-10:3.

## S2-10:2 — chặn cấu hình thiếu/sai đơn giá (02/10/2026)

- Hoàn thành AC không lưu giá thiếu/0 và bổ sung chặn giá âm, độc lập điện/nước theo cách tính đang chọn. Giữ validation IValidatableObject và kiểm tra service hiện có; controller bỏ ModelState của giá không được chọn rồi kiểm tra lại giá đang chọn (MVC có thể bỏ qua object validation khi trường số không dùng lỗi binding). Giữ lỗi binding/giá thử nhập của trường đang chọn; không tải lại cấu hình DB khi trả form lỗi. Một giá sai không gọi service, không ghi cấu hình/audit hoặc lưu một phần.
- Frontend dùng _ValidationScriptsPartial và jQuery unobtrusive hiện có, metadata required/number/range, HTML min=1/step=1. JavaScript bật required cho ô đang chọn, disabled/ẩn ô không dùng và dọn lỗi tại ô vừa ẩn; thông báo giá >0 bằng tiếng Việt. Ô ẩn không chặn submit, kể cả request thủ công gửi chuỗi không phải số/giá tràn long ở ô không dùng.
- File sửa: Controllers/DichVuController.cs; ViewModels/CauHinhDienNuocViewModel.cs; Views/DichVu/DienNuoc.cshtml; wwwroot/js/electric-water-config.js; docs/tien-do.md. Không sửa service, schema, initializer, hóa đơn hoặc cách xác định số người; không đổi phân quyền CHU_NHA/PHONG_TRO, quyền sở hữu hay CSRF.
- Truy cập như S2-10:1: Dịch vụ và đơn giá → chọn tòa → Cấu hình điện nước, hoặc /DichVu/DienNuoc?toaNhaId=<id>. Đơn vị tiếp tục VND/kWh, VND/m³ và VND/người/tháng. Hiệu lực kỳ sau và nguồn số người vẫn dành cho lát được giao sau, chưa triển khai.
- Xác minh HTTP thực tế trên DB mới chứa dữ liệu giả (initializer/updater v8, obj/S2102Check được ignore): 24 trường hợp trống/0/âm của hai dịch vụ × hai cách tính × các tổ hợp phương thức PASS; lỗi đúng trường, giữ phương thức/giá vừa nhập, toàn bộ cấu hình và audit không đổi. PASS bốn tổ hợp hợp lệ/lưu/mở lại; đồng thời hai giá sai; ví dụ điện khoán 120000/nước chỉ số 0; bỏ qua ô không chọn trống/0/âm/chuỗi/overflow; ô đang chọn vẫn báo sai khi ô không chọn lỗi binding. GET/POST tòa chủ khác 403, thiếu CSRF 400, SQLite integrity/FK PASS. Không chạy/sửa project Tests/tests.
- Browser thật (Codex in-app browser, DB giả) PASS 12 trường hợp trống/0/âm cho điện/nước và hai cách tính, lỗi ngay tại ô, không ghi cấu hình/audit; giữ điện khoán 120000/nước chỉ số 0; đổi cách tính với ô cũ -2 bị ẩn rồi lưu giá mới hợp lệ thành công. Ảnh xác minh tại obj/S2102Check/validation-proof.png (ignore). Chưa kiểm tra responsive/mobile, Safari/Firefox, người dùng ghi đồng thời hoặc toàn bộ giá biên; đây không phải kết quả test tự động Tests/tests.
- Build project chính Debug PASS với output/cache riêng; lệnh từ gốc repo: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -c Debug -p:IntermediateOutputPath=obj/S2102Final/ -p:OutputPath=bin/S2102Final/`. Build mặc định thất bại vì không có quyền ghi obj/Debug/net10.0/apphost.exe; không kill process/sửa môi trường. Cảnh báo có sẵn ImageSharp license/CS8601 AuthController; harness tạm có NU1900 vì không truy cập được vulnerability feed NuGet. Server xác minh dùng Logging__EventLog__LogLevel__Default=None để tránh hạn chế Windows Event Log của môi trường; không sửa config app.
- Database local thực tế Data/local-dev.sqlite đã có schema v8, chỉ khảo sát read-only; không nâng cấp/ghi DB cá nhân. Không đổi quy trình DB/README. Không stage/commit/push hay tạo PR; thay đổi project test đã có sẵn được giữ nguyên. Dừng ở S2-10:2, không thực hiện S2-10:3.

## S2-10:1 — cấu hình cách tính điện/nước theo tòa (02/10/2026)

- Hoàn thành chọn độc lập Theo chỉ số đồng hồ / Khoán theo đầu người cho điện và nước, nhập giá tương ứng, lưu cùng transaction, thông báo thành công và mở lại đúng cấu hình. Tên tòa hiển thị từ database; GET/POST đều kiểm tra CHU_NHA, quyền ghi PHONG_TRO và quyền sở hữu; POST có CSRF. Audit dùng cơ chế hiện có.
- PO chốt đơn vị: điện VND/kWh, nước VND/m³, khoán VND/người/tháng. Không tự xác định số người hoặc thay đổi logic hóa đơn. Quy tắc nguồn số người/thời điểm chốt còn cần PO quyết định trong lát tính hóa đơn sau.
- Tận dụng CauHinhDichVu (cach_tinh, don_vi_tinh, don_gia, da_chot_gia), mã DIEN/NUOC và DichVuToaNha; không thêm bảng/cột/migration, không sửa quy trình DB. Lưu trực tiếp cấu hình đang hiệu lực; chưa trì hoãn đến kỳ sau. Khi chưa có cấu hình, tạo riêng điện/nước; không tự gán cho phòng hoặc bật mặc định. Giữ phiên bản khác, trạng thái áp dụng, cờ mặc định, dịch vụ khác và snapshot hóa đơn. Kiểm tra giá tối thiểu: bắt buộc số nguyên đồng >0, hai cách tính hợp lệ; kiểm tra đầy đủ đơn giá chưa thuộc lát này.
- File: Controllers/DichVuController.cs; Services/DichVuService.DienNuoc.cs; ViewModels/CauHinhDienNuocViewModel.cs; Views/DichVu/DienNuoc.cshtml; Views/DichVu/Index.cshtml; Views/PhongTro/ToaNha.cshtml; wwwroot/js/electric-water-config.js.
- Truy cập: Chủ nhà → Quản lý tòa nhà → Cấu hình điện nước (tòa đang hoạt động), hoặc Dịch vụ và đơn giá → Cấu hình điện nước. URL /DichVu/DienNuoc?toaNhaId=<id>. Chọn cách tính, nhập giá, Lưu cấu hình. Dùng quy trình README hiện có để khởi tạo/cập nhật DB rồi chạy project chính.
- Xác minh thực tế: build project chính Debug PASS với `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -c Debug -p:IntermediateOutputPath=obj/S210/ -p:OutputPath=bin/S210/` (từ gốc repo); đầu ra riêng vì cache Debug cũ bị từ chối quyền ghi. Còn cảnh báo ImageSharp license và CS8601 cũ. Build thử configuration S210Verification không đạt do ImageSharp yêu cầu license; không thay đổi thư viện/cấu hình project để xử lý.
- HTTP trên database mới chứa dữ liệu giả do LocalDatabaseInitializer/updater v8 tạo, nằm trong obj/S210Check (ignore): PASS cả bốn tổ hợp cách tính, mức giá/đơn vị persisted, thông báo, mở lại; GET/POST tòa chủ khác 403 và không ghi; thiếu CSRF 400; thiếu giá/0/cách tính sai/không phải số không lưu; audit; tòa có dịch vụ mặc định; bỏ qua giá không được chọn; giữ tòa khác/dịch vụ khác/cờ mặc định; integrity/FK PASS. JavaScript kiểm tra bằng Node với DOM giả: PASS khởi tạo, chuyển lựa chọn, ẩn/hiện, disabled/required, hai khu vực độc lập.
- Chưa xác minh trực quan trên browser/mobile hoặc database của thành viên; chưa thử giá biên đầy đủ, ghi đồng thời, snapshot hóa đơn/lịch giá trong phiên này. Database local mặc định Data/local-dev.sqlite không tồn tại; không sửa database cá nhân. Chỉ thử ghi trên DB giả. Server xác minh cần tắt EventLog provider bằng biến môi trường vì môi trường không có quyền ghi Windows Event Log; không sửa mã app cho vấn đề này.
- Không sửa Tests/tests, không commit/push. File project test có thay đổi sẵn trước task được giữ nguyên. Bàn giao tại thời điểm S2-10:1: validation đầy đủ và hiệu lực kỳ sau chưa thực hiện; trạng thái S2-10:2 mới nhất ở mục phía trên.

## Quy ước README ADMIN local (02/10/2026)

- Khôi phục khối chạy nhanh trong README theo đúng yêu cầu: dừng phiên cũ, restore, updater, cấu hình ADMIN mẫu, tạo ADMIN và chạy web. Giữ nguyên thông tin ADMIN mẫu trong các lần cập nhật sau, trừ khi người dùng yêu cầu đổi; ghi quy ước tại `00-huong-dan.md`.
- Giữ hướng dẫn máy mới khởi tạo DB và máy có DB giữ dữ liệu. ADMIN đã tồn tại thì bỏ qua lệnh tạo; ví dụ không thay đổi mật khẩu tài khoản cũ.
- Xác minh: đọc cấu hình tạo ADMIN và kiểm tra diff tài liệu; không chạy khối lệnh, build/test hoặc thao tác database trong lần sửa README này. Chưa commit/push.

## S2-01 — ngừng dịch vụ phòng theo kỳ hóa đơn (02/10/2026)

- Hoàn thành bốn tiêu chí: ghi `yeu_cau_luc_utc` và `ngung_tu_ky`; PO chốt bỏ giữa tháng vẫn tính hết tháng đó, ngừng từ ngày đầu tháng sau; hóa đơn cũ hiển thị snapshot đã phát hành; kỳ sau không còn dịch vụ đã ngừng, phòng khác không đổi.
- Migration **v8** thêm `ngung_dich_vu_phong`, liên kết lựa chọn phòng/dịch vụ, unique cho lần ngừng đang mở và lịch sử áp dụng lại. Không xóa lựa chọn, giá riêng hoặc hóa đơn; updater tạo backup, chạy transaction và kiểm tra integrity/FK. DB local thực tế vẫn v5, chỉ được kiểm tra read-only; chưa chạy updater trên file local.
- `DichVuPhongService` tập trung quy tắc kỳ Việt Nam, áp dụng cho cả màn hình phòng, form hóa đơn và lúc phát hành để chống gửi form cũ. Hóa đơn chỉ đọc dòng `chi_tiet_hoa_don` snapshot khi xem lại. Có thể phát hành hóa đơn chỉ gồm tiền phòng khi phòng không còn dịch vụ trong kỳ.
- Xác minh thực tế: **40/40 xUnit PASS**, gồm hóa đơn cũ, kỳ hiện tại/kỳ sau, hai phòng độc lập, form HTTP, quyền/CSRF, giá riêng, migration v7→v8 và ranh giới cuối tháng/múi giờ Việt Nam. `python verification/s201_database.py` PASS trên bản sao v5→v8, giữ dữ liệu/hash nguồn, backup, integrity/FK, chạy lặp và khởi tạo mới không ghi đè. Build PASS; cảnh báo ImageSharp và CS8601 cũ vẫn có.
- Demo mới `verification/ServiceRemovalDemo`: chỉ tạo dữ liệu giả trong `data/service-removal-demo/` (ignore), có `DEMO-A`, `DEMO-B`, Gửi xe 100.000đ và hóa đơn kỳ trước; README có cách chạy. Không commit credential/demo DB. Chưa nghiệm thu trực quan bằng browser hoặc SMTP thật.
- File thay đổi chính: RoomServiceRemovalSchema/model/SQL v8, service/controller/view dịch vụ phòng và hóa đơn, test `RoomServicesTests.Invoices.cs`, verification và README/tài liệu CSDL. Không commit/push. File project test có thay đổi sẵn trước task, giữ riêng khi stage.

## S2-01 — đơn giá riêng dịch vụ phòng (01/10/2026)

- Bổ sung `don_gia_rieng` nullable vào từng liên kết phòng/dịch vụ; khi chưa đặt giá riêng dùng giá chung. Chủ nhà nhập giá lúc gán dịch vụ, cập nhật hoặc xóa giá riêng để quay về giá chung.
- Bảng dịch vụ phòng hiển thị giá chung, giá áp dụng, cảnh báo khi giá riêng khác giá chung và tổng dịch vụ cố định dự kiến theo giá áp dụng. Audit ghi nhận thay đổi giá riêng; quyền chủ nhà, CSRF và kiểm tra tòa/phòng tiếp tục ở backend.
- Migration v7: `RoomServicePriceSchema`, `docs/sql/S2-01-gia-rieng-dich-vu-phong.sql`; nâng cấp theo updater có backup/transaction, không đổi các lựa chọn hiện hữu hoặc hóa đơn. Không mở rộng hành vi hóa đơn khi bỏ dịch vụ giữa kỳ.
- Xác minh lần này: `dotnet restore` và `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore` PASS. Theo yêu cầu, không chạy test. Không mở hoặc nâng cấp DB local; cần cập nhật bằng `--update-database` trước khi chạy web dùng schema v7.
- Còn lại: demo trực tiếp kịch bản giá riêng gửi xe và các phòng dùng giá chung; nghiệm thu ảnh hưởng lên hóa đơn giữa kỳ chưa nằm trong phạm vi. Chưa commit/push.

## S2-01 — dịch vụ tòa/phòng (01/10/2026)

- Nhánh `feature/S2-01/building-room-services`. Hoàn thành cờ mặc định cấp tòa, gán khi tạo phòng đơn/hàng loạt, thêm/bỏ độc lập từng phòng, bảng dịch vụ và tổng cố định/tháng theo giá chung hiện hành. Giữ quyền sở hữu/PHONG_TRO, CSRF và audit; không thêm thư viện.
- Thành phần: model/EF/audit `DichVuToaNha`, `DichVuPhong`; `DichVuPhongService`, controller/ViewModel/View tương ứng; tích hợp `DichVuService`, `DichVuController`, `PhongTroController` và Razor hiện có. Tổng dùng decimal, chỉ cộng CO_DINH có giá đang áp dụng; chưa gồm tiền thuê/phí biến đổi/dịch vụ chưa chốt giá.
- Migration **v6**: `RoomServicesSchema`, `docs/sql/S2-01-dich-vu-phong.sql`, updater; kiểm tra chỉ đọc, backup, transaction, unique/FK và chặn dịch vụ khác tòa. Cài schema dịch vụ S1-09 nếu chưa có; không cài hợp đồng/hóa đơn. Backfill danh mục tòa từ giá cũ, năm mã gợi ý bật mặc định; phòng có trước migration chưa có lựa chọn mới.
- Giả định chờ PO: đổi/thêm mặc định **không tự gán phòng cũ**, tập trung tại `GanMacDinhChoPhongMoiAsync` với TODO. Không làm giá riêng hoặc tác động hóa đơn. Mô tả PR sẵn tại [pr-s2-01.md](pr-s2-01.md); chưa tạo PR từ xa, chưa commit/push.
- Máy mới: `--initialize-database` tạo file mới bằng baseline + updater, chỉ seed vai trò/quyền; từ chối ghi đè, không có ADMIN/mật khẩu cố định. Máy có DB: dừng app, `--update-database`, `--check-database`. Sau đó chạy web, Chủ nhà vào `/DichVu`, chọn **Dịch vụ** trên danh sách phòng. Chi tiết trong README và cap-nhat-csdl.md.
- Đã `git rm --cached` đúng `QL_PhongTro/Data/local-dev.sqlite`, giữ file trên máy; mở rộng ignore database/sidecar/backup/data local. **Đồng đội cần sao lưu DB ra ngoài repository trước lần pull nhận thay đổi bỏ theo dõi**, vì Git có thể xóa file từng được theo dõi.
- Xác minh thực tế: build thành công; **32/32 xUnit PASS** (7 test S2-01: mặc định, cô lập phòng, tổng/giá chung, quyền, CSRF, audit rollback, migration và HTTP tạo đơn/hàng loạt/hiển thị riêng). Fixture Auth/Permission nay tạo DB tạm từ script, không dùng DB cá nhân và không đặt DatabasePath toàn process. `python verification/s201_database.py` PASS: bản sao local v5 → v6, giữ toàn bộ dòng cũ, backup, integrity/FK, chạy lại không đổi, khởi tạo mới/từ chối ghi đè, hash nguồn không đổi.
- DB local thực tế vẫn **v5**, chưa nâng cấp; chỉ kiểm tra nguồn bằng kết nối read-only. Chưa nghiệm thu đồ họa trình duyệt, SMTP hoặc fixture S109 lịch sử; các công cụ demo/schema cũ cần rà soát trước khi dùng với v6. Build còn cảnh báo ImageSharp có sẵn; full build trước đó có CS8601 cũ trong AuthController.
- Lint thực tế: `dotnet format --verify-no-changes` PASS cho C# mới (app và RoomServicesTests); `dotnet format style`/`analyzers --severity warn` PASS trên các file C# ứng dụng thay đổi. `git diff --check` phần task PASS; toàn working tree còn dòng trống cuối project test có sẵn trước task. Đã kiểm tra status/staged và ignore: staged chỉ có việc bỏ theo dõi DB, không có database/backup/bí mật được thêm mới.
- Bước tiếp: PO chốt chính sách phòng cũ; review mô tả PR, thử nâng cấp trên bản sao máy đồng đội và nghiệm thu UI. File `Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj` có thay đổi sẵn trước task, giữ nguyên để chủ máy quyết định stage.

Các mục dưới đây là bàn giao trước S2-01; thông tin v6/quy trình database ở trên và README thay thế ghi chú cũ tương ứng.

Cập nhật: 29/09/2026. Tổng hợp tài liệu S1-03, S1-04, S1-05, S1-09 (chức năng/demo), S1-10 và tiến độ cũ. Kết quả kiểm thử từ các phiên trước được ghi riêng, không phải lần chạy mới khi biên tập tài liệu.

## Trạng thái hiện tại

### Bàn giao nhánh giới hạn sức chứa (02/10/2026)

- Theo yêu cầu push `feature/S2-06/validate-room-occupancy-limit`, commit AC sức chứa rồi hợp nhất nhánh đích `ca9c469`, giữ cả S2-06 và dịch vụ/phòng S2-01. Giải quyết xung đột DbContext, đăng ký service/CLI, initializer và tài liệu; giữ cấu hình ADMIN riêng thay vì mật khẩu cố định. Không force-push, không thay database local. Project test có thay đổi sẵn tiếp tục giữ local, không stage.
- Sau merge: build Debug PASS; toàn xUnit **44/44 PASS**, 0 lỗi/0 bỏ qua (`data/test-results/occupancy-merge-regression.trx`); HTTP S2-06 PASS trên DB giả mới `data/S2-06/20261002-024157-1f4175`, server tự dừng. Fixture Auth/Permission mới tự tạo DB tạm, không cần DB mặc định. DB mặc định vẫn chưa tồn tại. Chưa chạy lại UI/mobile hoặc nâng cấp DB nguồn trong lượt merge.
- Trạng thái schema sau merge: nền **v8** theo nhánh đích, rental_request_schema=1 cài riêng. Ghi chú v5 bên dưới thuộc lượt trước merge; máy có DB giữ dữ liệu dùng updater có backup theo README/cap-nhat-csdl.md khi được phép cập nhật. Không chạy updater trên DB thật trong task push. AC sức chứa không bổ sung bước schema.

### S2-06 — giới hạn số người dự kiến ở (02/10/2026)

- Hoàn tất AC sức chứa: service đọc `so_nguoi_toi_da` của phòng gắn với tin public trong transaction khóa ghi, từ chối trước khi tạo profile/cấp mã/lưu yêu cầu. Controller gắn lỗi vào `Form.SoNguoiDuKien`; form có max theo phòng, hướng dẫn và thông báo tiếng Việt “Phòng chỉ cho phép tối đa N người.”. Dữ liệu sức chứa gửi từ client không được dùng. Giữ kiểm tra số nguyên dương; chưa kiểm soát yêu cầu đang mở trùng.
- File: `Services/YeuCauThueService.cs`, `Controllers/TinDangController.cs`, `Views/TinDang/ChiTiet.cshtml`, `verification/s206_http.py`, README và tiến độ. Không đổi schema/quy trình DB; không có DatabasePath override, DB mặc định vẫn chưa tồn tại. Giữ thay đổi project test có sẵn; không commit/push.
- Xác minh thực tế: build Debug `--no-restore -o data/S2-06/runtime` PASS, 0 lỗi (còn ImageSharp/CS8601 cũ). HTTP PASS trên DB giả mới `data/S2-06/20261002-022828-906055`: 1/2/3/4 được lưu cho phòng tối đa 4; 5 bị từ chối cho XEM_PHONG và THUE_NGAY, lỗi đúng trường/giữ giá trị nhập. Thử giới hạn phòng 2 thì 3 bị từ chối với thông báo đúng 2. Sau các POST vượt giới hạn, yêu cầu/profile/counter/audit đều không đổi. Hồi quy ngày, CSRF/quyền/sở hữu, mã/đồng thời, audit/rollback/hết mã, tin public và integrity/FK PASS. Server kiểm thử đã dừng.
- Lượt sandbox đầu lỗi Event Log/Data Protection, đã dừng đúng tiến trình thử và chạy lại ngoài sandbox thành công. Không có DB nguồn nên không chạy nhánh thử nâng cấp bản sao nguồn; chưa nghiệm thu tương tác UI/mobile hoặc chạy lại toàn bộ xUnit.
- Kiểm thử lại theo yêu cầu người dùng: xUnit PASS **29/29**, 0 lỗi/0 bỏ qua; kết quả `data/test-results/capacity-regression.trx` (auth, quyền, tòa/phòng, hồ sơ và ngày). Fixture cũ đọc đường dẫn DB mặc định: chỉ khởi tạo nền rỗng khi file chưa tồn tại bằng CLI, các test chạy trên bản sao tạm, sau đó xóa đúng file nền vừa tạo; DB mặc định trước/sau đều không tồn tại. Không dùng dữ liệu cá nhân. HTTP S2-06 chạy lại PASS trên fixture mới `data/S2-06/20261002-023112-d66378`, cổng 5267, kiểm tra đầy đủ sức chứa và hồi quy như trên; nguồn kiểm thử được đặt tới đường dẫn chưa tồn tại để không đọc fixture nền của xUnit đang chạy.
- UI Browser thực tế PASS: đăng nhập khách giả, nhập 5 bị chặn tại ô số người với thông báo tối đa 4; sửa thành 4 xóa lỗi và gửi thành công, mã `YC-202610-0010`, trang xác nhận ghi số người 4. Ảnh `data/S2-06/capacity-validation-ui.png` và `capacity-success-ui.png`. Kết quả này thay thế giới hạn “chưa nghiệm thu UI/chạy toàn xUnit” ở lượt triển khai; chưa kiểm tra mobile hoặc SMTP thật. Server thử đã dừng; không commit/push. Lần này chỉ cập nhật tiến độ, không sửa mã ứng dụng.
- Chạy/test: build rồi `python verification/s206_http.py`; chạy demo qua `data/S2-06/latest.txt` theo README, phòng S206-101 tối đa 4 người. Không cần updater cho task này. Giả định giới hạn áp dụng cả hai loại yêu cầu, gồm người gửi trong tổng số dự kiến ở. Bước tiếp: nghiệm thu UI/mobile; kiểm soát yêu cầu mở trùng là task riêng.

### Xử lý xung đột PR #24 (02/10/2026)

- Nhánh nhận PR là main, đã revert tại 6d08d0f về trạng thái chỉ còn tài liệu. Theo yêu cầu xử lý xung đột và chỉ push feature/S2-06/validate-desired-date-range, merge lịch sử main vào feature bằng chiến lược ours, giữ toàn bộ cây mã/tài liệu feature đã kiểm thử; không đưa việc xóa ứng dụng vào feature.
- PR so với main sẽ khôi phục ứng dụng và các chức năng hiện có, không chỉ riêng giới hạn ngày. Không thay schema/DB, không đưa DB/backup/credential local vào commit. Project test khác do Tests/tests vẫn giữ local.
- Xác minh: cây mã ứng dụng/script/test trong index không đổi so với commit 0a3d675; build qua dotnet test và 4/4 DesiredDateTests PASS. Kết quả 29/29 và HTTP S2-06 PASS là lượt kiểm thử trước trên cùng mã; không chạy lại full suite trong lượt xử lý merge.
- Chỉ push feature; chưa merge PR vào main/dev. Người duyệt cần lưu ý phạm vi khôi phục mã trước khi merge PR.


### S2-06 — gửi yêu cầu, mã xác nhận và giới hạn ngày (02/10/2026)

- Hoàn tất AC giới hạn ngày: khoảng gồm cả hôm nay và ngày +60, theo Asia/Ho_Chi_Minh qua ITimeProvider. Controller trả lỗi tại `Form.NgayMongMuon`; service kiểm tra lại trước transaction/ghi dữ liệu. Form có min/max, hướng dẫn khoảng ngày và lỗi tiếng Việt tại trường ngày, kể cả khi nhập tay. Giữ loại yêu cầu, CSRF, quyền, mã theo tháng, audit/rollback hiện có; chưa kiểm tra sức chứa hoặc yêu cầu mở trùng.
- File thay đổi: `Services/YeuCauThueService.cs`, `Controllers/TinDangController.cs`, `ViewModels/GuiYeuCauViewModel.cs`, `Views/TinDang/ChiTiet.cshtml`; cập nhật `verification/s206_http.py`, thêm `Tests/QL_PhongTro.Tests/DesiredDateTests.cs`, README và ignore `/data/test-results/`. Giữ thay đổi project test có sẵn do Tests/tests trùng trên Windows; không commit/push.
- Kiểm tra cấu hình thực tế: appsettings và launch profile không đặt DatabasePath; không có override môi trường. Mặc định `QL_PhongTro/Data/local-dev.sqlite` hiện không tồn tại. Không tạo DB đang sử dụng, không đổi schema/quy trình DB, không sửa dữ liệu cũ. Schema nền v5 và rental_request_schema=1 giữ nguyên. Đã bỏ theo dõi DB trong task trước; đồng đội vẫn cần sao lưu ngoài repo trước pull thay đổi bỏ theo dõi.
- Xác minh thực tế: build Debug `data/S2-06/date-runtime` PASS, 0 lỗi; còn cảnh báo ImageSharp/CS8601 cũ. HTTP bản cuối cổng 5267 PASS trên fixture mới `data/S2-06/20261002-011420-6c62e3`: hôm nay/+60 được lưu, hôm qua/+61 bị từ chối cho cả hai loại, lỗi đúng trường/giữ giá trị nhập; dữ liệu yêu cầu/profile/counter/audit không đổi sau ngày sai. Toàn bộ hồi quy S2-06 cũng PASS (CSRF/role/ownership, mã/concurrency, audit/rollback/hết mã, tin public, integrity/FK). Nguồn local không tồn tại nên nhánh nâng cấp bản sao nguồn trong script không chạy; không khẳng định đã test nâng cấp DB cũ lần này. Chạy ngoài sandbox do Event Log/Data Protection.
- Unit test mới: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~DesiredDateTests` PASS 4/4, kiểm tra biên chuyển ngày Việt Nam lúc 17:00 UTC và service từ chối trước khi truy cập DB. Lượt kiểm thử lại theo yêu cầu (02/10): toàn bộ xUnit PASS 29/29 (25 auth/quyền/tòa-phòng/hồ sơ + 4 ngày), kết quả `data/test-results/regression-final.trx`. Dùng DB trống tạo tạm bằng CLI tại đường dẫn fixture bộ test cũ và xóa khi xong; DB mặc định trước/sau không tồn tại. Build runtime PASS, HTTP S2-06 chạy lại cổng 5267 PASS trên fixture `data/S2-06/20261002-011757-219438`, server tự dừng. Không chạy lại UI lần này; dùng kết quả UI thực tế trong task triển khai bên dưới.
- UI Browser thực tế: ngày 02/10/2026 và 01/12/2026 hợp lệ; 01/10 và 02/12 không hợp lệ, có lỗi tiếng Việt ngay dưới ô ngày; bấm gửi với ngày +61 bị chặn tại form. Ảnh `data/S2-06/date-validation-ui.png`. HTTP đã xác minh lưu thành công các biên; UI lần này chỉ kiểm tra chọn ngày/chặn gửi, chưa kiểm tra mobile.
- Chạy/test: `dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug -o data/S2-06/runtime`, `python verification/s206_http.py`. Đọc access.json/latest.txt và khởi động demo theo README; không chia sẻ credential hoặc DB fake. Không cần updater cho thay đổi giới hạn ngày. Bộ HTTP tự dừng server.
- Giả định: “hôm nay” là ngày Việt Nam, 60 ngày lịch và gồm cả hai đầu. Bước tiếp: nghiệm thu mobile, tích hợp tin thật; sức chứa/trùng yêu cầu là task riêng. Lỗi đăng nhập đồng thời SQLite locked đã được ghi nhận trước đây, ngoài phạm vi này.
### Nhánh đăng ký, đăng nhập, phân quyền (30/09/2026)

- Đã tạo nhánh `Feature/fix/dang-ky-dang-nhap-phan-quyen` từ `dev`. Bổ sung schema v4: `email_confirmed`, `is_deleted`, `email_confirmation`; cập nhật có backup qua `--update-database`, không tự chạy khi web khởi động.
- Đăng ký khách thuê tạo tài khoản chờ xác nhận, gửi mã 6 số qua `IRegistrationEmailSender`, mã hết hạn sau 15 phút; form `/Account/ConfirmEmail` có Gửi lại mã. Mã hết hạn khi người dùng gửi form sẽ hủy mềm tài khoản chờ xác nhận; đăng nhập bị chặn trước xác nhận. SMTP thật dùng cấu hình `PasswordReset__*`; chưa có SMTP thật trong môi trường nên chưa xác minh thư đến inbox.
- Thêm nút hiện/ẩn mật khẩu ở đăng nhập/đổi mật khẩu, xác nhận khi đăng xuất, hiển thị vai trò cạnh tên tài khoản và thông báo hết phiên khi polling phát hiện cookie không còn. Cookie hiện hết hạn 30 phút theo cấu hình app; request backend vẫn là lớp bảo vệ chính.
- ADMIN đã lọc/thấy tài khoản KHACH_THUE; thêm xóa mềm tài khoản không phải ADMIN. Xóa QUAN_LY hủy kích hoạt, thu hồi phiên và chuyển các tòa đang giao về `Chưa phân công`; dữ liệu lịch sử vẫn giữ.
- Build Debug PASS, còn cảnh báo ImageSharp license và CS8601 cũ. Kiểm tra updater v4 trên bản sao bị Windows Application Control chặn khi chạy DLL trong môi trường này; chưa ghi DB gốc. Cần chạy `--update-database`/`--check-database` trên bản sao bằng terminal Windows bình thường trước khi dùng nhánh.

### Cập nhật giao diện dịch vụ/hóa đơn (30/09/2026)

- Danh sách dịch vụ có nút Sửa đơn giá và Xóa (xác nhận trước khi gửi), dùng Manage/Delete hiện có; giữ CSRF, quyền ghi và chặn xóa dịch vụ đã tham chiếu. Phạm vi sửa là đơn giá/lịch sử, chưa thêm sửa tên/cách tính.
- Hóa đơn gần đây thêm mã phòng/tên tòa qua ViewModel và truy vấn join, giữ lọc theo tòa/chủ nhà và 30 hóa đơn mới nhất; không thay schema/snapshot hóa đơn.
- File: Views/DichVu/Index.cshtml, Views/HoaDonDichVu/Index.cshtml, ViewModels/HoaDonDichVuViewModels.cs, Controllers/HoaDonDichVuController.cs. Build Debug PASS (ImageSharp và CS8601 cũ); HTTP đăng nhập Chủ nhà, form sửa/xóa có CSRF, hóa đơn có đúng DEMO-101/tên tòa PASS; diff phần mã sạch. Chưa thử xác nhận JavaScript bằng trình duyệt hoặc chạy lại thao tác ghi sửa/xóa.
- Demo cùng DB/tài khoản chạy tại localhost:5250, PID mới 20944; tải lại trang để dùng. Không đổi DB gốc, không commit/push. Bước tiếp: nghiệm thu giao diện và thao tác trên dịch vụ thử chưa sử dụng nếu cần.

- ASP.NET Core MVC/C#, .NET 10, EF Core SQLite; HTML/CSS trong Razor Views. MVC dùng cookie, API xác thực có JWT; mật khẩu BCrypt.
- Đã có đăng ký/đăng nhập, tài khoản quản lý, phân quyền, mật khẩu, hồ sơ, tòa/phòng, dịch vụ/hóa đơn tối thiểu và nhật ký. Trang danh mục module chưa có nghiệp vụ không được coi là đã hoàn thành backlog.
- Web yêu cầu SQLite đã tồn tại; máy mới khởi tạo rõ ràng bằng `--initialize-database`, không tự tạo khi startup. Schema nền **v5**, giữ ngoại lệ tự nâng v4 → v5 có backup; S2-06 là module tùy chọn version 1 cài qua CLI. Hợp đồng/dịch vụ/hóa đơn cũng là schema tùy chọn; bảng đã có phải đủ cột tương ứng.
- Staging/Docker, reset-seed và dashboard thử nghiệm đã gỡ. Các ghi chú staging cũ không còn áp dụng.
- Theo bàn giao local ngày 29/09: 6 tài khoản, 1 hồ sơ, 1 tòa, 4 phòng, 8 dòng nhật ký; chưa cài S1-09. Backup trước demo: `data/backups/local-dev.before-small-demo-20260929-074854.sqlite`. Đây là dữ liệu riêng trên máy, không bảo đảm có trên máy đồng đội; lần biên tập này không mở DB để kiểm tra lại.
- DB local đã được bỏ theo dõi bằng `git rm --cached`, giữ file trên máy. Đồng đội phải sao lưu DB ra ngoài repository trước lần pull nhận thay đổi này vì Git có thể xóa file đã theo dõi. Clone mới dùng quy trình khởi tạo rõ ràng trong README; không chia sẻ DB local.

## Chạy và dữ liệu

Hướng dẫn chung: [README](../README.md), [cập nhật SQLite](cap-nhat-csdl.md). Dừng đúng phiên server bằng Ctrl+C trước khi build, sao chép hoặc nâng cấp DB; không chạy updater/web đồng thời trên cùng file.

- Mặc định `QL_PhongTro/Data/local-dev.sqlite`; biến môi trường `DatabasePath` chọn file khác. Không chép đè DB đồng đội, không dùng EnsureDeleted/EnsureCreated để nâng cấp.
- `--check-database` chỉ kiểm tra; `--update-database` sao lưu `*.before-update-<id>.bak` và ghi `app_schema_version`. Chỉ nâng cấp DB đang dùng khi được yêu cầu. v1: hạ tầng nền; v2: must_change_password và unique email chuẩn hóa/điện thoại; v3: nhật ký, snapshot tên, trigger bảo vệ. Dữ liệu tài khoản trùng làm v2 dừng, không tự gộp/xóa.
- Bước v3 có transaction, kiểm tra schema/integrity/FK; các bước v1/v2 cũ không phải một transaction chung cho toàn updater. Khi lỗi cần xem backup và trạng thái thực tế; không tự phục hồi đè dữ liệu. Check không chứng minh mọi constraint/nghiệp vụ đều đúng.
- ADMIN local: cấu hình `LocalAdmin__Email`, `LocalAdmin__Password`, `LocalAdmin__Phone` rồi chạy `--create-local-admin` trong Development trên đúng DatabasePath đã cập nhật. Lệnh có backup, từ chối email/điện thoại trùng. Không đưa credential, DB, backup hoặc email local vào Git.

Ví dụ chạy trên **bản sao mới** từ gốc repo, sau khi dừng app dùng DB nguồn:

```powershell
New-Item -ItemType Directory -Force data | Out-Null
if (Test-Path data/review.sqlite) { throw 'Chọn tên bản sao mới, không ghi đè.' }
Copy-Item -LiteralPath QL_PhongTro/Data/local-dev.sqlite -Destination data/review.sqlite
$env:DatabasePath = (Resolve-Path data/review.sqlite).Path
dotnet restore QL_PhongTro/QL_PhongTro.csproj
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore
dotnet run --project QL_PhongTro --no-build -- --update-database
dotnet run --project QL_PhongTro --no-build -- --check-database
$env:PasswordReset__PublicBaseUrl = 'http://localhost:5247'
$env:PasswordReset__PickupDirectory = Join-Path $env:TEMP 'qlphongtro-mail-preview'
dotnet run --project QL_PhongTro --no-build --launch-profile http
```

Mở `http://localhost:5247/Account/Login`. Sau khi dừng demo, xóa biến DatabasePath khỏi terminal nếu muốn về DB mặc định. Biến môi trường chỉ có hiệu lực trong terminal đã đặt.

## Tài khoản, phân quyền và mật khẩu (S1-01–S1-05)

- Đăng ký chỉ tạo KHACH_THUE. Đăng nhập bằng email/điện thoại; xác thực có khóa tạm khi sai mật khẩu, JWT access 30 phút và refresh 7 ngày. Quyền và trạng thái tài khoản kiểm tra ở backend.
- `/ManagedAccounts`: chỉ ADMIN tạo/khóa/mở khóa Chủ nhà và Quản lý; không tự khóa hoặc khóa ADMIN/Khách thuê tại chức năng này. Danh sách mọi vai trò, lọc vai trò/trạng thái, 20 dòng/trang.
- Mật khẩu tạm 16 ký tự ngẫu nhiên đủ bốn nhóm, gửi email và buộc đổi trước khi dùng chức năng khác. API không cấp token khi còn chờ đổi. Gửi lỗi vẫn giữ tài khoản chờ đổi; gửi lại thay mật khẩu tạm cũ. SMTP và DB không cùng transaction.
- Khóa/đổi mật khẩu thu hồi cookie/JWT/refresh cũ. Trang polling 20 giây; tab treo/mất mạng không bảo đảm tự chuyển đúng hạn, nhưng request tiếp theo vẫn bị chặn.
- `/Account/ChangePassword`: mọi vai trò đã đăng nhập; kiểm tra mật khẩu hiện tại, mới khác cũ, ít nhất 8 ký tự có chữ/số, xác nhận khớp và CSRF. Thành công phải đăng nhập lại. Ghi chú cũ “chỉ khách thuê, không thu hồi phiên” đã hết hiệu lực.
- ForgotPassword/ResetPassword hiện chỉ cho KHACH_THUE đang hoạt động. Token 256 bit, DB lưu SHA-256, hạn 30 phút/dùng một lần; tối đa 3 yêu cầu/email/giờ, lần 4 trả 429. Email không tồn tại trả thông báo trung tính; gửi lỗi vẫn tính lượt và xóa token gửi lỗi. Reset vô hiệu hóa token/phiên/refresh và bỏ khóa đăng nhập tạm trong transaction.
- `/Permissions`, `/api/permissions`: ADMIN/CHU_NHA xem bốn vai trò, KHACH_THUE chỉ vai trò mình. Quyền đọc từ app_role/app_module/role_permission; seed tại `Data/permissions.seed.json` gồm 4 vai trò/9 module/36 cặp, không cấp lại quyền đã thu hồi khi chạy lại.
- Quyết định PO khác Excel: **QUAN_LY không truy cập TAI_CHINH**. Ma trận còn lại theo yêu cầu và seed. Thiếu cặp quyền thì từ chối; OwnDataOnly không thay kiểm tra sở hữu ở nghiệp vụ mới.
- MVC chưa đăng nhập chuyển Login; MVC trái quyền trả trực tiếp 403; API của cơ chế quyền trả JSON 401/403. Cookie/Data Protection tách theo đường dẫn DB. Ghi chú cũ “không dùng JWT” hoặc “trái quyền luôn redirect” không còn đúng.
- Thành phần: `Controllers/AccountController*.cs`, AuthController, ManagedAccountsController, PermissionsController; `Authorization/*`; Services AuthService, TokenService, SessionVersionStore, PasswordResetService, PasswordEmailSender và ViewModels/Razor tương ứng.

Email local là file .eml trong pickup, không gửi inbox. SMTP cần các khóa `PasswordReset__Host`, Port, EnableSsl, From, Username, Password, PublicBaseUrl; đặt `PasswordReset__PickupDirectory = ' '` để ghi đè pickup Development. Production yêu cầu URL HTTPS; SMTP dùng STARTTLS (thường 587). Không commit bí mật; máy chủ nhận thư chưa chứng minh thư tới inbox.

Demo sau build: `python verification/prepare_s103_demo.py data/s103-demo-new.sqlite`. Cần DB nguồn tồn tại; script tạo bản sao mới, in credential ngẫu nhiên, thêm 45 Quản lý hoạt động + 8 khóa, từ chối ghi đè. Đặt DatabasePath bản sao rồi chạy web. Nghiệm thu: ADMIN tạo/trùng/validation → đọc .eml → đăng nhập/ép đổi → gửi lại → khóa/mở khóa ở phiên thứ hai → lọc/phân trang và thử trái quyền.

## Hồ sơ và tòa/phòng (S1-06–S1-08)

- `/HoSo`: tạo/sửa hồ sơ mình; căn cước 9/12 chữ số, để trống khi sửa giữ giá trị cũ. `/HoSo/Xem?id=<id>`: tra cứu theo quyền. Server che chỉ còn 4 số cuối; ADMIN/chủ nhà của phòng đang thuê mới thấy đầy đủ. Query/form không nâng quyền.
- Quyền chủ nhà dựa hợp đồng hiệu lực, kỳ thuê, người đứng tên/người ở ghép và ngày ở thực tế tại Việt Nam; thiếu schema/quan hệ thì không mở dữ liệu nhạy cảm. Người không liên quan chỉ nhận thông tin giới hạn.
- Ảnh JPG/PNG tối đa 5MiB mỗi ảnh, resize rộng tối đa 1600px, giữ tỷ lệ; giới hạn 50 megapixel, frame đầu, request tổng 16MiB. Cho bổ sung từng mặt để tương thích hồ sơ cũ. Lưu ngoài wwwroot; endpoint kiểm tra quyền/no-store; chủ hồ sơ vẫn xem ảnh mình.
- IdentityImagePath mặc định `QL_PhongTro/App_Data/identity-images`; sao lưu DB và ảnh. Giấy tờ/ảnh chưa mã hóa trên đĩa; chưa xử lý hoàn chỉnh tệp mồ côi sau crash.
- Tòa/phòng: tạo tòa, gán quản lý, sửa/ngừng/xóa theo ràng buộc; tạo phòng đơn/hàng loạt, lọc trạng thái, mã không trùng trong tòa, giá tối thiểu 500.000đ. Quy tắc mã tầng + 2 chữ số, giới hạn 1..99 còn cần PO xác nhận.
- Thành phần: HoSoController, PhongTroController, Services HoSoAccess/GiayToImageStore, Models/ViewModels/Views tương ứng và SQL hồ sơ/quan hệ thuê trong `docs/sql`. Schema quan hệ thuê để kiểm tra quyền không đồng nghĩa đã có CRUD hợp đồng đầy đủ.

## Dịch vụ và hóa đơn tối thiểu (S1-09)

- `/DichVu?toaNhaId=<id>`: dịch vụ theo tòa của chủ nhà, ba cách tính (chỉ số/người/cố định), validation/CSRF. Khởi tạo DIEN, NUOC, RAC, GUI_XE, INTERNET bằng POST; marker/transaction/unique chống trùng; không tạo lại dịch vụ đã chủ động xóa.
- Giá chưa chốt hiển thị “Chưa thiết lập”, không được chọn lập hóa đơn. Điện/nước mặc định >0, dịch vụ khác cho phép 0. `DichVuMacDinh:DanhSach` cấu hình Ma/Ten/CachTinh/DonVi/DonGia chỉ áp dụng khi khởi tạo mới.
- `/DichVu/Manage`: giá đầu chỉ sửa trực tiếp khi chưa dùng/chưa nhiều phiên bản. Giá mới đóng khoảng cũ trước ngày hiệu lực, trùng ngày bị từ chối. Ngừng/kích hoạt từ kỳ sau và sau phiên bản mới nhất; cách triển khai hiện tại không hồi tố/chèn trước phiên bản mới nhất.
- Chặn xóa nếu có cấu hình phòng riêng, tham chiếu hợp đồng/hóa đơn hoặc snapshot không rõ cấu trúc. Đọc hop_dong_dich_vu, dòng hóa đơn và ky_hop_dong.thong_tin_chot; JSON nhận diện: `{"dich_vu":[{"dich_vu_id":123}]}`.
- `/HoaDonDichVu`: chỉ hợp đồng hiệu lực thuê **trọn tháng với một giá phòng**; từ chối kỳ lẻ, gia hạn/trả phòng giữa tháng và cấu hình phòng riêng. Tiền phòng lấy từ kỳ thuê; số người/chỉ số do chủ nhà nhập, chưa tích hợp chỉ số/người ở ghép. Cuối ≥ đầu, không âm, tối đa 3 chữ số thập phân.
- Tính decimal, làm tròn từng dòng tới đồng bằng AwayFromZero, kiểm tra tràn số. Snapshot tên/cách tính/đơn vị/giá/chỉ số/số lượng/thành tiền; unique chống lặp hợp đồng/tháng, trigger khóa hóa đơn/dòng đã phát hành; hạn trả 7 ngày từ ngày phát hành tại Việt Nam.
- `HoaDonDichVuService.GanVaoHopDongAsync` tích hợp snapshot hợp đồng. Chưa làm đầy đủ nháp/chỉnh sửa/hủy/thay thế, thanh toán, email, tiền phòng theo ngày hoặc CRUD hợp đồng.
- PO đã chốt trùng ngày từ chối, giữ snapshot, ngừng/kích hoạt kỳ sau. Giá thương mại và đơn vị/cách tính còn chờ: đề xuất điện kWh, nước m³ theo chỉ số; rác theo người; gửi xe/internet theo phòng. Không tự dùng giá demo cho DB thật.
- Thành phần: Models/ViewModels/Controllers/Views DichVu và HoaDonDichVu; Services DichVuService*, DichVuThamChieu, HoaDonDichVuService; Data/DichVuSchemaInitializer; SQL `S1-09-dich-vu.sql`, `S1-09-hoa-don.sql`.

Chỉ cài module trên DB thử khi chưa được yêu cầu cài DB thật. Lệnh demo: `dotnet run --project verification/S109/S109.csproj -- . --prepare-demo`; tạo fixture riêng và ghi credential vào demo-access.txt được ignore. Công cụ cũ chưa được chạy lại với v3 trong lần tổng hợp; kiểm tra tương thích trước khi dùng. Các lệnh `--initialize-services`, `--initialize-service-invoices` cần schema tiên quyết đã kiểm tra, có backup; không tự tạo bảng tiên quyết. Không chạy lại SQL tổng hợp trên schema AC1 cũ.

Demo lịch sử 28/09: `data/S1-09-verification/20260928102752-c3ffa2/test.sqlite` (nếu còn trên máy), phòng DEMO-101 giá 2.000.000đ; điện 3.000 → 3.500 từ 01/10, nước 15.000, rác 20.000/người, gửi xe 50.000/phòng, internet 100.000/phòng. Tháng 9 chỉ chọn điện 100→110 cho tổng 2.030.000đ; tháng 10 dự kiến 2.035.000đ, hóa đơn cũ không đổi. Điện chặn xóa do hóa đơn, nước do hợp đồng; dịch vụ chưa dùng xóa được; dịch vụ trạng thái ngừng 01/10, bật lại 01/11. Đây là số liệu/kịch bản thử, không khẳng định demo đang chạy hoặc tháng 10 chưa có hóa đơn.

## Nhật ký hoạt động (S1-10)

- `/NhatKy` chỉ ADMIN, xác minh role/trạng thái từ DB; 50 dòng/trang, mới nhất trước, lọc kết hợp ngày/người/loại, xem JSON trước/sau. Lưu UTC, hiển thị UTC+7; lọc ngày Việt Nam theo [đầu ngày từ, đầu ngày sau ngày đến), năm 1900–9998.
- Audit cùng transaction cho tài khoản tạo/khóa/mở khóa/gửi lại mật khẩu tạm/tự đăng ký; tòa/phòng; dịch vụ/giá/trạng thái; hóa đơn/dòng/phát hành và gán dịch vụ hợp đồng qua SaveChanges. Chưa có CRUD hợp đồng, chỉ số độc lập, thanh toán nên chưa có luồng audit nghiệp vụ tương ứng.
- `Data/AppDbContext.Audit.cs` dùng allowlist, snapshot tên/role truy vấn từ DB. Không ghi mật khẩu/hash/token, email/điện thoại tài khoản, hồ sơ/giấy tờ/ảnh hoặc ghi chú tự do. Sửa chỉ ghi trường đổi, no-op không ghi; gửi lại mật khẩu có hành động riêng dù JSON rỗng, không chứng minh email đã đến.
- SaveChanges dùng transaction hoặc savepoint khi có transaction ngoài; lỗi audit rollback nghiệp vụ. EF chặn thêm tay/sửa/xóa entity nhật ký; trigger audit_no_update/audit_no_delete chặn sửa/xóa SQL. Không có endpoint ghi nhật ký. Người quản trị file SQLite vẫn có thể bỏ trigger.
- v3 giữ nhật ký cũ; thiếu snapshot tên thì NULL, không suy đoán từ tên hiện tại. ghi_chu giữ tương thích schema, không ghi text tự do từ request.
- Không audit hạ tầng login/refresh/reset/đổi mật khẩu thường, quyền module, hồ sơ nhạy cảm hoặc CLI. Bulk SQL/ExecuteUpdate của nghiệp vụ mới cần tích hợp riêng; ChangeTracker không tự bắt. Nhật ký theo đối tượng/mỗi SaveChanges, không gộp toàn request.
- Thành phần: Models/NhatKyHoatDong, Data/AppDbContext.Audit, AuditSchema, DatabaseUpdates; NhatKyController, NhatKyViewModel, Views/NhatKy/Index; tích hợp Account/ManagedAccounts và layout.

Nghiệm thu: ADMIN/non-ADMIN/anonymous; tạo/khóa/mở khóa/gửi lại tài khoản; tạo/sửa tòa/phòng; trên fixture S1-09 thử đổi giá/phát hành. Kiểm tra snapshot không chứa bí mật, lọc biên ngày/kết hợp/phân trang; no-op/form lỗi không thêm audit. Thử rollback/SQL bất biến chỉ trên DB kiểm thử.

## Xác minh và giới hạn

Kết quả ghi nhận trong các phiên 27–29/09/2026, chưa chạy lại khi chỉnh tài liệu:

| Phạm vi | Kết quả bàn giao | Công cụ hiện có |
| --- | --- | --- |
| S1-03 | HTTP PASS tạo/trùng/validation/CSRF/email pickup/lỗi gửi/ép đổi/khóa/cookie-JWT-refresh/lọc/phân trang; hash DB gốc không đổi | `verification/s103_http.py` |
| S1-04 | 7 test quyền và HTTP bốn vai trò từng đạt; bản merge sau đó từng lỗi fixture thiếu toa_nha; chưa xác nhận lại toàn suite hiện tại | `Tests/QL_PhongTro.Tests/PermissionTests.cs` |
| S1-05 | Bộ riêng từng PASS đổi/reset/hết hạn/giới hạn/đồng thời/thu hồi phiên trên bản sao và email pickup | `verification/S105` được nhắc trong tài liệu cũ nhưng **không có trong checkout hiện tại** |
| S1-06 | HTTP hồ sơ/ảnh/quyền PASS; từng xem che/đầy đủ trong trình duyệt | `Tests/s1_06_http.py`, `s1_06_images.py`, `s1_06_permissions.py` |
| S1-09 | Build và các luồng HTTP demo đạt; console suite từng bị Windows Application Control chặn, chưa nghiệm thu đầy đủ | `verification/S109/` |
| Updater/S1-10 | HTTP, schema lỗi/lặp/bảo toàn, console EF/savepoint/rollback và hồi quy S1-03 PASS; integrity/FK đạt, hash DB nguồn không đổi | `verification/database_updates.py`, `s110_schema.py`, `s110_http.py`, `verification/S110/` |

- Sau build, các lệnh kiểm chứng: `python verification/database_updates.py`, `python verification/s103_http.py`, `python verification/s110_schema.py`, `python verification/s110_http.py`. Đọc fixture trước khi chạy, chỉ dùng bản sao. Sau HTTP S1-10 có thể build S110.csproj rồi chạy DLL với đường dẫn gốc repo và audit.sqlite do HTTP suite tạo.
- Python S1-06 nhận URL app đang chạy và DB thử; script ảnh cần Pillow và thư mục ảnh riêng. Các giả định đăng ký/redirect có từ trước merge, cần cập nhật fixture theo auth/CSRF hiện tại trước khi chạy lại.
- Không chạy toàn bộ AuthTests vào DB mặc định: fixture cũ có EnsureDeleted/EnsureCreated. Chưa xử lý va chạm Git Tests/tests; giữ thay đổi local project test.
- Build Debug từng đạt; còn ImageSharp license, CS8601 ở AuthController và NU1900 khi không truy cập NuGet. Release cần license ImageSharp hợp lệ; NU1900 không chứng minh đã kiểm tra lỗ hổng.
- Chưa xác minh SMTP thật, UI đồ họa/responsive S1-03/S1-09/S1-10 hoặc tải đồng thời/crash toàn hệ thống. Test đồng thời S1-05 không thay thế concurrency các module khác.

## Việc tiếp theo và lần cập nhật này

### Demo dịch vụ/hóa đơn đã chuẩn bị (29/09/2026)

- Thêm `verification/prepare_service_demo.py`, hướng dẫn README và ignore `/data/service-demo/`. Tạo bản sao mới từ schema nguồn đã đọc chỉ đọc, không ghi DB gốc; công cụ không ghi đè demo, từ chối nguồn đã có schema tùy chọn cần rà soát.
- Demo thành công tại `data/service-demo/20260929-231718-28a08b/demo.sqlite`, server nền localhost:5250, PID lúc tạo 8944. Có bốn tài khoản mới, tòa ID 4, ba phòng DEMO-101/102/103, hai hợp đồng HD-DEMO-1/2 (24 tháng từ 01/09/2026), năm dịch vụ có giá. DEMO-101 có hóa đơn tháng 09/2026 tổng 2.030.000đ; DEMO-102 để người dùng tự lập. Giá chỉ là dữ liệu thử. Credential thật ở access.json cạnh DB, latest.txt trỏ demo mới nhất; không ghi mật khẩu vào tài liệu Git.
- Hợp đồng/hồ sơ/role demo được chuẩn bị bằng SQL/CLI chỉ trên bản sao; tòa/phòng/dịch vụ/hóa đơn qua HTTP thật và có audit. Bản sao giữ dữ liệu nguồn nên vẫn có thể chứa dữ liệu local cũ, không chia sẻ DB.
- Xác minh thực tế: build Debug thành công (cảnh báo ImageSharp); cả bốn tài khoản đăng nhập được, ADMIN vào nhật ký, Quản lý bị chặn dịch vụ, Chủ nhà thiết lập giá và phát hành hóa đơn đúng tổng; phát hành trùng bị từ chối; trang chi tiết HTTP 200; check-database, integrity=ok/FK rỗng, SHA-256 DB nguồn trước/sau giống nhau. Chưa nghiệm thu UI đồ họa, SMTP hoặc toàn bộ suite.
- Lần đầu trong sandbox lỗi Data Protection/Event Log, script đã dừng server lỗi; chạy lại ngoài sandbox thành công, giữ bản lỗi riêng không sử dụng. App gốc cổng 5247 không bị dừng. Dùng cổng 5250 và tài khoản Chủ nhà của demo; xem cách chạy lại/đọc credential ở README.

1. Khởi tạo DB và bỏ theo dõi DB đã triển khai trong S2-06; xem hướng dẫn README và bàn giao mới nhất. Đồng đội cần sao lưu ra ngoài repo trước lần pull nhận thay đổi bỏ theo dõi DB.
2. Chốt giá/cách tính dịch vụ, mã phòng, nội dung email reset còn mở; kiểm tra SMTP/UI trên bản sao trước nghiệm thu.
3. Rà soát fixture sau merge, khôi phục/chuyển bộ S1-05 nếu cần. Tích hợp quyền/phạm vi dữ liệu/audit khi triển khai nghiệp vụ mới, không tự làm toàn backlog.
4. Lần này: viết lại tien-do.md; gộp và xóa sáu tài liệu chức năng/demo được thay thế; sửa liên kết README/cap-nhat-csdl.md và mô tả phiên bản updater. Giữ hướng dẫn, yêu cầu, mô hình và SQL.
5. Xác minh lần này: đối chiếu mã startup/updater/auth/email, kiểm tra file test hiện có, liên kết nội bộ và diff tài liệu. Không build/chạy test, không mở/ghi database, không sửa mã ứng dụng, không commit/push.

### Khắc phục xóa tài khoản (30/09/2026)
- Kiểm tra DB local ở chế độ chỉ đọc: schema version 4; `abc@gmail.com` có `is_deleted=1`, nên xóa đã được ghi nhận (soft-delete) và dữ liệu lịch sử vẫn giữ.
- Cập nhật `Views/ManagedAccounts/Index.cshtml` để không hiện nút Xóa lại cho tài khoản đã xóa.
- Cập nhật `AccountController.ConfirmEmail` dùng SQL trực tiếp cho bước xác nhận ẩn danh, tránh yêu cầu actor audit.
- Đã dừng tiến trình cũ và build Debug thành công, 0 lỗi (còn cảnh báo license ImageSharp).
- Chưa thực hiện kiểm thử SMTP thật hoặc thao tác UI sau khi khởi động lại.

Cách chạy lại: `dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --launch-profile http`, đăng nhập ADMIN rồi tải lại `/ManagedAccounts`. Hành vi hiển thị tài khoản đã xóa trong ghi chú cũ được thay thế bởi bản sửa dưới đây.

### Hoàn thiện xóa và tái sử dụng tài khoản tại ổ D (30/09/2026)

- Làm việc tại `D:\DEV\TTCS_T926_K12C3_N1`, nhánh `Feature/fix/dang-ky-dang-nhap-phan-quyen`. Giữ các thay đổi chưa commit có sẵn, bao gồm DB local và project test; không reset, commit/push hoặc sửa DB nguồn trong quá trình kiểm thử.
- Kiểm tra SQLite nguồn chỉ đọc: v4, hai UNIQUE index `ux_account_email_normalized` trên `lower(trim(email))` và `ux_account_phone` trên số điện thoại, chưa có điều kiện loại tài khoản đã xóa. Có sáu tài khoản đã xóa. FK tham chiếu theo ID từ tòa, hồ sơ, nhật ký, reset/phiên/xác nhận; FK check không lỗi. DB local chưa cài bảng hợp đồng/hóa đơn đầy đủ như mô hình tham chiếu.
- Thêm `Data/AccountReuseSchema.cs`, nâng phiên bản hiện tại lên v5. Web tự chạy bước v4 → v5 sau kiểm tra schema, có backup nhất quán cạnh DB và transaction. Chỉ thay hai index thành UNIQUE `WHERE is_deleted = 0`; không đổi ID/email/số điện thoại cũ, không dựng lại bảng, không xóa dây chuyền. Schema UNIQUE không nhận diện được bị từ chối để tránh làm hỏng ràng buộc. Updater cũng dùng cùng bước này.
- v5 xử lý tài khoản đã xóa từ trước: khóa, thu hồi phiên/refresh/reset, bỏ mã xác nhận và bỏ phân công tòa của Quản lý. Giữ nguyên hồ sơ và nhật ký; không giả lập người thực hiện cho sửa dữ liệu hạ tầng. Các schema trước v4 vẫn cần quy trình nâng cấp một lần như tài liệu CSDL.
- `/ManagedAccounts` loại `IsDeleted` trước đếm/lọc/phân trang. ADMIN bấm Xóa và xác nhận là tài khoản biến mất, email/số điện thoại dùng lại được. Tài khoản chỉ khóa vẫn giữ chỗ thông tin. Xóa mới giữ audit, bỏ phân công và thu hồi phiên trong cùng transaction; mở khóa/gửi lại mật khẩu/xóa lại ID đã xóa trả 404, không phục hồi tài khoản cũ. Vẫn cấm tự xóa/xóa ADMIN, kiểm tra quyền backend và CSRF.
- Đồng bộ kiểm tra trùng ở tự đăng ký và tạo bởi ADMIN; đăng nhập cookie/API, refresh, xác nhận/gửi lại email, đổi/reset mật khẩu, kiểm tra phiên đều loại tài khoản đã xóa. Tài khoản mới nhận ID riêng; token cũ không tác động tài khoản mới. Không áp dụng global query filter lên quan hệ lịch sử. Xử lý hết hạn xác nhận ẩn danh bằng transaction SQL để không lỗi do thiếu actor audit.
- Thêm `verification/account_delete_http.py`: tự tạo bản sao và tài khoản kiểm thử, email pickup, không dùng SQL sửa dữ liệu thật. PASS HTTP xóa/đăng ký lại/tạo lại, xác nhận, đăng nhập email/SĐT, reset đúng ID mới, quyền/CSRF, cookie/JWT/refresh cũ, tài khoản khóa, bỏ phân công Quản lý, rollback khi audit lỗi, UNIQUE SQLite từng trường, lọc/phân trang, backup v4, từ chối UNIQUE lạ không đổi DB, integrity/FK và chạy lại v5. Đối chiếu hồ sơ/nhật ký cũ giữ nguyên; kiểm thử hợp đồng/hóa đơn có điều kiện nếu DB nguồn có các bảng đó, chưa phải nghiệm thu module hợp đồng đầy đủ.
- Xác minh: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore` thành công, 0 lỗi; còn cảnh báo ImageSharp license và CS8601 có sẵn. Bộ HTTP chạy ngoài sandbox do Windows Event Log/Data Protection bị chặn trong sandbox; không phải thuộc tính chỉ đọc của ổ D. SHA-256 DB nguồn trước/sau kiểm thử giống nhau. Chưa chạy toàn bộ suite cũ hoặc nghiệm thu UI đồ họa/SMTP thật. `git diff --check` chỉ còn dòng trống cuối project test từ thay đổi có sẵn, không sửa phần đó.
- Sử dụng: chạy lại app với mã mới như lệnh trên; DB v4 tự nâng lên v5 và tạo backup, không cần người dùng chạy SQL/lệnh sửa dữ liệu mỗi lần xóa. Phiên làm việc này chỉ nâng cấp DB tạm; DB local thật sẽ được xử lý khi app mới khởi động.

### Đăng ký lại khi tài khoản mới đang chờ xác nhận (30/09/2026)

- Đối chiếu ảnh báo trùng với DB thật ở chế độ chỉ đọc: schema đã lên v5 và có đúng hai partial UNIQUE index. Bản ghi cũ ID 12 đã xóa; lần đăng ký sau tạo ID 13, chưa xóa và chưa xác nhận email. Vì vậy lỗi trùng đến từ tài khoản mới đang chờ mã, không phải bản ghi đã xóa giữ chỗ.
- `AccountController.Register` chuyển về trang xác nhận khi cả email và SĐT khớp cùng tài khoản Khách thuê đang hoạt động/chưa xác nhận/chưa xóa. Không tạo thêm ID, đổi mật khẩu hoặc gửi email tự động trong lần thử lại. Chỉ trùng một trường, tài khoản đã xác nhận hoặc bị khóa vẫn được xử lý kiểm tra trùng như cũ.
- Trang xác nhận hiển thị lỗi gửi mã từ lần đăng ký trước (`RegisterError`), thay vì bỏ qua thông báo. Người dùng có thể tiếp tục nhập mã hoặc Gửi lại mã, không cần xóa tài khoản rồi đăng ký liên tục.
- Build `--no-restore -p:UseAppHost=false` PASS, giữ tiến trình web hiện tại để không mất cấu hình SMTP trong terminal của người dùng. Bộ `verification/account_delete_http.py` PASS trên bản sao, bổ sung kiểm tra retry chuyển đúng trang, không đổi ID/hash mật khẩu và không nối nhầm khi chỉ trùng email. Fixture hỗ trợ DB nguồn v5 đã có email/SĐT tái sử dụng. SHA-256 DB nguồn không đổi; không commit/push. Cần khởi động lại app trong cùng terminal cấu hình SMTP để nạp bản sửa.

### Phân biệt gửi email thật, pickup và lỗi gửi mã (30/09/2026)

- Phát hiện `ResendConfirmation` nuốt lỗi gửi thư và luôn hiện thông báo đã gửi. Đã bỏ hành vi này: đăng ký/gửi lại mã báo riêng kết quả pickup, SMTP nhận thư hoặc lỗi cấu hình/SMTP/quá thời gian. Không khẳng định SMTP nhận thư đồng nghĩa đã tới inbox. Trang xác nhận không còn mặc định khẳng định mã đã gửi.
- `IRegistrationEmailSender` trả về chế độ giao thư sau khi gửi thành công. Giới hạn chờ async 15 giây bằng cancellation token; các luồng gửi mật khẩu tạm/reset cũng xử lý lỗi timeout thay vì trả 500.
- Thêm `--check-email-config`: chỉ in chế độ gửi, host/port/TLS và boolean có cấu hình username/password/from; không in thông tin đăng nhập và không gửi email. README bổ sung hướng dẫn Gmail và yêu cầu chạy trong cùng terminal đã đặt biến môi trường.
- Build riêng tại `D:\DEV\.tmp-email-verification` PASS vì app hiện tại khóa DLL đầu ra mặc định; không dừng app hoặc làm mất môi trường SMTP của người dùng. Bộ HTTP trên bản sao PASS, gồm mô phỏng lỗi thư mục gửi, kiểm tra thông báo lỗi không bị thay thành thành công và gửi lại được sau khi khôi phục. Hash DB nguồn không đổi. Chưa kiểm thử Gmail thật hoặc tình huống máy chủ SMTP treo; không đọc được biến môi trường riêng trong terminal chạy app của người dùng.
- Lệnh kiểm tra trong môi trường agent trả PICKUP, SMTP host rỗng, username/password chưa cấu hình; kết quả này không chứng minh cấu hình của tiến trình web hiện đang chạy. Cần người dùng chạy lệnh kiểm tra ngay tại terminal của app và cung cấp đầu ra đã che thông tin đăng nhập sẵn. App phải khởi động lại để nạp mã mới. Không commit/push.

### Chẩn đoán lỗi Windows chặn DLL (30/09/2026)

- Lệnh chạy của người dùng bị `FileLoadException 0x800711C7` trước khi app khởi động. Đã đọc CodeIntegrity/Operational: các sự kiện 3077 lúc 06:26 chỉ đúng `QL_PhongTro.dll`, policy `VerifiedAndReputableDesktop`, GUID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}` (Smart App Control). DLL chưa ký số và không có stream Zone.Identifier. Đây là chặn thực thi của Windows, không phải lỗi SMTP, cảnh báo license ImageSharp hay thuộc tính chỉ đọc ổ D.
- Chỉ chẩn đoán, không thay đổi chính sách bảo mật, không xóa/rebuild DB hoặc tạo lại ADMIN. App chưa chạy nên không thể gửi mã. Cần chủ máy quyết định cấu hình Smart App Control phù hợp môi trường phát triển hoặc dùng bản build ký số được tin cậy; với máy được quản lý cần quản trị viên xử lý. Không khẳng định chạy Administrator hoặc Unblock-File sẽ giải quyết chặn này.

### Ẩn quản lý tòa/phòng khỏi Khách thuê (30/09/2026)

- Menu trước đây lấy quyền READ của module PHONG_TRO để dẫn Khách thuê vào controller quản lý dành cho nhân sự. `PermissionService.MenuAsync` nay loại module này khỏi menu Khách thuê, gồm hai liên kết Tòa nhà/phòng/bảng giá và Quản lý tòa nhà. Giữ các chức năng hồ sơ và module khác; không sửa quyền trong DB hoặc tự triển khai trang phòng đang thuê.
- `PhongTroController` yêu cầu vai trò CHU_NHA/QUAN_LY/ADMIN ở backend, vẫn kiểm tra quyền module và phạm vi dữ liệu hiện có. Khách thuê gõ URL trực tiếp hoặc gửi POST tạo tòa bị từ chối. Không dùng riêng ẩn menu làm kiểm soát quyền.
- Build Debug ở thư mục kiểm thử riêng thành công. Bổ sung HTTP kiểm tra menu Khách thuê, hồ sơ vẫn hiện, GET `/PhongTro` và `/PhongTro/ToaNha` trả 403, POST `/PhongTro/TaoToaNha` trả 403 và ADMIN vẫn GET 200 trên DB bản sao. Cập nhật kỳ vọng tương ứng trong PermissionTests; chưa chạy lại toàn bộ xUnit suite cũ. Không sửa DB thật, không commit/push. Cần khởi động lại app để nạp bản sửa.

### Nút con mắt cho mật khẩu (30/09/2026)

- Thay nút chữ Hiện rộng cả dòng ở Đăng nhập và Đổi mật khẩu bằng SVG con mắt nhỏ nằm bên phải bên trong ô nhập, dùng partial `_PasswordToggle`. Khi hiện mật khẩu, biểu tượng có gạch chéo; giữ thao tác bàn phím, nhãn trợ năng và `aria-pressed`. CSS riêng tránh quy tắc `.auth-card .btn` làm nút chiếm cả dòng.
- Build Debug vào thư mục riêng PASS, không ảnh hưởng tiến trình app đang chạy; chưa kiểm tra trực quan trên trình duyệt. Không sửa DB, chưa commit/push thay đổi giao diện này.
### S2-07 — Danh sách yêu cầu của chủ nhà (02/10/2026)

- Đã thêm bảng `yeu_cau` ở schema v9 với mã yêu cầu, khách, phòng, tòa nhà, loại yêu cầu, ngày mong muốn, trạng thái và thời điểm tạo; không sửa database local.
- Đã thêm `YeuCauController.Index` và `/YeuCau`: lọc theo tòa thuộc chủ nhà, trả đủ thông tin yêu cầu và sắp xếp mới nhất trước; có trạng thái danh sách rỗng.
- Dữ liệu mẫu giả nằm ở `docs/sql/S2-07-yeu-cau.sql`, chỉ dùng trên database demo/bản sao.
- File thay đổi: model, EF mapping, updater v9, controller, view model, Razor View, SQL mẫu, README và tài liệu tiến độ.
- Xác minh: build project chính với output riêng `S207Check` PASS, 0 lỗi; còn cảnh báo ImageSharp license và CS8601 có sẵn. Chưa chạy updater, test HTTP/UI hoặc script dữ liệu mẫu.
- Chưa làm: đánh dấu quá 24 giờ, số lượng chưa xử lý trên menu, tạo/sửa yêu cầu. Bộ lọc được bổ sung tại AC2 bên dưới.

### S2-07 AC2 — Lọc yêu cầu (02/10/2026)

- PO chốt 5 trạng thái: Mới, Đã hẹn lịch, Đã duyệt, Từ chối, Đã huỷ. `/YeuCau` nhận `trangThai` và `toaNhaId`, lấy danh sách tòa đang hoạt động của chủ nhà, hỗ trợ lọc riêng hoặc kết hợp; nút bỏ bộ lọc đưa về danh sách rộng hơn.
- Thứ tự `ngay_tao DESC, id DESC` được giữ sau mọi bộ lọc. Không triển khai cảnh báo quá 24 giờ hoặc số lượng chưa xử lý trên menu.
- Xác minh cần thực hiện: build, test từng trạng thái/tòa nhà/kết hợp/bỏ lọc/không có kết quả/thứ tự. Chưa chạy kiểm thử trong lượt này.

### S2-07 AC3 — Nhận biết yêu cầu chưa xử lý quá 24 giờ (02/10/2026)

- Giả định PO cần xác nhận: `Mới` và `Đã hẹn lịch` là chưa xử lý; `Đã duyệt`, `Từ chối`, `Đã huỷ` là đã có kết quả. Mốc bắt đầu tính là `ngay_tao`; đúng 24 giờ đã được đánh dấu, tính theo UTC.
- Controller trả thêm `QuaHanChuaXuLy`; view giữ nguyên toàn bộ thông tin và tô nổi bật dòng, kèm nhãn “Quá 24 giờ, chưa xử lý”. Vì tính trên từng dòng sau truy vấn, dấu hiệu vẫn đúng khi lọc trạng thái hoặc tòa nhà.
- Chưa có số lượng yêu cầu chưa xử lý trên menu. Build/test thực tế cần thực hiện trước nghiệm thu; database local không bị ghi.
## Local email pickup file names (05/10/2026)

- Cap nhat che do email pickup local: khi chua dung SMTP, app tu ghi file `.txt` de doc voi tien to danh so `0001-...`, chua gui vao inbox. Noi dung file co thoi gian, nguoi gui, nguoi nhan, tieu de va body co ma xac nhan.
- Trang `/Account/ConfirmEmail` hien them thu muc pickup va file moi nhat sau khi dang ky/gui lai ma. `--check-email-config` in duong dan pickup tuyet doi; `run.bat` in `Email pickup local=...` khi cau hinh pickup den tu `.env.local`/bien moi truong.
- File thay doi: `QL_PhongTro/Services/PasswordEmailSender.cs`, `QL_PhongTro/Controllers/AccountController.cs`, `QL_PhongTro/Views/Account/ConfirmEmail.cshtml`, `QL_PhongTro/Program.cs`, `run.bat`.
- Xac minh thuc te: `dotnet build .\QL_PhongTro\QL_PhongTro.csproj --no-restore` PASS; `dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --no-build --launch-profile http -- --check-email-config` PASS va in `Pickup directory: C:\Users\cter4\AppData\Local\Temp\s105-mail-preview`. Con can restart app va gui thu ma tren UI de thay file pickup moi sinh. Khong doi schema/database, khong ghi SMTP secret.

## Rà soát giao diện, quyền và nghiệp vụ vận hành (06/10/2026)

- Tách nội dung trang chủ và điều hướng theo vai trò; Khách thuê tập trung tìm phòng/yêu cầu/lịch hẹn, Chủ nhà và Quản lý dùng màn vận hành, ADMIN có toàn quyền và xem dữ liệu toàn hệ thống. Backend vẫn kiểm tra vai trò và phạm vi dữ liệu, không chỉ ẩn/hiện menu.
- Danh sách phòng mặc định hiển thị tất cả phòng nhìn thấy được, có lọc theo tòa nhà/người quản lý/trạng thái; bổ sung STT cho các danh sách quản lý chính. Danh sách yêu cầu của Chủ nhà hiển thị lời nhắn ngay ngoài bảng; các trạng thái dùng màu nhất quán, trong đó Mới màu xanh lá và Hủy/Từ chối màu đỏ.
- Thiết kế lại khối dịch vụ trên chi tiết tin đăng, tách nhóm cách tính và giải thích rõ chi phí cố định, tiền cọc, điện/nước theo sử dụng. Luồng tải ảnh kiểm tra số lượng, loại và dung lượng trước khi gửi, đồng thời có thông báo lỗi có thể đóng.
- Khi đổi cấu hình dịch vụ chung, phòng đang có đơn giá riêng được tạo snapshot cấu hình riêng đầy đủ (cách tính, đơn vị, đơn giá) và hóa đơn đọc đúng snapshot này. Thay đổi cấu hình điện/nước chung không làm phòng riêng chuyển theo cách tính mới. Không đổi schema/database.
- Khi Chủ nhà hoặc ADMIN xác nhận lịch hẹn, hệ thống gửi email cho Khách thuê với thời gian và liên kết chi tiết yêu cầu. Lỗi SMTP không rollback lịch đã xác nhận; giao diện hiện cảnh báo để người vận hành xử lý.
- Cập nhật test cho quyết định mới về ADMIN toàn quyền, danh sách phòng mặc định tất cả, nội dung dịch vụ và bảo toàn cấu hình riêng. Build Debug cách ly PASS, 0 lỗi. Nhóm kiểm thử liên quan đạt 36/37; sau khi sửa kỳ vọng cũ, test còn lại chạy riêng PASS (1/1). Còn cảnh báo NU1900 do không lấy được dữ liệu lỗ hổng NuGet, cảnh báo license ImageSharp và CS8601 có sẵn ở AuthController.
- Chưa kiểm chứng trực quan responsive 360px và chưa gửi Gmail SMTP thật. `run.bat` có cấu hình SMTP theo yêu cầu người dùng; không ghi lại bí mật trong tài liệu này. Cần đổi App Password nếu repository đã được chia sẻ ngoài phạm vi tin cậy.
- Chi tiết tin đăng: thay hai nhóm thẻ dịch vụ bằng bảng giá ba cột `Dịch vụ / Cách tính / Đơn giá`, gộp dịch vụ theo sử dụng và khoản cố định vào cùng một bảng; giữ trạng thái rỗng và phần tổng chi phí bên dưới. Bảng có vùng cuộn ngang trên màn hình nhỏ. Build Debug cách ly ngày 06/10/2026 PASS, 0 lỗi; chưa kiểm tra trực quan trên trình duyệt sau khi khởi động lại app.
- Tách dashboard đăng nhập theo vai trò ngày 06/10/2026: ADMIN xem số liệu toàn hệ thống và liên kết quản trị; Chủ nhà/Quản lý xem số liệu tòa, phòng, tình trạng thuê và yêu cầu trong phạm vi; Khách thuê xem yêu cầu cá nhân cùng tin đăng mới. Số liệu lấy từ database hiện tại, không dùng số giả từ ảnh thiết kế.
- Layout Khách thuê không render sidebar; thay bằng header ngang và menu xổ xuống gồm Tổng quan, Tìm phòng, Tin đăng, Yêu cầu thuê, Hồ sơ. Các trang Tìm phòng/Tin đăng khi đã đăng nhập cũng dùng layout theo vai trò; bản ẩn danh vẫn giữ public layout. ADMIN/Chủ nhà tiếp tục dùng sidebar với quyền backend hiện có.
- Áp dụng bảng màu nâu/be được duyệt: primary `#5B3A1B`, hover `#4A2F16`, active `#3D2612`, nền `#F6EEE4`, header `#E6D5C2`, sidebar `#FAF6F0`, cùng màu border/text/trạng thái tương ứng. Có responsive cho dashboard và menu Khách thuê. Build Debug cách ly PASS, 0 lỗi; 3 test quyền/menu PASS và lần chạy lại test menu sau thay đổi layout PASS. Chưa nghiệm thu trực quan trên trình duyệt/mobile thật.
- 07/10/2026: dựng lại bố cục dashboard theo ảnh mẫu Latte/Warm Brown. Thêm bảng yêu cầu gần đây, thẻ tình trạng phòng, bảng nhật ký hoạt động, biểu đồ phân bổ vai trò theo số lượng thật; Chủ nhà có vùng doanh thu/việc cần xử lý, Khách thuê có vùng phòng/hóa đơn/yêu cầu và bốn thẻ thống kê. Các nghiệp vụ chưa triển khai hiển thị trạng thái thiếu dữ liệu, không giả lập doanh thu, công nợ hay thanh toán. Bộ màu mới ở `latte-dashboard.css`; cập nhật logo/sidebar/header tìm kiếm và cấu trúc thẻ. Build cách ly PASS; HTTP test menu theo vai trò PASS. Chưa xác minh trực quan trình duyệt hoặc độ khớp ảnh từng pixel; không đổi schema/database.
- 07/10/2026: gom sidebar theo mẫu Chủ nhà (Vận hành/Tài chính/Chăm sóc) và ADMIN (danh sách quản trị). Loại liên kết tìm phòng và hồ sơ khỏi sidebar nhân sự, đưa tài khoản lên header, thay icon SVG gọn. Tòa/phòng có tab điều hướng nội bộ; Chủ nhà ADMIN dẫn đến `/ManagedAccounts?role=CHU_NHA`; Công nợ/Thông báo phân biệt bằng tham số section trên trang module, vẫn kiểm tra quyền module. Các nghiệp vụ chưa triển khai vẫn mở trang module thông báo hiện trạng. Build Razor/C# cách ly PASS, không đổi database.
- 07/10/2026: mục ADMIN “Tài khoản & phân quyền” chuyển đến `/ManagedAccounts`; menu tài khoản trên header chỉ giữ Hồ sơ, Đổi mật khẩu, Đăng xuất. Sidebar Chủ nhà/Quản lý tách Quản lý tòa nhà (`/PhongTro/ToaNha`) và Quản lý phòng (`/PhongTro`). Phân biệt active giữa danh sách tài khoản và bộ lọc Chủ nhà để không tô sáng hai mục cùng lúc.
- 07/10/2026 — S3-01 AC1–AC2: thêm `/HopDong/Create`, chọn yêu cầu DA_DUYET trong phạm vi chủ nhà/ADMIN, tự lấy khách/phòng, nhập giá thuê nguyên VND, cọc, ngày bắt đầu, kỳ hạn và ngày chốt. Ngày kết thúc readonly, tính server `AddMonths(kỳ hạn).AddDays(-1)`; không bind ngày kết thúc gửi lên. Lưu hợp đồng NHAP và kỳ thuê trong một transaction, có nhật ký và unique yêu cầu để chống lưu trùng. `/Modules/HOP_DONG` và nút lập hợp đồng từ lịch hẹn dẫn đến màn thật. Danh sách hợp đồng giới hạn theo chủ nhà, quản lý được phân công hoặc khách đứng tên; tạo chỉ với quyền ghi. Build C#/Razor và 33 test mục tiêu PASS (ContractCreationTests, LichHenLapHopDongTests, nâng cấp quyền v13→v14); git diff --check PASS.
- Giao diện S3-01 dùng Latte/Warm Brown theo mẫu: breadcrumb, thẻ yêu cầu đã duyệt, form trái, thẻ thời gian/thông tin tự động bên phải, thanh lưu cuối trang và responsive. Không giả lập kiểm tra chồng lấn, chỉ số đầu kỳ, mã HD hay trạng thái phòng: các phần này ngoài phạm vi AC1–AC2; chỉ lưu nháp với tham chiếu nội bộ NHAP. Đã kiểm tra render HTTP/form readonly và POST giả ngày kết thúc; chưa nghiệm thu trực quan/pixel trên trình duyệt.
- Schema v14 bổ sung cột hợp đồng/kỳ thuê bằng ALTER additive, không thay dữ liệu cũ; updater hiện có tạo backup trước nâng cấp. Kiểm thử trên SQLite tạm: bảo toàn hợp đồng legacy, cập nhật lặp không đổi dữ liệu, integrity_check và foreign_key_check đạt. Không chạy updater trên database người dùng. Trước demo: dừng app, dùng đúng DatabasePath, chạy `dotnet run --project QL_PhongTro -- --update-database`, rồi mở `/HopDong/Create`. Bộ test mở rộng có các assertion giao diện cũ không khớp (sidebar/menu, bảng giá tin đăng, lớp password-field); không coi toàn suite PASS.
- 07/10/2026 — điều chỉnh form S3-01 theo yêu cầu mới: giá thuê readonly theo giá hiện tại của phòng; chọn cọc nguyên 0–3 tháng (mặc định 1), tự tính tiền cọc bằng giá phòng × số tháng. Máy chủ bỏ qua giá thuê/tiền cọc gửi giả và tính lại trong transaction. Ngày bắt đầu tối thiểu là hôm nay theo múi giờ Việt Nam từ ITimeProvider, kiểm tra ở form, POST và endpoint kiểm tra chồng lấn; yêu cầu/nháp có ngày cũ mở form sẽ mặc định hôm nay. Không thay dữ liệu hợp đồng đã lưu, không đổi schema. Build C#/Razor cách ly PASS, 67 kiểm thử mục tiêu PASS, gồm cọc 0/1/2/3 và ngoài giới hạn, ngày quá khứ/hôm nay, mốc nửa đêm Việt Nam, POST giả giá/cọc, readonly và min trên HTML. Chưa nghiệm thu trực quan trình duyệt.


## 07/10/2026 — Đồng bộ màu giao diện

- Theo yêu cầu người dùng: graphite/champagne cho trang công khai, đăng nhập/đăng ký và tất cả vai trò. Bảng màu chung trong `champagne-theme.css`, nạp cuối ở ba layout; cập nhật CSS từng trang và hai logo SVG.
- Màu phụ: chữ xám `#656A73`, hover `#25282D`, focus champagne có độ trong suốt; giữ đỏ cho lỗi/xóa. Không đổi bố cục hoặc nghiệp vụ.
- Không chụp ảnh theo yêu cầu. Kiểm chứng build và HTTP; chưa nghiệm thu trực quan trên trình duyệt/thiết bị 360px.
- Xác minh: build thành công (0 lỗi; cảnh báo ImageSharp/CS8601 có sẵn); 8 trang công khai và 20 trang theo 4 vai trò trả HTTP 200, đều nạp CSS mới. Website chạy lại tại http://localhost:5247 với bộ demo hiện tại.
- Phối lại trang chủ: overlay graphite cho banner, vạch champagne ở tiêu đề, tìm kiếm trắng/viền champagne và nút graphite; trang chủ theo vai trò dùng thẻ trắng với viền nhấn champagne. CSS giới hạn trong `.public-main-home` và `.role-home`; HTTP trang chủ/CSS 200, không chụp ảnh.
- Tinh chỉnh trang chủ công khai theo bảng 6 màu: tiêu đề lớn/nét vừa, overlay graphite, champagne chỉ dùng vạch nhấn và viền trên khung tìm kiếm; thẻ trắng bo 16px, nền khu vực xám nhạt. Dưới 640px tìm kiếm xếp dọc, nút đủ chiều rộng. HTTP trang chủ/CSS 200; chưa kiểm chứng trực quan, không chụp ảnh.
- Theo ảnh mẫu người dùng: trang chủ có menu trắng/logo champagne/nút đăng ký graphite, banner toàn màn hình và tiêu đề góc trên trái. Khung tìm kiếm chuyển ngay dưới banner. CSS giới hạn `.public-home`/`.public-main-home`; build 0 lỗi, HTTP trang chủ/logo 200; không chụp ảnh, chưa xác minh trực quan.
- Theo yêu cầu tiếp theo: trang chủ cao 100dvh, menu lấy chiều cao thực và banner chiếm phần còn lại; thanh tìm kiếm absolute đè trên nền gần đáy. Mobile dùng 2 hàng tìm kiếm; màn hình thấp thu gọn tiêu đề/khoảng cách. Trang chủ/CSS HTTP 200; không chụp ảnh.
- Tăng chữ header trang chủ: menu, đăng nhập/đăng ký và thông tin tài khoản 16px; mobile <=640px dùng 14px. CSS HTTP 200, không chụp ảnh.
- Tăng toàn bộ cụm thương hiệu SVG ở header trang chủ từ cao 38px lên 52px (logo, tên NhàTốt và slogan cùng tăng theo tỷ lệ); mobile 44px. CSS HTTP 200.
- Bo tròn dạng viên thuốc cho Đăng nhập và Đăng ký ở header trang chủ; Đăng nhập có viền xám nhạt và hover champagne. CSS HTTP 200.
- Đồng bộ Tìm phòng với trang chủ: header trắng/logo champagne/nút bo tròn; ô tìm kiếm, bộ lọc, thẻ tin trắng; giá chữ graphite, nhãn champagne, biểu tượng xám; phân trang và focus đồng bộ. Kiểm tra HTTP trang mặc định/lọc/sắp xếp/trang 2/không kết quả và trang chủ đều 200; không chụp ảnh.
- Thêm icon ngôi nhà SVG trước Trang chủ trong breadcrumb Tìm phòng, dùng currentColor graphite. Build thành công, HTTP 200 và HTML chứa icon; không chụp ảnh.
- Thêm icon ngôi nhà trước Trang Chủ trong header công khai, dùng currentColor để theo màu active/hover. Build 0 lỗi; trang chủ và Tìm phòng HTTP 200, đều chứa icon header.
- Bỏ viền trên champagne của thanh tìm kiếm Tìm phòng; giữ viền xám nhạt giống trang chủ. CSS HTTP 200.

## 07/10/2026 — Giao diện chủ nhà theo ảnh mẫu

- Thêm `_OwnerDashboard.cshtml` và `owner-dashboard.css`: sidebar graphite, header trắng, nền #F5F5F4; 4 thẻ thống kê; bố cục 2 cột với tình hình thu tiền/tình trạng phòng và yêu cầu gần đây/việc cần xử lý; danh sách cơ sở với thanh lấp đầy.
- Dùng số liệu phòng, đặt cọc, yêu cầu và cơ sở thực tế. Tổng hợp cơ sở chỉ từ tòa đang hoạt động thuộc chủ nhà đăng nhập. Không đổi schema, không thay dữ liệu nghiệp vụ.
- Phần thu tiền hiển thị chưa có dữ liệu; thay bảng hóa đơn cần thu bằng yêu cầu thuê hiện có. Chưa có dữ liệu thanh toán/báo hỏng để mô phỏng toàn bộ ảnh mẫu. Không tạo số liệu minh họa.
- Build thành công; HTTP xác minh 2 chủ nhà thấy đúng cơ sở riêng, các link phòng/yêu cầu/hợp đồng/dịch vụ mở được. Admin/khách/quản lý vẫn dùng dashboard hiện tại. Không chụp ảnh; chưa kiểm chứng trực quan hoặc thiết bị 360px.
- Test `OwnerDashboardAggregatesOnlyActiveOwnedBuildings`: PASS, kiểm tra cách ly cơ sở chủ nhà/ngừng hoạt động và tỷ lệ lấp đầy từ dữ liệu thực.
- Chuyển Thông báo từ sidebar lên icon chuông giữa tìm kiếm và avatar. Hiển thị theo quyền YEU_CAU_THUE; giữ liên kết module thông báo hiện có (chưa triển khai thông báo/badge chưa đọc). Build 0 lỗi; HTTP/HTML vị trí và liên kết đạt.
- Sidebar chủ nhà rộng 248px (trước 224px), giảm khoảng cách menu và ẩn thanh cuộn; màn hình thấp vẫn cuộn được để truy cập đủ mục. CSS HTTP 200.
- Header chủ nhà: thay biểu tượng ba sọc bằng link icon nhà / Tổng quan. Mobile giữ nút chữ Menu để truy cập sidebar và menu tài khoản. Build 0 lỗi; HTTP/HTML đúng breadcrumb.
- Tăng chữ Tổng quan và dấu / trên header chủ nhà lên 16px, icon ngôi nhà lên 21px. CSS HTTP 200.
- Header tìm kiếm: thay ký tự kính lúp bằng SVG giống trang chủ, đặt nút kính lúp trước ô nhập để đứng trước placeholder. Giữ form GET /TimTin và nút submit có aria-label.
- Tình hình thu tiền: thêm select 6 tháng gần nhất (3/6/12 tháng), nhãn trục tháng đổi theo lựa chọn; chú giải Đã thu graphite/Phải thu champagne giống ảnh. Dữ liệu thu tiền vẫn chưa có; build 0 lỗi, HTTP 200 và HTML chứa bộ chọn/chú giải.
- Cụm tài khoản chủ nhà theo ảnh: avatar champagne nhạt 36px/chữ cái hai từ cuối tên, tên 12px, nhãn Chủ nhà 10px và mũi tên xuống, viền phân cách trái. Giữ tên thực và liên kết menu tài khoản. Build 0 lỗi; HTTP/HTML đạt.
- Tăng nhẹ chữ sidebar chủ nhà: mục menu 12→13px, tiêu đề nhóm 9→10px. CSS HTTP 200.
- Tăng chữ thanh tìm kiếm header chủ nhà từ 11px lên 13px; CSS HTTP 200.
- Đổi mật khẩu đồng bộ trang đăng nhập: LoginLayout, ảnh bên trái/form bên phải, logo, màu graphite, icon khóa/hiện mật khẩu và nút cùng kiểu. Giữ fields, CSRF và validation. Build 0 lỗi; HTTP trang và POST không hợp lệ 200, không thay mật khẩu demo; không chụp ảnh.
- Đồng bộ 5 trang xác thực với trang chủ: logo champagne (bản chữ trắng trên ảnh), overlay graphite, nền #F5F5F4, input trắng, chữ #25282D, nút graphite bo tròn và focus champagne. CSS dùng chung giới hạn .login-page. HTTP cả 5 trang và assets 200; không đổi nghiệp vụ hoặc mật khẩu, không chụp ảnh.
- Ẩn thanh cuộn trên cả 5 trang xác thực qua CSS chung .login-page; giữ cuộn bằng chuột/cảm ứng/bàn phím để không che form khi màn hình nhỏ. CSS HTTP 200.
- Quản lý tòa nhà theo ảnh: 4 thẻ thống kê, khối thu tiền/Cần chú ý, bảng trắng với công suất thuê/thanh champagne. Sidebar giữ nguyên, CSS chỉ trong .building-management. Dùng số phòng thuê thật; tài chính chưa có dữ liệu. Giữ toàn bộ form quản lý/CSRF. Build 0 lỗi; HTTP mặc định/tìm kiếm/không kết quả đạt và không lộ tòa chủ nhà khác. Không chụp ảnh.
- Tiến độ thu tiền tòa nhà: bộ chọn tháng kiểu ảnh mẫu, icon lịch trái/mũi tên phải, mặc định tháng hiện tại giờ VN và 12 tháng gần nhất. Dữ liệu tài chính vẫn chưa có. Build 0 lỗi; HTTP/HTML đạt; sidebar không đổi.
- Đầu danh sách tòa nhà theo ảnh: kính lúp/ô tìm tên-địa chỉ, lọc khu vực/tình trạng, nút list-grid và sắp xếp mới nhất theo ID hoặc tên A-Z. Lọc client trên các tòa đã được backend giới hạn quyền. Giữ sidebar. Build 0 lỗi; HTTP HTML/JS đạt; không chụp ảnh.
- Nút list/grid và Mới nhất dùng SVG nét mảnh theo ảnh, nền nhóm xám/ô active trắng, bo góc 10px. Giữ icon khi đổi sắp xếp. Build 0 lỗi; HTTP đạt, không chụp ảnh.


### 07/10/2026 — Bố cục danh sách tòa nhà theo ảnh tham chiếu
- Thêm cột thu tiền theo tháng, cần xử lý và liên kết Chi tiết; giữ số phòng, công suất và thanh tiến độ. Gom thao tác quản trị vào mục Thao tác, giữ nguyên sidebar.
- Chưa có nguồn thanh toán/báo hỏng: hiển thị rõ chưa có dữ liệu, không tạo số giả hoặc thay đổi schema.
- Kiểm chứng: build thành công; đăng nhập chủ nhà và GET /PhongTro/ToaNha trả 200, có các cột và liên kết mới. Chưa kiểm chứng trực quan ở 360px/Safari.


### 07/10/2026 — Điều hướng chi tiết tòa nhà
- Thêm biểu tượng nhà và breadcrumb Không gian quản lý → Tòa nhà → Chi tiết tòa nhà theo ảnh; chỉnh khoảng cách và liên kết quay lại. Sidebar giữ nguyên.
- Build thành công; HTTP chi tiết trả 200 và có breadcrumb mới. Chưa kiểm chứng trực quan/mobile.

- Điều chỉnh theo yêu cầu: breadcrumb chi tiết tòa nhà đặt trong dashboard-header của layout, bỏ breadcrumb trong nội dung; giới hạn theo route chi tiết. Build và HTTP kiểm tra đạt.

- Đồng bộ breadcrumb dashboard-header với Tổng quan: chữ 16px/600, màu #25282D, biểu tượng nhà 21px, khoảng cách 12px; giữ header và sidebar chung.

- Breadcrumb: trang hiện tại giữ màu #25282D; các liên kết cấp trước giảm độ đậm xuống #25282D80.

- 08/10/2026: Bỏ chữ Tổng quan trong breadcrumb chi tiết tòa nhà, giữ biểu tượng nhà có nhãn truy cập và các mục Tòa nhà / Chi tiết tòa nhà.

- 08/10/2026: Breadcrumb cuối cùng: biểu tượng nhà / Quản lý tòa nhà › Chi tiết tòa nhà; bỏ liên kết Danh sách tòa nhà dưới header theo yêu cầu.

- 08/10/2026: Cột Thao tác danh sách tòa nhà: Chi tiết màu nhấn, nút sửa hình bút và menu ba chấm; giữ các form có xác nhận và CSRF.

- Menu ba chấm: bỏ khung các nút và menu, xếp Cấu hình điện nước / Ngừng hoạt động / Xóa theo cột dọc; giữ nguyên hành vi và xác nhận.

- Menu ba chấm hiển thị nổi bằng position absolute; mở menu không tăng chiều cao hàng. Mobile dùng menu fixed để giữ cuộn ngang bảng.

- 08/10/2026: Thanh lọc phòng chi tiết tòa nhà theo ảnh: tìm mã/tên khách, tầng, loại phòng, mã tăng dần; giữ trạng thái tab và loại phòng khi phân trang. Thanh toán tạm vô hiệu vì chưa có nguồn dữ liệu. Chưa thêm tìm SĐT; build và HTTP ba trường hợp đạt, chưa kiểm chứng trực quan mobile.

- Ô tìm kiếm phòng theo ảnh: kính lúp 19px cùng hàng, cao 44px, bo 11px, viền xám mảnh, chữ 14px; giữ nội dung placeholder đúng chức năng tìm mã/tên hiện có.

- Chi tiết phòng: thêm cột thanh toán tháng theo quyền tài chính (chưa có dữ liệu), hiển thị ngày hết hạn hợp đồng và menu nổi ba chấm. Không giả lập đã trả/quá hạn.

- Thay nút quản lý phòng/cấu hình điện nước bằng Nhập danh sách (dẫn CreateBulk hiện có, chưa nhập tệp) và chuyển danh sách/lưới trong trang.

- Dấu ba chấm của phòng mở trực tiếp trang Edit; bỏ menu nổi chỉnh sửa/xem hợp đồng theo yêu cầu.

- 08/10/2026: Thiết kế lại SuaToaNha theo ảnh: bốn nhóm thông tin, cột tóm tắt bên phải, thanh lưu/hủy bên dưới; CSS riêng, sidebar giữ nguyên. Chỉ trường được backend hỗ trợ, ảnh minh họa thay upload. Build đạt; GET và POST thiếu trường trả form có validation; không chụp màn hình, chưa kiểm chứng trực quan.

- Bổ sung chi tiết chỉnh sửa tòa nhà: breadcrumb theo tòa, ảnh phòng đại diện, tổng phòng/đang thuê/tổng diện tích, trạng thái và SĐT quản lý hiện tại chỉ đọc; nạp lại tóm tắt khi validation lỗi. Chưa có loại hình/tiện ích/upload ảnh tòa riêng; không đổi schema, không chụp ảnh. Build và GET/POST không hợp lệ đạt.

- 08/10/2026: PO đồng ý bổ sung trường lưu diện tích đất và sáu tiện ích chung. Schema v17 additive + backup; EF, form, validation decimal invariant, audit allowlist tích hợp. Kiểm chứng bảo toàn cột cũ/integrity/FK/idempotence trên bản sao, khởi tạo DB rỗng đạt, HTTP lưu/tải lại/chặn âm/chặn sở hữu chéo đạt. Chi tiết tại docs/nang-cap-toa-nha-v17.md. Sidebar giữ nguyên; không chụp ảnh.

- Ba trường Tỉnh/thành phố, Quận/huyện, Phường/xã có dấu sao và validation bắt buộc phía server/client; kiểm tra POST bỏ trống không lưu đạt.

- 08/10/2026: Quản lý phòng theo ảnh: bốn thống kê thực tế, thanh phòng trống, tabs trạng thái, bộ lọc cơ sở/quản lý, tìm mã/tên khách hiện tại, bảng khách/giá/trạng thái, menu giữ thao tác cũ, phân trang client 10/20/50, danh sách/lưới và CSV. Không gán ngừng cho thuê thành bảo trì; sidebar giữ nguyên; không chụp ảnh. Build và HTTP bộ lọc đạt; chưa kiểm chứng trực quan và JS trong trình duyệt.

- 08/10/2026: Thêm phòng theo bố cục ảnh, partial/CSS riêng chỉ nạp Create; giữ Edit và backend cũ. Form có thông tin/giá/trạng thái, sidebar ảnh hướng dẫn thêm sau lưu; chưa hỗ trợ tiện nghi/ghi chú/cọc ở phòng, không tạo trường giả. Build và HTTP form/validation/Edit đạt, không chụp ảnh.

- 08/10/2026: Chỉnh khối Thông tin cơ bản của Thêm phòng theo ảnh: viền, khoảng cách, đơn vị, địa chỉ quận/tỉnh theo cơ sở thực tế và radio Còn trống/Bảo trì. Bảo trì dùng NGUNG_CHO_THUE hiện có, chưa có nghiệp vụ bảo trì riêng. Tầng giữ input số, không tự giới hạn tầng. Không đổi schema, không thêm nghiệp vụ, không chụp màn hình. Build Debug đạt; git diff --check đạt. Chưa kiểm chứng trực quan/mobile trong trình duyệt. Tiện nghi riêng vẫn chưa có; ảnh thêm sau khi lưu theo luồng hiện có. Mã HopDongController thực tế đã có cập nhật DANG_THUE, khác ghi chú Sprint 3 chưa làm trong AGENTS.md.

- 08/10/2026: Làm đẹp dropdown cơ sở ở form thêm phòng: danh sách trắng bo góc, bóng nhẹ, lựa chọn màu be và dấu tick; hỗ trợ phím mũi tên/Home/End/Escape, đóng khi rời focus/bấm ngoài. Select gốc giữ nguyên binding và validation, tự dùng lại giao diện gốc khi JavaScript không chạy. Chỉ sửa CSS/JS, không thêm nghiệp vụ. git diff --check đạt; chưa kiểm chứng trực quan trình duyệt, không chụp màn hình.

- 08/10/2026: Tinh chỉnh dropdown cơ sở theo ảnh mới: nhãn Cơ sở màu nhạt trong ô chọn, tên cơ sở đậm, menu thoáng 41px mỗi mục, nền be và tick màu vàng nâu. Giữ Chọn cơ sở khi chưa chọn vì form tạo phòng yêu cầu một cơ sở cụ thể; không dùng Tất cả cơ sở của bộ lọc. node --check và git diff --check đạt. Chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Tăng cột rc-aside của form thêm phòng từ 300 lên 350px; ở breakpoint <=950px tăng từ 260 lên 300px. Giữ bố cục một cột <=750px. Chỉ chỉnh CSS, không thêm chức năng; chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Chỉnh hiệu ứng nổi cho nút chọn cơ sở theo ảnh: viền xám nhẹ, bo góc 10px, bóng quanh nút và menu, trạng thái mở/focus dùng bóng be nhẹ. Chỉ CSS, giữ chức năng và chiều rộng rc-aside. git diff --check đạt; chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Sửa dropdown cơ sở không hiện giao diện mới ở demo đang chạy. HTTP xác nhận Razor runtime cũ có select#ToaNhaId nhưng thiếu data-room-building, trong khi server đã phục vụ JS mới. Bổ sung selector theo id trong room-create-page để tương thích form đang chạy, không ảnh hưởng Edit/CreateBulk. HTTP xác nhận server phục vụ bản sửa; node --check và git diff --check đạt. Không chụp màn hình, chưa kiểm chứng trực quan trình duyệt.

- 08/10/2026: Tăng thêm chiều rộng rc-aside theo yêu cầu: màn hình lớn 350 → 400px, breakpoint <=950px 300 → 330px; giữ một cột <=750px. Chỉ CSS, không thêm chức năng. git diff --check đạt; chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Tăng tiếp rc-aside theo yêu cầu: màn hình lớn 400 → 450px, breakpoint <=950px 330 → 350px; giữ một cột <=750px. Chỉ CSS. git diff --check đạt; chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Chỉnh lại khối Thông tin cơ bản theo ảnh mới, biểu tượng SVG, bố cục/ô nhập gọn và bỏ nhãn Cơ sở lặp bên trong nút. Tầng có gợi ý từ SoTang của cơ sở, vẫn nhập số nếu chưa khai báo; không thay validation backend. Bảo trì chỉ còn radio disabled, không bind NGUNG_CHO_THUE và có thông báo chưa hỗ trợ. Còn trống và các trường hiện có giữ hoạt động. Không thêm nghiệp vụ, không đổi schema; node --check và git diff --check đạt, chưa kiểm chứng trực quan. Bản demo dùng DLL riêng cần build/khởi động lại để nhận Razor mới; không chụp màn hình.

- 08/10/2026: Cập nhật đúng DLL demo đang phục vụ cổng 5247: dừng PID theo netstat, build runtime demo hiện có và chạy lại với cùng DB/ảnh/keys/pickup. HTTP đăng nhập xác nhận Create trả rc-basic-card, Bảo trì disabled và data-room-building mới. Không chạy updater, không chụp màn hình.

- 08/10/2026: Giá thuê & tiền cọc theo ảnh: giữ input giá thuê hoạt động; thêm ô cọc disabled và khung 4 phí Điện/Nước/Internet/Vệ sinh với checkbox, giá minh họa, cách tính, Thêm phí khác đều disabled và không có name để không gửi/lưu. Thông báo rõ từng mục chưa hoạt động; dịch vụ mặc định hiện có không đổi. CSS responsive. Build runtime demo đạt và khởi động lại cùng DB; HTTP xác nhận card mới, 4 dòng phí và cọc disabled; diff --check đạt. Không chụp màn hình; chưa kiểm chứng trực quan/mobile.

- 08/10/2026: Thêm khung Ghi chú dưới phần giá/phí theo ảnh: biểu tượng, tiêu đề/mô tả, textarea bo góc, nội dung mẫu qua placeholder, dòng chỉ hiển thị cho chủ nhà và 0/500 ký tự. Ô disabled, không có name và không lưu vì form chưa hỗ trợ ghi chú; có thông báo rõ. Không đổi schema/backend. Build demo và diff --check đạt, khởi động lại cùng cấu hình; HTTP xác nhận khung và textarea disabled. Chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Chuyển Ghi chú từ cột chính sang rc-aside dưới Ảnh phòng theo bố cục mẫu. Build lại/khởi động demo cùng DB; HTTP xác nhận Ghi chú nằm trong aside. Ô vẫn disabled, không lưu. Không chụp màn hình.

- 08/10/2026: Thêm nút Tạo nhanh phòng giữa Xuất danh sách và Thêm phòng trên Quản lý phòng, dùng CreateBulk hiện có và giữ toaNhaId đang lọc. Điều kiện quyền ghi PHONG_TRO và có cơ sở giống Thêm phòng. Không thêm nghiệp vụ/schema. Build demo đạt, khởi động lại cùng DB; HTTP xác nhận thứ tự nút và CreateBulk 200. diff --check đạt; không chụp màn hình.

- 08/10/2026: Bỏ toàn bộ khung Lưu ý và liên kết Tạo nhanh nhiều phòng trong sidebar trang Thêm phòng theo ảnh. Nút tạo nhanh ở Quản lý phòng giữ nguyên. Build runtime demo/khởi động lại cùng DB đạt; HTTP xác nhận không còn rc-bulk; diff --check đạt. Không chụp màn hình.

- 08/10/2026: Đổi giao diện Dịch vụ & đơn giá theo ảnh với CSS riêng service-management.css chỉ nạp Index: 4 thống kê từ dữ liệu thật của tòa đang chọn; lịch giá sắp áp dụng khi có; bảng tên/mã, cách tính, giá, đơn vị, phạm vi tòa, trạng thái, hiệu lực và menu thao tác; hai thẻ giải thích. Sidebar/layout không sửa. Giữ chọn tòa, thêm/sửa đơn giá, đặt mặc định, xóa, cấu hình điện nước, hóa đơn và CSRF/quyền hiện có. Xuất danh sách, tìm kiếm, tab lọc, lọc cách tính, sắp xếp, phân trang chỉ có khung disabled và thông báo rõ; không thêm backend/schema. Build runtime demo đạt (cảnh báo ImageSharp/CS8601 có sẵn), khởi động lại cùng DB. HTTP Index/CSS/Manage 200, 4 thống kê, 5 dịch vụ thực tế, token CSRF còn; diff --check đạt, không có diff Views/Shared. Chưa kiểm chứng trực quan/mobile; không chụp màn hình.

- 08/10/2026: Thay mũi tên chữ của Xem đơn giá trong banner sắp áp dụng bằng SVG nét mảnh màu #8D7953 theo ảnh. Chỉ giao diện, sidebar giữ nguyên. Build runtime demo đạt, khởi động lại cùng cấu hình, HTTP xác nhận CSS mới; diff --check đạt. Banner chỉ xuất hiện khi có lịch giá thực tế. Không chụp màn hình.

- 08/10/2026: Nút Xuất danh sách trang Dịch vụ dùng SVG tải xuống nét mảnh và chữ đậm theo ảnh. Nút vẫn disabled vì chưa hỗ trợ xuất; không thêm nghiệp vụ/sidebar. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận icon mới, diff --check đạt. Không chụp màn hình.

- 08/10/2026: Chuyển nút Cấu hình điện nước lên giữa Xuất danh sách và Thêm dịch vụ trong header Dịch vụ & đơn giá; bỏ vị trí cũ, giữ liên kết hóa đơn bên dưới. Giữ toaNhaId, quyền ghi và trạng thái sẵn sàng. Build runtime demo đạt, khởi động cùng cấu hình; HTTP xác nhận nút header và DienNuoc 200. Không đổi sidebar, không chụp màn hình.

- 08/10/2026: Thêm trang tổng quan phòng Details theo ảnh và chuyển liên kết Chi tiết trong Quản lý phòng sang trang này; nút Chỉnh sửa giữ form Edit cũ. Dữ liệu phòng/cơ sở/giá thật; quyền module và phạm vi tòa owner/manager/admin được kiểm tra backend, dữ liệu khác trả 403. CSS riêng, không sửa layout/sidebar, không đổi schema. Các khối khách thuê/hợp đồng/hóa đơn/cọc/phí/tiện nghi chưa kết nối chỉ hiển thị dấu — hoặc nhãn minh họa; tab phụ, tạo hóa đơn trực tiếp, xem hồ sơ/hợp đồng, ghi chú/bảo trì disabled. Khối tài chính/hợp đồng chỉ hiển thị khi có quyền. Link xem hóa đơn theo tòa và lịch sử người ở dùng luồng hiện có. Build demo đạt, HTTP trang chi tiết/CSS 200, liên kết Edit còn và 9 nút preview disabled; diff --check đạt, Views/Shared không có diff. Chưa kiểm chứng trực quan/mobile, không chụp màn hình.

- 08/10/2026: Sửa tên hiển thị trang thành Chi tiết phòng (tiêu đề trang, h1, tab và mô tả) theo yêu cầu; giữ bố cục/sidebar/chức năng. Build demo đạt và đã khởi động lại cùng cấu hình. Không chụp màn hình.

- 08/10/2026: Header Chi tiết phòng có breadcrumb icon nhà / Phòng cho thuê / mã phòng thực tế theo ảnh; liên kết về danh sách giữ cơ sở đang xem. Chỉ thêm nhánh header Details và CSS riêng, sidebar không sửa. Build demo/HTTP breadcrumb đạt, không chụp màn hình.

- 08/10/2026: Đồng bộ breadcrumb Chi tiết phòng với Chi tiết tòa: chữ 16px/600, icon 21px stroke 1.6, gap 12px và cùng màu chữ/phân cách. Chỉ CSS riêng, sidebar giữ nguyên. HTTP xác nhận server phục vụ CSS mới, diff --check đạt; không chụp màn hình.

- 08/10/2026: Bỏ liên kết Quay lại danh sách phòng ở nội dung Chi tiết phòng theo ảnh. Breadcrumb header giữ nguyên. Build runtime demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận không còn rd-back. Không chụp màn hình.

- 08/10/2026: Thay 4 ký hiệu phí dịch vụ trong Chi tiết phòng bằng SVG nét mảnh màu xám: tia sét, giọt nước, Wi-Fi, thùng rác theo ảnh. Chỉ giao diện, giữ trạng thái khung chưa kết nối/sidebar. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận 4 SVG, diff --check đạt. Không chụp màn hình.

- 08/10/2026: Chỉnh form Edit theo ảnh bằng partial _EditRoomForm và CSS room-edit riêng: header/breadcrumb, banner lưu ý, hai cột thông tin/giá/phí/ghi chú và khách thuê/tiện nghi/ảnh, footer lưu/hủy. Sidebar không sửa. Giữ toàn bộ binding/validation/CSRF và POST Edit, select trạng thái hiện có, cơ sở disabled + hidden; JS dropdown bỏ qua select disabled. Giữ nguyên image manager và endpoint upload/reorder/delete. Cọc, phí dịch vụ, thêm phí, ghi chú, tiện nghi chỉ là khung disabled không gửi/lưu; khách thuê/hợp đồng/cọc/công nợ chưa kết nối hiển thị —, không dùng dữ liệu mẫu. Giá mẫu phí chỉ placeholder. Build demo đạt, khởi động lại cùng DB; HTTP xác nhận form/breadcrumb/endpoint ảnh, POST dữ liệu không hợp lệ trả validation và không lưu; Create vẫn hiển thị partial cũ. node --check/diff --check đạt; chưa kiểm chứng trực quan/mobile/upload trong trình duyệt, không chụp màn hình.

- 08/10/2026: Bỏ liên kết Quay lại chi tiết phòng trên form Edit theo ảnh; breadcrumb và nút Hủy giữ nguyên. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận không còn re-back, diff --check đạt. Không chụp màn hình.

- 08/10/2026: Giao diện Thêm dịch vụ theo ảnh: 4 khối thông tin/cách tính/phạm vi/hiệu lực, tóm tắt cập nhật client từ dữ liệu nhập, giải thích công thức, footer lưu/hủy, breadcrumb riêng. Sidebar không đổi. Giữ tên/cách tính (3 loại hiện có)/đơn giá nguyên đồng/đơn vị/chọn một tòa/mặc định phòng mới và POST/CSRF/validation hiện có. Mã tự sinh sau lưu, không lấy mã mẫu. Chọn nhiều tòa, phòng cụ thể, hẹn hiệu lực, chọn trạng thái, ghi chú chưa hỗ trợ và disabled/ghi rõ; trạng thái/ngày theo backend đang áp dụng từ ngày khai báo. Không thêm backend/schema. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận form, 3 cách tính, tóm tắt, CSS 200; POST không hợp lệ trả validation không lưu. node --check/diff --check đạt. Chưa kiểm chứng trực quan/client interaction/mobile; không chụp màn hình.

- 08/10/2026: Bỏ liên kết quay lại Dịch vụ & đơn giá phía trên tiêu đề Thêm dịch vụ theo ảnh. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận không còn sc-back. Không chụp màn hình.

- 08/10/2026: Xếp Theo tiêu thụ/Cố định/Theo đầu người thành một hàng trên form Thêm dịch vụ; <=750px giữ một cột. Chỉ CSS, không đổi xử lý. HTTP xác nhận CSS mới; không chụp màn hình.

- 08/10/2026: Tinh chỉnh khối cách tính/đơn giá theo ảnh: ô chọn cao 72px, radio nét mảnh với nền be khi chọn, viền vàng nâu, đơn vị có mũi tên và thanh công thức nhạt. Giữ 3 cách tính một hàng theo yêu cầu trước, không bỏ Theo đầu người. Chỉ CSS, chức năng không đổi; HTTP xác nhận CSS mới. Không chụp màn hình.

- 08/10/2026: Ô Theo tiêu thụ dùng SVG đồng hồ nét mảnh thay ký hiệu chữ, nền trắng/viền xám/spacing theo ảnh. Giữ ba cách tính một hàng và xử lý radio. Build demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận icon mới. Không chụp màn hình.

- 08/10/2026: Sửa bố cục/kích thước ô cách tính theo làm rõ của người dùng: grid radio 15px + nội dung, icon absolute góc phải, cao tối thiểu 82px, padding dành riêng cho icon; cột tóm tắt 290px (260px <=1000) để tăng không gian ba ô cùng hàng. Mobile giữ một cột/ô cao 72px. Chỉ CSS; HTTP xác nhận bản sửa, không chụp màn hình.

- 08/10/2026: Đồng bộ rõ cả ba ô Theo tiêu thụ/Cố định/Theo đầu người: cùng chiều cao, padding, cột radio/nội dung và vị trí icon; grid stretch giữ kích thước ngang nhau. Giữ ba cách tính hiện có và mobile một cột. Chỉ CSS; HTTP xác nhận bản mới. Không chụp màn hình.

- 08/10/2026: Đồng bộ hoàn chỉnh ba ô cách tính: cả ba icon dùng SVG cùng kích thước/nét/màu/vị trí, cùng trạng thái chọn nền be/viền vàng nâu. Giữ kích thước, font, padding và ba ô cùng hàng đã chỉnh. Build runtime demo đạt, khởi động lại cùng cấu hình; HTTP xác nhận 3 SVG. Không sửa sidebar/chức năng, không chụp màn hình.

- 08/10/2026: Chỉnh kích thước/cỡ chữ/bố cục ba ô cách tính theo ảnh mới: min-height 70px, title 12px/600/17px, mô tả 10px/16px, padding 11px, radio 15px, icon góc phải 15px, gap 10px và 3 cột bằng nhau. Mobile một cột. Chỉ CSS, không đổi nội dung/nghiệp vụ/sidebar. HTTP xác nhận server phục vụ CSS mới; diff --check đạt, chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Giảm chiều cao tối thiểu ba khung cách tính từ 70 xuống 64px và padding dọc từ 11 xuống 8px theo yêu cầu chỉnh chiều cao; giữ cỡ chữ/bố cục. HTTP xác nhận CSS mới. Không chụp màn hình.

- 08/10/2026: Làm đẹp menu Đơn vị tính ở Thêm dịch vụ: thay popup datalist khi JS hoạt động bằng menu trắng full-width, bo góc/bóng nhẹ, nền be/tick khi chọn; giữ input gốc và đơn vị tự nhập, name/validation/preview không đổi. Hỗ trợ phím mũi tên/Enter/Escape/Tab, đóng khi bấm ngoài. Không có backend mới. node --check/diff --check đạt; HTTP xác nhận JS mới. Chưa kiểm chứng tương tác trực quan, không chụp màn hình.

- 08/10/2026: Sửa nguyên nhân chiều cao ba ô cách tính không cập nhật: selector :has(#sc-meter/fixed/person) có specificity ID giữ min-height 82px và ghi đè quy tắc 64px. Chuyển selector về :has(input) cùng specificity để quy tắc cuối 64px/padding 8px có hiệu lực cho cả ba. HTTP xác nhận không còn selector ID và CSS 64px được phục vụ; diff --check đạt. Chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Xóa các khai báo min-height 82px cũ trong CSS ba ô cách tính theo yêu cầu. Giữ quy tắc cuối 64px. HTTP xác nhận CSS không còn 82px, vẫn có 64px. Không chụp màn hình.

- 08/10/2026: Cân đối ba khung cách tính: 3 cột bằng nhau, cao tối thiểu 72px, gap/padding 12px, radio/icon 15px, chữ 12px và mô tả 10px, cùng hàng/cùng chiều cao; mobile một cột. Gộp các override cách tính cũ thành một khối thống nhất để tránh ghi đè chiều cao. Giữ CSS menu đơn vị và xử lý hiện có. HTTP xác nhận CSS mới; diff --check đạt. Không chụp màn hình.

- 08/10/2026: Giảm các ô sc-methods từ min-height 72 xuống 60px, padding dọc 8px, căn icon top 9px. Giữ ba ô cùng hàng và nội dung. HTTP xác nhận CSS mới; không chụp màn hình.

- 08/10/2026: Thu thấp sc-methods xuống min-height 52px, padding dọc 6px. Căn radio 15px với tiêu đề line-height 15px cùng hàng, mô tả phía dưới line-height 14px/gap 3px, icon top 6px. Giữ ba lựa chọn và responsive. HTTP xác nhận CSS mới, diff --check đạt; chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Căn ba ô cách tính theo ảnh: padding 10px dọc/11px trái, radio ngang tiêu đề, mô tả thẳng dưới tiêu đề cách 4px, icon góc phải top 11px/right 12px; giữ min-height 52px, tự giãn khi chữ xuống dòng. Chỉ CSS. HTTP xác nhận bản mới; không chụp màn hình.

- 08/10/2026: Sửa căn ba ô bằng grid 3 cột rõ ràng: radio cột 1/hàng 1, khối tiêu đề+mô tả cột 2/hàng 1, SVG cột 3/hàng 1. Bỏ icon absolute và override căn cũ, min-height 58px/padding 10px. HTTP xác nhận CSS grid mới được phục vụ; diff --check đạt. Chưa kiểm chứng trực quan, không chụp màn hình.

- 08/10/2026: Ẩn nút tăng/giảm mặc định của input số Đơn giá ở form Thêm dịch vụ theo ảnh; giữ input number và validation hiện có. Chỉ CSS, HTTP xác nhận bản mới. Không chụp màn hình.

- 08/10/2026: Khối Phạm vi áp dụng theo ảnh: tòa đang chọn hiển thị dạng thẻ trong menu riêng bo góc, chọn tòa giữ select/binding gốc; phạm vi mặc định dùng nút hình tròn giữ boolean cũ. Vẫn chỉ một tòa mỗi lần, không tạo dữ liệu/thẻ mẫu nhiều tòa; không gán tất cả phòng hiện có, không mở chọn phòng cụ thể. Có nhãn giải thích giới hạn hiện có. JS hỗ trợ bàn phím/đóng ngoài; node --check/diff --check đạt, HTTP xác nhận JS mới. Không sửa sidebar/schema/backend, chưa kiểm chứng trực quan, không chụp màn hình.

## 08/10/2026 — Đồng bộ origin/dev vào feature/fix/UI

- Fetch dev tới 8cb2167 và merge vào nhánh UI, giữ giao diện hiện có. Giải quyết xung đột AccountReuseSchema/DatabaseUpdates/tien-do bằng bước đối chiếu schema v19 vì hai nhánh dùng v17 khác nhau; giữ nội dung tiến độ hai phía.
- Build Debug đạt. Test lọc MeterReadingListTests/PhongTroNhatKyTests/ContractActivationTests: 113/113 PASS. Lượt đầu 111/113 do test cũ chỉ xóa marker18 và kỳ vọng18; cập nhật kịch bản cho v19 rồi chạy lại đạt.
- Có hai ca mới từ UI v17 thiếu bảng chỉ số và dev v18 thiếu cột tòa: backup, integrity/FK, giữ dữ liệu, chạy lại không đổi. Thử updater trên bản sao mới data/sync-dev-20261008-070900/verification.sqlite: bảo toàn 264 dòng trong 34 bảng cũ; integrity/FK đạt; chạy lặp/check schema đạt.
- Không chạy updater/initializer trên DB local/demo gốc. App cổng5247 đã dừng trước build và giữ dừng để tránh chạy mã mới với DB cũ; cần cập nhật DB có chủ đích trước khi chạy lại. Không push, không chụp màn hình. Cảnh báo ImageSharp, CS8601, NU1900/xUnit2013 có sẵn.

### 08/10/2026 — Chuẩn bị DB kiểm thử S3-06

- Tạo bản sao DB demo Sprint 2 tại `data/s306-demo/20261008-225500/s306.sqlite`; giữ nguyên toàn bộ 449 dòng nguồn, tài khoản và mật khẩu cũ. Schema nguồn v20, không chạy updater.
- Thêm tòa 5: 4 phòng đủ dữ liệu (3.780.000 / 3.980.000 / 4.060.000 / 4.220.000 đ), A105 thiếu nước; tòa 6: 50 phòng đủ dữ liệu, mỗi phòng 4.060.000 đ. Giá điện 4.000 đ tại ngày chốt 31/10, giá mới 6.000 đ từ 01/11 để kiểm tra lịch sử.
- `.env.local` chuyển sang bản sao; cấu hình cũ lưu `env.before.txt`, DB trước test lưu `s306-ready.backup.sqlite`. Hướng dẫn trong `HUONG-DAN-TEST.md` cùng thư mục. Dữ liệu/credential đều bị ignore.
- Kiểm tra: integrity_check=ok, FK không lỗi; CLI `--check-database` thành công. Chưa phát hành hóa đơn thử; chưa xác minh HTTP vì sandbox chặn localhost (WinError 10013). Web kiểm tra đã dừng, người dùng chạy `run.bat` để test.

- Giao diện kiểm tra hóa đơn tháng: phân trang 15 dòng cho danh sách sẵn sàng, bỏ qua, đã có hóa đơn và lịch sử phát hành; các bảng chuyển trang độc lập. Kiểm tra cú pháp JavaScript và git diff đạt; chưa kiểm tra trực quan trình duyệt.

### 09/10/2026 — Giao diện tạo nhanh phòng theo mẫu

- Đổi riêng trang `PhongTro/CreateBulk`: thẻ cài đặt chung, thông tin bổ sung thu gọn, cột xem trước và thanh hành động; giữ nguyên sidebar.
- Xem trước cập nhật theo tòa nhà, số tầng, số phòng mỗi tầng, diện tích, giá, sức chứa và trạng thái. Mã xem trước giữ đúng quy tắc backend (tầng 1..N + hai chữ số); giới hạn hiển thị 100 dòng, ghi rõ tổng loạt.
- Giữ nguyên model, POST, kiểm tra quyền, CSRF và quy tắc tạo phòng. Không thêm loại phòng/prefix tùy chỉnh hoặc thay đổi schema.
- Xác minh: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/sprint2-runtime/20261009-105552-d35514` thành công (0 lỗi; cảnh báo ImageSharp/CS8601 có sẵn). Mở form bằng tài khoản chủ nhà demo; nhập 1 tầng × 5 phòng, xem trước đúng 101–105, giá 4.500.000 đ/tháng. Browser 360px: document width 359px, không tràn ngang.
- Chưa thử lưu loạt mới trong phiên này; không thay đổi dữ liệu phòng demo. Chưa nghiệm thu Safari/thiết bị cảm ứng thực.
- Bổ sung theo mẫu breadcrumb trang CreateBulk: biểu tượng nhà, liên kết “Phòng cho thuê”, nhãn “Tạo nhanh phòng”; không có dấu phân cách, không sửa sidebar.
- Đồng bộ breadcrumb CreateBulk với trang thêm phòng: cùng class rc-breadcrumb, icon, dấu /, màu chữ, cỡ chữ và khoảng cách; nhãn cuối là Tạo nhanh phòng.
- Bỏ liên kết quay lại “Danh sách phòng & căn hộ” ở phần nội dung CreateBulk theo yêu cầu; giữ breadcrumb và nút Hủy.
- Chỉnh nhãn Tạo đồng loạt theo ảnh: nền kem bo góc, SVG tia sét màu vàng, chữ đậm màu xám.
- Đổi icon cài đặt chung thành SVG cửa, nét xám đậm, nền xám bo góc theo ảnh mẫu.
- Chỉnh thẻ Thông tin bổ sung theo mẫu (icon thanh trượt, nhãn Không bắt buộc, mô tả, Thêm thông tin và chevron; mặc định đóng). Đưa sức chứa bắt buộc về cài đặt chung, giữ nguyên binding/validation.
- Thêm checkbox Tạo tiếp loạt phòng khác sau khi hoàn tất theo mẫu. Khi chọn, POST thành công chuyển về CreateBulk và giữ tòa nhà/số tầng; không chọn giữ redirect danh sách như trước. Mã trùng vẫn bị backend từ chối.
- Đổi dropdown trạng thái CreateBulk thành menu nổi bo góc, bóng nhẹ, hover và dấu chọn; giữ select gốc để binding/validation, hỗ trợ phím mũi tên/Home/End/Escape và đóng khi click ngoài.
- Thu gọn bộ chọn trạng thái: menu rộng bằng nút 190px, nút cao khoảng 36px, dòng lựa chọn khoảng 34px, giảm khoảng cách/bóng; điện thoại vẫn dùng chiều rộng đầy đủ.
- Căn khoảng đệm footer 24px ngang/20px dọc (mobile 16px), dòng mô tả checkbox thẳng với nhãn và cách 6px.
- Thêm nhập cọc dự kiến, sức chứa, tiện ích/nội thất/ghi chú cho loạt. Dùng cột hiện có tien_coc_du_kien/so_nguoi_toi_da/mo_ta; không đổi schema. Form thông báo mô tả có thể hiển thị công khai. Build PASS; kiểm tra HTTP trên bản sao: cọc âm từ chối và không tạo phòng, tạo 2 phòng lưu đúng cọc 2.000.000/sức chứa 3/nội dung mô tả; integrity/FK PASS. CSDL demo đang dùng giữ tòa 5 chưa có phòng.
- Số người tối đa trong CreateBulk không bắt buộc theo yêu cầu: bỏ dấu *, bỏ Required riêng model bulk, để trống lưu sức chứa 1; giá trị nhập vẫn phải lớn hơn 0. Form thêm phòng riêng giữ quy tắc cũ.

### 09/10/2026 — Giao diện thêm tòa nhà theo ảnh mẫu
- Trang TaoToaNha: 5 thẻ thông tin cơ bản/địa chỉ/quy mô/người phụ trách/trạng thái và ghi chú, cột tóm tắt cập nhật trực tiếp, khung ảnh và lưu ý thêm phòng; breadcrumb riêng. Sidebar giữ nguyên.
- Giữ nguyên POST, CSRF, quyền và dữ liệu lưu. Các trường chưa hỗ trợ (mã tự nhập/loại hình/ảnh/liên hệ riêng) chỉ đọc hoặc vô hiệu hóa có giải thích; không đổi schema. Tòa mới vẫn có 0 phòng và đang hoạt động.
- Build thành công (0 lỗi, cảnh báo ImageSharp/CS8601 có sẵn). Mở form bằng chủ nhà demo; DOM có 5 nhóm và tóm tắt; viewport 360px, document width 345px, không tràn ngang. Chưa thử lưu mới/Safari/thiết bị cảm ứng trong phiên giao diện này.
- 10/10/2026: Đồng bộ breadcrumb thêm tòa nhà với trang thêm phòng (rc-breadcrumb, icon, cỡ chữ/màu/khoảng cách và dấu /), giữ tên Không gian quản lý / Tòa nhà / Thêm tòa nhà.
- 10/10: Chỉnh thẻ thông tin cơ bản thêm tòa theo mẫu: icon SVG, tên/mã tỉ lệ 2.15:1, thẻ radio loại hình với viền vàng; loại hình chỉ xem trước (thông báo rõ), mã vẫn tự tạo sau lưu. Không đổi schema.
- 10/10: Chỉnh thẻ địa chỉ theo mẫu: icon vị trí, tỉnh/phường cùng hàng, số nhà và địa chỉ bổ sung toàn hàng. Giữ Quận/Huyện bắt buộc và các binding hiện có; địa chỉ bổ sung chưa hỗ trợ lưu riêng.
- 10/10: Thẻ Quy mô theo mẫu: icon tầng SVG, hướng dẫn dưới từng ô, đơn vị bên phải, thanh lưu ý nền xám nhạt. Tổng số phòng vẫn chỉ đọc theo dữ liệu thực tế; không tự tạo phòng.
- 10/10: Chỉnh thẻ người phụ trách/liên hệ theo hình: SVG người, mô tả, 2 cột, ô 40px, email không bắt buộc. Giữ quyền phân công quản lý và trạng thái chỉ đọc/vô hiệu hóa liên hệ chưa hỗ trợ lưu riêng.
- 10/10: Thẻ trạng thái/ghi chú theo mẫu: SVG điều chỉnh, select trạng thái (tòa mới chỉ Đang hoạt động), hướng dẫn và textarea cao 78px. Giữ hành vi lưu tòa mới đang hoạt động.
- 10/10: Thẻ tóm tắt theo mẫu: header/icon, khối tên, quy mô nền xám có vạch ngăn, các dòng thông tin, trạng thái vàng nhạt, footer chưa lưu. Hiển thị số phòng thực 0 và mã tạo sau lưu; loại hình theo lựa chọn xem trước.
- 10/10: Thẻ ảnh tòa nhà theo mẫu: khung nét đứt nền nhạt, icon ảnh/vòng tròn, nút upload, JPG/PNG 5MB và hướng dẫn 4:3. Nút vẫn vô hiệu hóa và có thông báo vì chưa hỗ trợ lưu ảnh tòa riêng.

### 10/10/2026 — Giao diện cấu hình điện & nước
- Đổi riêng DienNuoc: header/tabs, phạm vi áp dụng, thẻ điện/nước/hiệu lực, cột tóm tắt và ví dụ, footer lưu/hủy. Sidebar không thay đổi.
- Giữ POST/binding/CSRF/trường chống ghi đồng thời/cấu hình hiện tại và chờ. Nút cách tính đồng bộ select gốc, JS validation giữ nguyên; chỉ hỗ trợ theo chỉ số/theo người như backend. Ngày áp dụng kỳ tiếp theo chỉ đọc; không mở rộng schema/quy tắc.
- Ví dụ dùng BigInt: 100 kWh, 5 m³ hoặc 2 người; giá nhập cập nhật ngay, không ghi dữ liệu hóa đơn.
- Build PASS, JS syntax PASS, HTTP chủ nhà 200 và kiểm tra binding/CSRF/sidebar PASS. Browser kiểm tra trực quan bị unavailable nên chưa xác minh 360px/ảnh hiển thị trong phiên này; CSS có breakpoint mobile. Chưa gửi POST thay giá demo.
- 10/10: Thẻ phạm vi áp dụng theo mẫu: SVG tòa, select tòa hiện tại, thẻ tất cả phòng/chọn cụ thể và thanh danh sách. Chọn cụ thể vô hiệu hóa có giải thích; bảo toàn phạm vi chung và ưu tiên cấu hình riêng. Không hiển thị số phòng giả.
- 10/10: Thẻ tiền điện theo mẫu: SVG tia sét, badge trạng thái thực, lựa chọn tính phí (bậc thang/khoán phòng chưa hỗ trợ nên disabled; theo người vẫn giữ), hàng đơn giá/đơn vị khóa, công thức trực tiếp. Cấu hình hiện tại/chờ trong details, giữ nguyên dữ liệu/validation.
- 10/10: Thẻ nước theo mẫu: SVG giọt nước, 3 ô cách tính (khoán phòng disabled), đơn giá/đơn vị cạnh nhau và công thức trực tiếp. Giữ theo người hoạt động, binding/validation nguyên bản.
- 10/10: Thời điểm hiệu lực theo mẫu: SVG lịch, ngày và kỳ cùng hàng, khối cảnh báo nền kem. Ngày/kỳ vẫn cố định theo kỳ kế tiếp, không thay đổi nghiệp vụ.
- 10/10: Tóm tắt áp dụng theo mẫu: số phòng truy vấn thật sau kiểm tra sở hữu (GET/POST), nhãn cách tính theo lựa chọn, đơn giá, ngày hiệu lực kèm SVG lịch. Không đổi schema/dữ liệu.
- 10/10: Thẻ ví dụ điện/nước theo mẫu: 1250→1350 kWh, 120→125 m³, formula/tiền riêng và tổng nền kem; khi theo người ẩn chỉ số minh họa và dùng 2 người. Tiền BigInt theo giá nhập, dữ liệu chỉ minh họa.

### 10/10/2026 — Giao diện tin đăng cho thuê theo mẫu
- QuanLy: 4 thẻ đếm trạng thái, thanh phòng trống, bảng ảnh/giá/diện tích/ngày đăng/trạng thái, tìm kiếm/lọc cơ sở-trạng thái/sắp xếp/phân trang 10-20-50 dòng. Sidebar giữ nguyên.
- Bổ sung dữ liệu view từ phòng/tin/ảnh hiện có; bảo toàn kiểm tra sở hữu. Ngày hiển thị UTC+7, tiền vi-VN. Chưa có thống kê quan tâm nên ghi rõ chưa có dữ liệu, không tạo số giả.
- Giữ các action xem/sửa/đăng lại/gỡ và CSRF. Nút Tạo tin đăng lọc phòng chưa có tin để chọn; phòng chưa trống giữ chặn như trước. Không đổi schema hoặc dữ liệu.
- Build/JS syntax PASS; HTTP owner 200, action/CSRF/sidebar có đủ, tòa của chủ khác không xuất hiện. Chưa kiểm tra trực quan 360px/Safari hoặc gửi POST gỡ tin trong phiên này.
- 10/10: Ba menu lọc tin đăng chuyển thành dropdown nổi bo góc/bóng nhẹ/hover/dấu chọn; đồng bộ select gốc, tab trạng thái và nút tạo tin. Có keyboard mũi tên/Home/End/Escape, đóng khi rời focus/click ngoài.
- 10/10: Cột thao tác theo mẫu: Xem tin + bút chì SVG, menu Trạng thái phía dưới chứa action gỡ/tạm ẩn; giữ POST/CSRF/confirm. Các phòng chưa trống và tin nháp vẫn giữ action hiện có.

### 10/10/2026 — Giao diện tạo tin theo mẫu
- Form Tao: thẻ nội dung, giá/thông số/dịch vụ, địa chỉ và ảnh; cột xem trước trực tiếp, liên hệ, hiển thị; footer hủy/lưu nháp/đăng. Breadcrumb riêng, sidebar giữ nguyên.
- Giá/cọc/tầng/địa chỉ/ảnh lấy từ phòng và tòa hiện có. Ảnh chỉnh tại hồ sơ phòng; chưa hỗ trợ thay liên hệ riêng/thời hạn tùy chọn. Giữ title/description POST và intent, quyền/CSRF; không đổi schema, không nhập dữ liệu giả.
- Build/JS syntax PASS; GET chủ nhà phòng 4 HTTP 200, binding/intent/CSRF/sidebar đủ. Chưa kiểm tra UI trực quan/mobile/Safari hoặc gửi POST đăng mới trong phiên này.

### 10/10/2026 — Chọn phòng khi tạo tin
- Nút Tạo tin đăng trên QuanLy mở /TinDang/Tao. GET không có phòng tự chọn phòng hợp lệ đầu tiên; form chọn tòa/phòng tải lại thông tin phòng tương ứng. Nếu không có phòng hợp lệ, hiển thị trang hướng dẫn.
- Danh sách chọn backend chỉ tòa đang hoạt động của chủ nhà hoặc giao quản lý, phòng trống chưa có tin đang hiển thị; khi sửa giữ phòng hiện tại. POST vẫn kiểm tra sở hữu/trạng thái/trùng tin/CSRF như cũ. Cảnh báo khi đổi phòng có nội dung chưa lưu.
- Build/JS syntax PASS. HTTP kiểm tra nút liên kết, form lựa chọn, loại phòng đang thuê/đang đăng/chủ khác, đổi phòng và CSRF PASS; khách thuê bị 403. Không gửi POST đổi dữ liệu demo trong phiên này.
- 10/10: Thẻ giá/thông số tạo tin theo mẫu: SVG ví, 3 ô giá/cọc/diện tích, tiện nghi checkbox chỉ đọc tham khảo từ mô tả (nêu rõ chưa lưu riêng), giữ bảng dịch vụ và thông tin phòng thực.
- 10/10: Thẻ ảnh phòng tạo tin theo mẫu: icon ảnh, khung nét đứt upload, số ảnh, gallery 3 cột, nhãn đại diện và ô thêm ảnh. Liên kết quản lý ảnh tại hồ sơ phòng, giữ giới hạn 8 ảnh/backend hiện có.
- 10/10: Xem trước tin theo mẫu: ảnh lớn/số ảnh, giá, diện tích/sức chứa/tầng, địa chỉ SVG, mô tả thu gọn/Xem thêm, cọc và liên hệ chủ nhà thực lấy sau kiểm tra sở hữu. Build/JS syntax kiểm tra; không đổi schema.
- 10/10: Thẻ liên hệ/hiển thị tạo tin theo mẫu: tên/số chủ nhà chỉ đọc, checkbox Zalo disabled có chú thích, đăng ngay/hẹn lịch disabled, select 30 ngày và ngày hết hạn. Không thêm chức năng chưa hỗ trợ hoặc thay nghiệp vụ.
- 10/10: Hai menu chọn cơ sở/phòng chuyển thành dropdown nổi, bo góc/bóng/hover/dấu chọn, đồng bộ select gốc và điều hướng khi đổi; hỗ trợ keyboard/đóng ngoài. JS syntax PASS.
- 10/10: Mở tạo tin từ danh sách mặc định Chọn tòa nhà/Chọn phòng, chưa cho submit đến khi chọn. Mở trực tiếp phòng hoặc sửa tin giữ lựa chọn hiện tại; đổi tòa tải phòng hợp lệ.
- 10/10: Chọn tòa không tự chọn phòng; reset ô phòng về Chọn phòng cho thuê và chặn lưu đến khi chọn phòng. Chọn phòng mới tải chi tiết phòng tương ứng; sửa tin giữ lựa chọn hiện có.
## 10/10/2026 — Cảnh báo hợp đồng sắp hết hạn cho khách thuê

- Đã thêm số ngày còn lại từ hôm nay theo múi giờ Việt Nam vào model danh sách/chi tiết hợp đồng; cảnh báo chỉ hiển thị khi còn `> 0` và `< 30` ngày. Đúng 30 ngày, trên 30 ngày và hợp đồng đã hết hạn không cảnh báo.
- Trang `HopDongCuaToi/Details` có banner màu cảnh báo theo bố cục ảnh mẫu, hiển thị số ngày còn lại và ngày kết thúc; danh sách hợp đồng dùng cùng quy tắc lọc và số ngày.
- Đã thêm test biên trong `HopDongCuaToiTests`: 29, 1, 30, 31, 0, âm ngày và ngày kết thúc thiếu. Không đổi schema hoặc database local.
- Xác minh: `git diff --check` đạt. Chưa chạy được build/test hoàn chỉnh vì tiến trình web hiện hữu đang khóa `QL_PhongTro.dll/.exe`; thử output cách ly cần restore nhưng môi trường bị từ chối đọc `NuGet.Config`. Cần dừng tiến trình đó hoặc chạy terminal có quyền phù hợp rồi chạy lại test.
- Xac minh bo sung sau khi dung dung tien trinh QL_PhongTro.exe PID 11296 dang khoa output: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~HopDongCuaToiTests` PASS 18/18. Canh bao ImageSharp license va xUnit2013 la canh bao san co.
- Cap nhat giao dien xem PDF hop dong theo mau: them route `PreviewPdf`/`PdfPreviewFile`, trang `Views/HopDongCuaToi/PreviewPdf.cshtml`, khung xem truoc PDF, header, file card, nut quay lai va nut tai xuong; nut tu danh sach/chi tiet chuyen sang Xem PDF. Build cach ly `data/pdf-preview-check-runtime` PASS 0 loi; canh bao ImageSharp/CS8601 da co san. Khong doi schema/database.
# Cập nhật 11/10/2026 — ADMIN local trong run.bat

- `run.bat` gọi `--create-local-admin` bằng profile Development sau khi kiểm tra schema và trước khi chạy web. Tài khoản dùng email theo yêu cầu; mật khẩu được băm BCrypt bởi initializer hiện có.
- ADMIN đã tồn tại được giữ nguyên, không đặt lại mật khẩu. SĐT mặc định `0900000000`, có thể thay bằng `LocalAdmin__Phone` trong môi trường hoặc `.env.local`; SĐT trùng sẽ khiến script dừng.
- `run.bat --check-only` không tạo tài khoản. Không đổi schema.
- Đã kiểm tra profile `http`, luồng CLI và `git diff --check`; chưa chạy script trên DB thực tế.

## 11/10/2026 — Bộ CSDL test Sprint 3 theo yêu cầu

- Tạo bộ SQLite mới riêng trong `data/s308-demo/`, tài khoản chủ nhà theo yêu cầu; không sửa DB local hiện có. Credential và hướng dẫn chi tiết chỉ ở `data/` bị Git ignore.
- Dữ liệu gồm yêu cầu đã duyệt, hợp đồng/người ở ghép, hợp đồng còn 20 ngày, lịch sử chỉ số ba kỳ, phòng thiếu chỉ số nước, hóa đơn nháp/đã phát hành và tòa 50 phòng đủ chỉ số.
- Chạy lại web bằng `data/start-s3-test.bat`, cổng 5251; hướng dẫn theo story ở `data/HUONG-DAN-TEST-S3.md`.
- Xác minh: build output riêng thành công; SQLite integrity_check và foreign_key_check đạt; đăng nhập chủ nhà thành công; trang hợp đồng, form tạo từ yêu cầu, hóa đơn tháng và tiến độ chỉ số trả HTTP 200. Chưa nghiệm thu toàn bộ AC, responsive 360px hoặc thời gian phát hành 50 phòng.

## 11/10/2026 — Chuyển dữ liệu test vào CSDL mặc định

- Theo yêu cầu mới của người dùng, dùng `QL_PhongTro/Data/local-dev.sqlite` và `run.bat` thông thường, cổng 5247. Khi kiểm tra, file DB mặc định chưa tồn tại và không có cấu hình DatabasePath khác.
- Đã tạo DB mặc định từ dữ liệu test đã chuẩn bị; 102 phòng, đủ các tình huống Sprint 3. Tài khoản chủ nhà theo yêu cầu được giữ nguyên; tạo ADMIN bằng CLI hiện có theo yêu cầu trước đó, không reset tài khoản cũ.
- Đã dừng web test cổng 5251. SQLite integrity/FK đã kiểm tra đạt; chưa chạy lại web mặc định.

## 11/10/2026 — Thêm ảnh mẫu vào bản ghi mặc định

- Đã thêm 306 ảnh phòng (3 ảnh/phòng cho 102 phòng), phân bổ luân phiên 10 ảnh do người dùng cung cấp; thumbnail qua RoomImageStore hiện có.
- Đã thêm ảnh CCCD mẫu trước/sau cho 60 hồ sơ khách thuê còn thiếu ảnh, xử lý qua GiayToImageStore và lưu private ngoài wwwroot. Không đổi dữ liệu giấy tờ hoặc thông tin tài khoản.
- Có backup SQLite trước khi nhập trong data/ (Git ignore). Kiểm tra toàn bộ đường dẫn ảnh/thumbnail có file, giới hạn 8 ảnh/phòng, integrity_check và foreign_key_check đều đạt. Chưa kiểm tra hiển thị qua trình duyệt.

## 11/10/2026 — Cho nhập chỉ số đầu kỳ khi thiếu tham chiếu

- Trang chốt chỉ số hiển thị ô chỉ số đầu kỳ điện/nước khi chưa có kỳ trước hoặc bàn giao. Cho nhập cùng chỉ số cuối kỳ, không mặc định đầu kỳ bằng 0.
- Backend chỉ nhận mốc người dùng nhập khi không có lịch sử; kiểm tra không âm, tối đa 3 số thập phân, giới hạn chỉ số và cuối >= đầu. Lưu đầu/cuối cùng transaction hiện có; tải lại sử dụng mốc đầu đã lưu và không cho giả mốc mới để bỏ qua kiểm tra giảm chỉ số.
- Giữ nguyên backend phân quyền, CSRF, khóa lạc quan, xác nhận bất thường và khóa kỳ sau phát hành. Không đổi schema hoặc DB thực tế.
- Kiểm thử nhóm MeterReading/MeterUsageWarning: 100/101 đạt lần đầu, ca cũ còn kỳ vọng lỗi DienMoi được cập nhật thành DienDau và chạy lại PASS 1/1. Build thành công; git diff --check đạt. Chưa kiểm tra trực quan 360px trên trình duyệt.

## 11/10/2026 — Làm lại dữ liệu mẫu mặc định còn 36 phòng

- Theo yêu cầu trực tiếp, thay bộ dữ liệu test bằng 36 phòng (3 tòa x 12), 24 hợp đồng bắt đầu hôm nay, 6 yêu cầu đã duyệt. Giữ tài khoản/hash đăng nhập, ảnh phòng và hồ sơ mẫu; không đổi schema.
- Không có hóa đơn phát hành, bản chốt hoặc hợp đồng lịch sử gây cảnh báo kỳ cũ. Có chỉ số bàn giao điện/nước cho 21 hợp đồng; 3 hợp đồng thiếu mốc đầu để thử chức năng nhập đầu kỳ mới. Kỳ hiện tại chưa khóa.
- Đã sao lưu toàn bộ DB trước thay trong data/; đường dẫn và hướng dẫn mới ở data/HUONG-DAN-36-PHONG.md. Kiểm tra schema CLI, integrity/FK, số phòng và toàn bộ file ảnh đạt.

## 11/10/2026 — Chỉnh dịch vụ ngay tại trang sửa phòng

- Thay bảng giá minh họa ở /PhongTro/Edit bằng dịch vụ và giá thực tế của phòng. Cho lưu giá riêng (để trống dùng giá chung), thêm dịch vụ đã khai báo trong tòa, ngừng từ kỳ sau hoặc áp dụng lại ngay tại trang.
- Dùng lại service/endpoint nghiệp vụ hiện có, form POST độc lập có CSRF; lưu xong quay lại phần phí dịch vụ của phòng. Các thao tác dịch vụ lưu riêng với thông tin phòng; UI nhắc lưu thông tin phòng trước khi thao tác dịch vụ. Cách tính tiếp tục theo cấu hình cấp tòa.
- Kiểm tra sở hữu và quyền backend giữ nguyên; không đổi schema hay DB. Build PASS. Test HTTP mở trang sửa, khớp form/giá, lưu giá riêng không ảnh hưởng phòng khác, ngừng dịch vụ và chặn chủ nhà khác PASS 1/1 (HttpSingleAndBulkRoomsReceiveDefaultsAndRenderDistinctSelections). git diff --check đạt. Chưa kiểm tra trực quan 360px.
## 11/10/2026 — Sửa chữ tiếng Việt ở trang sửa phòng

- Sửa dòng bị mất dấu thành “Tiền cọc được quản lý trong hợp đồng.” trong `_EditRoomForm.cshtml`, giữ UTF-8.
- Kiểm tra các partial dịch vụ liên quan không còn chữ bị thay bằng dấu hỏi; `git diff --check` đạt. Không đổi logic hoặc CSDL.

## 11/10/2026 — Lưu ghi chú và tiện nghi phòng

- Bật ghi chú tối đa 500 ký tự và 8 tiện nghi trên trang sửa phòng; lưu/tải lại dữ liệu bằng nút Lưu thay đổi. Ghi chú nội bộ tách khỏi mô tả công khai và không ghi nội dung vào audit.
- Schema v24 thêm hai cột nullable, bước nâng cấp có transaction và backup. Đã thử trên bản sao v23, so sánh bảo toàn mọi dữ liệu cũ, integrity/FK đạt và chạy lặp không đổi; đã áp dụng vào DB mặc định. Tài liệu tại `schema-v24-ghi-chu-tien-nghi.md`.
- Build đạt. Test HTTP lưu ghi chú/tiện nghi, tải lại, từ chối ghi chú quá dài/mã tiện nghi lạ và quyền chủ nhà khác đạt. Chưa kiểm tra trực quan mobile 360px.

## 11/10/2026 — Giao diện tổng quan chủ nhà theo ảnh tham chiếu

- Giữ nguyên lời chào theo giờ/tên tài khoản, sidebar, khối Tình hình thu tiền và Tình trạng phòng; hai khối này nằm trước Việc cần xử lý.
- Ba thẻ tổng quan, bốn thẻ công việc và lối tắt quản lý; danh sách yêu cầu ưu tiên cũ nhất và phòng trống chưa có tin công khai lấy trong phạm vi tòa đang hoạt động của chủ nhà. Giữ dữ liệu thật, không dùng số mẫu từ ảnh.
- Tổng công nợ và xác nhận thanh toán chưa có luồng tổng hợp trên dashboard, hiển thị thông tin giới hạn; cảnh báo điện nước dùng service hiện có. Không sửa CSDL hoặc nghiệp vụ thu tiền.
- Build demo đạt; kiểm tra trực quan desktop 1440px và mobile 360px không tràn ngang. Lời chào và thứ tự các khối đạt. Chưa nghiệm thu Safari/thiết bị thật; không chạy toàn bộ test suite.

## 11/10/2026 — Bộ lọc tổng quan chủ nhà

- Thêm chọn cơ sở và tháng dưới lời chào. Cơ sở lấy từ các tòa đang hoạt động của chủ nhà; chọn cơ sở lọc số phòng, yêu cầu và danh sách phòng chưa đăng tin. Tháng lọc cảnh báo điện nước theo kỳ; số phòng là trạng thái hiện tại, không suy diễn lịch sử.
- Giữ lựa chọn qua query string, từ chối cơ sở của chủ nhà khác bằng 403 và kỳ sai bằng 400. Không đổi schema.
- Build đạt. Kiểm tra HTTP bộ lọc, giữ lựa chọn, tháng khác, chặn sở hữu chéo 403 và kỳ sai 400 đạt. Chưa kiểm tra trực quan bộ lọc trên Safari/thiết bị thật.

## 11/10/2026 — Bật lối tắt Thêm khách thuê

- Thêm trang `/HopDong/ThemKhachThue` cho chủ nhà có quyền ghi HOP_DONG: chọn hợp đồng chờ/đang hiệu lực tại cơ sở đang hoạt động, nhập họ tên, SĐT, căn cước và ngày vào ở.
- Hồ sơ mới gắn với hợp đồng như người ở ghép, chưa tạo tài khoản đăng nhập. Không tạo hồ sơ độc lập chưa có phạm vi sở hữu; không sửa schema.
- Dùng lại POST ThemNguoi có CSRF, transaction, nhật ký và kiểm tra ngày vào/sức chứa/trùng căn cước/phien_ban. Hợp đồng thuộc chủ nhà khác trả 403; vai trò khác không được mở trang mới. Lưu xong mở chi tiết hợp đồng.
- Bật nút trên tổng quan. Build đạt; nhóm Roommate đạt 21/21, gồm HTTP trang mới, quyền sở hữu, CSRF và luồng lưu. Chưa kiểm tra trực quan trên Safari/thiết bị thật.

### 11/10/2026 — Thẻ thống kê quản lý tòa nhà
- Chỉnh riêng bốn thẻ tại /PhongTro/ToaNha theo ảnh: kích thước gọn, biểu tượng SVG nền xám, số và đơn vị căn baseline, thanh tiến độ vàng nhạt. Giữ ba thẻ tổng quan chủ nhà và số liệu thực; không thêm số tăng trưởng/thu tiền mẫu khi chưa có dữ liệu.
- Build demo thành công; HTTP xác minh bốn thẻ và bốn biểu tượng, tổng quan vẫn ba thẻ. Chưa kiểm chứng trực quan trên thiết bị 360px; giữ breakpoint hai cột hiện có. Không thay đổi schema/CSDL.

### 11/10/2026 — Menu tháng quản lý tòa nhà
- Thay select native bằng menu details nền trắng, bo góc, bóng nhẹ, cuộn gọn; tháng chọn nền kem và dấu kiểm. Đóng khi chọn, bấm ngoài hoặc Escape, trả focus về nút.
- Build demo và HTTP xác minh 12 tháng/một lựa chọn thành công; JavaScript kiểm tra cú pháp đạt. Chưa kiểm tra trực quan tương tác trên thiết bị. Giữ phạm vi chọn tháng hiện có, chưa bổ sung báo cáo thu tiền.

### 11/10/2026 — Cần chú ý tại quản lý tòa nhà
- Chỉnh khung ba dòng biểu tượng, nhãn số nhóm việc, phân cách và liên kết tổng quan theo ảnh. Hợp đồng đang hiệu lực có kỳ cuối kết thúc trong 30 ngày được đếm trong phạm vi tòa tài khoản được xem, ngày Việt Nam qua ITimeProvider. Công nợ và bảo trì chưa có tổng hợp nên ghi trạng thái thiếu dữ liệu, không dùng số mẫu.
- Build demo/HTTP trang thành công, kiểm tra ba dòng và liên kết; không đổi schema. Chưa kiểm chứng trực quan trên thiết bị.

### 11/10/2026 — Thu gọn menu tháng
- Menu quản lý tòa nhà rộng 148px, cao tối đa 190px, dòng khoảng 29px, chữ 10px, cuộn mảnh và bóng nhẹ; nút mở giảm khoảng đệm. Chỉ đổi CSS, không cần rebuild. HTTP menu 12 tháng đạt; chưa kiểm chứng trực quan trên thiết bị.

### 11/10/2026 — Menu khu vực và tình trạng tòa nhà
- Nâng cấp hai select bằng JavaScript thành menu details gọn 34px, nền trắng, bo góc, lựa chọn nền kem/dấu kiểm. Giữ select và sự kiện input cho bộ lọc hiện có; không có JS vẫn dùng select gốc. Đóng khi chọn, bấm ngoài hoặc Escape; chỉ mở một menu lọc.
- Kiểm tra cú pháp JavaScript và HTTP trang đạt; thay đổi CSS/JS được phục vụ trực tiếp, không cần build lại. Chưa kiểm tra trực quan tương tác trên thiết bị.

### 11/10/2026 — Đồng bộ khoảng cách trang quản trị
- Thêm dashboard-spacing.css dùng chung trong layout chủ nhà/quản lý/admin: padding 28px desktop, 20px trên/dưới và 16px hai bên khi rộng tối đa 600px, max-width 1600px theo tổng quan. Xóa margin trên bổ sung của wrapper/header đầu tiên, giữ bố cục nội dung và sidebar.
- Build demo thành công; HTTP 8 trang chủ nhà xác minh nạp stylesheet mới; trang hóa đơn trả 403 theo quyền tài khoản demo. Chưa đo trực quan trên trình duyệt/thiết bị; không thay schema/nghiệp vụ.

### 11/10/2026 — Khoảng cách trang thêm tòa nhà
- Bỏ max-width 1280px của wrapper thêm tòa nhà để bám cùng mép nội dung tổng quan; bỏ margin-top 14px của tiêu đề. Dùng padding chung 28px desktop, 20px/16px mobile. Giữ form và nghiệp vụ.
- HTTP GET /PhongTro/TaoToaNha và CSS xác minh thành công; chỉ sửa CSS nên không cần rebuild. Chưa đo trực quan trên thiết bị.

### 12/10/2026 — Khoảng cách chi tiết tòa nhà
- Bỏ padding-top 6px của wrapper và margin-top 9px của tiêu đề chi tiết tòa nhà; bỏ max-width riêng 1440px để căn mép theo tổng quan. Giữ padding chung 28px desktop, 20px trên và 16px hai bên mobile; không đổi nội dung/nghiệp vụ.
- HTTP trang chi tiết tòa nhà và CSS đạt; chưa đo trực quan trên thiết bị. Chỉ đổi CSS, không cần rebuild.

### 12/10/2026 — Khối thông tin chi tiết tòa nhà
- Căn khối theo ảnh: grid ảnh 96x80, thông tin chính ba cột và người quản lý bên phải có đường phân cách; giảm khoảng đệm/chữ. Dưới 850px chuyển thông tin quản lý xuống hàng dưới, 480px thông tin hai cột. Giữ dữ liệu thật, không thêm điện thoại hay tiện ích mẫu.
- HTTP trang/CSS đạt; chưa kiểm chứng trực quan trên thiết bị. Chỉ sửa CSS.

### 12/10/2026 — Mũi tên liên kết chi tiết tòa nhà
- Đổi mũi tên ký tự của liên kết yêu cầu thuê trong khung Cần chú ý sang SVG 12px nét mảnh màu vàng nâu, căn giữa với chữ theo ảnh. Build và HTTP xác minh SVG đạt; chưa kiểm tra trực quan trên thiết bị.

### 12/10/2026 — Menu lọc chi tiết tòa nhà
- Nâng cấp ba select tầng/loại phòng/thanh toán thành giao diện menu nổi bo góc, bóng nhẹ, lựa chọn nền kem/dấu kiểm. Tầng và loại phòng giữ sự kiện change gửi GET form. Thanh toán giữ trạng thái chưa hỗ trợ, không mở menu giả. Không JS dùng select gốc.
- Build, cú pháp JS và HTTP trang/script/bộ lọc đạt. Chưa kiểm chứng trực quan tương tác trên thiết bị.

### 12/10/2026 — Khoảng cách chỉnh sửa tòa nhà
- Bỏ max-width riêng 1200px của wrapper và margin-top 14px ở tiêu đề, bám chuẩn tổng quan với padding chung 28px desktop, 20px trên/16px hai bên mobile. Giữ form và nghiệp vụ.
- HTTP GET chỉnh sửa tòa nhà và CSS đạt; chưa đo trực quan trên thiết bị. Chỉ sửa CSS.

### 12/10/2026 — Kích thước thông tin chung chỉnh sửa tòa nhà
- Căn riêng #be-general: padding 20px, biểu tượng 32px, tiêu đề 16px; ba ô tỉ lệ 1.4/1/1, cao 40px, khoảng cách 16px; mô tả tối thiểu 88px. Mobile một cột/padding 16px. Giữ dữ liệu/form.
- HTTP trang/CSS đạt, chưa kiểm tra trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Kích thước địa chỉ chỉnh sửa tòa nhà
- Đồng bộ #be-address với thông tin chung: padding 20px, biểu tượng 32px, tiêu đề 16px; ba ô địa chỉ đều nhau cao 40px, chữ 12px; thu gọn dòng xem trước. Mobile một cột/padding 16px. Giữ form và dữ liệu.
- HTTP trang/CSS đạt, chưa kiểm tra trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Kích thước quy mô và tiện ích
- Đồng bộ #be-size: padding 20px, biểu tượng 32px, tiêu đề 16px; ba ô bằng nhau cao 40px, đơn vị căn giữa; tiện ích chữ 12px, checkbox 15px, khoảng cách đều. Mobile ô nhập một cột/tiện ích hai cột/padding 16px. Giữ nghiệp vụ.
- HTTP trang/CSS đạt; chưa kiểm tra trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Kích thước người phụ trách
- Đồng bộ #be-manager: padding 20px, biểu tượng 32px, tiêu đề 16px; hai ô bằng nhau cao 40px, chữ 12px và khoảng cách 16px. Mobile một cột/padding 16px. Giữ bộ chọn quản lý và dữ liệu liên hệ.
- HTTP trang/CSS đạt; chưa kiểm tra trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Đơn vị kích thước thích nghi
- Đổi CSS tự viết của dự án (42 file) từ px sang rem cho font, khoảng cách, padding/margin/gap và giới hạn khung nội dung theo tỉ lệ 16px = 1rem. html dùng font-size 100% tôn trọng cỡ chữ trình duyệt. Khung rộng cố định từ 320px trở lên dùng min(100%, giới hạn rem); giữ khung tổng quan width 100%/max-width 100rem. Giữ px cho viền, biểu tượng/chi tiết cố định và breakpoint; không sửa CSS thư viện.
- Khoảng đệm tổng quan tương đương: 1.75rem desktop; 1.25rem trên và 1rem hai bên mobile. Kiểm tra các khối CSS cân bằng, font-size/padding/margin/gap không còn px; HTTP 7 trang và CSS đang phục vụ đạt; git diff --check đạt. Không có parser CSS trong môi trường; chưa kiểm chứng trực quan ở nhiều cỡ chữ/thiết bị. Chỉ CSS, không cần rebuild.

### 12/10/2026 — Khoảng cách quản lý phòng
- Bỏ giới hạn riêng 87.5rem của wrapper quản lý phòng, dùng width 100% theo khung chung max-width 100rem. Khoảng đệm tổng quan 1.75rem desktop, 1.25rem trên/1rem hai bên mobile; giữ tiêu đề và chức năng.
- Kiểm tra CSS và HTTP quản lý phòng đạt; chưa đo trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Biểu tượng thống kê quản lý phòng
- Thay ký tự thống kê bằng SVG nét mảnh: tòa nhà, chìa khóa, cửa phòng và cờ lê. Nền biểu tượng xám, phòng trống nền kem vàng. Giữ nhãn/trạng thái và dữ liệu thật.
- Build demo và HTTP bốn biểu tượng xác minh; chưa kiểm tra trực quan thiết bị.

### 12/10/2026 — Chọn cách thêm phòng
- Bỏ nút Tạo nhanh phòng riêng ở quản lý phòng; nút Thêm phòng mở dialog chọn Tạo một phòng hoặc Tạo nhanh nhiều phòng, dẫn tới form Create/CreateBulk và giữ tòa đang chọn. Có Hủy, nút đóng, Escape native và bấm ngoài; trả focus về nút mở. Giữ quyền backend/form hiện có.
- Build, cú pháp JS và HTTP trigger/dialog/liên kết hai form đạt. Chưa kiểm chứng thao tác trực quan trên thiết bị.

### 12/10/2026 — Khoảng cách trang thêm phòng
- Trang thêm phòng dùng width 100%, bỏ max-width riêng 78.125rem và margin-top 0.875rem của tiêu đề. Bám khung chung tổng quan padding 1.75rem desktop, 1.25rem trên/1rem hai bên mobile. Giữ form và nghiệp vụ.
- HTTP trang/CSS đạt; chưa đo trực quan thiết bị. Chỉ CSS.

### 12/10/2026 — Tiện nghi khi thêm phòng
- Thay thông báo chưa hỗ trợ bằng tám checkbox từ danh mục hiện có, bố cục hai cột và SVG, đếm lựa chọn trực tiếp. Lưu JSON tiện nghi vào cột phong_tro.tien_nghi có sẵn; validate allowlist giống sửa phòng. Không mặc định chọn năm tiện nghi mẫu.
- Build/cú pháp JS/HTTP tám checkbox và đếm đạt; kiểm tra schema đọc cột hiện có; POST không hợp lệ bị từ chối. Chưa thử lưu phòng hợp lệ để không thêm dữ liệu demo và chưa kiểm tra trực quan thiết bị.

### 12/10/2026 — Ảnh và ghi chú khi thêm phòng
- Khối ảnh theo tham chiếu: kéo thả/chọn JPG PNG, xem trước hai cột, xóa lựa chọn và đánh dấu ảnh đầu đại diện; giữ giới hạn nghiệp vụ 8 ảnh/5MB mỗi ảnh. Ghi chú nội bộ nhập được và đếm tối đa 500 ký tự. Form multipart; xử lý bằng RoomImageStore hiện có, lưu phòng/dịch vụ/ảnh cùng transaction, rollback và dọn file khi lỗi; không đổi schema.
- Build, cú pháp JS và HTTP đạt. Test CreateRoomSavesSelectedPhotoAmenitiesAndInternalNote trên DB tạm đạt 1/1: lưu ảnh đúng thứ tự, tiện nghi và ghi chú. Chưa kiểm chứng kéo thả trực quan trên thiết bị.

### 12/10/2026 — Giá mặc định thêm phòng
- GET thêm phòng đặt GiaThueDisplay bằng 0; ô tiền cọc xem trước hiển thị 0, bỏ placeholder 4.800.000. Giữ validation giá tối thiểu 500.000 và trạng thái tiền cọc chưa hỗ trợ của form.
- Build và HTTP xác minh hai giá trị mặc định. Không đổi DB.

### 12/10/2026 — Căn checkbox tiện nghi
- Sửa độ ưu tiên CSS để label tiện nghi dùng flex thay vì display block từ form chung; checkbox 15px, cách chữ 0.5rem, hàng tối thiểu 2rem, hai cột cân đối; dòng đếm có phân cách nhẹ. Chỉ CSS, giữ lựa chọn/lưu.
- HTTP tiện nghi/validation đạt; chưa kiểm tra trực quan thiết bị.

### 12/10/2026 — Phí dịch vụ khi thêm phòng
- Thay bảng minh họa bằng dịch vụ và giá đã chốt của tòa nhà đang chọn. Cho phép bật/tắt, chỉnh đơn giá riêng và chọn cách tính; nút Thêm phí khác mở danh mục dịch vụ còn lại của tòa. Biểu tượng SVG và bảng ba cột, co lại trên màn hình nhỏ.
- Lưu dịch vụ được chọn và cấu hình giá riêng cùng transaction tạo phòng/ảnh; bỏ chọn không tự gán lại mặc định. Backend kiểm tra danh mục thuộc tòa, giá nguyên không âm, điện/nước phải lớn hơn 0 và dùng chỉ số hoặc đầu người. Không đổi schema.
- Build và cú pháp JavaScript đạt. Test DB tạm đạt 4/4 (giá riêng, bỏ chọn mặc định, chặn giá điện 0/âm); HTTP xác minh dữ liệu giá thực, trang thêm phòng và 403 với tòa ngoài phạm vi. Demo khởi động lại thành công. Chưa kiểm tra trực quan trên thiết bị.

### 12/10/2026 — Căn khối ghi chú thêm phòng
- Đồng bộ padding 1.25rem, biểu tượng/tiêu đề nhỏ gọn; ô nhập cao tối thiểu 6.25rem, chữ 0.75rem, viền focus vàng nhẹ. Dòng quyền hiển thị và bộ đếm cân hai mép, tự xuống dòng khi hẹp. Bỏ cursor not-allowed còn sót của ô minh họa; giữ nhập/lưu và giới hạn 500 ký tự.
- Chỉ thay CSS. Chạy data/check-create-photos.py đạt: HTTP trang thêm phòng, ô ghi chú nhập được và kiểm tra đơn vị/kết cấu CSS. Chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Căn bảng phí dịch vụ theo ảnh
- Căn ba cột theo tỷ lệ tham chiếu, hàng gọn hơn, ô nhập/chọn cao 1.75rem, mũi tên SVG và focus vàng nhẹ; nút Thêm phí khác dùng con trỏ tương tác. Rút phần dưới bảng còn một dòng hướng dẫn, giữ dữ liệu thực của tòa và chức năng lưu hiện có.
- Build demo và HTTP kiểm tra trang/dịch vụ/quyền đạt; kiểm tra CSS đạt. Chưa kiểm tra trực quan trên thiết bị.

### 12/10/2026 — Bỏ popup danh mục phí trống
- Bỏ popup thông báo không còn phí khác; ẩn nút thêm phí khi đang tải hoặc tất cả dịch vụ đã có trong bảng. Vẫn chọn thêm được khi danh mục còn dịch vụ. Kiểm tra cú pháp JavaScript đạt.

### 12/10/2026 — Liên kết nút Thêm dịch vụ
- Theo yêu cầu mới, nút luôn hiển thị và chuyển tới /DichVu/Create, truyền toaNhaId đang chọn để điền sẵn tòa nhà. Kiểm tra cú pháp JavaScript và HTTP trang đích đạt.

### 12/10/2026 — Bỏ nút Thêm dịch vụ trên form phòng
- Theo yêu cầu mới nhất, bỏ nút, dialog và JavaScript xử lý liên quan khỏi form thêm phòng. Giữ bảng chọn dịch vụ, đơn giá và cách tính. Build demo, cú pháp JavaScript và HTTP xác minh nút đã bỏ đạt.

### 12/10/2026 — Căn hàng phí dịch vụ
- Tăng độ ưu tiên label để tránh CSS form chung làm checkbox/biểu tượng/chữ sát nhau; căn flex giữa hàng, khoảng cách 0.5rem. Đồng bộ ô giá/cách tính cao 1.875rem, hàng tối thiểu 2.875rem và khoảng cách tiêu đề/hướng dẫn. Chỉ CSS, giữ dữ liệu và lưu. Kiểm tra CSS và HTTP trang/dịch vụ đạt; chưa kiểm tra trực quan thiết bị.

### 12/10/2026 — Đồng bộ khoảng cách tạo nhanh phòng
- Bỏ max-width 87.5rem, căn giữa và padding trên riêng của room-bulk-page; dùng width 100%, margin/padding 0 và bỏ margin trên tiêu đề. Trang theo app-content chung của tổng quan: 1.75rem desktop, 1.25rem dọc/1rem ngang dưới 600px, khung tối đa 100rem. Giữ bố cục và nghiệp vụ tạo hàng loạt.
- Kiểm tra CSS và HTTP CreateBulk/CSS đang phục vụ đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Căn nhãn tạo đồng loạt
- Nhãn cao tối thiểu 2.125rem, chữ 0.75rem, biểu tượng 16px, khoảng cách 0.5rem; nền vàng nhạt và viền nhẹ, căn giữa chữ/biểu tượng. Giữ ẩn trên màn hình nhỏ như trước. Kiểm tra CSS/HTTP đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Căn khối cài đặt chung tạo nhanh phòng
- Khối dùng padding 1.25rem, biểu tượng 2.25rem, khoảng cách hàng 1rem; đồng bộ ô nhập cao 2.5rem, chữ 0.75rem và đơn vị căn giữa. Thu gọn mô tả/đường phân cách trạng thái, nút trạng thái bo 8px. Mobile giữ một cột và padding 1rem. Chỉ CSS, không đổi nghiệp vụ.
- Kiểm tra CSS và HTTP trang tạo nhanh phòng đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Căn thông tin bổ sung tạo nhanh phòng
- Đồng bộ padding 1.25rem, khoảng cách hàng 1rem, ô nhập cao 2.5rem và chữ 0.75rem với cài đặt chung; ô văn bản cao tối thiểu 5rem, mô tả nhỏ gọn, đơn vị căn giữa. Header tự xuống dòng khi hẹp, giữ thao tác mở/đóng và dữ liệu hiện có. Mobile padding 1rem.
- Chỉ CSS; kiểm tra CSS và HTTP trang tạo nhanh phòng đạt. Chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Đóng mặc định thông tin bổ sung
- Bỏ thuộc tính open của details trên form tạo nhanh phòng. Nội dung ẩn khi vào trang, mở/đóng qua summary và mũi tên bằng hành vi HTML hiện có. Build demo và HTTP xác minh details không có open đạt.

### 12/10/2026 — Bố cục tổng quan chi tiết phòng và ảnh
- Căn khung chi tiết theo mép tổng quan, hai cột theo tỷ lệ tham chiếu, thêm liên kết quay lại danh sách trong tiêu đề; tab chính đổi thành Tổng quan phòng. Chuyển ảnh thực của phòng vào mục Ảnh phòng trong thẻ Thông tin cơ bản, dưới tiện nghi; ảnh ba cột desktop/hai cột mobile, ảnh đầu có nhãn đại diện, bấm mở ảnh lớn. Không đổi schema hoặc nghiệp vụ.
- Build demo và HTTP trang Details đạt; xác minh vị trí ảnh trong thông tin cơ bản, kiểm tra CSS đạt. Chưa kiểm chứng trực quan trên thiết bị. Các khối hợp đồng/hóa đơn/dịch vụ chưa kết nối dữ liệu từ trước vẫn giữ trạng thái hiện có.

### 12/10/2026 — Bỏ liên kết quay lại trên chi tiết phòng
- Bỏ dòng Quay lại danh sách phòng theo yêu cầu mới nhất. Build demo và HTTP xác minh liên kết đã bỏ đạt.

### 12/10/2026 — Đồng bộ khoảng cách chi tiết phòng với tổng quan
- Khung chi tiết dùng width 100%, margin/padding 0, không giới hạn chiều rộng riêng; tiêu đề không có khoảng đệm trên và căn đầu hàng. Dùng app-content chung của tổng quan: padding 1.75rem desktop, 1.25rem dọc/1rem ngang dưới 600px, max-width 100rem. Giữ các thẻ và chức năng hiện có.
- Kiểm tra CSS và HTTP chi tiết phòng/CSS đang phục vụ đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Biểu tượng cửa chi tiết phòng
- Thay ký tự cạnh tên phòng bằng SVG cửa mở nét mảnh, màu vàng trầm trên nền vàng nhạt theo ảnh. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Bỏ nút ba chấm chi tiết phòng
- Bỏ nút thao tác khác chưa hỗ trợ cạnh nút chỉnh sửa phòng theo ảnh. Build demo và HTTP xác minh nút đã bỏ đạt.

### 12/10/2026 — Biểu tượng tiện nghi chi tiết phòng
- Thay năm ký tự minh họa tiện nghi bằng SVG nét mảnh theo ảnh; nhãn nền xám nhạt, căn giữa biểu tượng và chữ với khoảng cách 0.375rem. Chỉ đổi giao diện. Build demo/HTTP năm SVG và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Biểu tượng ảnh phòng
- Đổi ký tự ô vuông ở tiêu đề Ảnh phòng thành SVG hình ảnh nét mảnh, đặt giữa nền xám bo góc hiện có. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Giao diện thanh toán/hóa đơn chi tiết phòng
- Căn khối theo tham chiếu: SVG hóa đơn, nền vàng nhạt cho tóm tắt cần thu, hai cột tiền/hạn thanh toán, đầu bảng bốn cột, dòng chú thích cuối. Liên kết Xem hóa đơn tới danh sách hóa đơn hiện có. Mobile tóm tắt một cột.
- Chỉ thay giao diện; khối chưa kết nối dư nợ/danh sách hóa đơn từ trước vẫn hiển thị dấu — và trạng thái trống, không đưa số tiền/trạng thái mẫu vào. Build demo, HTTP và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Ghi chú và bảo trì theo tham chiếu
- SVG ghi chú/cờ lê nét mảnh, căn tiêu đề và hàng bảo trì, nội dung ghi chú chữ 0.75rem với giãn dòng 1.75. Hiển thị ghi_chu_noi_bo hiện có cho CHU_NHA, Razor mã hóa văn bản, không dùng ghi chú mẫu. Dữ liệu bảo trì chưa kết nối và nút thêm ghi chú vẫn chưa hỗ trợ như trước.
- Build demo, HTTP khối ghi chú và kiểm tra CSS đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Biểu tượng ví giá thuê
- Thay ký tự ô vuông của Giá thuê & phí dịch vụ bằng SVG ví nét mảnh màu xám đậm trên nền xám bo góc theo ảnh. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Biểu tượng khách thuê
- Thay ký tự trong tiêu đề Khách thuê & hợp đồng bằng SVG hai người nét mảnh theo ảnh, đồng bộ màu và kích thước với biểu tượng ví. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Bố cục chỉnh sửa phòng theo tham chiếu
- Khung theo tổng quan, hai cột tỷ lệ 1.9:1, khoảng cách 1.25rem; khối thông tin/giá/ghi chú bên trái, hợp đồng/tiện nghi/ảnh bên phải. Đồng bộ padding thẻ, tiêu đề, khoảng cách form và thanh lưu; thay ký tự bằng SVG cửa/ghi chú/khách thuê/tiện nghi. Sửa label checkbox để không bị CSS form chung ép block, ô ghi chú nhập bình thường. Mobile một cột.
- Giữ nghiệp vụ lưu phòng, dịch vụ riêng từng dòng và quản lý ảnh hiện có; không đưa dữ liệu hợp đồng mẫu vào. Build demo, HTTP form/tiện nghi/ảnh và kiểm tra CSS đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Khoảng cách chỉnh sửa phòng theo tổng quan
- Đối chiếu trang Edit nạp dashboard-spacing.css chung: padding 1.75rem desktop, 1.25rem dọc/1rem ngang dưới 600px, khung tối đa 100rem. Wrapper width 100%, margin/padding 0, min-width 0; tiêu đề không có padding/margin trên riêng. Không đổi chức năng.
- Kiểm tra CSS và HTTP form/CSS phục vụ đạt; chưa kiểm chứng trực quan trên thiết bị.

### 12/10/2026 — Menu nổi trạng thái chỉnh sửa phòng
- Nâng select trạng thái thành nút/menu nổi nền trắng bo góc, bóng nhẹ, dấu chọn vàng nhạt; giữ select gốc gửi giá trị về backend. Hỗ trợ phím mũi tên/Escape, đóng khi bấm ngoài hoặc chuyển focus, giữ select mặc định nếu JS không chạy.
- Kiểm tra cú pháp JavaScript và CSS/HTTP đạt. Chưa kiểm chứng tương tác trực quan trên thiết bị.

### 12/10/2026 — Bỏ liên kết lịch sử người ở trên form sửa phòng
- Bỏ liên kết Lịch sử người ở khỏi khối hợp đồng trên trang chỉnh sửa phòng theo yêu cầu. Build demo và HTTP xác minh liên kết đã bỏ đạt.

### 12/10/2026 — Khối ảnh chỉnh sửa phòng theo ảnh
- Tiêu đề SVG ảnh, vùng chọn/kéo thả nền xám nhạt viền nét đứt, biểu tượng tải lên và chú thích ảnh đại diện. Giữ giới hạn 8 ảnh/5MB nghiệp vụ hiện có dù ảnh tham chiếu ghi 10; giữ upload/queue/xóa/sắp xếp hiện có, kéo thả dùng cùng xử lý upload.
- Build demo, cú pháp JavaScript và HTTP khối ảnh/CSS đạt; chưa kiểm chứng kéo thả trực quan trên thiết bị.

### 12/10/2026 — Bảng phí trên form chỉnh sửa theo tham chiếu
- Ba cột dịch vụ/giá/cách tính, trạng thái chọn có checkbox chỉ đọc, SVG và ô đơn vị nhỏ gọn; thêm khung lưu ý nền xám về giá riêng và dữ liệu hợp đồng. Giá chung hiển thị bằng placeholder khi chưa có giá riêng; giữ để trống dùng giá chung.
- Giữ nút lưu giá và thêm/ngừng riêng từng dòng để không thay đổi luồng nghiệp vụ; cách tính chỉ đọc từ cấu hình tòa. Build demo, HTTP bảng/nút lưu và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Đổi nhãn thêm dịch vụ trên form sửa phòng
- Đổi Thêm phí khác thành Thêm dịch vụ, giữ liên kết trang tạo dịch vụ và tòa nhà được chọn. Build demo/HTTP nhãn mới đạt.

### 12/10/2026 — Bỏ nút lưu giá và ngừng dịch vụ trên form sửa
- Bỏ hai nút Lưu giá/Ngừng từ kỳ sau theo yêu cầu, giữ nút Thêm vào phòng/Áp dụng lại khi phù hợp. Sửa chú thích không hướng dẫn bấm nút đã bỏ. Không đổi API hoặc nghiệp vụ hiệu lực giá; form lưu phòng chưa lưu giá dịch vụ. Build demo/HTTP xác minh hai nhãn đã bỏ đạt.

### 12/10/2026 — Bỏ chú thích tiền cọc form sửa
- Bỏ dòng Tiền cọc được quản lý trong hợp đồng và tham chiếu aria-describedby tương ứng theo yêu cầu. Build demo/HTTP xác minh dòng đã bỏ đạt.

### 12/10/2026 — Biểu tượng dịch vụ form sửa
- Thay SVG chung bằng tia sét cho điện, giọt nước cho nước, sóng Wi-Fi cho Internet và thùng rác cho vệ sinh/rác theo ảnh. Giữ nét mảnh xám và căn đều với tên. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Thu gọn kích thước bảng phí form sửa
- Cân ba cột, ô giá/cách tính cao 1.875rem, hàng tối thiểu 2.75rem; ẩn vùng thao tác rỗng để bỏ khoảng trống còn sót sau khi bỏ nút. Căn giữa đơn vị tiền và thu gọn khung chú thích. Mobile giữ hai cột, cách tính dưới ô giá. Chỉ CSS, không đổi dữ liệu/lưu.
- Kiểm tra CSS và HTTP form đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Khôi phục thao tác phí dịch vụ
- Theo yêu cầu chức năng mới, khôi phục nút Cập nhật giá và Bật/Tắt gọn trên từng dòng, dùng form SetPrice/Set hiện có với CSRF/quyền/sở hữu. Giá để trống dùng giá chung; tắt từ kỳ kế tiếp. Nhắc lưu thông tin phòng trước khi thao tác dịch vụ vì các form độc lập. Không đổi schema/API/quy tắc nghiệp vụ.
- Build demo/HTTP form đạt. Ba kiểm thử tập trung đạt: HTTP lưu giá/ngừng dịch vụ/CSRF/quyền, mặc định và lựa chọn độc lập, sở hữu và rollback. Chạy rộng RoomServicesTests phát hiện lỗi ở Version23AddsPaymentHistoryWithoutChangingExistingInvoiceData và V7CopyUpgradePreservesIssuedInvoicesAndPrivatePrices; dừng lượt rộng để kiểm tra tập trung, chưa xác định nguyên nhân hai lỗi nâng cấp ngoài phạm vi. Chưa kiểm chứng tương tác trực quan trên thiết bị.

### 12/10/2026 — Bỏ Cập nhật/Tắt theo yêu cầu mới
- Bỏ hai nút Cập nhật và Tắt khỏi bảng phí; giữ Bật khi dịch vụ chưa chọn/đang ngừng, cập nhật hướng dẫn. Không thay API; thao tác lưu giá/ngừng không còn nút trực tiếp tại form này. Build demo và HTTP xác minh hai nút đã bỏ đạt.

### 12/10/2026 — Căn dòng phí theo ảnh nhỏ gọn
- Checkbox chọn nền charcoal có dấu tích trắng, biểu tượng 14px, ô giá/cách tính cao 1.625rem, hàng tối thiểu 2.375rem; thêm mũi tên vào ô cách tính và bỏ spinner giá. Chỉ CSS; cách tính vẫn chỉ đọc, không đổi luồng lưu. Kiểm tra CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Checkbox dịch vụ thao tác trực tiếp
- Bỏ disabled, checkbox phản ánh yêu cầu bật/tắt (dịch vụ đang chờ ngừng hiển thị bỏ chọn). Khi thay đổi, gửi form Set hiện có bằng fetch với token CSRF; khóa checkbox trong lúc lưu, hiện thông báo kết quả, hoàn lại lựa chọn nếu lỗi. Không chuyển trang, giữ thông tin phòng chưa lưu và không thêm lại nút Cập nhật/Tắt. Ngừng vẫn áp dụng kỳ sau.
- Build demo/cú pháp JS/HTTP checkbox không disabled đạt. Kiểm thử HTTP backend dịch vụ (giá, ngừng, quyền, CSRF) đạt 1/1. Chưa kiểm chứng thao tác fetch trực quan trên thiết bị.

### 12/10/2026 — Bỏ nút Bật dịch vụ
- Bỏ nút Bật còn lại khỏi bảng phí dịch vụ; giữ checkbox tự lưu bật/tắt. Build demo và HTTP xác minh nút đã bỏ, checkbox còn hoạt động qua JS hiện có.

### 12/10/2026 — Giao diện lịch sử cư trú theo tham chiếu
- Đổi danh sách thẻ thành bảng người ở/phòng/ngày vào/ngày ra/thời gian/trạng thái, ba thẻ thống kê và bộ lọc gọn. Giữ phạm vi một phòng và truy vấn ngày/kiểm tra quyền backend hiện có; không đưa dữ liệu người ở mẫu vào. Phân biệt ngày kết thúc theo hạn hợp đồng với chuyển đi thực tế.
- Tìm tên/mã hợp đồng và lọc trạng thái trên kết quả đã tải, xuất CSV các dòng đang hiển thị; bảng cuộn trong khung trên mobile. Build demo, HTTP trang lịch sử và kiểm tra CSS/cú pháp JS đạt. Chưa kiểm chứng trực quan hoặc tải CSV trên thiết bị.

### 12/10/2026 — Khoảng cách lịch sử phòng theo tổng quan
- Xác minh trang lịch sử nạp dashboard-spacing.css chung. Wrapper width 100%, max-width none, margin/padding 0; tiêu đề không đệm trên và căn đầu hàng. Khoảng đệm ngoài theo tổng quan: 1.75rem desktop, 1.25rem dọc/1rem ngang dưới 600px, khung tối đa 100rem.
- Chỉ CSS; kiểm tra CSS và HTTP trang/CSS đang phục vụ đạt, giữ nguyên tra cứu lịch sử. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Nút xuất lịch sử theo hình
- Thay ký tự tải xuống bằng SVG nét mảnh 14px, nút gọn cao tối thiểu 2rem, chữ 0.6875rem, viền xám và bo 6px. Giữ xuất CSV hiện có. Build demo/HTTP biểu tượng và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Biểu tượng thống kê lịch sử
- Thay ba ký tự bằng SVG lịch sử/nhóm người/cửa phòng nét mảnh 18px theo ảnh. Giữ nền vàng nhạt cho lịch sử, nền xám cho hai biểu tượng còn lại và số liệu thật. Build demo/HTTP ba SVG và kiểm tra CSS đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Kính lúp ô tìm lịch sử
- Thêm SVG kính lúp nét mảnh 14px bên trái ô tìm kiếm; đệm chữ tránh chồng biểu tượng, khung tìm rộng toàn hàng trên mobile. Giữ chức năng lọc hiện có. Build demo, kiểm tra CSS và HTTP biểu tượng đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Menu trạng thái lịch sử
- Thay menu trạng thái mặc định bằng popup bo góc, nền trắng, bóng nhẹ; lựa chọn hiện tại vàng nhạt, viền focus màu chủ đề. Đồng bộ select hiện có và sự kiện change để giữ lọc bảng; hỗ trợ phím lên/xuống, Home/End, Escape và đóng khi bấm ngoài. Select gốc vẫn dùng khi JavaScript không chạy.
- Kiểm tra cú pháp JavaScript, CSS và HTTP trang demo đạt. Chưa kiểm chứng trực quan hoặc thao tác trên thiết bị.

### 12/10/2026 — Bảng lịch sử theo ảnh
- Căn lại độ rộng cột, khoảng cách dòng, nền tiêu đề, thanh thống kê và liên kết chi tiết theo ảnh; bổ sung số lượt đã chuyển đi từ dữ liệu thật. Giữ phạm vi tra cứu một phòng và chức năng lọc hiện có. Kiểm tra CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Đường dẫn lịch sử người ở
- Thêm breadcrumb trang chủ / Phòng cho thuê / mã phòng / Lịch sử người ở trên header. Mã và liên kết chi tiết lấy từ phòng đang tra cứu, không cố định A101-DEMO. Kiểm tra build/HTTP và CSS; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Đổi tên trang lịch sử
- Đổi tiêu đề trang và breadcrumb từ Lịch sử người ở thành Lịch sử theo yêu cầu. Giữ nguyên route và chức năng.

### 12/10/2026 — Đồng bộ cỡ chữ breadcrumb lịch sử
- Dùng cùng cỡ chữ 1rem, đậm 600, chiều cao dòng 1.5 và biểu tượng 21px như các trang tạo/sửa/chi tiết phòng. Mobile giữ cỡ chữ, thu khoảng cách và cho xuống dòng. Kiểm tra CSS và HTTP demo đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Khoảng cách Dịch vụ & đơn giá theo Tổng quan
- Bỏ giới hạn chiều rộng riêng 84.375rem và căn giữa của trang dịch vụ; nội dung dùng hết khung chung với Tổng quan, không thêm đệm trong wrapper. Khoảng ngoài 1.75rem desktop, 1.25rem dọc/1rem ngang ở mobile, tối đa 100rem theo dashboard-spacing.css. Tiêu đề căn đầu hàng, không đệm trên. Chỉ CSS, giữ chức năng dịch vụ. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Biểu tượng thống kê dịch vụ
- Đổi bốn ký tự thành SVG danh sách/đồng hồ đo/hóa đơn/tòa nhà nét mảnh 16px theo ảnh, giữ nền xám bo góc và số liệu thật. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Kích thước thẻ thống kê dịch vụ
- Căn đều bốn thẻ với đệm 1rem, khoảng cách 1rem; số liệu 1.75rem/600, nhãn 0.75rem, nền biểu tượng 24px/SVG 14px. Mobile giữ hai cột, đệm 0.875rem, số 1.5rem. Kiểm tra CSS và HTTP demo đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Bỏ nút cấu hình điện nước
- Bỏ nút Cấu hình điện nước trong hàng thao tác trang Dịch vụ & đơn giá theo yêu cầu; giữ action và nghiệp vụ hiện có.

### 12/10/2026 — Khôi phục nút cấu hình điện nước
- Người dùng hủy yêu cầu bỏ nút; khôi phục Cấu hình điện nước ở hàng thao tác với điều kiện hiển thị và liên kết cũ.

### 12/10/2026 — Bật sắp xếp mã dịch vụ
- Bỏ disabled ở nút sắp xếp, dùng mã thật để sắp toàn bộ dòng tăng/giảm khi nhấn; mặc định tăng dần. Cập nhật mô tả các chức năng chưa hỗ trợ. Kiểm tra cú pháp JavaScript đạt; chưa kiểm chứng thao tác trực quan.

### 12/10/2026 — Kính lúp tìm dịch vụ
- Thay ký tự kính lúp trong placeholder bằng SVG nét mảnh 14px bên trái ô tìm, đệm chữ 2rem tránh chồng. Giữ trạng thái tìm kiếm chưa hỗ trợ hiện có; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Bật lọc cách tính dịch vụ
- Bỏ disabled, thêm lựa chọn tất cả/theo tiêu thụ/theo người/cố định, lọc theo CachTinh thật trên các dòng đã tải. Cập nhật số dòng và thông báo rỗng; giữ sắp xếp hiện có. Kiểm tra cú pháp JS đạt; chưa kiểm chứng thao tác trực quan.

### 12/10/2026 — Bỏ nút Xem dịch vụ
- Bỏ nút Xem dịch vụ theo yêu cầu; chọn tòa nhà tự gửi form GET để giữ khả năng đổi danh sách. Bộ lọc cách tính và sắp xếp giữ nguyên.

### 12/10/2026 — Biểu tượng danh sách dịch vụ
- Đổi ký tự sang SVG nét mảnh cho điện, nước, internet, vệ sinh, xe máy, xe đạp, quản lý, giặt sấy và rác, chọn theo tên/mã dịch vụ thật. Giữ nền vàng/xám của loại dịch vụ và dữ liệu hiện có. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Thẻ giải thích cách tính theo ảnh
- Căn hai thẻ theo tiêu thụ/phí cố định hai cột đều nhau, đệm 0.875rem, biểu tượng SVG đồng hồ đo/hóa đơn 15px trên nền xám 26px; tiêu đề 0.75rem, mô tả 0.625rem. Mobile một cột. Giữ nội dung công thức hiện có; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Menu nổi tòa nhà và cách tính
- Thay hai select bộ lọc bằng menu trắng bo góc, bóng nhẹ, mục chọn vàng nhạt và viền focus đồng bộ. Giữ select gốc/form GET, sự kiện change để đổi tòa/lọc cách tính; hỗ trợ bàn phím, Escape, đóng khi ra ngoài. Kiểm tra cú pháp JS/CSS và HTTP đạt; chưa kiểm chứng trực quan/thao tác thiết bị.

### 12/10/2026 — Phân trang dịch vụ theo ảnh
- Căn mũi tên và ô trang nền charcoal 24px theo ảnh; bật chuyển trang 10/20/50 dòng, cập nhật khoảng dòng và khóa mũi tên ở đầu/cuối. Bộ lọc/sắp xếp đưa về trang đầu và giữ dữ liệu thật. Kiểm tra cú pháp JS đạt; chưa kiểm chứng thao tác trực quan.

### 12/10/2026 — Biểu tượng sửa đơn giá
- Thay ký tự bút ở menu thao tác bằng SVG bút trong ô vuông nét mảnh 14px theo ảnh; giữ menu và hành động hiện có. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Mặc định Chọn tòa nhà
- Trang dịch vụ không tự chọn tòa đầu tiên khi thiếu toaNhaId; thêm lựa chọn rỗng Chọn tòa nhà và lời nhắc chọn tòa để xem dữ liệu. Truy cập có toaNhaId tiếp tục giữ lựa chọn và kiểm tra sở hữu hiện có.

### 12/10/2026 — Bỏ nút lập và xem hóa đơn
- Bỏ nút Lập và xem hóa đơn cùng khung chứa khỏi trang Dịch vụ & đơn giá theo yêu cầu. Giữ module hóa đơn hiện có.

### 12/10/2026 — Nút Thêm dịch vụ theo ảnh
- Nút nền charcoal, dấu cộng SVG 14px, chữ 0.6875rem/600, cao tối thiểu 2.125rem. Hiển thị cho người có quyền ngay cả khi chưa chọn tòa; trang Create hỗ trợ toaNhaId nullable để chọn tòa tại form. Giữ kiểm tra quyền backend; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Nút cấu hình điện nước ở giữa
- Hiển thị Cấu hình điện nước giữa Xuất danh sách và Thêm dịch vụ cho người có quyền. Nếu chưa chọn tòa, bấm mở ô chọn tòa; đã chọn thì dẫn tới cấu hình tòa tương ứng. Kiểm tra cú pháp JS; chưa kiểm chứng thao tác trực quan.

### 12/10/2026 — Khoảng cách cấu hình điện nước theo Tổng quan
- Bỏ giới hạn riêng 80rem và margin auto của trang cấu hình; wrapper rộng 100%, không thêm padding, tiêu đề không đệm trên. Dùng dashboard-spacing.css chung: ngoài 1.75rem desktop, 1.25rem dọc/1rem ngang trên mobile, khung tối đa 100rem. Kiểm tra CSS và HTTP trang cấu hình đạt; chưa kiểm chứng trực quan thiết bị. Giữ nguyên form và nghiệp vụ.

### 12/10/2026 — Căn kích thước phạm vi áp dụng
- Thẻ đệm 1.25rem, hai cột đều nhau, ô tòa/lựa chọn phòng cao 2.25rem; thống nhất chữ và khoảng cách tiêu đề, nhãn, thanh giải thích. Dưới 750px chuyển một cột, dưới 480px xếp lựa chọn phòng dọc. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Không đổi phạm vi áp dụng nghiệp vụ.

### 12/10/2026 — Căn kích thước thẻ tiền điện
- Căn tiêu đề, biểu tượng 30px, nhãn trạng thái, khoảng cách cách tính/đơn giá; lựa chọn và ô giá/đơn vị cao 2.5rem. Mobile xếp dọc tránh tràn. Giữ hai cách tính đang hỗ trợ và quy tắc giá hiện có. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Căn kích thước thẻ tiền nước
- Đồng bộ tiền nước với tiền điện: tiêu đề 0.9375rem, biểu tượng 30px, mô tả 0.6875rem, ô giá/đơn vị và lựa chọn cao 2.5rem; khoảng cách đều, mobile xếp dọc. Không đổi cách tính hay lưu giá. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Căn kích thước thời điểm hiệu lực
- Đồng bộ đệm thẻ 1.25rem, biểu tượng 30px, tiêu đề 0.875rem, hai ô ngày/kỳ thu cao 2.25rem và khoảng cách 1rem. Thanh thông báo căn biểu tượng/chữ gọn; mobile một cột. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ ngày/kỳ áp dụng nghiệp vụ.

### 12/10/2026 — Bỏ liên kết hướng dẫn ghi điện nước
- Bỏ liên kết Hướng dẫn ghi điện nước trong thẻ ghi chú theo yêu cầu, giữ nội dung ghi chú.

### 12/10/2026 — Bỏ thông báo khung giao diện dịch vụ
- Bỏ dòng Chưa hoạt động… ở cuối trang Dịch vụ & đơn giá theo yêu cầu; giữ chức năng và trạng thái các nút hiện có.

### 12/10/2026 — Điều hướng Thêm dịch vụ theo Chi tiết phòng
- Bỏ chữ Không gian quản lý, giữ biểu tượng nhà có nhãn truy cập; dùng dấu /, chữ 1rem/600, khoảng cách 0.75rem và line-height 1.5 như Chi tiết phòng. Giữ liên kết điều hướng.

### 12/10/2026 — Khoảng cách Thêm dịch vụ theo Tổng quan
- Bỏ giới hạn chiều rộng riêng 78.125rem và margin auto; wrapper rộng 100%, không thêm padding, tiêu đề căn đầu hàng. Dùng khoảng ngoài chung 1.75rem desktop, 1.25rem dọc/1rem ngang mobile và khung tối đa 100rem. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ nguyên form thêm dịch vụ.

### 12/10/2026 — Thông tin dịch vụ theo ảnh
- Biểu tượng danh sách SVG 16px trên nền 28px, tiêu đề/mô tả gọn, hai cột tên/mã tỷ lệ 2.15:1, ô cao 2.25rem; mobile một cột. Giữ mã tự sinh khi lưu, không đưa mã mẫu DV-009 vào form. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Thẻ cách tính và đơn giá theo ảnh
- Thay biểu tượng hóa đơn SVG; căn lựa chọn cách tính hai cột, viền vàng/nền nhạt mục chọn, ô giá/đơn vị cao 2.25rem, thanh công thức gọn. Giữ lựa chọn Theo đầu người hiện có ở hàng riêng và đơn vị tùy nhập để bảo toàn nghiệp vụ. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Phạm vi thêm dịch vụ theo ảnh
- Thay biểu tượng tòa nhà SVG; căn tiêu đề, ô chọn cao 2.25rem, mô tả và lựa chọn phòng gọn theo ảnh. Giữ một tòa mỗi lần tạo và checkbox mặc định cho phòng mới theo backend hiện có; không mô phỏng chọn nhiều tòa hay áp dụng mọi phòng. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Hiệu lực và ghi chú theo ảnh
- Thêm SVG lịch 15px trên nền 28px; căn hai ô ngày/trạng thái cao 2.25rem, thanh chú thích và textarea cao tối thiểu 4.5rem; mobile một cột. Giữ các trường chưa hỗ trợ bị vô hiệu hóa và thông báo thật, không giả lập lưu ghi chú/hẹn hiệu lực. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Tóm tắt dịch vụ theo ảnh
- Thêm biểu tượng SVG tiêu đề/tên/thông báo chưa lưu, khối giá nổi bật nền xám nhạt, căn dòng thông tin và đường phân cách theo ảnh. Giữ tóm tắt theo dữ liệu nhập, một tòa, ngày/trạng thái thật; không dùng dữ liệu mẫu. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Cân kích thước thông tin dịch vụ
- Thẻ đệm 1.25rem, nhãn 0.75rem, mô tả 0.6875rem, hai ô cao 2.375rem và cách nhau 1rem; giữ tỷ lệ tên/mã 2.15:1, mobile một cột. Kiểm tra CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ mã tự sinh và nhập liệu hiện có.

### 12/10/2026 — Cân kích thước cách tính và đơn giá
- Căn lựa chọn bằng hàng ngang nút chọn/chữ/biểu tượng, đệm 0.875rem, cao tối thiểu 4.5rem; nhãn 0.75rem, mô tả 0.6875rem. Ô giá/đơn vị hai cột cao 2.375rem; mobile một cột. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ ba cách tính hiện có.

### 12/10/2026 — Cân kích thước phạm vi thêm dịch vụ
- Đồng bộ đệm 1.25rem, nhãn 0.75rem, mô tả 0.6875rem, ô tòa cao 2.375rem; tách rõ nhóm chọn tòa và nhóm áp dụng phòng, căn checkbox/chữ. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ phạm vi và khả năng backend hiện có.

### 12/10/2026 — Cân kích thước hiệu lực và ghi chú
- Đồng bộ đệm 1.25rem, nhãn 0.75rem, mô tả 0.6875rem; hai ô cao 2.375rem, textarea tối thiểu 5rem, khoảng thông báo đều. Mobile đệm 1rem. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ trạng thái trường và chức năng hiện có.

### 12/10/2026 — Cân kích thước tóm tắt dịch vụ
- Căn đệm 1rem cho tiêu đề/tên/khối giá, giá 1.625rem, mô tả 0.6875rem và thông tin 0.75rem; căn cột nhãn/giá trị và trạng thái gọn. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị. Giữ cập nhật tóm tắt theo form.

### 12/10/2026 — Ba cách tính cùng hàng
- Xếp ba lựa chọn Theo tiêu thụ/Cố định/Theo đầu người thành ba cột đều nhau, bỏ span toàn hàng của lựa chọn cuối. Dưới 600px xếp dọc để dễ đọc. Kiểm tra CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Đồng bộ bố cục ba lựa chọn cách tính
- Cố định cả ba lựa chọn cùng flex hàng ngang: radio bên trái, nội dung giữa, SVG bên phải trên cùng dòng; bỏ ảnh hưởng position/margin cũ. Giữ ba cột desktop và lựa chọn nghiệp vụ. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Đơn giá mặc định 0
- GET Thêm dịch vụ khởi tạo DonGia bằng 0 và placeholder 0; giữ giá nhập khi POST có lỗi và quy tắc kiểm tra giá hiện có.

### 12/10/2026 — Cân ba ô cách tính
- Căn ba ô cùng chiều cao tối thiểu 4.5rem, đệm 0.875rem, gap 0.875rem; chữ và biểu tượng 14px thẳng hàng với radio. Mobile đệm 0.75rem. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Chọn nhiều tòa khi tạo dịch vụ
- Thêm menu checkbox chọn nhiều tòa và tóm tắt các tòa đã chọn. Backend kiểm tra sở hữu tất cả tòa trước khi ghi, tạo chung một danh mục dịch vụ và giá/gán mặc định mỗi tòa trong cùng transaction; loại ID trùng, giữ tương thích ToaNhaId đơn.
- Test MultipleBuildingsShareServiceAndRejectForeignBuildingAtomically đạt 1/1 trên SQLite giả: hai tòa cùng dịch vụ/giá và từ chối tòa khác không ghi một phần. Build/JS syntax đạt; chưa kiểm chứng thao tác trực quan thiết bị. Không đổi schema.

### 12/10/2026 — Căn menu chọn nhiều tòa
- Ô chọn cao tối thiểu 2.375rem, mũi tên nét mảnh bên phải; các dòng menu cao tối thiểu 2.25rem với checkbox 14px căn giữa, chữ 0.6875rem và nền nhạt mục chọn. Giữ chọn nhiều tòa và dữ liệu form. CSS/HTTP đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Bật chọn phòng cụ thể
- Cho chọn phòng hiện có theo các tòa đã tích; thay đổi tòa loại lựa chọn phòng không thuộc phạm vi. Backend xác minh ID/phạm vi phòng trước khi tạo, gán dịch vụ đúng phòng trong transaction tạo dịch vụ, không gán mặc định phòng mới khi chọn cụ thể. Giữ CSRF/quyền hiện có, không đổi schema.
- Hai test SQLite giả về nhiều tòa và gán phòng/phạm vi đạt; build/JS syntax đạt. Chưa kiểm chứng thao tác trực quan thiết bị.

### 12/10/2026 — Bật trạng thái và ghi chú thêm dịch vụ
- Bật Đang áp dụng/Tạm ngừng, lưu dang_ap_dung cho giá tại mỗi tòa; ghi chú tối đa 500 ký tự lưu vào dich_vu.mo_ta hiện có, trim/chuỗi rỗng thành null. Tóm tắt cập nhật trạng thái theo form, giữ dữ liệu khi validation lỗi. Đã kiểm tra schema SQLite đọc-only, không đổi schema.
- Test ServiceCreationPersistsStatusAndNotesAndRejectsInvalidStatus trên DB giả đạt: lưu trạng thái/ghi chú và từ chối trạng thái giả. JS syntax/build đạt; chưa kiểm chứng thao tác trực quan thiết bị.

### 12/10/2026 — Menu trạng thái thêm dịch vụ
- Menu trạng thái bo góc/bóng nhẹ, ô cao 2.375rem, chữ 0.6875rem; nền vàng nhạt mục chọn. Giữ select gốc và change để lưu/tóm tắt; hỗ trợ phím điều hướng, Escape và đóng ngoài. JS syntax đạt; chưa kiểm chứng thao tác trực quan thiết bị.

### 12/10/2026 — Giao diện báo hỏng và bảo trì theo mẫu
- Dựng trang module BAO_HONG theo ảnh: bốn thống kê, bộ lọc, bảng sáu yêu cầu, phân trang minh họa, lịch bảo trì và khung chi tiết/timeline. Chỉ giao diện với dữ liệu minh họa có nhãn; nút và bộ lọc vô hiệu hóa theo yêu cầu, không thêm nghiệp vụ/DB. Responsive một cột dưới 1050px, bảng cuộn trong khung. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Nút bút sửa dịch vụ theo ảnh
- Đổi SVG thành bút chéo nét mảnh trong ô trắng 28px, viền xám và bo 6px theo ảnh. Giữ menu thao tác hiện có. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Menu thao tác dịch vụ nổi
- Menu absolute bên dưới nút bút, căn phải, bo 8px/bóng nhẹ; không giãn dòng bảng. Chừa vùng cuộn cho menu cuối bảng; đóng khi bấm ngoài/Escape, chỉ mở một menu. Giữ action/CSRF hiện có. JS syntax đạt; chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Khoảng cách Tin đăng cho thuê theo Tổng quan
- Bỏ giới hạn riêng 93.75rem và margin auto; wrapper rộng 100%, không thêm đệm, tiêu đề căn đầu hàng. Dùng dashboard-spacing.css chung: ngoài 1.75rem desktop, 1.25rem dọc/1rem ngang mobile, khung tối đa 100rem. HTTP trang đạt; chưa kiểm chứng trực quan thiết bị. Giữ chức năng tin đăng.

### 12/10/2026 — Cột thao tác tin đăng chỉ nút bút
- Bỏ Xem tin/menu trạng thái/nhãn cũ khỏi cột thao tác, thay nút bút trong ô trắng 28px dẫn tới Tao theo phòng. Giữ điều kiện phòng có thể sửa/đăng, nút khóa khi chưa đủ điều kiện; backend giữ nguyên.

### 12/10/2026 — Menu đổi trạng thái tin đăng
- Nút bút mở menu công khai/nháp/tạm ẩn/đã kết thúc/chưa đăng. POST CSRF kiểm tra quyền chủ nhà/quản lý, sở hữu, trạng thái đã xem, phòng trống/tòa hoạt động và tin công khai trùng. Giữ quy tắc công khai phải tạm ẩn trước về nháp. Chưa đăng không phải trạng thái lưu, không xóa tin/historic; phòng chưa có tin dẫn form tạo. Build/JS syntax đạt; chưa kiểm chứng thao tác trực quan thiết bị.

### 12/10/2026 — Tách nút bút và ba chấm tin đăng
- Nút bút trở lại liên kết chỉnh sửa, thêm ba chấm bên phải để mở menu trạng thái hiện có. Giữ điều kiện sửa tin và các POST trạng thái. Chưa kiểm chứng trực quan thiết bị.

### 12/10/2026 — Liên kết nút bút tới chỉnh sửa tin đăng
- Nút bút mở /TinDang/Sua theo ID tin của dòng; trang dùng lại biểu mẫu hiện có và nạp đúng nội dung tin. Phòng chưa có tin hiển thị nút vô hiệu hóa.
- GET kiểm tra quyền ghi và sở hữu tòa ở backend. Không đổi schema hoặc dữ liệu.
- Build demo thành công; kiểm tra HTTP đăng nhập, lấy liên kết bút và mở trang chỉnh sửa trả 200 có ID tin. Chưa kiểm tra trực quan trên thiết bị thật.

### 12/10/2026 — Bỏ mục Chưa đăng trong menu tin đăng
- Xóa mục Chưa đăng khỏi menu ba chấm; giữ bốn trạng thái còn lại. Đã kiểm tra nguồn Razor, không đổi nghiệp vụ hoặc schema.

### 12/10/2026 — Nhãn trạng thái Công khai
- Đổi Đang đăng thành Công khai trong bộ lọc, thẻ thống kê và nhãn trạng thái danh sách tin đăng. Giữ nguyên mã DANG_HIEN_THI và hành vi lọc. Đã kiểm tra nguồn Razor.

### 12/10/2026 — Đồng bộ khung trang tạo tin đăng với tổng quan
- Bỏ giới hạn chiều rộng và khoảng đệm riêng của trang tạo/chỉnh sửa tin đăng; dùng khung app-content chung (tối đa 100rem, đệm 1.75rem; màn hình nhỏ 1.25rem 1rem). Tiêu đề bắt đầu cùng vị trí trang tổng quan.
- Chỉ thay CSS phạm vi trang tin đăng, không đổi nghiệp vụ. Kiểm tra HTTP và stylesheet; chưa nghiệm thu trực quan ở 360px.

### 12/10/2026 — Xác minh khung trang chỉnh sửa tin đăng
- Trang Sua dùng chung biểu mẫu và class listing-create-page nên đã nhận chiều rộng, đệm hai bên và khoảng cách tiêu đề theo tổng quan từ dashboard-spacing.css.
- Kiểm tra HTTP mở trang Sua từ nút bút: trả 200, nạp đúng class và stylesheet chung. Không cần thêm CSS trùng lặp; chưa kiểm tra trực quan trên thiết bị thật.

### 12/10/2026 — Điều hướng trang chỉnh sửa tin đăng
- Áp dụng breadcrumb hiện có cho action Sua: Trang chủ / Tin đăng cho thuê / Chỉnh sửa tin đăng. Mục ở giữa liên kết về QuanLy.

### 12/10/2026 — Giao diện yêu cầu thuê theo hình tham chiếu
- Thẻ thống kê gọn, bảng danh sách và khung chi tiết bên phải; chọn dòng hoặc Enter để xem dữ liệu thật, có nút đóng. Avatar chữ cái và SVG theo theme. Responsive chuyển một cột ở màn hình nhỏ.
- Giữ bộ lọc, xuất CSV, phân trang và liên kết xử lý hiện có. Không thêm dữ liệu mẫu hoặc đổi schema. Các thông tin chưa có trong model danh sách không được giả lập.
- Build demo và HTTP /YeuCau thành công, Node --check JS đạt; chưa nghiệm thu trực quan trên trình duyệt/360px.

### 12/10/2026 — Đổi tên trang yêu cầu
- Đồng bộ tiêu đề, tên trang và breadcrumb thành Yêu cầu thuê & lịch xem. Giữ tên Yêu cầu của tôi cho khách thuê; không đổi chức năng.

### 12/10/2026 — Breadcrumb yêu cầu đồng bộ chi tiết phòng
- Thay biểu tượng ký tự bằng SVG nhà giống chi tiết phòng; cỡ chữ 1rem, độ đậm 600, khoảng cách .75rem và biểu tượng 21px theo room-overview.css. Không đổi liên kết hoặc nghiệp vụ.

### 12/10/2026 — Nút xuất yêu cầu theo hình
- Nút Xuất danh sách gọn, nền #30343B, chữ trắng và SVG tải xuống 14px; giữ chức năng xuất CSV. Kiểm tra nguồn và cú pháp JS hiện có, chưa kiểm tra trực quan.

### 12/10/2026 — Đồng bộ khoảng cách trang yêu cầu thuê với tổng quan
- Khung owner-requests.or-reference dùng 100% app-content, không thêm đệm/margin riêng; tiêu đề bắt đầu cùng vị trí tổng quan. Dùng đệm chung 1.75rem, màn hình nhỏ 1.25rem 1rem và chiều rộng tối đa 100rem.
- Chỉ áp dụng danh sách chủ nhà/quản lý, giữ bố cục bảng và khung chi tiết; không đổi luồng khách thuê hoặc nghiệp vụ.

### 12/10/2026 — Cân đối bốn thẻ thống kê yêu cầu
- Thẻ cao tối thiểu 7rem, đệm 1.25rem; nhãn .75rem, số liệu 1.75rem, mô tả .6875rem. Biểu tượng 36px, khoảng cách đều; màn hình nhỏ thu gọn và tránh tràn chữ.
- Kiểm tra HTTP và CSS được phục vụ; chưa kiểm tra trực quan trên thiết bị thật.

### 12/10/2026 — Biểu tượng tìm kiếm yêu cầu
- Thêm SVG kính lúp trước chữ trong ô tìm kiếm, căn giữa theo chiều cao và chừa khoảng đệm cho nội dung. Giữ ID và chức năng lọc hiện có.

### 12/10/2026 — Menu nổi bộ lọc yêu cầu
- Ba select được nâng thành menu trắng bo góc, bóng nhẹ, màu chọn vàng nhạt và dấu kiểm; hỗ trợ bàn phím, Escape và bấm ngoài để đóng. Giữ select gốc và change event để bảo toàn lọc/submit hiện có.
- Node --check đạt; chưa kiểm tra tương tác trực quan trên trình duyệt.

### 12/10/2026 — Bảng yêu cầu theo hình tham chiếu
- Thanh lọc gồm tìm kiếm, tòa nhà và tháng; bảng năm cột với avatar, phòng, ngày/giờ gửi, trạng thái và liên kết chi tiết. Sắp mới nhất trước, dòng chọn nền vàng nhạt; phân trang có nút trước/sau.
- Bộ lọc tháng chạy trên dữ liệu danh sách, giữ lọc trạng thái qua tab và tòa qua server. Xuất CSV cập nhật theo bốn cột dữ liệu. Không đổi schema.
- Node --check đạt; build/HTTP demo xác minh; chưa nghiệm thu trực quan.

### 12/10/2026 — Cân đối cỡ chữ danh sách yêu cầu
- Tab/bộ lọc .75rem, tên người thuê và phòng .8125rem, thông tin phụ .6875rem, nhãn trạng thái .625rem. Đồng bộ chiều cao bộ lọc 2.5rem, khoảng đệm dòng và phân trang; thu gọn ở màn hình nhỏ.
- Kiểm tra trang và stylesheet qua HTTP, chưa nghiệm thu trực quan.

### 12/10/2026 — Thanh tìm kiếm theo hình
- Ba ô cùng hàng theo tỷ lệ 3:1:.95, cao 2.25rem; kính lúp bên phải, ô tòa nhà và tháng gọn, viền/bo góc đồng bộ. Màn hình nhỏ đưa tìm kiếm lên hàng riêng.
- Kiểm tra stylesheet được phục vụ; không đổi chức năng lọc.

### 12/10/2026 — Căn mép thanh lọc với bảng
- Thanh lọc và bảng cùng chiều rộng 100%, bỏ đệm ngang và giới hạn chiều rộng tối thiểu của ô; dùng border-box để hai mép thẳng nhau. Giữ bố cục responsive và chức năng hiện có.

### 12/10/2026 — Ô chọn tháng theo hình
- Hiển thị nhãn Tháng M, YYYY cùng SVG lịch bên phải, giữ input month và sự kiện lọc. Nhãn cập nhật khi chọn; khi chưa lọc hiển thị tháng hiện tại Việt Nam. Node --check đạt; chưa kiểm tra trực quan.

### 12/10/2026 — Phân trang yêu cầu theo hình
- Trang hiện tại nền tối bo góc 6px; số còn lại nền trong suốt màu xám, mũi tên gọn hai đầu. Giữ điều hướng và trạng thái vô hiệu hóa ở đầu/cuối.

### 12/10/2026 — Căn lại ô tháng
- Dùng grid hai cột cho nhãn tháng và SVG lịch, ghi đè kiểu label chung khiến biểu tượng xuống dòng; giữ chiều cao 2.25rem và lịch căn phải. Kiểm tra stylesheet qua HTTP, chưa kiểm tra trực quan.

### 12/10/2026 — Bảng chọn tháng tiếng Việt
- Thay picker native bằng menu Tháng 1–12, Năm, Năm trước/sau và Xóa bộ lọc. Giữ input month và change event của bộ lọc. Node --check đạt; chưa kiểm tra tương tác trực quan.

### 12/10/2026 — Khung chi tiết yêu cầu theo hình
- Bổ sung thông tin phòng/ảnh/giá/diện tích, email, nghề nghiệp và số người từ dữ liệu hiện có; giao diện liên hệ, phòng, lời nhắn và ba nút điều hướng theo hình. Thời hạn thuê chưa lưu trong yêu cầu nên hiển thị Chưa cung cấp; không giả lập xác thực số điện thoại.
- Giữ luồng xử lý qua LichHen/ChiTiet và phạm vi truy vấn hiện có; không đổi schema. Build demo/HTTP đạt; chưa kiểm tra trực quan.

### 12/10/2026 — Hai nút xử lý yêu cầu
- Bỏ Hẹn xem phòng trong khung chi tiết danh sách; giữ Duyệt yêu cầu và đổi nhãn Từ chối yêu cầu. Hai nút dẫn đến trang xử lý hiện có.

### 12/10/2026 — Ba chấm mở chi tiết yêu cầu
- Thay dấu đóng bằng SVG ba chấm ở góc khung chi tiết. Liên kết cập nhật theo dòng được chọn tới LichHen/ChiTiet, ẩn khi không có yêu cầu. Node --check đạt.

### 12/10/2026 — Xử lý yêu cầu tại danh sách
- Hai nút đổi thành POST trực tiếp, CSRF, chỉ chủ nhà và quyền ghi; tái sử dụng LichHenService kiểm tra sở hữu, trạng thái và transaction. Từ chối mở lý do/ghi chú tại khung; duyệt xem phòng yêu cầu xác nhận khách đồng ý và đã hẹn lịch. Thành công/lỗi quay lại danh sách.
- Build và HTTP đạt: form render, yêu cầu/lý do không hợp lệ được xử lý, thiếu CSRF 400. Chưa kiểm tra luồng thành công thay đổi dữ liệu hoặc UI trực quan.

### 12/10/2026 — Cân đối khung chi tiết yêu cầu
- Đồng bộ đệm 1rem, nhịp khoảng cách, cỡ tên khách .875rem, nội dung .75rem và thông tin phụ .625–.6875rem; tên phòng dài tự xuống dòng. Hai nút cao tối thiểu 2.375rem, chữ .75rem.
- Chỉ đổi CSS, giữ xử lý trực tiếp; kiểm tra stylesheet được phục vụ, chưa kiểm tra trực quan.

### 12/10/2026 — Menu lý do từ chối
- Menu trắng bo góc, bóng nhẹ, chọn vàng nhạt và dấu kiểm; hỗ trợ bàn phím, bấm ngoài, giữ select gốc và validation bắt buộc. Tự khởi tạo lại khi chọn dòng yêu cầu. Node --check đạt; chưa kiểm tra tương tác trực quan.

### 12/10/2026 — Bỏ checkbox xác nhận khách đồng ý thuê
- Bỏ checkbox theo yêu cầu; bấm Duyệt yêu cầu gửi xác nhận cho luồng sau xem phòng. Giữ kiểm tra loại yêu cầu, lịch hẹn, sở hữu và trạng thái trong service. Kiểm tra nguồn Razor.

### 12/10/2026 — Khôi phục ba nút trong chi tiết yêu cầu
- Duyệt ở trên, Hẹn xem phòng có SVG lịch và Từ chối ở hàng dưới. Hẹn xem mở trang xử lý lịch hiện có; duyệt/từ chối vẫn POST từ danh sách, lý do từ chối mở ngay dưới. Giữ điều kiện duyệt sau hẹn lịch.

### 12/10/2026 — Hộp thoại từ chối yêu cầu
- Thay biểu mẫu nổi bằng dialog giữa màn hình có nền phủ, tiêu đề, lý do/ghi chú, Hủy và Xác nhận. Giữ POST, CSRF và menu lý do tiếng Việt; đóng bằng nút hoặc Escape. Node --check đạt.

### 12/10/2026 — Xác nhận duyệt yêu cầu
- Nút duyệt mở dialog xác nhận tên khách/phòng, Hủy và Xác nhận duyệt; chỉ POST sau xác nhận. Giữ điều kiện duyệt, CSRF và xử lý hiện có. Node --check đạt.

### 12/10/2026 — Trang sửa đơn giá theo hình
- Bố cục bảng mức giá hiện tại bên trái, thẻ tòa nhà/nguyên tắc; biểu mẫu cập nhật bên phải, tóm tắt giá và thông báo giữ hóa đơn cũ. Chọn dịch vụ trong bảng mở đúng Manage; lưu qua SavePrice hiện có.
- Giữ lịch sử, mặc định/trạng thái trong phần bổ sung; không thêm lựa chọn nhiều phòng hoặc ghi chú chưa có backend. Không đổi schema. Chưa kiểm tra trực quan.

### 12/10/2026 — Nút quay lại đơn giá
- Nút trắng gọn, viền nhẹ, SVG mũi tên và chữ .625rem theo hình; giữ liên kết về danh sách đơn giá của tòa.

### 12/10/2026 — Đồng bộ khoảng cách sửa đơn giá với tổng quan
- Trang service-price-page dùng khung app-content chung: tối đa 100rem, đệm 1.75rem; màn hình nhỏ 1.25rem 1rem. Bỏ khoảng đệm riêng và căn tiêu đề cùng vị trí tổng quan.
- Giữ bảng giá và biểu mẫu hai cột, không đổi nghiệp vụ; kiểm tra trang và CSS qua HTTP.

### 12/10/2026 — Breadcrumb sửa đơn giá đồng bộ chi tiết phòng
- SVG nhà 21px, chữ 1rem/600, khoảng cách .75rem và màu theo room-overview.css; giữ liên kết và nhãn Sửa đơn giá.

### 12/10/2026 — Tiêu đề Điện
- Đổi tiêu đề h1 trang sửa đơn giá từ Sửa đơn giá thành Điện theo yêu cầu.

### 12/10/2026 — Tiêu đề động theo dịch vụ
- Tiêu đề h1 lấy latest.DichVu.TenDichVu thay chữ Điện cố định, hiển thị đúng dịch vụ đang chỉnh sửa.

### 12/10/2026 — Thẻ tòa nhà sửa giá theo hình
- SVG tòa nhà, nhãn Khu nhà đang quản lý, tên, địa chỉ và tổng phòng từ DB; mũi tên bên phải liên kết về dịch vụ tòa. Không đổi schema.

### 12/10/2026 — Bảng mức giá theo hình
- Biểu tượng dịch vụ, số loại giá, cột phạm vi đếm phòng đã gán dịch vụ, dòng chân bảng và highlight dịch vụ đang sửa. Dữ liệu thật, không giả lập giá thuê phòng hoặc phạm vi toàn tòa; giữ liên kết Manage.

### 12/10/2026 — Thẻ nguyên tắc đơn giá theo hình
- SVG thông tin, nội dung gọn, đường phân cách và SVG đồng hồ bên dưới. Hiển thị ngày hiệu lực phiên bản gần nhất từ dữ liệu thật, không giả lập tên người/thời điểm cập nhật chưa có trong model.

### 12/10/2026 — Biểu mẫu chỉnh giá theo hình
- Icon/tiêu đề gọn, mức giá hiện tại/mới, khối phạm vi giá chung, tòa nhà, kỳ áp dụng cập nhật theo ngày, tóm tắt và nút Hủy/Lưu. Chỉ hiển thị khả năng hiện có: giá riêng qua hồ sơ phòng, không thêm chọn phòng hoặc ghi chú giả.

### 12/10/2026 — Bỏ nút lưu giá ban đầu
- Bỏ nút Lưu giá ban đầu và dòng hướng dẫn liên quan khỏi Manage; giữ Hủy và Lưu thay đổi theo phiên bản giá.

### 12/10/2026 — Đổi tòa tại trang sửa giá
- Mũi tên mở danh sách tòa có quyền. Chọn tòa giữ dịch vụ nếu có, nếu không mở dịch vụ đầu tiên; tòa chưa có giá về danh sách. GET kiểm tra sở hữu, không thay đổi dữ liệu.

### 12/10/2026 — Giao diện nhật ký phòng theo hình
- Thông tin phòng thật, tabs điều hướng, lọc ngày và tìm trong trang, dòng thời gian theo tháng, hồ sơ liên quan. Xuất CSV trang hiện tại. Nhật ký giữ chỉ đọc; ghi chú chưa có backend hiển thị vô hiệu hóa, không fake lưu.
- Không đổi schema/quyền; giữ phân trang và dữ liệu AuditDisplay đã lọc. Node --check đạt; chưa kiểm tra trực quan.

### 12/10/2026 — Bỏ liên kết tòa trên nhật ký phòng
- Bỏ dòng quay lại tên tòa phía trên tiêu đề theo yêu cầu; giữ liên kết chi tiết phòng trong thẻ thông tin.

### 12/10/2026 — Breadcrumb nhật ký phòng
- Trang chủ / Phòng cho thuê / Nhật ký phòng, liên kết giữa về danh sách phòng; kiểu chữ và SVG theo chi tiết phòng.

### 12/10/2026 — Biểu tượng xuất nhật ký
- SVG tải xuống 15px trong nút trắng bo góc nhẹ, giữ chức năng xuất CSV.

### 12/10/2026 — Thẻ tóm tắt nhật ký phòng theo hình
- Bốn cột ảnh/phòng, khách đứng tên, hợp đồng hiệu lực, giá thuê và dải thông tin dưới. Dùng dữ liệu thật; không giả lập thanh toán/công nợ. Responsive hai rồi một cột.

### 12/10/2026 — Thanh lọc nhật ký theo hình
- Tìm kiếm có SVG, chọn nhóm sự kiện, khoảng ngày trong popup, tab nhóm phía dưới. Lọc tìm/nhóm trên trang hiện tại; ngày lọc server. Nhóm chưa được truy vấn hiển thị vô hiệu hóa, không tạo sự kiện giả. Node --check đạt.

### 12/10/2026 — Đồng bộ khoảng cách nhật ký phòng với tổng quan
- Khung room-journal dùng app-content chung: tối đa 100rem, đệm 1.75rem, mobile 1.25rem 1rem. Bỏ margin/đệm riêng, căn tiêu đề cùng vị trí tổng quan. Giữ bố cục timeline và sidebar.
- Kiểm tra trang và stylesheet qua HTTP; chưa nghiệm thu trực quan.

### 12/10/2026 — Bỏ nút thêm ghi chú trên đầu trang
- Bỏ nút Thêm ghi chú cạnh Xuất nhật ký theo yêu cầu.

### 12/10/2026 — Thẻ thêm ghi chú theo hình
- Bố cục bản nháp, nội bộ, tiêu đề/nội dung, giới hạn ký tự, ghim, đính kèm và Hủy/Lưu. Giữ vô hiệu hóa do chưa có backend, không giả lập lưu/đính kèm.

### 12/10/2026 — Thẻ hồ sơ liên quan theo hình
- Icon nền xám, hai dòng thông tin, mũi tên chéo và chân thẻ. Giữ liên kết thật thông tin phòng/lịch sử khách/danh sách phòng, không giả lập biên bản/hóa đơn chưa được nối.

### 12/10/2026 — Liên kết khách thuê cuối thẻ phòng
- Chữ Xem hồ sơ khách thuê gọn, mũi tên chéo SVG; giữ đích LichSuNguoiO hiện có.

### 12/10/2026 — Dòng thời gian nhật ký theo hình
- Số sự kiện/nhãn sắp mới nhất, nhóm tháng có số sự kiện và đường phân cách; SVG theo thao tác, nhãn loại, nội dung/thay đổi và người thực hiện theo dữ liệu thật. Giữ nhật ký chỉ đọc và phân trang.

### 12/10/2026 — Ghi chú phòng hoạt động
- Lưu tiêu đề/nội dung, ghim và một tệp PDF/PNG/JPG <=10 MB; tệp riêng tư qua endpoint quyền và no-store. Chống gửi trùng UUID, audit metadata allowlist cùng giao dịch, không lưu nội dung/tiêu đề/tên tệp trong audit.
- Thêm schema v25 RoomNotesSchema; updater giữ v24, tạo backup. Chỉ nâng bản sao mới data/room-notes-demo-v25; DB gốc không thay đổi. Kiểm tra nâng lặp, bảo toàn số bản ghi cũ, integrity/FK và khởi tạo CSDL rỗng v25 đạt. Demo mặc định trỏ bản sao.
- Build đạt; verification/verify_room_notes.py đạt lưu/ghim/tệp, tải quyền, gửi trùng, tệp giả, CSRF, sở hữu chéo và rollback khi audit thất bại. Chưa kiểm tra trực quan mobile. Hướng dẫn nâng cấp/phục hồi: docs/room-notes-v25.md.

### 12/10/2026 — Nhãn sắp xếp nhật ký
- SVG sắp xếp 11px cạnh Mới nhất trước, màu xám/cỡ chữ gọn theo hình; giữ thứ tự truy vấn hiện có.

### 12/10/2026 — Cân đối bộ lọc nhật ký
- Ô lọc cao 2.375rem, chữ .75rem; tab nhóm .625rem với chiều cao 1.75rem, khoảng cách đều. Nhãn ngày căn trái, biểu tượng 14px. Giữ responsive và hành vi lọc.

### 12/10/2026 — Menu nổi loại sự kiện
- Select loại sự kiện đổi thành menu trắng, bo góc/bóng nhẹ, chọn vàng nhạt và dấu kiểm. Giữ select/change cho bộ lọc; tab nhóm đồng bộ nhãn. Hỗ trợ bàn phím/Escape/bấm ngoài. Node --check đạt.

### 12/10/2026 — Bỏ ô ghim ghi chú
- Bỏ checkbox Ghim ghi chú cho phòng này khỏi biểu mẫu; ghi chú mới mặc định không ghim, giữ dữ liệu cũ.

### 12/10/2026 — Giao diện chi tiết yêu cầu theo hình
- Chủ nhà: tiêu đề Chi tiết yêu cầu thuê & lịch xem, tiến trình, thông tin khách/phòng, lịch sử bên trái, lịch hẹn/kết quả/xử lý bên phải. Giữ form và backend xác nhận/đổi lịch/duyệt/từ chối/lập hợp đồng/hủy.
- Dữ liệu thật; thời hạn thuê và chat chưa có backend không giả lập, nút gửi vô hiệu hóa. Luồng khách giữ giao diện cũ. Không đổi schema.

- Kiểm chứng: build demo thành công; HTTP mở toàn bộ yêu cầu demo ở các trạng thái trả 200, có bố cục mới và phần xử lý. Chưa nghiệm thu trực quan trên mobile/trình duyệt.

### 12/10/2026 — Đồng bộ khoảng cách chi tiết yêu cầu với tổng quan
- Khung request-detail-page dùng app-content chung: tối đa 100rem, đệm 1.75rem, mobile 1.25rem 1rem. Không thêm đệm/margin riêng; căn tiêu đề cùng vị trí tổng quan. Giữ bố cục thông tin và xử lý hai cột.
- Kiểm tra trang và CSS qua HTTP; chưa nghiệm thu trực quan.

### 12/10/2026 — Nút quay lại danh sách ở chi tiết yêu cầu
- Nút trắng gọn, viền nhẹ, SVG mũi tên 13px và chữ .625rem theo hình; giữ liên kết về danh sách yêu cầu.

### 12/10/2026 — Thanh tiến trình theo hình
- Vòng tròn 20px, chữ gọn và đường nối mảnh giữa năm bước; màu hoàn tất tối, bước hiện tại vàng. Giữ trạng thái dữ liệu hiện có và bố cục responsive.

### 12/10/2026 — Thẻ khách và nhu cầu thuê theo hình
- Avatar/tên/nghề nghiệp, điện thoại gọi điện/email, bốn cột nhu cầu và khung lời nhắn gọn. Nghề/email lấy dữ liệu thực sau kiểm tra quyền; thời hạn/ngân sách chưa có thì hiển thị Chưa cung cấp. Không giả lập nguồn Website An Cư hoặc liên hệ đã xác nhận.

### 12/10/2026 — Thẻ phòng quan tâm theo hình
- SVG phòng nền xám, tên/địa chỉ, trạng thái tiếng Việt, ba cột diện tích/vị trí, giá và cọc; hàng liên kết phía dưới. Không giả lập tiện nghi chưa có trong model chi tiết yêu cầu.

### 12/10/2026 — Lịch sử trao đổi theo hình
- Avatar, tên/thời gian và bong bóng nội dung, composer có icon đính kèm/gửi. Dùng lời nhắn thật ban đầu, không thêm tin mẫu; chức năng chat vẫn vô hiệu hóa và ghi rõ chưa hỗ trợ.

### 12/10/2026 — Lịch hẹn xem phòng theo hình
- Ô tháng/ngày, giờ hẹn, SVG địa điểm/người, trạng thái lịch và nút xác nhận; đổi lịch trong khối mở rộng. Giữ input ngày/giờ 24h và form xử lý cũ. Không giả lập thời lượng hoặc xác nhận riêng của khách chưa có dữ liệu.

### 12/10/2026 — Thẻ kết quả xem phòng theo hình
- Nhãn nhỏ, SVG clipboard, mô tả và nút ghi nhận kết quả. Nút chuyển tới khối xử lý sẵn có để duyệt/từ chối; không giả lập lưu kết quả xem riêng chưa có backend.

### 12/10/2026 — Thẻ xử lý yêu cầu theo hình
- Trạng thái/người phụ trách, ghi chú và nút cập nhật theo bố cục hình; trường mới giữ chỉ đọc/vô hiệu hóa do chưa có lưu riêng. Các form nghiệp vụ duyệt, từ chối, lập hợp đồng/hủy giữ trong khối thao tác mở sẵn. Không cho đổi trạng thái bỏ qua service.

### 12/10/2026 — Tiến trình xử lý theo hình
- Đổi tiêu đề, số mốc cập nhật, đường dọc/chấm vàng, nội dung và người thực hiện bên trái, thời gian bên phải. Giữ lịch sử thực tế và dữ liệu đã lọc.

### 12/10/2026 — Cân đối chữ thẻ khách chi tiết yêu cầu
- Đệm 1.25rem, tiêu đề/tên .875rem, nội dung .75rem, thông tin phụ .625–.6875rem; avatar 36px và khoảng cách đều. Mobile thu đệm 1rem, giữ bố cục linh hoạt. Chỉ đổi CSS.

### 12/10/2026 — Cân đối thẻ phòng quan tâm
- Đệm 1.25rem, tiêu đề/tên phòng .875rem, thông số .75rem và chữ phụ .625–.6875rem; biểu tượng 36px, ba cột và liên kết căn đều. Mobile đệm 1rem. Chỉ đổi CSS.

### 12/10/2026 — Cân đối lịch sử trao đổi
- Đệm thẻ 1.25rem, avatar 30px, tiêu đề .875rem, nội dung .75rem, tên .6875rem; khung soạn gọn và nút cân chiều cao. Giữ chức năng gửi chưa hỗ trợ và trạng thái vô hiệu hóa.

### 12/10/2026 — Cân đối chữ thẻ lịch hẹn
- Đệm 1.25rem, tiêu đề .875rem, nội dung .75rem, chữ phụ .625rem; ô ngày 46px, SVG 14px, khoảng cách và hàng trạng thái đồng đều. Giữ dữ liệu và xử lý lịch hiện có.

### 12/10/2026 — Cân đối thẻ kết quả xem phòng
- Đệm 1.25rem, tiêu đề .875rem, mô tả .75rem; biểu tượng 28px, nút cao tối thiểu 2.375rem và chữ .6875rem. Chỉ đổi CSS, giữ liên kết tới khối xử lý.

### 12/10/2026 — Cân đối thẻ xử lý yêu cầu
- Đệm 1.25rem, tiêu đề .875rem, nhãn .6875rem, nội dung ô .75rem; ô và nút cao 2.375rem, textarea 5rem, khoảng cách đều. Giữ trạng thái trường/nút và nghiệp vụ hiện có.

### 12/10/2026 — Cân đối chữ tiến trình xử lý
- Đệm 1.25rem, tiêu đề .875rem, nội dung .75rem, chữ phụ .6875rem và thời gian .625rem. Căn thời gian phải, nhịp mốc và chấm/đường dọc đều; mobile chuyển thời gian lên trên. Chỉ đổi CSS.

### 12/10/2026 — Bỏ nút xem tin đăng ở thẻ phòng
- Bỏ Xem tin đăng, giữ Xem chi tiết phòng căn phải.

### 12/10/2026 — Mũi tên xem chi tiết phòng
- Thay mũi tên chéo bằng SVG sang phải 12px theo hình, giữ liên kết chi tiết phòng.

### 12/10/2026 — Bỏ thẻ lịch sử trao đổi
- Bỏ toàn bộ thẻ Lịch sử trao đổi và ô soạn tin khỏi chi tiết yêu cầu; lời nhắn ban đầu vẫn ở phần Mong muốn của khách.

### 12/10/2026 — Sắp lại bố cục chi tiết yêu cầu
- Cân cột chính và sidebar 21rem (23rem màn hình lớn), khoảng cách thẻ 1rem, đệm 1.125rem và giảm khoảng trống trong từng phần. Màn hình vừa thu sidebar/nhóm thông tin, dưới 800px chuyển một cột. Giữ chức năng và dữ liệu hiện có.
- Kiểm tra trang và CSS qua HTTP; chưa nghiệm thu trực quan.

### 12/10/2026 — Chuyển thẻ xử lý yêu cầu
- Chuyển Xử lý yêu cầu thuê sang cột trái ngay trên Tiến trình xử lý. Căn trạng thái/người phụ trách hai cột trong thẻ rộng, giữ forms nghiệp vụ.

### 12/10/2026 — Cân đối khối xử lý ở cột chính
- Đệm 1.25rem, nhãn .6875rem, nội dung .75rem; đồng bộ ô và nút thao tác 2.5rem, giảm khoảng trống và căn phần từ chối/hủy. Mobile xếp form một cột. Chỉ đổi CSS, giữ trạng thái và hành vi hiện có.

### 12/10/2026 — Sửa checkbox xác nhận thuê
- Ghi đè chiều rộng input chung cho checkbox: 15px, không padding; nhãn/chữ cùng hàng, căn trên khi chữ dài. Cân khoảng cách và nút duyệt; giữ required và xử lý hiện có.

### 12/10/2026 — Đặt nút hủy cạnh từ chối
- Bỏ mục Hủy yêu cầu riêng, đặt nút Hủy yêu cầu cạnh Từ chối; dùng formaction Huy cùng CSRF, bỏ validation lý do khi hủy và giữ xác nhận.


### 12/10/2026 — Giao diện hợp đồng & người ở ghép
- Sắp lại trang danh sách theo hình mẫu: 3 thẻ thống kê, bộ lọc riêng, bảng toàn chiều rộng và phần xem nhanh hợp đồng/thành viên cư trú bên dưới. Đồng bộ khoảng cách với tổng quan, CSS chỉ áp dụng trang danh sách.
- Số người ở ghép lấy từ ngày vào/ra thực tế; chọn hợp đồng chỉ trong tập dữ liệu được phép xem. Giữ các thao tác tạo, chi tiết và thêm người ở ghép hiện có; không đổi schema.
- Bộ lọc phòng/người ở ghép/hết hạn, phân trang và xuất CSV dùng dữ liệu đã được backend cấp quyền.
- Kiểm tra: build thành công (cảnh báo ImageSharp/CS8601 có sẵn), node --check, HTTP danh sách và chọn hợp đồng 200, chọn ngoài phạm vi 403. Chưa kiểm tra trực quan bằng trình duyệt hoặc thiết bị 360px.


### 12/10/2026 — Đồng bộ khoảng cách trang hợp đồng với tổng quan
- Áp dụng cùng khung app-content tối đa 100rem, lề 1.75rem (mobile 1.25rem/1rem), loại bỏ khoảng đệm riêng của wrapper hợp đồng. Tiêu đề cùng clamp 1.375–1.8125rem, nhịp giữa các khối 1.5rem, gap thẻ 1rem và padding thẻ theo tổng quan.
- CSS đặt trong dashboard-spacing.css nạp cuối, chỉ áp dụng danh sách hợp đồng ở giao diện quản trị. Không đổi nghiệp vụ.
- Kiểm tra HTTP trang/CSS và git diff --check; chưa kiểm tra trực quan trên thiết bị.

- 12/10/2026: Đổi biểu tượng Xuất danh sách sang SVG tải xuống theo mẫu; kiểm tra trang HTTP 200 và SVG đã render.

- 12/10/2026: Thẻ thống kê hợp đồng thu gọn theo mẫu: SVG hồ sơ/lịch/nhóm người, icon 32px, số 1.5rem, nhãn .75rem, mô tả .625rem; giữ dữ liệu thật và responsive.

- 12/10/2026: Cân chỉnh thẻ thống kê hợp đồng: số 1.625rem, nhãn .8125rem, mô tả .6875rem dễ đọc hơn, icon 36px; padding 1.125rem/1.25rem, chiều cao tối thiểu 5.5rem, mobile thu gọn. Kiểm tra CSS được phục vụ HTTP 200 và diff không lỗi khoảng trắng; chưa QA trực quan.

- 12/10/2026: Chỉnh bộ lọc hợp đồng theo mẫu: ô tìm có SVG kính lúp, ngày dữ liệu bên phải, bốn bộ lọc và nút Áp dụng đồng chiều cao; giữ bố cục responsive.

- 12/10/2026: Bốn bộ lọc hợp đồng dùng menu nổi bo góc/đổ bóng, trạng thái chọn màu vàng nhạt, đóng khi bấm ngoài/Escape và điều hướng phím mũi tên. Đồng bộ chiều cao 2.5rem; select gốc vẫn là nguồn giá trị cho bộ lọc.

- 12/10/2026: Đổi mũi tên bảng hợp đồng thành menu ba chấm với Chi tiết hợp đồng và Thêm người ở ghép khi trạng thái/quyền cho phép. Menu nổi ngoài vùng cuộn bảng, đóng khi bấm ngoài/Escape/cuộn.

- 12/10/2026: Bỏ khối xem nhanh chi tiết hợp đồng/thành viên bên dưới danh sách theo yêu cầu; bỏ truy vấn DetailModel phục vụ khối này. Truy cập chi tiết và thêm người qua menu ba chấm.

### 12/10/2026 — Giao diện tạo hợp đồng theo ảnh tham chiếu
- Chỉnh riêng `HopDong/Create`: thông tin phòng, người thuê, thời hạn và thanh toán thành các thẻ bên trái; tóm tắt tiền thuê, cọc và nút thao tác bên phải. CSS riêng chỉ nạp ở trang tạo hợp đồng; bố cục một cột trên màn hình nhỏ.
- Giữ nguyên các trường, CSRF, lưu nháp, tính ngày kết thúc, kiểm tra chồng lấn và xử lý backend. Không đổi schema. Chức năng mẫu hợp đồng/tải tài liệu trong ảnh chưa thuộc form hiện có.
- Xác minh: `dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore --nologo` thành công (cảnh báo ImageSharp và CS8601 có sẵn); HTTP sau đăng nhập demo trả 200, đủ 9 trường nghiệp vụ và CSRF; yêu cầu khác chủ nhà trả 403. Đã xem trang ở viewport hẹp trong trình duyệt; chưa nghiệm thu thiết bị thật hoặc toàn bộ luồng lưu.
- Chạy thử trên bản sao DB demo schema 25; DB mặc định schema 16 giữ nguyên. Mở `/HopDong/Create` tại localhost:5247.

### 12/10/2026 — Đồng bộ khoảng cách trang tạo hợp đồng với tổng quan
- Bỏ giới hạn riêng 1240px/760px và padding lồng của trang tạo hợp đồng; dùng trực tiếp khung `.app-content` chung (tối đa 100rem, lề desktop 1.75rem; mobile 1.25rem 1rem).
- Đồng bộ khoảng cách cột 1.25rem, khoảng cách thẻ/tiêu đề 1.5rem, đệm thẻ 1.25rem (mobile 1rem), kích thước tiêu đề theo tổng quan. Chỉ chỉnh CSS, giữ nguyên form và nghiệp vụ.
- Xác minh: CSS được phục vụ HTTP 200, `git diff --check` đạt. Không cần build lại cho thay đổi CSS; chưa đo trực tiếp kích thước bằng trình duyệt trong lần chỉnh này.

- 12/10/2026: bỏ breadcrumb trên HopDong/Create theo yêu cầu; build thành công và khởi động lại bản demo.

- 12/10/2026: thêm liên kết Hợp đồng & người ở ghép vào thanh đầu trang riêng HopDong/Create, giữa biểu tượng tổng quan và tiêu đề trang.

- 12/10/2026: đồng bộ breadcrumb đầu trang tạo hợp đồng với chi tiết phòng: chữ 1rem/600, gap .75rem, icon 21px, màu liên kết #25282D80, dấu phân cách #25282D66; giữ liên kết về danh sách hợp đồng.

- 12/10/2026: chỉnh nút Quay lại danh sách theo ảnh: nút trắng gọn, viền xám nhạt, bo góc 5px, chữ 11px, chiều cao tối thiểu 26px. Chỉ thay CSS.

- 12/10/2026: thẻ thông tin phòng theo ảnh: chọn tòa/phòng cạnh nhau, lọc phòng theo tòa, icon tòa nhà, dải địa chỉ/tầng/diện tích/trạng thái từ dữ liệu thật. Chỉ hiển thị các yêu cầu đã duyệt được phép lập hợp đồng; giữ YeuCauId và kiểm tra quyền hiện có, không đổi schema.

- 12/10/2026: cập nhật nút Quay lại danh sách theo ảnh mới: chữ đậm 600 màu #30343b, đệm 6px 12px, cao tối thiểu 30px; CSS riêng trang tạo hợp đồng.

- 12/10/2026: thẻ người thuê theo mẫu hai cột: họ tên, điện thoại, giấy tờ, email, quê quán toàn chiều rộng và số người dự kiến. Dùng HoSoAccess để kiểm tra quan hệ hợp đồng trước khi lấy hồ sơ, che điện thoại với người chưa có quyền. Giữ luồng lập hợp đồng từ yêu cầu đã duyệt; chưa thêm luồng tạo người thuê mới độc lập. Không đổi schema.

- 12/10/2026: bố cục Thời hạn & thanh toán theo ảnh: ba ô ngày/kỳ hạn, hai ô tiền kèm VND, ba ô chu kỳ/ngày chốt/hình thức ghi nhận thu; icon lịch. Giữ ngày kết thúc tự tính, kỳ hạn 1–120, cọc 0–3 tháng và ngày chốt hóa đơn. Hình thức thanh toán chưa lưu ở hợp đồng, hiển thị Chọn khi ghi nhận thu. Không đổi schema.

- 12/10/2026: thêm thẻ Phí dịch vụ dạng bảng theo ảnh, hiển thị dữ liệu thật qua LayGiaHoaDonAsync theo ngày hiện tại (giá riêng và kỳ ngừng dịch vụ được áp dụng). Dấu chọn biểu thị dịch vụ đang áp dụng; liên kết Quản lý phí dịch vụ mở chỉnh sửa phòng, không thay đổi lựa chọn ngay trong form hợp đồng. Không đổi schema.

- 12/10/2026: thêm thẻ Điều khoản & tài liệu theo bố cục ảnh. Kiểm tra model/schema mapping chưa có lưu điều khoản/tệp/tạo hóa đơn tự động: các điều khiển được vô hiệu hóa và ghi rõ trạng thái, không nhận dữ liệu rồi bỏ mất, không đổi schema hoặc tự thêm cơ chế lưu bên ngoài DB.

- 12/10/2026: menu nổi riêng cho Ngày chốt hóa đơn và Thời hạn hợp đồng: cùng chiều rộng ô, bo góc, bóng nhẹ, cuộn tối đa 240px, lựa chọn hiện tại có dấu tick. Giữ select gốc và dữ liệu POST, phát sự kiện input/change để cập nhật ngày kết thúc và kiểm tra chồng lấn; hỗ trợ phím mũi tên/Home/End/Escape/Tab, đóng khi bấm ngoài, fallback select gốc khi JS tắt.

- 12/10/2026: tóm tắt hợp đồng theo ảnh: phòng/người thuê, thời hạn/ngày thuê, giá thuê, tổng phí CO_DINH đang áp dụng, tổng tháng, cọc và dự kiến kỳ đầu. Tổng server dùng decimal, client dùng BigInt; chỉ ước tính trọn tháng, loại phí theo người/chỉ số, không phát hành hóa đơn. Cập nhật khi sửa giá/cọc/kỳ hạn/ngày chốt; nút xem trước mở hộp thoại tóm tắt, chưa tạo hợp đồng. Build và cú pháp JS kiểm tra thành công.

- 12/10/2026: áp dụng menu nổi đồng bộ cho Tòa nhà và Phòng/căn hộ. Menu phòng dựng lại ngay sau lọc tòa, giữ onchange tải dữ liệu yêu cầu; chữ dài thu gọn ở ô chọn và xuống dòng trong menu, dấu tick không co. Kiểm tra cú pháp JS và diff đạt.

- 12/10/2026: tinh chỉnh menu Thời hạn hợp đồng: giới hạn cuộn 220px, dòng cao 34px, khoảng cách 2px, mục đang chọn có nền champagne và vạch nhấn, viền focus bàn phím rõ. Chỉ thay CSS.

- 12/10/2026: đồng bộ menu Ngày chốt hóa đơn với menu thời hạn: cuộn 220px, dòng 34px, đệm 8px 12px, nền champagne và vạch nhấn ở lựa chọn hiện tại, focus bàn phím. Giữ giá trị ngày 1–31 và dữ liệu lưu.

- 12/10/2026: đổi nhãn liên kết Quản lý phí dịch vụ thành Thêm dịch vụ theo yêu cầu; giữ liên kết đến phần dịch vụ trong chỉnh sửa phòng.

- 12/10/2026: bỏ thẻ Kiểm tra hiệu lực hợp đồng. Giữ kiểm tra chồng lấn bằng JS và backend; chỉ hiện cảnh báo xung đột trong tóm tắt khi có chồng lấn, ngày thuê vẫn ở tóm tắt. Kiểm tra cú pháp JS và diff đạt.

- 12/10/2026: chỉnh thẻ Chỉ số điện nước đầu kỳ: icon công tơ SVG, mô tả bàn giao, hai ô cao 44px có đơn vị kWh/m³ và ví dụ gợi ý, hướng dẫn nền nhẹ. Mobile một cột; giữ giá trị, binder, min 0 và step 0.001.

- 12/10/2026: bỏ cụm nút Hủy/Lưu nháp/Tạo hợp đồng ở cuối cột trái theo yêu cầu; giữ nút ở tóm tắt, chuyển id save-contract sang nút submit liên kết form để giữ kiểm tra chồng lấn và thao tác lưu.

- 12/10/2026: thu gọn tóm tắt hợp đồng: đệm thẻ 16px, dòng thông tin 8px, thẻ phòng 10px 12px, tổng tiền 23px, chú thích 10px và nút cao tối thiểu 34px; CSS chỉ áp dụng trong cột tóm tắt, giữ nguyên dữ liệu/tính tiền.

- 12/10/2026: tóm tắt bám theo cuộn trên desktop bằng sticky, cách dưới header 16px (đo chiều cao header qua ResizeObserver). Giới hạn chiều cao theo viewport, cho cuộn nội dung khi màn hình thấp; bố cục một cột vẫn cuộn theo trang bình thường. Không đổi dữ liệu/nghiệp vụ.

- 12/10/2026: chỉnh trang chi tiết hợp đồng theo ảnh tham chiếu: dải tổng quan bốn mục, thẻ phòng/các bên/thời hạn/dịch vụ/điều khoản/tài liệu bên trái; tài chính/hóa đơn/hoạt động/quản lý bên phải. Cột phải bám theo cuộn trên desktop, bố cục thu về một cột ở màn hình nhỏ. Dùng snapshot dịch vụ của hợp đồng và hóa đơn thật; chỉ truy vấn/hiển thị hóa đơn khi có quyền TAI_CHINH. Giữ thao tác kích hoạt, gia hạn, người ở ghép và chỉ số bàn giao; phần chưa có dữ liệu hiển thị trạng thái trống. Không đổi schema. Build đạt (cảnh báo có sẵn); GET Details/2 và tab people trên DB thử nghiệm đều HTTP 200. Chưa kiểm tra trực quan trong trình duyệt.

- 12/10/2026: đồng bộ khoảng cách trang chi tiết hợp đồng với tổng quan: dùng khung app-content tối đa 100rem, lề chung 1.75rem (mobile 1.25rem/1rem), khoảng cách cột/thẻ 1rem, đệm thẻ 1.375rem, khoảng dưới tiêu đề 1.75rem. Quy tắc đặt trong dashboard-spacing.css nạp cuối để tránh CSS giao diện ghi đè; chỉ áp dụng trang chi tiết cho vai trò quản trị/chủ nhà/quản lý. CSS được phục vụ HTTP 200, diff check đạt; không đổi nghiệp vụ/CSDL.

- 12/10/2026: bỏ liên kết Quay lại danh sách ở đầu trang chi tiết hợp đồng theo yêu cầu; giữ các thao tác hợp đồng còn lại.

- 12/10/2026: chuyển nút Gia hạn hợp đồng vào cụm bên phải, ngay dưới Nhật ký phòng; hai nút cùng chiều rộng, cách nhau 8px. Giữ điều kiện ChoGiaHan và ẩn khi đang ở tab người ở.

- 12/10/2026: thêm nút Tải hợp đồng bên trái Gia hạn hợp đồng, cùng hàng dưới Nhật ký phòng. Nút tải vô hiệu hóa do trang chủ nhà chưa có endpoint xuất PDF; giữ chức năng gia hạn hiện có.

- 12/10/2026: bỏ margin và padding trên/dưới của contract-heading trên trang chi tiết hợp đồng, đồng bộ cả quy tắc desktop/mobile.

- 12/10/2026: gom nhóm thao tác đầu trang chi tiết hợp đồng thành lưới hai cột bên phải: lịch sử/nhật ký ở trên, tải/gia hạn ở dưới; nút cao tối thiểu 36px, cách 8px, trên mobile nhóm chiếm toàn chiều rộng. Giữ quyền và điều kiện hiện có.

- 12/10/2026: bỏ giao diện Thêm khách thuê riêng và lối tắt trên tổng quan. Liên kết Thêm người ở ghép trong danh sách chuyển đến tab người ở của chi tiết hợp đồng; URL cũ chuyển hướng, giữ kiểm tra quyền/sở hữu. Không xóa hồ sơ hoặc nghiệp vụ thêm người ở.

- 12/10/2026: bỏ menu nổi thao tác ở danh sách hợp đồng; dấu ba chấm là liên kết trực tiếp tới chi tiết hợp đồng, hỗ trợ bàn phím và mở tab mới.

- 12/10/2026: thẻ thông tin phòng trên chi tiết hợp đồng theo ảnh: icon tòa nhà SVG, bỏ mô tả phụ, ba cột tòa/phòng/diện tích-tầng; dải địa chỉ có icon vị trí và số người ở thật ở bên phải, responsive mobile.

- 12/10/2026: thẻ Các bên trong hợp đồng theo ảnh: hai cột, nhãn vai trò, avatar chữ cái, điện thoại/email/giấy tờ/địa chỉ. Liên hệ lấy từ tài khoản thật; CCCD và địa chỉ chủ nhà chưa có trường dữ liệu hiển thị Chưa cập nhật. Điện thoại/giấy tờ che ở server với người không phải chủ nhà hoặc admin. Không đổi schema.

- 12/10/2026: thẻ Thời hạn & thanh toán trên chi tiết theo ảnh: icon lịch SVG, ba cột ngày/kỳ hạn, đường phân cách, hai hàng thông tin tiền/chu kỳ; đệm và chữ đồng bộ thẻ phòng. Ngày ký chưa có dữ liệu hiển thị Chưa cập nhật; hình thức thanh toán chọn khi ghi nhận thu, giữ ngày chốt hóa đơn đúng nghiệp vụ. Build đạt, không đổi schema.

- 12/10/2026: thẻ Phí dịch vụ chi tiết theo ảnh: icon SVG, bảng viền bo góc, tiêu đề cột viết hoa, cách tính màu nhạt và đơn giá căn phải. Thêm tổng phí CO_DINH bên dưới, lấy snapshot hợp đồng, giữ cách tính theo người/chỉ số thực tế.

- 12/10/2026: bố cục Điều khoản hợp đồng theo ảnh: icon tài liệu SVG, mẫu hợp đồng, đường phân cách, điều khoản bổ sung và dải xác nhận có icon khiên. Model hiện chưa lưu mẫu/điều khoản/ngày ký: hiển thị chưa ghi nhận, không giả lập nội dung hoặc trạng thái đã ký. Không đổi schema.

- 12/10/2026: chỉnh thẻ Tài liệu hợp đồng theo bố cục ảnh: icon thư mục, số tài liệu bên phải, hàng trạng thái trống dạng tệp và nút thêm viền nét đứt. Chưa có dữ liệu/lưu tệp hợp đồng nên hiển thị 0 tài liệu và vô hiệu hóa nút thêm; không tạo tên tệp hoặc trạng thái đã ký giả.

- 12/10/2026: tóm tắt tài chính theo ảnh: tiêu đề/dòng tiền gọn, tổng tháng nền champagne, phần thanh toán ngăn bằng đường kẻ. Giữ cọc thỏa thuận và ngày chốt đúng dữ liệu, không đổi thành cọc đang giữ hoặc ngày thu tiền. Chưa có tài khoản ngân hàng nên hiển thị chưa cập nhật. Build đạt.

- 12/10/2026: thẻ Thanh toán liên quan theo ảnh: khoản cọc và từng hóa đơn có tiêu đề/trạng thái, mã/số tiền, hạn thanh toán, đường phân cách và nút xem hóa đơn. Chỉ hiển thị với quyền tài chính; cọc là thỏa thuận, trạng thái hóa đơn là nháp/phát hành, không suy đoán đã thanh toán hoặc còn nợ.

- 12/10/2026: Hoạt động gần đây theo ảnh: icon tròn SVG, tiêu đề/mô tả/thời gian gọn; hai lần gia hạn gần nhất xếp mới trước và sự kiện tạo hợp đồng từ dữ liệu thật, không thêm sự kiện ký hoặc nhận cọc giả.

- 12/10/2026: thẻ Quản lý hợp đồng theo ảnh: gửi bản sao/in/chấm dứt với SVG và ghi chú phụ lục. In mở hộp thoại trình duyệt với CSS ẩn điều hướng/cột bên; gửi bản sao và chấm dứt vô hiệu hóa do chưa có chức năng backend, không gửi thông tin ra ngoài. Quản lý người ở vẫn ở tab và lịch sử vẫn ở đầu trang. Build đạt.

- 12/10/2026: breadcrumb trang chi tiết hợp đồng: biểu tượng nhà / Hợp đồng & người ở ghép / Chi tiết hợp đồng. Mục giữa liên kết danh sách, mục cuối aria-current page, có responsive.

- 12/10/2026: đồng bộ breadcrumb chi tiết hợp đồng với rd-breadcrumb chi tiết phòng: chữ 1rem/600, gap .75rem, icon 21px nét 1.6, màu liên kết #25282D80, dấu phân cách #25282D66. Chỉ sửa CSS.

- 12/10/2026: giao diện thêm người ở ghép trong tab people theo bố cục ảnh: form trái gồm phòng/hợp đồng, thông tin người, giấy tờ, cư trú, tài liệu; tóm tắt phải và nút lưu cuối form. Giữ Input.HoTen/SoDienThoai/SoGiayTo/NgayVao/PhienBanPhong, CSRF, validation và backend kiểm tra sức chứa; danh sách/lịch sử người ở giữ phía dưới. Trường chưa lưu không tạo ô nhập giả; không đổi schema.

- 12/10/2026: đổi tiêu đề tab người ở từ Người ở trong hợp đồng thành Người ở ghép theo yêu cầu.

- 12/10/2026: bỏ khối thông tin cũ phía dưới form người ở ghép (sức chứa, người đứng tên, danh sách hiện tại/sắp vào/đã rời, quy định sức chứa) theo ảnh. Giữ form mới và tóm tắt, dữ liệu và kiểm tra backend không đổi.

- 12/10/2026: thẻ phòng/hợp đồng trong form người ở ghép theo ảnh: icon tòa nhà, hai ô readonly có khóa, dải địa chỉ/thời hạn/trạng thái thật, avatar chữ cái người đứng tên, điện thoại/email theo quyền và số người hiện tại. Không giả định đã ký.

- 12/10/2026: thẻ thông tin người ở ghép theo ảnh: icon người SVG, hàng họ tên/điện thoại, hàng ba cột ngày sinh/giới tính/quốc tịch, email/quan hệ, giấy tờ và ngày/nơi cấp. Các trường chưa hỗ trợ lưu được disabled và ghi rõ, giữ trường POST hiện có và responsive.

- 12/10/2026: thẻ Thông tin cư trú theo ảnh: icon nhà SVG, ngày vào/tạm trú hai cột, hướng dẫn thời hạn, địa chỉ toàn hàng và liên hệ khẩn cấp hai cột. Giữ ngày vào binder/validation; trường chưa lưu disabled.

- 12/10/2026: thẻ Tài liệu & ghi chú người ở ghép theo ảnh: icon thư mục, nhãn định dạng/kích thước, vùng tệp trống, nút kéo thả viền nét đứt và textarea sau đường phân cách. Các điều khiển chưa có lưu backend được disabled, không giả lập tệp đã tải.

- 12/10/2026: nút lưu người ở ghép đổi nhãn Lưu thêm người ở ghép, icon SVG thêm người, kiểu nút gọn theo ảnh; giữ submit form hiện có.

- 12/10/2026: sửa `run.bat` bắt cả mã thoát âm của .NET khi restore/khởi tạo/check schema/tạo ADMIN lỗi, dừng trước các bước tiếp theo và hiển thị DatabasePath cần dùng khi nâng cấp. Không tự nâng cấp DB đang dùng. Kiểm chứng `run.bat --check-only` trên bản sao v24: dừng với mã lỗi; updater v24 → v25 tạo backup, giữ toàn bộ dữ liệu 37 bảng cũ, integrity/FK đạt; check-only trên bản sao v25 trả 0. Build đạt, còn cảnh báo license ImageSharp. DB gốc vẫn v24, cần người dùng chạy updater cùng DatabasePath sau khi dừng app; chưa kiểm chứng khởi động web.

- 12/10/2026: tóm tắt bổ sung theo ảnh: trạng thái Chưa lưu, icon/thẻ phòng, người bổ sung avatar/tên/điện thoại/ngày vào cập nhật bằng textContent theo form, số người trước-sau. Tạm trú/tệp giữ trạng thái chưa ghi nhận/0 tệp. Không đổi lưu nghiệp vụ.

- 12/10/2026: rà đủ 44 AC Sprint 3 theo tệp người dùng gửi; báo cáo [kiem-tra-sprint3-20261012.md](kiem-tra-sprint3-20261012.md). xUnit liên quan 370 ca: 319 PASS, 51 FAIL, 0 SKIP; 32 lỗi truy vấn bảng dịch vụ hợp đồng tùy chọn, 12 lỗi fixture schema, 7 lỗi HTTP/HTML/model. Tái hiện trên SQLite tạm: hợp đồng mới không lưu snapshot dịch vụ dù form có dịch vụ; DB v25 hợp lệ chưa cài hóa đơn bị lỗi khi mở Details. Tab người ở hiện thiếu danh sách/nút chuyển đi; cảnh báo tới ngày chốt cần xác minh lại UI. Suite S308 gốc dừng tại fixture nâng cấp cũ; runner core tạm đạt 115 assertions, bỏ kiểm tra migration/browser và không sửa test gốc. Test HTTP 50 hóa đơn đạt ngưỡng <30s. Không sửa mã nghiệp vụ/schema/DB local; metadata DB local cuối lượt là v25. Chưa nghiệm thu trực quan 360px/Safari/PDF hoặc SMTP thật; chính sách giá gia hạn giữa tháng cần làm rõ.

- 12/10/2026: hoàn thiện theo yêu cầu tự sửa: phục hồi danh sách người ở/đang ở/sắp vào/đã rời và form chuyển đi; lưu snapshot dịch vụ/giá phòng khi tạo hoặc sửa Nháp cùng transaction, phục vụ PDF; chặn truy vấn bảng dịch vụ/hóa đơn tùy chọn chưa cài; thêm cảnh báo đến ngày chốt trên dashboard. PDF không ghi người chưa ở là đã chuyển đi; ngày chuyển đi vẫn được tính là đang ở. Cập nhật fixture schema/route/nhãn/masking, sửa bài test concurrent tránh treo synchronization context, bộ S308 dùng thanh toán thật và hỗ trợ Edge. **373/373 xUnit tổng hợp PASS**, sau chỉnh PDF/biên ngày **21/21 PASS** (2 ca mới; tổng **375 ca duy nhất**); **193 assertions S308 đầy đủ PASS**, gồm migration, HTTP, rollback và browser 360px. QA Edge trên DB giả xác nhận người ở ghép/chốt chỉ số/hợp đồng khách không tràn ngang 360px, lưu từng phòng; đã mở và kiểm tra trực quan PDF thật sau chỉnh. Không sửa schema/DB local hay project test csproj, không backfill giá hợp đồng lịch sử. Giữ công thức gia hạn chia ngày và chống trùng hợp đồng/tháng; chưa nghiệm thu Safari/thiết bị thật/SMTP thật hoặc review-hợp nhất. Báo cáo [hoan-thien-sprint3-20261012.md](hoan-thien-sprint3-20261012.md).
