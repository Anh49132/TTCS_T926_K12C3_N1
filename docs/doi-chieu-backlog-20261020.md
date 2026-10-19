# Đối chiếu backlog 40 story — 20/10/2026

> Đây là hiện trạng trước sửa. Các điểm khóa đăng nhập, giá hợp đồng của Quản lý và phân trang đã được sửa; người dùng đã chốt gia hạn chia theo ngày. Xem [bàn giao sau sửa](sua-tieu-chi-backlog-20261020.md) và kết quả cuối trong `tien-do.md`. Các giới hạn nghiệm thu khác vẫn giữ nguyên.

## Kết luận và giới hạn

**Chưa đủ toàn bộ tiêu chí để nghiệm thu.** Tệp người dùng gửi có đủ S1-01…S4-10. Đánh giá này đối chiếu mã hiện tại, test và biên bản kiểm chứng đã có trong repository; không chỉ dựa vào trạng thái trong AGENTS.md hoặc cột “Chưa bắt đầu” của tệp.

Lượt này không sửa mã nghiệp vụ, không chạy updater, không thay DB và không chạy lại full test hay nghiệm thu trình duyệt. Các số test/thời gian bên dưới là bằng chứng từ các lượt trước, không phải kết quả chạy mới. Chưa xác minh schema của DB mà phiên web hiện tại sử dụng. “Có triển khai” không đồng nghĩa toàn bộ AC đã được nghiệm thu lại trên phiên bản hiện tại.

## Các điểm chưa đáp ứng được xác nhận trong mã

| Story / tiêu chí | Hiện trạng | Bằng chứng |
| --- | --- | --- |
| S1-02 AC2: sai 5 lần **trong 15 phút**, khóa 15 phút và báo thời gian còn lại | `FailedLoginCount` cộng dồn, không có kiểm tra cửa sổ thời gian; 5 lần sai cách xa nhau vẫn có thể bị khóa. Mật khẩu dưới 8 ký tự bị trả lỗi trước khi đếm. Lần sai thứ 5 trả thông báo chung dù đã đặt khóa, chưa báo thời gian còn lại ngay. `CheckLockoutAsync` còn có thể đặt lại thời hạn khóa khi bộ đếm vẫn ≥5. | `Services/CredentialValidationService.cs`, đặc biệt `ValidateAsync`, `CheckLockoutAsync`, `HandleFailedLogin`. Test `L2_5_FailedLogin5TimesIn15Min_LocksAccount` chỉ chứng minh chuỗi sai trong khoảng ngắn; không chứng minh xử lý các lần sai ngoài cửa sổ. |
| S1-04: Quản lý không thấy giá thuê thỏa thuận riêng (mục tiêu story) | Query danh sách hợp đồng cho phép Quản lý được phân công xem; view render `k.GiaThue` cho mọi dòng, không có điều kiện che giá theo vai trò. Đây là điểm chưa đáp ứng mục tiêu story, dù không nằm thành một bullet AC riêng. | `Controllers/HopDongController.cs`, action `Index`; `Views/HopDong/Index.cshtml`, dòng bảng giá thuê. |
| S2-04 AC3: 12 tin/trang | `TimTinViewModel.KichThuocTrang = 6`. Có các kiểu sắp xếp nhưng số tin mỗi trang khác yêu cầu. | `ViewModels/TimTinViewModel.cs:7`. |

## Khác yêu cầu gốc hoặc cần chốt lại đặc tả

