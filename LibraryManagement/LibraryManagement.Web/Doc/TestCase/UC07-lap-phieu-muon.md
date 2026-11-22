# Test case UC07 Lập phiếu mượn

Code: `LoansController.Create` (GET `?cardNumber=` để kiểm tra độc giả, POST để lập phiếu), `LoanService.CheckBorrowerAsync`, `LoanService.CreateAsync` (transaction), gọi `ReservationService.CompleteForLoanAsync`. Trang chi tiết phiếu: `LoansController.Details`.

Bước 2–3 dùng AJAX: nhập mã thẻ (bấm "Kiểm tra" hoặc gõ đủ `DG` + 6 số) thì trang gọi `CheckReader` và thay khối thông tin độc giả + form chọn sách (partial `_Borrower`) mà không tải lại trang. Tắt JavaScript thì form gửi GET `Create?cardNumber=` như cũ.

Sau mỗi test thành công, kiểm tra trong MongoDB: `loans` có phiếu mới, `books.availableQuantity` giảm đúng số cuốn.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC07-01 | Chính | Lập phiếu hợp lệ | `DG000004` mượn Đắc Nhân Tâm + Nhà Giả Kim, giữ hạn trả đề xuất | Phiếu mới *Đang mượn*, hạn trả = hôm nay + 14 ngày (`LoanDays`); số còn lại mỗi sách giảm 1; thông báo thành công | Phiếu `PM000010` *Đang mượn*, mượn 22/11/2026, hạn 06/12/2026; Đắc Nhân Tâm 4→3, Nhà Giả Kim 4→3; item có bản sao tên và thể loại; chuyển tới trang chi tiết phiếu kèm thông báo | Đạt | 22/11/2026 |
| TC07-02 | 3 | Hiện thông tin độc giả | Nhập `DG000001` | Hiện họ tên, số sách đang mượn x/5, hạn thẻ, số phiếu quá hạn | Nguyễn Văn An, đang mượn 2/5 (còn được mượn 3), 0 phiếu quá hạn, hạn thẻ; hiện bảng chọn sách | Đạt | 22/11/2026 |
| TC07-03 | 2a | Mã thẻ không tồn tại | `DG999999` | Báo không tìm thấy, ở lại bước nhập mã thẻ | "Không tìm thấy độc giả có mã thẻ DG999999." dưới ô mã thẻ | Đạt | 22/11/2026 |
| TC07-04 | 3a | Thẻ bị khóa | `DG000009` (seed bị khóa) | Báo lý do không được mượn, không cho chọn sách | Khung đỏ "Thẻ đang bị khóa (lý do: ...)", không có bảng chọn sách; gửi POST thẳng cũng không tạo phiếu | Đạt | 22/11/2026 |
| TC07-05 | 3a | Thẻ hết hạn | `DG000010` (hết hạn 22/11/2025) | Báo thẻ hết hạn, không cho mượn | "Thẻ đã hết hạn ngày 22/11/2025." | Đạt | 22/11/2026 |
| TC07-06 | 3a | Có phiếu quá hạn | `DG000002` có `PM000002` quá hạn (seed) | Báo đang có phiếu quá hạn, không cho mượn | "Độc giả đang có 1 phiếu quá hạn, cần trả sách trước khi mượn tiếp." | Đạt | 22/11/2026 |
| TC07-07 | 5a | Sách đã hết | Đặt "Không Gia Đình" còn 1 cuốn, `DG000005` mượn cuốn đó; rồi `DG000007` mượn "Không Gia Đình" + Đắc Nhân Tâm | Lần 2 báo sách đã hết, bỏ sách khỏi danh sách | "Sách đã hết: "Không Gia Đình". Đã bỏ khỏi danh sách..."; Đắc Nhân Tâm vẫn được giữ tích chọn; sách hết hiện badge *Hết*, không tích được | Đạt | 22/11/2026 |
| TC07-08 | 5b | Vượt 5 cuốn | `DG000001` đang mượn 2 cuốn, chọn thêm 4 | Báo số cuốn còn được mượn, không lập phiếu | "Độc giả đang mượn 2 cuốn, chỉ còn được mượn thêm 3 cuốn (tối đa 5)." | Đạt | 22/11/2026 |
| TC07-09 | 5c | Mượn đúng sách đã đặt trước | `DG000006` có đặt trước *Đã duyệt* "Tuổi Trẻ Đáng Giá Bao Nhiêu" (seed), mượn sách đó | Yêu cầu đặt trước chuyển *Hoàn tất*, `completedLoanId` = id phiếu mới | Yêu cầu chuyển `Completed`, `completedLoanId` trỏ tới `PM000011`; yêu cầu *Chờ duyệt* của người khác không đổi | Đạt | 22/11/2026 |
| TC07-10 | 7a | Hạn trả không sau ngày mượn | Hạn trả = hôm nay | Báo lỗi hạn trả, không lập phiếu | "Hạn trả phải sau ngày mượn (hôm nay)." dưới ô hạn trả | Đạt | 22/11/2026 |
| TC07-11 | Transaction | Lỗi giữa chừng không làm lệch số lượng | "Bàn Về Tự Do" còn 1 cuốn. Gửi **cùng lúc** 2 phiếu: `DG000003` (Mắt Biếc + Bàn Về Tự Do) và `DG000008` (Rừng Na Uy + Bàn Về Tự Do) | Chỉ một phiếu được tạo; phiếu kia không tạo, sách thứ nhất của nó **không** bị trừ | Phiếu của `DG000003` tạo được (Mắt Biếc 2→1, Bàn Về Tự Do 1→0). Phiếu `DG000008` báo "Sách "Bàn Về Tự Do" vừa hết" (lỗi xảy ra **bên trong** transaction), Rừng Na Uy giữ 2; chỉ có 1 phiếu mới | Đạt | 22/11/2026 |
| TC07-12 | Phân quyền | Độc giả mở trang lập phiếu | Đăng nhập `DG000001`, gõ `/Loans/Create` | Trang "Không có quyền truy cập" | Chuyển tới `/Account/AccessDenied` | Đạt | 22/11/2026 |

## Ghi chú

- AJAX `CheckReader` (22/11/2026, Đạt): mã thẻ hợp lệ trả khối thông tin và form có token chống giả mạo, lập phiếu từ form đó thành công; mã sai / trống báo lỗi trong khối; thẻ khóa không có form; độc giả gọi `CheckReader` bị chặn (403); không có JavaScript vẫn chạy bằng GET thường.

- Thêm ngoài danh sách (Đạt): không chọn sách nào thì báo "Vui lòng chọn ít nhất một sách."; trang chi tiết phiếu `/Loans/Details/{id}` hiện đủ thông tin; id sai định dạng thì quay về trang lập phiếu kèm thông báo.
- Lỗi đã sửa trong lúc thử: mã thẻ không tồn tại ban đầu không hiện lỗi (do `ModelState` đã có khóa `cardNumber` từ query string); lỗi hạn trả hiện hai lần. Đã sửa và thử lại đạt.
