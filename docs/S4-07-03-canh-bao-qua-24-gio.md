# S4-07:03 — Cảnh báo báo hỏng Gấp quá 24 giờ

Ngày 15/10/2026. Nhánh `feature/S4-07/03-urgent-repair-overdue-alert`, kế thừa S4-07:01 và S4-07:02 tại commit nền `f2d6628`. Chức năng, kiểm thử tự động và nghiệm thu trực quan desktop/360px trong trình duyệt tích hợp đã hoàn thành; chưa nghiệm thu Safari. Chưa commit hoặc push phần S4-07:03.

## Quy tắc PO đã xác nhận

- Bắt đầu tính từ `bao_hong.ngay_tao`, tức thời điểm gửi báo hỏng lưu UTC.
- Chỉ cảnh báo khi mức độ `GAP`, trạng thái `MOI` và thời gian đã chờ **lớn hơn 24 giờ**. Đúng 24 giờ chưa cảnh báo.
- Dừng cảnh báo ngay khi báo hỏng chuyển sang `DANG_XU_LY`, `HOAN_THANH` hoặc `TU_CHOI`.
- Phản hồi nhưng chưa đổi trạng thái không kết thúc cảnh báo và không đặt lại giờ bắt đầu.
- Mỗi lần tải danh sách hoặc chi tiết đều tính lại bằng `ITimeProvider.UtcNow`; không tự cập nhật trang đang mở. Thời gian chờ hiển thị tổng giờ và phút tròn xuống, không làm tròn để quyết định quá hạn.

Ví dụ: báo Gấp/Mới chờ 26 giờ hiện **Gấp — Quá 24 giờ**, **Đã chờ xử lý: 26 giờ 0 phút**. Sau khi nhận xử lý, tải lại danh sách/chi tiết sẽ không còn cảnh báo.

## Triển khai

`Services/BaoHongCanhBaoService.cs` chứa điều kiện dùng chung. So sánh khoảng thời gian UTC chính xác tới tick; không đổi sang ngày Việt Nam để tính ngưỡng. Giá trị DateTime UTC từ SQLite có Kind Unspecified vẫn được hiểu theo quy ước UTC của dự án.

`QuanLyBaoHongService` kiểm tra tài khoản/quyền/phân công như trước rồi tính cảnh báo trên dữ liệu trong phạm vi. Danh sách dùng một lần đọc đồng hồ cho toàn bộ kết quả. Không thêm truy vấn theo từng dòng, giữ phân trang và thứ tự mới nhất trước. Chi tiết dùng cùng bộ tính. Không thay đổi điều kiện quyền ghi, CSRF, luồng phản hồi, chi phí hoặc tài chính.

`BaoHongChiTiet.CanhBaoQuaHan` và record `BaoHongCanhBao` là dữ liệu hiển thị, không ánh xạ EF. Partial `_CanhBaoQuaHan.cshtml` dùng chung cho danh sách/chi tiết Quản lý; nhãn, biểu tượng và màu viền/nền giúp nhận biết mà không chỉ dựa vào màu. CSS chỉ nằm trong stylesheet báo hỏng. Trang Chủ nhà/Khách không được bổ sung cảnh báo trong lát này.

Không đổi schema, migration, quyền seed hoặc dữ liệu thật. Cảnh báo chỉ được tính khi đọc, không tạo lịch sử/audit hay tăng phiên bản báo hỏng.

## Tiêu chí và kết quả kiểm thử

| Tiêu chí | Bằng chứng | Kết quả |
|---|---|---|
| Chốt mốc bắt đầu, trạng thái chưa xử lý và kết thúc | Người dùng chọn “Chốt quy tắc trên (đề xuất)” trong hội thoại | Đã chốt |
| Gấp dưới/đúng/vượt 24 giờ | `OverdueUsesStrictTwentyFourHourBoundary`: 24 giờ −1 tick, đúng 24 giờ, +1 tick; `FutureAndRecentReportsDoNotWarn` | PASS |
| Thường, đang xử lý, hoàn thành, từ chối không cảnh báo | `OrdinaryAndHandledReportsNeverWarn`, HTTP sáu tình huống | PASS |
| Nhãn nổi bật và thời gian chờ 26 giờ | `WaitingDurationKeepsTotalHoursAndMinutes`, `OverdueHttpRendersOnlyEligibleReportsAndRefreshesAfterTimeAndStatusChange` | PASS HTML/HTTP |
| Danh sách và chi tiết nhất quán | `OverdueListAndDetailRefreshFromClockWithoutWritingAndRespectAssignment`, partial dùng chung | PASS |
| Tải lại theo thời gian hiện tại | Service/HTTP thay đồng hồ, tải lại ca đúng 24 giờ sau 1 phút | PASS |
| Cập nhật cảnh báo sau đổi trạng thái | `OverdueWarningEndsAfterRealTransitionButReplyDoesNotResetWaiting` cho ba trạng thái; HTTP POST nhận xử lý | PASS |
| Chặn truy cập chéo và quyền ghi/CSRF | Service đổi phân công, tài khoản READ; HTTP quản lý ngoài phạm vi/khách403, thiếu CSRF400 | PASS |
| GET không ghi dữ liệu | Test số dòng audit/lịch sử/phiên bản và snapshot HTTP các bảng nghiệp vụ | PASS |
| Desktop và 360px không tràn ngang, nghiệm thu trực quan | Danh sách/chi tiết 1366×900: scrollWidth1351≤1366; 360×800: scrollWidth345≤360; nhãn/giờ chờ rõ, không bị cắt | PASS trong trình duyệt tích hợp |
| Nhận xử lý bằng UI 360px | Chọn Đang xử lý, nhập ghi chú, lưu; toast thành công, lịch sử đúng; danh sách và chi tiết đều còn0cảnh báo | PASS |
| Safari thực tế | Không có Safari trong danh sách trình duyệt kết nối | Chưa kiểm chứng |

