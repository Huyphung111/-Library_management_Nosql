# Báo cáo Kiểm thử Nghiệp vụ Toàn hệ thống — LibraryManagement

- **Ngày kiểm thử:** 23/11/2026
- **Commit kiểm tra:** `cfd68a4` (*Sach yeu thich, dat giu dang hop, lap phieu muon 2 tab va viec can xu ly*)
- **Phạm vi:** Toàn bộ 16 Use Case (UC01–UC16), 4 tính năng mở rộng (Trang chủ công khai, Đặt giữ online `Held` + `HoldExpiryService`, Sách yêu thích, Tải ảnh bìa từ URL), bảo mật (RBAC, CSRF, SSRF, IDOR) và kiểm thử live HTTP (`Antigravity/qa_verify.py`).
- **Trạng thái môi trường lúc chạy live:** Ứng dụng `LibraryManagement.Web` đang chạy trên `http://localhost:5101`, tuy nhiên dịch vụ Windows `MongoDB` đang ở trạng thái **`Stopped`** (`ECONNREFUSED 127.0.0.1:27017`). Do đó:
  - Đã kiểm thử thực tế trực tiếp các luồng **RBAC cho khách chưa đăng nhập** (6/6 PASS) và **xử lý route / ObjectId sai định dạng**.
  - Các luồng đọc/ghi dữ liệu trên MongoDB được kiểm định sâu **từng dòng code** trên toàn bộ 14 Service, 16 Controller, View và JavaScript.

---

## 1. Bảng tổng hợp kết quả theo Use Case & Tính năng

| Mã UC / Module | Tên chức năng | Trạng thái | Tóm tắt đánh giá |
|---|---|---|---|
| **UC01 – UC03** | Đăng nhập, Đăng xuất, Đổi mật khẩu | **PASS** *(1 lưu ý nhẹ)* | BCrypt, thông báo chung khi sai mật khẩu, logout an toàn khi hết phiên, `CardNotice` cho thẻ khóa/hết hạn. |
| **UC04** | Quản lý sách & Ảnh bìa GridFS | **CẦN SỬA** | Upload file + tải ảnh từ URL chống SSRF rất tốt; còn sót case xóa sách đang có đặt trước `Pending`/`Approved` và câu báo lỗi khi giảm số lượng. |
| **UC05** | Quản lý thể loại | **PASS** | Unique index collation `vi` strength 2 (trùng không phân biệt hoa thường), chặn xóa khi còn sách. |
| **UC06** | Quản lý độc giả | **PASS** | Transaction tạo reader + user, đồng bộ họ tên sang `users`/`loans`, chống bấm đúp khi gia hạn thẻ. |
| **UC07** | Lập phiếu mượn (2 tab + Hộp Mẫu 1) | **PASS** | Transaction trừ kho + `ConsumeHoldAsync` + `CompleteForLoanAsync`; đã vá tốt giới hạn `unpickedHeld`; giao diện 2 tab & hộp Mẫu 1 chặt chẽ. |
| **UC08** | Nhận trả sách | **PASS** | Trả từng phần bằng `arrayFilters`, tính phạt đúng giờ VN, mất sách trừ `TotalQuantity`, hiển thị danh sách người chờ sau trả. |
| **UC09 & UC15** | Gia hạn phiếu mượn (Thủ thư & Độc giả) | **CẦN SỬA** | Chống gửi trùng & duyệt đúp rất tốt; còn 1 lỗi phiếu đã `Returned` vẫn xuất hiện ở `GetPendingRenewalsAsync()`. |
| **UC10 & UC16** | Đặt giữ online & Đặt trước sách | **CẦN SỬA** | Transaction `Held` / `ReleaseHold` + tác vụ nền `HoldExpiryService` tốt; còn 3 điểm ở `ExpireHoldsAsync`, filter trạng thái chống race condition và kiểm tra thẻ khi duyệt. |
| **UC11 & UC13** | Tìm kiếm & Tra cứu sách (khách + độc giả) | **PASS** | Đã escape regex, chặn ObjectId sai định dạng, phân trang gọn, lọc quá hạn chuẩn múi giờ VN. |
| **UC12** | Báo cáo & Thống kê + Dashboard | **CẦN SỬA** | Aggregation pipeline đúng `timezone: "Asia/Ho_Chi_Minh"`, Dashboard bổ sung mục đặt giữ tốt; còn lỗi hardcode `5000L` và mốc cuối ngày `toDate`. |
| **Mở rộng 1** | Trang chủ công khai (`Home/Landing`) | **PASS** | Khách xem sách & ảnh bìa `/Covers/{id}` bình thường; toàn bộ trang quản lý & độc giả đều redirect về `/Account/Login`. |
| **Mở rộng 2** | Sách yêu thích (`FavoriteService`) | **PASS** *(2 lưu ý nhẹ)* | `$addToSet` nguyên tử + giới hạn 50 cuốn, tự dọn sách đã xóa khỏi kho, AJAX + fallback không JS chuẩn. |
| **Bảo mật** | RBAC, Anti-CSRF, Anti-IDOR, Anti-SSRF | **PASS** | Đủ `[Authorize(Roles = ...)]`, `[ValidateAntiForgeryToken]`, `ReaderId` lấy từ Claim, tải ảnh URL chặn mạng nội bộ. |

