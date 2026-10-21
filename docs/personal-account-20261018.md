# Chỉnh sửa tài khoản cá nhân — 18/10/2026

- Cả bốn vai trò mở **Menu tài khoản → Chỉnh sửa tài khoản** (`/PersonalAccount/Edit`), hoặc mở từ hồ sơ cá nhân.
- Sửa họ tên và số điện thoại; kiểm tra định dạng, trùng số và biểu mẫu cũ. Tên/số điện thoại đồng bộ vào hồ sơ khách thuê nếu đã có. Biểu mẫu giấy tờ khách thuê cũ cũng đồng bộ tên về tài khoản.
- Email đăng nhập đã xác thực và vai trò chỉ xem, không nhận giá trị sửa từ client. Thông tin giấy tờ/ngày sinh/nghề nghiệp của khách vẫn chỉnh ở luồng hồ sơ hiện có.
- Tải/thay/xóa ảnh đại diện JPG/PNG tối đa 5MB, 50 megapixel; kiểm tra nội dung, bỏ metadata và cắt vuông 512×512. Ảnh hiển thị ở menu và trang hồ sơ; thiếu ảnh dùng chữ cái tên.
- Ảnh private lưu cạnh DB trong `<database>.avatars/`, tách theo DB và ID tài khoản, bị Git bỏ qua. Endpoint kiểm tra tài khoản hiện tại, không cho đọc ảnh tài khoản khác, `no-store`, `nosniff`. Khi sao lưu/chuyển máy cần mang theo thư mục ảnh nếu muốn giữ ảnh đại diện. Không đổi schema.
- Mọi POST có CSRF. Không nhận ID tài khoản để ghi chéo, không cho tự đổi vai trò/email/mật khẩu qua biểu mẫu này. Cơ chế khóa/thu hồi phiên và đổi mật khẩu hiện có giữ nguyên.

## Kiểm chứng

Build và 7 kiểm thử đạt: sửa riêng tài khoản ở bốn vai trò, dữ liệu giả mạo bị bỏ qua, đồng bộ hồ sơ khách, chống ghi đè biểu mẫu cũ, số điện thoại trùng/sai, CSRF, upload/đọc/xóa ảnh và chặn đọc chéo. Kết quả: `data/personal-edit-tests/personal-edit.trx`.

Edge headless trên bản sao DB đạt lưu tên/điện thoại và tải ảnh ở cả bốn vai trò, ảnh trả về 512px; giao diện 1440px/360px không tràn ngang. Ảnh QA: `data/personal-edit-qa/browser-check-8c13b8b8`. Chưa kiểm tra Safari hoặc thiết bị thật. Server QA đã dừng; DB/tài khoản đang dùng giữ nguyên, chưa commit/push.
