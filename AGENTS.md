# AGENTS.md — Hệ thống quản lý cho thuê phòng trọ (NhàTốt)

> File dành cho AI agent. Đọc hết file này trước khi sửa mã. Nó gộp 3 nguồn: yêu cầu hệ thống (`01-yeu-cau-he-thong.md`), mô hình CSDL (`02-csdl.md` / `02-csdl.dbml`) và tiến độ (`tien-do.md`).
> **Mã nguồn và CSDL thực tế là nguồn sự thật cuối cùng.** File này chỉ là bản đồ. Khi nó mâu thuẫn với mã đang chạy, tin mã, rồi báo lại chỗ lệch.

---

## 0. Cách dùng nhanh

1. Đọc mục 1 (bối cảnh) và mục 3 (quy tắc cứng) trước.
2. Xác định task thuộc story nào ở mục 8, đọc tiêu chí chấp nhận (AC) tương ứng.
3. Xem bảng liên quan ở mục 6 và quy tắc nghiệp vụ ở mục 7.
4. Kiểm tra mã, cấu hình và schema SQLite thực tế trước khi viết code.
5. Chỉ làm đúng phạm vi task. Kết thúc theo checklist ở mục 11.

Thứ tự ưu tiên khi các nguồn mâu thuẫn:

1. Yêu cầu trực tiếp mới nhất của người dùng.
2. Mã nguồn và schema SQLite thực tế.
3. Mục "quyết định hiện hành" trong file này (mục 2).
4. Tiến độ (`tien-do.md`), ưu tiên mục có ngày mới hơn.
5. Yêu cầu Excel (`01`) và DBML (`02`). Hai file này là đặc tả tham chiếu, không phải bằng chứng hệ thống đã khớp.

---

## 1. Bối cảnh sản phẩm

- **Sản phẩm:** ứng dụng web quản lý phòng, hợp đồng, hóa đơn và công nợ cho chủ nhà/quản lý trọ (vài chục đến vài trăm phòng).
- **Vòng đời cần phủ:** đăng tin phòng trống → khách gửi yêu cầu → chủ nhà lập hợp đồng → chốt điện nước → phát hành hóa đơn → thu tiền (kể cả trả thiếu) → trả phòng, tất toán cọc → phòng về trống, tin đăng bật lại.
- **Nguyên tắc cốt lõi:** mọi con số gắn với một hợp đồng cụ thể, có lịch sử và tra lại được sau nhiều tháng.
- **Dự án thực tập:** 4 sprint × 1 tuần, 40 story, 170 point, 6 epic (EP-01…EP-06), 5 lập trình viên fullstack, không có tester riêng.
- **Một tổ chức chủ nhà duy nhất.** Không phải nền tảng đa tổ chức.

### Ngoài phạm vi (không tự làm)

Cổng thanh toán online / VietQR đối soát tự động; ký số hợp đồng; app mobile native; đẩy tin sang sàn khác; khai báo tạm trú; hóa đơn điện tử chuẩn thuế; IoT đọc đồng hồ / AI đọc ảnh; chấm điểm tín nhiệm, gợi ý giá thị trường.

### Giả định đã nêu (chờ PO xác nhận, không tự mở rộng)

1. Một phòng chỉ có một hợp đồng hiệu lực tại một thời điểm.
2. Đơn giá điện/nước phẳng theo đơn vị, không bậc thang EVN.
3. Kỳ hóa đơn = tháng dương lịch, chốt cùng ngày cho cả tòa.
4. Thanh toán do chủ nhà ghi nhận thủ công sau khi tự đối chiếu sao kê.
5. Hợp đồng dùng một mẫu thống nhất, xuất PDF để ký giấy.
6. Chỉ phục vụ một tổ chức chủ nhà.

---

## 2. Quyết định công nghệ hiện hành

Stack trong file Excel là nội dung lịch sử. **Dùng bảng này.**

| Tầng | Hiện hành |
| --- | --- |
| Backend | ASP.NET Core MVC, C#, .NET 10 |
| Giao diện | HTML5 + CSS3 trong Razor Views (`.cshtml`), JavaScript khi cần. **Không React** |
| CSDL | **SQLite hiện có**, giữ nguyên dữ liệu; không tạo CSDL mới |
| Truy cập dữ liệu | Entity Framework Core ánh xạ vào schema hiện có |
| Xác thực | Cookie cho MVC; API xác thực có JWT (access 30 phút, refresh 7 ngày); mật khẩu BCrypt |
| Email | Pickup file `.eml`/`.txt` khi chưa cấu hình SMTP; SMTP qua cấu hình `PasswordReset__*` |
| Lưu ảnh | Ngoài `wwwroot`, qua endpoint kiểm tra quyền (ví dụ `IdentityImagePath` mặc định `QL_PhongTro/App_Data/identity-images`). Tiến độ không ghi nhận MinIO; xác nhận trong mã trước khi giả định |
| Staging/Docker | **Đã gỡ.** Bỏ qua các ghi chú Docker Compose/staging cũ |

Không còn hiệu lực: Spring Boot, React, MinIO bắt buộc, Docker Compose, staging, "luôn redirect khi trái quyền", "không dùng JWT".

### Quyết định PO khác với Excel

- **QUAN_LY không truy cập module TAI_CHINH** (hóa đơn/thanh toán/công nợ). Ma trận còn lại theo mục 5.
- Khóa tài khoản và đổi/reset mật khẩu thu hồi cookie/JWT/refresh cũ.
- Quên mật khẩu hiện chỉ áp dụng cho KHACH_THUE đang hoạt động.

---

## 3. Quy tắc cứng

### 3.1 CSDL và schema

- **Không** tự đổi tên bảng/cột, không chạy migration làm đổi cấu trúc/dữ liệu cũ.
- **Không** dùng `EnsureDeleted` / `EnsureCreated` để nâng cấp dữ liệu.
- **Không** xóa dữ liệu, bảng hoặc migration hiện có. Không chép đè CSDL của đồng đội.
- Trước khi ánh xạ EF, kiểm tra bảng/cột/khóa/kiểu thực tế. Nếu khác DBML, **báo rõ**, không tự sửa schema.
- DBML không phải migration đã chạy. Không coi nó là bằng chứng SQLite đã khớp.
- Không triển khai toàn bộ 22 bảng hoặc toàn bộ backlog. Chỉ làm phần cần cho task.
- PR đổi schema phải kèm: bước nâng cấp có **phiên bản mới** (giữ nguyên bước cũ, thêm theo thứ tự), backup, kiểm tra integrity/FK, tài liệu, và xác minh bảo toàn dữ liệu trên **bản sao**. Thay đổi phá hủy cần quyết định riêng.
- Không chạy updater/initializer trên DB đang dùng khi chưa được yêu cầu rõ. Thử nghiệm luôn dùng DB giả mới hoặc bản sao.
- Dừng app trước khi build/sao chép/nâng cấp DB. Không chạy app và updater cùng lúc trên một file.
- DB local **không nằm trong Git** (`QL_PhongTro/Data/local-dev.sqlite` đã bỏ theo dõi). Không commit DB, backup, `.env.local`, credential, dữ liệu cá nhân hay thư mục `data/`.

### 3.2 Bảo mật

