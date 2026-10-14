# S4-04 task 1 — Ghi nhận trả phòng và hóa đơn kỳ cuối

Ngày triển khai: 14/10/2026.

## Phạm vi và quyết định PO

Người dùng đã xác nhận trong phiên làm việc:

- Tính cả ngày trả thực tế; ngày trả nằm trong kỳ hợp đồng, không sau hôm nay theo Việt Nam.
- Tiền phòng từ đầu tháng hoặc ngày bắt đầu thuê nếu muộn hơn, đến ngày trả. Mẫu số là số ngày thực tế của tháng.
- Điện nước dùng giá có hiệu lực tại ngày trả, ưu tiên cấu hình riêng/giá riêng của phòng theo dịch vụ đã gán.

Chỉ tính ba khoản được yêu cầu: tiền phòng, điện và nước theo công tơ. Phòng đang dùng điện/nước khoán đầu người được báo rõ chưa phù hợp với luồng này; không tự chuyển cách tính. Chưa tính phí cố định, cọc, hư hỏng, công nợ cũ, phạt, đóng hợp đồng, đưa phòng về trống hoặc xuất PDF. Các màn hình tất toán trong ảnh tham chiếu dành cho các bước tiếp theo.

## Cách sử dụng

1. Vào **Hợp đồng & người ở ghép → Ghi nhận trả phòng**, hoặc mở hợp đồng đang hiệu lực → **Xử lý trả phòng**.
2. Nhập ngày trả thực tế, chỉ số điện cuối và chỉ số nước cuối. Bên phải hiển thị dự tính do máy chủ tính lại.
3. Chọn **Tính hóa đơn kỳ cuối** để xem bảng từng dòng, công thức, số lượng, đơn giá, thành tiền và tổng.
4. Có thể quay lại **Sửa dữ liệu bàn giao** trước khi lưu. Chọn **Lưu hóa đơn kỳ cuối** để ghi hóa đơn `KY_CUOI` trạng thái `NHAP`.
5. Truy cập lại hợp đồng sẽ mở hóa đơn đã lưu. Không lập thêm một hóa đơn kỳ cuối chưa hủy cho hợp đồng đó.

Nhập sai chỉ số hoặc ngày được báo lỗi và giữ dữ liệu đã nhập. Ngày trả trước lần chốt gần nhất bị chặn; nếu chưa có lần chốt thì lấy chỉ số bàn giao của chính hợp đồng. Điện/nước thiếu dịch vụ được gán hoặc thiếu giá dương cũng bị chặn.

Nếu tháng trả đã có hóa đơn chưa hủy, hệ thống yêu cầu xử lý hóa đơn cũ theo luồng hiện có, không tạo thêm hóa đơn chồng tháng. Nếu đã có hóa đơn sau ngày trả cũng bị chặn. Không tự hủy hoặc sửa chứng từ cũ.

## Dữ liệu và an toàn

Không đổi schema, không thêm bảng, không chạy initializer/updater lên DB local. Dữ liệu bàn giao nằm trong snapshot hóa đơn hiện có:

| Dữ liệu | Nơi lưu |
| --- | --- |
| Hợp đồng | `hoa_don.hop_dong_id` |
| Ngày trả thực tế | `hoa_don.den_ngay`, `ngay_chot` |
| Ngày đầu tính tiền phòng | `hoa_don.tu_ngay` |
| Chỉ số điện/nước đầu và cuối | `chi_tiet_hoa_don.chi_so_dau`, `chi_so_cuoi` |
| Giá, lượng tiêu thụ, thành tiền | snapshot trong từng dòng chi tiết |
| Ngày chốt trước | ghi chú dòng dịch vụ, hiển thị cùng dữ liệu đầu vào |

Không ghi `hop_dong.ngay_tra_phong` hoặc đổi trạng thái hợp đồng/phòng ở task này: đó là tác động của bước tất toán sau. Không ghi thêm bản công tơ kỳ tháng vào module chốt chỉ số; dùng snapshot riêng trên hóa đơn kỳ cuối. Nội dung hóa đơn đã lưu được giữ nguyên khi giá/chỉ số nguồn thay đổi.