Build ứng dụng/Razor/test đạt 0 lỗi. Bộ `FullyQualifiedName~BaoHong`: **114 PASS/0 FAIL/0 SKIP**, gồm 98 ca hồi quy và 16 ca mới. TRX: `data/s407-overdue/results/repair-final.trx` (ignored). Cảnh báo build hiện hữu: NU1900, ImageSharp license, CS8601 trong AuthController.

HTTP thực tế tại localhost:5291 trên bản sao riêng: sáu ca Gấp26h/Thường26h/Gấp23h/Hoàn thành/Đang xử lý/Từ chối đều đạt; list/detail đúng nhãn/thời gian, no-store, ngoài phạm vi403, snapshot báo hỏng/chi phí/lịch sử/audit không đổi sau GET. Kết quả tại `overdue-http-results.json` trong thư mục demo ignored.

Không chạy lại toàn bộ suite 871 ca trong lát này. Kết quả lịch sử trước lát này là 853 PASS/18 FAIL cũ, không dùng làm kết quả toàn dự án sau thay đổi.

Lượt đầu mở trình duyệt bị **automatic approval review** từ chối vì hết usage limit. Sau khi người dùng yêu cầu nghiệm thu trực quan, thử lại cùng công cụ đã được phép. Tạo bản demo mới `data/s407-overdue/demo-20261015-044703-a7e78c/`, giữ nguyên nguồn; đăng nhập Quản lý qua UI. Quan sát danh sách/chi tiết caGấp26giờ ở desktop1366×900 và360×800: nhãn, giờ chờ, viền/nền rõ, không tràn ngang. Các ca Thường26h/Gấp23h/Đang xử lý/Hoàn thành/Từ chối không cảnh báo. Nhận xử lý ca0005 trên UI360px lưu thành công, hiển thị lịch sử và loại cảnh báo khỏi hai trang.

Bằng chứng ignored trong bản demo mới: `desktop-detail.jpg`, `desktop-list.jpg`, `mobile-detail.jpg`, `mobile-list.jpg`, `mobile-after-processing.jpg`. Đã bỏ override viewport sau nghiệm thu. Không thay đổi mã ứng dụng trong lượt nghiệm thu này. Chỉ có trình duyệt tích hợp và MCP Apps được kết nối, không có Safari; kết quả360px là kiểm tra responsive, không phải chứng nhận Safari/iPhone thật.

## Demo riêng và lệnh kiểm thử

Không chạy initializer/updater trên DB demo nguồn. Dừng app dùng DB nguồn trước khi tạo bản sao; script chỉ cho phép nguồn giả dưới `data/s407/demo-*`, kiểm tra v31 và ghi vào thư mục mới dưới `data/s407-overdue/demo-*`. Không xóa/reset/ghi đè bản demo trước.

```powershell
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter 'FullyQualifiedName~BaoHong' --logger 'trx;LogFileName=repair-final.trx' --results-directory data/s407-overdue/results
python verification/New-S407OverdueDemo.py data/s407/demo-20261014-164333-49008c
.\verification\Start-S407OverdueDemo.ps1
# Chạy ở terminal khác ngay sau khi tạo/chạy demo:
python verification/Verify-S407OverdueHttp.py <thu-muc-demo-moi>
```

`data/s407-overdue/latest.txt` chứa đường dẫn bản demo mới nhất. URL localhost:5291; tài khoản/mật khẩu trong `access.json` ignored, không chép vào Git. Đăng nhập Quản lý bằng tài khoản demo có sẵn, vào `/QuanLyBaoHong`; sáu ca mới có mô tả “Dữ liệu giả S4-07:03”. Mở ca `urgent_26h` để xem cùng cảnh báo ở chi tiết. Chuyển sang Đang xử lý kèm ghi chú để nghiệm thu việc mất cảnh báo; thao tác chỉ ghi vào bản demo riêng.

Giờ demo tiếp tục trôi theo giờ thật: ca 26 giờ có thể thành 27 giờ và ca 23 giờ sẽ quá hạn sau hơn một giờ. Để chạy script đối chiếu sáu ca với đúng thời lượng dự kiến hoặc demo lại ca 26 giờ, tạo **bản demo mới** bằng lệnh trên; không sửa giờ báo hỏng trong DB cũ. Mốc chính xác 24 giờ dùng test đồng hồ giả, không đợi theo thời gian thật.

Runtime demo trỏ tới `QL_PhongTro/bin/Debug/net10.0/QL_PhongTro.dll` đã build. Không cập nhật launcher/runtime demo chính S4-07:02. Dừng demo riêng trước lần build tiếp theo.

## Bảo toàn dữ liệu và phạm vi Git

DB nguồn chỉ đọc có schema31, integrity=ok và không lỗi FK. SHA256 trước/sau và lúc tiếp tục kiểm tra đều là `36309eefe07d5c774e7d6016fcc63deca65ac46be93e55667a536fc271196697`. Backup v30 giữ hash `e873aba7f5fc0d3e0eddb1a0277926de2da20464403396dc7239047ddb512924`. Không đổi DB local/Sprint2; không tác động các server dự án khác ở D:\hh.

Thay đổi chỉ gồm service cảnh báo/tích hợp đọc của Quản lý, ViewModel hiển thị, hai view và partial cảnh báo, CSS báo hỏng, hai file test, ba script demo/HTTP và tài liệu/tiến độ. Không sửa file csproj test. `data/`, credential, runtime, ảnh và TRX đều ignored. Chưa stage/commit/push/merge; chưa tuyên bố hoàn tất DoD review/merge hoặc nghiệm thu thiết bị thật.
