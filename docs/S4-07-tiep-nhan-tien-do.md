# Bàn giao S4-07:1 — Tiếp nhận và cập nhật tiến độ báo hỏng

## 1. Đã triển khai

Ngày 14/10/2026, nhánh `feature/S4-07/repair-request-progress`.

Tái sử dụng S4-06: danh sách theo phân công Quản lý, chi tiết/ảnh, đổi trạng thái kèm ghi chú, phản hồi, lịch sử bất biến, transaction/audit, retry và chống cập nhật đồng thời. Bổ sung dữ liệu thật cho Chủ nhà ở `/ChuNhaBaoHong`, `/ChuNhaBaoHong/Details/{id}`, `/ChuNhaBaoHong/Image/{id}?order=1..3`. Menu BAO_HONG của Chủ nhà chuyển tới danh sách này. Chi tiết của cả ba vai trò thêm họ tên người gửi; danh sách Quản lý/Chủ nhà thêm mô tả sự cố. Razor mã hóa nội dung khi hiển thị.

PO xác nhận trong yêu cầu ngày 14/10/2026: Chủ nhà chỉ xem toàn bộ nội dung/ảnh/trạng thái/lịch sử của báo hỏng thuộc **tòa hiện sở hữu**, kể cả hợp đồng kết thúc; không đổi trạng thái/gửi phản hồi. Backend kiểm tra tài khoản hoạt động, vai trò, quyền đọc BAO_HONG và sở hữu hiện tại ở mọi GET. Seed FULL không cấp quyền xử lý cho Chủ nhà. Đổi chủ sở hữu có hiệu lực ở lần truy cập tiếp theo. Không bổ sung email/SĐT/giấy tờ vào trang báo hỏng.

Giữ quy tắc S4-06: MOI→DANG_XU_LY/TU_CHOI; DANG_XU_LY→HOAN_THANH; trạng thái cuối không mở lại; mọi lần chuyển có ghi chú. Phản hồi Quản lý giữ nguyên hành vi S4-06. Khách chỉ xem báo hỏng do mình gửi.

## 2. Chưa nghiệm thu

Không còn yêu cầu chức năng S4-07:1 đang chờ triển khai. Chưa nghiệm thu Safari/thiết bị thật, review thành viên khác và phê duyệt của người dùng; chưa tuyên bố hoàn thành toàn bộ DoD. Chi phí bảo trì/bên chịu phí/đưa vào hóa đơn và các lát cắt S4-07 khác ngoài phạm vi.

## 3. File thêm/sửa

Thêm: `Services/ChuNhaBaoHongService.cs`, `Controllers/ChuNhaBaoHongController.cs` trong QL_PhongTro; `Tests/QL_PhongTro.Tests/BaoHongOwnerTests.cs`; `verification/S407Demo.py`; `verification/Start-S407Demo.ps1`; tài liệu này.

Sửa trong QL_PhongTro: `Program.cs`, `Controllers/ModulesController.cs`, `Services/BaoHongService.cs`, `Services/QuanLyBaoHongService.cs`, `ViewModels/BaoHongViewModels.cs`, `Views/BaoHong/Details.cshtml`, `Views/QuanLyBaoHong/Index.cshtml`, `wwwroot/css/tenant-repairs.css`. Sửa `tien-do.md` để ghi kết quả. CSS thu gọn tiêu đề tài khoản trên màn hình nhỏ chỉ áp dụng trong trang báo hỏng.

## 4. Database

**Không đổi schema, không thêm migration, không chạy updater.** Demo riêng tại `data/s407/demo-20261014-164333-49008c/s407.sqlite`, schema v30. SQLite backup từ nguồn Sprint 2 mở read-only; chỉ thêm dữ liệu giả vào bản sao: tòa, phòng, hợp đồng, quản lý ngoài phạm vi, báo hỏng. Ảnh phòng cũng được sao chép riêng. File/runtime/mật khẩu dưới data/ đã Git ignore.

HTTP test so SHA256 file database Sprint 2 trước/sau: **không đổi**. Integrity=ok, FK=0. Không sửa DB chính/Sprint2. Tên người gửi là tên tài khoản hiện tại; không thêm snapshot/schema. Lịch sử người xử lý vẫn dùng snapshot tên S4-06.

