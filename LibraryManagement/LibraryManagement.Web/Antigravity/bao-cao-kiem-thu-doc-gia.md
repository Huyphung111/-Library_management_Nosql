# Báo cáo Kiểm thử Chuyên sâu — Vai trò Độc giả (`Reader`)

- **Ngày kiểm thử:** 24/11/2026
- **Môi trường kiểm thử thực tế:**
  - Server cô lập `http://localhost:5199` kết nối database tạm `library_management_qa_reader` (khởi tạo từ `Data/MongoData/import.js`, dọn sạch sau khi test — **không ảnh hưởng dữ liệu thật `library_management`**).
  - Kiểm tra toàn vẹn trực tiếp trên database thật `library_management` (60 đầu sách, 60 ảnh bìa GridFS, 12 phiếu mượn, 7 đặt trước, 11/11 index).
- **Script kiểm thử tự động:** `Antigravity/qa_reader_suite.py` (kết quả JSON tại `Antigravity/qa_reader_results.json`).
- **Tổng kết:** **46 / 48 kịch bản PASS** · **2 FAIL thực tế** · **3 lưu ý nghiệp vụ cận biên** · **0 lỗi toàn vẹn dữ liệu MongoDB**.

---

## 1. Xác nhận các thay đổi bạn vừa cập nhật

### Đã kiểm chứng PASS trên phần mới sửa (UC14 — Sách của tôi)
- **Tách mục `Lịch sử mượn` (`/MyLoans/History`)**: Hiển thị đầy đủ **cả cuốn đang mượn lẫn cuốn đã trả**, tính chính xác `LateDays`, `IsOverdue`, số lần gia hạn và dòng **Tổng tiền phạt** → **`R40` PASS**.
- **Thêm tab `Lịch sử gia hạn` (`/MyLoans/RenewalHistory`)**: Hiển thị lịch sử yêu cầu gia hạn (cả *Online* và *Tại quầy*), có badge *Chờ duyệt / Thành công / Bị từ chối* và mở modal xem đúng lý do từ chối (`#reasonModal`) → **`R41` PASS**.

### Lưu ý về danh sách `BUG-01` → `BUG-10` từ báo cáo trước
- Qua kiểm tra `git diff`, các file `ReportService.cs`, `BookService.cs`, `ReservationService.cs`, `FavoriteService.cs` **chưa có chỉnh sửa** trên working tree hiện tại (các thay đổi vừa rồi chỉ tập trung ở `MyLoansController.cs`, `LoanService.cs` phần UC14, `LoanVMs.cs`, `Views/MyLoans/*`, `Doc/*`).
- Do đó, các lỗi `BUG-01` → `BUG-10` trong `Antigravity/bao-cao-loi.md` vẫn còn mở (đặc biệt `BUG-05` vừa bị tái hiện trực tiếp ở test case `R38` bên dưới).

---

## 2. Kết quả kiểm thử toàn vẹn MongoDB thật (`library_management`)

| Hạng mục kiểm tra | Kết quả |
|---|---|
| Số lượng bản ghi | 60 sách · 60 ảnh bìa GridFS (`covers.files` & `covers.chunks`) · 6 thể loại · 7 độc giả · 8 tài khoản · 12 phiếu mượn · 7 đặt trước |
| Index bắt buộc (11 index) | **Đạt 11/11** (`ux_username`, `ux_cardNumber`, `ux_idNumber`, `ux_name`, `ix_categoryIds`, `tx_title_authors`, `ux_loanCode`, `ix_reader_status`, `ix_status_dueDate`, `ix_itemsBookId_status`, `ix_book_status_reservedAt`) |
| Toàn vẹn tồn kho (`total - avail == đang mượn + đang giữ`) | **0 cuốn lệch** (`stockMismatchCount = 0`) |
| Ảnh bìa thiếu / lỗi | **0 cuốn thiếu** (`missingCoverCount = 0`) |
| Lỗi/cảnh báo cấu trúc dữ liệu | **0 lỗi, 0 cảnh báo** (`issues: []`, `warnings: []`) |

---

## 3. Bảng kết quả 48 kịch bản kiểm thử Role Độc giả

