# Báo cáo Lỗi (Bug Report) — Hệ thống Quản lý Mượn Trả Sách

- **Ngày lập:** 23/11/2026
- **Phiên bản / Commit kiểm tra:** `cfd68a4`
- **Tổng số lỗi phát hiện:** **10 lỗi** (2 mức Cao · 5 mức Trung bình · 3 mức Nhỏ)

---

## 1. Bảng tổng hợp danh sách lỗi

| Mã lỗi | Mức độ | UC / Module | Vị trí file & dòng | Tóm tắt lỗi | Trạng thái |
|---|---|---|---|---|---|
| `BUG-01` | **Cao** | UC12 – Báo cáo quá hạn | `Services/ReportService.cs:209` | Hardcode tiền phạt `5000L` thay vì đọc `LibraryRules.FinePerDay` | `[x]` Đã sửa (24/11/2026) |
| `BUG-02` | **Cao** | UC12 – Báo cáo thống kê | `Services/ReportService.cs:127, 249, 324, 389-392` | Lệch mốc cuối ngày `toDate` do `VnDateToUtc` cắt giờ; Báo cáo tiền phạt lọc theo `BorrowDate` thay vì `ReturnDate` | `[x]` Đã sửa (24/11/2026) |
| `BUG-03` | **Trung bình** | UC09 – Gia hạn | `Services/LoanService.cs:626-631` | Phiếu đã trả (`Returned`) vẫn hiện ở danh sách gia hạn chờ duyệt (`/Renewals`) | `[x]` Đã sửa (24/11/2026) |
| `BUG-04` | **Trung bình** | UC04 – Xóa sách | `Services/BookService.cs:213-231` | Xóa sách chưa chặn/hủy yêu cầu đặt trước `Pending`/`Approved`, tạo bản ghi mồ côi | `[x]` Đã sửa (24/11/2026) |
| `BUG-05` | **Trung bình** | UC16 – Đặt giữ / Đặt trước | `Services/ReservationService.cs:214-296` | Chưa gọi `ExpireHoldsAsync()` trước khi kiểm tra tồn kho lúc mở hộp xác nhận và lúc tạo đặt giữ | `[x]` Đã sửa (24/11/2026) |
| `BUG-06` | **Trung bình** | UC10 & UC16 – Đặt trước | `Services/ReservationService.cs:337, 434, 459, 491` | `UpdateOneAsync` thiếu điều kiện trạng thái cũ trong filter (race condition khi 2 phía bấm cùng lúc) | `[x]` Đã sửa (24/11/2026) |
| `BUG-07` | **Trung bình** | UC10 – Duyệt đặt trước | `Services/ReservationService.cs:420-436` | `ApproveAsync` chưa kiểm tra lại thẻ bị khóa / hết hạn phía server | `[x]` Đã sửa (24/11/2026) |
| `BUG-08` | **Nhỏ** | Sách yêu thích | `Services/FavoriteService.cs:104-108` | Chưa lọc `HoldUntil >= StartOfTodayVnUtc()` khi hiện nhãn *"Đang giữ cho bạn"* | `[x]` Đã sửa (24/11/2026) |
| `BUG-09` | **Nhỏ** | UC04 – Sửa sách | `Services/BookService.cs:171-176` | Thông báo lỗi giảm tổng số lượng chỉ ghi *"đang cho mượn"* dù thực tế bao gồm cả sách `Held` | `[x]` Đã sửa (24/11/2026) |
| `BUG-10` | **Nhỏ** | UC01 – Phiên đăng nhập | `Controllers/AccountController.cs:61-62` | Claim `CardNotice` không tự cập nhật nếu Thủ thư khóa/mở khóa thẻ trong lúc Độc giả đang đăng nhập | `[x]` Đã sửa (24/11/2026) |

---

## 2. Chi tiết từng lỗi & Hướng khắc phục

### `BUG-01` — Hardcode tiền phạt `5000L` trong Báo cáo quá hạn
- **Mức độ:** Cao | **UC:** UC12
- **Vị trí:** `Services/ReportService.cs` (dòng `209`)
- **Code hiện tại:**
  ```csharp
  var estimatedFine = 5000L * overdueDays * overdueBooksCount;
  ```
- **Vấn đề:** Vi phạm quy định không viết cứng tham số nghiệp vụ (`Options/LibraryRules.cs`). Nếu đổi `LibraryRules:FinePerDay` trong `appsettings.json`, báo cáo quá hạn sẽ tính sai so với tiền phạt thực tế ở `LoanService`.
- **Hướng sửa:** Inject `IOptions<LibraryRules> rules` vào `ReportService` và thay `5000L` bằng `_rules.FinePerDay`.