## 5. Build/test thực tế

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/s407/runtime --nologo
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~BaoHongTests --logger 'trx;LogFileName=s407.trx' --results-directory data/s407/test-results
```

Build đạt, 0 lỗi; test cuối **50/50 PASS**, 0 fail/skip: 40 ca S4-06 và 10 ca Chủ nhà mới. Bằng chứng `data/s407/test-results/s407.trx`. Lần đầu sandbox chặn kết nối testhost gây aborted; chạy lại ngoài sandbox đạt. Không chạy toàn bộ suite. HTTP thật đạt, bằng chứng `data/s407/demo-20261014-164333-49008c/http-verification.json`, script `verification/S407Demo.py`.

## 6. TC01–TC16

U: xUnit `s407.trx`, lớp partial BaoHongTests trong BaoHongTrackingTests.cs/BaoHongOwnerTests.cs. H: HTTP `http-verification.json`.

| TC | Kết quả | Bằng chứng thực chạy |
|---|---|---|
| TC01 | PASS | H: danh sách Quản lý có báo; U: ManagerUpdatesAndRepliesAppearOnSameTenantDetailAfterContractEnds |
| TC02 | PASS | H: detail/image/POST ngoài phân công 403; U: ScopeIsRecheckedAndReadOnlyOrRevokedPermissionCannotWrite |
| TC03 | PASS | H: Mới→Đang xử lý→Hoàn thành, Mới→Từ chối, HTTP302 |
| TC04 | PASS | H/U: ghi chú rỗng không lưu, EmptyFeedbackAndStatusNoteAreRejected |
| TC05 | PASS | H/U: ghi chú khoảng trắng/newline/tab không lưu |
| TC06 | PASS | H: Mới→Hoàn thành không lưu; U: InvalidTransitionsDoNotWriteHistoryOrChangeState, 6 ca |
| TC07 | PASS | H: SQL lịch sử đúng MOI/DANG_XU_LY/HOAN_THANH |
| TC08 | PASS | H: đúng manager ID/timestamp; U: OwnerSeesSenderPhotosAndOrderedProgressAfterContractEndsWithoutWriteControls kiểm tra UTC bằng clock |
| TC09 | PASS | H: đúng ghi chú Đã liên hệ thợ sửa chữa/Đã sửa xong vòi nước; U: trim/snapshot |
| TC10 | PASS | U: cùng timestamp sắp theo ID; H: SQL; trình duyệt Chủ nhà thấy đúng thứ tự |
| TC11 | PASS | H/U: Chủ nhà thấy trạng thái/lịch sử/ảnh sau hết hợp đồng; ngoài sở hữu/đổi chủ/thu hồi quyền bị chặn |
| TC12 | PASS | H/U: Khách gửi thấy cập nhật sau hết hợp đồng; khách khác 403 |
| TC13 | PASS | H: Chủ nhà/Khách/Quản lý ngoài phạm vi gọi POST trực tiếp 403; thiếu CSRF400 |
| TC14 | PASS | H/U: Hoàn thành/Từ chối không mở lại; không có lựa chọn tiếp theo |
| TC15 | PASS | U: AuditFailureRollsBackStatusVersionAndFeedback, 2 ca; MOI/phiên bản0/lịch sử rỗng |
| TC16 | PASS | U: ConcurrentTransitionsRejectStaleVersionAndConcurrentFeedbackKeepsAllMessages, 2 cập nhật đồng thời chỉ 1 thắng; H: stale409/retry không trùng |

## 7. Tài khoản/demo

URL `http://localhost:5271`. `data/s407/latest.txt` chọn bộ demo. Mật khẩu chung nằm trong access.json ignored, kế thừa bộ giả đã được người dùng yêu cầu; không hardcode trong mã/script.

| Vai trò | Email |
|---|---|
| Quản lý | manager.demo@demo.local |
| Chủ nhà | owner.demo@demo.local |
| Khách gửi | tenant1.demo@demo.local |
| Chủ nhà khác | owner.b.demo@demo.local |
| Quản lý ngoài phạm vi | manager.other.s407@demo.local |
| Khách khác | tenant3.demo@demo.local |

ID1 đã kiểm thử: Hoàn thành, có ảnh và 2 lần đổi trạng thái. **ID2 còn Mới**, có ảnh, giữ để người dùng nghiệm thu. ID3 Từ chối. Tạo báo hỏng Mới khác để nghiệm thu nhánh Từ chối.

## 8. Hạn chế