### Nhóm 1 — Tài khoản, Đăng nhập, Đăng xuất, Đổi mật khẩu (UC01 – UC03)
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R01` | Bỏ trống tên đăng nhập / mật khẩu | **PASS** | Giữ lại trang Login, hiện lỗi validation |
| `R02` | Sai mật khẩu | **PASS** | Hiện thông báo chung, không lộ tài khoản tồn tại |
| `R03` | Độc giả hoạt động (`DG000001`) đăng nhập | **PASS** | Chuyển `/Catalog`, không hiện `CardNotice` |
| `R04` | Nhập username có khoảng trắng đầu/cuối (`"  DG000001  "`) | **PASS** | Tự trim và đăng nhập bình thường |
| `R05` | Nhập mã thẻ chữ thường (`dg000001`) | **FAIL (Nhỏ)** | **Bị từ chối** vì `AuthService` so sánh phân biệt hoa/thường |
| `R06` | Thẻ bị khóa (`DG000009`) đăng nhập | **PASS** | Vẫn vào `/Catalog` + hiện thanh cảnh báo `card-notice` kèm lý do |
| `R07` | Thẻ hết hạn (`DG000010`) đăng nhập | **PASS** | Vẫn vào `/Catalog` + hiện thanh cảnh báo `card-notice` kèm ngày hết hạn |
| `R08` | Đổi MK: sai mật khẩu hiện tại | **PASS** | Chặn & báo lỗi |
| `R09` | Đổi MK: mật khẩu mới < 6 ký tự | **PASS** | Chặn & báo lỗi (QĐ10) |
| `R10` | Đổi MK: mật khẩu mới trùng mật khẩu cũ | **PASS** | Chặn & báo lỗi |
| `R11` | Đổi MK: xác nhận không khớp | **PASS** | Chặn & báo lỗi |
| `R12` | Đổi MK thành công (`DG000008`) | **PASS** | MK cũ bị từ chối, MK mới đăng nhập được |
| `R13` | POST `/Account/Logout` khi phiên đã hết hạn (không token) | **PASS** | Redirect về `/` (không báo lỗi 400, đúng UC02 bước 1a) |

### Nhóm 2 — Bảo mật: Phân quyền (RBAC), Chống IDOR, Chống CSRF
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R14` | Độc giả mở 10 trang Thủ thư (`/Dashboard`, `/Books`, `/Readers`, `/Loans/Create`, `/Reports/...`) | **PASS** | Cả 10/10 bị chặn `302 -> /Account/AccessDenied` |
| `R15` | Độc giả gửi POST vào action Thủ thư (`/Renewals/Approve`) | **PASS** | Bị chặn `AccessDenied` |
| `R16` | **Anti-IDOR**: `DG000001` thử GET `/MyLoans/RequestRenewal/{phiếu của người khác}` | **PASS** | Chặn & chuyển về `/MyLoans` |
| `R17` | **Anti-IDOR**: `DG000001` thử POST hủy đặt trước của `DG000002` | **PASS** | Chặn; trạng thái đặt trước của `DG000002` vẫn nguyên `Pending` |
| `R18` | **Anti-CSRF**: POST `/MyFavorites/Add` thiếu `__RequestVerificationToken` | **PASS** | Trả `HTTP 400 Bad Request` |