---

### `BUG-02` — Lệch mốc cuối ngày `toDate` và sai trường ngày lọc ở Báo cáo tiền phạt
- **Mức độ:** Cao | **UC:** UC12
- **Vị trí:** `Services/ReportService.cs` (dòng `127`, `249`, `324`, `389–392`)
- **Code hiện tại:**
  ```csharp
  dateFilter.Add("$lte", BsonDateTime.Create(DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1))));
  ```
  và ở `GetFinesAsync`:
  ```csharp
  filter &= filterBuilder.Gte(l => l.BorrowDate, DateHelper.VnDateToUtc(fromDate.Value));
  filter &= filterBuilder.Lte(l => l.BorrowDate, DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1)));
  ```
- **Vấn đề:**
  1. `DateHelper.VnDateToUtc(DateTime vnDate)` gọi `vnDate.Date` nên cắt bỏ `23:59:59`, làm mốc cuối thực chất chỉ đến `00:00:00` ngày `toDate`.
  2. `GetFinesAsync` lọc theo `BorrowDate` (ngày mượn) thay vì `item.ReturnDate` (ngày trả phát sinh tiền phạt). Phiếu mượn tháng 10 nhưng trả trễ tháng 11 sẽ không xuất hiện trong báo cáo thu phạt tháng 11.
- **Hướng sửa:**
  1. Đổi toàn bộ mốc kết thúc sang `$lt DateHelper.VnDateToUtc(toDate.Value.AddDays(1))` (và cuối năm sang `$lt DateHelper.VnDateToUtc(new DateTime(selectedYear + 1, 1, 1))`).
  2. Trong `GetFinesAsync`, lọc các `item` theo `item.ReturnDate >= fromUtc && item.ReturnDate < toNextDayUtc` (dùng aggregation `$unwind "$items"` hoặc lọc kết hợp `ElemMatch`).

---

### `BUG-03` — Phiếu đã trả hết (`Returned`) vẫn hiện ở trang Gia hạn chờ duyệt
- **Mức độ:** Trung bình | **UC:** UC09
- **Vị trí:** `Services/LoanService.cs` (dòng `626–631`)
- **Kịch bản tái hiện:**
  1. Độc giả gửi yêu cầu gia hạn online (`Pending`).
  2. Trước khi Thủ thư xử lý, độc giả mang toàn bộ sách đến quầy trả hết (`Status` chuyển `Returned`).
  3. Thủ thư mở Dashboard thấy `0` gia hạn chờ duyệt, nhưng mở `/Renewals` vẫn thấy phiếu đó xuất hiện.
- **Hướng sửa:**
  1. Thêm điều kiện `Builders<Loan>.Filter.Eq(l => l.Status, LoanStatus.Borrowing)` vào `GetPendingRenewalsAsync()`.
  2. Trong `LoanService.ReturnAsync`, khi đóng phiếu sang `Returned`, cập nhật luôn các phần tử `RenewalRequests` còn `Pending` sang `Rejected` (lý do: *"Phiếu đã trả hết sách"*).

---

### `BUG-04` — Xóa sách chưa chặn/hủy các yêu cầu Đặt trước (`Pending` / `Approved`)
- **Mức độ:** Trung bình | **UC:** UC04
- **Vị trí:** `Services/BookService.cs` (dòng `213–231`)
- **Vấn đề:** `DeleteAsync` đã kiểm tra `HasOpenLoanForBookAsync` và `ReservationStatus.Held`, nhưng bỏ sót `Pending` và `Approved`. Nếu xóa sách đang có người đặt trước, document trong `reservations` sẽ thành mồ côi trỏ tới `bookId` không còn tồn tại.
- **Hướng sửa:** Kiểm tra thêm `Pending` / `Approved` để chặn xóa (báo rõ số yêu cầu đang chờ), hoặc tự hủy chúng kèm lý do trong transaction trước khi xóa sách.

---

