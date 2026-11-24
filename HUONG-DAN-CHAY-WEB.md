# Hướng dẫn cài đặt và chạy web Quản lý mượn trả sách (dành cho AI thực hiện)

> **Dành cho trợ lý AI** (Claude Code, Antigravity, Copilot...) đang chạy trên máy của người dùng.
> Làm lần lượt từng bước. Mỗi bước có lệnh **Kiểm tra**: chỉ sang bước sau khi kiểm tra đạt.
> Gặp tình huống ghi **DỪNG** thì hỏi người dùng, không tự xử lý.
> Lệnh viết cho **Windows PowerShell**. Máy macOS / Linux dùng phương án Docker ở bước 3B, các lệnh khác giữ nguyên ý.

## Mục tiêu

Chạy được web ASP.NET Core MVC (.NET 10) + MongoDB tại **http://localhost:5101**, có sẵn dữ liệu mẫu (28 sách, 10 độc giả, phiếu mượn, đặt trước), đăng nhập được bằng tài khoản Thủ thư và Độc giả.

## Quy tắc cho AI

- **Không** commit, push, đổi nhánh hay sửa code của dự án, trừ khi người dùng yêu cầu.
- **Không** xóa hay ghi đè database MongoDB nào khác ngoài `library_management`. Nếu `library_management` **đã có dữ liệu**, **DỪNG** và hỏi người dùng trước khi chạy `import.js` (lệnh này xóa dữ liệu cũ của các collection rồi nhập lại).
- Các bước cần quyền Administrator (cài phần mềm, sửa `mongod.cfg`, khởi động lại service, mở firewall): báo người dùng và để họ xác nhận.
- Xong thì báo lại: phiên bản đã cài, các bước đã làm, kết quả từng lệnh kiểm tra, và địa chỉ web.

---

## Bước 1. Kiểm tra công cụ có sẵn

```powershell
git --version
dotnet --list-sdks          # cần một dòng bắt đầu bằng 10.
mongosh --version           # MongoDB Shell
Get-Service MongoDB -ErrorAction SilentlyContinue   # MongoDB cài dạng service Windows
docker --version            # chỉ cần nếu dùng phương án 3B
```

Ghi lại cái nào đã có, cái nào thiếu, rồi chỉ cài phần thiếu ở bước 2 và 3.

