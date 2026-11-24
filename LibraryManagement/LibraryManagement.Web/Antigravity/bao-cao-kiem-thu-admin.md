# Báo cáo Kiểm thử Chuyên sâu — Vai trò Admin / Thủ thư (`Librarian`) & Hồi quy Độc giả

- **Ngày kiểm thử:** 24/11/2026
- **Môi trường:** Server cô lập `http://localhost:5199` + database tạm (`library_management_qa_reader`, `library_management_qa_admin`), dọn sạch sau khi chạy — **không ảnh hưởng dữ liệu thật `library_management`**.
- **Scripts tự động:** `Antigravity/qa_reader_suite.py` (48 case) · `Antigravity/qa_admin_suite.py` (47 case).
- **Tổng điểm thực thi trực tiếp:** **`91 / 95 PASS`** (`47/48` Role Độc giả + `44/47` Role Admin) · **0 lỗi toàn vẹn tồn kho**.

---

## 1. Kết quả Hồi quy Role Độc giả (47 / 48 PASS)

| Hạng mục vừa sửa | Trạng thái mới | Chi tiết kiểm chứng |
|---|---|---|
| `R05` — Đăng nhập mã thẻ chữ thường (`dg000001`) | **ĐÃ SỬA & PASS** | `AuthService.LoginAsync` tự chuẩn hóa `dg...` → `DG...`, đăng nhập thành công vào `/Catalog`. |
| `BUG-03` — Trả hết sách tự đóng gia hạn `Pending` | **ĐÃ SỬA & PASS** | `LoanService.ReturnAsync` tự chuyển `Pending` → `Rejected` (lý do *"Phiếu đã trả hết sách trước khi được duyệt."*) ngay trong transaction. Tab **Lịch sử gia hạn** không còn treo *"Chờ duyệt"*. |
| `R38` (`BUG-05`) — Hộp xác nhận đặt giữ khi bản giữ cuối quá hạn | **CÒN FAIL (TB)** | Chưa gọi `await ExpireHoldsAsync();` ở `ReservationService.GetBookForReservationConfirmAsync` & `CreateAsync`. |

---

## 2. Bảng kết quả 47 kịch bản kiểm thử Role Admin / Thủ thư (`44 / 47 PASS`)

### Nhóm 1 — Xác thực, Bảo mật & Dashboard (4/4 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A01` | Đăng nhập Thủ thư (`admin / admin123`) | **PASS** | Chuyển thẳng `/Dashboard` |
| `A02` | RBAC: Thủ thư bị chặn 4 trang Độc giả (`/Catalog`, `/MyLoans`, `/MyReservations`, `/MyFavorites`) | **PASS** | Cả 4 bị chặn `302 -> AccessDenied` |
| `A03` | Anti-CSRF: POST `/Categories/Create` thiếu token | **PASS** | Trả `HTTP 400` |
| `A04` | Dashboard hiển thị đúng số liệu KPI và khung *Việc cần xử lý* | **PASS** | Khớp 100% với MongoDB |

### Nhóm 2 — UC05 Quản lý Thể loại (5/5 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A05` | Thêm thể loại mới hợp lệ | **PASS** | Lưu thành công |
| `A06` | Chặn trùng tên không phân biệt hoa/thường (`collation vi` strength 2) | **PASS** | `"thể loại qa test"` bị chặn trùng với `"Thể loại QA Test"` |
| `A07` | Sửa tên & mô tả thể loại | **PASS** | Cập nhật đúng |
| `A08` | Chặn xóa thể loại còn sách (QĐ09) | **PASS** | Giữ nguyên trong DB, báo lỗi |
| `A09` | Xóa thể loại không còn sách | **PASS** | Xóa sạch khỏi DB |

### Nhóm 3 — UC04 Quản lý Sách & Ảnh bìa GridFS (6/7 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A10` | **Chống SSRF**: Chặn tải ảnh bìa từ `http://127.0.0.1:5199/...` | **PASS** | Báo lỗi *"Không tải ảnh từ địa chỉ nội bộ"*, không tạo sách |
| `A11` | Chặn tải ảnh bìa scheme `ftp://` | **PASS** | Bị từ chối |
| `A12` | Thêm sách mới hợp lệ | **PASS** | `availableQuantity == totalQuantity == 3` |
| `A13` | Sửa tăng tổng SL (`3 -> 5`) | **PASS** | Tự cộng `availableQuantity` (`3 -> 5`) nguyên tử bằng `$inc` |
| `A14` | Chặn giảm tổng SL dưới số đang cho mượn / `<= 0` (QĐ09) | **PASS** | Chặn & giữ nguyên DB |
| `A15` | Chặn xóa sách đang có phiếu mượn chưa trả (`HasOpenLoanForBookAsync`) | **PASS** | Chặn thành công |
| `A16` | **BUG-04**: Xóa sách đang có đặt trước `Pending` | **FAIL (TB)** | Sách vẫn bị xóa (`bookLeft=0`), để lại **1 reservation `Pending` mồ côi**! |

