# Đối chiếu backlog NhàTốt — 12/10/2026

Kết luận: chưa đủ toàn bộ tiêu chí. Báo cáo rà mã hiện tại (kể cả thay đổi chưa commit), schema đọc chỉ và kiểm thử tự động. Không sửa nghiệp vụ, không nâng cấp DB đang dùng.

## Phạm vi và cách đọc

Tệp gửi có tiêu đề 40 story nhưng chỉ chứa S1-01…S3-10 (30 story). Phần Sprint 4 dưới đây lấy từ AGENTS.md, không coi là nguyên văn tệp gửi.

“Đạt ở mức mã” nghĩa là tìm thấy triển khai đáp ứng nội dung AC, không đồng nghĩa đã nghiệm thu thủ công toàn bộ. Các điểm thiếu, khác quyết định và chưa xác nhận được ghi riêng. Kết quả test thất bại được giữ nguyên, không coi mọi lỗi assertion là lỗi nghiệp vụ.

DB mặc định `QL_PhongTro/Data/local-dev.sqlite` hiện ở v16, integrity_check=ok, foreign_key_check=0 lỗi. Chưa có `chi_so_dien_nuoc`, `hoa_don`, `chi_tiet_hoa_don`, `thanh_toan`, `thong_bao`, `yeu_cau_thue`. Đây là lệch schema với mã mới. Chưa xác định tiến trình web đang chạy dùng DB mặc định hay DatabasePath khác; không kết luận toàn bộ website đang dùng v16. Không chạy updater.

## Đối chiếu từng tiêu chí trong tệp gửi

### S1-01 — Là Khách thuê, tôi muốn tự đăng ký tài khoản bằng số điện thoại và email, để gửi yêu cầu thuê ngay khi thấy tin phù hợp mà không phải chờ ai cấp tài khoản.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/AccountController.cs; Controllers/RegisterViewModel.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Biểu mẫu gồm họ tên, số điện thoại, email và mật khẩu; số điện thoại đúng định dạng 10 chữ số bắt đầu bằng số 0 | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Mật khẩu tối thiểu 8 ký tự, có ít nhất một chữ cái và một chữ số, lưu dưới dạng băm BCrypt | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Trùng số điện thoại hoặc trùng email đã tồn tại thì bị từ chối kèm thông báo chỉ rõ trường nào trùng | Một phần / khác hành vi | Có báo trùng từng trường với tài khoản đã xác nhận. Cặp email/SĐT của tài khoản đang chờ được chuyển tới xác nhận thay vì từ chối trùng. |
| 4 | Tài khoản đăng ký thành công được gán vai trò Khách thuê và đăng nhập được ngay | Khác backlog, đúng quyết định hiện hành | Phải xác nhận mã email trước khi đăng nhập; không đăng nhập ngay sau gửi form. |

### S1-02 — Là Khách thuê, tôi muốn đăng nhập và duy trì phiên làm việc, để không phải nhập lại mật khẩu mỗi lần mở lại trang trên điện thoại.

Bằng chứng mã trong `QL_PhongTro/`: Services/CredentialValidationService.cs; TokenService.cs; TokenBlacklistService.cs; JwtSettings.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Đăng nhập bằng số điện thoại hoặc email cùng mật khẩu, trả về access token hạn 30 phút và refresh token hạn 7 ngày | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Sai mật khẩu 5 lần trong 15 phút thì khoá đăng nhập tài khoản đó 15 phút và báo rõ thời gian còn lại | Chưa đạt | FailedLoginCount cộng dồn, không có mốc cửa sổ 15 phút. Mật khẩu dưới 8 ký tự bị trả về trước bước đếm. Lần sai thứ năm chưa báo thời gian khóa ngay. |
| 3 | Thông báo lỗi khi sai thông tin không tiết lộ tài khoản có tồn tại hay không | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Đăng xuất làm mất hiệu lực refresh token, gọi lại API bằng token cũ trả về mã 401 | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-03 — Là Quản trị hệ thống, tôi muốn tạo và khoá tài khoản cho chủ nhà cùng quản lý toà nhà, để người trông coi nghỉ việc là mất quyền truy cập dữ liệu khách thuê ngay trong ngày.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/ManagedAccountsController.cs; Authorization/AppCookieEvents.cs; Services/PasswordEmailSender.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Tạo tài khoản với họ tên, email, số điện thoại và chọn vai trò trong danh sách Chủ nhà, Quản lý toà nhà, Quản trị hệ thống | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Hệ thống sinh mật khẩu tạm và gửi qua email, người dùng buộc đổi mật khẩu ở lần đăng nhập đầu tiên | Một phần / chưa xác nhận | Có sinh mật khẩu tạm, buộc đổi và sender SMTP/pickup. Chưa kiểm chứng email đến inbox thật. |
| 3 | Khoá tài khoản làm mọi phiên đang hoạt động của tài khoản đó hết hiệu lực trong vòng 1 phút | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Danh sách tài khoản lọc được theo vai trò và trạng thái, phân trang 20 dòng một trang | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-04 — Là Quản trị hệ thống, tôi muốn phân quyền theo vai trò và ẩn hiện menu tương ứng, để quản lý toà nhà không vào được màn hình công nợ và không thấy giá thuê thoả thuận riêng của từng phòng.

Bằng chứng mã trong `QL_PhongTro/`: Authorization/PermissionService.cs; ModuleAccessAttribute.cs; Views/Shared/_Sidebar.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Mỗi vai trò có danh sách quyền theo module đúng như bảng phân quyền, lưu trong cơ sở dữ liệu chứ không gán cứng trong mã nguồn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Menu bên trái chỉ hiển thị mục mà vai trò hiện tại có quyền xem | Có triển khai; chưa xác nhận toàn bộ ma trận | Menu được lọc theo quyền DB, nhưng test ma trận menu/route hiện thất bại; cần đối chiếu marker và quyền của từng vai trò. |
| 3 | Gọi thẳng API của module không có quyền trả về mã 403 kèm thông điệp thống nhất, không trả dữ liệu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Truy cập đường dẫn trực tiếp trên trình duyệt tới trang không có quyền thì chuyển về trang báo không đủ quyền | Khác backlog, đúng quyết định hiện hành | MVC trả trực tiếp HTTP 403; không chuyển trang như câu chữ backlog. |