Chỉ chủ nhà sở hữu hợp đồng, còn hoạt động, có quyền ghi `HOP_DONG` và `TAI_CHINH` được truy cập. Mọi POST có CSRF; phản hồi không cache. Save tính lại từ DB dưới transaction SQLite ghi độc quyền (`BEGIN IMMEDIATE`), so dấu vân tay bao gồm giá, chỉ số/phiên bản, kỳ thuê và phiên bản phòng trước khi ghi. Gửi đồng thời chỉ một lần lưu thành công. EF ghi nhật ký trong cùng transaction; lỗi audit rollback toàn bộ.

Chặn sửa, phát hành hoặc sao chép thay thế kỳ cuối qua chức năng hóa đơn tháng để bảo toàn thông tin bàn giao và tránh vượt qua bước tất toán chưa triển khai. Hóa đơn tháng thông thường giữ hành vi cũ.

DB mặc định được kiểm tra chỉ đọc trong checkout này có `app_schema_version` tối đa 5, chưa có `hop_dong`, `hoa_don`, `chi_tiet_hoa_don`, `chi_so_dien_nuoc`. Hiện trạng này khác ghi chú tiến độ nói DB local đã ở v27. Không thay DB; trang trả phòng hiển thị thông báo khi thiếu module. Cần chọn DB đã thiết lập các module theo README để demo dữ liệu thực tế.

## Kiểm thử

```powershell
dotnet restore Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --source https://api.nuget.org/v3/index.json
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -o data/s404-tests -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Checkout|FullyQualifiedName~Monthly|FullyQualifiedName~InvoiceCollection|FullyQualifiedName~Collection|FullyQualifiedName~Draft'
```

Fixture tạo DB giả riêng trong thư mục tạm, dùng schema/initializer thật của dự án. Các ca bao phủ:

Lượt xác minh cuối: **27/27 ca S4-04 đạt**, build ứng dụng và Razor Views thành công. Lượt cuối dùng output `data/s404-final-tests` để tách khỏi các tiến trình đang dùng output khác.

- Nhập hợp lệ, lưu nháp, mở lại snapshot; không kết thúc hợp đồng/phòng.
- 11 ví dụ tiền phòng (tháng 28/29/30/31 ngày), tiền điện nước, làm tròn từng dòng. Công thức nhân giá với số ngày trước rồi chia; ca 500.025 × 1 / 30 = 16.667,5 phải ra 16.668 đ.
- Người vào giữa tháng; không tiêu thụ; dùng chỉ số bàn giao khi chưa chốt.
- Chỉ số thấp hơn, âm, quá ba chữ số thập phân, ngày trả tương lai/trước lần chốt.
- Giá hiệu lực tại ngày trả, giá riêng phòng, stale preview.
- Trùng cùng hợp đồng khác tháng; tháng đã có hóa đơn; hai request đồng thời.
- Audit lỗi rollback; chặn luồng sửa/phát hành/thay thế hóa đơn tháng đối với kỳ cuối.
- HTTP render form/preview/lưu/chi tiết, chủ nhà khác 403, khách thuê 403, thiếu CSRF 400.

Giao diện dùng layout/sidebar hiện có, thẻ trắng, điểm nhấn vàng, hai cột desktop và một cột mobile. Chưa nghiệm thu trực quan Chrome/Safari hoặc thiết bị 360px thực; chưa có review của thành viên khác/hợp nhất nhánh theo DoD.

Bộ mở rộng đã chạy 121 ca: 119 đạt, 2 lỗi. Hai ca `ResavedDraftRefreshesServicesWithoutDuplicatingThem` (mong Redirect, nhận View) và `MonthlyAllAmountsSnapshotSurvivesChanges` (thiếu chuỗi “Cố định theo phòng”) được chạy lại trên bản mã gốc `HEAD` độc lập trong thư mục ignored `data/s404-baseline-source`, đều tái hiện cùng lỗi. Không sửa hai lỗi ngoài phạm vi này. Build còn các cảnh báo ImageSharp thiếu license và CS8601 có sẵn trong AuthController. Môi trường không có Node/Playwright nên chưa chạy kiểm tra JavaScript bằng các công cụ đó.