### Nhóm 4 — UC06 Quản lý Độc giả (8/8 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A17` | Thêm độc giả mới → tạo đồng thời `readers` + `users` trong transaction | **PASS** | Sinh mã `DG000011`, tài khoản khớp mã thẻ |
| `A18` | Chặn thêm độc giả trùng số CCCD (`IdNumber`) | **PASS** | Báo lỗi đúng trường CCCD |
| `A19` | Chặn ngày sinh tương lai (`2099-01-01`) | **PASS** | Báo lỗi ngày sinh |
| `A20` | Sửa họ tên độc giả đồng bộ sang `users.fullName` | **PASS** | Cập nhật đồng bộ trong transaction |
| `A21` | Khóa thẻ thiếu lý do | **PASS** | Chặn, thẻ vẫn `Active` |
| `A22` | Khóa thẻ kèm lý do → `Locked`; Mở khóa → `Active` | **PASS** | Chuyển trạng thái chuẩn |
| `A23` | Gia hạn thẻ cộng đúng `+1 năm` (QĐ01) | **PASS** | `2027 -> 2028` |
| `A24` | Đặt lại mật khẩu về mặc định (`123456`) | **PASS** | Đăng nhập lại thành công vào `/Catalog` |

### Nhóm 5 — UC07 Lập phiếu mượn 2 Tab + Hộp Mẫu 1 (6/6 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A25` | Trang `/Loans/Create` hiện đủ 2 tab (*Danh sách độc giả* & *Phiếu mượn gần đây*) | **PASS** | `HTTP 200` |
| `A26` | AJAX `CheckReader` nhận mã thẻ chữ thường + render Hộp Phiếu Mẫu 1 (`#slipModal`) | **PASS** | Tự chuẩn hóa chữ HOA |
| `A27` | `CheckReader` thẻ bị khóa (`DG000009`) báo không được mượn | **PASS** | Chặn rõ lý do |
| `A28` | **Chặn vượt 5 cuốn tính cả sách đang giữ chưa chọn (`unpickedHeld`)** | **PASS** | Đang giữ 1 cuốn + chọn 5 cuốn khác bị chặn đúng QĐ02 |
| `A29` | Chặn hạn trả `<=` ngày mượn (QĐ03) | **PASS** | Từ chối lập phiếu |
| `A30` | Lập phiếu gồm **1 cuốn `Held` + 2 cuốn thường** | **PASS** | `ConsumeHold` chuyển `Held -> Completed` **không trừ kho 2 lần**, 2 cuốn thường giảm đúng `-1` |

### Nhóm 6 — UC09 Gia hạn & UC08 Nhận trả sách (8/8 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A31` | Từ chối gia hạn thiếu lý do | **PASS** | Chặn & yêu cầu nhập lý do |
| `A32` | Từ chối gia hạn kèm lý do | **PASS** | `Rejected`, hạn trả giữ nguyên |
| `A33` | Gia hạn tại quầy (`RenewAtCounter`) | **PASS** | `RenewalCount = 1`, hạn trả cộng đúng `+7 ngày` |
| `A34` | Chặn gia hạn khi sách có người đặt trước (`HasWaitingAsync`, QĐ04) | **PASS** | Chặn đúng, `RenewalCount` giữ nguyên |
| `A35` | **Trả từng phần (1/3 cuốn)** có người đặt trước `Approved` | **PASS** | Phiếu vẫn `Borrowing`, kho `+1`, `ReturnResult` hiện đúng người chờ (*Nguyễn Văn An*) |
| `A36` | Trả sách hư hỏng thiếu mức phạt | **PASS** | Chặn & yêu cầu nhập mức phạt |
| `A37` | **Trả cuốn Hư hỏng (`30.000đ`) + cuốn Mất (`Book.Price`)** | **PASS** | Đóng phiếu `Returned`, `TotalFine = 350.000đ`, sách mất **trừ `TotalQuantity -1`** (không cộng `AvailableQuantity`) |
| `A38` | **Xác minh fix `BUG-03`**: Trả hết sách tự đóng `Pending` renewal | **PASS** | `pendingLeft = 0`, không còn xuất hiện trên `/Renewals` |