- Kiểm tra quyền **ở backend** cho mọi action/API. Ẩn nút trên giao diện không phải kiểm soát truy cập.
- Không lưu mật khẩu rõ. Dùng cơ chế băm của ASP.NET Core / BCrypt, giữ tương thích hash cũ.
- Bật CSRF cho mọi POST của form. Cookie auth bật các cờ bảo vệ phù hợp.
- Chặn sở hữu chéo: Chủ nhà chỉ thao tác tòa mình sở hữu (`toa_nha.chu_nha_id`); Quản lý chỉ thao tác tòa có `quan_ly_id` là mình. Truy cập dữ liệu người khác trả **403**.
- Số căn cước/điện thoại khách: che bớt (chỉ 4 số cuối) với mọi vai trò trừ chủ nhà của phòng đang thuê và ADMIN. Che ở server, không che bằng CSS.
- Ảnh giấy tờ, chứng từ, ảnh báo hỏng là **private**: phục vụ qua endpoint kiểm tra quyền, `no-store`. Chứng từ thanh toán dùng liên kết hết hạn sau 15 phút.
- Không ghi mật khẩu, hash, token, giấy tờ rõ, URL ký vào nhật ký hoặc log.
- Khóa đăng nhập: sai 5 lần trong 15 phút thì khóa 15 phút; thông báo lỗi không tiết lộ tài khoản có tồn tại.

### 3.3 Dữ liệu và nghiệp vụ

- Tiền VND là **số nguyên đồng**. Tính bằng `decimal` của C#, không dùng `double/float`.
- Làm tròn **từng dòng** tới đồng (AwayFromZero), rồi cộng.
- Ngày nghiệp vụ dùng `date`. Thời điểm thao tác lưu **UTC**, hiển thị `Asia/Ho_Chi_Minh` (UTC+7). "Hôm nay" là ngày Việt Nam, lấy qua `ITimeProvider`.
- Khoảng ngày `tu_ngay`–`den_ngay` **bao gồm cả hai đầu**; `den_ngay` NULL = chưa xác định. Hai khoảng chung ngày là chồng lấn. Kỳ tiếp theo bắt đầu ngày sau ngày kết thúc kỳ trước. (Riêng `lich_su_trang_thai_phong` dùng `[từ, đến)` theo thời điểm.)
- Giữ lịch sử tài chính, **không xóa dây chuyền**. Chứng từ ưu tiên `ON DELETE RESTRICT`.
- Đổi trạng thái nghiệp vụ + ghi lịch sử/nhật ký phải **cùng một transaction**; một bước lỗi thì rollback tất cả.
- SQLite không có ràng buộc EXCLUDE: kiểm tra chồng lấn ở service và/hoặc trigger. Unique có điều kiện phải khai báo riêng khi cột có thể NULL.
- Chống gửi trùng và ghi đồng thời: dùng transaction và khóa lạc quan (`phien_ban`). Người lưu sau nhận cảnh báo dữ liệu đã đổi và phải tải lại.
- Sửa nghiệp vụ mới bằng `ExecuteUpdate`/SQL hàng loạt **không tự sinh nhật ký**; phải tích hợp ghi nhật ký riêng.

### 3.4 Phạm vi và quy trình

- Giữ nguyên chức năng và quy tắc nghiệp vụ hiện có khi đổi công nghệ.
- Không tự thêm thư viện nếu task không cần.
- Giữ thay đổi sẵn có của file project test (`Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj`); không stage chung khi chưa được yêu cầu.
- Không đổi email/mật khẩu/số điện thoại ADMIN mẫu trong khối lệnh chạy nhanh của README trừ khi người dùng yêu cầu rõ. Không đặt lại mật khẩu tài khoản trong DB.
- Đừng viết lại hoặc "dọn" hàng loạt ngoài phạm vi task.

### 3.5 Giao diện và ngôn ngữ

- Toàn bộ giao diện **tiếng Việt**.
- Tiền: dấu chấm ngăn nghìn (ví dụ `2.000.000 đ`). Ngày giờ: `dd/MM/yyyy HH:mm`, giờ 24h.
- Responsive từ **360px**; khách thuê dùng được toàn bộ luồng trên điện thoại, không tràn ngang.
- Theme theo yêu cầu 07/10/2026: menu/nút `#30343B`, điểm nhấn `#BFA77A`, nền trang `#F5F5F4`, thẻ `#FFFFFF`, nền biểu tượng `#E9EAEC`, chữ `#25282D`; thương hiệu **NhàTốt – SỐNG ĐÚNG NƠI**. CSS theo vai trò (`homepage-dashboard-theme.css` cho chủ nhà/quản lý/khách/admin, `tenant-theme.css`, `listing-theme.css` cho `TinDang`) chỉ nạp đúng phạm vi.

---

## 4. Yêu cầu phi chức năng

| Nhóm | Yêu cầu |
| --- | --- |
| Hiệu năng | Danh sách phòng/hóa đơn < 2 giây với 500 bản ghi; phát hành hóa đơn hàng loạt 50 phòng < 30 giây; phát hành cho tòa 30 phòng < 15 phút kể từ khi nhập xong chỉ số |
| Quy mô | 10 tòa, 500 phòng, 500 hợp đồng hiệu lực, ~6.000 hóa đơn (12 tháng), 30 người dùng đồng thời |
| Bảo mật | BCrypt, khóa 5 lần/15 phút, kiểm tra quyền ở backend |
| Dữ liệu cá nhân | Căn cước/SĐT che bớt theo vai trò; chứng từ liên kết 15 phút |
| Sao lưu | DB sao lưu tự động hằng ngày 02:00, giữ 7 bản, có kịch bản phục hồi đã thử ít nhất một lần |
| Tính tiền | Có bộ ví dụ tính tay ≥ 10 trường hợp, chuyển thành test tự động. Đổi công thức phải chạy lại bộ này |

### Definition of Done của story

- Mọi AC được kiểm thử thủ công và ghi lại kết quả.
- Có review của thành viên khác, đã hợp nhất vào nhánh chính.
- Có test tự động cho quy tắc tính tiền và ràng buộc chỉ số điện nước.
- Quyền kiểm tra ở backend; giao diện đúng ở 360px (Chrome, Safari).
- Định dạng VND và múi giờ đúng.
- Không còn lỗi mức nghiêm trọng/cao đang mở.
- Đổi schema có script chạy được từ CSDL rỗng.

---

## 5. Vai trò và phân quyền

Bốn vai trò, mỗi tài khoản **một** vai trò:

| Mã DB | Vai trò | Ghi chú |
| --- | --- | --- |
| `KHACH_THUE` | Khách thuê | Đăng ký tự do (luôn nhận vai trò này; không lấy role từ form). Chủ yếu dùng điện thoại |
| `CHU_NHA` | Chủ nhà | Chịu trách nhiệm giá, hợp đồng, dòng tiền |
| `QUAN_LY` | Quản lý tòa nhà | Ghi chỉ số điện nước, xử lý hỏng hóc. Một quản lý phụ trách nhiều tòa |
| `ADMIN` | Quản trị hệ thống | Cấp/khóa tài khoản, xem nhật ký |

Ma trận theo module: `F` toàn quyền · `W` ghi trong phạm vi được giao · `R` chỉ xem · `–` không truy cập · `*` chỉ dữ liệu của chính mình.

| Module | Khách | Chủ nhà | Quản lý | Admin |
| --- | --- | --- | --- | --- |
| Tài khoản và phân quyền | R* | R | – | F |
| Tòa nhà, phòng, bảng giá | R | F | R | R |
| Tin đăng cho thuê | R | F | R (xem mục dưới) | R |
| Yêu cầu thuê, lịch xem phòng | W* | F | R | R |
| Hợp đồng, người ở ghép | R* | F | R | R |
| Chỉ số điện nước | R* | F | W | R |
| Hóa đơn, thanh toán, công nợ | R* | F | **– (quyết định PO)** | R |
| Báo hỏng, bảo trì | W* | F | W | R |
| Báo cáo doanh thu, lấp đầy | – | F | R | R |

