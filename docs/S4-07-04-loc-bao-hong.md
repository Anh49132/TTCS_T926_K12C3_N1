# S4-07:04 — Lọc danh sách báo hỏng

Ngày 15/10/2026. Nhánh `feature/S4-07/04-repair-request-filters` được tạo từ `a9c69a4`, kế thừa đầy đủ S4-07:01–03. Phạm vi dành cho Quản lý; không thay schema hoặc dữ liệu demo nguồn. Chưa commit/push.

## Hành vi

- Bộ lọc GET gồm Tòa nhà, Trạng thái và Mức độ khẩn; các điều kiện được kết hợp bằng AND trước khi phân trang.
- Danh sách tòa chỉ lấy từ `toa_nha.quan_ly_id` trùng tài khoản Quản lý hiện tại, kể cả tòa được giao chưa có báo hỏng. Không suy quyền từ giá trị gửi trên form.
- Trạng thái: Mới=`MOI`, Đang xử lý=`DANG_XU_LY`, Đã xong=`HOAN_THANH`, Từ chối=`TU_CHOI`. Mức độ: Thường=`THUONG`, Gấp=`GAP`.
- Bỏ trống một trường nghĩa là không lọc theo trường đó. Xóa bộ lọc dẫn về `/QuanLyBaoHong`, về trang đầu và bỏ cả ba điều kiện.
- Giữ thứ tự mới nhất trước, 20 kết quả/trang và kiểm tra có trang tiếp theo bằng dòng thứ21. Áp dụng form lọc mới về trang1.
- Không có kết quả khi đang lọc hiện “Không có báo hỏng phù hợp với bộ lọc đã chọn.”
- Cảnh báo Gấp/Mới/>24giờ vẫn dùng bộ tính S4-07:03 sau khi truy vấn kết quả lọc; không tính giờ mới hoặc đặt lại giờ báo hỏng.
- Điều kiện và trang hiện tại đi theo liên kết chi tiết, nút quay lại, phân trang và các form trạng thái/phản hồi/chi phí. Cả lỗi validation và redirect sau lưu đều giữ ngữ cảnh. Không dùng URL quay lại tùy ý do người dùng gửi.
- Quyền BAO_HONG và trạng thái tài khoản được kiểm tra như trước. Chọn/giả mạo tòa không được giao hoặc không tồn tại trả403; mã lọc sai hoặc tham số số không hợp lệ trả400. Nếu phân công thay đổi, yêu cầu tiếp theo kiểm tra lại quyền hiện tại.
- Khách/Chủ nhà không được mở danh sách Quản lý. View danh sách dùng chung của Chủ nhà không hiển thị bộ lọc này và giữ liên kết mặc định.

## Các thành phần thay đổi

`BaoHongBoLoc` là ViewModel chứa query, chuẩn hóa/whitelist giá trị và tạo route. `ToaNhaBaoHongLoc` chỉ chứa ID/tên tòa để hiển thị. `BaoHongChiTiet.ToaNhaId` giúp lọc trong truy vấn EF; không tạo cột hoặc ánh xạ bảng mới.

`QuanLyBaoHongService.DanhSachLocAsync` luôn bắt đầu bằng truy vấn giới hạn Quản lý. Phương thức danh sách cũ giữ lại để tương thích các nơi gọi trước. Controller kiểm tra binding/filter, truyền dữ liệu bộ lọc vào view và tạo route nội bộ có ID đầy đủ. Index, Details và các partial form chỉ bổ sung ngữ cảnh; không đổi nghiệp vụ trạng thái, tiền, nhật ký hoặc CSRF.

CSS bộ lọc dùng ba cột trên desktop và một cột ở màn hình≤600px, nằm trong stylesheet báo hỏng hiện có. Không thêm thư viện hoặc JavaScript.

## Kiểm thử

Các test mới trong `BaoHongFilterTests.cs` và `BaoHongFilterHttpTests.cs` dùng database giả tự tạo, không dùng DB local hoặc ghi vào nguồn demo:

| Tiêu chí | Kiểm chứng |
|---|---|
| Lọc từng trường và kết hợp cả ba | Service kiểm tra chính xác ID/số kết quả; HTTP kiểm tra kết quả kết hợp |
| Chỉ tòa được giao trong bộ chọn | Có nhiều tòa được giao, tòa trống và tòa khác Quản lý; không có option tòa ngoài quyền |
| Danh sách rỗng và xóa lọc | HTTP kiểm tra thông báo, href xóa lọc và trở lại danh sách mặc định |
| Giữ điều kiện khi quay lại | HTTP lần theo URL chi tiết/back; kiểm tra cả trang2 |
| Giữ khi validation/lưu | POST phản hồi lỗi và thành công đều giữ query; không đổi redirect ra website khác |
| Cảnh báo quá24giờ | Service/HTTP giữ nhãn trên bản Gấp/Mới26giờ sau lọc |
| Phân trang đúng sau lọc | 22kết quả phù hợp xen23kết quả không phù hợp; trang1/trang2 không trùng, không lấy dữ liệu ngoài lọc |
| Phân quyền và tham số giả mạo | Tòa ngoài quyền/không tồn tại403, thu hồi quyền/phân công, khách403, mã/số sai400 |
| GET không ghi dữ liệu | Số bản ghi lịch sử/audit không đổi; script HTTP đối chiếu snapshot |

Lượt đầu125ca:123PASS/2FAIL tại liên kết chi tiết404. Nguyên nhân kết hợp `asp-route-id` với `asp-all-route-data` làm mất ID trong URL được sinh. Đã gom ID và điều kiện vào một dictionary route duy nhất cho link/form. Lượt tiếp theo hai test parser không tìm được chữ tiếng Việt do chưa giải mã HTML entities trong nhãn link; sửa helper đọc HTML, giữ yêu cầu và các assertion nghiệp vụ. Bổ sung POST trạng thái/chi phí có ngữ cảnh và POST với query sai phải400.

**Kết quả test liên quan:125PASS/0FAIL/0SKIP**, gồm114ca hồi quy và11ca mới. Build C#/Razor đạt0lỗi, còn cảnh báo có sẵn NU1900/ImageSharp license/CS8601. TRX tại `data/s407-filters/results/filters-final.trx`, ignored.

**Chạy lại full suite ngày15/10/2026:898ca,880PASS/18FAIL/0SKIP**, không timeout hoặc hủy lượt chạy. Cả125ca báo hỏng trong lượt full đều PASS. So sánh tên từng ca lỗi trong TRX với baseline `data/s407-cost/audit-20261015/full-final.trx` (871ca,853PASS/18FAIL): danh sách18FAIL giống hệt, không có lỗi mới hoặc lỗi cũ biến mất. Lượt full dùng binary đã build khớp nguồn, không sửa mã trong khi chạy. Không sửa các module ngoài phạm vi để làm xanh toàn dự án.

Bằng chứng ignored: `data/s407-filters/results/full-final.trx`, `full-final.log`, `full-baseline-comparison.json`. 18FAIL gồm: LichHenMigrationTests(2), MeterReadingListTests(4), ContractCreationTests(1), PermissionTests(11). Danh sách chi tiết trong JSON và báo cáo baseline `docs/S4-07-02-ra-soat-20261015.md`.

HTTP thực tế tại5292 trên demo riêng:6truy vấn lọc đạt, đúng số kết quả, tòa ngoài phạm vi403, query sai400, cảnh báo vẫn có, link chi tiết/back giữ query, xóa lọc đúng và snapshot các bảng báo hỏng/chi phí/lịch sử/audit không đổi sau GET. Bằng chứng `filter-http-results.json` trong thư mục demo ignored.

## Nghiệm thu trực quan

Đã thao tác trực tiếp bằng trình duyệt tích hợp trên bản demo `data/s407-filters/demo-20261015-045946-1efb88/`:

