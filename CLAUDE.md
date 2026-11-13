# CLAUDE.md

Đồ án môn NoSQL: **Hệ thống quản lý mượn trả sách**, dùng ASP.NET Core MVC (C#) + MongoDB.
Giao tiếp với người dùng bằng tiếng Việt.

## ⚠️ Việc cần làm trước khi code (nhắc người dùng ở đầu mỗi phiên cho đến khi xong)

Kiểm tra lại bằng lệnh trước khi nhắc. Khi một mục đã xong, đánh dấu `[x]` và ghi ngày.

- [ ] **Cài .NET 10 SDK.** Ngày 13/11/2026 máy chỉ có SDK 8.0.125 và 9.0.304, cả hai hết hỗ trợ từ 10/11/2026. Tải tại https://dotnet.microsoft.com/download/dotnet/10.0 (máy không có winget). Kiểm tra: `dotnet --list-sdks`.
- [ ] **Bật service MongoDB.** MongoDB 8.3 đã cài tại `C:\Program Files\MongoDB\Server\8.3`, service `MongoDB` đang Stopped. Bật: `Start-Service MongoDB` (PowerShell quyền admin) hoặc mở services.msc. Nên đặt Startup type = Automatic.
- [ ] **Cấu hình replica set 1 node** (bắt buộc để dùng transaction): thêm `replication: replSetName: rs0` vào `C:\Program Files\MongoDB\Server\8.3\bin\mongod.cfg`, khởi động lại service, chạy `rs.initiate()` trong mongosh. Chi tiết ở mục 7.1 của `LibraryManagement/LibraryManagement.Web/Doc/thiet-ke-ky-thuat.md`.
- [ ] **Cài mongosh và MongoDB Compass** nếu chưa có (`mongosh` chưa có trong PATH).
- [ ] **Tạo project** sau khi có .NET 10: chạy các lệnh ở mục 1 của `Doc/thiet-ke-ky-thuat.md` trong thư mục `LibraryManagement/`. Thư mục con (kể cả `Doc/`) đã được tạo sẵn, không xóa.

## Tài liệu (nguồn sự thật)

Nằm trong thư mục `LibraryManagement/LibraryManagement.Web/Doc/`:

| File | Nội dung |
|---|---|
| `quan-ly-muon-tra-sach.md` | Mô tả đề tài ban đầu. Phần database trong file này **đã cũ**, theo `thiet-ke-ky-thuat.md` |
| `dac-ta-use-case.md` | Quy định nghiệp vụ QĐ01–QĐ10, quy trình nghiệp vụ, đặc tả 16 use case UC01–UC16 |
| `thiet-ke-ky-thuat.md` | Công nghệ, cấu trúc thư mục, ánh xạ UC → Controller, schema MongoDB, index, transaction, aggregation, thứ tự triển khai |
| `phan-cong.md` | Phân công 2 người (A, B), file nào ai sửa, 5 hàm giao kèo giữa `LoanService` và `ReservationService`, quy tắc Git |

Ảnh mẫu bảng đặc tả use case (`Ảnh chụp màn hình 2026-11-13 160147.png`) nằm ở thư mục gốc.

Code phải khớp với các tài liệu này. Khi thay đổi nghiệp vụ hoặc schema, **cập nhật tài liệu trước**, rồi mới sửa code.

## Các quyết định đã chốt

- Không có chức năng tự đăng ký: tài khoản độc giả do Thủ thư cấp (UC06).
- Gia hạn online do Thủ thư duyệt (UC09, UC15).
- Đặt trước: chỉ đặt khi sách đang hết; **không giữ sách riêng**; 5 trạng thái `Pending, Approved, Rejected, Completed, Cancelled`. Khi độc giả mượn đúng sách đã đặt, yêu cầu tự chuyển `Completed`.
- Cho phép trả sách từng phần trong một phiếu.
- **Không dùng Area.** Controller hai vai trò nằm chung `Controllers/`, phân quyền bằng `[Authorize(Roles = ...)]`. Controller của độc giả: `Catalog`, `MyLoans`, `MyReservations`.

## Quy ước code

- Controller → Service → MongoDB. Controller không dùng trực tiếp `IMongoCollection`.
- Quy định nghiệp vụ (số sách tối đa, số ngày mượn, mức phạt...) đọc từ `LibraryRules` trong `appsettings.json`, không viết cứng.
- MongoDB: field camelCase, enum lưu dạng string (đăng ký trong `MongoConventions`). Lưu thời gian dạng UTC, tính ngày theo giờ Việt Nam qua một hàm dùng chung.
- Trạng thái "quá hạn" và "thẻ hết hạn" tính khi truy vấn, không lưu trong database.
- Lập phiếu mượn và nhận trả sách chạy trong transaction. Trừ số lượng sách bằng update có điều kiện `availableQuantity > 0`.
- Tên class, property tiếng Anh PascalCase; giao diện và thông báo tiếng Việt.
- Ghi mã UC (ví dụ `// UC07`) ở đầu các action và method service tương ứng.

## Phạm vi công việc

Người dùng muốn đi từng bước. Chỉ tạo hoặc viết code khi được yêu cầu rõ ràng; khi được bảo "chỉ tạo thư mục" thì không tạo file code.