### S1-05 — Là Khách thuê, tôi muốn đổi mật khẩu và lấy lại mật khẩu khi quên, để tự xử lý mà không phải nhờ chủ nhà gọi cho quản trị viên.

Bằng chứng mã trong `QL_PhongTro/`: Services/PasswordResetService.cs; Controllers/AccountController.Passwords.cs; AccountController.ResetPassword.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Đổi mật khẩu yêu cầu nhập đúng mật khẩu hiện tại, mật khẩu mới phải khác mật khẩu cũ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Quên mật khẩu gửi liên kết đặt lại qua email, liên kết hết hạn sau 30 phút và chỉ dùng được một lần | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Yêu cầu đặt lại mật khẩu giới hạn 3 lần trong một giờ cho mỗi địa chỉ email | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Đặt lại mật khẩu thành công thì mọi phiên đang đăng nhập của tài khoản đó bị đăng xuất | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-06 — Là Khách thuê, tôi muốn khai báo hồ sơ cá nhân gồm số căn cước và ảnh giấy tờ, để đến lúc ký hợp đồng chủ nhà không phải hỏi lại và chụp lại giấy tờ từ đầu.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HoSoController.cs; Services/HoSoAccess.cs; GiayToImageStore.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hồ sơ gồm họ tên, ngày sinh, số căn cước, quê quán, nghề nghiệp và ảnh mặt trước mặt sau giấy tờ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Ảnh chấp nhận JPG hoặc PNG, tối đa 5MB mỗi tệp, tự nén chiều rộng tối đa 1600px trước khi lưu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Số căn cước hiển thị dạng che bớt chỉ còn 4 chữ số cuối với mọi vai trò trừ chủ nhà của phòng đang thuê và quản trị hệ thống | Khác phạm vi backlog | Có che phía server. Chủ nhà có quan hệ hợp đồng còn được xem rõ với CHO_HIEU_LUC và DA_KET_THUC, theo quyết định hiện hành. |
| 4 | Trường số căn cước chỉ nhận 9 hoặc 12 chữ số, nhập sai thì báo lỗi ngay tại trường đó | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-07 — Là Chủ nhà, tôi muốn khai báo các toà nhà và khu trọ của mình, để mọi phòng, hợp đồng và hoá đơn về sau đều quy về đúng địa điểm khi xem báo cáo.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/PhongTroController.cs; Views/PhongTro/ToaNha.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Toà nhà gồm tên, địa chỉ đầy đủ tới phường xã, số tầng, người quản lý phụ trách và ghi chú | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Một toà nhà gán được cho một tài khoản Quản lý toà nhà, một quản lý phụ trách được nhiều toà | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Không cho xoá toà nhà đang có phòng, hệ thống đề nghị chuyển sang trạng thái ngừng hoạt động thay vì xoá | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Danh sách toà nhà hiển thị số phòng, số phòng đang trống và tìm kiếm theo tên hoặc địa chỉ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-08 — Là Chủ nhà, tôi muốn khai báo từng phòng với diện tích, giá thuê và trạng thái, để nhìn một màn hình là biết còn phòng nào trống thay vì lần lại sổ tay.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/PhongTroController.cs; ViewModels/PhongTroViewModels.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Phòng gồm mã phòng, toà nhà, tầng, diện tích theo mét vuông, giá thuê theo tháng, số người ở tối đa và trạng thái | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Trạng thái phòng thuộc một trong bốn giá trị: Trống, Đã đặt cọc, Đang thuê, Ngừng cho thuê | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Mã phòng không được trùng trong cùng một toà nhà, trùng thì báo lỗi ngay khi lưu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Giá thuê là số nguyên dương tính theo VND, tối thiểu 500.000 và hiển thị có dấu chấm ngăn cách nghìn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Tạo nhanh nhiều phòng theo mẫu số tầng và số phòng mỗi tầng, hệ thống sinh mã phòng tự động | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-09 — Là Chủ nhà, tôi muốn khai báo các dịch vụ kèm theo và đơn giá của chúng, để hoá đơn hằng tháng lấy đúng đơn giá thay vì mỗi tháng gõ lại bằng tay.

Bằng chứng mã trong `QL_PhongTro/`: Services/DichVuService*.cs; DichVuThamChieu.cs; HoaDonDichVuService*.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Dịch vụ gồm tên, cách tính tiền theo chỉ số hoặc theo đầu người hoặc cố định theo phòng, đơn vị tính và đơn giá | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Hệ thống tạo sẵn năm dịch vụ mặc định: điện, nước, rác, gửi xe, internet, chủ nhà sửa được đơn giá | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Sửa đơn giá phải ghi ngày bắt đầu hiệu lực, hoá đơn đã phát hành trước ngày đó giữ nguyên đơn giá cũ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Không cho xoá dịch vụ đang được tham chiếu bởi hợp đồng hoặc hoá đơn, chỉ cho ngừng áp dụng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S1-10 — Là Quản trị hệ thống, tôi muốn xem nhật ký thao tác trên dữ liệu quan trọng, để khi khách nói hoá đơn bị sửa thì có căn cứ trả lời chứ không phải đoán.

Bằng chứng mã trong `QL_PhongTro/`: Data/AppDbContext.Audit.cs; Data/AuditSchema.cs; Controllers/NhatKyController.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Ghi lại thao tác tạo, sửa, xoá trên phòng, đơn giá dịch vụ, hợp đồng, chỉ số điện nước, hoá đơn và thanh toán | Một phần | Có audit phòng, giá, hợp đồng, chỉ số, hóa đơn. ThanhToanHoaDon chưa thuộc allowlist audit; chưa có luồng thu tiền đầy đủ. |
| 2 | Mỗi dòng nhật ký có thời điểm, người thực hiện, vai trò, đối tượng tác động và giá trị trước sau của các trường bị đổi | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Lọc nhật ký theo khoảng ngày, theo người thực hiện và theo loại đối tượng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Nhật ký chỉ đọc, không tài khoản nào sửa hay xoá được kể cả quản trị hệ thống | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-01 — Là Chủ nhà, tôi muốn gán dịch vụ và đơn giá riêng cho từng phòng, để phòng tầng trệt có thêm phí gửi xe còn phòng trên tầng thì không, mà vẫn tính đúng khi lên hoá đơn.

