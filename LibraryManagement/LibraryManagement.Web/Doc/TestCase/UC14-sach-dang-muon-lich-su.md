# Test case UC14 Xem sách đang mượn và lịch sử mượn

Code: `MyLoansController` (`Index`, `History`), `LoanService.GetCurrentLoansAsync`, `LoanService.GetHistoryAsync`. `ReaderId` lấy từ claim, không nhận từ URL hay form.

Lịch sử gồm mọi cuốn đã trả, kể cả cuốn đã trả trong phiếu còn đang mượn (trả từng phần).

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC14-01 | 2 | Tab "Đang mượn" | `DG000003` có phiếu `PM000013` đang mượn | Hiện tên sách, ngày mượn, hạn trả, số lần đã gia hạn; menu "Sách đang mượn" được tô sáng | `PM000013`: Mắt Biếc, Bàn Về Tự Do, mượn 22/11/2026, hạn 06/12/2026, đã gia hạn 0, nút "Gia hạn"; tab và menu "Sách đang mượn" tô sáng | Đạt | 22/11/2026 |
| TC14-02 | 2 | Phiếu quá hạn được đánh dấu | `DG000005`, sửa `dueDate` của `PM000012` về 16/11/2026 | Phiếu có badge *Quá hạn* (đỏ), nổi bật so với phiếu khác | Dòng tô nền đỏ, hạn trả chữ đỏ, badge "Quá hạn 6 ngày" | Đạt | 22/11/2026 |
| TC14-03 | 2a | Chưa mượn sách nào | `DG000007` (chưa có phiếu); `DG000001` (đã trả hết) | Hiện "Bạn chưa mượn sách nào" | Cả hai hiện "Bạn chưa mượn sách nào." kèm nút "Tìm sách" | Đạt | 22/11/2026 |
| TC14-04 | 4 | Tab "Lịch sử" | `DG000001` có các phiếu đã trả, có phạt | Hiện tên sách, ngày mượn, ngày trả, tiền phạt; menu "Lịch sử mượn" được tô sáng | 5 dòng, mới nhất trước: Đắc Nhân Tâm *Mất* 86.000đ, Clean Code *Hư hỏng* 20.000đ (`PM000001`), các cuốn của `PM000009`, Clean Code `PM000005` 25.000đ; có dòng tổng tiền phạt; menu "Lịch sử mượn" tô sáng | Đạt | 22/11/2026 |
| TC14-05 | 4a | Chưa có lịch sử | `DG000005`, `DG000007` | Hiện "Chưa có lịch sử mượn" | Hiện "Chưa có lịch sử mượn." | Đạt | 22/11/2026 |
| TC14-06 | Bảo mật | Không xem được phiếu của người khác | Đăng nhập `DG000002`, thêm `?readerId=` của `DG000001` vào URL lịch sử | Vẫn chỉ thấy dữ liệu của `DG000002` | Chỉ hiện `PM000002`, `PM000006` của `DG000002`. Thủ thư mở `/MyLoans` bị chuyển tới "Không có quyền truy cập" | Đạt | 22/11/2026 |