- Desktop1366×900: ba ô lọc nằm cùng hàng; chọn Tòa A/Mới/Gấp trả đúng một thẻ báo hỏng có nhãn quá24giờ. scrollWidth1366≤viewport1366.
- Mobile360×800: ba ô xếp dọc, nút áp dụng/xóa rõ, cảnh báo và kết quả đọc được; scrollWidth345≤360.
- Mở chi tiết0005 rồi bấm quay lại: giữ giá trị6/MOI/GAP (ID6 là Tòa A demo), đúng một kết quả và cảnh báo.
- Chọn Tòa Trống/Mới/Gấp: hiện thông báo không có báo hỏng phù hợp,0thẻ; scrollWidth360≤360.
- Bấm Xóa bộ lọc: URL về `/QuanLyBaoHong`, cả ba lựa chọn rỗng, danh sách mặc định10thẻ; scrollWidth345≤360.

Ảnh ignored đã có từ lượt nghiệm thu trước: `desktop-filtered.jpg`, `mobile-filtered.jpg`, `mobile-empty.jpg`, `mobile-reset.jpg`. Đã reset viewport override sau nghiệm thu. Theo yêu cầu mới, lượt kiểm tra cuối không tạo/chụp thêm ảnh. Kiểm tra danh sách trình duyệt khả dụng chỉ có trình duyệt tích hợp và MCP Apps, không có Safari kết nối nên **chưa nghiệm thu Safari/thiết bị iPhone thật**; kích thước360px là kiểm tra responsive trong trình duyệt tích hợp. Cần Safari trên macOS/iOS để hoàn tất tiêu chí này; không coi giả lập kích thước hoặc đổi user-agent là Safari. Chưa có review thành viên khác/merge theo DoD; chưa thể xác nhận toàn bộ DoD hoặc full suite xanh.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter 'FullyQualifiedName~BaoHong' --logger 'trx;LogFileName=filters-final.trx' --results-directory data/s407-filters/results
# Sau khi build bản hiện tại, dừng demo trước khi build:
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=full-final.trx' --results-directory data/s407-filters/results
python verification/New-S407FilterDemo.py data/s407/demo-20261014-164333-49008c
.\verification\Start-S407FilterDemo.ps1
# Terminal khác, dùng đúng thư mục vừa tạo:
python verification/Verify-S407FilterHttp.py <thu-muc-demo>
```

## Demo và bảo toàn dữ liệu

`New-S407FilterDemo.py` chỉ nhận demo giả dưới `data/s407/demo-*`, mở nguồn read-only và sao lưu SQLite sang thư mục mới `data/s407-filters/demo-*`. Không nâng schema hoặc ghi đè file cũ. Thêm Tòa A/B/tòa trống/tòa ngoài phạm vi và báo hỏng giả đủ trường hợp trên bản sao; dữ liệu tài chính nguồn giữ nguyên. Mã BH vẫn theo bộ đếm tháng Việt Nam; kiểm tra integrity/FK trước bàn giao.

`Start-S407FilterDemo.ps1` đọc `data/s407-filters/latest.txt`, chạy localhost:5292. Tài khoản và mật khẩu nằm trong `access.json` ignored. Runtime là bản build tại `QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll`; dừng demo trước khi build lại.

Đăng nhập Quản lý demo, chọn **Tòa A — demo lọc**, **Mới**, **Gấp**, bấm **Áp dụng bộ lọc**. Chỉ một báo hỏng phù hợp có nhãn cảnh báo xuất hiện. Mở chi tiết và quay lại để kiểm tra ba lựa chọn vẫn giữ. Chọn tòa trống để xem thông báo không có kết quả; bấm **Xóa bộ lọc** trở lại danh sách mặc định.

Schema nguồn đã đối chiếu trực tiếp: v31; bảng/cột tòa/phòng/báo hỏng phù hợp truy vấn hiện tại. Hash nguồn trước/sau triển khai `36309eefe07d5c774e7d6016fcc63deca65ac46be93e55667a536fc271196697`. Backup v30 giữ hash `e873aba7f5fc0d3e0eddb1a0277926de2da20464403396dc7239047ddb512924`. Không sửa DB local, không chạy updater hoặc reset demo nguồn. Tất cả database/backup/runtime/credential/TRX/ảnh nghiệm thu ở `data/` ignored; không đưa vào Git. Không sửa project test hoặc nhánh01–03.
