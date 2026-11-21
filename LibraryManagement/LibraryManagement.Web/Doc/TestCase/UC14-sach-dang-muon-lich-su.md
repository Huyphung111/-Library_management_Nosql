# Test case UC14 Xem sách đang mượn và lịch sử mượn

Chưa code. Code dự kiến: `MyLoansController` (`Index`, `History`), `LoanService`. `ReaderId` lấy từ claim, không nhận từ URL hay form.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC14-01 | 2 | Tab "Đang mượn" | `DG000001` có phiếu đang mượn | Hiện tên sách, ngày mượn, hạn trả, số lần đã gia hạn; menu "Sách đang mượn" được tô sáng | | Chưa thử | |
| TC14-02 | 2 | Phiếu quá hạn được đánh dấu | Một phiếu có `dueDate` trong quá khứ | Phiếu có badge *Quá hạn* (đỏ), nổi bật so với phiếu khác | | Chưa thử | |
| TC14-03 | 2a | Chưa mượn sách nào | Đăng nhập `DG000002` | Hiện "Bạn chưa mượn sách nào" | | Chưa thử | |
| TC14-04 | 4 | Tab "Lịch sử" | Độc giả có phiếu đã trả, có phạt | Hiện tên sách, ngày mượn, ngày trả, tiền phạt; menu "Lịch sử mượn" được tô sáng | | Chưa thử | |
| TC14-05 | 4a | Chưa có lịch sử | Độc giả chưa trả phiếu nào | Hiện "Chưa có lịch sử mượn" | | Chưa thử | |
| TC14-06 | Bảo mật | Không xem được phiếu của người khác | Đăng nhập `DG000002`, thử thêm `?readerId=` của `DG000001` vào URL | Vẫn chỉ thấy dữ liệu của `DG000002` | | Chưa thử | |