## Bước 2. Cài .NET 10 SDK (nếu thiếu)

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
```

Mở cửa sổ PowerShell **mới** (để nhận PATH mới).
**Kiểm tra:** `dotnet --list-sdks` có dòng `10.x.x`.

## Bước 3. Cài MongoDB chạy chế độ replica set `rs0`

App dùng **transaction** (lập phiếu mượn, nhận trả sách), mà MongoDB chỉ cho transaction khi chạy **replica set**, kể cả khi chỉ có 1 máy. Chuỗi kết nối của app đã cố định `replicaSet=rs0`. Chọn **một** trong hai phương án.

### 3A. MongoDB cài trực tiếp trên Windows (khuyên dùng)

1. Cài MongoDB Server và mongosh (nếu thiếu, cần quyền Administrator):
   ```powershell
   winget install --id MongoDB.Server -e
   winget install --id MongoDB.Shell -e
   ```
   Mở PowerShell mới. Nếu vẫn không thấy `mongosh` thì nạp lại PATH:
   ```powershell
   $env:Path = [Environment]::GetEnvironmentVariable('Path','User') + ';' + [Environment]::GetEnvironmentVariable('Path','Machine')
   ```
2. Tìm file cấu hình: `C:\Program Files\MongoDB\Server\<phiên bản>\bin\mongod.cfg`. Sao lưu thành `mongod.cfg.bak`, rồi thêm vào cuối file (nếu chưa có mục `replication`):
   ```yaml
   replication:
     replSetName: rs0
   ```
   Nếu file đã có `replication` với tên khác `rs0`: **DỪNG**, hỏi người dùng.
3. Khởi động lại service (PowerShell chạy bằng quyền Administrator):
   ```powershell
   Restart-Service MongoDB
   ```
4. Khởi tạo replica set (chỉ làm một lần). Đặt host là `localhost` để app kết nối được ổn định:
   ```powershell
   mongosh --quiet --eval "rs.initiate({_id:'rs0', members:[{_id:0, host:'localhost:27017'}]})"
   ```
   Nếu báo `already initialized` thì bỏ qua.
   (Trong `--eval` chỉ dùng nháy đơn cho chuỗi JS: Windows PowerShell làm rơi dấu ngoặc kép lồng bên trong khi truyền cho chương trình ngoài.)

### 3B. MongoDB chạy bằng Docker (macOS / Linux, hoặc không muốn cài vào Windows)

```powershell
docker run -d --name library-mongo -p 27017:27017 mongo:8 --replSet rs0 --bind_ip_all
docker exec library-mongo mongosh --quiet --eval "rs.initiate({_id:'rs0', members:[{_id:0, host:'localhost:27017'}]})"
```

Vẫn cần cài `mongosh` trên máy (bước 3A.1, hoặc `brew install mongosh` trên macOS) để nhập dữ liệu ở bước 5.

**Kiểm tra (cả 3A và 3B):** chờ khoảng 5 giây rồi chạy:
```powershell
mongosh "mongodb://localhost:27017/?replicaSet=rs0" --quiet --eval 'db.hello().isWritablePrimary'
```
Kết quả phải là `true`.

## Bước 4. Lấy mã nguồn

```powershell
git clone https://github.com/Huyphung111/-Library_management_Nosql.git LibraryApp
cd LibraryApp
git checkout feature2
```

- Clone vào thư mục tên `LibraryApp`: tên repo bắt đầu bằng dấu `-` nên một số lệnh hiểu nhầm là tham số.
- Nhánh `feature2` là bản mới nhất tính đến 24/11/2026. `main` là bản nộp ổn định, có thể cũ hơn. Người dùng chỉ định nhánh khác thì dùng nhánh đó.
- Repo là private mà máy chưa có quyền truy cập: **DỪNG**, nhờ người dùng xin quyền từ chủ repo (`Huyphung111`).

**Kiểm tra:** có file `LibraryManagement\LibraryManagement.slnx` và thư mục `LibraryManagement\LibraryManagement.Web\Data\MongoData`.

## Bước 5. Nhập dữ liệu mẫu (làm TRƯỚC lần chạy app đầu tiên)

Trước hết kiểm tra database đã có dữ liệu chưa:
```powershell
mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --quiet --eval 'db.users.countDocuments()'
```
- Ra `0`: nhập dữ liệu như dưới đây.
- Ra số khác `0`: **DỪNG**, hỏi người dùng có muốn xóa để nhập lại dữ liệu mẫu không.

```powershell
cd LibraryManagement\LibraryManagement.Web\Data\MongoData
mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file import.js
cd ..\..\..\..
```

`import.js` đọc các file `.json` trong **thư mục hiện tại**, nên bắt buộc phải `cd` vào `Data\MongoData` trước khi chạy.

**Kiểm tra:** màn hình in ra từng collection, trong đó `books 28`, `readers 10`, `users 11`, và dòng cuối `Đã nhập 7 collection...`.

> Vì sao phải nhập trước? Nếu chạy app khi database còn trống, app sẽ tự sinh dữ liệu (với `_id` khác) **và ghi đè** các file trong `Data\MongoData`. Lỡ xảy ra thì chạy `git checkout -- LibraryManagement/LibraryManagement.Web/Data/MongoData/` để trả lại các file, rồi chạy `import.js` như trên để có đúng bộ dữ liệu chung.

## Bước 6. Chạy web

```powershell
cd LibraryManagement\LibraryManagement.Web
dotnet run --launch-profile http
```

- Lần đầu sẽ tải gói NuGet (MongoDB.Driver, BCrypt.Net-Next) nên hơi lâu.
- Khi khởi động, app tự tạo index và nạp **ảnh bìa** cho 28 sách mẫu vào GridFS (lấy từ `Data\SeedCovers`).
- Cửa sổ phải giữ chạy. Muốn chạy nền thì dùng tùy chọn chạy nền của công cụ AI, rồi kiểm tra bằng lệnh dưới.

**Kiểm tra** (ở cửa sổ khác):
```powershell
(Invoke-WebRequest http://localhost:5101/Account/Login -UseBasicParsing).StatusCode    # phải là 200
```

Báo người dùng mở trình duyệt vào **http://localhost:5101**.

## Bước 7. Tài khoản đăng nhập

| Vai trò | Tên đăng nhập | Mật khẩu | Ghi chú |
|---|---|---|---|
| Thủ thư | `admin` | `admin123` | Vào Dashboard, quản lý sách, độc giả, mượn trả, thống kê |
| Độc giả | `DG000001` … `DG000008` | `123456` | Thẻ đang hoạt động (gõ chữ thường `dg000001` cũng được) |
| Độc giả | `DG000009` | `123456` | Thẻ **bị khóa**: vào được nhưng không mượn, đặt sách được |
| Độc giả | `DG000010` | `123456` | Thẻ **hết hạn**: vào được nhưng không mượn, đặt sách được |

## Bước 8 (tùy chọn). Cho máy khác cùng mạng LAN truy cập

Chỉ làm khi người dùng yêu cầu.

1. Dừng app đang chạy (Ctrl+C), rồi chạy lại cho phép nghe mọi địa chỉ:
   ```powershell
   cd LibraryManagement\LibraryManagement.Web
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://0.0.0.0:5101"
   dotnet run --no-launch-profile
   ```
2. Mở cổng 5101 trên tường lửa (PowerShell quyền Administrator, người dùng xác nhận trước):
   ```powershell
   New-NetFirewallRule -DisplayName "LibraryManagement 5101" -Direction Inbound -Protocol TCP -LocalPort 5101 -Action Allow
   ```
3. Lấy IP máy chạy app: `(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like '192.168.*' -or $_.IPAddress -like '10.*' }).IPAddress`
4. Máy khác mở `http://<IP-đó>:5101`.

