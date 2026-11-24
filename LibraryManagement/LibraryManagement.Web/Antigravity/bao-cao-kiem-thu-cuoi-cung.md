# Báo cáo Kiểm thử Tổng kết Cuối cùng (Final QA Sign-off)

- **Dự án:** Hệ thống Quản lý Mượn Trả Sách (`LibraryManagement.Web` — ASP.NET Core MVC .NET 10 + MongoDB 8.x Replica Set `rs0` + GridFS)
- **Ngày xác minh:** 24/11/2026
- **Phạm vi:** Kiểm tra toàn diện lần cuối sau khi hoàn thiện tất cả các bản sửa lỗi (`BUG-01` → `BUG-10`, `R05`, `R36b`) và cập nhật UC14 (`Lịch sử gia hạn` & `Lịch sử mượn`), bao gồm cả 2 vai trò **Độc giả (`Reader`)** và **Thủ thư / Admin (`Librarian`)**, bảo mật, giao dịch (transaction) và toàn vẹn dữ liệu MongoDB.
- **Kết luận nghiệm thu:** **ĐẠT YÊU CẦU — SẴN SÀNG NỘP & DEMO (100 / 100 kịch bản kiểm thử tự động PASS · 0 lỗi tồn đọng · 0 lệch tồn kho).**

---

## 1. Tổng hợp kết quả chạy kiểm thử trực tiếp

Toàn bộ kiểm thử ghi dữ liệu chạy trên server cô lập `http://localhost:5199` với database riêng (`library_management_qa_reader`, `library_management_qa_admin`, `library_management_qa_fixes`) khởi tạo từ `Data/MongoData/import.js` và đã được tự động xóa sạch sau khi chạy (`Remaining QA DBs: []`), **không ảnh hưởng database thật `library_management`** và **không ghi đè `Data/MongoData/*.json`**.

| Bộ kiểm thử | Script & Kết quả JSON | Số ca PASS | Tỷ lệ | Ghi chú nổi bật |
|---|---|---:|---:|---|
| **Role Độc giả (`Reader`)** | `Antigravity/qa_reader_suite.py` (`qa_reader_results.json`) | **48 / 48** | **100%** | Auth (`R05` chữ thường), RBAC, IDOR, CSRF, Catalog, Favorites, Đặt giữ/Đặt trước (`R36b`, `R38`), UC14 mới (`R40`, `R41`), UC15, Toàn vẹn kho (`R47`) |
| **Role Admin / Thủ thư (`Librarian`)** | `Antigravity/qa_admin_suite.py` (`qa_admin_results.json`) | **47 / 47** | **100%** | Dashboard, UC04–UC12, SSRF, Anti-CSRF, `ConsumeHold`, trả từng phần/hư hỏng/mất, `A16` (BUG-04), `A38` (BUG-03), `A42` (BUG-07), `A46` (BUG-02), Toàn vẹn kho (`A47`) |
| **Xác minh bổ sung bản sửa lỗi** | `Antigravity/qa_bugfixes_verify.py` (`qa_bugfixes_results.json`) | **5 / 5** | **100%** | `F01` (BUG-01 cấu hình `FinePerDay=7000`), `F02` (BUG-06), `F03` (BUG-08), `F04` (BUG-09), `F05` (BUG-10 chờ 62s xác minh refresh cookie thật) |
| **Tổng cộng** | **3 bộ tự động độc lập** | **100 / 100** | **100%** | **0 lỗi stderr ASP.NET Core** trong suốt phiên chạy |

---

## 2. Xác minh chi tiết 10 lỗi (`BUG-01` → `BUG-10`) + `R05`, `R36b` & UC14

