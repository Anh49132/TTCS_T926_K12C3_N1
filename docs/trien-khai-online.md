# Triển khai NhàTốt trên Railway

## Giới hạn chi phí đã chốt

Người dùng chốt ngày 22/10/2026: chỉ dùng miễn phí. Trước khi tạo dịch vụ, phải xác minh workspace đang ở Free hoặc Trial còn credit; không tạo tài nguyên trong workspace trả phí. Không nâng gói Hobby/Pro, thêm phương thức thanh toán, bật nạp credit hoặc mua tên miền/license. Nếu hết credit/hạn mức thì dừng triển khai hoặc để dịch vụ dừng, không tự chuyển trả phí. Free không bảo đảm đủ tài nguyên để chạy liên tục 24/7. Xem [gói Railway](https://docs.railway.com/pricing/plans).

Theo yêu cầu mới ngày 22/10/2026, xử lý ảnh dùng [SkiaSharp 4.153.1](https://www.nuget.org/packages/SkiaSharp/4.153.1), giấy phép [MIT](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md). Không cần đăng ký hoặc cấu hình license ImageSharp. Gói native Linux cùng phiên bản và thông báo bản quyền được kèm khi publish. Email chỉ dùng tài khoản/gói miễn phí đã xác minh; không tự chuyển Gemini key hoặc bật API tính phí để triển khai.

## Trạng thái 22/10/2026

Plugin Railway đã kết nối thành công với tài khoản `anh49132`; workspace cá nhân `DTC245200957's Projects`. Sau khi người dùng đăng nhập, dashboard xác nhận **Trial, còn $5.00 credit và 30 ngày** lúc khoảng 19:20 ngày 22/10/2026 (UTC+7). Không nâng gói/thêm thẻ/nạp credit.

[Sự cố nền tảng Railway](https://status.railway.com/incident/QH8L8B3E) đang chặn build Free/Trial (bản cập nhật 18:47 UTC+7). Thử tạo project riêng `NhaTot` qua plugin bị từ chối: `Deploys have been paused due to an upstream issue`. Gọi lại danh sách xác nhận vẫn **0 project**. Chưa tạo service/volume/domain, chưa upload dữ liệu và chưa có URL online. Không thử lặp cùng thao tác trong sự cố.

Lần kiểm tra DB trước: schema v42, `quick_check = ok`, không có vi phạm foreign key. Lượt chuyển SkiaSharp không chạy initializer/updater trên DB nguồn, không copy/sửa DB hoặc đổi tài khoản. Build Release đã thành công, không còn phụ thuộc ImageSharp. Kết quả hồi quy và gói publish được ghi thêm ở phần kiểm chứng bên dưới. Chưa kiểm chứng Linux/container do máy không có Docker/Bash.

Các file trong `deployment/` chuẩn bị cấu hình chạy Release cho `linux-x64`. Đây là cấu hình mới cho yêu cầu online, không khôi phục staging/Docker Compose cũ. Phát hiện cần xử lý trước public: [Railway Free/Trial chặn SMTP](https://docs.railway.com/networking/outbound-networking#email-delivery), ứng dụng hiện đã có transport Brevo HTTPS API (xem cập nhật Brevo bên dưới). Không bỏ kiểm tra email ở `start.sh` để che vấn đề; cần tích hợp email qua HTTPS API miễn phí trước khi nghiệm thu đầy đủ.

## Chuẩn bị bản Release

1. Dừng app dùng checkout trước khi publish. Giữ các thay đổi sẵn có trong working tree.
2. Restore các gói trong `QL_PhongTro.csproj`, gồm SkiaSharp và native Linux. Không cần `SixLaborsLicenseKey`.
3. Chạy `./deployment/Prepare-Online.ps1` từ PowerShell tại gốc repository. Script tạo thư mục duy nhất dưới `data/online/`, publish Release cho `linux-x64` không self-contained/không apphost (container dùng `dotnet QL_PhongTro.dll`), loại dữ liệu/cấu hình local qua `ReleasePublish.targets`, rồi kiểm tra lại trước khi đóng gói. Không gửi thư mục publish dang dở khi script lỗi.
4. Chỉ upload build context được script trả về; không upload toàn repository hoặc database qua build context. Nếu publish bị chặn bởi nội dung local, cần thiết lập loại bỏ nội dung đó khỏi publish rồi chạy lại; script hiện từ chối để tránh lộ dữ liệu.

## Hosting và dữ liệu

- Tạo một Railway service từ build context, sử dụng Dockerfile và `railway.json` trong đó; một replica. Tham khảo [ASP.NET Core](https://docs.railway.com/guides/aspnet-core).
- Gắn [Railway Volume](https://docs.railway.com/volumes) tại `/data` trước khi chạy. Database, file riêng, ảnh phòng và Data Protection keys phải nằm trên volume, không ở lớp filesystem tạm của container. Giữ một instance cho SQLite.
- Dừng app dùng database nguồn trước khi xuất dữ liệu. Sao lưu nguyên bộ database SQLite bằng backup API, kiểm tra integrity/FK trên bản sao; không copy riêng file chính nếu vẫn còn WAL hoạt động. Giữ bản gốc, không tự nâng schema trên bản gốc.
- Chuyển bản sao đã kiểm tra bằng kênh private vào `/data/nhatot.sqlite`. Chuyển kèm các thư mục bên cạnh database `.avatars`, `.bank-qr`, `.payment-proofs`, `.repair-images`, `.contract-documents` nếu có; tên trên máy chủ bắt đầu bằng `nhatot.sqlite`.
- Ảnh giấy tờ lấy từ cấu hình `IdentityImagePath`, mặc định `QL_PhongTro/App_Data/identity-images`, chuyển tới `/data/identity-images`. Ảnh phòng lấy từ `RoomImagesPath`, mặc định `QL_PhongTro/wwwroot/uploads/rooms`, chuyển tới `/data/rooms`. Chỉ ảnh phòng được phục vụ tĩnh; giữ file riêng ở ngoài webroot.
- Không đưa `.env.local`, credential demo, email pickup, DB backup hoặc license vào container image/Git. Không đổi tài khoản hoặc reset mật khẩu trong DB.
- Giữ bản gốc và backup ngoài Railway. Theo [chính sách Trial](https://docs.railway.com/pricing/free-trial#data-retention), volume của tài khoản Trial có thể bị xóa sau 30 ngày kể từ khi credit hết hạn. Xuất backup trước hạn; không nâng gói để tránh mất dữ liệu theo mặc định.

## Email sau khi build Release thành công

Không thể dùng SMTP trực tiếp trên Free/Trial, kể cả khi đã có tài khoản SMTP miễn phí: Railway chặn outbound SMTP trên các gói này. Không nâng gói hoặc mở cổng public làm cách vòng qua giới hạn.

1. Chọn tài khoản dịch vụ email có HTTPS API và hạn mức miễn phí, không bật nạp tiền tự động hoặc phương thức tính phí. Xem [danh sách ví dụ của Railway](https://docs.railway.com/networking/outbound-networking#email-service-examples); cần kiểm tra hạn mức và quyền gửi của tài khoản thực tế.
2. Xác minh địa chỉ/người gửi trong dashboard dịch vụ; lưu API key trong secret local/hosting, không gửi trong chat/Git. Không tự mua tên miền để xác minh.
3. Tích hợp transport HTTPS API sau `PasswordEmailSender`, giữ các interface đăng ký/reset/lịch hẹn/hóa đơn/báo hỏng/thanh toán. Đã tích hợp Brevo HTTPS API; cấu hình như phần cập nhật Brevo bên dưới, không dùng biến SMTP trên Free/Trial. Cập nhật kiểm tra email/startup cùng transport khi đã chọn nhà cung cấp.
4. Đặt `PasswordReset__PublicBaseUrl` bằng domain HTTPS thực tế, tắt pickup, gửi thử email bằng tài khoản được phép và kiểm tra inbox/reset URL. Chỉ nghiệm thu sau khi gửi thật thành công.

## Biến môi trường hiện có

Bảng dưới là cấu hình hiện tại của ứng dụng. Các biến SMTP cần được thay bằng cấu hình transport HTTPS API ở bước email trên trước khi chạy Railway Free/Trial.

| Tên | Giá trị |
| --- | --- |
| `Jwt__SecretKey` | Secret ngẫu nhiên riêng, tối thiểu 32 ký tự; giữ ổn định qua lần deploy |
| `PasswordReset__PublicBaseUrl` | URL HTTPS Railway thực tế sau khi tạo domain |
| `PasswordReset__Host`, `PasswordReset__Port` | SMTP thật và cổng tương ứng |
| `PasswordReset__EnableSsl` | `true` |
| `PasswordReset__From`, `PasswordReset__Username`, `PasswordReset__Password` | Địa chỉ gửi và thông tin SMTP qua secret của hosting |
| `PasswordReset__PickupDirectory` | Để trống |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` chỉ với container đi qua Railway HTTPS edge; không mở thêm TCP public trực tiếp vào Kestrel |
| `AllowedHosts` | Hostname Railway thực tế, không gồm `https://` |
| `Gemini__ApiKey` | Tùy chọn, nếu cần chatbot; dùng secret hosting |

Các đường dẫn dữ liệu và môi trường Production đã khai báo trong Dockerfile. `PORT` và `RAILWAY_VOLUME_MOUNT_PATH` do Railway cung cấp. Container dừng nếu thiếu volume, database, khóa JWT hoặc cấu hình email; không chạy initializer/updater. Việc tin cậy forwarded headers phải phù hợp topology thực tế trước khi public domain; tham khảo [Microsoft](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Sau khi xác minh Free/Trial và cấu hình email HTTPS API: kiểm tra bản sao DB với đúng runtime, chuyển dữ liệu riêng, cấu hình backup trong hạn mức miễn phí và thử phục hồi riêng, deploy rồi kiểm tra trang công khai, ảnh phòng, đăng nhập/đăng xuất, quyền 403, file private và email. Chỉ báo hoàn tất khi URL HTTPS thực tế trả lời đúng.

## Kiểm chứng SkiaSharp và Release ngày 22/10/2026

- Thay toàn bộ ImageSharp trong mã chạy, bộ tạo demo và fixture test bằng SkiaSharp 4.153.1. Giữ JPG/PNG theo nội dung và phần mở rộng, giới hạn 5 MiB/50 megapixel (ghi chú phòng: 10 MiB, 40 megapixel, mỗi chiều 12.000), giải mã frame đầu, từ chối kết quả decode chưa hoàn chỉnh. Giữ bản gốc ảnh phòng/báo hỏng/chứng từ; thumbnail rộng tối đa 400, giấy tờ rộng tối đa 1600, avatar crop giữa 512×512, xoay/lật EXIF và loại metadata khi encode ảnh dẫn xuất. Không đổi endpoint, quyền, đường dẫn lưu hay schema.
- 155 ca liên quan khác nhau đạt qua các lượt: 21 ca unit/ảnh, 134 ca workflow/quyền. Bao gồm 8 hướng EXIF (kiểm tra vị trí màu), PNG alpha, ảnh cắt/hỏng, kích thước khai báo vượt 50 MP, upload 8 ảnh, giới hạn 5 MiB, 403/no-store, avatar, QR, báo hỏng, chứng từ 15 phút và rollback. Ba test ảnh cũ đọc `/TinDang` gặp redirect được cập nhật sang trang hiện hành `/TimTin`; chạy lại đạt. Crop avatar kiểm tra với dung sai nội suy màu. Không phải full suite hoặc nghiệm thu UI/Linux/SMTP.
- TRX trong `data/skia-verification/`: `image-unit.trx`, `image-workflow.trx` (lượt đầu có 3 lỗi đường dẫn test), `image-final.trx` (3 đường dẫn đạt, assertion màu avatar cần dung sai), `avatar-final.trx` (đạt). Tổng hợp kết quả cuối theo từng test: 155 Passed, 0 Failed, 0 Skipped. Testhost bị treo trong sandbox; các kết quả đạt được chạy ngoài sandbox với ảnh sinh/CSDL tạm, không dùng DB nguồn.
- `dotnet build Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj -c Release --no-restore --nologo` đạt; còn 2 cảnh báo CS8601 có sẵn tại AuthController. Không còn lỗi license và không sửa project test.
- `./deployment/Prepare-Online.ps1` đạt. Gói sạch: `data/online/20261022-191934-1a1177e9`, 866 file, 61,47 MiB, native `libSkiaSharp.so`, đủ MIT/native notices. Kiểm tra 0 file riêng tư, không còn ImageSharp trong deps, không có endpoint upload local trong static manifest; shell LF, PowerShell parse và `git diff --check` đạt.
- Lượt publish trước bị guard từ chối do static web assets vẫn chép upload dù đã loại `ResolvedFileToPublish`; đã sửa `ReleasePublish.targets` để loại cả Content/None trước khi phát hiện static assets. Không dùng gói bị từ chối `data/online/20261022-191736-224bb967`; giữ dữ liệu nguồn, không upload gói đó.
- Checksum SQLite nguồn trước/sau giống hệt. Không updater/initializer, không reset tài khoản, không stage/commit hoặc gửi secret. Còn lại: chờ Railway mở lại Free/Trial; chọn và tích hợp email HTTPS API miễn phí; tạo service/volume, chuyển bản sao DB/ảnh qua kênh private, kiểm tra Linux/backup/phục hồi/URL HTTPS và luồng đăng nhập/phân quyền/email.

## Cập nhật Brevo HTTPS API

- Người dùng đã chọn Brevo và cung cấp ảnh người gửi NhàTốt trạng thái Verified. Dùng địa chỉ người gửi đó trong secret `PasswordReset__From`; không lưu địa chỉ cá nhân hoặc API key vào Git.
- Đặt `PasswordReset__Provider=Brevo`, `Brevo__ApiKey=<secret>`, `Brevo__SenderName=NhàTốt`, `PasswordReset__From=<email đã xác minh>`, `PasswordReset__PickupDirectory` rỗng. `PasswordReset__PublicBaseUrl` dùng URL HTTPS Railway thực tế. Không cần SMTP host/user/password cho Brevo.
- Mã gửi qua endpoint cố định https://api.brevo.com/v3/smtp/email với api-key header; client không theo redirect, timeout 15 giây, che header trong log, không log phản hồi lỗi/key/nội dung thư/người nhận. Chỉ ghi thành công khi HTTP 201 và có messageId; không tự gửi lại trong transport, giữ chính sách thử lại của hàng đợi nghiệp vụ.
- Áp dụng cho cả PasswordEmailSender và InvoiceEmailSender: đăng ký/reset/mật khẩu tạm, lịch hẹn, hóa đơn/phát hành/nhắc nợ, báo hỏng, thanh toán. Giữ SMTP/pickup cho cấu hình cũ và chặn pickup ở Production. start.sh chấp nhận Brevo thay cho SMTP khi đã có key/người gửi.
- Lưu key local: chạy deployment/Set-BrevoKey.ps1 trong PowerShell; nhập key bằng ô ẩn. Script lưu User variable đúng tên Brevo__ApiKey, không ghi key ra file/lệnh. Tên Brevo_ApiKey một dấu gạch dưới không ánh xạ thành Brevo:ApiKey. Các lệnh kiểm tra hiện chưa tìm thấy key ở Process/User/Machine; chưa gửi email thật hoặc xác nhận quyền/hạn mức tài khoản Brevo.
- 15/15 ca kiểm thử email liên quan đạt trên HTTP giả/CSDL tạm, gồm 12 loại nội dung, quyền header/recipient, 401/403/429/500/redirect, không retry/fallback, lỗi được lọc và chặn Production pickup. Release build đạt, còn 2 CS8601 cũ. TRX: data/brevo-verification/brevo.trx. Không đổi DB/schema/tài khoản hoặc project test. Chưa xác minh inbox/SMTP/Linux container.

- Publish Brevo Release linux-x64 đã đạt: data/online/20261022-193959-6891a421 (guard không có DB/private config/upload). Script nhập key parse 0 lỗi; SHA256 SQLite nguồn vẫn giống trước. Chưa gửi email thật.
- 22/10/2026: Người dùng xác nhận Brevo Free và cho phép gửi một thư thử tới hộp thư do người dùng chỉ định. Đọc key từ User environment Brevo__ApiKey trong bộ nhớ; POST HTTPS /v3/smtp/email trả HTTP 201 và messageId, đúng 1 request, không retry. Brevo đã tiếp nhận; chưa xác nhận thư vào inbox. GET /v3/account trước đó trả 403 do thiếu scope đọc tài khoản, không phải lỗi quyền gửi. Không lưu key/nội dung phản hồi/địa chỉ email vào tài liệu, không đổi gói/thanh toán/DB.
- 22/10/2026: Người dùng xác nhận đã nhận thư thử trong hộp thư; nghiệm thu gửi/nhận thực tế Brevo HTTPS API đạt. Kiểm tra lại trạng thái Railway: bản tin 19:30 UTC+7 vẫn chưa mở build Hobby/Free/Trial. Không thử tạo project lặp khi nền tảng còn tạm dừng, không nâng gói/thêm thanh toán. Gói Release Brevo đã sẵn sàng, còn chờ Railway khôi phục để chuyển dữ liệu và kiểm chứng online.
- 22/10/2026 20:31 UTC+7: Theo yêu cầu tiếp tục, Railway plugin vẫn kết nối được nhưng create-project trả Deploys have been paused due to an upstream issue. Trạng thái công khai cập nhật 20:16 UTC+7 vẫn chỉ mở build Pro/Enterprise, chưa Hobby/Free/Trial; hẹn thông báo tiếp trước 21:00 UTC+7, không phải ETA phục hồi. Không tạo được project/tài nguyên, không gửi DB/secret, không phát sinh phí. Bản Release và Brevo đã sẵn sàng.