Bằng chứng mã trong `QL_PhongTro/`: Services/DichVuPhongService.cs; Controllers/DichVuPhongController.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Mỗi phòng chọn được các dịch vụ áp dụng, mặc định lấy theo cấu hình của toà nhà | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Cho phép đặt đơn giá riêng cho một dịch vụ ở một phòng, khi đó đơn giá riêng thắng đơn giá chung | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Màn hình phòng hiển thị bảng dịch vụ đang áp dụng kèm tổng chi phí cố định dự kiến mỗi tháng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Bỏ áp dụng một dịch vụ chỉ ảnh hưởng tới hoá đơn phát hành từ kỳ sau, không đụng vào hoá đơn cũ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-02 — Là Chủ nhà, tôi muốn tải lên ảnh thực tế của phòng, để khách nhìn đúng phòng rồi mới hẹn đến xem, đỡ mất công dẫn khách lên xuống cả buổi.

Bằng chứng mã trong `QL_PhongTro/`: Services/RoomImageStore.cs; RoomImageDeletionService.cs; wwwroot/js/room-images.js.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Mỗi phòng tải được tối đa 8 ảnh, mỗi ảnh tối đa 5MB, định dạng JPG hoặc PNG | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Kéo thả để sắp xếp thứ tự ảnh, ảnh đầu tiên là ảnh đại diện hiển thị trên danh sách tin | Có triển khai; chưa nghiệm thu cảm ứng | Có sắp xếp và ảnh đầu đại diện; chưa thao tác kéo thả trên thiết bị thật trong lượt này. |
| 3 | Ảnh được tạo thêm bản thu nhỏ chiều rộng 400px dùng cho danh sách để tải nhanh trên 3G | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Xoá ảnh có hỏi xác nhận và xoá cả tệp trên kho lưu trữ, không để lại tệp mồ côi | Có triển khai; có giới hạn | Có xác nhận, xóa tệp và retry. Tệp bị khóa có thể chưa xóa tức thì; không chứng minh không còn tệp mồ côi trong mọi tình huống crash. |

### S2-03 — Là Chủ nhà, tôi muốn đăng tin cho thuê từ một phòng đang trống, để không phải gõ lại diện tích và giá đã có sẵn trong hệ thống mỗi lần đăng.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/TinDangController.cs; Services/TinDangExpirationService.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Chỉ phòng ở trạng thái Trống mới tạo được tin đăng mới, phòng đang thuê thì nút đăng tin bị vô hiệu kèm lý do | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Tin đăng lấy sẵn diện tích, giá thuê, ảnh và dịch vụ của phòng, chủ nhà chỉnh tiêu đề và mô tả thêm | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Tin có trạng thái Nháp, Đang hiển thị, Tạm ẩn, Đã cho thuê và ngày hết hạn hiển thị mặc định 30 ngày | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Một phòng chỉ có tối đa một tin đang hiển thị tại một thời điểm | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Tin quá ngày hết hạn tự chuyển sang Tạm ẩn và chủ nhà thấy cảnh báo trên danh sách tin | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-04 — Là Khách thuê, tôi muốn tìm và lọc tin theo khu vực, khoảng giá và diện tích, để nhanh chóng loại bỏ những phòng ngoài khả năng chi trả.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/TimTinController.cs; ViewModels/TimTinViewModel.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Lọc theo quận huyện, khoảng giá thuê, khoảng diện tích và số người ở tối đa, các bộ lọc kết hợp được với nhau | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Chỉ hiển thị tin ở trạng thái Đang hiển thị và chưa quá ngày hết hạn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Sắp xếp theo mới đăng nhất, giá tăng dần hoặc giá giảm dần; phân trang 12 tin mỗi trang | Một phần | Có ba kiểu sắp xếp, nhưng KichThuocTrang = 6, yêu cầu là 12 tin/trang. |
| 4 | Kết quả trả về dưới 2 giây với 500 tin trong cơ sở dữ liệu | Chưa đủ bằng chứng nghiệm thu | Chưa đo endpoint tìm tin với 500 tin trong lượt này; test pass không thay thế phép đo AC này. |
| 5 | Không có kết quả thì hiển thị gợi ý nới rộng khoảng giá thay vì để trang trắng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-05 — Là Khách thuê, tôi muốn xem trang chi tiết tin với đầy đủ chi phí kèm theo, để biết tổng tiền phải trả mỗi tháng chứ không chỉ mỗi tiền phòng.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/TinDangController.cs; Views/TinDang/ChiTiet.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Trang hiển thị bộ ảnh, diện tích, giá thuê, số người ở tối đa, tiền cọc dự kiến và mô tả | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Bảng dịch vụ liệt kê rõ điện và nước tính theo đơn giá bao nhiêu một đơn vị, các khoản cố định bao nhiêu một tháng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Có dòng ước tính tổng chi phí tháng đầu gồm tiền phòng cộng các khoản cố định, kèm chú thích chưa bao gồm điện nước theo thực tế sử dụng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Trang xem được khi chưa đăng nhập, bố cục đọc tốt trên màn hình rộng 360px | Một phần / chưa xác nhận | Có truy cập ẩn danh và CSS responsive; chưa nghiệm thu trực quan 360px/Chrome/Safari. |

### S2-06 — Là Khách thuê, tôi muốn gửi yêu cầu thuê hoặc đặt lịch xem phòng ngay trên tin đăng, để không phải gọi điện nhiều lần mà vẫn giữ được chỗ.

