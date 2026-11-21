# Test case UC06 Quản lý độc giả

Chưa code. Code dự kiến: `ReadersController` (`Index`, `Create`, `Edit`, `Lock`, `Unlock`, `ExtendCard`, `ResetPassword`), `ReaderService`.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC06-01 | Chính | Xem danh sách | Đăng nhập `admin` → "Độc giả" | Bảng gồm mã thẻ, họ tên, SĐT, ngày hết hạn, trạng thái thẻ (badge) | | Chưa thử | |
| TC06-02 | Chính | Thêm độc giả hợp lệ | Họ tên, ngày sinh, CCCD mới, SĐT, email đúng, địa chỉ | Tạo `readers` với mã thẻ mới (`DG000003`), ngày cấp hôm nay, hết hạn sau 1 năm, *Hoạt động*; tạo `users` role Reader, tên đăng nhập = mã thẻ; màn hình hiện tài khoản vừa cấp; đăng nhập được bằng mật khẩu mặc định | | Chưa thử | |
| TC06-03 | 6a | Bỏ trống trường bắt buộc | Bỏ trống họ tên / CCCD / SĐT | Báo lỗi dưới từng ô, không tạo dữ liệu | | Chưa thử | |
| TC06-04 | 6a | Email sai định dạng | Email `abc` | Báo lỗi dưới ô email | | Chưa thử | |
| TC06-05 | 6a | CCCD đã tồn tại | CCCD trùng với `DG000001` | Báo lỗi dưới ô CCCD, không tạo độc giả, **không** tạo tài khoản, bộ đếm mã thẻ không bị nhảy số vô ích (hoặc ghi chú nếu có nhảy) | | Chưa thử | |
| TC06-06 | 3a | Sửa thông tin | Sửa SĐT của `DG000001` | Cập nhật thành công; mã thẻ, ngày cấp, hạn thẻ không đổi | | Chưa thử | |
| TC06-07 | 3a, 6a | Sửa thành CCCD của người khác | Đổi CCCD `DG000001` thành CCCD của `DG000002` | Báo lỗi dưới ô CCCD | | Chưa thử | |
| TC06-08 | 3b | Khóa thẻ kèm lý do | Khóa `DG000002`, lý do "Làm mất sách" | Trạng thái *Bị khóa*, lưu `lockReason`; đăng nhập `DG000002` thấy thanh cảnh báo (liên quan TC01-07) | | Chưa thử | |
| TC06-09 | 3c | Mở khóa thẻ | Mở khóa `DG000002` | Trạng thái *Hoạt động*, `lockReason` xóa | | Chưa thử | |
| TC06-10 | 3d | Gia hạn thẻ | Gia hạn `DG000001` | Ngày hết hạn cộng thêm 1 năm (`CardValidYears`) | | Chưa thử | |
| TC06-11 | 3d | Gia hạn thẻ đã hết hạn | Thẻ hết hạn 02/10/2026, gia hạn | Hạn mới tính theo quy định đã chốt trong tài liệu (cộng 1 năm vào ngày hết hạn) và badge không còn *Hết hạn* nếu hạn mới sau hôm nay | | Chưa thử | |
| TC06-12 | 3e | Đặt lại mật khẩu | Đặt lại cho `DG000001` | Mật khẩu về mặc định; đăng nhập bằng mật khẩu mặc định được | | Chưa thử | |
| TC06-13 | Phân quyền | Độc giả mở trang quản lý độc giả | Đăng nhập `DG000001`, gõ `/Readers` | Chuyển tới trang "Không có quyền truy cập" | | Chưa thử | |