- Quyền lưu trong DB (`app_role` / `app_module` / `role_permission`), seed tại `Data/permissions.seed.json` (4 vai trò, 9 module, 36 cặp). **Thiếu cặp quyền = từ chối.** Chạy lại seed không cấp lại quyền đã thu hồi (`NONE`).
- **Tin đăng:** chỉ `CHU_NHA` và `QUAN_LY` được tạo/sửa/gỡ tin. Quyền `TIN_DANG` mặc định của `QUAN_LY` đã nâng từ `READ` lên `WRITE` (updater v13; quyền đã thu hồi không bị mở lại). Quản lý chỉ thao tác tòa gán qua `toa_nha.quan_ly_id`. Khách, ADMIN và quản lý không được phân công bị chặn.
- MVC chưa đăng nhập → chuyển Login. MVC trái quyền → trả trực tiếp 403. API của cơ chế quyền → JSON 401/403. `OwnDataOnly` không thay cho kiểm tra sở hữu trong nghiệp vụ mới.
- Khách chỉ xem/sửa hồ sơ gắn với tài khoản của mình. Chủ nhà xem hồ sơ (không sửa/xóa) của người đứng tên/ở ghép thuộc hợp đồng tại tòa mình. Hợp đồng `CHO_HIEU_LUC`, `DANG_HIEU_LUC`, `DA_KET_THUC` cấp quyền xem; `NHAP`, `DA_HUY` và yêu cầu thuê thì không.

---

## 6. Mô hình dữ liệu (22 bảng nghiệp vụ, SQLite)

Quy ước: tên bảng/cột `snake_case` tiếng Việt không dấu. Tiền `bigint`. Khóa chính `id integer autoincrement`. Mã trạng thái là chuỗi IN HOA.

### 6.1 Bản đồ bảng

| # | Bảng | Vai trò | Khóa/ràng buộc đáng chú ý |
| --- | --- | --- | --- |
| 01 | `tai_khoan` | Tài khoản đăng nhập (custom user) | email, `so_dien_thoai` unique (email chuẩn hóa strip+lowercase; SĐT `^0[0-9]{9}$`); `vai_tro`; `dang_hoat_dong` |
| 02 | `khach_thue` | Hồ sơ người thuê / ở ghép | `tai_khoan_id` unique, **tùy chọn** (người ở ghép có thể chưa có tài khoản); giấy tờ 9 hoặc 12 chữ số; ảnh giấy tờ private |
| 03 | `toa_nha` | Tòa nhà | `chu_nha_id`, `quan_ly_id`; `ngay_chot_hang_thang` 1..31; không xóa tòa còn phòng (chuyển ngừng hoạt động) |
| 04 | `phong_tro` | Phòng | unique `(toa_nha_id, ma_phong)`; `gia_thue` ≥ 500.000; `phien_ban` |
| 05 | `anh_phong` | Ảnh phòng | ≤ 8 ảnh/phòng, `thu_tu` 1..8, unique `(phong_id, thu_tu)`; ảnh đầu = đại diện |
| 06 | `dich_vu` | Danh mục dịch vụ | `ma_dich_vu` unique; seed DIEN, NUOC, RAC, GUI_XE, INTERNET |
| 07 | `cau_hinh_dich_vu` | Giá + lịch sử giá dịch vụ | `phong_id` NULL = mặc định tòa; có giá trị = giá riêng phòng; khoảng ngày không chồng lấn |
| 08 | `tin_dang` | Tin cho thuê | unique có điều kiện `phong_id` khi `DANG_HIEN_THI`; `tin_goc_id` |
| 09 | `yeu_cau_thue` | Yêu cầu xem phòng/thuê ngay | `ma_yeu_cau` unique; unique có điều kiện khách/tin khi còn mở; `phien_ban` |
| 10 | `hop_dong` | Hợp đồng (thông tin chung) | `ma_hop_dong` unique; `yeu_cau_thue_id` unique; **không giữ giá/ngày** |
| 11 | `ky_hop_dong` | Kỳ gốc + từng lần gia hạn | unique `(hop_dong_id, so_thu_tu)` và `(hop_dong_id, ngay_bat_dau)`; chứa giá thuê và snapshot điều khoản |
| 12 | `nguoi_o_ghep` | Người ở ghép | unique `(hop_dong_id, khach_thue_id, ngay_vao)`; có kỳ bắt đầu/ngừng tính phí |
| 13 | `chi_so_dien_nuoc` | Chỉ số công tơ | unique `(hop_dong_id, dich_vu_id, tu_ngay, den_ngay)`; `da_khoa`; `phien_ban` |
| 14 | `hoa_don` | Hóa đơn | unique có điều kiện `(hop_dong_id, nam, thang)` khi `trang_thai <> 'DA_HUY'`; `ma_hoa_don` unique; `thay_the_hoa_don_id` unique |
| 15 | `chi_tiet_hoa_don` | Dòng hóa đơn (snapshot) | unique `(hoa_don_id, so_thu_tu)`; bất biến sau phát hành |
| 16 | `thanh_toan` | Báo trả + xác nhận thu | `khoa_chong_trung` unique; chỉ `DA_XAC_NHAN` mới giảm nợ |
| 17 | `giao_dich_coc` | Sổ cọc | `THU_COC` / `HOAN_COC` / `CAN_TRU`; `khoa_chong_trung` unique |
| 18 | `bao_hong` | Báo hỏng thiết bị | `ma_bao_hong` unique; 3 cột ảnh cố định; `phien_ban` |
| 19 | `thong_bao` | Thông báo in-app + trạng thái email | unique `(hoa_don_id, nguoi_nhan_id, loai_thong_bao)` |
| 20 | `nhat_ky_hoat_dong` | Nhật ký chung, **chỉ thêm** | tham chiếu đa hình `loai_doi_tuong` + `doi_tuong_id` (không có FK) |
| 21 | `thanh_ly_hop_dong` | Biên bản tất toán | `hop_dong_id` unique; bất biến sau `DA_CHOT` |
| 22 | `lich_su_trang_thai_phong` | Lịch sử trạng thái phòng (báo cáo lấp đầy) | unique `(phong_id, tu_thoi_diem)`; `den_thoi_diem` NULL = hiện tại |

**Bảng hạ tầng/mở rộng không thuộc 22 bảng** (có trong hệ thống thực tế theo tiến độ): `app_schema_version`, `app_role`, `app_module`, `role_permission`, `email_confirmation`, các bảng reset mật khẩu, `dich_vu_toa_nha`, `dich_vu_phong` (có `don_gia_rieng` nullable), `ngung_dich_vu_phong`, `hop_dong_dich_vu`. Luôn kiểm tra danh sách bảng thật.

### 6.2 Bộ mã trạng thái