Bằng chứng mã trong `QL_PhongTro/`: Services/YeuCauThueService.cs; Controllers/TinDangController.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Biểu mẫu gồm loại yêu cầu là Xem phòng hoặc Thuê ngay, ngày mong muốn, số người dự kiến ở và lời nhắn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Ngày mong muốn không được là ngày quá khứ và không xa quá 60 ngày kể từ hôm nay | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Số người dự kiến ở vượt số người tối đa của phòng thì bị từ chối kèm thông báo nêu rõ giới hạn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Một tài khoản chỉ gửi được một yêu cầu đang mở cho cùng một tin, gửi lại bị từ chối và chỉ dẫn tới yêu cầu cũ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Gửi thành công sinh mã yêu cầu dạng YC-yyyyMM-xxxx và hiển thị ngay cho khách | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-07 — Là Chủ nhà, tôi muốn xem danh sách yêu cầu theo trạng thái trên một màn hình, để sáng ra biết ngay hôm nay phải trả lời những ai thay vì lục lại tin nhắn.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/YeuCauController.cs; Views/YeuCau/_OwnerRequests.cshtml; Views/Shared/_Sidebar.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Danh sách hiển thị mã yêu cầu, tên khách, số điện thoại, phòng, loại yêu cầu, ngày mong muốn và trạng thái | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Lọc theo trạng thái Mới, Đã hẹn lịch, Đã duyệt, Từ chối, Đã huỷ và lọc theo toà nhà | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Mặc định sắp xếp yêu cầu mới nhất lên đầu và đánh dấu nổi bật yêu cầu chưa xử lý quá 24 giờ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Hiển thị số lượng yêu cầu chưa xử lý ngay trên menu để không bỏ sót | Chưa đạt | Sidebar hiện chỉ render icon và tiêu đề; không có số yêu cầu chưa xử lý. Thống kê trong trang yêu cầu/trang chủ không thay thế badge ngay trên menu. |

### S2-08 — Là Chủ nhà, tôi muốn xác nhận lịch hẹn xem phòng, đổi lịch hoặc từ chối yêu cầu, để khách biết rõ có đến được hay không mà không phải gọi lại hỏi.

Bằng chứng mã trong `QL_PhongTro/`: Services/LichHenService.cs; Controllers/LichHenController.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Xác nhận lịch phải chọn ngày giờ cụ thể, hệ thống cảnh báo nếu đã có lịch khác cùng phòng trong khoảng 30 phút | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Từ chối bắt buộc chọn lý do trong danh sách gồm đã có khách thuê, không phù hợp số người, khách không liên lạc được, lý do khác kèm ghi chú | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Mỗi lần đổi trạng thái đều ghi lịch sử với người thực hiện và thời điểm, khách xem được lịch sử này | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Duyệt yêu cầu loại Thuê ngay chuyển phòng sang trạng thái Đã đặt cọc và mở nút lập hợp đồng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-09 — Là Khách thuê, tôi muốn theo dõi trạng thái các yêu cầu đã gửi, để biết mình còn phải chờ hay nên đi tìm phòng khác.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/YeuCauController.cs; Views/YeuCau/_TenantRequests.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Danh sách yêu cầu của tôi hiển thị mã yêu cầu, phòng, ngày gửi, trạng thái hiện tại và lịch hẹn nếu có | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Yêu cầu bị từ chối hiển thị lý do chủ nhà đã chọn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Khách tự huỷ được yêu cầu khi trạng thái còn là Mới hoặc Đã hẹn lịch, huỷ rồi thì không khôi phục | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Từ mỗi dòng bấm được sang trang chi tiết tin đăng tương ứng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S2-10 — Là Chủ nhà, tôi muốn cấu hình cách tính tiền điện nước cho từng toà nhà, để toà dùng nước khoán theo đầu người và toà dùng đồng hồ riêng đều tính đúng.

Bằng chứng mã trong `QL_PhongTro/`: Services/DichVuService.DienNuoc.cs; HoaDonDichVuService.Monthly.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Mỗi dịch vụ điện và nước ở một toà chọn được cách tính theo chỉ số đồng hồ hoặc khoán theo đầu người | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Cách tính theo chỉ số yêu cầu nhập đơn giá trên một đơn vị, cách tính khoán yêu cầu nhập số tiền một người một tháng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Đổi cách tính chỉ áp dụng từ kỳ hoá đơn kế tiếp, hệ thống hiển thị rõ kỳ bắt đầu áp dụng trước khi lưu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Không cho lưu cấu hình thiếu đơn giá hoặc đơn giá bằng 0 | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-01 — Là Chủ nhà, tôi muốn lập hợp đồng thuê từ yêu cầu đã duyệt, để tiền cọc, ngày bắt đầu và giá thuê được ghi lại một chỗ thay vì ghi trên tờ giấy dễ thất lạc.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HopDongController.cs; ViewModels/HopDongCreateViewModel.cs; Services/HopDongService.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hợp đồng lấy sẵn thông tin khách và phòng từ yêu cầu đã duyệt, chủ nhà nhập tiền cọc, giá thuê chốt, ngày bắt đầu, kỳ hạn tính theo tháng và ngày chốt hoá đơn hằng tháng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Ngày kết thúc tự tính từ ngày bắt đầu cộng kỳ hạn, không cho sửa tay để tránh lệch với kỳ hạn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Không cho lập hợp đồng mới cho phòng đã có hợp đồng hiệu lực chồng lấn khoảng thời gian, hệ thống chỉ ra hợp đồng đang vướng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Tiền cọc mặc định bằng một tháng giá thuê, sửa được nhưng không nhỏ hơn 0 và không lớn hơn ba tháng giá thuê | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Lưu thành công sinh mã hợp đồng dạng HD-yyyy-xxxx, chuyển phòng sang Đang thuê và ghi chỉ số điện nước đầu kỳ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-02 — Là Chủ nhà, tôi muốn ghi nhận danh sách người ở ghép trong phòng bên cạnh người đứng tên, để tính đúng các khoản khoán theo đầu người và biết trong phòng hiện có những ai.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HopDongController.cs; Services/HoaDonDichVuService.cs; LichSuNguoiOService.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Mỗi hợp đồng có đúng một người đứng tên, là người chịu trách nhiệm thanh toán và nhận lại tiền cọc | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Thêm được người ở ghép với họ tên, số điện thoại, số căn cước và ngày bắt đầu ở cùng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Tổng số người ở gồm người đứng tên và người ở ghép không vượt số người tối đa của phòng, vượt thì chặn kèm thông báo nêu rõ giới hạn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Ghi nhận ngày chuyển đi của một người ở ghép, từ kỳ hoá đơn sau các khoản khoán theo đầu người giảm tương ứng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Lịch sử người ở của phòng tra lại được theo từng khoảng thời gian | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-03 — Là Khách thuê, tôi muốn xem hợp đồng của mình và tải bản PDF, để lúc cần đối chiếu điều khoản thì mở điện thoại ra xem chứ không phải tìm tờ giấy.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HopDongCuaToiController.cs; Services/HopDongPdfService.cs; ViewModels/HopDongCuaToiViewModels.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Trang hợp đồng hiển thị mã hợp đồng, phòng, giá thuê, tiền cọc, ngày bắt đầu, ngày kết thúc và danh sách người ở | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Tải được bản PDF có đầy đủ các thông tin trên cùng bảng dịch vụ và đơn giá áp dụng | Có triển khai; chưa nghiệm thu bố cục | Có sinh PDF gồm thông tin và dịch vụ; chưa render kiểm tra bố cục PDF trong lượt này. |
| 3 | Khách chỉ xem được hợp đồng mà mình là người đứng tên hoặc người ở ghép, gọi tới hợp đồng khác trả về mã 403 | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Hợp đồng còn dưới 30 ngày là hết hạn thì hiển thị cảnh báo kèm số ngày còn lại | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-04 — Là Chủ nhà, tôi muốn phòng và tin đăng tự đổi trạng thái khi hợp đồng có hiệu lực, để không còn cảnh khách gọi hỏi một phòng đã cho thuê từ tuần trước.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HopDongController.cs; Tests/ContractActivationTests.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hợp đồng chuyển sang hiệu lực thì phòng chuyển sang Đang thuê và tin đăng của phòng chuyển sang Đã cho thuê trong cùng một giao dịch | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Tin đã ẩn không còn xuất hiện trên trang tìm kiếm công khai ngay lần tải trang kế tiếp | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Nếu một bước thất bại thì toàn bộ giao dịch bị hoàn tác, không để phòng đang thuê mà tin vẫn hiển thị | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Mọi lần đổi trạng thái phòng đều được ghi vào nhật ký hoạt động | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-05 — Là Quản lý toà nhà, tôi muốn nhập chỉ số điện nước cuối kỳ ngay trên điện thoại khi đi ghi từng phòng, để không phải ghi giấy rồi tối về gõ lại và gõ nhầm.

