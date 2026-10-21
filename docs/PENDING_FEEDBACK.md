# Chờ xử lý — ghi nhận vấn đề thuộc phần của người khác

Không tự sửa vào nhánh đang làm. Ghi lại ở đây để không bị quên và không làm rối PR hiện tại.

## S2-08 — lát 5 `track_status_history`

Lát 6 (`approve_instant_rental`) đã xong. Lát 5 làm tiếp được một phần, nhưng còn thiếu dữ liệu
của hai phần khác nên chưa thể làm trọn vẹn.

### 1. Thiếu dòng lịch sử `TAO_YEU_CAU` — thuộc S2-06

`YeuCauThueService.Send` (`QL_PhongTro/Services/YeuCauThueService.cs:60`) tạo yêu cầu nhưng
không gì vào `yeu_cau_thue_lich_su`. Nên mọi yêu cầu hiện mở đều có lịch sử bắt đầu từ dòng
đầu tiên là một hành động của chủ nhà, thiếu mốc "khách đã gửi yêu cầu".

Bản gốc của lát 5 lách bằng `GhiBanGhiTaoLaiNeuThieuAsync`: mở trang lịch sử lần đầu thì
ghi bù một dòng `TAO_YEU_CAU` với `thoi_diem = ngay_tao` của yêu cầu. Cách này cho dữ liệu đúng
nhưng thời điểm ghi sai — sự kiện đã xảy ra từ lâu mới được ghi lại.

Đề xuất: S2-06 ghi thẳng một dòng `TAO_YEU_CAU` ngay trong `Send`, cùng transaction tạo yêu cầu.
S2-08 chỉ cần thêm hằng số `HanhDongYeuCau.TaoYeuCau` và nhãn tiếng Việt.

### 2. Thiếu bảng thông báo chung — không rõ thuộc story nào

Bản gốc của lát 5 đọc `db.ThongBaos`, tức bảng `thong_bao`. Trong `dev` (sau PR #34) **không có**
bảng này và cũng không có entity `ThongBao`. S2-08 chỉ có `yeu_cau_thue_thong_bao`
(migration v10), gắn với từng yêu cầu chứ không gom về một hộp thư chung cho tài khoản.

Cần ai đó xác nhận: trung tâm thông báo chung thuộc story nào, và lát 5 có nên tự đọc
`yeu_cau_thue_thong_bao` thay vì chờ bảng chung hay không.

### 3. Chuông thông báo trên thanh trên cùng — chạm `_Layout.cshtml`

`Views/Shared/_Layout.cshtml` nằm trong danh sách file dùng chung mà dự án cấm tự sửa, và phần
chuông thuộc module thông báo chung chứ không thuộc S2-08. Bản gốc lát 5 sửa 5 dòng ở file này
(commit `cf733f7`).

Đề xuất: làm phần lịch sử và trang danh sách thông báo trước, phần chuông để lại, chờ nhóm sở hữu
`_Layout.cshtml` chốt.

### Phần làm được ngay khi quyết định xong mục 2 và 3

- Cột `da_doc` và index theo người nhận đã có sẵn (`QL_PhongTro/Data/AppDbContext.cs:223`).
- Bảng `yeu_cau_thue_lich_su` đã ghi đủ mọi hành động từ lát 1, 3, 4, 6.