---

## 2. Những gì đã đạt được (Điểm mạnh nổi bật)

1. **Thiết kế MongoDB & Transaction bài bản**:
   - Áp dụng transaction nguyên tử cho toàn bộ 5 thao tác nhiều bảng/nhiều bản ghi: tạo độc giả + tài khoản, đồng bộ đổi tên độc giả sang `users` & `loans`, lập phiếu mượn (trừ kho có điều kiện `availableQuantity > 0` + tiêu thụ `Held` + hoàn tất `Pending/Approved`), nhận trả từng phần bằng `arrayFilters`, và đặt giữ/hủy giữ sách (`Held`).
   - Sử dụng `FindOneAndUpdate` + `$inc` cho bộ đếm mã (`DG000001`, `PM000001`) và `$addToSet` + `Exists("favoriteBookIds.49", false)` cho sách yêu thích — không có race condition.
2. **Xử lý ngày giờ Việt Nam nhất quán**:
   - Tập trung toàn bộ qua `DateHelper` (mọi ngày lưu 00:00 giờ VN quy ra UTC) và truyền `timezone: "Asia/Ho_Chi_Minh"` trong Aggregation Pipeline của MongoDB.
3. **Vá tốt bài toán giới hạn 5 cuốn khi đặt giữ**:
   - `LoanService.CreateAsync` (dòng 228–232) và JS `Views/Loans/Create.cshtml` (dòng 284–289) đã tính đầy đủ `BorrowingBooks + ids.Count + unpickedHeld <= MaxBooksPerReader`, chặn trường hợp bỏ tick sách đang giữ rồi chọn sách khác vượt ngưỡng 5 cuốn.
4. **Bảo mật tốt vượt mức đồ án sinh viên thông thường**:
   - Tính năng tải ảnh bìa từ URL (`BookService.DownloadCoverAsync`) kiểm tra kỹ scheme `http/https`, phân giải DNS chặn toàn bộ IP private/loopback/link-local/CGNAT (chống SSRF), giới hạn tối đa 5 redirect, giới hạn stream đọc 2 MB không tin header `Content-Length`, và kiểm tra magic bytes JPG/PNG/WebP.
   - Mọi thao tác phía Độc giả (`MyLoans`, `MyReservations`, `MyFavorites`) chỉ dùng `ReaderId` từ Cookie Claim, không nhận từ URL/form.
5. **Trải nghiệm người dùng (UX) mượt mà và đồng bộ**:
   - Giao diện HUIT Navy `#0f2a47` / `#1e3a5f` + Vàng đồng `#c8963e` thống nhất từ Trang chủ khách → Đăng nhập → Độc giả → Thủ thư.
   - Hỗ trợ AJAX mượt (nút trái tim, hộp xác nhận đặt giữ, `CheckReader`, hộp Phiếu mượn Mẫu 1) kèm fallback vẫn chạy được nếu trình duyệt tắt JavaScript.

---

## 3. Danh sách các lỗi cần sửa (Bugs)

### Mức ưu tiên CAO / TRUNG BÌNH (Nên sửa trước khi nộp)

#### `BUG-01` [UC12] Hardcode mức phạt `5000L` trong Báo cáo quá hạn
- **Vị trí:** `Services/ReportService.cs` — dòng `209` (`var estimatedFine = 5000L * overdueDays * overdueBooksCount;`)
- **Mô tả:** Viết cứng `5000L` thay vì inject `IOptions<LibraryRules>` (`_rules.FinePerDay`). Vi phạm quy định cấu hình tập trung trong `Options/LibraryRules.cs`.
- **Tác động:** Nếu giảng viên hoặc quản trị viên đổi `FinePerDay` trong `appsettings.json` (ví dụ thành `10000`), tiền phạt ước tính trên báo cáo quá hạn sẽ sai lệch hoàn toàn với tiền phạt thực thu lúc trả sách.
- **Cách sửa:** Inject `IOptions<LibraryRules> rules` vào constructor `ReportService`, dùng `_rules.FinePerDay * overdueDays * overdueBooksCount`.

