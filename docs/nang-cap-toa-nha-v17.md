# Nâng cấp thông tin quy mô và tiện ích tòa nhà (v17)

Dừng ứng dụng trước khi nâng cấp. Giữ nguyên đường dẫn DB đang dùng, không tạo lại hoặc chép đè DB.

```powershell
dotnet run --project QL_PhongTro -- --DatabasePath 'DUONG_DAN_DB' --update-database
dotnet run --project QL_PhongTro -- --DatabasePath 'DUONG_DAN_DB' --check-database
```

Updater tự sao lưu `DB.before-update-<id>.bak`, thêm bước v17 sau v16 trong transaction. Bổ sung `toa_nha.dien_tich_dat` (decimal TEXT, nullable, >0) và sáu cột boolean mặc định 0: thang_may, bai_do_xe, camera_an_ninh, bao_ve_24h, khu_giat_say, san_thuong. Không đổi bảng/cột cũ. NULL diện tích nghĩa là chưa khai báo; không lấy tổng diện tích phòng thay diện tích đất. Cập nhật được ghi nhật ký qua allowlist hiện có.

Đã kiểm chứng trên bản sao DB v16: mọi giá trị ở mọi cột cũ giữ nguyên, integrity_check=ok, foreign_key_check không lỗi; nâng cấp lặp không thay đổi. Khởi tạo DB rỗng lên v17 đạt. HTTP trên bản sao: lưu 240 m² và các tiện ích, tải lại đúng; chặn diện tích âm; chủ nhà khác không được sửa. DB demo hiện tại được nâng cấp sau kiểm chứng và có backup, DB cá nhân không thay đổi. Chưa kiểm tra trực quan/mobile, không chụp màn hình.