| Story / AC | Khác biệt | Cách đánh giá |
| --- | --- | --- |
| S1-01 AC3–4 | Đăng ký đang chờ có thể tiếp tục bước xác nhận; người mới phải xác nhận mã email trước khi đăng nhập, không đăng nhập ngay sau gửi form. | Hành vi xác nhận email đã được ghi trong quyết định hiện hành; cập nhật backlog, không mặc định là lỗi cần bỏ xác nhận. |
| S1-04 AC4 | Trang MVC trái quyền trả trực tiếp HTTP 403, không redirect. | Đúng quyết định hiện hành trong AGENTS.md. |
| S1-06 AC3 | Chủ nhà có quan hệ hợp đồng chờ hiệu lực hoặc đã kết thúc cũng có thể xem hồ sơ rõ, rộng hơn câu “phòng đang thuê”. | `HoSoAccess.RelatedProfileIdsAsync` và AGENTS.md đã quy định phạm vi này. Cần đồng bộ câu chữ nghiệm thu. |
| S3-06 AC5 | Chống trùng theo hợp đồng/tháng thay vì cấm mọi hóa đơn thứ hai của cùng phòng/tháng. | Đúng quyết định hiện hành, cho phép hai khách nối tiếp cùng phòng. |
| S3-09 AC3 | Gia hạn giữa tháng có chính sách chia tiền theo đoạn ngày của từng kỳ hợp đồng; không đơn thuần đợi tháng sau mới lấy giá mới. | Biên bản `hoan-thien-sprint3-20261012.md` ghi rõ cần PO chốt nếu mong muốn khác. Không kết luận đạt nguyên văn AC này. |
| S4-10 AC4 và sprint goal | Docker Compose/staging đã được gỡ theo quyết định hiện hành. | Không đáp ứng nguyên văn backlog cũ, nhưng không yêu cầu tự khôi phục Docker. Cần thay AC bằng môi trường bàn giao đã thống nhất. |

## Tổng quan đủ 40 story

“Có” dưới đây là có triển khai và/hoặc bằng chứng kiểm thử trước đó; vẫn chịu giới hạn ở đầu báo cáo.