#### `BUG-02` [UC12] Lệch mốc cuối ngày `toDate` và sai tiêu chí lọc ngày ở Báo cáo tiền phạt
- **Vị trí:** `Services/ReportService.cs` — dòng `127` (`GetTopBooksAsync`), dòng `249` (`GetBorrowByMonthAsync`), dòng `324` (`GetBorrowByCategoryAsync`), dòng `389–392` (`GetFinesAsync`).
- **Mô tả:**
  1. Code dùng `DateHelper.VnDateToUtc(toDate.Value.AddDays(1).AddSeconds(-1))`, nhưng bên trong `DateHelper.VnDateToUtc()` có gọi `vnDate.Date` nên phần `23:59:59` bị cắt bỏ → mốc lọc thực tế chỉ đến `00:00:00` ngày `toDate`. Nếu có bản ghi `borrowDate` chứa giờ khác `00:00` giờ VN (ví dụ dữ liệu nhập tay), bản ghi trong ngày `toDate` sẽ bị sót.
  2. Trong `GetFinesAsync`, khoảng thời gian `fromDate` – `toDate` đang lọc theo **`BorrowDate` (ngày mượn)** thay vì **`items.returnDate` (ngày trả sách — ngày thực tế phát sinh/thu tiền phạt)**.
- **Cách sửa:**
  - Dùng điều kiện `$lt DateHelper.VnDateToUtc(toDate.Value.AddDays(1))` thay cho `$lte ...AddSeconds(-1)`.
  - Ở `GetFinesAsync`, lọc theo `item.ReturnDate` nằm trong `[fromUtc, toNextDayUtc)` để phản ánh đúng doanh thu phạt của tháng/khoảng thời gian được chọn.

#### `BUG-03` [UC09] Phiếu đã trả hết (`Returned`) vẫn hiện ở danh sách Gia hạn chờ duyệt
- **Vị trí:** `Services/LoanService.cs` — dòng `626–631` (`GetPendingRenewalsAsync`).
- **Mô tả:** `GetPendingRenewalsAsync()` chỉ lọc `ElemMatch(l => l.RenewalRequests, r => r.Status == RenewalStatus.Pending)` mà **không lọc `l.Status == LoanStatus.Borrowing`** (trong khi `ReportService.GetDashboardAsync` dòng `56–58` có lọc `Status == Borrowing`).
- **Kịch bản tái hiện:** Độc giả gửi yêu cầu gia hạn online (`Pending`), sau đó mang sách ra quầy trả hết trước khi Thủ thư duyệt/từ chối → Phiếu chuyển sang `Returned` nhưng `RenewalRequest` vẫn giữ `Pending` → Dashboard báo `0 chờ duyệt`, còn trang `/Renewals` vẫn hiển thị phiếu đã trả!
- **Cách sửa:** Thêm `Builders<Loan>.Filter.Eq(l => l.Status, LoanStatus.Borrowing)` vào filter của `GetPendingRenewalsAsync()` (và tốt hơn nữa: khi đóng phiếu sang `Returned` trong `LoanService.ReturnAsync`, tự chuyển các `RenewalRequests` còn `Pending` sang `Rejected` với lý do *"Phiếu đã trả hết sách"*).

#### `BUG-04` [UC04] Xóa sách chưa xử lý yêu cầu Đặt trước (`Pending` / `Approved`)
- **Vị trí:** `Services/BookService.cs` — dòng `213–231` (`DeleteAsync`).
- **Mô tả:** Đã chặn xóa khi sách đang có phiếu mở (`HasOpenLoanForBookAsync`) hoặc đang có `ReservationStatus.Held`, nhưng chưa chặn hoặc hủy các yêu cầu đặt trước `Pending` / `Approved` trỏ tới `bookId`.
- **Tác động:** Sau khi xóa sách, độc giả và thủ thư vẫn thấy yêu cầu đặt trước của cuốn sách đã bị xóa; bấm xem chi tiết sẽ báo không tìm thấy sách.
- **Cách sửa:** Kiểm tra thêm `Pending` / `Approved` trong `DeleteAsync` để chặn xóa (hoặc tự động chuyển sang `Cancelled` với lý do *"Đầu sách đã bị xóa khỏi danh mục"* trong cùng transaction).