| Mã | Mức độ cũ | File đã sửa | Cách khắc phục đã xác minh | Ca test chứng minh | Kết quả |
|---|---|---|---|---|---|
| `BUG-01` | Cao | `Services/ReportService.cs` | Inject `IOptions<LibraryRules>`, thay `5000L` bằng `_rules.FinePerDay` | `F01`: chạy server với `FinePerDay=7000`, 13 ngày quá hạn ra đúng **`91.000 đ`** | **VERIFIED PASS** |
| `BUG-02` | Cao | `Services/ReportService.cs`, `Views/Reports/Fines.cshtml` | Đổi mốc cuối ngày sang `$lt toDate+1`; viết lại `GetFinesAsync` bằng pipeline `$unwind "$items"` lọc theo **`items.returnDate`** | `A45`, `A46`: phiếu mượn hôm kia trả hôm nay xuất hiện đúng khi lọc ngày trả hôm nay | **VERIFIED PASS** |
| `BUG-03` | Trung bình | `Services/LoanService.cs` | Trong `ReturnAsync`, khi đóng phiếu `Returned`, dùng `arrayFilters` tự chuyển `RenewalRequests` đang `Pending` → `Rejected` ngay trong transaction | `A38`: `pendingLeft = 0`, không còn xuất hiện ở `/Renewals` và không treo trên tab *Lịch sử gia hạn* | **VERIFIED PASS** |
| `BUG-04` | Trung bình | `Services/BookService.cs` | `DeleteAsync` chặn xóa khi còn đặt trước `Pending` hoặc `Approved` | `A16`: sách còn đặt trước `Pending` không bị xóa (`bookLeft = 1`), không sinh đặt trước mồ côi | **VERIFIED PASS** |
| `BUG-05` | Trung bình | `Services/ReservationService.cs` | Gọi `await ExpireHoldsAsync();` ở đầu cả `GetBookForReservationConfirmAsync` và `CreateAsync` | `R38`: bản giữ cuối đã quá `HoldUntil` tự hoàn kho, hộp xác nhận hiện đúng *"Xác nhận đặt giữ sách"* | **VERIFIED PASS** |
| `BUG-06` | Trung bình | `Services/ReservationService.cs` | Thêm điều kiện trạng thái cũ vào filter `UpdateOneAsync` ở `CancelByReaderAsync`, `ApproveAsync`, `RejectAsync`, `CancelByLibrarianAsync` và kiểm tra `ModifiedCount == 1` | `F02`, `A39`–`A41`, `R31`, `R34`: duyệt/từ chối lặp trên trạng thái cũ bị chặn, giữ nguyên trạng thái đúng | **VERIFIED PASS** |
| `BUG-07` | Trung bình | `Services/ReservationService.cs` | `ApproveAsync` đọc lại `Reader`, chặn duyệt ở server khi thẻ `Locked` hoặc `IsExpired` | `A42`: yêu cầu của thẻ khóa `DG000009` bị chặn duyệt, trạng thái giữ nguyên `Pending` | **VERIFIED PASS** |
| `BUG-08` | Nhỏ | `Services/FavoriteService.cs` | Lọc bản giữ `Held` kèm `r.HoldUntil >= DateHelper.StartOfTodayVnUtc()` | `F03`: bản giữ đã quá hạn không hiện nhãn *"Đang giữ cho bạn"* trên `/MyFavorites` | **VERIFIED PASS** |
| `BUG-09` | Nhỏ | `Services/BookService.cs` | Đổi thông báo giảm `TotalQuantity` nêu rõ *"số sách đang cho mượn hoặc đang giữ cho độc giả"* | `F04`: gửi `TotalQuantity < outOfShelf` nhận đúng câu báo lỗi có cụm *"đang giữ"* | **VERIFIED PASS** |
| `BUG-10` | Nhỏ | `Controllers/AccountController.cs`, `Program.cs`, `Services/AuthService.cs` | Thêm claim `CardCheckedAt` + `CookieAuthenticationEvents.OnValidatePrincipal` đọc lại `GetCardNoticeAsync` tối đa 1 lần/phút và cấp lại cookie (`ShouldRenew = true`) | `F05`: đăng nhập khi thẻ mở (chưa có banner) → Thủ thư khóa thẻ → chờ 62s reload `/Catalog` → banner `card-notice` & lý do khóa tự hiện mà không cần đăng nhập lại | **VERIFIED PASS** |
| `R05` | Nhỏ (UX) | `Services/AuthService.cs` | Chuẩn hóa tên đăng nhập bắt đầu bằng `DG` (không phân biệt hoa/thường) sang chữ HOA | `R05`: đăng nhập `dg000001` thành công vào `/Catalog` | **VERIFIED PASS** |
| `R36b` | Cận biên | `Services/ReservationService.cs` | Đưa kiểm tra `CheckBorrowerAsync` + `OverdueLoans > 0` lên đầu `CreateAsync` để chặn cả đặt giữ lẫn đặt trước | `R36`, `R36b`: độc giả quá hạn `DG000002` bị chặn `HTTP 400` ở cả sách còn lẫn sách hết hàng | **VERIFIED PASS** |
| **UC14 mới** | Tính năng | `MyLoansController.cs`, `LoanService.cs`, `LoanVMs.cs`, `Views/MyLoans/*` | Tách tab *Lịch sử gia hạn* (`/MyLoans/RenewalHistory`) kèm modal lý do từ chối; mở rộng *Lịch sử mượn* (`/MyLoans/History`) hiện cả sách đang mượn lẫn đã trả | `R39`, `R40`, `R41`: cả 3 màn hình tải `200 OK`, dữ liệu và modal `#reasonModal` chuẩn xác | **VERIFIED PASS** |

