# TTCS_QLPhongTro

Ứng dụng ASP.NET Core MVC dùng C#, .NET 10 và Entity Framework Core 10 (SQLite).

## Môi trường

- Cài [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) phù hợp với máy. Chỉ cài Runtime là chưa đủ để build mã nguồn.
- `global.json` chọn SDK 10.0 bản ổn định, cho phép các bản cập nhật trong dòng 10.0.
- Kiểm tra bằng `dotnet --list-sdks` và `dotnet --list-runtimes`; cần có SDK 10.0.x và Microsoft.AspNetCore.App 10.0.x.

## Chạy dự án

**Demo Sprint 2 đầy đủ:** xem [hướng dẫn chạy, tài khoản/mật khẩu và từng tiêu chí S2-01…S2-10](docs/huong-dan-test-sprint2.md). Tạo dữ liệu bằng `verification/New-Sprint2Demo.ps1`, chạy bằng `verification/Start-Sprint2Demo.ps1`; dùng database mới riêng với 7 tài khoản, 35 tin công khai (3 trang), 6 tin bị loại, 5 ảnh upload qua HTTP, dịch vụ, hợp đồng/hóa đơn và các trạng thái yêu cầu. Mật khẩu chung được sinh ngẫu nhiên và chỉ lưu trong `data/.../access.json` cùng `report.md` đã ignore.

Mở Terminal tại thư mục chứa README này. Dừng đúng phiên app bằng Ctrl+C trước khi cập nhật database.

**Trước lần pull nhận thay đổi S2-01 bỏ theo dõi database:** sao lưu database local và các file phụ SQLite ra ngoài repository (dừng app trước). Git có thể xóa bản database trước đây được theo dõi khi pull. Sau pull, khôi phục bản của chính bạn nếu file bị mất; không ghi đè file đang tồn tại.

Mặc định dùng `QL_PhongTro/Data/local-dev.sqlite`; có thể đặt `$env:DatabasePath` là đường dẫn tuyệt đối riêng trong cùng terminal. `run.bat` cũng đọc các khóa `DatabasePath`, `RoomImagesPath`, `DataProtectionKeysPath` và `PasswordReset__PickupDirectory` từ `.env.local` ở gốc repository. File này chỉ dành cho máy hiện tại và đã được Git ignore; biến môi trường đang đặt trong terminal luôn được ưu tiên.

Chạy nhanh trên Windows:

Mỗi máy có thể tự cấu hình `.env.local` để chọn database và kho ảnh riêng. Mặc định `run.bat` dùng `QL_PhongTro/Data/local-dev.sqlite` và chạy tại `http://localhost:5247`; email dùng `PasswordReset__PublicBaseUrl` trong cấu hình local. Cổng mặc định của script tạo demo cũng là 5247. Credential demo nếu có nằm trong `access.json` và `report.md` đã ignore. Server cần đang chạy để mở website; sau khi khởi động lại máy, chạy lại `run.bat`.

```bat
run.bat
```

`run.bat` tự chạy `dotnet restore`, nạp `.env.local` nếu có, dùng `DatabasePath` hiện có hoặc đường dẫn mặc định, tạo database mới bằng `--initialize-database` nếu file chưa tồn tại, rồi chạy `--check-database` trước khi mở web tại `http://localhost:5247`. Với database đã tồn tại, script giữ nguyên file và **không tự động nâng cấp**; nếu schema cũ, script dừng để bạn sao lưu rồi chạy `--update-database` theo quy trình bên dưới. Dùng `run.bat --check-only` để chỉ restore/khởi tạo nếu cần/kiểm tra mà không mở web. Script không tạo ADMIN hoặc nhúng thông tin đăng nhập.

- Máy mới chưa có database: không tạo `.env.local`, hoặc trỏ `DatabasePath` tới một file chưa tồn tại; `run.bat` sẽ dùng initializer và từ chối ghi đè.
- Máy đã có database cần giữ: trỏ `.env.local` tới đúng file hiện có và kho ảnh tương ứng; `run.bat` chỉ kiểm tra schema, không sao chép, xóa hay nâng cấp tự động.

Máy mới **chưa có database**:

```powershell
dotnet restore .\QL_PhongTro\QL_PhongTro.csproj
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj -- --initialize-database
```

Lệnh tạo file mới từ schema nền và các bước cập nhật đã phiên bản hóa, chỉ nạp vai trò/quyền, không tạo tài khoản hoặc dữ liệu demo. Lệnh từ chối file đã tồn tại; nếu khởi tạo thất bại, giữ file để kiểm tra và chọn đường dẫn mới khi thử lại.

Khối lệnh chạy nhanh cố định cho máy **đã có database cần giữ dữ liệu**, hoặc vừa khởi tạo/khôi phục bản sao của chính mình:

Khối lệnh dưới đây nhận cấu hình ADMIN riêng trên máy, không nhúng mật khẩu cố định. Nếu ADMIN đã tồn tại, bỏ qua lệnh `--create-local-admin` và đăng nhập bằng mật khẩu hiện có; lệnh này không đổi mật khẩu tài khoản cũ.

```powershell
# Dừng phiên QL_PhongTro cũ nếu terminal trước đã đóng hoặc không còn thấy dòng "Now listening"
Get-Process QL_PhongTro -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet restore .\QL_PhongTro\QL_PhongTro.csproj
# Máy mới: chỉ chạy khi file DatabasePath chưa tồn tại; lệnh từ chối ghi đè.
# dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj -- --initialize-database
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj -- --update-database
$env:LocalAdmin__Email = Read-Host 'Email ADMIN local'
$localAdminSecret = Read-Host 'Mat khau ADMIN local' -AsSecureString
$env:LocalAdmin__Password = [System.Net.NetworkCredential]::new('', $localAdminSecret).Password
$env:LocalAdmin__Phone = Read-Host 'So dien thoai ADMIN local'
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj -- --create-local-admin
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --launch-profile http
```

Updater kiểm tra schema, sao lưu và nâng lên phiên bản hiện hành v41; không tạo lại database. Web không tự nâng schema cũ. Sau khi cập nhật, có thể kiểm tra chỉ đọc bằng `dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj -- --check-database`.
Truy cập http://localhost:5247. Giữ terminal đang hiện `Now listening on: http://localhost:5247` mở trong lúc sử dụng. Muốn dừng server, bấm vào đúng terminal đó, nhấn Ctrl+C và chờ dấu nhắc `PS C:\...>` xuất hiện lại rồi mới build hoặc chạy lần nữa.