Bằng chứng mã trong `QL_PhongTro/`: Services/ChiSoDienNuocService*.cs; Views/ChiSoDienNuoc/Index.cshtml.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Màn hình nhập liệt kê toàn bộ phòng đang thuê của toà theo thứ tự tầng và mã phòng, hiển thị sẵn chỉ số kỳ trước | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Chỉ số mới không được nhỏ hơn chỉ số kỳ trước, nhập nhỏ hơn thì chặn lưu và báo lỗi ngay tại dòng đó | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Mức tiêu thụ chênh quá 200 phần trăm so với trung bình ba kỳ gần nhất thì cảnh báo và yêu cầu người nhập xác nhận trước khi lưu | Có triển khai; cần thống nhất cách hiểu | Mã dùng tiêu thụ mới > 2 lần trung bình 3 kỳ; không cảnh báo khi thiếu đủ 3 kỳ. Nếu PO hiểu tăng thêm >200% (tức >3 lần), cần đổi đặc tả/công thức. |
| 4 | Lưu được từng phòng một, phòng chưa nhập vẫn giữ nguyên trạng thái chưa chốt và hiển thị rõ còn bao nhiêu phòng chưa nhập | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Bố cục dùng được bằng một tay trên màn hình rộng 360px, ô nhập bật sẵn bàn phím số | Chưa đủ bằng chứng nghiệm thu | Có input số và CSS mobile, chưa kiểm chứng dùng một tay trên màn hình 360px/thiết bị thật. |

### S3-06 — Là Chủ nhà, tôi muốn phát hành hoá đơn tháng cho cả toà chỉ bằng một lần thao tác, để không mất nửa buổi gõ Excel và cộng nhầm tiền điện.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HoaDonDichVuController.cs; Services/HoaDonDichVuService.Monthly.cs; HoaDonDichVuService.Publish.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hoá đơn gồm tiền phòng, tiền điện và nước tính theo chỉ số nhân đơn giá, các khoản dịch vụ cố định và khoản khoán theo đầu người | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Tiền điện nước lấy đúng đơn giá có hiệu lực tại ngày chốt kỳ, không lấy đơn giá sửa sau đó | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Hoá đơn có kỳ áp dụng, ngày phát hành và hạn thanh toán mặc định 7 ngày kể từ ngày phát hành, sửa được trước khi phát hành | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Phòng chưa chốt chỉ số thì bị bỏ qua và liệt kê rõ trong kết quả chạy, không phát hành hoá đơn thiếu dữ liệu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 5 | Một phòng chỉ có một hoá đơn cho một kỳ, chạy lại lần hai không tạo hoá đơn trùng, phát hành cho 50 phòng hoàn tất dưới 30 giây | Một phần / khác quyết định hiện hành | Có chống trùng và phát hành tối đa 50 Nháp một lần. Unique theo hợp đồng/tháng, khác phòng/kỳ. Test HTTP phát hành 50 Nháp, khóa 100 chỉ số, tạo 50 thông báo, gửi lặp và ngưỡng <30 giây đã PASS trong lượt mới (toàn test 4,92 giây). |

### S3-07 — Là Khách thuê, tôi muốn xem hoá đơn tháng với từng khoản được bóc tách, để biết tiền điện tháng này cao là do dùng nhiều chứ không phải bị tính nhầm.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/ThongBaoController.cs; ViewModels/ChiTietHoaDonKhachViewModel.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hoá đơn hiển thị từng dòng khoản mục kèm chỉ số đầu kỳ, chỉ số cuối kỳ, số đơn vị tiêu thụ, đơn giá và thành tiền | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Có dòng tổng cộng, số đã thanh toán, số còn phải trả và hạn thanh toán | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Hoá đơn quá hạn hiển thị nhãn quá hạn kèm số ngày trễ tính theo múi giờ Asia/Ho_Chi_Minh | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Danh sách hoá đơn của tôi lọc được theo kỳ và theo trạng thái thanh toán | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-08 — Là Chủ nhà, tôi muốn kiểm tra và sửa hoá đơn nháp trước khi gửi cho khách, để không phải xin lỗi rồi gửi lại hoá đơn đính chính.

