# Hệ thống quản lý mượn trả sách (MongoDB)

Đề tài môn NoSQL. Database: MongoDB.

## Mô tả

Hệ thống giúp thư viện quản lý đầu sách, độc giả và quá trình mượn, trả, gia hạn, xử lý quá hạn.

Có hai vai trò:

- **Thủ thư** (kiêm quản trị)
- **Độc giả**

## Chức năng

### Thủ thư

1. **Đăng nhập, đăng xuất.**
2. **Quản lý sách:** thêm, sửa, xóa, xem danh sách đầu sách; chọn thể loại từ danh mục có sẵn; theo dõi số lượng tổng và số còn lại.
3. **Quản lý thể loại:** thêm, sửa, xóa (chỉ khi không còn sách thuộc thể loại đó), xem danh sách thể loại.
4. **Quản lý độc giả:** thêm, sửa thông tin, cấp tài khoản, khóa và mở khóa thẻ.
5. **Lập phiếu mượn:** chọn độc giả và sách, đặt hạn trả. Hệ thống kiểm tra thẻ còn hiệu lực, chưa vượt số sách tối đa, không có sách quá hạn, sách còn trong kho, rồi trừ số lượng còn lại.
6. **Nhận trả sách:** ghi ngày trả và tình trạng sách, tính tiền phạt quá hạn, cộng lại số lượng trong kho.
7. **Gia hạn phiếu mượn:** giới hạn 1–2 lần.
8. **Xử lý đặt trước:** xem và duyệt yêu cầu đặt sách của độc giả.
9. **Tìm kiếm:** sách, độc giả, phiếu mượn.
10. **Thống kê:** top sách mượn nhiều, phiếu quá hạn, lượt mượn theo tháng và theo thể loại, tổng tiền phạt.

### Độc giả

1. **Đăng nhập, đăng xuất, đổi mật khẩu.**
2. **Tìm kiếm sách:** theo tên, tác giả, thể loại; xem chi tiết và tình trạng còn hay hết.
3. **Xem sách đang mượn và lịch sử mượn.**
4. **Yêu cầu gia hạn.**
5. **Đặt trước sách.**

## Database

Tên database: `library_management`

| Collection | Lưu gì |
|---|---|
| `users` | Tài khoản đăng nhập, `role`: `librarian` hoặc `reader` |
| `readers` | Thông tin độc giả, thẻ thư viện, trạng thái thẻ |
| `books` | Đầu sách, mảng `authors`, `categoryIds` tham chiếu thể loại, số lượng tổng và còn lại |
| `categories` | Danh mục thể loại: tên, mô tả |
| `loans` | Phiếu mượn: độc giả, danh sách sách mượn, ngày mượn, hạn trả, ngày trả, số lần gia hạn, tiền phạt |
| `reservations` | Yêu cầu đặt trước: độc giả, sách, ngày đặt, trạng thái |

Ghi chú: tác giả nhúng trong `books`, tiền phạt nằm trong `loans`, nên không có collection riêng cho hai phần này.