### `BUG-05` — Chưa gọi `ExpireHoldsAsync()` trước khi kiểm tra tồn kho khi Đặt giữ
- **Mức độ:** Trung bình | **UC:** UC16
- **Vị trí:** `Services/ReservationService.cs` (dòng `214–241` và `250–296`)
- **Kịch bản tái hiện:** Bản giữ cuối cùng của sách đã qua ngày `HoldUntil`, nhưng tác vụ nền `HoldExpiryService` (chạy 5 phút/lần) chưa kịp chạy. Độc giả khác mở hộp xác nhận hoặc bấm đặt sẽ thấy `AvailableQuantity == 0` và bị đẩy vào hàng chờ `Pending` thay vì được đặt giữ `Held`.
- **Hướng sửa:** Gọi `await ExpireHoldsAsync();` ở đầu `GetBookForReservationConfirmAsync` và `CreateAsync`.

---

### `BUG-06` — Thiếu điều kiện trạng thái cũ khi cập nhật yêu cầu Đặt trước (Race Condition)
- **Mức độ:** Trung bình | **UC:** UC10, UC16
- **Vị trí:** `Services/ReservationService.cs` (dòng `337`, `434`, `459`, `491`)
- **Vấn đề:** `UpdateOneAsync(r => r.Id == id, update)` không kiểm tra trạng thái hiện tại trong filter MongoDB. Nếu độc giả hủy yêu cầu `Pending` đồng thời Thủ thư bấm *Duyệt*, trạng thái `Cancelled` sẽ bị ghi đè ngược thành `Approved`.
- **Hướng sửa:** Gộp điều kiện trạng thái vào filter:
  - `ApproveAsync` / `RejectAsync`: `r => r.Id == id && r.Status == ReservationStatus.Pending`
  - `CancelByLibrarianAsync`: `r => r.Id == id && r.Status == ReservationStatus.Approved`
  - `CancelByReaderAsync`: `r => r.Id == reservationId && r.ReaderId == readerId && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Approved)`
  - Kiểm tra `ModifiedCount == 1`, nếu `0` thì báo *"Yêu cầu vừa thay đổi trạng thái, vui lòng tải lại trang"*.

---

### `BUG-07` — `ApproveAsync` chưa kiểm tra lại trạng thái thẻ độc giả ở server
- **Mức độ:** Trung bình | **UC:** UC10
- **Vị trí:** `Services/ReservationService.cs` (dòng `420–436`)
- **Vấn đề:** Nếu thẻ độc giả bị khóa hoặc hết hạn sau thời điểm đặt trước, giao diện chỉ hiện cảnh báo nhưng server vẫn cho phép duyệt sang `Approved`.
- **Hướng sửa:** Đọc lại `Reader` theo `res.ReaderId` trong `ApproveAsync`; nếu `CardStatus == CardStatus.Locked || IsExpired` thì trả `ServiceResult.Fail(...)` (hoặc ghi chú rõ trong đặc tả nếu cố ý cho phép Thủ thư vẫn duyệt).

---

### `BUG-08` — Trang Sách yêu thích có thể hiển thị nhãn *"Đang giữ cho bạn"* cho bản giữ đã quá hạn
- **Mức độ:** Nhỏ | **Module:** Sách yêu thích
- **Vị trí:** `Services/FavoriteService.cs` (dòng `104–108`)
- **Hướng sửa:** Đổi điều kiện lọc `Held` thành `(r.Status == ReservationStatus.Held && r.HoldUntil >= DateHelper.StartOfTodayVnUtc())` (hoặc gọi `ExpireHoldsAsync()`).

---

### `BUG-09` — Câu báo lỗi khi giảm tổng số lượng sách chưa nhắc tới sách đang đặt giữ (`Held`)
- **Mức độ:** Nhỏ | **UC:** UC04
- **Vị trí:** `Services/BookService.cs` (dòng `171–176`)
- **Hướng sửa:** Đổi thông báo từ `"số sách đang cho mượn ({currentlyBorrowed} cuốn)"` thành `"số sách đang cho mượn và đặt giữ ({currentlyBorrowed} cuốn)"`.

---

### `BUG-10` — Thanh cảnh báo `CardNotice` không tự cập nhật ngay trong phiên đăng nhập hiện tại
- **Mức độ:** Nhỏ | **UC:** UC01
- **Vị trí:** `Controllers/AccountController.cs` (dòng `61–62`)
- **Mô tả:** `CardNotice` lưu trong Cookie Claim lúc đăng nhập nên chỉ đổi khi Độc giả đăng xuất đăng nhập lại (nghiệp vụ server vẫn đọc DB trực tiếp nên không bị lọt quyền).
- **Hướng sửa (tùy chọn):** Đọc trạng thái thẻ trực tiếp hoặc qua ViewComponent nhẹ ở `_Layout.cshtml` thay vì lưu tĩnh trong Claim.