| Đối tượng | Giá trị |
| --- | --- |
| Phòng `phong_tro.trang_thai` | `TRONG`, `DA_DAT_COC` (chỉ giữ chỗ, tiền cọc thực thu nằm ở `giao_dich_coc`), `DANG_THUE`, `NGUNG_CHO_THUE` |
| Tin `tin_dang.trang_thai` | `NHAP`, `DANG_HIEN_THI`, `TAM_AN`, `DA_CHO_THUE` |
| Yêu cầu `loai_yeu_cau` | `XEM_PHONG`, `THUE_NGAY` |
| Yêu cầu `trang_thai` | `MOI`, `DA_HEN_LICH`, `DA_DUYET`, `TU_CHOI`, `DA_HUY` |
| Hợp đồng | `NHAP`, `CHO_HIEU_LUC`, `DANG_HIEU_LUC`, `DA_KET_THUC`, `DA_HUY` |
| Cách tính dịch vụ | `THEO_CHI_SO`, `THEO_NGUOI`, `CO_DINH` |
| Hóa đơn | loại `DINH_KY`, `KY_CUOI`; trạng thái `NHAP`, `DA_PHAT_HANH`, `DA_HUY` |
| Dòng hóa đơn `loai_khoan` | `TIEN_PHONG`, `DICH_VU`, `PHAT_SINH`, `GIAM_TRU` |
| Thanh toán | hình thức `TIEN_MAT`, `CHUYEN_KHOAN`; trạng thái `CHO_XAC_NHAN`, `DA_XAC_NHAN`, `TU_CHOI`, `DA_HUY` |
| Giao dịch cọc | loại `THU_COC`, `HOAN_COC`, `CAN_TRU`; trạng thái `DA_XAC_NHAN`, `DA_HUY` |
| Báo hỏng | mức `THUONG`, `GAP`; trạng thái `MOI` → `DANG_XU_LY` → `HOAN_THANH`, hoặc `TU_CHOI` trước hoàn thành; bên chịu phí `CHU_NHA`, `KHACH_THUE` |
| Thông báo | loại `HOA_DON_MOI`, `TRUOC_HAN_2_NGAY`, `QUA_HAN`; email `CHO_GUI`, `DANG_GUI`, `DA_GUI`, `THAT_BAI`, `DA_HUY` |
| Thanh lý | `NHAP`, `DA_CHOT` |

### 6.3 Định dạng mã sinh tự động

| Đối tượng | Dạng |
| --- | --- |
| Yêu cầu thuê | `YC-yyyyMM-xxxx` |
| Hợp đồng | `HD-yyyy-xxxx` |
| Báo hỏng | `BH-yyyyMM-xxxx` |
| Mã phòng khi tạo hàng loạt | tầng + 2 chữ số (giới hạn 1..99 còn chờ PO) |

---

## 7. Quy tắc nghiệp vụ cần nhớ

### 7.1 Giá và dịch vụ

- Giá riêng của phòng (`don_gia_rieng` hoặc `cau_hinh_dich_vu` có `phong_id`) **thắng** giá mặc định tòa. Cấu hình riêng `dang_ap_dung = false` nghĩa là ngừng dịch vụ, **không** rơi về giá mặc định.
- Thay giá = tạo phiên bản mới, đóng `den_ngay` cũ vào ngày trước `tu_ngay` mới. Trùng ngày bị từ chối.
- Đổi cách tính hoặc ngừng dịch vụ chỉ áp dụng **từ kỳ hóa đơn kế tiếp**. Đang ngừng trong tháng vẫn tính hết tháng đó; bắt đầu loại từ ngày đầu tháng sau. Áp dụng lại không xóa lịch sử ngừng.
- Hóa đơn chọn cấu hình theo **ngày chốt**, rồi snapshot tên/cách tính/giá/số lượng/chỉ số. Không tự sửa giá lịch sử đã dùng. Hóa đơn đã phát hành giữ nguyên đơn giá cũ.
- Dịch vụ phải được gán cho phòng thì mới lấy giá để hiển thị trên tin hoặc lập hóa đơn (phòng không chọn gửi xe thì không bị cộng phí gửi xe).
- Không xóa dịch vụ đã được cấu hình/hợp đồng/hóa đơn tham chiếu; chỉ ngừng áp dụng.
- Điện/nước đang áp dụng phải có đơn giá > 0. Đơn giá không âm. Cấu hình thiếu giá hoặc giá 0 không được lưu.
- Dịch vụ mặc định cấp tòa chỉ được gán khi **tạo phòng mới**; phòng có trước không tự nhận (giả định chờ PO).

### 7.2 Hợp đồng

- Hợp đồng không lưu ngày/giá; nằm ở `ky_hop_dong`. Một người đứng tên, không lặp trong `nguoi_o_ghep`.
- Ngày kết thúc tự tính = ngày bắt đầu + số tháng − 1 ngày; không cho sửa tay.
- Cọc thỏa thuận mặc định 1 tháng giá thuê, nằm trong 0..3 tháng giá kỳ đầu. Đây **không** phải tiền thực nhận.
- Cấm khoảng sử dụng phòng chồng lấn giữa các hợp đồng đã chốt; hợp đồng `DA_HUY` không chiếm phòng.
- Gia hạn nối tiếp ngày sau kỳ trước; không đứt quãng, không chồng lấn. Giá mới áp dụng từ kỳ hóa đơn đầu tiên nằm trong khoảng gia hạn.
- Tổng người ở (đứng tên + ở ghép) không vượt `so_nguoi_toi_da` tại mọi mốc ngày.
- `CHO_HIEU_LUC` giữ chỗ; kích hoạt đổi phòng/tin trong cùng transaction.
- Sau `DA_KET_THUC` không tạo hóa đơn mới nhưng vẫn nhận tiền trả công nợ cũ. Phải lập đủ hóa đơn cuối trước khi đóng.

### 7.3 Chỉ số điện nước

- Chỉ số mới ≥ chỉ số kỳ trước; chỉ số đầu khớp cuối kỳ trước hoặc bàn giao. Khoảng liên tiếp nối ngày.
- Chỉ áp dụng cho dịch vụ `THEO_CHI_SO` đang áp dụng cho phòng.
- Tiêu thụ chênh > 200% so với trung bình 3 kỳ gần nhất thì cảnh báo và yêu cầu xác nhận (cột `da_xac_nhan_bat_thuong`). Công thức và xử lý thiếu lịch sử còn chờ PO chốt.
- Khóa bản chỉ số đã dùng khi phát hành hóa đơn; kỳ đã khóa không sửa. Reset đồng hồ/điều chỉnh bản khóa chưa được hỗ trợ.

### 7.4 Hóa đơn

- **Một hóa đơn chưa hủy / hợp đồng / tháng.** Cho phép hai khách nối tiếp cùng phòng/tháng (khác câu "phòng/kỳ" của Excel). Chạy lại phát hành không tạo trùng.
- Chỉ bản `NHAP` sửa được. Phát hành khóa nội dung + dòng (trigger), ghi người/thời điểm, tạo thông báo. Đã phát hành chỉ **hủy kèm lý do rồi phát hành lại**; bản hủy vẫn lưu.
- Hạn thanh toán mặc định = ngày phát hành + 7 ngày (sửa được trước khi phát hành).
- `tong_tien` tính từ chi tiết, không nhập độc lập. `GIAM_TRU` lưu số **dương** và bị **trừ** khi cộng tổng. `PHAT_SINH`/`GIAM_TRU` bắt buộc có ghi chú.
- Tiền phòng chia ngày: `giá tháng × ngày ở / ngày trong tháng` bằng `decimal` rồi làm tròn một lần. Không nhân tỉ lệ đã làm tròn.
- Dư nợ = tổng − thanh toán `DA_XAC_NHAN` − giao dịch cọc `CAN_TRU` đã xác nhận.
- Trạng thái "đã trả / trả một phần / chờ xác nhận / quá hạn" tính riêng, không trộn vào trạng thái chứng từ.
- Không tự hủy bản đã có khoản thu hoặc cấn cọc đã xác nhận. Thay thế phải cùng hợp đồng/kỳ và bản nguồn đã hủy.

### 7.5 Thanh toán và cọc

- Một hóa đơn nhận nhiều lần thanh toán. Không thu vượt tổng. Kiểm tra lại dư nợ khi xác nhận và chống xử lý hai lần (`khoa_chong_trung`).
- Nội dung báo đã gửi không sửa. Khách chỉ hủy khoản của mình khi còn `CHO_XAC_NHAN`. Chủ nhà hủy khoản ghi nhầm phải có lý do. Hủy ≠ đã hoàn tiền thật.
- `CAN_TRU` bắt buộc có `hoa_don_id` cùng hợp đồng, `hinh_thuc` NULL. `THU_COC`/`HOAN_COC` không có `hoa_don_id` và phải có hình thức. Cấn trừ không ghi trùng vào `thanh_toan`.
- **Cọc giữ = Σ THU_COC − Σ HOAN_COC − Σ CAN_TRU** (đã xác nhận), không được âm. Cấn trừ đồng thời không vượt cọc giữ và dư nợ hóa đơn.

