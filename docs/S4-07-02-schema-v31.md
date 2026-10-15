# S4-07:2 — Giai đoạn 2/6: schema v31

Ngày 14/10/2026, nhánh feature/S4-07/02-repair-cost, nền4364280. Chỉ cấu trúc dữ liệu/EF, updater và test. Không có service/controller/ViewModel/Razor mới, không tự tính phí/phát hành, không chuyển giai đoạn3.

## Database được phép

- Nguồn data/s407/demo-20261014-164333-49008c/s407.sqlite: chỉ đọc, v30, SHA256 trước/sau e61a904283f9c47ed0856e54a872c5eead99bcc3f70064e0fa5071a46d2709e5.
- Backup data/s407-cost/stage1-20261014-01/source-v30.backup.sqlite: chỉ đọc, v30, SHA256 trước/sau e873aba7f5fc0d3e0eddb1a0277926de2da20464403396dc7239047ddb512924.
- Chỉ nâng data/s407-cost/stage1-20261014-01/s407-cost-v30.sqlite lên v31. Giữ tên file đã duyệt; tên v30 không phản ánh schema sau nâng. Dung lượng577536 byte; SHA25656146db16f1da2c62fa806112269977462d2f3c9c3c896539277e487efa9b08b.

Đã kiểm tra tiến trình và file handle trước khi nâng; nguồn/backup được giữ handle read-only chặn writer trong khi updater chạy. Updater tạo backup riêng *.before-update-06139858d88b42ea959cdce7ebd6eb48.bak và file *.update.lock theo cơ chế repository. Không chạm Sprint2, không seed/reset.

## Cấu trúc mới

`chi_phi_bao_hong`: id; bao_hong_id UNIQUE; hop_dong_id snapshot gốc; so_tien INTEGER dương; ben_chiu_phi CHU_NHA/KHACH_THUE; cho_thu_khach0/1; ky_du_kien nullable ngày đầu tháng; nguoi_tao_id/nguoi_cap_nhat_id; ngay_tao/ngay_cap_nhat UTC; phien_ban bắt đầu1. Bốn FK RESTRICT. Index(hop_dong_id,cho_thu_khach,ky_du_kien).

CHECK tiền typeof=integer/>0; bên chịu whitelist; Chủ nhà phải cho_thu_khach0/kỳNULL, Khách phải1; kỳ phải ISO ngày đầu tháng và không trước đầu tháng sau tháng ghi nhận theoUTC+7; cho phép kỳNULL nếu chưa xác định được. Không yêu cầu hợp đồng còn hiệu lực để lưu chi phí. Trigger kiểm tra HOAN_THANH, hợp đồng khớp báo hỏng, Quản lý hoạt động/xác nhận email/không bị xóa hoặc buộc đổi mật khẩu, phân công và quyền BAO_HONG WRITE/FULL. Không sửa nguồn báo hỏng/hợp đồng/người/thời điểm tạo; mỗi sửa tăng đúng1 phiên bản. Không xóa chi phí.

`lich_su_chi_phi_bao_hong`: id; chi_phi_id; người/thời điểm; loạiTAO/SUA; số tiền/bên chịu/kỳ trước-sau; phiên bản trước-sau; khoa_chong_trung32 ký tựhex. Hai FK RESTRICT. UNIQUE khóa gửi và UNIQUE(chi_phi_id,phien_ban_sau). Index(chi_phi_id,thoi_diem,id). CHECK tiền/ngày/loại/chuỗi phiên bản; trigger snapshot khớp chi phí hiện hành và phiên bản trước khớp lịch sử. Trigger cấm sửa/xóa. Giai đoạn3 vẫn phải lưu chi phí+lịch sử+audit chung transaction; schema không tự viết lịch sử/audit cho một UPDATE chi phí độc lập.

`chi_phi_bao_hong_trang_thai` là VIEW đọc, không có cờ phân bổ độc lập dễ lệch. cho_thu_khach là điều kiện thu khách, không phải bằng chứng khoản đã thu. VIEW phân biệt KHONG_THU_KHACH, CHO_DUA_VAO_HOA_DON, TRONG_HOA_DON_NHAP, DA_DUA_VAO_HOA_DON, HOA_DON_DA_HUY dựa trên các dòng/hóa đơn thực tế. Hủy không tự requeue hoặc tạo khoản thu.

## Bảo vệ dòng hóa đơn

Giữ cột bao_hong_id và bảng hóa đơn cũ, không rebuild để thêm FK. Trigger kiểm tra toàn vẹn liên kết: phải có chi phí Khách chịu, hợp đồng hóa đơn khớp hợp đồng gốc, dòng PHAT_SINH/số lượng1/đơn giá và thành tiền khớp nguồn, ghi chú theo quy ước PHAT_SINH hiện tại. Chỉ gắn vào NHAP.