| Story | Kết quả đối chiếu |
| --- | --- |
| S1-01 | Có form/validation/BCrypt/vai trò khách; AC3–4 có khác biệt xác nhận email như trên. |
| S1-02 | Có email/SĐT, token, refresh và logout; **AC2 chưa đạt**. |
| S1-03 | Có cấp tài khoản, mật khẩu tạm, buộc đổi, khóa phiên, lọc và phân trang; chưa xác nhận email tới inbox thật. |
| S1-04 | Có quyền DB, menu và kiểm tra quyền; **chưa che giá thỏa thuận với Quản lý**; AC4 khác quyết định. |
| S1-05 | Có đổi/reset mật khẩu, thời hạn, dùng một lần, giới hạn và thu hồi phiên; SMTP thật chưa xác nhận. |
| S1-06 | Có hồ sơ, validation giấy tờ, ảnh private/resize và masking; phạm vi xem rõ khác bản gốc. |
| S1-07 | Có tòa/quản lý, chặn xóa khi còn phòng, số phòng và tìm kiếm. |
| S1-08 | Có dữ liệu phòng, 4 trạng thái, mã duy nhất, giá tối thiểu và tạo hàng loạt. |
| S1-09 | Có dịch vụ, 5 dịch vụ mặc định, lịch giá/snapshot và chặn xóa dịch vụ đã tham chiếu. |
| S1-10 | Có audit và bảo vệ chỉ thêm; audit thanh toán đã được bổ sung trong triển khai Sprint 4, không còn dùng kết luận thiếu của báo cáo 12/10. |
| S2-01 | Có dịch vụ riêng phòng, ưu tiên giá riêng, tổng phí và ngừng từ kỳ sau. |
| S2-02 | Có giới hạn ảnh, sắp xếp, thumbnail và xóa/retry; cần nghiệm thu lại thao tác kéo thả/xóa với tệp thật trên bản hiện tại. |
| S2-03 | Có điều kiện phòng trống, dữ liệu kế thừa, trạng thái/hạn và tác vụ hết hạn. |
| S2-04 | Có lọc/sắp xếp/gợi ý; **AC3 chưa đạt: 6 thay vì 12**; cần bằng chứng đo mới cho AC4 <2 giây/500 tin. |
| S2-05 | Có thông tin/ảnh/dịch vụ/ước tính/xem ẩn danh; chưa có nghiệm thu lại chi tiết tin trên Safari và thiết bị thật. |
| S2-06 | Có form/ngày Việt Nam/sức chứa/chống mở trùng/mã yêu cầu. |
| S2-07 | Có danh sách/lọc/quá 24h; badge menu đã có, đếm cả Mới và Đã hẹn lịch. |
| S2-08 | Có lịch/cảnh báo trùng/lý do/lịch sử/duyệt giữ chỗ và lập hợp đồng. |
| S2-09 | Có danh sách khách/lý do/hủy theo trạng thái/liên kết tin. |
| S2-10 | Có cấu hình điện nước/giá/hiệu lực kỳ sau; khoản theo người đã được khôi phục trong luồng hóa đơn theo tài liệu sau sửa. |
| S3-01 | Có lập từ yêu cầu duyệt/ngày tự tính/chồng lấn/mã/chỉ số. POST hiện nhận giá chốt và tiền cọc, kiểm tra cọc ≤3 tháng; kết luận cũ “không nhập được giá/cọc tùy ý” đã hết hiệu lực. |
| S3-02 | Có người đứng tên/ở ghép/sức chứa/ngày đi/lịch sử và tính phí theo người. |
| S3-03 | Có hợp đồng khách/PDF/quyền và cảnh báo; đã có kiểm tra PDF ở lượt Sprint 3. |
| S3-04 | Có đổi hợp đồng/phòng/tin cùng transaction và kiểm tra rollback. |
| S3-05 | Có chỉ số trước/chặn giảm/cảnh báo/lưu từng phòng/đếm thiếu/input số; trải nghiệm một tay trên điện thoại thật chưa xác nhận. |
| S3-06 | Có hóa đơn đầy đủ thành phần/giá ngày chốt/hạn/bỏ qua thiếu chỉ số; đã có test 50 hóa đơn <30 giây; khóa trùng theo hợp đồng/tháng theo quyết định. |
| S3-07 | Có dòng chi tiết/tổng/đã trả/còn nợ/quá hạn/lọc. |
| S3-08 | Có Nháp/sửa/phát hành/hủy-thay thế/audit; có kiểm thử tích hợp chuyên biệt trước đó. |
| S3-09 | Có gia hạn liên tiếp/lịch sử/giá mới; AC3 cần thống nhất cách áp dụng khi gia hạn giữa tháng. |
| S3-10 | Có tiến độ/phòng thiếu/khóa kỳ/cảnh báo dashboard. |
| S4-01 | Có báo trả/ảnh/private/link 15 phút/chờ/hủy; luồng xác nhận đã nối, lỗi thiếu nối ngày 16/10 đã được sửa. |
| S4-02 | Có nhiều lần thu/chặn vượt/cập nhật dư nợ/hủy có lý do và lưu lịch sử. |
| S4-03 | Có công nợ/lọc/tổng/CSV; fixture ≥5 trả thiếu và 2 quá hạn nhiều kỳ đã được bổ sung, có bằng chứng sau sửa. |
| S4-04 | Có trả phòng/chỉ số/hóa đơn cuối/tất toán/phạt/kết thúc/PDF; có bằng chứng trình duyệt trước đó. |
| S4-05 | Có phòng trống/nhân bản Nháp/đăng/dashboard; biên bản 16/10 ghi thao tác đăng 0,63 giây. |
| S4-06 | Có báo hỏng/giới hạn ảnh/mã/quyền/theo dõi; thiết bị thật chưa nghiệm thu. |
| S4-07 | Có trạng thái/ghi chú/chi phí/kỳ sau/cảnh báo/lọc, đã có nhóm test. |
| S4-08 | Có thông báo/email/08:00 VN/chống trùng/retry/log/chuông; thiếu nghiệm thu SMTP/inbox thật. |
| S4-09 | Có tài chính/lấp đầy/lọc/biểu đồ; biên bản 16/10 ghi 14/14 test gồm benchmark 500 phòng ×12 tháng <3 giây sau warm-up; chưa đo lại máy nghiệm thu. |
| S4-10 | Có bộ dữ liệu và smoke 4 vai trò; **chưa đủ bằng chứng cho toàn bộ AC**, xem dưới. |