Bằng chứng mã trong `QL_PhongTro/`: Services/HoaDonDichVuService.Draft.cs; .Publish.cs; .Cancel.cs; .History.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Hoá đơn sinh ra ở trạng thái Nháp, chỉ khi bấm phát hành mới gửi thông báo cho khách | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Ở trạng thái Nháp được sửa chỉ số, thêm khoản phát sinh hoặc khoản giảm trừ kèm ghi chú bắt buộc | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Hoá đơn đã phát hành không sửa được, chỉ huỷ kèm lý do rồi phát hành lại, bản huỷ vẫn lưu để tra cứu | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Mọi lần sửa hoá đơn nháp đều ghi nhật ký với giá trị trước và sau | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-09 — Là Chủ nhà, tôi muốn gia hạn hợp đồng sắp hết hạn, để khách ở tiếp mà không phải lập lại hợp đồng mới và mất lịch sử thanh toán cũ.

Bằng chứng mã trong `QL_PhongTro/`: Controllers/HopDongController.cs (GiaHan); Services/HoaDonDichVuService.Monthly.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Gia hạn nhập kỳ hạn mới tính theo tháng và giá thuê mới, mặc định giữ nguyên giá cũ | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Ngày bắt đầu kỳ gia hạn là ngày liền sau ngày kết thúc hiện tại, không cho tạo khoảng trống hay chồng lấn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Giá thuê mới chỉ áp dụng từ kỳ hoá đơn đầu tiên nằm trong khoảng gia hạn | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Lịch sử gia hạn hiển thị trên hợp đồng gồm từng lần, người thực hiện và giá áp dụng | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

### S3-10 — Là Chủ nhà, tôi muốn theo dõi tiến độ chốt chỉ số của từng toà theo kỳ, để đến ngày phát hành hoá đơn không bị thiếu phòng nào.

Bằng chứng mã trong `QL_PhongTro/`: Services/TienDoChiSoService.cs; KyChiSoLockService.cs; Controllers/HomeController.cs.

| AC | Tiêu chí gốc | Đánh giá | Ghi chú |
| --- | --- | --- | --- |
| 1 | Màn hình theo kỳ hiển thị tổng số phòng đang thuê, số phòng đã chốt và số phòng còn thiếu của từng toà | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 2 | Bấm vào số phòng còn thiếu mở ra danh sách phòng cụ thể kèm tên người quản lý phụ trách | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 3 | Khoá kỳ sau khi phát hành hoá đơn, kỳ đã khoá không cho sửa chỉ số nữa | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |
| 4 | Đến ngày chốt mà còn phòng chưa nhập thì hiển thị cảnh báo trên trang chủ của chủ nhà | Đạt ở mức mã | Có triển khai tương ứng; xem phạm vi kiểm thử và giới hạn nghiệm thu. |

## Sprint 4 — tham chiếu AGENTS.md

| Story | Kết luận | Thiếu / phần đã có |
| --- | --- | --- |
| S4-01 Báo chuyển khoản | Chưa đạt | Chưa có form/API báo tiền và ảnh chứng từ private với link 15 phút, kiểm tra tiền và hủy khoản chờ. |
| S4-02 Xác nhận thu | Chưa đạt | Bảng/model tối thiểu và đọc tổng đã trả không phải luồng xác nhận nhiều lần, chống vượt nợ, hủy có lý do. |
| S4-03 Công nợ | Chưa đạt | Menu Công nợ dẫn trang module chưa triển khai; chưa có bộ lọc/tổng/CSV/bộ đối chiếu trả thiếu, quá hạn. |
| S4-04 Trả phòng/tất toán | Chưa đạt | Chưa có sổ cọc, thanh lý, hóa đơn cuối và chốt nghĩa vụ trong transaction. |
| S4-05 Phòng trống/tin mới | Một phần | Có trang chủ đếm phòng trống chưa đăng; chưa có luồng kết thúc hợp đồng → phòng trống + nhân bản tin nháp. |
| S4-06 Khách báo hỏng | Chưa đạt | BaoHong.cshtml chỉ có mảng dữ liệu minh họa, nút disabled; chưa có gửi báo hỏng/ảnh/mã BH/quyền hợp đồng. |
| S4-07 Xử lý báo hỏng | Chưa đạt | Chưa có backend chuyển trạng thái, ghi chi phí/bên chịu và chuyển phí sang hóa đơn. |
| S4-08 Nhắc hóa đơn | Một phần | Có HOA_DON_MOI, danh sách thông báo, đánh dấu đọc và chuông đếm chưa đọc ở _TenantHeader.cshtml. Dispatcher không tạo nhắc trước hạn/quá hạn lúc 08:00; email THAT_BAI không được chọn gửi lại, chưa đủ retry ≤3 lần. |
| S4-09 Báo cáo | Một phần | Có tổng quan phòng/lấp đầy hiện tại. Báo cáo doanh thu/biểu đồ còn placeholder, chưa có lịch sử lấp đầy cuối tháng và đo 500 phòng×12 tháng. |
| S4-10 Demo nghiệm thu | Chưa đạt toàn bộ | Có seeder/demo Sprint 2. Chưa chứng minh bộ mẫu đúng 2 tòa/30 phòng/20 hợp đồng/3 kỳ, ca trả thiếu/quá hạn và demo xuyên suốt thu tiền→trả phòng. |

## Chức năng bổ sung ngoài backlog

Các mục sau không được yêu cầu rõ trong 30 story gửi. “Bổ sung” không mặc nhiên có nghĩa phải xóa; có thể là yêu cầu đã được chấp thuận ở lần làm trước.