### 7.6 Trả phòng và tất toán

- Nhập ngày trả thực tế + chỉ số cuối cùng (không nhỏ hơn lần chốt gần nhất); hệ thống tạo hóa đơn `KY_CUOI` tính tiền phòng theo số ngày ở thực tế.
- Hư hỏng/phạt là dòng `PHAT_SINH` **trong hóa đơn cuối**, rồi dùng `CAN_TRU`. Không khấu trừ ở sổ khác. Cột hư hỏng/phạt trong `thanh_ly_hop_dong` chỉ để phân tích, **không cộng thêm lần hai** vào tổng nợ.
- Hoàn khách = `max(cọc trước chốt − tổng nợ, 0)`. Khách trả thêm = `max(tổng nợ − cọc trước chốt, 0)`. Cấn trừ = `min(cọc, tổng nợ)`, phân vào từng hóa đơn bằng `giao_dich_coc`.
- Chốt thanh lý, đóng hợp đồng, đưa phòng về `TRONG`, ghi lịch sử phòng và nhân bản tin nháp: **cùng một transaction**. Biên bản đã chốt bất biến.
- `DA_CHOT` là chốt nghĩa vụ, chưa đồng nghĩa đã thu/hoàn tiền xong.
- Tin nháp mới nhân bản từ tin cũ (`tin_goc_id`), cập nhật theo giá phòng hiện tại; chủ nhà tự duyệt đăng.

### 7.7 Tin đăng và yêu cầu

- Chỉ phòng `TRONG` mới tạo được tin mới. Một phòng tối đa một tin `DANG_HIEN_THI`. Hạn mặc định 30 ngày; tin quá hạn chuyển `TAM_AN` (tác vụ nền).
- Truy vấn công khai kiểm tra đủ: tin đang hiển thị, chưa hết hạn, phòng `TRONG`, tòa đang hoạt động. Giá/ảnh lấy từ phòng hiện tại.
- Yêu cầu: ngày mong muốn từ hôm nay đến +60 ngày (Việt Nam); số người 1..sức chứa (tính cả người gửi); một tài khoản chỉ một yêu cầu **đang mở** cho cùng tin (gửi lại bị từ chối, dẫn tới yêu cầu cũ).
- Khách tự hủy chỉ khi `MOI` hoặc `DA_HEN_LICH`; hủy rồi không khôi phục. `MOI` và `DA_HEN_LICH` đều tính là "chưa xử lý" (badge menu, đánh dấu quá 24 giờ).
- Từ chối bắt buộc chọn lý do từ danh sách (đã có khách thuê, không phù hợp số người, khách không liên lạc được, lý do khác kèm ghi chú). Cảnh báo nếu lịch hẹn cùng phòng trong 30 phút. Giờ hẹn nhập 24h `HH:mm`.
- Duyệt `THUE_NGAY` kiểm tra lại phòng còn trống và giữ chỗ trong cùng transaction.
- Lịch sử đổi trạng thái/lịch hẹn nằm trong `nhat_ky_hoat_dong`; khách chỉ thấy các trường nghiệp vụ đã lọc.

### 7.8 Nhật ký hoạt động

- Chỉ thêm. Cấm sửa/xóa qua ứng dụng, kể cả ADMIN. EF chặn và trigger `audit_no_update` / `audit_no_delete` chặn SQL.
- Ghi cùng transaction với thay đổi nghiệp vụ (savepoint nếu có transaction ngoài). Lỗi ghi nhật ký rollback nghiệp vụ.
- Dùng allowlist trường; chỉ ghi trường thay đổi; không ghi bí mật/giấy tờ/ghi chú tự do. Lưu snapshot tên và vai trò lúc thực hiện; nhật ký cũ thiếu snapshot giữ NULL, không suy đoán.
- Không audit hạ tầng đăng nhập/refresh/reset/đổi mật khẩu thường, quyền module, hồ sơ nhạy cảm hay CLI.

---

## 8. Backlog và trạng thái triển khai

Trạng thái theo tiến độ đến **06/10/2026**. "Xong" nghĩa là đã triển khai và có kiểm thử trong phạm vi tài liệu tiến độ, không phải đã nghiệm thu trên thiết bị thật/SMTP thật.

### Sprint 1 — Nền tảng tài khoản và danh mục gốc (42 điểm)

| ID | Vai trò | Nội dung / AC chính | Trạng thái |
| --- | --- | --- | --- |
| S1-01 | Khách | Tự đăng ký (họ tên, SĐT 10 số bắt đầu 0, email, mật khẩu ≥ 8 ký tự có chữ + số); báo rõ trường trùng. Thực tế: đăng ký tạo tài khoản chờ xác nhận, mã 6 số qua email, hết hạn 15 phút | Xong |
| S1-02 | Khách | Đăng nhập, duy trì phiên (token 30 phút / refresh 7 ngày), khóa 5 lần sai/15 phút | Xong |
| S1-03 | Admin | Tạo/khóa tài khoản Chủ nhà, Quản lý; mật khẩu tạm gửi email, buộc đổi lần đầu; khóa hiệu lực ≤ 1 phút; lọc theo vai trò/trạng thái, 20 dòng/trang | Xong |
| S1-04 | Admin | Phân quyền theo vai trò lưu DB; menu theo quyền; truy cập trái quyền 403 | Xong (QUAN_LY không có TAI_CHINH) |
| S1-05 | Khách | Đổi mật khẩu; quên mật khẩu (link 30 phút, dùng một lần, 3 lần/giờ/email); đăng xuất mọi phiên sau reset | Xong |
| S1-06 | Khách | Hồ sơ cá nhân + ảnh giấy tờ (JPG/PNG ≤ 5MB, resize ≤ 1600px, căn cước 9/12 số, che 4 số cuối) | Xong |
| S1-07 | Chủ nhà | Khai báo tòa nhà, gán quản lý; không xóa tòa còn phòng | Xong |
| S1-08 | Chủ nhà | Khai báo phòng, 4 trạng thái, tạo nhanh hàng loạt, giá ≥ 500.000 | Xong |
| S1-09 | Chủ nhà | Dịch vụ + đơn giá + lịch sử hiệu lực. Hóa đơn chỉ ở mức tối thiểu: hợp đồng hiệu lực thuê **trọn tháng một giá phòng**; chưa có nháp/chỉnh sửa/hủy/thay thế, thanh toán, email, tiền phòng theo ngày | Dịch vụ xong; **hóa đơn tối thiểu** |
| S1-10 | Admin | Nhật ký thao tác `/NhatKy`, 50 dòng/trang, lọc ngày/người/loại, chỉ đọc | Xong |

### Sprint 2 — Tin đăng và yêu cầu thuê (42 điểm) — **đã rà soát 04/10/2026**