## S4-10 và các bằng chứng nghiệm thu còn thiếu

1. **AC1:** Bộ 19/10 có 10 tòa, 480 phòng, 280 hợp đồng, 840 hóa đơn của 3 kỳ và các trạng thái thanh toán. Đủ loại tình huống và quy mô lớn hơn bộ 2 tòa/30 phòng/20 hợp đồng, nhưng khác số lượng nguyên văn. Cần chốt có chấp nhận bộ mở rộng thay bộ chuẩn hay không.
2. **AC2:** Có script tạo dữ liệu, nhưng `Seed-Acceptance20261019.py` yêu cầu DB trống đã khởi tạo và từ chối DB có tài khoản. Chưa chứng minh một lệnh tái tạo bộ chuẩn, không còn dữ liệu cũ, hoàn tất <2 phút. Không được hiểu AC này là cho phép xóa DB thật.
3. **AC3:** Smoke 4 vai trò/20 lượt mở trang trong biên bản 19/10 không thay thế ma trận kiểm thử tất cả module và tất cả truy cập trái quyền. Có nhiều test quyền, nhưng chưa tổng hợp kết quả mới thành ma trận nghiệm thu đầy đủ 4 vai trò × module × phạm vi sở hữu.
4. **AC4:** Staging/Docker không còn hiệu lực theo quyết định. Tài khoản mẫu đã có trong tệp cục bộ riêng; cần cập nhật tiêu chí và tài liệu bàn giao theo môi trường thực tế.
5. **AC5:** Có nhiều kịch bản tích hợp và demo từng phần. Chưa tìm thấy trong các biên bản đã đọc một kết quả mới ghi đầy đủ một lượt xuyên suốt đăng tin → yêu cầu → hợp đồng → chỉ số → hóa đơn → trả thiếu/xác nhận → công nợ → trả phòng/tất toán → đăng lại trên bản hiện hành.

Các giới hạn chung: chưa nghiệm thu Safari/iOS/điện thoại thật, giao nhận SMTP thật, và chưa chạy lại toàn bộ hồi quy sau các thay đổi giao diện ngày 19–20/10 trong lượt này. Biên bản 20/10 kiểm tra 14 trang Khách thuê và một trang Chủ nhà trên Chromium 1440/360px, không phải mọi trang/mọi trạng thái của bốn vai trò. Chưa đủ bằng chứng review thành viên khác/hợp nhất và toàn bộ Definition of Done.

## Tài liệu và thứ tự xử lý

- Sửa khóa đăng nhập và chặn hiển thị giá thỏa thuận cho Quản lý trước; sửa phân trang nếu yêu cầu 12 tin vẫn giữ nguyên.
- Chốt lại các khác biệt có quyết định, đặc biệt giá gia hạn giữa tháng và thay thế AC staging.
- Hoàn thiện bằng chứng S4-10; chạy lại hồi quy và nghiệm thu các thiết bị/email còn thiếu.
- Cập nhật AGENTS.md: trạng thái Sprint 3 “chưa làm” và các ghi chú hóa đơn tối thiểu không còn phản ánh mã hiện tại. Không dùng báo cáo lỗi 12/10 hoặc phần “trước sửa” 16/10 như danh sách lỗi hiện hữu.

Nguồn đối chiếu: tệp `Pasted text.txt` người dùng gửi; `AGENTS.md`; `docs/doi-chieu-backlog-20261012.md`; `docs/hoan-thien-sprint3-20261012.md`; `docs/hoan-thien-tieu-chi-S4-20261016.md`; `docs/acceptance-dataset-20261019.md`; `docs/kiem-tra-bootstrap-20261020.md`; controller/service/view/test được chỉ ra ở trên.