Không cần cập nhật database hoặc tạo ADMIN mỗi lần chạy. Sau khi pull thay đổi schema, chạy updater một lần. Tài khoản mới đang chờ xác nhận thì tiếp tục nhập/gửi lại mã, không cần xóa rồi đăng ký lại.

Khối lệnh trên nhận cấu hình ADMIN riêng trên máy. Lệnh tạo ADMIN không ghi đè tài khoản đã có. Nếu có lỗi, dừng và xử lý lỗi trước khi chạy bước tiếp theo. Xem thêm [hướng dẫn cập nhật SQLite](docs/cap-nhat-csdl.md).

### Nhận mã xác nhận qua Gmail

Các lệnh chạy app ở trên **không cấu hình Gmail**. Development mặc định lưu thư `.eml` vào `%TEMP%\s105-mail-preview`, không gửi tới hộp thư. Nếu dùng `$env:...`, phải cấu hình trong cùng terminal chạy app; terminal mới không giữ các biến của terminal cũ.

Sau Ctrl+C, kiểm tra chế độ gửi (không hiện mật khẩu, không gửi thư):

```powershell
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --launch-profile http -- --check-email-config
```

Để gửi Gmail thật, đặt các biến dưới đây rồi chạy app trong **cùng terminal**. Dùng mật khẩu ứng dụng Google của tài khoản gửi; không nhập mật khẩu vào chat hoặc commit vào file cấu hình.

```powershell
$env:PasswordReset__PickupDirectory = ' ' # Dấu cách để ghi đè chế độ lưu file
$env:PasswordReset__Host = 'smtp.gmail.com'
$env:PasswordReset__Port = '587'
$env:PasswordReset__EnableSsl = 'true'
$env:PasswordReset__Username = Read-Host 'Gmail gui thu'
$env:PasswordReset__From = $env:PasswordReset__Username
$smtpSecret = Read-Host 'Mat khau ung dung Google' -AsSecureString
$env:PasswordReset__Password = [System.Net.NetworkCredential]::new('', $smtpSecret).Password
$env:PasswordReset__PublicBaseUrl = 'http://localhost:5247'
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --launch-profile http -- --check-email-config
```