#### `BUG-05` [UC16] Chưa gọi `ExpireHoldsAsync()` trước khi kiểm tra kho lúc Đặt giữ / Mở hộp xác nhận
- **Vị trí:** `Services/ReservationService.cs` — dòng `214–241` (`GetBookForReservationConfirmAsync`) và dòng `250–296` (`CreateAsync`).
- **Mô tả:** Nếu bản giữ cuối cùng của sách đã qua ngày `HoldUntil` nhưng tác vụ nền `HoldExpiryService` (chạy 5 phút/lần) chưa kịp quét, độc giả bấm vào sách vẫn thấy `AvailableQuantity == 0` và bị đẩy vào hàng chờ `Pending` thay vì được đặt giữ `Held` ngay.
- **Cách sửa:** Gọi `await ExpireHoldsAsync();` ở đầu `GetBookForReservationConfirmAsync` và `CreateAsync` (tương tự cách đã làm ở `GetMyReservationsAsync`, `GetLibrarianListAsync` và `CheckBorrowerAsync`).

#### `BUG-06` [UC10 & UC16] Cập nhật trạng thái đặt trước thiếu điều kiện trạng thái trong filter (Race Condition)
- **Vị trí:** `Services/ReservationService.cs` — `CancelByReaderAsync` (dòng `337`), `ApproveAsync` (dòng `434`), `RejectAsync` (dòng `459`), `CancelByLibrarianAsync` (dòng `491`).
- **Mô tả:** Code kiểm tra `res.Status` ở tầng C# rồi gọi `UpdateOneAsync(r => r.Id == id, update)` không kèm điều kiện trạng thái cũ.
- **Tác động:** Nếu độc giả tự hủy yêu cầu `Pending` đúng lúc Thủ thư bấm *Duyệt* (hoặc 2 tab bấm cùng lúc), lệnh sau sẽ ghi đè trạng thái đã hủy thành `Approved`.
- **Cách sửa:** Gộp điều kiện trạng thái vào filter `UpdateOneAsync`, ví dụ: `r => r.Id == id && r.Status == ReservationStatus.Pending` và kiểm tra `result.ModifiedCount == 1`.

#### `BUG-07` [UC10] Thủ thư duyệt đặt trước (`ApproveAsync`) chưa chặn thẻ bị khóa / hết hạn ở server
- **Vị trí:** `Services/ReservationService.cs` — dòng `420–436`.
- **Mô tả:** Nếu độc giả đặt trước lúc thẻ còn tốt, sau đó thẻ bị khóa hoặc hết hạn, server `ApproveAsync` vẫn cho phép duyệt sang `Approved` mà không kiểm tra lại hồ sơ `readers`.
- **Cách sửa:** Xác định rõ theo đặc tả UC10 bước 4a: nếu yêu cầu *không được duyệt* khi thẻ khóa/hết hạn thì thêm kiểm tra `reader.CardStatus == CardStatus.Locked || reader.IsExpired` ngay trong `ApproveAsync`; nếu chỉ cảnh báo thì ghi rõ chú thích trong code và tài liệu.

---

### Mức ưu tiên NHỎ / CẬN BIÊN (Minor)

#### `BUG-08` [Yêu thích] Sách hết hạn giữ có thể còn hiển thị nhãn *"Đang giữ cho bạn"* tối đa 5 phút
- **Vị trí:** `Services/FavoriteService.cs` — dòng `104–108` (`GetListAsync`).
- **Mô tả:** Truy vấn `ReservationStatus.Held` chưa lọc `r.HoldUntil >= DateHelper.StartOfTodayVnUtc()`. Chỉ cần thêm điều kiện `HoldUntil >= DateHelper.StartOfTodayVnUtc()` là nhãn hiển thị luôn chính xác tức thì.

#### `BUG-09` [UC04] Câu thông báo lỗi khi giảm tổng số lượng sách có thể gây hiểu nhầm khi có bản `Held`
- **Vị trí:** `Services/BookService.cs` — dòng `171–176`.
- **Mô tả:** `currentlyBorrowed = existingBook.TotalQuantity - existingBook.AvailableQuantity` thực chất là **đang mượn + đang đặt giữ online (`Held`)**, nhưng câu báo lỗi chỉ ghi *"số sách đang cho mượn"*. Nên sửa thành *"số sách đang cho mượn và đặt giữ"*.

