# Test case UC08 Nhận trả sách

Code: `LoansController.Return` (GET tìm, POST nhận trả), `LoansController.ReturnResult`, `LoanService.FindForReturnAsync`, `LoanService.ReturnAsync` (transaction, cập nhật item bằng `arrayFilters`), gọi `ReservationService.GetWaitingListAsync` sau transaction.

Mức phạt đọc từ `LibraryRules.FinePerDay` (5.000đ / ngày / cuốn). Tiền phạt một cuốn = phạt quá hạn + (mức hư hỏng Thủ thư nhập, hoặc giá sách nếu mất). Muốn tạo phiếu quá hạn thì sửa `dueDate` của phiếu về quá khứ bằng mongosh.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC08-01 | Chính | Tìm theo mã thẻ | Nhập `DG000001` (đang có phiếu) | Hiện các phiếu *Đang mượn*, sách chưa trả, hạn trả | Hiện `PM000001` với 2 cuốn chưa trả, hạn trả, badge *Đang mượn* | Đạt | 22/11/2026 |
| TC08-02 | Chính | Tìm theo mã phiếu | Nhập `PM000010` | Hiện đúng phiếu đó | Hiện đúng `PM000010`, badge *Quá hạn 2 ngày*, phạt dự kiến 10.000đ mỗi cuốn | Đạt | 22/11/2026 |
| TC08-03 | 3a | Không có phiếu đang mượn | Nhập `DG000007` (chưa mượn) | Báo không có sách cần trả | "Độc giả Bùi Quang Nhật (DG000007) không có phiếu nào đang mượn, không có sách cần trả." Mã phiếu đã trả (`PM000004`) và mã thẻ không tồn tại cũng báo rõ | Đạt | 22/11/2026 |
| TC08-04 | 5a | Trả đúng hạn, bình thường | `PM000011` trả hết, *Bình thường* | Phạt 0; `availableQuantity` +1 mỗi cuốn; phiếu *Đã trả* | Phạt 0đ; Tuổi Trẻ Đáng Giá Bao Nhiêu 2/4 → 3/4; phiếu `Returned` | Đạt | 22/11/2026 |
| TC08-05 | 5 | Trả trễ | `PM000002` quá hạn 3 ngày (hạn 19/11/2026), trả 1 cuốn bình thường | Phạt 15.000đ cho cuốn đó | Phạt 15.000đ, `totalFine` 15.000 | Đạt | 22/11/2026 |
| TC08-06 | 5 | Sách hư hỏng | `PM000001` đúng hạn, Clean Code *Hư hỏng*, nhập phạt 20.000đ | Phạt 20.000đ; sách vẫn cộng lại vào kho (`availableQuantity` +1) | Phạt 20.000đ; Clean Code 3/4 → 4/4. Không nhập mức phạt thì báo "Vui lòng nhập mức phạt hư hỏng..." | Đạt | 22/11/2026 |
| TC08-07 | 5, 7 | Sách mất | `PM000001` đúng hạn, Đắc Nhân Tâm *Mất* | Phạt bằng giá sách; `totalQuantity` −1, `availableQuantity` không đổi | Phạt 86.000đ (giá sách); Đắc Nhân Tâm 3/5 → 3/4 | Đạt | 22/11/2026 |
| TC08-08 | 5 | Trễ và hư hỏng cùng lúc | `PM000010` quá hạn 2 ngày: Đắc Nhân Tâm *Hư hỏng* 20.000đ + Nhà Giả Kim bình thường | Phạt 30.000đ (10.000 + 20.000); tổng phạt hiển thị đúng | Đắc Nhân Tâm 30.000đ, Nhà Giả Kim 10.000đ, tổng 40.000đ | Đạt | 22/11/2026 |
| TC08-09 | 8a | Trả từng phần | `PM000001` 2 cuốn, trả 1 (Clean Code) | Cuốn trả có `returnDate`; phiếu vẫn *Đang mượn*; lần sau chỉ hiện cuốn còn lại | Phiếu giữ `Borrowing`, báo "Phiếu còn 1 cuốn chưa trả."; tìm lại chỉ còn 1 cuốn | Đạt | 22/11/2026 |
| TC08-10 | 8 | Trả nốt phần còn lại | Tiếp TC08-09, trả cuốn còn lại | Phiếu chuyển *Đã trả*; `totalFine` = tổng phạt cả hai lần | Phiếu `Returned`, `totalFine` = 106.000 (20.000 + 86.000) | Đạt | 22/11/2026 |
| TC08-11 | 7a | Sách có người đặt trước | Sapiens (`PM000003`) có 2 yêu cầu *Đã duyệt* (DG000007 đặt 19/11, DG000008 đặt 20/11) và 1 *Chờ duyệt* (DG000005); trả Sapiens | Sau khi trả, hiện danh sách người chờ (họ tên, SĐT) theo ngày đặt, sớm nhất lên đầu | Hiện 2 người *Đã duyệt*: 1. DG000007 (19/11), 2. DG000008 (20/11), kèm SĐT và email; yêu cầu *Chờ duyệt* không hiện | Đạt | 22/11/2026 |
| TC08-12 | 8 | Trả hết phiếu đang có yêu cầu gia hạn *Chờ duyệt* (lỗi BUG-03 báo cáo Antigravity) | `PM000002` (1 cuốn) có 1 yêu cầu *Chờ duyệt*; trả hết | Phiếu *Đã trả*; yêu cầu tự chuyển *Từ chối*, lý do "Phiếu đã trả hết sách trước khi được duyệt."; không còn trong danh sách duyệt UC09 | Đúng; tab "Lịch sử gia hạn" của DG000002 hiện *Bị từ chối* kèm lý do trên | Đạt | 24/11/2026 |
| TC08-13 | 8a | Trả một phần phiếu có yêu cầu gia hạn *Chờ duyệt* | `PM000001` (2 cuốn) có 1 yêu cầu *Chờ duyệt*; trả 1 cuốn | Phiếu vẫn *Đang mượn*, yêu cầu vẫn *Chờ duyệt* | Đúng như mong đợi | Đạt | 24/11/2026 |

## Ghi chú

- Thêm ngoài danh sách (Đạt): không chọn cuốn nào thì báo "Vui lòng chọn ít nhất một cuốn được trả."; gửi lại cuốn đã trả thì báo "Cuốn ... đã được trả trước đó." và không cộng kho lần hai.
- Trang nhận trả tính trước tiền phạt bằng JavaScript và hỏi xác nhận "Độc giả đã nộp phạt?" (bước 6); server tính lại khi lưu.
- 24/11/2026: TC08-12, TC08-13 và TC01-11 thử trên database riêng `library_management_claude_fix` (đã xóa sau khi thử).
