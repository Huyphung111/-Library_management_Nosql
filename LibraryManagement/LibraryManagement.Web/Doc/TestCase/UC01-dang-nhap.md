# Test case UC01 Đăng nhập

Code: `AccountController.Login`, `AuthService.LoginAsync`, `Views/Account/Login.cshtml`.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC01-01 | Chính | Thủ thư đăng nhập đúng | `admin` + mật khẩu đúng | Vào hệ thống, chuyển tới `/Dashboard` (trang chính Thủ thư), topbar ghi "Quản trị viên · Thủ thư", sidebar đủ menu Thủ thư | Chuyển tới `/Dashboard`, trang hiện đủ (B đã làm Dashboard); topbar "Quản trị viên Thư viện · Thủ thư" | Đạt | 22/11/2026 |
| TC01-02 | Chính | Độc giả đăng nhập đúng | `DG000001` + mật khẩu mặc định | Chuyển tới `/Catalog`, topbar ghi "Nguyễn Văn An · Độc giả", sidebar chỉ có 4 mục của độc giả | Chuyển tới `/Catalog`, trang hiện đủ; sidebar 4 mục | Đạt | 22/11/2026 |
| TC01-03 | 3a | Bỏ trống cả hai ô | Bấm "Đăng nhập" khi chưa nhập gì | Báo lỗi đỏ dưới từng ô, ở lại trang đăng nhập | "Vui lòng nhập tên đăng nhập." và "Vui lòng nhập mật khẩu." hiện dưới đúng ô | Đạt | 21/11/2026 |
| TC01-04 | 3a | Chỉ bỏ trống mật khẩu | Nhập `admin`, để trống mật khẩu | Chỉ báo lỗi dưới ô mật khẩu, giữ lại tên đăng nhập | Chỉ có "Vui lòng nhập mật khẩu.", ô tên đăng nhập giữ `admin` | Đạt | 22/11/2026 |
| TC01-05 | 4a | Sai mật khẩu | `admin` + mật khẩu sai | Báo "Tên đăng nhập hoặc mật khẩu không đúng.", giữ tên đăng nhập, xóa ô mật khẩu | Đúng như mong đợi | Đạt | 21/11/2026 |
| TC01-06 | 4a | Tên đăng nhập không tồn tại | `DG999999` + mật khẩu bất kỳ | Cùng câu báo như TC01-05 (không tiết lộ tên đăng nhập có tồn tại hay không) | Cùng câu báo như TC01-05 | Đạt | 22/11/2026 |
| TC01-07 | 5a | Thẻ bị khóa vẫn đăng nhập được | Seed đầy đủ có sẵn `DG000009` bị khóa. Đăng nhập `DG000009` | Vào được; dưới topbar có thanh vàng "Thẻ của bạn đang bị khóa (lý do: ...)..." | Vào được `/Catalog`, thanh cảnh báo "Thẻ của bạn đang bị khóa (lý do: Vi phạm quy định: làm mất sách chưa đền bù)..." | Đạt | 22/11/2026 |
| TC01-08 | 5a | Thẻ hết hạn vẫn đăng nhập được | Seed đầy đủ có sẵn `DG000010` hết hạn. Đăng nhập `DG000010` | Thanh vàng "Thẻ của bạn đã hết hạn ngày ..." | Thanh cảnh báo "Thẻ của bạn đã hết hạn ngày 22/11/2025..." | Đạt | 22/11/2026 |
| TC01-09 | Chính | Quay lại trang đang định mở | Chưa đăng nhập, mở `/Account/ChangePassword` → bị chuyển về đăng nhập → đăng nhập `admin` | Sau đăng nhập về thẳng `/Account/ChangePassword` | Bị chuyển về `/Account/Login?ReturnUrl=%2FAccount%2FChangePassword`; đăng nhập xong về `/Account/ChangePassword` | Đạt | 22/11/2026 |
| TC01-10 | Chính | Đã đăng nhập mà mở lại trang đăng nhập | Đang đăng nhập, gõ `/Account/Login` | Chuyển về trang chính theo vai trò, không hiện form | Chuyển về `/` rồi tới trang chính | Đạt | 22/11/2026 |
| TC01-11 | 4 | Mã thẻ gõ chữ thường / lẫn hoa thường (lỗi R05 báo cáo Antigravity) | `dg000001`; `  Dg000002 ` (có khoảng trắng) / mật khẩu đúng | Đăng nhập được như `DG000001` | Cả hai đăng nhập được (302 về trang chính). `ADMIN` vẫn bị từ chối (chỉ chuẩn hóa mã thẻ `DG`) | Đạt | 24/11/2026 |
| TC01-12 | 5a | Thẻ bị khóa / mở khóa khi độc giả đang đăng nhập (BUG-10) | `DG000005` đang đăng nhập; Thủ thư khóa thẻ (lý do "Thử BUG-10"), sau đó mở khóa | Thanh cảnh báo hiện / mất theo trạng thái thẻ trong vòng 1 phút, không cần đăng nhập lại | Ngay sau khi khóa: chưa hiện (đọc lại tối đa 1 lần/phút); sau 1 phút: hiện kèm lý do; mở khóa, sau 1 phút: mất; vẫn đăng nhập; Thủ thư không ảnh hưởng | Đạt | 24/11/2026 |

## Ghi chú

- 22/11/2026: thử lại với seed đầy đủ của B (database test riêng `library_management_test`) bằng HTTP (curl giữ cookie, gửi kèm token chống giả mạo), kiểm tra dữ liệu bằng mongosh.