#### `BUG-10` [UC01] Banner cảnh báo `CardNotice` không tự cập nhật ngay trong phiên đang đăng nhập
- **Vị trí:** `Controllers/AccountController.cs` — dòng `61–62`.
- **Mô tả:** `CardNotice` lưu trong Cookie Claim lúc đăng nhập. Nếu Thủ thư mở khóa hoặc gia hạn thẻ khi Độc giả đang mở web, thanh cảnh báo vẫn hiện cho đến khi Độc giả đăng xuất đăng nhập lại (tuy nhiên các thao tác mượn/gia hạn/đặt sách ở server vẫn hoạt động đúng vì đọc trực tiếp DB).

---

## 4. Những thứ cần lưu ý & cân nhắc trước khi hoàn thiện / nộp đồ án

1. **Khởi động lại dịch vụ MongoDB và chạy kiểm thử live toàn phần**:
   - Lúc kiểm thử, Windows Service `MongoDB` đang ở trạng thái `Stopped`. Hãy mở PowerShell (Run as Administrator) chạy:
     ```powershell
     Start-Service MongoDB
     python D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_verify.py
     ```
   - Khi MongoDB bật lại, `qa_verify.py` sẽ tự động kiểm tra luôn **bảo toàn tồn kho của cả 60 đầu sách** (`totalQuantity - availableQuantity == đang mượn + đang giữ`), kiểm tra đủ index, ảnh bìa GridFS, và chạy toàn bộ 63 kiểm thử HTTP live.
2. **Đồng bộ 32 ảnh bìa mới & xuất lại `Data/MongoData/*.json`**:
   - Hiện `Data/MongoData/books.json` vẫn chỉ chứa 28 sách cũ (trong khi DB đã có 60 sách) và 7 file JSON trong `Data/MongoData/` đang ở trạng thái `modified` chưa commit.
   - 32 ảnh bìa đã cào & duyệt trong `Antigravity/BookCovers/` nên được chép sang `Data/SeedCovers/` và cập nhật thay thế `extra-00..31.jpg` trong GridFS, sau đó chạy `export.js` để 2 thành viên và giảng viên nhập dữ liệu mẫu là có ngay đủ 60 đầu sách + ảnh bìa thật.
3. **Bổ sung tài liệu trong `Doc/` cho khớp tính năng thực tế**:
   - Nên cập nhật ngắn gọn 4 mục mới vào `Doc/dac-ta-use-case.md` và `Doc/thiet-ke-ky-thuat.md`: Trang chủ công khai, Đặt giữ online (`Held` + `HoldExpiryService`), Sách yêu thích (`readers.favoriteBookIds`), và Tải ảnh bìa từ URL (chống SSRF). Đây đều là điểm cộng lớn khi chấm đồ án NoSQL.
4. **Dọn dẹp thư mục nháp trước khi merge lên `main`**:
   - Kiểm tra các thư mục chưa track (`ThamKhảo/`, `../output/`) và quyết định giữ hay đưa `Antigravity/` vào `.gitignore` khi đóng gói bản nộp cuối.

---

## 5. Gợi ý lộ trình xử lý ưu tiên

1. **Ưu tiên 1 (~20 phút — vá các lỗi nghiệp vụ thực tế):**
   - Sửa `BUG-01` & `BUG-02` trong `ReportService.cs` (inject `LibraryRules`, chuẩn hóa mốc `$lt toDate + 1 ngày`, lọc tiền phạt theo `ReturnDate`).
   - Sửa `BUG-03` trong `LoanService.cs` (lọc `Status == Borrowing` ở `GetPendingRenewalsAsync` + đóng yêu cầu gia hạn `Pending` khi trả hết sách).
   - Sửa `BUG-04` (`BookService.DeleteAsync`), `BUG-05` & `BUG-06` (`ReservationService.cs`).
2. **Ưu tiên 2 (~10 phút — chạy lại kiểm thử tự động):**
   - Bật lại service `MongoDB` (`Start-Service MongoDB`), chạy `python Antigravity/qa_verify.py` và kiểm tra kết quả 63/63 PASS + 0 lỗi toàn vẹn MongoDB.
3. **Ưu tiên 3 (~15 phút — hoàn thiện dữ liệu & hồ sơ nộp bài):**
   - Nạp 32 ảnh bìa thật từ `Antigravity/BookCovers/` vào `Data/SeedCovers/` & GridFS, chạy `export.js` cập nhật `Data/MongoData/`, cập nhật thêm các tính năng mới vào `Doc/`.
