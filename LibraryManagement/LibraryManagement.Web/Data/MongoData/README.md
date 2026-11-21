# Dữ liệu MongoDB mẫu

Bản xuất database `library_management` để mọi người có **cùng một bộ dữ liệu**, giữ nguyên `_id`. Mỗi collection một file JSON, dạng Extended JSON canonical (mỗi dòng một document), nên giữ đúng kiểu `ObjectId`, ngày giờ và số `long`.

Xuất ngày 21/11/2026 từ seed tối thiểu:

| Collection | Số document | Nội dung |
|---|---|---|
| `users` | 3 | `admin` (Thủ thư, mật khẩu `admin123`), `DG000001`, `DG000002` (Độc giả, mật khẩu = `LibraryRules.DefaultPassword`) |
| `readers` | 2 | Nguyễn Văn An, Trần Thị Bình; thẻ *Hoạt động*, hết hạn 20/11/2027 |
| `categories` | 2 | Kỹ năng sống, Văn học |
| `books` | 5 | Đắc Nhân Tâm, Nhà Giả Kim, Tuổi Trẻ Đáng Giá Bao Nhiêu, Dế Mèn Phiêu Lưu Ký, Cho Tôi Xin Một Vé Đi Tuổi Thơ |
| `counters` | 1 | Bộ đếm mã thẻ đang ở 2 |
| `loans`, `reservations` | 0 | File rỗng |

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

Ai sửa dữ liệu mẫu (ví dụ B làm seed đầy đủ) thì xuất lại rồi commit các file `.json`:

```powershell
cd LibraryManagement/LibraryManagement.Web/Data/MongoData
mongosh "mongodb://localhost:27017/library_management?replicaSet=rs0" --file export.js
```

Ghi lại ngày xuất và cập nhật bảng ở đầu file này.
