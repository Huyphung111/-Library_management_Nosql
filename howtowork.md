# Cơ chế hoạt động của Hệ thống Quản lý Mượn trả Sách Thư viện (NoSQL)

> Tài liệu này mô tả kiến trúc, luồng nghiệp vụ và cơ chế hoạt động của ứng dụng web Quản lý thư viện viết bằng **ASP.NET Core 10 MVC** kết hợp cơ sở dữ liệu **MongoDB 9.x** (Replica Set `rs0`).

---

## 1. Môi trường và Thiết lập vận hành

* **Nền tảng**: .NET 10 (C# 13), MongoDB 9.0 Replica Set (`rs0`) tại `mongodb://localhost:27017/?replicaSet=rs0`.
* **Cơ chế Khởi tạo Dữ liệu (DbInitializer)**:
  - Khi ứng dụng khởi động (`Program.cs`), hệ thống tự động kiểm tra và tạo các Unique Index với Collation tiếng Việt (`strength = 2` - phân biệt dấu nhưng không phân biệt chữ hoa/thường).
  - Tự động nạp dữ liệu mẫu (Seeder) nếu database trống: 1 tài khoản Thủ thư, 6 thể loại sách, 28 đầu sách, 10 độc giả (kèm tài khoản đăng nhập), các phiếu mượn và đặt trước mẫu.

---

## 2. Xác thực và Phân quyền (Authentication & Authorization)

* **Cookie Authentication**: Đăng nhập bằng tên đăng nhập và mật khẩu được mã hóa bằng **BCrypt**.
* **Phân quyền theo vai trò (Role-based)**:
  1. **Thủ thư (`Role.Librarian`)**:
     - Menu truy cập: Dashboard, Quản lý sách, Thể loại, Độc giả, Lập phiếu mượn, Nhận trả sách, Xử lý gia hạn, Xử lý đặt trước, Tìm kiếm, Thống kê.
     - Sau khi đăng nhập: Tự động chuyển hướng đến `/Dashboard`.
  2. **Độc giả (`Role.Reader`)**:
     - Menu truy cập: Tra cứu sách (`/Catalog`), Sách đang mượn, Lịch sử mượn, Đặt trước của tôi (`/MyReservations`).
     - Sau khi đăng nhập: Tự động chuyển hướng đến `/Catalog`.
     - Lưu trữ Claim đặc biệt:
       - `ReaderId`: Mã định danh MongoDB ObjectId của hồ sơ độc giả trong collection `readers`.
       - `CardNotice`: Thông báo cảnh báo tình trạng thẻ (thẻ bị khóa hoặc thẻ đã hết hạn) hiển thị liên tục qua banner.

---

## 3. Cơ chế hoạt động các chức năng đã triển khai (Thành viên B)

### 3.1. Dashboard Thủ thư (`/Dashboard`)
* **Dữ liệu thời gian thực**:
  - 4 thẻ `stat-card`: Tổng số đầu sách & cuốn sách trong kho, Số độc giả đang hoạt động, Số phiếu đang mượn, Số phiếu mượn quá hạn (truy vấn thời gian UTC).
  - Khung **Việc cần xử lý**: Đếm số yêu cầu gia hạn chờ duyệt, yêu cầu đặt trước chờ duyệt, và các yêu cầu đặt trước đã duyệt có sách vừa trả về kho cần liên hệ.
  - Phím tắt hành động nhanh: Lập phiếu mượn và Nhận trả sách.

### 3.2. Quản lý Thể loại - UC05 (`/Categories`)
* **Ràng buộc duy nhất**: Tên thể loại không trùng lặp (áp dụng collation `vi` tránh trùng kiểu "Văn học" và "văn học").
* **Ràng buộc QĐ09**: Không cho phép xóa thể loại nếu vẫn còn sách đang thuộc thể loại đó trong thư viện.

### 3.3. Quản lý Sách - UC04 (`/Books`)
* **Thông tin sách**: Tên sách, tác giả, thể loại, nhà xuất bản, năm xuất bản, giá bìa, số lượng tổng và số lượng còn lại (`AvailableQuantity`).
* **Ràng buộc cập nhật kho**: Số lượng tổng mới không được phép nhỏ hơn số lượng sách đang cho mượn (`TotalQuantity - AvailableQuantity`).
* **Ràng buộc xóa**: Gọi `LoanService.HasOpenLoanForBookAsync(bookId)` để ngăn chặn việc xóa sách khi còn phiếu mượn mở.

### 3.4. Tra cứu Sách phía Độc giả - UC13 (`/Catalog`)
* **Tìm kiếm & Bộ lọc**:
  - Tìm kiếm toàn văn (Text Index) trên trường `Title` và `Authors`.
  - Bộ lọc dropdown theo Thể loại: Tự động tải lại kết quả ngay khi chọn danh mục.
* **Giao diện Lưới thẻ sách (`book-grid`)**:
  - Hiển thị bìa sách phối màu xen kẽ chuẩn phong cách thư viện.
  - Nhãn tình trạng: "Còn X cuốn" (màu xanh) hoặc "Hết sách" (màu đỏ).
  - Với sách đã hết: Nút "Đặt trước" đưa thẳng độc giả đến form đăng ký.
* **Trang Chi tiết sách (`/Catalog/Details/{id}`)**:
  - Hiển thị thông số chi tiết của sách.
  - Nếu sách còn: Hướng dẫn độc giả đến quầy mượn trực tiếp.
  - Nếu sách hết: Kích hoạt nút bấm đặt trước.

### 3.5. Đặt trước sách phía Độc giả - UC16 (`/MyReservations`)
* **Quy định nghiệp vụ QĐ08**:
  - Chỉ cho phép đặt trước khi sách **đang hết trong kho** (`AvailableQuantity == 0`).
  - Thẻ độc giả phải đang ở trạng thái **Hoạt động** và **chưa hết hạn**.
  - Mỗi độc giả chỉ được có tối đa **1 yêu cầu còn hiệu lực** (*Chờ duyệt* hoặc *Đã duyệt*) cho mỗi đầu sách.
  - Độc giả không thể đặt trước nếu bản thân đang mượn chính cuốn sách đó.
* **Chính sách không giữ sách riêng**: Hệ thống ghi nhận thứ tự ưu tiên theo thời gian đăng ký (`ReservedAt`).
* **Hủy yêu cầu (bước 1b)**: Độc giả có thể tự bấm hủy yêu cầu của mình khi đang ở trạng thái *Chờ duyệt* hoặc *Đã duyệt*.

### 3.6. Xử lý Đặt trước phía Thủ thư - UC10 (`/Reservations`)
* **Quản lý theo Tab**:
  - *Tất cả*: Toàn bộ lịch sử yêu cầu đặt trước.
  - *Chờ duyệt*: Các yêu cầu mới gửi từ độc giả, hỗ trợ nút **Duyệt** hoặc **Từ chối** (nhập lý do).
  - *Sách đã về kho*: Đánh dấu nổi bật các yêu cầu *Đã duyệt* mà cuốn sách hiện có `AvailableQuantity > 0`. Hiển thị ngay Số điện thoại và Email của độc giả, sắp xếp người đặt sớm nhất lên đầu để Thủ thư gọi điện đến nhận sách.
  - *Đã duyệt*: Các yêu cầu đã được chấp thuận. Thủ thư có thể bấm **Hủy** kèm lý do (ví dụ: liên hệ nhiều lần độc giả không đến nhận).
  - *Hoàn tất*: Đã hoàn thành khi độc giả đến mượn sách.
  - *Từ chối / Đã hủy*: Danh sách các yêu cầu bị từ chối hoặc bị hủy.

### 3.7. Các hàm giao kèo hệ thống (Inter-Service Contracts)
1. `CompleteForLoanAsync(session, readerId, bookIds, loanId)`:
   - Chạy trực tiếp trong **Mongo Client Session (Transaction)** của chức năng Lập phiếu mượn (Member A).
   - Tự động chuyển các yêu cầu *Chờ duyệt* / *Đã duyệt* của độc giả đối với những cuốn sách vừa mượn sang trạng thái *Hoàn tất* (`Completed`) và lưu `CompletedLoanId`.
2. `GetWaitingListAsync(bookIds)`:
   - Lấy danh sách độc giả đang chờ sách theo thứ tự thời gian đặt sớm nhất, tự động kết nối thông tin họ tên, mã thẻ, SĐT, email từ `readers`.
3. `HasWaitingAsync(bookIds)`:
   - Kiểm tra nhanh xem sách có người đang chờ hay không để Thủ thư/Độc giả có căn cứ từ chối gia hạn theo quy định QĐ04.

---

## 4. Trải nghiệm người dùng và UI/UX

* **Bảng màu chủ đạo**: Navy `#1e3a5f` (chuyên nghiệp, tri thức) và Vàng đồng `#c8963e` (điểm nhấn).
* **Toast Notification Thông minh**:
  - Toàn bộ thông báo `TempData["Success"]` và `TempData["Error"]` hiển thị dưới dạng **Toast Floating** ở góc dưới bên phải màn hình.
  - Không làm trồi sụt hay xê dịch giao diện (Zero Layout Shift).
  - Tự động trượt vào nhẹ nhàng và biến mất sau 3.5 giây, hoặc có thể đóng thủ công bằng nút `x`.

---

## 5. Dữ liệu tài khoản thử nghiệm

| Tên tài khoản | Mật khẩu | Vai trò | Tình trạng thẻ | Mô tả nghiệp vụ |
| :--- | :---: | :---: | :---: | :--- |
| **`admin`** | `admin123` | Thủ thư | - | Toàn quyền quản trị hệ thống |
| **`DG000001`** | `123456` | Độc giả | Hoạt động | Thẻ chuẩn, đang mượn 2 cuốn sách |
| **`DG000002`** | `123456` | Độc giả | Hoạt động | Đang có 1 cuốn sách mượn quá hạn |
| **`DG000009`** | `123456` | Độc giả | Bị khóa | Làm mất sách chưa đền bù (báo cảnh báo, chặn mượn/đặt) |
| **`DG000010`** | `123456` | Độc giả | Hết hạn | Thẻ quá hạn sử dụng (báo cảnh báo, chặn mượn/đặt) |

