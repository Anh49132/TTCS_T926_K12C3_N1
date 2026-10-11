# Schema v24 — Ghi chú và tiện nghi phòng

Thêm hai cột nullable vào `phong_tro`: `ghi_chu_noi_bo` (TEXT, tối đa 500 ký tự) và `tien_nghi` (TEXT, mảng JSON). Không dùng `mo_ta` công khai để lưu ghi chú riêng. Nâng cấp nằm trong `Data/RoomDetailsSchema.cs`, nối tiếp v23; không sửa bước cũ.

Dừng web trước khi nâng cấp. Sao lưu DB ra vị trí riêng, thử trên bản sao bằng `DatabasePath`, rồi chạy:

```powershell
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
```

Updater tự backup trước thay đổi và thêm hai cột trong cùng transaction với marker v24. Nếu cột đã tồn tại nhưng thiếu marker thì dừng để rà soát. Chạy lại khi đã v24 không đổi dữ liệu. Máy mới chạy `--initialize-database` trên đường dẫn chưa tồn tại sẽ có schema v24.

Đã thử nâng cấp DB v23 trên bản sao: mọi bảng và cột dữ liệu cũ (ngoài marker phiên bản mới) được so sánh giữ nguyên; SQLite integrity_check/FK đạt; chạy lặp không đổi dữ liệu. Sau đó đã nâng cấp DB mặc định 36 phòng với backup tự động.

Trang sửa phòng nạp/lưu dữ liệu thật, tiện nghi dùng allowlist 8 mã. Ghi chú được encode bởi Razor, không đưa vào mô tả tin đăng hoặc nhật ký; tiện nghi có nhật ký theo allowlist cùng giao dịch lưu phòng hiện có. POST giữ kiểm tra sở hữu và CSRF.
