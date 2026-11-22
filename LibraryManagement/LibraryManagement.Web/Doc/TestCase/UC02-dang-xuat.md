# Test case UC02 Đăng xuất

Code: `AccountController.Logout` (POST, `[IgnoreAntiforgeryToken]`, tự kiểm tra token khi còn đăng nhập), nút Đăng xuất trong dropdown tài khoản của `_Layout`.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC02-01 | Chính | Đăng xuất bình thường | Đăng nhập `admin` → mở dropdown tài khoản → "Đăng xuất" | Về trang đăng nhập | Về `/Account/Login` | Đạt | 22/11/2026 |
| TC02-02 | Hậu ĐK | Sau đăng xuất không vào lại được trang cần đăng nhập | Sau TC02-01, gõ `/Account/ChangePassword` | Bị chuyển về trang đăng nhập (kèm `ReturnUrl`) | Chuyển về `/Account/Login?ReturnUrl=%2FAccount%2FChangePassword` | Đạt | 22/11/2026 |
| TC02-03 | 1a | Phiên đã hết hạn rồi mới bấm Đăng xuất | Đăng nhập → xóa cookie `.LibraryManagement.Auth` → bấm "Đăng xuất" ở trang đang mở (token cũ) | Về thẳng trang đăng nhập, **không** báo lỗi 400 | 302 về `/Account/Login`, không lỗi 400 | Đạt | 22/11/2026 |
