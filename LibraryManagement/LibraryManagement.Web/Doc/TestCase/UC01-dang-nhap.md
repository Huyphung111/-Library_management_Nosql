# Test case UC01 Đăng nhập

Code: `AccountController.Login`, `AuthService.LoginAsync`, `Views/Account/Login.cshtml`.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC01-01 | Chính | Thủ thư đăng nhập đúng | `admin` + mật khẩu đúng | Vào hệ thống, chuyển tới `/Dashboard` (trang chính Thủ thư), topbar ghi "Quản trị viên · Thủ thư", sidebar đủ menu Thủ thư | Chuyển tới `/Dashboard` đúng; trang báo 404 vì `DashboardController` là phần của B, chưa làm. Mở `/Account/ChangePassword` thấy layout và menu Thủ thư đúng | Đạt | 21/11/2026 |
| TC01-02 | Chính | Độc giả đăng nhập đúng | `DG000001` + mật khẩu mặc định | Chuyển tới `/Catalog`, topbar ghi "Nguyễn Văn An · Độc giả", sidebar chỉ có 4 mục của độc giả | Đúng; `/Catalog` báo 404 vì là phần của B | Đạt | 21/11/2026 |
| TC01-03 | 3a | Bỏ trống cả hai ô | Bấm "Đăng nhập" khi chưa nhập gì | Báo lỗi đỏ dưới từng ô, ở lại trang đăng nhập | "Vui lòng nhập tên đăng nhập." và "Vui lòng nhập mật khẩu." hiện dưới đúng ô | Đạt | 21/11/2026 |
| TC01-04 | 3a | Chỉ bỏ trống mật khẩu | Nhập `admin`, để trống mật khẩu | Chỉ báo lỗi dưới ô mật khẩu, giữ lại tên đăng nhập | | Chưa thử | |
| TC01-05 | 4a | Sai mật khẩu | `admin` + mật khẩu sai | Báo "Tên đăng nhập hoặc mật khẩu không đúng.", giữ tên đăng nhập, xóa ô mật khẩu | Đúng như mong đợi | Đạt | 21/11/2026 |
| TC01-06 | 4a | Tên đăng nhập không tồn tại | `DG999999` + mật khẩu bất kỳ | Cùng câu báo như TC01-05 (không tiết lộ tên đăng nhập có tồn tại hay không) | | Chưa thử | |
| TC01-07 | 5a | Thẻ bị khóa vẫn đăng nhập được | Khóa tạm thẻ: `db.readers.updateOne({cardNumber:"DG000002"},{$set:{cardStatus:"Locked",lockReason:"Test"}})`. Đăng nhập `DG000002` | Vào được; dưới topbar có thanh vàng "Thẻ của bạn đang bị khóa (lý do: Test)...". Thử xong mở lại: `$set:{cardStatus:"Active",lockReason:null}` | | Chưa thử | |
| TC01-08 | 5a | Thẻ hết hạn vẫn đăng nhập được | Lùi hạn thẻ về quá khứ: `$set:{expiryDate:ISODate("2026-10-01T17:00:00Z")}` (= 02/10/2026 giờ VN). Đăng nhập `DG000002` | Thanh vàng "Thẻ của bạn đã hết hạn ngày 02/10/2026...". Thử xong trả lại `ISODate("2027-11-19T17:00:00Z")` | | Chưa thử | |
| TC01-09 | Chính | Quay lại trang đang định mở | Chưa đăng nhập, mở `/Account/ChangePassword` → bị chuyển về đăng nhập → đăng nhập `admin` | Sau đăng nhập về thẳng `/Account/ChangePassword` | | Chưa thử | |
| TC01-10 | Chính | Đã đăng nhập mà mở lại trang đăng nhập | Đang đăng nhập, gõ `/Account/Login` | Chuyển về trang chính theo vai trò, không hiện form | | Chưa thử | |

## Ghi chú

- TC01-01, TC01-02: khi B làm xong `Dashboard` và `Catalog`, thử lại để xác nhận trang chính hiện đúng thay vì 404.
