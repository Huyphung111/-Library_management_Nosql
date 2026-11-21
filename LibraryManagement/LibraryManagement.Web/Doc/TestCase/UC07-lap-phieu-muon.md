# Test case UC07 Lập phiếu mượn

Chưa code. Code dự kiến: `LoansController.Create`, `CheckReader` (AJAX), `LoanService.CreateAsync` (transaction), gọi `ReservationService.CompleteForLoanAsync`.

Sau mỗi test thành công, kiểm tra trong MongoDB: `loans` có phiếu mới, `books.availableQuantity` giảm đúng số cuốn.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC07-01 | Chính | Lập phiếu hợp lệ | `DG000001` mượn Đắc Nhân Tâm + Nhà Giả Kim, giữ hạn trả đề xuất | Phiếu `PM000001` *Đang mượn*, hạn trả = hôm nay + 14 ngày (`LoanDays`); Đắc Nhân Tâm 2/3, Nhà Giả Kim 1/2; thông báo thành công | | Chưa thử | |
| TC07-02 | 3 | Hiện thông tin độc giả | Nhập `DG000001` | Hiện họ tên, số sách đang mượn x/5, hạn thẻ, số phiếu quá hạn | | Chưa thử | |
| TC07-03 | 2a | Mã thẻ không tồn tại | `DG999999` | Báo không tìm thấy, ở lại bước nhập mã thẻ | | Chưa thử | |
| TC07-04 | 3a | Thẻ bị khóa | Khóa tạm `DG000002` | Báo lý do không được mượn, không cho chọn sách | | Chưa thử | |
| TC07-05 | 3a | Thẻ hết hạn | Lùi hạn thẻ `DG000002` | Báo thẻ hết hạn, không cho mượn | | Chưa thử | |
| TC07-06 | 3a | Có phiếu quá hạn | Sửa `dueDate` một phiếu của `DG000001` về quá khứ | Báo đang có phiếu quá hạn, không cho mượn | | Chưa thử | |
| TC07-07 | 5a | Sách đã hết | Mượn "Cho Tôi Xin Một Vé Đi Tuổi Thơ" (1/1) cho `DG000001`, rồi lập phiếu khác cho `DG000002` cùng sách | Lần 2 báo sách đã hết, bỏ sách khỏi danh sách | | Chưa thử | |
| TC07-08 | 5b | Vượt 5 cuốn | `DG000001` đang mượn 4 cuốn, chọn thêm 2 | Báo "còn được mượn 1 cuốn", không lập phiếu | | Chưa thử | |
| TC07-09 | 5c | Mượn đúng sách đã đặt trước | `DG000002` có đặt trước *Đã duyệt* cho một sách, rồi mượn sách đó | Yêu cầu đặt trước chuyển *Hoàn tất*, `completedLoanId` = id phiếu mới | | Chưa thử | |
| TC07-10 | 7a | Hạn trả không sau ngày mượn | Sửa hạn trả = hôm nay | Báo lỗi hạn trả, không lập phiếu | | Chưa thử | |
| TC07-11 | Transaction | Lỗi giữa chừng không làm lệch số lượng | Chọn 2 sách, trong đó sách thứ 2 vừa hết (số còn lại 0) | Không tạo phiếu, sách thứ nhất **không** bị trừ số lượng | | Chưa thử | |
| TC07-12 | Phân quyền | Độc giả mở trang lập phiếu | Đăng nhập `DG000001`, gõ `/Loans/Create` | Trang "Không có quyền truy cập" | | Chưa thử | |