Dùng xong thì xóa luật tường lửa: `Remove-NetFirewallRule -DisplayName "LibraryManagement 5101"`.

---

## Xử lý sự cố

| Triệu chứng | Nguyên nhân / cách xử lý |
|---|---|
| `Transaction numbers are only allowed on a replica set member or mongos` | Chưa bật replica set. Làm lại bước 3 (thêm `replSetName: rs0`, khởi động lại, `rs.initiate`). |
| App treo rồi báo `A timeout occurred after 30000ms selecting a server` | MongoDB chưa chạy (`Get-Service MongoDB` / `docker ps`), hoặc replica set khởi tạo với tên máy không phân giải được. Kiểm tra `mongosh --eval 'rs.conf().members[0].host'`; nếu không phải `localhost:27017` thì **DỪNG**, hỏi người dùng trước khi cấu hình lại. |
| `dotnet` báo không có SDK `net10.0` | Chưa cài .NET 10 SDK (bước 2) hoặc chưa mở PowerShell mới. |
| `mongosh` không nhận lệnh trong Git Bash | Dùng PowerShell. Nếu vẫn không được thì nạp lại PATH như bước 3A.1. |
| `import.js` báo `Không thấy file .json nào` | Chưa `cd` vào `LibraryManagement\LibraryManagement.Web\Data\MongoData`. |
| Build báo không ghi được `LibraryManagement.Web.exe` (file đang bị dùng) | App cũ còn chạy: `Get-Process LibraryManagement.Web \| Stop-Process -Force` rồi chạy lại. |
| Cổng 5101 đã bị chiếm | Chạy cổng khác: `$env:ASPNETCORE_URLS = "http://localhost:5111"; dotnet run --no-launch-profile` (đặt thêm `$env:ASPNETCORE_ENVIRONMENT = "Development"`). |
| Sách không có ảnh bìa | Ảnh được nạp lúc khởi động từ `Data\SeedCovers`. Khởi động lại app một lần. |
| `git status` thấy các file `Data\MongoData\*.json` bị sửa | App đã tự xuất dữ liệu khi gặp database trống. Xem ghi chú cuối bước 5. Không commit các thay đổi này. |

## Tài liệu tham khảo trong repo

- `CLAUDE.md`: tổng quan kiến trúc, lệnh thường dùng, quy ước code.
- `LibraryManagement/LibraryManagement.Web/Data/MongoData/README.md`: chi tiết bộ dữ liệu mẫu, cách nhập và xuất lại.
- `LibraryManagement/LibraryManagement.Web/Doc/dac-ta-use-case.md`: quy định nghiệp vụ và 16 use case (để biết web làm được gì).
- `LibraryManagement/LibraryManagement.Web/Doc/thiet-ke-ky-thuat.md`: thiết kế MongoDB (mục 7.1: replica set và transaction).