---

## 3. Lưu ý môi trường kiểm thử

- Khi chạy script kiểm thử tự động `Antigravity/qa_verify.py` lúc 20:26 ngày 23/11/2026, dịch vụ Windows **`MongoDB` đang ở trạng thái `Stopped`** (`ECONNREFUSED 127.0.0.1:27017`).
- Sau khi sửa các lỗi trên, bật lại MongoDB (PowerShell Run as Administrator) và chạy kiểm thử hồi quy:
  ```powershell
  Start-Service MongoDB
  python D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_verify.py
  ```

---

## 4. Kết quả sửa lỗi (24/11/2026)

Đã sửa toàn bộ `BUG-01` → `BUG-10` và điểm `R36b` (độc giả có phiếu quá hạn không được đặt trước). Riêng `BUG-02`: phần "lệch mốc cuối ngày" không làm sai kết quả (`borrowDate` lưu 00:00 giờ VN), chỉ viết lại mốc thành `$lt` 00:00 ngày hôm sau cho rõ; phần báo cáo tiền phạt đổi sang lọc theo **ngày trả** (đã ghi vào UC12). Tài liệu cập nhật trước: QĐ09, UC04, UC10 (4a, 5b), UC12, UC16 bước 2, mục 6.3 `thiet-ke-ky-thuat.md`.

Kiểm chứng trên database riêng (đã xóa sau khi thử), app chạy với `FinePerDay = 7000` để chứng minh báo cáo quá hạn đọc cấu hình: **26/26 Đạt** — BUG-01, BUG-02 (lọc hôm nay / theo ngày mượn / 30 ngày trước / không lọc, hồi quy Top sách, theo tháng, theo thể loại), BUG-04 (+ hồi quy xóa sách bình thường), BUG-05 (hộp xác nhận và tạo đặt giữ khi bản giữ cuối quá hạn), BUG-06 (duyệt lại bị chặn, hồi quy duyệt / từ chối / hủy), BUG-07 (thẻ khóa, thẻ hết hạn), BUG-08, BUG-09, BUG-10 (khóa thẻ → sau 1 phút hiện cảnh báo; mở khóa → sau 1 phút mất cảnh báo; Thủ thư không ảnh hưởng), R36b (+ hồi quy độc giả bình thường). Tồn kho không lệch, không có đặt trước mồ côi.

---

## 5. Xác minh độc lập cuối cùng (QA Sign-off — 24/11/2026)

- **Trạng thái toàn bộ 10 lỗi (`BUG-01` → `BUG-10`) + `R05` + `R36b`:** **ĐÃ ĐÓNG 100% (VERIFIED PASS)** qua kiểm tra trực tiếp mã nguồn (`git diff`) và chạy thực tế trên server cô lập `http://localhost:5199`.
- **Kết quả hồi quy tự động toàn hệ thống:** **`100 / 100 PASS (100%)`**
  - Role Độc giả (`Antigravity/qa_reader_suite.py`): **48 / 48 PASS** (`R01`–`R47`, gồm `R05`, `R36b`, `R38`/BUG-05, UC14 `RenewalHistory` & `History`, `R47` toàn vẹn tồn kho).
  - Role Admin / Thủ thư (`Antigravity/qa_admin_suite.py`): **47 / 47 PASS** (`A01`–`A47`, gồm `A16`/BUG-04, `A38`/BUG-03, `A42`/BUG-07, `A46`/BUG-02, `A47` toàn vẹn tồn kho).
  - Xác minh bổ sung từng bản sửa (`Antigravity/qa_bugfixes_verify.py`): **5 / 5 PASS** (`F01`/BUG-01 chạy thật với `LibraryRules__FinePerDay=7000` tính đúng `13 ngày × 7.000 = 91.000 đ`; `F02`/BUG-06; `F03`/BUG-08; `F04`/BUG-09; `F05`/BUG-10 chờ đủ 62 giây xác nhận cookie `CardNotice` tự làm mới).
- **Database thật `library_management` (kiểm toán chỉ đọc):** 60 đầu sách · 60 ảnh bìa GridFS (`covers.files`, `0` thiếu) · 12 custom index · `0` lệch tồn kho · `0` thể loại mồ côi.
- Xem báo cáo tổng kết đầy đủ tại `Antigravity/bao-cao-kiem-thu-cuoi-cung.md`.