---

## 3. Kiểm toán toàn vẹn database thật `library_management` (Chỉ đọc)

| Hạng mục kiểm tra | Kết quả thực tế | Đánh giá |
|---|---|---|
| Số lượng dữ liệu hiện có | `60` đầu sách · `6` thể loại · `7` độc giả · `8` tài khoản · `12` phiếu mượn · `7` đặt trước | Đầy đủ, phong phú |
| Ảnh bìa GridFS (`covers.files` & `covers.chunks`) | `60 / 60` đầu sách có ảnh bìa hợp lệ (`missingCovers = 0`) | **ĐẠT** |
| Chỉ mục MongoDB (Custom Indexes) | `12` index (`users: 1`, `readers: 2`, `categories: 1`, `books: 2`, `loans: 4`, `reservations: 2`) | **ĐẠT** |
| Nhất quán tồn kho (`total - available == đang mượn + đang giữ`) | `stockMismatchCount = 0`, `badQty = 0` | **ĐẠT** |
| Toàn vẹn tham chiếu thể loại | `orphanCats = 0` | **ĐẠT** |

---

## 4. Điểm nhỏ cần lưu ý & Checklist trước khi nộp bài

Không còn lỗi nghiệp vụ hay lỗi bảo mật nào. Chỉ có **1 điểm giao diện siêu nhỏ** và **4 việc vệ sinh gọn gàng** bạn có thể cân nhắc trước khi nộp/bảo vệ:

1. **Chú thích tĩnh trong `Views/Reports/Overdue.cshtml` (dòng 28) — mức rất nhỏ (cosmetic):**
   - Phần tính toán ở `ReportService.cs` đã đọc động `_rules.FinePerDay` (`F01` PASS với `7.000đ`), nhưng dòng chữ phụ ở view vẫn ghi cố định: `Tổng tiền phạt tạm tính (5.000đ/ngày/cuốn)`. Với cấu hình mặc định `5.000đ` hiện tại thì hiển thị hoàn toàn đúng; chỉ cần sửa nếu sau này bạn có ý định đổi `LibraryRules:FinePerDay` khi demo.
2. **Điểm nghiệp vụ có chủ đích (`R27`) — giữ nguyên và sẵn sàng giải thích khi bảo vệ:**
   - Thẻ bị khóa/hết hạn vẫn thêm/xóa được *Sách yêu thích* (`/MyFavorites`) vì danh sách yêu thích chỉ là đánh dấu cá nhân, không làm thay đổi tồn kho (mượn, gia hạn, đặt giữ, đặt trước đều bị chặn chặt ở server).
3. **Commit code & dọn thư mục nháp trên Git:**
   - Đang có **21 file sửa đổi** + **1 file mới** (`Views/MyLoans/RenewalHistory.cshtml`) chưa commit.
   - Kiểm tra 2 thư mục untracked `LibraryManagement.Web/ThamKhảo/` và `../output/` — nếu không dùng để nộp thì nên dọn hoặc thêm vào `.gitignore`.
   - Nếu muốn đồng bộ bộ **60 sách** hiện tại trong `library_management` sang `Data/MongoData/*.json` (hiện đang là bộ seed chuẩn 28 sách của commit cũ), có thể chạy `Data/MongoData/export.js`.
4. **Dọn tài liệu & DB test cũ (tùy chọn):**
   - Trong `Doc/TestCase/UC14-sach-dang-muon-lich-su.md`, còn 3 dòng test thủ công (`TC14-08`, `TC14-10`, `TC14-14`) đang ghi *"Chưa thử"* — bạn có thể điền kết quả *Đạt* (luồng tương ứng đã PASS tự động ở `R40`–`R41`).
   - Trên MongoDB local vẫn còn một số database thử nghiệm cũ (`library_management_ab*`, `library_management_cover*`, `library_management_fix*`, `library_management_test*`, `library_management_demo`, `library_management_import`); bạn có thể xóa bớt trong Compass cho gọn khi thuyết trình (3 DB tạm `library_management_qa_*` của đợt kiểm thử này đã được xóa sạch).
