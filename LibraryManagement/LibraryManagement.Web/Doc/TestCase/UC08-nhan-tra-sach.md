# Test case UC08 Nhận trả sách

Chưa code. Code dự kiến: `LoansController.Return`, `LoanService.ReturnAsync` (transaction), gọi `ReservationService.GetWaitingListAsync`.

Mức phạt đọc từ `LibraryRules.FinePerDay` (5.000đ / ngày / cuốn). Muốn tạo phiếu quá hạn thì sửa `dueDate` của phiếu về quá khứ bằng mongosh.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC08-01 | Chính | Tìm theo mã thẻ | Nhập `DG000001` (đang có phiếu) | Hiện các phiếu *Đang mượn*, sách chưa trả, hạn trả | | Chưa thử | |
| TC08-02 | Chính | Tìm theo mã phiếu | Nhập `PM000001` | Hiện đúng phiếu đó | | Chưa thử | |
| TC08-03 | 3a | Không có phiếu đang mượn | Nhập `DG000002` (chưa mượn) | Báo không có sách cần trả | | Chưa thử | |
| TC08-04 | 5a | Trả đúng hạn, bình thường | Trả hết sách của phiếu, tình trạng *Bình thường* | Phạt 0; `availableQuantity` +1 mỗi cuốn; phiếu *Đã trả* | | Chưa thử | |
| TC08-05 | 5 | Trả trễ | Phiếu quá hạn 3 ngày, trả 1 cuốn bình thường | Phạt 15.000đ cho cuốn đó | | Chưa thử | |
| TC08-06 | 5 | Sách hư hỏng | Trả đúng hạn, *Hư hỏng*, nhập phạt 20.000đ | Phạt 20.000đ; sách vẫn cộng lại vào kho (`availableQuantity` +1) | | Chưa thử | |
| TC08-07 | 5, 7 | Sách mất | Trả đúng hạn, *Mất* | Phạt bằng giá sách; `totalQuantity` −1, `availableQuantity` không đổi | | Chưa thử | |
| TC08-08 | 5 | Trễ và hư hỏng cùng lúc | Quá hạn 2 ngày + *Hư hỏng* 20.000đ | Phạt 30.000đ (10.000 + 20.000); tổng phạt hiển thị đúng | | Chưa thử | |
| TC08-09 | 8a | Trả từng phần | Phiếu 2 cuốn, trả 1 | Cuốn trả có `returnDate`; phiếu vẫn *Đang mượn*; lần sau chỉ hiện cuốn còn lại | | Chưa thử | |
| TC08-10 | 8 | Trả nốt phần còn lại | Tiếp TC08-09, trả cuốn còn lại | Phiếu chuyển *Đã trả*; `totalFine` = tổng phạt cả hai lần | | Chưa thử | |
| TC08-11 | 7a | Sách có người đặt trước | Sách vừa trả có 2 yêu cầu *Đã duyệt* | Sau khi trả, hiện danh sách người chờ (họ tên, SĐT) theo ngày đặt, sớm nhất lên đầu | | Chưa thử | |