### Nhóm 7 — UC10 Xử lý Đặt trước (3/4 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A39` | Duyệt đặt trước `Pending -> Approved` | **PASS** | Cập nhật đúng |
| `A40` | Hủy đặt trước `Approved` kèm lý do → `Cancelled` | **PASS** | Cập nhật đúng |
| `A41` | Thủ thư hủy đặt giữ (`Held`) → **hoàn trả đúng 1 cuốn vào kho** | **PASS** | `availableQuantity: 3 -> 4` |
| `A42` | **BUG-07**: Duyệt đặt trước của độc giả đang bị khóa thẻ (`DG000009`) | **FAIL (TB)** | Server vẫn cho duyệt sang `Approved` dù thẻ đang bị khóa |

### Nhóm 8 — UC11 Tìm kiếm, UC12 Báo cáo & Toàn vẹn tồn kho (4/5 PASS)
| Mã | Kịch bản kiểm thử | Kết quả | Chi tiết |
|---|---|---|---|
| `A43` | Tìm kiếm Sách / Độc giả / Phiếu quá hạn / ký tự đặc biệt + `bad-id` | **PASS** | Cả 4 trả `200`, dữ liệu chuẩn |
| `A44` | Báo cáo `TopBooks`: `fromDate > toDate` | **PASS** | Báo lỗi hợp lệ |
| `A45` | Cả 5 màn hình báo cáo (`TopBooks`, `Overdue`, `ByMonth`, `ByCategory`, `Fines`) | **PASS** | Đều `200 OK` |
| `A46` | **BUG-02**: Báo cáo Tiền phạt lọc theo ngày trả hôm nay cho phiếu mượn hôm kia | **FAIL (Cao)** | **Không thấy `PM999FIN`** vì `GetFinesAsync` đang lọc theo `BorrowDate` thay vì `ReturnDate` |
| `A47` | **Toàn vẹn tồn kho cuối phiên** sau 46 thao tác CRUD/Mượn/Trả/Mất/Hủy giữ | **PASS** | **0 cuốn lệch kho** trên toàn bộ đầu sách |

---

## 3. Tóm tắt các lỗi còn lại cần xử lý dứt điểm (chỉ còn 5 lỗi chính)

Sau khi bạn đã sửa xong `R05` (đăng nhập `dg...`) và `BUG-03` (tự đóng gia hạn khi trả hết sách), toàn bộ hệ thống chỉ còn lại **5 lỗi thực tế** (xếp theo mức ưu tiên):

1. **[CAO] `BUG-01` — Hardcode `5000L` trong Báo cáo quá hạn** (`Services/ReportService.cs:209`): Inject `IOptions<LibraryRules>` thay cho `5000L`.
2. **[CAO] `BUG-02` (`A46`) — Lỗi lọc ngày ở Báo cáo thống kê** (`Services/ReportService.cs:127, 249, 324, 389–392`):
   - Đổi mốc kết thúc sang `$lt DateHelper.VnDateToUtc(toDate.Value.AddDays(1))`.
   - Đổi `GetFinesAsync` lọc theo `item.ReturnDate` thay vì `BorrowDate`.
3. **[TRUNG BÌNH] `BUG-04` (`A16`) — Xóa sách để lại đặt trước `Pending`/`Approved` mồ côi** (`Services/BookService.cs:213–231`): Chặn xóa hoặc tự hủy các đặt trước đó khi xóa sách.
4. **[TRUNG BÌNH] `BUG-05` (`R38`) — Chưa gọi `ExpireHoldsAsync()` khi mở hộp & tạo đặt giữ** (`Services/ReservationService.cs:214, 250`): Thêm `await ExpireHoldsAsync();`.
5. **[TRUNG BÌNH] `BUG-06` & `BUG-07` (`A42`) — Xử lý Đặt trước ở `ReservationService.cs`**:
   - Gộp điều kiện trạng thái cũ vào filter `UpdateOneAsync` (chống race condition).
   - Kiểm tra lại trạng thái thẻ trong `ApproveAsync` để không duyệt cho thẻ đã bị khóa/hết hạn (hoặc ghi rõ ngoại lệ nếu cố ý cho duyệt).