| ID | Vai trò | Nội dung / AC chính | Trạng thái |
| --- | --- | --- | --- |
| S2-01 | Chủ nhà | Gán dịch vụ mặc định theo tòa khi tạo phòng, thêm/bỏ riêng từng phòng, giá riêng ưu tiên, tổng cố định/tháng, ngừng từ kỳ sau | Xong (schema v6–v8) |
| S2-02 | Chủ nhà | ≤ 8 ảnh/phòng, ≤ 5MB, kéo thả sắp xếp, thumbnail 400px, xóa có xác nhận + xóa tệp (có retry) | Xong |
| S2-03 | Chủ nhà | Đăng tin từ phòng `TRONG`, 4 trạng thái, hạn 30 ngày, tự hết hạn | Xong |
| S2-04 | Khách | Tìm/lọc (quận, giá, diện tích, số người), sắp xếp 3 kiểu, 12 tin/trang, gợi ý nới giá; 500 tin < 2 giây | Xong |
| S2-05 | Khách | Chi tiết tin, bảng dịch vụ, ước tính chi phí tháng đầu, xem được khi chưa đăng nhập, 360px | Xong |
| S2-06 | Khách | Gửi yêu cầu (loại, ngày 0–60, số người ≤ sức chứa, chống mở trùng, mã `YC-yyyyMM-xxxx`) | Xong (module `rental_request_schema=1` cài riêng) |
| S2-07 | Chủ nhà | Danh sách yêu cầu: lọc trạng thái/tòa, mới nhất trước, nổi bật > 24 giờ, badge menu | Xong |
| S2-08 | Chủ nhà | Xác nhận/đổi lịch, từ chối có lý do, lịch sử, duyệt Thuê ngay → phòng `DA_DAT_COC` + mở nút lập hợp đồng | Xong (nút dẫn tới S3-01) |
| S2-09 | Khách | Danh sách yêu cầu của tôi, xem lý do từ chối, tự hủy, liên kết tin | Xong |
| S2-10 | Chủ nhà | Điện/nước theo chỉ số hoặc khoán đầu người, áp dụng từ kỳ sau, chặn thiếu giá/giá 0 | Xong |

### Sprint 3 — Hợp đồng, chốt điện nước, hóa đơn tháng (43 điểm) — **chưa làm**

| ID | Vai trò | Pt | Ưu tiên | AC chính |
| --- | --- | --- | --- | --- |
| S3-01 | Chủ nhà | 8 | Must | Lập hợp đồng từ yêu cầu đã duyệt; nhập cọc, giá chốt, ngày bắt đầu, kỳ hạn (tháng), ngày chốt hóa đơn; ngày kết thúc tự tính; chặn chồng lấn; cọc 0..3 tháng (mặc định 1); mã `HD-yyyy-xxxx`; phòng → `DANG_THUE` |
| S3-02 | Chủ nhà | 5 | Must | Người ở ghép (họ tên, SĐT, căn cước, ngày vào); tổng người ≤ sức chứa; ngày chuyển đi giảm khoán từ kỳ sau; lịch sử người ở |
| S3-03 | Khách | 3 | Must | Xem hợp đồng + tải PDF (kèm dịch vụ/đơn giá); chỉ người đứng tên/ở ghép, trái quyền 403; cảnh báo < 30 ngày hết hạn |
| S3-04 | Chủ nhà | 2 | Must | Hợp đồng hiệu lực → phòng `DANG_THUE` + tin `DA_CHO_THUE` cùng transaction; lỗi thì rollback; ghi nhật ký |
| S3-05 | Quản lý | 5 | Must | Nhập chỉ số cuối kỳ trên điện thoại: danh sách phòng theo tầng/mã, chỉ số kỳ trước sẵn, chặn nhỏ hơn, cảnh báo > 200%, lưu từng phòng, bố cục một tay 360px |
| S3-06 | Chủ nhà | 8 | Must | Phát hành hóa đơn cả tòa bằng một thao tác; tiền phòng + điện/nước + cố định + khoán đầu người; giá theo ngày chốt; hạn 7 ngày; bỏ qua phòng chưa chốt chỉ số; chạy lại không trùng; 50 phòng < 30 giây |
| S3-07 | Khách | 3 | Must | Xem hóa đơn bóc tách (chỉ số đầu/cuối, tiêu thụ, đơn giá, thành tiền), tổng, đã trả, còn lại, nhãn quá hạn, lọc theo kỳ/trạng thái |
| S3-08 | Chủ nhà | 3 | Must | Hóa đơn nháp: sửa chỉ số, thêm phát sinh/giảm trừ (ghi chú bắt buộc); phát hành mới gửi thông báo; đã phát hành chỉ hủy + phát hành lại; ghi nhật ký trước/sau |
| S3-09 | Chủ nhà | 3 | Should | Gia hạn hợp đồng: kỳ hạn mới, giá mới (mặc định giữ giá), nối tiếp ngày, lịch sử gia hạn |
| S3-10 | Chủ nhà | 3 | Should | Tiến độ chốt chỉ số theo tòa/kỳ, danh sách phòng thiếu kèm quản lý, khóa kỳ sau phát hành, cảnh báo đến ngày chốt |

### Sprint 4 — Thanh toán, công nợ, trả phòng, nghiệm thu (43 điểm) — xem tiến độ mới

> S4-06 task 1 triển khai 14/10/2026: [bàn giao báo hỏng](docs/S4-06-bao-hong.md). Schema v28 thêm bao_hong/bộ đếm. PO chốt DANG_HIEU_LUC + kỳ thuê hiện tại, người đứng tên/người ở ghép đang ở, chọn phòng khi nhiều hợp đồng; BH-yyyyMM-0001…9999 theo tháng Việt Nam toàn hệ thống, hết dải từ chối. Có gửi/list/detail và kiểm tra quyền, chưa ảnh/quản lý phản hồi/đổi trạng thái. DB thật không tự nâng; demo riêng cổng5248.

> Kế hoạch bàn giao S4-01/S4-02 ngày 13/10/2026: đọc [docs/ke-hoach-S4-01-S4-02.md](docs/ke-hoach-S4-01-S4-02.md) trước khi triển khai. Tách **khách báo thanh toán** khỏi **chủ nhà xác nhận thu**; báo chờ không cộng vào Tổng đã thu. Tài liệu ghi rõ hiện trạng, schema dự kiến, transaction/chống trùng, quyền, AC/test và các quyết định PO còn thiếu. Đây là kế hoạch, chưa phải chức năng đã hoàn thành; các trạng thái Sprint trong bảng cần đối chiếu mã/tiến độ mới.

> S4-02 task 1–2 / AC1–AC4 đã triển khai ngày 13/10/2026: [bàn giao ghi/hủy thu](docs/S4-02-ghi-thu.md). v26 thêm metadata `lan_thu_hoa_don`; v27 thêm sự kiện bất biến `huy_lan_thu_hoa_don`, chỉ đổi trạng thái khoản thu sang DA_HUY, giữ tiền/ngày/người ghi gốc. Summary loại khoản hủy khỏi tổng nhưng trả đầy đủ lịch sử. Người dùng đã chốt: chỉ chủ nhà sở hữu tòa có quyền tài chính được hủy, không cần là người ghi thu, không giới hạn thời gian; QUAN_LY/ADMIN không hủy. Chưa nối S4-01; các task trước chỉ nâng bản sao. Sau lỗi run.bat ngày 13/10/2026 đã sao lưu/nâng DB local lên v27, giữ nguyên 38 bảng/3.922 bản ghi cũ; run.bat --check-only đạt. 69 test liên quan/hồi quy và HTTP đạt; còn nghiệm thu UI trực quan/review theo DoD.

