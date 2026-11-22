# Dữ liệu MongoDB mẫu

Bản xuất database `library_management` để mọi người có **cùng một bộ dữ liệu**, giữ nguyên `_id`. Mỗi collection một file JSON, dạng Extended JSON canonical (mỗi dòng một document), nên giữ đúng kiểu `ObjectId`, ngày giờ và số `long`.

Xuất ngày 22/11/2026 từ seed đầy đủ (Member B hoàn tất Đợt 4), xuất lại cùng ngày sau khi A sửa seed: file không còn BOM (trước đó `import.js` báo `SyntaxError: Unexpected token`), số còn lại của sách còn hàng khớp với phiếu đang mượn. 3 sách cố ý để hết hàng (số còn lại 0) để thử đặt trước:

| Collection | Số document | Nội dung |
|---|---|---|
| `users` | 11 | `admin` (Thủ thư, mật khẩu `admin123`), 10 tài khoản Độc giả `DG000001` - `DG000010` (mật khẩu = `123456`) |
| `readers` | 10 | 10 độc giả đầy đủ họ tên, CCCD, SĐT, trong đó có thẻ Hoạt động, thẻ Khóa (`DG000009`), thẻ Hết hạn (`DG000010`) |
| `categories` | 6 | 6 thể loại phong phú (CNTT, Kỹ năng sống, Văn học, Kinh tế, Khoa học, Lịch sử) |
| `books` | 28 | 28 đầu sách thực tế, gồm sách còn và sách hết hàng để thử nghiệm đặt trước |
| `counters` | 3 | Bộ đếm mã thẻ (`DG`), mã phiếu mượn (`PM`), v.v. |
| `loans` | 9 | Phiếu mượn trải đều các tháng: đúng hạn, quá hạn, đã trả có phạt trễ hạn, phạt hư hỏng, phạt mất sách |
| `reservations` | 2 | Yêu cầu đặt trước mẫu ở các trạng thái Chờ duyệt, Đã duyệt có sách về kho |

## Nhập vào máy (cách nhanh, dùng mongosh)

Yêu cầu: MongoDB đang chạy replica set `rs0` (mục 7.1 `Doc/thiet-ke-ky-thuat.md`), có `mongosh`.

```powershell
cd LibraryManagement/LibraryManagement.Web/Data/MongoData
mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file import.js
```

- `import.js` **xóa sạch** dữ liệu cũ của từng collection có file `.json` rồi nhập lại. Chạy lại bao nhiêu lần cũng ra cùng kết quả.
- File không chứa index. Chạy ứng dụng một lần (`dotnet run --launch-profile http`), `DbInitializer` sẽ tự tạo đủ index. Vì `users` đã có dữ liệu nên `DbInitializer` không seed thêm.

## Cách khác

- **MongoDB Compass:** mở database `library_management` → từng collection → *Add Data* → *Import JSON or CSV file* → chọn file `.json` cùng tên.
- **mongoimport** (nếu có cài MongoDB Database Tools), cho từng file:
  `mongoimport --uri "mongodb://localhost:27017/library_management?replicaSet=rs0" --collection books --drop --file books.json`

## Xuất lại khi dữ liệu thay đổi

`DbInitializer` chỉ tự xuất các file này **một lần, ngay khi seed database mới** (không còn ghi đè mỗi lần khởi động). Muốn xuất dữ liệu hiện tại thì chạy `export.js` như dưới.


Ai sửa dữ liệu mẫu (ví dụ B làm seed đầy đủ) thì xuất lại rồi commit các file `.json`:

```powershell
cd LibraryManagement/LibraryManagement.Web/Data/MongoData
mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file export.js
```

Ghi lại ngày xuất và cập nhật bảng ở đầu file này.
