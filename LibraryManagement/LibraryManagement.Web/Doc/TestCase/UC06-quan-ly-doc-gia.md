# Test case UC06 Quản lý độc giả

Code: `ReadersController` (`Index`, `Details`, `Create`, `Edit`, `Lock`, `Unlock`, `ExtendCard`, `ResetPassword`), `ReaderService`, `ViewModels/Readers/ReaderVMs.cs`, `Views/Readers/`. Các thao tác khóa, mở khóa, gia hạn thẻ, đặt lại mật khẩu nằm ở trang chi tiết độc giả.

Dữ liệu: seed đầy đủ của B (10 độc giả, `DG000009` bị khóa, `DG000010` hết hạn 22/11/2025).

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC06-01 | Chính | Xem danh sách | Đăng nhập `admin` → "Độc giả" | Bảng gồm mã thẻ, họ tên, SĐT, ngày hết hạn, trạng thái thẻ (badge) | 10 dòng; badge 8 *Hoạt động*, 1 *Bị khóa* (`DG000009`), 1 *Hết hạn* (`DG000010`). Lọc "Hết hạn", "Bị khóa" và tìm theo tên "bình" ra đúng | Đạt | 22/11/2026 |
| TC06-02 | Chính | Thêm độc giả hợp lệ | Họ tên, ngày sinh, CCCD mới, SĐT, email đúng, địa chỉ | Tạo `readers` với mã thẻ mới, ngày cấp hôm nay, hết hạn sau 1 năm, *Hoạt động*; tạo `users` role Reader, tên đăng nhập = mã thẻ; màn hình hiện tài khoản vừa cấp; đăng nhập được bằng mật khẩu mặc định | Tạo `DG000011`: cấp 22/11/2026, hết hạn 22/11/2027, *Hoạt động*; `users` `DG000011` role Reader; trang chi tiết hiện khung "Tài khoản vừa cấp" (chỉ một lần, tải lại thì ẩn); đăng nhập `DG000011` / `123456` được | Đạt | 22/11/2026 |
| TC06-03 | 6a | Bỏ trống trường bắt buộc | Bỏ trống họ tên / CCCD / SĐT | Báo lỗi dưới từng ô, không tạo dữ liệu | Báo đủ 3 lỗi dưới 3 ô | Đạt | 22/11/2026 |
| TC06-04 | 6a | Email sai định dạng | Email `abc` | Báo lỗi dưới ô email | "Email không đúng định dạng." | Đạt | 22/11/2026 |
| TC06-05 | 6a | CCCD đã tồn tại | CCCD trùng với `DG000001` | Báo lỗi dưới ô CCCD, không tạo độc giả, **không** tạo tài khoản, bộ đếm mã thẻ không bị nhảy số vô ích (hoặc ghi chú nếu có nhảy) | "Số CCCD này đã được dùng cho độc giả khác." dưới ô CCCD; số độc giả, tài khoản và bộ đếm giữ nguyên | Đạt | 22/11/2026 |
| TC06-06 | 3a | Sửa thông tin | Sửa SĐT của `DG000001` | Cập nhật thành công; mã thẻ, ngày cấp, hạn thẻ không đổi | SĐT đổi; mã thẻ, ngày cấp, hạn thẻ giữ nguyên. Sửa họ tên thì bản sao trong `users` cũng đổi | Đạt | 22/11/2026 |
| TC06-07 | 3a, 6a | Sửa thành CCCD của người khác | Đổi CCCD `DG000001` thành CCCD của `DG000002` | Báo lỗi dưới ô CCCD | Báo lỗi dưới ô CCCD, không lưu | Đạt | 22/11/2026 |
| TC06-08 | 3b | Khóa thẻ kèm lý do | Khóa `DG000002`, lý do "Làm mất sách" | Trạng thái *Bị khóa*, lưu `lockReason`; đăng nhập `DG000002` thấy thanh cảnh báo (liên quan TC01-07) | `cardStatus` Locked, `lockReason` "Làm mất sách"; thanh cảnh báo hiện đúng lý do. Để trống lý do thì báo "Vui lòng nhập lý do khóa thẻ." | Đạt | 22/11/2026 |
| TC06-09 | 3c | Mở khóa thẻ | Mở khóa `DG000002` | Trạng thái *Hoạt động*, `lockReason` xóa | `Active`, `lockReason` null | Đạt | 22/11/2026 |
| TC06-10 | 3d | Gia hạn thẻ | Gia hạn `DG000001` | Ngày hết hạn cộng thêm 1 năm (`CardValidYears`) | 22/09/2027 → 22/09/2028 | Đạt | 22/11/2026 |
| TC06-11 | 3d | Gia hạn thẻ đã hết hạn | Thẻ `DG000010` hết hạn 22/11/2025, gia hạn | Hạn mới tính theo quy định đã chốt trong tài liệu (cộng 1 năm vào ngày hết hạn) và badge không còn *Hết hạn* nếu hạn mới sau hôm nay | Hạn mới 22/11/2026 (= hôm nay, còn hiệu lực), badge *Hoạt động*. Nếu hạn mới vẫn trước hôm nay, thông báo nhắc gia hạn thêm | Đạt | 22/11/2026 |
| TC06-12 | 3e | Đặt lại mật khẩu | Đổi tạm mật khẩu `DG000001`, rồi đặt lại | Mật khẩu về mặc định; đăng nhập bằng mật khẩu mặc định được | Trước khi đặt lại không đăng nhập được bằng `123456`, sau khi đặt lại thì được | Đạt | 22/11/2026 |
| TC06-13 | Phân quyền | Độc giả mở trang quản lý độc giả | Đăng nhập `DG000001`, gõ `/Readers` | Chuyển tới trang "Không có quyền truy cập" | Chuyển tới `/Account/AccessDenied` (cả `/Readers/Details/...`) | Đạt | 22/11/2026 |

## Ghi chú

- Thêm ngoài danh sách (22/11/2026, đều Đạt): CCCD không đủ 12 số, SĐT sai định dạng, ngày sinh ở tương lai đều báo lỗi đúng ô; id sai định dạng trên URL (`/Readers/Details/abc`) quay về danh sách, không lỗi 500.