| ID | Vai trò | Pt | Ưu tiên | AC chính |
| --- | --- | --- | --- | --- |
| S4-01 | Khách | 3 | Must | Báo đã chuyển khoản: số tiền (> 0, ≤ còn phải trả), ngày, hình thức, ảnh chứng từ ≤ 5MB (xem qua link 15 phút); hóa đơn "Chờ xác nhận"; chỉ hủy khi chủ nhà chưa xác nhận |
| S4-02 | Chủ nhà | 5 | Must | Xác nhận thu nhiều lần; "Trả một phần" / "Đã thanh toán"; không thu vượt; hủy lần thu ghi nhầm phải có lý do, giữ bản ghi |
| S4-03 | Chủ nhà | 5 | Must | Công nợ theo phòng/tòa; lọc tòa, ngày quá hạn, ngưỡng tiền; dòng tổng; khớp 100% với bộ test ≥ 5 trả thiếu + 2 quá hạn nhiều kỳ; xuất CSV UTF-8 |
| S4-04 | Chủ nhà | 8 | Must | Trả phòng + tất toán cọc (xem 7.6); chấm dứt trước hạn chọn lý do + phạt cọc (một dòng khấu trừ); xong thì hợp đồng `DA_KET_THUC` |
| S4-05 | Chủ nhà | 3 | Must | Hợp đồng kết thúc → phòng `TRONG` cùng transaction; nhân bản tin nháp; một nút đăng < 1 phút; trang chủ hiện phòng vừa trống chưa có tin |
| S4-06 | Khách | 3 | Must | Báo hỏng: loại thiết bị, mô tả, mức Thường/Gấp, ≤ 3 ảnh × 5MB, mã `BH-yyyyMM-xxxx`; chỉ phòng đang thuê theo hợp đồng hiệu lực; theo dõi phản hồi |
| S4-07 | Quản lý | 3 | Must | Chuỗi trạng thái báo hỏng (mỗi lần đổi có ghi chú); chi phí + bên chịu (khách chịu thì đưa vào hóa đơn kỳ sau); Gấp > 24 giờ nổi bật; lọc tòa/trạng thái/mức |
| S4-08 | Khách | 5 | Must | Email + thông báo in-app khi phát hành, còn 2 ngày, quá hạn; tác vụ 08:00 VN, mỗi mốc một lần; lỗi gửi thử lại ≤ 3 lần; chuông + số chưa đọc |
| S4-09 | Chủ nhà | 5 | Should | Báo cáo doanh thu (phát hành/đã thu/còn thu/tỉ lệ) + tỉ lệ lấp đầy cuối tháng; lọc ≤ 12 tháng; biểu đồ cột 6 tháng; < 3 giây với 500 phòng × 12 tháng |
| S4-10 | Admin | 3 | Must | Dữ liệu mẫu (2 tòa, 30 phòng, 20 hợp đồng, 3 kỳ hóa đơn, đủ ca trả đủ/thiếu/quá hạn), nạp lại bằng một lệnh < 2 phút; rà phân quyền 4 vai trò; kịch bản demo end-to-end |

**Mục tiêu nghiệm thu cuối:** chạy trọn luồng đăng tin → yêu cầu → hợp đồng → chốt điện nước → hóa đơn → thanh toán (kể cả trả thiếu rồi trả nốt) → trả phòng, tất toán cọc → phòng trống + tin bật lại, hoàn toàn trên web. Khi velocity tụt dưới 38 điểm, cắt các story `Should`/`Could` trước (báo cáo, nhật ký, cấu hình nâng cao).

---

## 9. Hiện trạng kỹ thuật

### 9.1 Đã có trong mã

- Đăng ký (xác nhận email), đăng nhập, quản lý tài khoản, phân quyền, mật khẩu: `Controllers/AccountController*.cs`, `AuthController`, `ManagedAccountsController`, `PermissionsController`, `Authorization/*`; `Services/AuthService`, `TokenService`, `SessionVersionStore`, `PasswordResetService`, `PasswordEmailSender`.
- Hồ sơ khách: `HoSoController`, `Services/HoSoAccess`, `GiayToImageStore`. Tòa/phòng: `PhongTroController`.
- Dịch vụ + hóa đơn tối thiểu: `DichVuController`, `HoaDonDichVuController`, `DichVuService*`, `HoaDonDichVuService`, `DichVuPhongService` (quy tắc gán mặc định, giá riêng, ngừng theo kỳ), `Data/DichVuSchemaInitializer`.
- Tin đăng/yêu cầu: `TinDangController`, `Services/YeuCauThueService`, `TinDangExpirationService` (tác vụ nền mỗi phút).
- Nhật ký: `Data/AppDbContext.Audit.cs`, `AuditSchema`, `NhatKyController`.
- Cập nhật schema: `Data/DatabaseUpdates.cs` (chạy theo phiên bản).
- Trang công khai: `_PublicLayout.cshtml` cho `/`, `/TimTin`, `/TinDang`, `/Home/GioiThieu`.

### 9.2 Chưa có / còn thiếu

- Toàn bộ **Sprint 3 và 4**: CRUD hợp đồng (form lập hợp đồng thuộc S3-01), nhập chỉ số độc lập, hóa đơn đầy đủ (nháp/hủy/thay thế/kỳ lẻ/tiền phòng theo ngày), thanh toán, công nợ, sổ cọc, thanh lý, báo hỏng, thông báo chung + chuông, báo cáo.
- Hóa đơn tối thiểu chưa tích hợp chỉ số độc lập và người ở ghép (số người/chỉ số do chủ nhà nhập tay).
- Trang chủ công khai: hiện chỉ hero banner; chưa có form tìm nhanh, tin nổi bật, cam kết trên trang chủ.
- Tập tệp mồ côi sau crash chưa xử lý hoàn chỉnh; ảnh giấy tờ chưa mã hóa trên đĩa.
- Tất cả bước updater cũ chưa nằm trong một transaction chung; lỗi giữa chừng phải xem backup và trạng thái thực tế, rồi chạy lại (không tự phục hồi đè dữ liệu mới).

### 9.3 Phiên bản schema (đọc kỹ)

- Tiến độ ghi nhận updater tới **v13** (04/10/2026). Các ghi chú "nền v5" ở mục cũ của tiến độ **đã lỗi thời**.
- Mốc đã biết: v2 (`must_change_password`, unique email chuẩn hóa/SĐT) · v3 (nhật ký + trigger) · v4 (xác nhận email, xóa mềm) · v5 (`AccountReuseSchema`: unique email/SĐT có điều kiện `is_deleted = 0`) · v6 (dịch vụ tòa/phòng) · v7 (`don_gia_rieng`) · v8 (`ngung_dich_vu_phong`) · v13 (quyền `TIN_DANG` của `QUAN_LY` → `WRITE`). Module S2-06 (`rental_request_schema=1`) cài riêng.
- **Cách xác định phiên bản thật:** đọc `app_schema_version` và chạy `--check-database` trên bản sao. Không suy từ tài liệu.
- `--check-database` chỉ xác nhận bảng/cột cần cho model, bảng mật khẩu và dữ liệu phân quyền. **Không** chứng minh toàn bộ kiểu dữ liệu, FK/index hay nghiệp vụ đúng.

### 9.4 Cảnh báo build và test đã biết

- Thiếu license **ImageSharp**: cần license hợp lệ trước khi phát hành (Release).
- `NU1900`: không lấy được dữ liệu lỗ hổng NuGet (không chứng minh đã quét).
- `CS8601` trong `AuthController` (có sẵn từ trước).
- Lần chạy 05/10/2026: 262/263 ca đạt, 1 ca trang chủ thất bại (thiếu form tìm nhanh; test còn kỳ vọng tin nổi bật/cam kết). Chạy lại để xem hiện trạng, không giả định đã sửa.
- Chưa nghiệm thu: mạng 3G thật, thiết bị cảm ứng vật lý, SMTP thật (email local chỉ ghi file), tải đồng thời.
- Lỗi SQLite `database is locked` khi đăng nhập đồng thời đã được ghi nhận trước đây; ngoài phạm vi các task đã làm.
- Fixture test cũ có `EnsureDeleted`/`EnsureCreated`: **không chạy** vào DB mặc định. Fixture Auth/Permission mới tự tạo DB tạm.
- Công cụ `verification/database_updates.py` và fixture S109 cũ chưa được cập nhật cho v7+; rà soát trước khi dùng. `verification/S105` được nhắc trong tài liệu cũ nhưng không có trong checkout.