- Tiện nghi phòng và ghi chú nội bộ riêng (checkbox tiện nghi, mô tả quản lý). Bằng chứng: Views/PhongTro/_CreateRoomForm.cshtml; ViewModels/TienNghiPhong.cs.
- Ghi chú phòng có ghim và tệp đính kèm PDF/ảnh đến 10MB. Bằng chứng: Controllers/PhongTroController.Notes.cs; Models/GhiChuPhong.cs. Nhật ký nghiệp vụ S1-10 có trong backlog, nhưng phần ghi chú tự do này là bổ sung.
- Tài liệu hợp đồng tải lên, điều khoản bổ sung và lưu tệp riêng. Bằng chứng: Controllers/HopDongController.cs; Services/ContractDocumentStore.cs; Views/HopDong/Create.cshtml. PDF hợp đồng tự sinh S3-03 là chức năng cần có; tải lên tài liệu là phần thêm.
- Admin đổi vai trò và xóa mềm tài khoản, gửi lại mật khẩu tạm. S1-03 yêu cầu tạo/khóa; các thao tác này mở rộng. Bằng chứng: Controllers/ManagedAccountsController.cs.
- Xuất danh sách yêu cầu thuê và bộ lọc bổ sung theo tháng/từ khóa. Bằng chứng: Views/YeuCau/_OwnerRequests.cshtml; wwwroot/js/owner-requests.js.
- Trang giới thiệu/Privacy, trang tổng quan và phong cách thương hiệu: hỗ trợ sản phẩm, không phải thiếu sót hay chức năng phải bỏ.

Các phần nên ẩn hoặc ghi rõ “chưa triển khai” khi nghiệm thu:

- Lập kế hoạch bảo trì định kỳ, PCCC, lịch thợ, gọi điện/nhắn tin trong trang Báo hỏng: chỉ là UI mẫu, vượt yêu cầu báo hỏng cơ bản S4-06/07.
- Menu Công nợ/Báo cáo chưa có nghiệp vụ; thẻ thu tiền/biểu đồ placeholder và phần lịch sử bảo trì chưa kết nối. Có giao diện không được tính là đã hoàn thành.
- Menu Admin “Cấu hình hệ thống” đang trỏ tới Permissions; cần phân biệt với cấu hình nghiệp vụ thật.

Không coi JWT, xác nhận email, quyền backend, CSRF, transaction, backup/updater và bảo vệ dữ liệu là chức năng thừa chỉ vì backlog không tách story riêng.

## Khác biệt được quyết định hiện hành cho phép

- Xác nhận email trước đăng nhập thay vì đăng nhập ngay (S1-01).
- MVC trả 403 trực tiếp thay vì redirect (S1-04).
- Hóa đơn unique theo hợp đồng/tháng thay vì phòng/kỳ (S3-06).
- Quyền hồ sơ còn dành cho chủ nhà có hợp đồng chờ hiệu lực/đã kết thúc (S1-06).
- Quản lý được tạo/sửa/gỡ tin của tòa được phân công, khác ma trận đọc cũ.
Cần đồng bộ backlog khi nghiệm thu; không tự sửa những quyết định này theo Excel cũ.

## Điểm bảo mật và nghiệm thu cần rà tiếp

- Danh sách yêu cầu đưa SĐT rõ từ server cho chủ nhà/quản lý; phù hợp trường danh sách trong backlog nhưng không khớp quy tắc che SĐT trong AGENTS.md khi chưa có quan hệ hợp đồng. Bằng chứng: Controllers/YeuCauController.cs.
- Chưa đạt ý định S1-04 “quản lý không thấy giá thuê thỏa thuận riêng”: HopDongController.Index lấy hợp đồng tòa được phân công và kỳ thuê; Views/HopDong/Index.cshtml render k.GiaThue không có điều kiện che cho QUAN_LY. Trang chi tiết chỉ cho chủ nhà không ngăn lộ giá ở danh sách.
- Logout API có cơ chế thu hồi token; AccountController.Logout của MVC chỉ SignOut cookie. Không coi logout bằng nút web là bằng chứng JWT/refresh cũng bị thu hồi; cần phân biệt hai luồng khi nghiệm thu S1-02.
- Chưa nghiệm thu Chrome/Safari 360px, kéo thả/cảm ứng thật, SMTP/inbox thật, 3G, tải 30 người đồng thời.
- Chưa xác nhận sao lưu tự động 02:00/giữ 7 bản/phục hồi; backup trước updater không thay thế backup hằng ngày.
- Bộ test pass không thay thế review thành viên khác, hợp nhất nhánh chính và nghiệm thu toàn bộ AC.

## Kiểm thử mới ngày 12/10/2026

Build/test chạy với BaseOutputPath riêng `data/audit-20261012/runtime` theo cách được AGENTS.md cho phép khi web đang chạy; test dùng SQLite tạm. Không dừng hay ghi đè runtime đang phục vụ web. Không sửa test/project test hoặc DB thật.

**Build thành công; 650 test: 617 PASS, 33 FAIL, 0 SKIP, thời gian khoảng 6 phút.** TRX: `data/audit-20261012/results/audit.trx`. Build có cảnh báo ImageSharp chưa tìm thấy license. Không khẳng định hệ thống đã nghiệm thu chỉ từ tỷ lệ test pass.

Tệp gửi: 30 story, 128 tiêu chí được liệt kê riêng ở trên.

### Phân loại 33 ca thất bại

| Nhóm | Số ca |
| --- | ---: |
| Fixture/schema v24 | 13 |
| Assertion giao diện/menu/form | 16 |
| Fixture/controller cần rà | 1 |
| Kỳ vọng version cũ | 1 |
| Route/redirect cần rà | 1 |
| Schema thanh toán | 1 |

Các fixture nâng cấp xóa marker phiên bản mới nhưng còn cột ghi_chu_noi_bo, khiến updater v24 từ chối theo thiết kế bảo vệ schema. Đây chưa phải bằng chứng nâng cấp DB thật gây mất dữ liệu. Riêng kỳ vọng version 23 đã lỗi thời so với updater 25. Các lỗi DOM/tiêu đề/redirect cần rà test với giao diện hiện tại; không tự coi tất cả là lỗi giả. Badge yêu cầu thiếu đã được xác nhận trực tiếp từ mã.