Cảnh báo build cũ: NU1900 (không lấy vulnerability data), license ImageSharp, CS8601 trong AuthController. Không có lỗi biên dịch mới. Trình duyệt xác minh Chủ nhà có tên/ảnh/lịch sử, không form ghi; Quản lý có ghi chú/hai lựa chọn của MOI. 360px: documentWidth Chủ nhà354, Quản lý345; ảnh tải được. Đã xem screenshot Chủ nhà; chưa nghiệm thu trực quan toàn bộ luồng/Safari/thiết bị thật. Ảnh HTTP test là PNG1×1, không đại diện chất lượng ảnh thật.

## 9. Git

Đúng nhánh feature, chưa stage/commit/push/merge. `git diff --check` đạt. `git diff --stat` mặc định chỉ tính file tracked, không tính file mới ở mục3. DB/runtime/account access không xuất hiện trong git status. Kiểm tra lại bằng `git status --short`/`git diff --stat` trước khi phê duyệt.

## 10. Chạy demo

```powershell
cd D:\DU_AN_KHO
.\verification\Start-S407Demo.ps1
```

1. Mở `/Account/Login` trên localhost5271, đăng nhập Quản lý.
2. Mở `/QuanLyBaoHong/Details/2`, chọn Đang xử lý, ghi chú `Đã liên hệ thợ sửa chữa`, Lưu.
3. Phiên khác: Chủ nhà `/ChuNhaBaoHong/Details/2`; Khách `/BaoHong/Details/2`. Kiểm tra trạng thái, ghi chú, người/thời gian và ảnh; không form ghi.
4. Quản lý tải lại ID2, chọn Hoàn thành kèm ghi chú; hai vai trò khác tải lại xem kết quả.
5. Chủ nhà khác/Quản lý ngoài phạm vi truy cập detail/image ID2 phải403.
6. Thử Từ chối trên báo hỏng Mới khác; không từ chối sau Đang xử lý, không mở lại trạng thái cuối.
7. Ctrl+C trong terminal server để dừng. Không chạy hai server cùng cổng. `run.bat` dùng DB mặc định khác, không dùng để mở bộ demo này.

Muốn chạy lại kiểm thử HTTP, tạo bộ mới trước:

```powershell
python verification/S407Demo.py prepare
.\verification\Start-S407Demo.ps1
# Terminal thứ hai:
$folder = (Get-Content data/s407/latest.txt -Raw).Trim()
python verification/S407Demo.py verify $folder
```

## UI-01 — Thông báo cập nhật thành công

14/10/2026: thông báo cũ đọc BaoHongSuccess trong một đoạn văn viền nhẹ ở đầu trang; toast chung chỉ đọc Success. Trang chi tiết nay chuyển thông báo BaoHongSuccess qua ViewData cho `_Toast` hiện có. Toast nền đậm/chữ trắng, vị trí cố định, role=status/aria-live; giữ đến khi người dùng bấm Đóng hoặc rời trang. Chỉ success trên trang báo hỏng bật persistent; toast các trang khác và lỗi vẫn tự ẩn sau3 giây. Không đổi controller/service/quyền/lịch sử/schema.

File giao diện sửa: `Views/BaoHong/Details.cshtml`, `Views/Shared/_Toast.cshtml`, `wwwroot/js/site.js` trong QL_PhongTro. Build riêng `data/s407-ui01/runtime` đạt0 lỗi; 50/50 test BaoHongTests đạt, bằng chứng `data/s407-ui01/test-results/ui01.trx`. Cảnh báo build cũ không đổi.

Kiểm tra trình duyệt thật chỉ ghi trên bản sao `data/s407-ui01/demo/ui01.sqlite`, localhost5272: lưu hợp lệ hiển thị đúng “Đã cập nhật trạng thái.”, toast còn sau3 giây; desktop1280px và mobile360px rõ/rộng trong viewport (documentWidth345). Nút đóng hoạt động; reload không lặp success. Ghi chú trắng không lưu, chỉ có error toast và không có success; error vẫn tự ẩn. Bản sao có đúng1 lần đổi trạng thái/1 lịch sử, integrity/FK đạt. SHA256 demo nguồn Sprint4 trước/sau không đổi. Bằng chứng và screenshot trong `data/s407-ui01/`.

Runtime demo S407 đang dùng trước đó chưa được ghi đè khi server chạy. Để nghiệm thu thay đổi Razor, dừng server cũ bằng Ctrl+C rồi:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/s407/runtime
.\verification\Start-S407Demo.ps1
```

Build này không sửa database. Server kiểm tra5272 đã dừng. Chưa commit/push/merge; Safari/thiết bị thật chưa kiểm tra.