---

## 10. Chạy, cập nhật và kiểm thử (Windows / PowerShell)

Yêu cầu: .NET SDK 10.0.x, Python 3 (chỉ thư viện chuẩn cho script HTTP; script ảnh cần Pillow). Không cần SQL Server hay cài SQLite riêng.

### 10.1 Cổng và đăng nhập

- Web chạy tại **`http://localhost:5247`**, đăng nhập ở `/Account/Login`.
- `run.bat` ở gốc repo: kiểm tra SDK, restore, dùng `DatabasePath` hoặc DB mặc định, tự khởi tạo khi file chưa tồn tại, kiểm tra schema rồi chạy profile HTTP. DB đã tồn tại thì **không ghi đè và không tự chạy updater**. `run.bat --check-only` chỉ kiểm tra.
- DB mặc định: `QL_PhongTro/Data/local-dev.sqlite`; đổi bằng biến môi trường `DatabasePath` (chỉ có hiệu lực trong terminal đã đặt).
- Tài khoản/mật khẩu demo nằm trong `access.json` và `report.md` của bộ demo (đã bị ignore). **Không chép ra Git.**

### 10.2 Cập nhật DB sau khi pull

```powershell
# 1) Dừng app (Ctrl+C), sao lưu DB ra NGOÀI repository
dotnet restore QL_PhongTro
dotnet run --project QL_PhongTro -- --update-database    # tự backup *.before-update-<id>.bak, ghi app_schema_version
dotnet run --project QL_PhongTro -- --check-database     # chỉ đọc
dotnet run --project QL_PhongTro --launch-profile http
```

- Máy mới, chưa có DB: `--initialize-database` (từ chối ghi đè, không có ADMIN/mật khẩu cố định). Tạo ADMIN riêng bằng cấu hình `LocalAdmin__Email`, `LocalAdmin__Password`, `LocalAdmin__Phone` rồi `--create-local-admin` (chỉ Development, đúng `DatabasePath`; từ chối email/SĐT trùng).
- Module yêu cầu thuê cài riêng: `--initialize-rental-requests` sau khi schema nền sẵn sàng. Web không tự cài module.
- Updater: chạy lại phiên bản đã xong không đổi dữ liệu; khóa `.update.lock` chỉ chặn hai updater chạy đồng thời.
- Thử trên **bản sao mới**: sao chép DB sang `data/<tên-mới>.sqlite`, đặt `$env:DatabasePath`, rồi chạy updater/check/web như trên. Không ghi đè bản sao cũ.

### 10.3 Bộ demo và kiểm thử

```powershell
.\verification\New-Sprint2Demo.ps1      # tạo bộ demo mới trong data/sprint2-demo/ (không ghi đè, cập nhật latest.txt khi thành công)
.\verification\Start-Sprint2Demo.ps1    # chạy bộ demo (đọc latest.txt, hoặc -Directory để chọn bản cũ)
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug --no-restore
dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj --no-restore
python verification/s206_http.py        # HTTP S2-06 trên DB giả mới
python verification/s201_database.py    # nâng cấp bản sao, giữ dữ liệu, integrity/FK, chạy lặp
```

- Bộ demo gồm 7 tài khoản đủ 4 vai trò, 4 tòa, 45 phòng, 35 tin công khai (phân trang 12/12/11) và 8 yêu cầu đủ 5 trạng thái.
- Khi build mà web đang chạy khóa file output: build ra thư mục riêng (`-o data/<tên>/runtime`).
- Nếu sandbox lỗi Event Log/Data Protection khi chạy server: chạy ngoài sandbox.
- Email local là file `.eml`/`.txt` trong thư mục pickup, **không** vào inbox thật. SMTP thật cần `PasswordReset__Host`, `Port`, `EnableSsl`, `From`, `Username`, `Password`, `PublicBaseUrl`; đặt `PasswordReset__PickupDirectory = ' '` để tắt pickup. Production yêu cầu URL HTTPS; không commit bí mật. Đổi cấu hình email phải khởi động lại server.

---

## 11. Checklist trước khi kết thúc task

- [ ] Phạm vi đúng task; không triển khai thêm bảng/story ngoài yêu cầu.
- [ ] Quyền + sở hữu + CSRF kiểm tra ở backend; có test trái quyền (403).
- [ ] Tiền dùng `decimal`, làm tròn từng dòng; có test cho quy tắc tính tiền mới.
- [ ] Đổi trạng thái + lịch sử/nhật ký cùng transaction; có test rollback.
- [ ] Không đổi schema ngầm. Nếu đổi: bước updater có phiên bản mới, backup, integrity/FK, chạy lặp không đổi dữ liệu, thử trên **bản sao**, cập nhật tài liệu.
- [ ] Không commit DB, backup, `.env.local`, credential, `data/`. Chạy `git status` trước khi stage.
- [ ] Build + test liên quan chạy được. Ghi rõ kết quả thật và những gì **chưa** kiểm chứng (UI trực quan, mobile 360px, SMTP, 3G).
- [ ] Giao diện tiếng Việt, định dạng VND/ngày giờ đúng, không tràn ngang ở 360px.
- [ ] Cập nhật `tien-do.md`: phạm vi đã xong, giả định, việc còn lại, lệnh chạy/test.

---

## 12. Câu hỏi PO chưa chốt (không tự quyết)

- Phòng có trước khi thêm dịch vụ mặc định: có tự gán không? (hiện **không**; tập trung ở `DichVuPhongService.GanMacDinhChoPhongMoiAsync`, có TODO).
- Giá thương mại và đơn vị/cách tính mặc định (đề xuất: điện kWh, nước m³ theo chỉ số; rác theo người; gửi xe/internet theo phòng). Không dùng giá demo cho DB thật.
- Giới hạn mã phòng tầng 1..99.
- Ngày kết thúc kỳ hợp đồng khi cộng tháng (ngày cuối tháng); giá kỳ giao cắt ngày gia hạn: chia đoạn hay chính sách khác.
- Người ở ghép mới có tăng phí từ kỳ sau không (đề xuất: có).
- Công thức cảnh báo 200% và xử lý khi thiếu lịch sử 3 kỳ.
- Chính sách thử lại email (đề xuất: 1 lần đầu + 3 lần thử lại).
- `DA_CHOT` thanh lý có cho đóng hợp đồng và truy thu nợ cũ không.
- Điều chỉnh/hoàn hóa đơn đã thu, thu hồi cọc đã hoàn: **chưa hỗ trợ**; cần chốt nghiệp vụ trước khi mở rộng.

---

## 13. Rủi ro dự án cần giữ trong đầu

| Rủi ro | Cách ứng phó |
| --- | --- |
| Phạm vi phình, không kịp đóng luồng | Luồng chính đăng tin → thu tiền là bắt buộc; cắt `Should`/`Could` trước; thay đổi phạm vi trong sprint phải đánh đổi một story cùng điểm |
| Sai công thức điện nước/tất toán cọc | Bộ ví dụ tính tay ≥ 10 ca → test tự động; đổi công thức phải chạy lại |
| Hai người sửa cùng hóa đơn/chỉ số | Khóa lạc quan `phien_ban`; khóa kỳ sau phát hành |
| SMTP bị chặn/giới hạn | Tách gửi thông báo sau interface chung; dùng pickup khi phát triển; có phương án Mailtrap cho demo |
| Yêu cầu đổi giữa chừng | Ghi thành story mới vào backlog, không chèn ngang sprint |