UNIQUE(hoa_don_id,bao_hong_id) WHERE bao_hong_id IS NOT NULL. Trigger chặn một nguồn xuất hiện trên hai hóa đơn NHAP/DA_PHAT_HANH, kể cả khác kỳ. Kiểm tra thêm khi đổi hợp đồng/trạng thái hóa đơn. Không đặt unique toàn cục bao_hong_id: bản DA_HUY và bản thay thế cùng giữ snapshot, nhưng chỉ một bản hiệu lực.

Dòng đã liên kết không được chuyển nguồn/chuyển hóa đơn/xóa. Không sửa chi phí nếu đã có bất kỳ snapshot hóa đơn, kể cả bản hủy; không tự mở khóa. Khoản điều chỉnh thông thường bao_hong_idNULL giữ hành vi cũ. Nếu v30 đã có dòng bao_hong_id khácNULL, updater dừng để review, không tạo nguồn phí giả hoặc sửa dữ liệu cũ.

Giai đoạn5 cần thông báo lỗi rõ ràng khi form sửa nháp gặp dòng có nguồn phí, xử lý hủy/thay thế/retry trong transaction, kiểm tra kỳ phù hợp, điều kiện hợp đồng và fingerprint. Không triển khai những luồng đó ở giai đoạn2. Nếu module hóa đơn được cài muộn trên DB không có hóa đơn khi nâng v31, cần tích hợp cài guard tương ứng; validation sẽ từ chối thiếu guard. Demo đã duyệt có module hóa đơn và đầy đủ guard.

## Kết quả kiểm chứng

- Build Debug đạt0 lỗi; cảnh báo cũ NU1900/ImageSharp/CS8601.
- 22/22 test BaoHongChiPhiSchemaTests đạt, không skip. Fixture chỉ đọc backup đã duyệt rồi tạo bản sao tạm độc lập dưới data/s407-cost/schema-tests; không seed/reset, không ghi dữ liệu test vào database thử nghiệm được duyệt. Test này cần backup local ở đúng đường dẫn; không phải fixture tự cấp dữ liệu cho checkout CI mới.
- Test tiền âm/0/NULL/lẻ/sai kiểu/vượtInt64; bên chịu sai; báo hỏng Từ chối/chưa hoàn thành; quyền và phân công; hợp đồng gốc; Chủ nhà không thu khách; kỳ không hợp lệ; khóa/version; lịch sử/snapshot/unique/bất biến; rollback; nguồn/chi phí đồng thời; phân bổ hóa đơn đồng thời; NHAP/phát hành/hủy/thay thế; nâng và rerun.
- Lần test đầu:19/20 đạt, một test đọc hash khi connection SQLite còn mở bị Windows khóa. Sửa test đóng connection/pool trước đọc hash; lần cuối22/22 đạt. Không sửa DB nghiệp vụ vì lỗi đó.
- Nâng database thử nghiệm:47 bảng, hai bảng chi phí mới0 dòng. Toàn bộ dữ liệu/cấu trúc45 bảng cũ giữ nguyên, kể cả SQLite sequence. app_schema_version thêm đúng một dòng31; tổng user rows299→300 chỉ do dòng phiên bản.
- integrity=ok, FK=0. Báo hỏng4, lịch sử6, ảnh3, hóa đơn2, chi tiết5 giữ nguyên.
- Updater lần2: database already up to date. Hash database thử nghiệm trước/sau lần2 giống nhau. Công cụ kiểm chứng ban đầu so tuple SQLite với list JSON nên báo sai; đối chiếu chỉ đọc bằng JSON chuẩn hóa và SHA256 xác nhận không có thay đổi thật, không sửa DB.
- Bằng chứng ignored: data/s407-cost/stage2-results/stage2-schema.trx, baseline.json, after-verification.json, after.json và rerun-verification.json.

Không chạy full suite/HTTP/UI của S4-07:1 trong giai đoạn2. Không tự cập nhật runtime demo cũ, không dùng runtime v31 trên nguồn/backup v30. Chưa có hành vi API idempotent, xác định kỳ hay UI chi phí — để giai đoạn3–5 sau phê duyệt.

## File và lệnh

Thêm Data/BaoHongChiPhiSchema.cs, Models/ChiPhiBaoHong.cs, Models/LichSuChiPhiBaoHong.cs, Tests/QL_PhongTro.Tests/BaoHongChiPhiSchemaTests.cs. Sửa Data/DatabaseUpdates.cs và Data/AppDbContext.cs. Không sửa updater28–30, service/controller/ViewModel/Razor.

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/s407-cost/stage2-runtime
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore --filter FullyQualifiedName~BaoHongChiPhiSchemaTests --logger 'trx;LogFileName=stage2-schema.trx' --results-directory data/s407-cost/stage2-results
```

Chỉ dùng --update-database với DatabasePath bản thử nghiệm đã duyệt, không chạy lại trên nguồn/backup. Không commit/push/merge; dừng ở giai đoạn2.