| Test thất bại | Nhóm | Lỗi đầu tiên |
| --- | --- | --- |
| QL_PhongTro.Tests.MeterReadingListTests.UpgradeRecoversEarlierMarkerWithExactV18Protections | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.MenuAndEveryModuleRouteMatchMatrixForAllRoles | Assertion giao diện/menu/form | Assert.Equal() Failure: Values differ |
| QL_PhongTro.Tests.PermissionTests.MoiVaDaHenLich_DeuDuocTinhChuaXuLyVaDanhDauQua24Gio | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.S304NghiemThuTests.TinDaChoThue_VangKhoiTimTinVaTrangChu_TuBuocDuyetVaConVangSauKichHoat | Fixture/controller cần rà | System.NullReferenceException : Object reference not set to an instance of an object. |
| QL_PhongTro.Tests.LichHenMigrationTests.CSDLTrong_UpdateLenV10_TaoDuHaiBang | Kỳ vọng version cũ | Assert.Equal() Failure: Strings differ |
| QL_PhongTro.Tests.RoomServicesTests.InvoiceDateSchemaUpgradePreservesIssuedInvoicesAndRepeatsWithoutChanges | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.LichHenMigrationTests.CSLDaCoV9_UpdateLenV10_KhongDungLaiBangCu | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.RoomServicesTests.V7CopyUpgradePreservesIssuedInvoicesAndPrivatePrices | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.ChuNha_DangLaiVaGoTinCuaPhongMinh | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.PermissionTests.RegisterHasPasswordToggleAndConfirmationEmailIsEditable | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.ContractCreationTests.HttpFormRendersAndIgnoresForgedEndDate | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.MeterReadingListTests.ReconcileUi17AndDev18PreservesBusinessRows(uiDatabase: True) | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.RoomServicesTests.OwnerDashboardShowsDueMeterWarningAndLinksToTheCorrectBuildingAndPeriod | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.PermissionTests.BuildingRoomCrudShowsNotificationsAndRetainsBuildingSelection | Assertion giao diện/menu/form | Assert.Equal() Failure: Values differ |
| QL_PhongTro.Tests.MeterReadingListTests.UpgradeV16PreservesRowsAndIsIdempotent | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.MeterReadingListTests.UpgradeRecognizesExactCompletedStepButRejectsMissingProtections | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.QuanLy_ChiDangVaGoTinCuaToaDuocPhanCong | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.ContractCreationTests.RoommateHttpDemoCsrfAndOwnership | Route/redirect cần rà | System.Net.Http.HttpRequestException : Response status code does not indicate success: 302 (Found). |
| QL_PhongTro.Tests.MeterReadingListTests.UpgradeV18RecoversMissingMarkerButRejectsIncompatibleColumn | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.TinQuaHan_TuDongTamAnVaCanhBaoChuNha | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.RoomServicesTests.Version23AddsPaymentHistoryWithoutChangingExistingInvoiceData | Schema thanh toán | System.InvalidOperationException : Database schema is not ready: thanh_toan: missing id, hoa_don_id, ngay_thanh_toan, so_tien, trang_thai |
| QL_PhongTro.Tests.MeterReadingListTests.ReconcileUi17AndDev18PreservesBusinessRows(uiDatabase: False) | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.UpdateV13_ChoQuanLyQuyenDangTinNhungKhongMoLaiQuyenDaThuHoi | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.PermissionTests.AnonymousGuestViewsNhaTotBrandedHomePage | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.MeterReadingListTests.HttpMenuReadOnlyHtmlAndGetPreservesDatabase | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.PermissionTests.ViewingApprovalRequiresConsentAndCsrfThenOpensContractForm | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.PermissionTests.BuildingRoomMutationsRejectInvalidInputAndProtectReferencedRooms | Assertion giao diện/menu/form | Assert.Equal() Failure: Values differ |
| QL_PhongTro.Tests.ContractCreationTests.HistoryHttpFiltersInvalidEmptyAndOwnership | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.PermissionTests.OwnerDashboardAggregatesOnlyActiveOwnedBuildings | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |
| QL_PhongTro.Tests.ContractCreationTests.UpgradeAddsMissingColumnsWithoutReplacingLegacyContract | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.RoomServicesTests.MigrationPreservesServicesAndRoomsAndRefusesOverwrite | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.MeterReadingListTests.UpgradeV17PreservesMeterRowsDefaultFalseAndReruns | Fixture/schema v24 | System.InvalidOperationException : Cột ghi_chu_noi_bo đã tồn tại nhưng chưa có phiên bản v24; cần rà soát schema. |
| QL_PhongTro.Tests.RoomServicesTests.DepartureHttpWarningSnapshotsAndFutureUnissuedBill | Assertion giao diện/menu/form | Assert.Contains() Failure: Sub-string not found |

### Bằng chứng đạt nổi bật trong lượt mới

- 13/13 AuthTests PASS, gồm token/refresh/logout. Tuy nhiên test hiện tại chưa phủ năm lần sai cách xa nhau nên không bác bỏ lỗi cửa sổ 15 phút.
- PublishFiftyDraftsThroughHttpIsAtomicAndIdempotent PASS: 50 hóa đơn phát hành, 100 chỉ số khóa, 50 thông báo; ngưỡng thao tác <30 giây; toàn test khoảng 4,92 giây.
- MonthlyBulkConcurrentFiftyRoomsHaveExactlyFiftyInvoicesUnderThirtySeconds PASS; toàn test khoảng 3,86 giây.
- MonthlyBulkHttpFiftyConfirmationThroughResultUnderThirtySecondsAndSafeRetry PASS; đây là tạo 50 Nháp, không dùng riêng test này để chứng minh phát hành.

### Giới hạn theo story do ca HTTP thất bại

S1-04 (ma trận menu), S1-07/08 (fixture form tòa/phòng), S2-03/08 (chuỗi UI tin/duyệt), S3-01 (form), S3-02 (route người ở/lịch sử/chuyển đi), S3-04 (fixture HomeController), S3-05/10 (chuỗi menu/cảnh báo trang chủ) có test HTTP chưa qua. Các AC ghi “đạt ở mức mã” ở trên chỉ xác nhận tìm thấy logic, chưa phải nghiệm thu web đầy đủ.

Ưu tiên: xử lý cửa sổ đăng nhập sai, che giá hợp đồng với quản lý, phân trang 12 và badge yêu cầu; sửa/đối chiếu 33 test thất bại và xác định DB thực tế của web; hoàn thiện thu tiền/công nợ/cọc/trả phòng/báo hỏng/nhắc hạn trước nghiệm thu end-to-end.