Chạy lệnh khởi động cuối cùng trong mục **Chạy dự án** ở cùng terminal, sau đó chọn **Gửi lại mã**. Trang sẽ báo riêng chế độ thử nghiệm, lỗi gửi, hoặc SMTP đã nhận thư. SMTP nhận thư chưa chứng minh thư đã tới inbox; kiểm tra cả Spam. Xem [hướng dẫn mật khẩu ứng dụng Google](https://support.google.com/mail/answer/185833?hl=vi).

Kết quả `--check-email-config`: `PICKUP` nghĩa là chỉ lưu file; `SMTP` nghĩa là dùng máy chủ gửi thư. Với Gmail, host phải là `smtp.gmail.com`, port `587`, TLS `True` và username/password/sender đều `configured: True`. Lệnh này chỉ kiểm tra cấu hình, chưa thử kết nối hoặc xác thực Gmail.

### Xử lý lỗi khởi động

Nếu terminal đã hiện dấu nhắc `PS C:\...>` nhưng website vẫn truy cập được, server đang chạy ở terminal hoặc tiến trình khác. Xác định đúng phiên đang dùng và dừng bằng Ctrl+C.

Lỗi `MSB3021` hoặc `MSB3027` kèm thông báo `QL_PhongTro.exe ... being used by another process` có nghĩa là server cũ đang khóa file build. Chạy lệnh dừng ở trên, đợi vài giây rồi chạy lại lệnh `dotnet run`. Lỗi SQLite như `no such table` có nguyên nhân khác; làm theo quy trình `--update-database` và tài liệu `docs/cap-nhat-csdl.md`.

**`FileLoadException`, `0x800711C7`, “An Application Control policy has blocked this file”**: Windows chặn thực thi DLL trước khi app chạy. Build có thể thành công nhưng web, updater và kiểm tra SMTP đều chưa chạy được. Không chạy lặp lại lệnh tạo ADMIN/cập nhật DB để xử lý lỗi này.

Trên máy đã kiểm tra, nhật ký `Microsoft-Windows-CodeIntegrity/Operational` có event 3077, policy `VerifiedAndReputableDesktop` (Smart App Control), chặn `QL_PhongTro.dll` chưa ký số. Đây không phải cảnh báo license ImageSharp hoặc bằng chứng ổ D chỉ đọc. Kiểm tra tại **Windows Security → App & browser control → Smart App Control settings**. Dùng bản build ký số được tin cậy để giữ bảo vệ; nếu chủ máy cá nhân chọn tắt Smart App Control cho môi trường phát triển, cần hiểu thay đổi áp dụng toàn máy. Máy do tổ chức quản lý cần quản trị viên xử lý. Xem [hướng dẫn Microsoft](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions). README không tự thay đổi chính sách bảo mật.

Lệnh `--update-database` sao lưu rồi cập nhật schema còn thiếu; nên dừng ứng dụng trước khi chạy. Ứng dụng yêu cầu file SQLite đã tồn tại và không tự tạo lại CSDL nền. Mặc định dùng `QL_PhongTro/Data/local-dev.sqlite`; có thể đặt biến môi trường `DatabasePath` để dùng file riêng.

Schema v6 bổ sung dịch vụ S1-09 nếu chưa có và hai bảng `dich_vu_toa_nha`, `dich_vu_phong`. Schema v7 thêm cột giá riêng nullable theo từng cặp phòng/dịch vụ; giá riêng thắng giá chung và không thay đổi snapshot hóa đơn. Schema v8 bổ sung lịch sử ngừng dịch vụ phòng. Schema v11 thêm bảng `yeu_cau`; schema v12 thêm trạng thái chờ xoá cho ảnh phòng để retry khi kho lưu trữ lỗi; schema v13 cấp quyền đăng tin cho Quản lý tại tòa được phân công. Hợp đồng/hóa đơn vẫn là module tùy chọn, không được tự triển khai bởi S2-01.

### Dịch vụ tòa nhà và phòng (S2-01)

Chủ nhà mở `/DichVu`, khai báo tên/đơn giá và bật “Áp dụng mặc định”. Từ danh sách phòng chọn **Dịch vụ** để thêm/bỏ và xem tổng dịch vụ cố định dự kiến/tháng. Khi gán dịch vụ có thể nhập đơn giá riêng; bảng hiển thị giá chung, giá áp dụng, đánh dấu giá riêng khác giá chung và cho sửa hoặc quay lại giá chung. Giá riêng chỉ áp dụng cho phòng đó. Phòng mới (đơn hoặc hàng loạt) nhận lựa chọn mặc định tại lúc tạo; thay đổi mặc định không cập nhật các phòng cũ. Phòng có trước migration giữ nguyên dữ liệu và bắt đầu với danh sách lựa chọn mới rỗng; chủ nhà chọn dịch vụ cần dùng.

Tổng không gồm tiền thuê, phí theo chỉ số/theo người hoặc dịch vụ chưa có giá đang áp dụng. PO đã chốt: bỏ dịch vụ giữa tháng vẫn tính hết tháng đó và ngừng từ ngày đầu tháng sau. Hóa đơn đã phát hành hiển thị snapshot tên, đơn giá và thành tiền đã lưu; không đọc lại trạng thái dịch vụ phòng hiện tại. Khi phát hành hóa đơn, chọn hợp đồng/phòng và kỳ để danh sách dịch vụ được lọc theo đúng phòng và tháng.

### Demo cấu hình điện nước theo tòa (S2-10)

Fixture này tạo database hoàn toàn mới trong `data/s210-demo/`, không đọc hoặc ghi `QL_PhongTro/Data/local-dev.sqlite`. Cổng demo là `5251`; hãy dừng phiên demo cũ trên đúng cổng trước khi tạo bản mới.

```powershell
dotnet build .\QL_PhongTro\QL_PhongTro.csproj -c Debug -o .\data\s210-demo\runtime --no-restore
python .\verification\prepare_s210_demo.py
```

Script tạo bốn tài khoản giả dùng chung một mật khẩu ngẫu nhiên, một tòa và hai phiên bản cấu hình cho mỗi dịch vụ; sau đó kiểm tra đăng nhập, lưu kỳ sau, từ chối giá 0, integrity/FK và giữ server chạy tại `http://localhost:5251`. Đọc thông tin bản mới nhất:

```powershell
$demoFolder = (Get-Content .\data\s210-demo\latest.txt -Raw).Trim()
$demo = Get-Content (Join-Path $demoFolder 'access.json') -Raw | ConvertFrom-Json
$demo.url
$demo.accounts.CHU_NHA.email
$demo.password
$demo.current
$demo.pending
```

Đăng nhập bằng tài khoản `CHU_NHA`, mở **Quản lý tòa nhà → Cấu hình điện nước**. Màn hình mẫu cho thấy kỳ hiện tại có điện theo chỉ số và nước theo đầu người; kỳ kế tiếp đảo lại hai cách tính và ghi rõ tháng bắt đầu áp dụng. Đổi lựa chọn hoặc giá hợp lệ để thấy thông báo kỳ trước nút lưu. Xóa giá hoặc nhập `0`, bấm lưu để thấy lỗi và dữ liệu không đổi.

Muốn chạy lại đúng database demo sau khi server đã dừng:

```powershell
$env:DatabasePath = $demo.database
$env:DataProtectionKeysPath = Join-Path $demoFolder 'keys'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $demo.url
dotnet .\data\s210-demo\runtime\QL_PhongTro.dll
```

`DataProtectionKeysPath` chỉ định thư mục khóa cookie riêng cho phiên demo, tránh dùng chung kho khóa của database khác. Database, khóa, mật khẩu, log và HTML kiểm tra đều nằm trong `data/` đã được Git ignore; không commit hoặc chia sẻ chúng.

### Demo bỏ dịch vụ khỏi phòng

Tạo fixture hoàn toàn giả trên database mới, không ghi database local:

```powershell
dotnet run --project .\verification\ServiceRemovalDemo\ServiceRemovalDemo.csproj -- .
$demoAccessPath = (Get-Content -Raw data\service-removal-demo\latest.txt).Trim()
$demo = Get-Content -Raw $demoAccessPath | ConvertFrom-Json
$env:DatabasePath = $demo.database
dotnet run --project .\QL_PhongTro\QL_PhongTro.csproj --launch-profile http
```

Mở file `access.json` tại đường dẫn trong `latest.txt` để lấy email/mật khẩu fixture. Đăng nhập Chủ nhà, vào **Phòng → Dịch vụ** của `DEMO-A`, bấm **Ngừng từ kỳ sau** tại Gửi xe. Mở lại hóa đơn kỳ trước để thấy Gửi xe vẫn là 100.000đ. Vào **Lập hóa đơn**, chọn `HD-DEMO-A` và kỳ sau ngày ngừng: danh sách không còn Gửi xe; phát hành sẽ chỉ còn tiền phòng. Chọn `HD-DEMO-B` cùng kỳ vẫn thấy Gửi xe. Database, credential và backup demo nằm trong `data/` đã được Git ignore.

Kiểm thử không cần database cá nhân: `dotnet test Tests/QL_PhongTro.Tests/QL_PhongTro.Tests.csproj`. Kiểm tra nâng cấp trên bản sao database local: build trước rồi chạy `python verification/s201_database.py`. Không commit database, backup, file phụ SQLite hoặc cấu hình bí mật.

## Database riêng và S2-06: gửi yêu cầu từ tin đăng

**Trước lần pull nhận thay đổi bỏ theo dõi `QL_PhongTro/Data/local-dev.sqlite`, mỗi thành viên phải dừng app và sao lưu database local ra ngoài repository. Git có thể xóa file đang được theo dõi khi pull.** File trên máy thực hiện task vẫn được giữ nguyên bởi `git rm --cached`. Không chép DB của thành viên khác vào repo; DB, WAL/SHM/journal, backup và credential demo đều bị ignore.

- Máy mới chưa có DB: đặt `DatabasePath` tới file riêng chưa tồn tại, chạy `--initialize-database`, rồi `--check-database`. Khởi tạo schema nền hiện hành v22 và quyền module; không tạo tài khoản, dữ liệu cá nhân hoặc demo. Lệnh từ chối file đã tồn tại; web không tự tạo database. ADMIN dùng cấu hình riêng như trên.
- Máy đã có DB: kiểm tra đúng `DatabasePath`, dừng app, sao lưu ngoài repository và chạy `--check-database` trước. Khi schema nền cần nâng cấp, dùng `--update-database` có backup, không chạy khởi tạo hoặc chép đè. Nếu lỗi schema lạ, dừng để rà soát.
- Để sử dụng S2-06 trên DB đã kiểm tra: chạy `--initialize-rental-requests` một lần trên đúng `DatabasePath`, rồi `--check-database`. Module có phiên bản riêng `rental_request_schema=1`, không thay đổi phiên bản nền; sử dụng bảng tin đăng đã có ở v9, chỉ bổ sung yêu cầu và bộ đếm. Cài mới tạo backup `*.before-rental-<id>.bak`, transaction và bảng tin/yêu cầu/bộ đếm; chạy lại không ghi dữ liệu. Schema module chưa có phiên bản hoặc không đầy đủ bị từ chối. Web không tự cài module.

Ví dụ sau khi đã chọn và kiểm tra DB phù hợp:

```powershell
dotnet run --project QL_PhongTro -- --initialize-rental-requests
dotnet run --project QL_PhongTro -- --check-database
dotnet run --project QL_PhongTro --launch-profile http
```

### Demo riêng cho xử lý lịch hẹn S2-08

Chỉ tạo demo trên một file mới trong `data/`; không trỏ các lệnh sau vào database đang sử dụng. Mật khẩu được truyền bằng biến môi trường và không lưu trong mã nguồn. Seeder tạo một Chủ nhà, một Khách thuê dùng để nghiệm thu và một Khách thuê giả nội bộ cho ca trùng lịch; không tạo ADMIN.

```powershell
$env:DatabasePath = Join-Path (Get-Location) 'data/s2-08-demo/demo.sqlite'
$env:RequestDemo__Password = '<mật-khẩu-demo-cục-bộ>'
$env:DataProtectionKeysPath = Join-Path (Get-Location) 'data/s2-08-demo/keys'
dotnet run --project QL_PhongTro -- --initialize-database
dotnet run --project QL_PhongTro -- --seed-request-demo
dotnet run --project QL_PhongTro -- --check-database
dotnet run --project QL_PhongTro -- --urls http://127.0.0.1:5268
```

`--initialize-database` từ chối ghi đè file đã tồn tại. `--seed-request-demo` kiểm tra schema trước khi ghi, cài module yêu cầu thuê có phiên bản nếu còn thiếu, tạo backup cạnh file demo và từ chối database đã có bất kỳ tài khoản/dữ liệu nghiệp vụ nào; không dùng lệnh này trên database cá nhân. Console in tài khoản demo, đường dẫn từng yêu cầu và giờ Việt Nam của lịch dùng để thử cảnh báo ±30 phút. Nếu máy đã có database cần giữ dữ liệu, tiếp tục dùng quy trình `--check-database`/`--update-database` trên bản sao như mục trên, không chạy seeder demo.

Chủ nhà quản lý đăng/gỡ tin tại `/TinDang/QuanLy`. Khách đã hủy yêu cầu `MOI` hoặc `DA_HEN_LICH` có thể quay lại đúng tin đăng và gửi yêu cầu mới; hệ thống chỉ chặn khi cùng khách vẫn còn yêu cầu `MOI`, `DA_HEN_LICH` hoặc `DA_DUYET` trên tin đó.

### Ảnh phòng: xoá, retry và đối chiếu kho

Chủ nhà xoá ảnh trong form sửa phòng bằng nút **Xóa** trên từng ảnh. Hệ thống hỏi xác nhận “Xoá ảnh này? Ảnh sẽ bị xoá vĩnh viễn và không thể khôi phục.”; chọn **Huỷ** không ghi gì, chọn **Xoá** sẽ xoá bản ghi, tệp gốc, thumbnail và đánh lại `thu_tu` liên tục. Nếu ảnh đại diện bị xoá, ảnh kế tiếp tự lên làm đại diện; nếu xoá hết, trang tin dùng `room-placeholder.svg`.

Nếu xoá tệp trên kho lưu trữ lỗi, bản ghi được giữ với `dang_cho_xoa=1`, lỗi gần nhất được lưu để background retry định kỳ. Có thể chạy thủ công:

```powershell
dotnet run --project QL_PhongTro -- --retry-pending-room-image-deletes
dotnet run --project QL_PhongTro -- --check-room-image-storage
```

`--check-room-image-storage` đối chiếu bản ghi `anh_phong` với `RoomImagesPath`/`wwwroot/uploads/rooms`, in `MISSING` và `ORPHAN`; exit code 2 khi còn lệch. Dừng app và dùng đúng `DatabasePath` trước khi chạy trên DB thật.

Khách thuê vào **Tin đăng cho thuê** → chi tiết tin → **Gửi yêu cầu**, chọn Xem phòng/Thuê ngay, ngày mong muốn, số người và lời nhắn tùy chọn. Gửi thành công chuyển ngay đến trang có mã `YC-yyyyMM-xxxx` (tháng Việt Nam). Profile tối thiểu được tạo từ tài khoản nếu chưa có; không yêu cầu nhập căn cước để gửi yêu cầu. Yêu cầu liên kết profile/tài khoản và tin từ URL, không nhận ID khách từ form. Mã tăng từ 0001 theo từng tháng, tối đa 9999 mã/tháng; hết mã báo lỗi và không lưu yêu cầu.

Task chỉ có trang xem danh sách/chi tiết tối thiểu; chưa có chức năng chủ nhà tạo/duyệt tin. Tin public phải đang hiển thị, còn hạn (hoặc không đặt hạn), phòng trống và tòa đang hoạt động. Ngày mong muốn chỉ được từ hôm nay đến 60 ngày sau, gồm cả hai đầu, theo múi giờ Việt Nam. Biểu mẫu giới hạn ngày chọn và báo lỗi tại trường ngày; server cũng từ chối ngày ngoài khoảng khi gửi POST trực tiếp. Số người phải là số nguyên dương và không vượt sức chứa hiện tại của phòng gắn với tin đăng. Vượt giới hạn báo “Phòng chỉ cho phép tối đa N người.” tại ô số người, không lưu yêu cầu. Giới hạn ngày/số người và kiểm soát gửi trùng không cần cập nhật schema database. Chưa triển khai duyệt, giữ chỗ, hủy hoặc hợp đồng.

PO chốt ngày 02/10/2026: `MOI`, `DA_HEN_LICH`, `DA_DUYET` là yêu cầu đang mở. Mỗi tài khoản chỉ gửi một yêu cầu đang mở cho cùng tin, tính chung cả Xem phòng và Thuê ngay. Gửi trùng báo “Bạn đã có yêu cầu đang mở cho tin đăng này. Không tạo thêm yêu cầu mới.” và có liên kết **Mở lại yêu cầu đang tồn tại**. Trang `/TinDang/YeuCau/{id}` hiển thị mã, trạng thái, loại, ngày mong muốn, số người, lời nhắn, thời điểm gửi và lịch hẹn/lý do từ chối nếu có; chỉ chính khách gửi được xem, kể cả khi tin đã hết hạn. `TU_CHOI` hoặc `DA_HUY` cho phép gửi lại khi tin vẫn public; tin khác hoặc tài khoản khác không bị chặn. Kiểm tra trong transaction SQLite khóa ghi trước khi cấp mã, tạo yêu cầu và audit nên gửi đồng thời cũng chỉ tạo một yêu cầu. Dữ liệu trùng từ trước được giữ nguyên; nếu có, liên kết trỏ yêu cầu mở có ID nhỏ nhất, không tự đóng/xóa dữ liệu cũ.

### Kiểm thử và demo bằng dữ liệu giả riêng

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug -o data/S2-06/runtime
python verification/s206_http.py
```

Script tạo DB mới chỉ có dữ liệu giả, tài khoản với mật khẩu ngẫu nhiên và một tin public; kiểm thử HTTP tại cổng 5266 rồi dừng server. Khi nguồn local tồn tại, chỉ đọc schema/sao lưu SQLite nhất quán và thử nâng cấp trên bản sao; đối chiếu dữ liệu cũ, SHA-256 nguồn. Không dùng DB đang sử dụng làm fixture. Nếu sandbox chặn Windows Event Log/Data Protection, chạy script trong terminal Windows bình thường.

Nếu đầu ra `runtime` hoặc cổng 5266 đang được sử dụng, build và chạy bộ kiểm thử gửi trùng riêng:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -c Debug -o data/S2-06/duplicate-runtime
$env:QL_TEST_DLL = Join-Path $PWD 'data/S2-06/duplicate-runtime/QL_PhongTro.dll'
$env:QL_TEST_BASE = 'http://localhost:5269'
python verification/s206_http.py
```

Fixture mới có tin A/B; `latest.txt` trỏ lượt PASS mới nhất, `access.json` ghi đúng database/runtime/URL của lượt đó. Bộ HTTP kiểm tra trạng thái mở/đóng, hai loại yêu cầu, quyền xem yêu cầu cũ, tin khác/tài khoản khác và bốn lần gửi đồng thời. Sau kiểm thử, tin A có một yêu cầu mở để demo từ chối và mở lại; tin B cho khách gửi lần đầu. Các thay đổi trạng thái trong kiểm thử chỉ thực hiện trên fixture giả.

Sau khi script PASS, chạy demo đã có (cổng 5266 phải trống):

```powershell
$s206Folder = Get-Content data/S2-06/latest.txt -Raw
$s206Access = Get-Content (Join-Path $s206Folder 'access.json') -Raw | ConvertFrom-Json
$s206Access | Select-Object email,password,listing
$env:DatabasePath = $s206Access.database
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet $s206Access.runtime --contentRoot "$PWD/QL_PhongTro" --urls $s206Access.url
```

Đăng nhập bằng thông tin local vừa đọc; mở URL trong `$s206Access.url` rồi vào `/TinDang`. Tin A (`listing`) đã có yêu cầu mở: gửi lại form hợp lệ sẽ bị từ chối, bấm **Mở lại yêu cầu đang tồn tại** để xem yêu cầu cũ. Tin B (`listing_b`) cho gửi thành công, lần gửi thứ hai bị từ chối. Credential chỉ lưu tại `data/S2-06/.../access.json` đã ignore, không chia sẻ file này. Dừng bằng Ctrl+C. Muốn quay về DB mặc định, xóa biến `DatabasePath` khỏi terminal. Demo không chứa dữ liệu cá nhân từ DB nguồn. Với fixture cũ chưa có trường `runtime`, chạy lại script bằng mã mới trước khi demo.

Demo giới hạn số người: phòng S206-101 của fixture mới cho phép tối đa 4 người. Nhập 5 sẽ thấy “Phòng chỉ cho phép tối đa 4 người.” và bị chặn; nhập từ 1 đến 4 với ngày hợp lệ gửi được. Bộ HTTP kiểm tra cả POST trực tiếp vượt giới hạn, thông báo đúng sức chứa thay đổi và không ghi dữ liệu khi từ chối.

## Tài khoản quản trị local

Các lệnh tạo ADMIN bằng cấu hình riêng đã có trong mục **Chạy dự án**. Lệnh chỉ chạy trong Development, không ghi đè tài khoản đã có và sao lưu trước khi tạo. Mật khẩu trong SQLite được lưu dưới dạng băm BCrypt.

Các biến `LocalAdmin__...` chỉ phục vụ tạo tài khoản, không cấu hình Gmail. Nếu ADMIN đã tồn tại, dùng tài khoản đó để đăng nhập.

Chạy trong lúc phát triển:

```powershell
dotnet watch --project .\QL_PhongTro\QL_PhongTro.csproj run --launch-profile http
```

SQLite mặc định nằm tại `QL_PhongTro/Data/local-dev.sqlite`. Không cần cài SQL Server; giữ lại file này nếu đã có dữ liệu và không chia sẻ CSDL có dữ liệu cá nhân.

Quy trình đồng bộ schema và hướng dẫn kiểm thử S1-03 nằm tại:

- [Cập nhật SQLite](docs/cap-nhat-csdl.md)
- [Tiến độ, chức năng và kiểm thử](docs/tien-do.md)

## Demo dịch vụ và hóa đơn trên bản sao

Các lệnh trong mục này là hướng dẫn demo lịch sử S1-09, **chưa được cập nhật/nghiệm thu cho v6**; không dùng để xác minh S2-01. Dùng bộ xUnit và `verification/s201_database.py` ở trên cho nhánh này.

Demo riêng dùng **http://localhost:5250**, không phải cổng 5247 của DB mặc định. Tạo mới từ gốc repo khi cổng 5250 đang trống:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj -c Debug -o data/service-demo/runtime
python verification/prepare_service_demo.py
```

Cần Python 3. Script dành cho DB nguồn hiện có chưa cài các bảng hợp đồng/dịch vụ/hóa đơn; nếu đã có, script dừng để kiểm tra fixture. Script đọc schema, tạo bản sao mới, cập nhật v3 và schema demo; không ghi DB nguồn. Windows sandbox có thể chặn Data Protection/Event Log: chạy từ terminal Windows bình thường, không thay đổi bảo mật ứng dụng.

Script in **email và mật khẩu thật**, tạo 4 tài khoản, 1 tòa, 3 phòng, 2 hợp đồng hiệu lực 24 tháng và 5 dịch vụ có giá; xác minh đăng nhập/hóa đơn rồi giữ server chạy nền. Dữ liệu hợp đồng là fixture SQL vì chưa có CRUD hợp đồng. Tạo tòa/phòng/dịch vụ và phát hành hóa đơn đi qua HTTP thật để ghi audit. Mọi artifact, credential và log nằm trong `data/service-demo/` đã ignore; file `latest.txt` trỏ thư mục demo mới nhất thành công.

Đăng nhập bằng email CHU_NHA được in ra. Mở `/DichVu`, sau đó `/HoaDonDichVu` và chọn tòa demo. DEMO-101 có hóa đơn tháng hiện tại 2.030.000đ (phòng 2.000.000 + điện 10×3.000). DEMO-102 chưa có hóa đơn: chọn HD-DEMO-2, chỉ chọn Điện, nhập đầu 100/cuối 110, kỳ hiện tại để thử phát hành cùng tổng tiền. ADMIN xem `/NhatKy`; ADMIN không thay thế vai trò Chủ nhà để quản lý dịch vụ.

Đọc lại credential và chạy lại bản demo đã có, không cần tạo thêm:

```powershell
$demoFolder = Get-Content data/service-demo/latest.txt -Raw
$demoInfo = Get-Content (Join-Path $demoFolder 'access.json') -Raw | ConvertFrom-Json
$demoInfo.accounts
$demoInfo.password
# Chỉ chạy tiếp khi phiên demo cổng 5250 đã dừng
$env:DatabasePath = $demoInfo.database
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:PasswordReset__PublicBaseUrl = 'http://localhost:5250'
$env:PasswordReset__PickupDirectory = Join-Path $demoFolder 'mail'
$env:IdentityImagePath = Join-Path $demoFolder 'images'
dotnet run --project QL_PhongTro --launch-profile http -- --urls http://localhost:5250
```

Lần tạo đầu server chạy nền, PID được in và lưu trong access.json; xác nhận tiến trình đúng trước khi dừng. Khi chạy lại bằng terminal, dùng Ctrl+C. Không commit database hoặc access.json. Chưa có xác minh UI đồ họa/SMTP thật trong công cụ này.

## Kiểm tra build

```powershell
dotnet build .\QL_PhongTro\QL_PhongTro.csproj
```

Các view `.cshtml` được biên dịch cùng dự án C#. Dùng `dotnet watch` khi phát triển để cập nhật thay đổi giao diện.

## Story #S1-02 — Auth & Session (Đăng nhập, Khoá tài khoản, Đăng xuất)

### Tính năng
- **Đăng nhập 1 ô**: Nhập số điện thoại (bắt đầu bằng 0, 10 chữ số) **hoặc** email. Hệ thống tự phát hiện loại tài khoản.
- **Khoá tài khoản**: Sau 5 lần đăng nhập sai trong 15 phút → khoá 15 phút. Hiển thị đếm ngược trên trang Login.
- **JWT Access Token** (mặc định 30 phút) + **Refresh Token** (7 ngày). Access token gửi qua header `Authorization: Bearer <token>`.
- **Đăng xuất**: Xoá refresh token trên server + blacklist access token. Client gọi `POST /api/auth/logout`.
- **Auto-refresh**: `auth-interceptor.js` tự làm mới access token khi hết hạn (401 → refresh → retry).

### API endpoints
| Method | Endpoint | Mô tả |
|--------|----------|-------|
| POST | `/api/auth/login` | Đăng nhập, nhận `{accessToken, refreshToken, ...}` |
| POST | `/api/auth/refresh` | Làm mới access token bằng refresh token |
| POST | `/api/auth/logout` | Đăng xuất, xoá token |
| POST | `/api/auth/me` | Lấy thông tin user từ JWT (cần Bearer token) |

### Request / Response mẫu
**Login request**
```json
{
  "TaiKhoanDangNhap": "0912345678",
  "MatKhau": "Passw0rd"
}
```

**Login success (200)**
```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "dGhpcyBpcyBh...",
  "accessTokenExpiresIn": 1800,
  "refreshTokenExpiresIn": 604800,
  "userId": "12",
  "hoTen": "Nguyễn Văn A",
  "vaiTro": "KHACH_THUE"
}
```

**Login failed (401)**
```json
{ "error": "LOGIN_FAILED", "message": "Tài khoản đã khoá. Vui lòng thử lại sau 12 phút", "code": 401 }
```

### Trang Login (`/Account/Login`)
- 1 ô nhập `TaiKhoanDangNhap` (sđt/email), 1 ô `MatKhau`.
- Hiển thị lỗi chung (`asp-validation-summary`).
- Nếu tài khoản bị khoá → hiện **alert vàng** với badge đếm ngược `mm:ss`.
- JS tự gọi `/api/auth/login`, lưu token vào `localStorage`, chuyển hướng về `/`.

### Cấu hình (appsettings.json)
```json
"JwtSettings": {
  "Issuer": "QL_PhongTro",
  "Audience": "QL_PhongTro_Client",
  "SecretKey": "thay-doi-secret-key-day-chieu-dai-32-ky-tu",
  "AccessTokenMinutes": 30,
  "RefreshTokenDays": 7
}
```

### Test nhanh
Khởi động server theo mục **Chạy dự án**, rồi mở terminal khác để thử đăng nhập:

```powershell
# Đăng nhập (PowerShell)
$body = @{ TaiKhoanDangNhap = '0912345678'; MatKhau = 'Passw0rd' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:5247/api/auth/login' -Body $body -ContentType 'application/json'
```

### Lưu ý triển khai
- `AuthService.LoginAsync` dùng **transaction** đảm bảo atomicity khi ghi refresh token.
- `SessionVersionStore` phiên bản hồ sơ để vô hiệu hóa token cũ khi đổi mật khẩu.
- `TokenBlacklistService` (in-memory) chặn access token đã logout cho đến khi hết hạn tự nhiên.
- Đảm bảo `JwtSettings.SecretKey` đủ dài (≥32 ký tự) và khác nhau giữa môi trường dev/staging/prod.

### ⚠️ Lưu ý build Release
Build Debug ở trên phù hợp để chạy và báo cáo local. Xử lý ảnh dùng `SkiaSharp` 4.153.1 (MIT), không cần đăng ký hoặc cấu hình khóa license khi build Release. Gói `SkiaSharp.NativeAssets.Linux.NoDependencies` cùng phiên bản phục vụ container Linux; bản quyền và thông báo thư viện kèm trong `QL_PhongTro/ThirdPartyNotices` và gói publish. Bản online hiện tại: [NhàTốt](https://nhatot-buk7.onrender.com), dùng Render Free và Neon Free. Xem [hướng dẫn triển khai Render/Neon](docs/trien-khai-render-neon.md); `docs/trien-khai-online.md` lưu phương án Railway trước đó.

## Nhật ký hoạt động S1-10

ADMIN xem tại `/NhatKy`. Mã hiện tại yêu cầu schema nhật ký v3; kiểm tra schema của đúng database trước khi chạy và chỉ nâng cấp DB đang dùng khi được yêu cầu. Xem mục **Nhật ký hoạt động (S1-10)** trong [bàn giao dự án](docs/tien-do.md).
### S2-07 — Danh sách yêu cầu

Schema v11 thêm bảng `yeu_cau`; schema v13 cập nhật quyền mặc định của Quản lý đối với tin đăng từ `READ` lên `WRITE`. Với database đã có dữ liệu, dừng web rồi chạy `--update-database` và `--check-database` theo quy trình bên trên; updater sẽ sao lưu trước và giữ nguyên dữ liệu cũ. Database mới được tạo bằng `--initialize-database` sẽ có schema nền v22 hiện hành nhưng không tự nạp dữ liệu cá nhân hay dữ liệu demo.

Màn hình danh sách của chủ nhà: `/YeuCau`. Dữ liệu mẫu giả để demo nằm tại `docs/sql/S2-07-yeu-cau.sql`; chỉ chạy trên database demo/bản sao đã có tối thiểu ba khách và ba phòng, không chạy trên database local đang sử dụng.

Database local đã bỏ theo dõi Git. **Sao lưu DB riêng ra ngoài repository trước lần pull nhận thay đổi này**, vì Git có thể xóa file từng theo dõi. Không chép đè DB đang sử dụng.

Dùng đúng `DatabasePath` trong cùng terminal; mặc định `QL_PhongTro/Data/local-dev.sqlite`. Web không tự tạo database thiếu, không tự cài bảng tin. Schema v9 chỉ thêm bảng tin/index/FK; không tự tạo tin, tài khoản hoặc mật khẩu.

### S3-07 — Chi tiết hóa đơn cho khách thuê

Quy ước PO, đường dẫn mở trực tiếp theo mã hóa đơn, trạng thái giao diện và demo hóa đơn đã phát hành được ghi tại [hướng dẫn S3-07](docs/s307-hoa-don-khach-thue.md). Chạy `.\verification\Start-S308Demo.ps1` để chuẩn bị bản sao demo; không dùng database local đang sử dụng.

**Máy mới chưa có database:** chọn đường dẫn mới, rồi khởi tạo (lệnh từ chối file đã tồn tại):

```powershell
$env:DatabasePath = Join-Path (Get-Location) 'data/my-local.sqlite'
dotnet run --project QL_PhongTro -- --initialize-database
dotnet run --project QL_PhongTro -- --check-database
```

ADMIN dùng cấu hình riêng và `--create-local-admin` theo hướng dẫn phía trên; không đưa credential lên Git.

**Máy đã có database:** dừng app dùng file đó, giữ dữ liệu riêng, chọn đúng DatabasePath rồi chạy:

```powershell
dotnet run --project QL_PhongTro -- --update-database
dotnet run --project QL_PhongTro -- --check-database
dotnet run --project QL_PhongTro --launch-profile http
```

Updater tạo backup cạnh DB trước ghi, thêm v6 trong transaction và kiểm tra integrity/FK. Nếu đã có bảng `tin_dang` chưa được quản lý phiên bản, updater từ chối để kiểm tra thủ công; không xóa bảng/chạy initializer để thay DB cũ. Giữ backup ngoài Git. Chỉ nâng cấp trên bản sao khi kiểm thử.

Mở `/TimTin`, kết hợp quận/huyện, giá thuê (VND nguyên), diện tích (m², nhập `20.5` cho 20,5 m²) và số người ở tối đa. Khoảng bao gồm cả hai biên; bỏ trống bỏ qua điều kiện. Số người lọc đúng sức chứa đã chọn. Form giữ điều kiện, báo lỗi min > max. Chỉ hiển thị tin DANG_HIEN_THI còn hạn UTC; hạn NULL bị loại. Không chọn quận thì tìm mọi khu vực. Có sắp xếp và phân trang theo Task 3; chưa có gợi ý khi không có kết quả. DB thiếu bảng tin báo chưa sẵn sàng.

**Kiểm thử và demo dữ liệu giả riêng** (Python 3, không cần package Python):

```powershell
dotnet restore QL_PhongTro/QL_PhongTro.csproj --source https://api.nuget.org/v3/index.json
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/task1-build/runtime
python verification/timtin_http.py --serve
```

Script tạo DB mới/schema hiện hành và tin giả, chạy 25 ca HTTP; nâng cấp chỉ trên bản sao read-only backup của DB nguồn và đối chiếu bảo toàn dữ liệu/hash. Nguồn mặc định là local-dev.sqlite; chọn nguồn khác bằng `TIMTIN_SOURCE_DATABASE` (không đổi DB nguồn). Không đưa dữ liệu nguồn vào demo. Nếu chưa có DB nguồn, dùng DB riêng đã khởi tạo làm nguồn. Script in URL/PID, giữ demo chạy với `--serve`; bỏ cờ này thì dừng sau kiểm thử. Trên Windows server nền chạy ẩn. `data/timtin-demo/latest.txt` trỏ thư mục kết quả có `result.json` và log. Xác nhận PID đúng trước khi `Stop-Process -Id <PID>`; không dừng các server khác. Nếu sandbox chặn Event Log/Data Protection, chạy từ terminal Windows bình thường.

Chọn Quận 2, giá 2000000..2000000, diện tích 20.5..20.5, sức chứa 3 → chỉ TIN-C. Demo public không cần đăng nhập; không có tài khoản dùng được hoặc mật khẩu cố định. Không seed dữ liệu giả vào DB đang sử dụng.

Task 3: chọn Mới đăng nhất (mặc định, ngày đăng NULL xếp cuối), Giá tăng dần hoặc Giá giảm dần rồi bấm Tìm kiếm. Mỗi trang tối đa 12 tin; tổng kết quả/tổng trang hiện phía trên. Link chuyển trang giữ lọc và sắp xếp, tìm lại từ form luôn về trang 1. Trùng ngày/giá dùng ID giảm dần. Không đổi schema/database cho Task 3.

Kiểm thử/demo Task 3 với 25 tin giả Quận 3 (3 trang: 12/12/1):

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/task1-build/task3-runtime
python verification/timtin_http.py --runtime data/task1-build/task3-runtime/QL_PhongTro.dll --task3 --serve
```

Script cũng chạy lại 25 ca Task 1/2; chỉ dùng DB mới/bản sao, không ghi database nguồn. URL và PID in sau khi PASS; bỏ --serve để tự dừng server sau kiểm thử. Chưa xác nhận hiệu năng dưới 2 giây hoặc UI bằng trình duyệt đồ họa.

### Chạy thử tìm tin — đầy đủ 5 tiêu chí

Từ gốc repository, tạo demo mới 500 tin giả bằng các lệnh:

```powershell
dotnet restore QL_PhongTro/QL_PhongTro.csproj --source https://api.nuget.org/v3/index.json
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/task1-build/final-search-runtime
python verification/timtin_performance.py --runtime data/task1-build/final-search-runtime/QL_PhongTro.dll --serve
```

Python 3 không cần package ngoài cho HTTP/performance. Script in URL/PID sau PASS, giữ server nền ẩn trên Windows; mở URL in ra, không cần đăng nhập. Không dùng database thật: mỗi lần tạo thư mục UUID/file mới, schema hiện hành qua initializer, 500 tin giả ở 5 quận (300 đang hiển thị còn hạn, còn lại ẩn/nháp/đã thuê/hết hạn). Không có mật khẩu cố định/dữ liệu cá nhân. Artifact và backup trong data/timtin-demo đã ignore.

1. Không lọc: 300 kết quả hợp lệ, 25 trang ×12 tin.
2. Chọn Quận 1, giá 1500000..3000000, diện tích 15..25, sức chứa 3: 6 tin khớp tất cả điều kiện.
3. Xóa bớt điều kiện để có >12 tin, thử ba sắp xếp và Trang sau/Trang trước; giữ lọc/sắp xếp. Bấm Tìm kiếm sau đổi điều kiện về trang đầu.
4. Xóa giá tối thiểu, nhập giá tối đa 1: 0 tin và gợi ý nới rộng khoảng giá. Khoảng min > max vẫn báo lỗi riêng.
5. DevTools Network throttling để quan sát Đang tải kết quả khi tìm/chuyển trang; trở lại trang không mắc spinner.

Bản sửa chuẩn bị truy vấn/Razor lúc startup trước khi nhận request (startup khoảng 3.19s, không phải thời gian tìm kiếm). Không cache dữ liệu kết quả. Đo hai lần khởi động 02/10/2026: request đầu 1.014s/0.223s, 260 lượt sau tối đa 0.241s, đạt <2s trên HTTP localhost tuần tự/500 tin. Report ghi riêng startup và đầy đủ samples/median/p95/max; không gồm browser render/static assets, không phải cam kết nhiều người dùng. Các lần FAIL của mã cũ được giữ trong artifact.

`--repeats` đổi số lượt mỗi kịch bản (mặc định 10); bỏ --serve sẽ tự dừng server sau đo. latest-performance.txt trỏ demo PASS có --serve; latest-performance-test.txt trỏ lần test tự dừng. performance.json ghi số liệu; browser.json/ảnh ghi xác minh Chrome. Chỉ dừng đúng PID đã in bằng Stop-Process, không dừng server khác. DB thật vẫn dùng quy trình máy mới/máy có DB phía trên; lần sửa này không đổi schema.

Kiểm thử chức năng/Chrome (chạy sau khi đo performance xong):

```powershell
python verification/timtin_http.py --runtime data/task1-build/final-search-runtime/QL_PhongTro.dll --task3
python -m pip install --target data/task1-build/browser-packages playwright
python verification/timtin_browser.py
```

Browser script dùng Chrome có sẵn, demo --serve từ latest-performance.txt và package Playwright trong thư mục đã ignore; không cần tải browser riêng. Chrome đã xác minh spinner/gợi ý/form sai/Back. HTTP regression 50 ca PASS, kiểm tra cả nguồn DB chỉ đọc/nâng cấp trên bản sao và bảo toàn dữ liệu. Windows sandbox có thể chặn Event Log/Data Protection, khi đó chạy terminal Windows bình thường.

### Task 5 — khoảng giá đề xuất và tìm lại

Khi không có kết quả, khoảng giá đề xuất nới mỗi đầu đã nhập 500.000đ (min không âm, max không tràn); đầu trống tiếp tục không giới hạn. Bấm **Áp dụng khoảng giá gợi ý** để tìm lại ở trang 1, giữ khu vực/diện tích/sức chứa/sắp xếp. Có kết quả thì gợi ý ẩn. Nếu giá đã không giới hạn hoặc không thể nới thêm, hiển thị giải thích và đề nghị chỉnh bộ lọc khác; không giả lập khoảng rộng hơn. Nới giá không bảo đảm có tin nếu các điều kiện khác vẫn loại hết.

Demo mới/data giả riêng:

```powershell
dotnet build QL_PhongTro/QL_PhongTro.csproj --no-restore -o data/task1-build/task5-runtime
python verification/timtin_performance.py --runtime data/task1-build/task5-runtime/QL_PhongTro.dll --serve
```

Mở URL in ra; Quận 1, giá 2100000..2200000, diện tích15..25, sức chứa3 → 0 tin và đề xuất1600000..2700000; bấm áp dụng để tìm thấy tin. Không cần đăng nhập. DB thật không được ghi; task không đổi schema/quy trình khởi tạo/nâng cấp cho máy mới/máy cũ.

Kiểm thử:

```powershell
python verification/timtin_http.py --runtime data/task1-build/task5-runtime/QL_PhongTro.dll --task3 --task5
python verification/timtin_browser.py
```

Browser cần Chrome và package Playwright theo hướng dẫn trên; dùng URL của demo --serve mới nhất. HTTP 9 ca Task 5/50 ca Task 1–3 và thao tác áp dụng trên Chrome đã PASS; báo cáo/ảnh trong thư mục demo đã ignore. Chỉ dừng đúng PID được in sau khi thử xong.

Sau merge S2-04 vào dev: dùng schema v9 và màn hình điện/nước S2-10 của dev; không dùng các demo utilities_http/utilities_browser của nhánh tìm kiếm cũ. Những DB v6 tạo từ nhánh tìm kiếm riêng có chuỗi phiên bản khác dev: sao lưu ngoài repository, kiểm tra trên bản sao và rà soát schema trước khi nâng; không sửa app_schema_version để bỏ qua kiểm tra. Lần merge không ghi hoặc nâng database đang sử dụng.