### Nhóm 3 — Tra cứu & Chi tiết sách (UC13)
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R19` | Tìm sách có ký tự regex đặc biệt `(test[+` & lọc thể loại hợp lệ | **PASS** | `HTTP 200`, không lỗi regex |
| `R20` | Lọc `categoryId=bad-id` sai định dạng ObjectId | **PASS** | `HTTP 200`, không lỗi 500 |
| `R21` | Xem `/Catalog/Details/{id}`, `/Catalog/Details/bad-id` và `/Covers/{id}` | **PASS** | Chi tiết `200`, `bad-id` quay về `/Catalog`, ảnh bìa `200 image/jpeg` |

### Nhóm 4 — Sách yêu thích (`MyFavorites`)
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R22` | Thêm sách yêu thích qua AJAX | **PASS** | Trả JSON `success: true, count: 1` |
| `R23` | Thêm trùng cùng 1 cuốn lần 2 | **PASS** | `$addToSet` không nhân bản, `count` giữ `1` |
| `R24` | Thêm với `bookId=bad-id` | **PASS** | Trả `HTTP 400` |
| `R25` | Xem trang `/MyFavorites` | **PASS** | Hiển thị đúng sách vừa thêm |
| `R26` | Xóa yêu thích (gọi 2 lần liên tiếp) | **PASS** | Idempotent, `count` về `0` |
| `R27` | Thẻ bị khóa (`DG000009`) thêm sách yêu thích | **PASS (Lưu ý)** | Hệ thống **cho phép** (`HTTP 200`) — xem mục 4 bên dưới |

### Nhóm 5 — Đặt giữ online (`Held`) & Đặt trước hàng chờ (`Pending`) (UC16)
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R28` | Đặt giữ sách còn hàng qua hộp xác nhận AJAX | **PASS** | Tạo `Held`, `availableQuantity` giảm đúng `1` (`3 -> 2`) |
| `R29` | Đặt trùng cuốn đang giữ (QĐ08) | **PASS** | Chặn `HTTP 400`, kho không bị trừ thêm |
| `R30` | Giới hạn `MaxHolds = 2` (đặt cuốn 2 OK, cuốn 3 bị chặn) | **PASS** | Cuốn 2 `200`, cuốn 3 `400` |
| `R31` | Tự hủy đặt giữ `Held` | **PASS** | Chuyển `Cancelled`, **hoàn trả đúng 1 cuốn vào kho** (`2 -> 3`) |
| `R32` | Đặt trước sách hết hàng (`availableQuantity == 0`) | **PASS** | Tạo `Pending`, kho giữ `0` |
| `R33` | **Chống chen hàng**: sách còn hàng nhưng đã có người xếp hàng `Pending` | **PASS** | Bắt buộc vào `Pending`, không cho đặt giữ `Held` vượt mặt |
| `R34` | Tự hủy đặt trước `Pending` | **PASS** | Chuyển `Cancelled` |
| `R35` | Đặt chính cuốn đang mượn (`IsBorrowingAsync`) | **PASS** | Chặn `HTTP 400` |
| `R36` | Độc giả có phiếu quá hạn (`DG000002`) đặt giữ sách còn | **PASS** | Chặn `HTTP 400` |
| `R36b` | Độc giả có phiếu quá hạn (`DG000002`) đặt trước sách hết hàng | **PASS (Lưu ý)** | Hệ thống **cho vào hàng chờ** (`HTTP 200`) — xem mục 4 |
| `R37` | Thẻ bị khóa (`DG000009`) & hết hạn (`DG000010`) đặt sách | **PASS** | Cả hai bị chặn `HTTP 400` |
| `R38` | Mở hộp xác nhận khi bản giữ cuối đã quá `HoldUntil` (`BUG-05`) | **FAIL (TB)** | **Không tự thu hồi kho**, hiển thị nhầm *"Xác nhận đặt trước"* thay vì *"Xác nhận đặt giữ"* |

### Nhóm 6 — Sách đang mượn, Lịch sử mượn, Lịch sử gia hạn & Yêu cầu gia hạn (UC14 – UC15)
| Mã | Kịch bản kiểm thử | Kết quả | Ghi chú |
|---|---|---|---|
| `R39` | Tab `Đang mượn` (`/MyLoans`) | **PASS** | Hiển thị đúng phiếu `Borrowing` của chính mình |
| `R40` | Trang `Lịch sử mượn` (`/MyLoans/History` — mới sửa) | **PASS** | Hiện đủ cả cuốn chưa trả + đã trả + tổng tiền phạt |
| `R41` | Tab `Lịch sử gia hạn` (`/MyLoans/RenewalHistory` — mới thêm) | **PASS** | Hiện dòng `Bị từ chối` kèm lý do trong modal `#reasonModal` |
| `R42` | Gửi yêu cầu gia hạn hợp lệ (GET preview + POST) | **PASS** | Tạo `RenewalRequest` trạng thái `Pending` |
| `R43` | Gửi trùng yêu cầu gia hạn lần 2 khi đang `Pending` | **PASS** | Chặn & báo đã có yêu cầu chờ duyệt |
| `R44` | Gia hạn phiếu đã quá hạn | **PASS** | Chặn & báo quá hạn |
| `R45` | Gia hạn phiếu đã đủ 2 lần (`MaxRenewals = 2`) | **PASS** | Chặn & báo đủ số lần |
| `R46` | Gia hạn khi thẻ bị khóa (trong lúc vẫn còn cookie đăng nhập cũ) | **PASS** | Chặn đúng ở server vì đọc trực tiếp DB |
| `R47` | Toàn vẹn tồn kho cuối phiên sau toàn bộ thao tác | **PASS** | **0 cuốn lệch kho** trên toàn bộ đầu sách |

---

## 4. Các lỗi & điểm cần lưu ý riêng của Role Độc giả

### Lỗi đã tái hiện thực tế (Nên sửa)
1. **`R38` / `BUG-05` (Mức Trung bình) — Chưa gọi `ExpireHoldsAsync()` khi mở hộp xác nhận và khi tạo đặt giữ**:
   - **Vị trí:** `Services/ReservationService.cs` (`GetBookForReservationConfirmAsync` dòng 214, `CreateAsync` dòng 250).
   - **Hậu quả:** Bản giữ cuối cùng đã quá hạn nhưng `HoldExpiryService` (5 phút/lần) chưa chạy → độc giả khác không đặt giữ được mà bị đẩy vào hàng chờ `Pending`.
   - **Sửa:** Thêm `await ExpireHoldsAsync();` ở đầu 2 hàm trên.
2. **`R05` (Mức Nhỏ — UX thực tế rất hay gặp) — Đăng nhập phân biệt chữ HOA/thường với mã thẻ `DG...`**:
   - **Vị trí:** `Services/AuthService.cs` dòng `38–39`.
   - **Hậu quả:** Độc giả gõ `dg000001` (chữ thường, rất phổ biến trên điện thoại) bị báo *"Tên đăng nhập hoặc mật khẩu không đúng"*, dù ở quầy Thủ thư hệ thống đã chuẩn hóa `.ToUpperInvariant()`.
   - **Sửa:** Nếu tên đăng nhập bắt đầu bằng `dg` (không phân biệt hoa/thường), chuẩn hóa `username = username.ToUpperInvariant()` (hoặc so sánh không phân biệt hoa/thường) trước khi truy vấn `users`.
3. **Liên kết `BUG-03` tới tab `Lịch sử gia hạn` mới làm của Độc giả**:
   - **Vị trí:** `Services/LoanService.cs` (`ReturnAsync` khi đóng phiếu sang `Returned`).
   - **Hậu quả:** Nếu độc giả gửi yêu cầu gia hạn (`Pending`) rồi mang sách đi trả hết trước khi Thủ thư duyệt, trên tab mới **Lịch sử gia hạn** của Độc giả dòng đó sẽ **mãi mãi hiện "Chờ duyệt"** dù phiếu đã trả xong!
   - **Sửa:** Khi `ReturnAsync` đóng phiếu sang `Returned`, tự động cập nhật mọi `RenewalRequests` đang `Pending` của phiếu đó sang `Rejected` với lý do *"Phiếu đã trả hết sách"*.

### Điểm cận biên cần thống nhất nghiệp vụ
4. **`R36b` — Độc giả có phiếu quá hạn vẫn đặt trước được sách hết hàng (`Pending`)**:
   - Hiện `OverdueLoans > 0` chỉ chặn trong `CreateHoldAsync()` (đặt giữ sách còn), không chặn ở `CreateAsync()` (đặt trước sách hết). Nếu muốn nhất quán QĐ07/QĐ08 (*đang có phiếu quá hạn thì không được đặt sách*), chuyển bước kiểm tra `OverdueLoans > 0` lên đầu `CreateAsync()`.
5. **`R27` — Thẻ bị khóa/hết hạn vẫn dùng được Sách yêu thích**:
   - Hiện `FavoriteService.AddAsync` không kiểm tra trạng thái thẻ. Điều này hợp lý nếu xem "Yêu thích" chỉ là danh sách đánh dấu cá nhân (không tác động kho), chỉ cần giữ câu cảnh báo `CardNotice` rõ ràng (*chưa mượn, gia hạn hay đặt trước sách được*).
6. **Ghi chú nhỏ về `BUG-08` trên trang `/MyFavorites`**:
   - `FavoriteService.GetListAsync` (dòng `104–108`) nên thêm `r.HoldUntil >= DateHelper.StartOfTodayVnUtc()` khi lọc `Held` để tránh hiện nhãn *"Đang giữ cho bạn"* cho bản giữ vừa hết hạn.

---

## 5. Gợi ý hành động tiếp theo

1. **Sửa nhanh 4 điểm ảnh hưởng trực tiếp tới Độc giả (~10 phút):**
   - `AuthService.cs`: chuẩn hóa `dg...` → `DG...` khi đăng nhập (`R05`).
   - `ReservationService.cs`: thêm `await ExpireHoldsAsync()` ở `GetBookForReservationConfirmAsync` & `CreateAsync` (`R38` / `BUG-05`), và quyết định có chặn `OverdueLoans > 0` cho cả đặt trước không (`R36b`).
   - `LoanService.cs`: khi `ReturnAsync` đóng phiếu sang `Returned`, tự đóng các `RenewalRequests` còn `Pending` để tab *Lịch sử gia hạn* của Độc giả không bị treo mãi ở *"Chờ duyệt"*.
   - `FavoriteService.cs`: lọc `HoldUntil >= StartOfTodayVnUtc()` cho nhãn `Held` (`BUG-08`).
2. **Tiếp tục xử lý các lỗi phía Thủ thư trong `Antigravity/bao-cao-loi.md`** (`BUG-01`, `BUG-02` ở `ReportService.cs`; `BUG-04` ở `BookService.cs`; `BUG-06`, `BUG-07` ở `ReservationService.cs`).
3. **Commit lại toàn bộ thay đổi UC14 + bản sửa lỗi**, sau đó chạy lại `Antigravity/qa_reader_suite.py` để xác nhận **48/48 PASS**.
