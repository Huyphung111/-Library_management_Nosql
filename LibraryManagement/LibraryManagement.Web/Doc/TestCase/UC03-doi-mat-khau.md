# Test case UC03 Đổi mật khẩu

Code: `AccountController.ChangePassword`, `AuthService.ChangePasswordAsync`, `ChangePasswordVM`. Độ dài tối thiểu đọc từ `LibraryRules.MinPasswordLength` (hiện là 6).

Nên thử các test làm đổi mật khẩu thật (TC03-06, TC03-07) bằng `DG000002`, thử xong đổi lại về mật khẩu mặc định để không ảnh hưởng người khác.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC03-01 | 4a | Sai mật khẩu hiện tại | Hiện tại: sai; mới và xác nhận: `matkhaumoi1` | Báo "Mật khẩu hiện tại không đúng." dưới ô mật khẩu hiện tại; mật khẩu không đổi | Đúng, lỗi hiện dưới đúng ô | Đạt | 21/11/2026 |
| TC03-02 | 4b | Mật khẩu mới quá ngắn | Hiện tại: đúng; mới và xác nhận: `abc` | Báo "Mật khẩu mới phải có ít nhất 6 ký tự." dưới ô mật khẩu mới; mật khẩu không đổi | Đúng, lỗi hiện dưới đúng ô | Đạt | 21/11/2026 |
| TC03-03 | 4b | Mật khẩu mới trùng mật khẩu hiện tại | Mới và xác nhận giống mật khẩu hiện tại | Báo "Mật khẩu mới phải khác mật khẩu hiện tại." dưới ô mật khẩu mới | | Chưa thử | |
| TC03-04 | 4b | Xác nhận không khớp | Mới: `matkhaumoi1`, xác nhận: `matkhaumoi2` | Báo "Xác nhận mật khẩu không khớp với mật khẩu mới." dưới ô xác nhận (báo ngay trên trình duyệt, chưa gửi lên server) | | Chưa thử | |
| TC03-05 | 4b | Bỏ trống | Bấm "Lưu" khi chưa nhập gì | Báo lỗi dưới cả 3 ô | | Chưa thử | |
| TC03-06 | Chính | Đổi thành công (Độc giả) | Đăng nhập `DG000002`; hiện tại: mật khẩu mặc định; mới và xác nhận: `matkhaumoi1` | Thông báo xanh "Đổi mật khẩu thành công."; trong MongoDB `passwordHash` đổi và bắt đầu bằng `$2a$`; đăng xuất rồi đăng nhập bằng mật khẩu cũ bị từ chối, bằng `matkhaumoi1` được. Thử xong đổi lại về mật khẩu mặc định | | Chưa thử | |
| TC03-07 | Chính | Đổi thành công (Thủ thư) | Như TC03-06 với `admin`, thử xong đổi lại `admin123` | Như TC03-06 | | Chưa thử | |
