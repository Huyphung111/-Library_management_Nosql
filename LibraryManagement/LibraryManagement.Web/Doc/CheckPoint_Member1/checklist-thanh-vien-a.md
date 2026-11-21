# Checklist công việc thành viên A

Danh sách theo `phan-cong.md`. Nghiệp vụ chi tiết xem `dac-ta-use-case.md`, cách cài đặt xem `thiet-ke-ky-thuat.md`.

Cách dùng: làm xong mục nào thì đổi `[ ]` thành `[x]` và ghi ngày ở cuối dòng, ví dụ `[x] ... (21/11/2026)`.

Test case chi tiết của từng UC và kết quả thử nằm trong thư mục [TestCase/](../TestCase/README.md). Dòng "Kiểm thử UC..." dưới đây chỉ đánh `[x]` khi mọi test case của UC đó đều **Đạt**.

## Chuẩn bị môi trường trên máy A

- [x] Cài .NET 10 SDK, kiểm tra bằng `dotnet --list-sdks` (21/11/2026, SDK 10.0.404)
- [x] Bật service MongoDB, đặt Startup type = Automatic (21/11/2026)
- [x] Cấu hình replica set `rs0` và chạy `rs.initiate()` (mục 7.1 thiết kế kỹ thuật) (21/11/2026, `rs0` là PRIMARY, `mongod.cfg.bak` là bản sao lưu file cấu hình gốc)
- [x] Cài mongosh (21/11/2026, bản 2.14.3, đã thêm vào PATH)
- [ ] Cài MongoDB Compass (chưa cài, tải tại https://www.mongodb.com/try/download/compass)

## Bước 0: Dựng nền (xong mới cho B tách nhánh)

**Project và cấu hình**
- [x] Tạo solution và project MVC theo mục 1 thiết kế kỹ thuật, cài `MongoDB.Driver`, `BCrypt.Net-Next` (21/11/2026, `LibraryManagement.slnx`, net10.0, MongoDB.Driver 3.14.1, BCrypt.Net-Next 4.2.0, build thành công)
- [x] `appsettings.json`: `MongoDb` và `LibraryRules` (21/11/2026)
- [x] `Options/LibraryRules.cs` (21/11/2026)
- [x] `Program.cs`: đăng ký **tất cả** service, MongoDB, cookie auth, route, gọi `DbInitializer`, có thêm `Lazy<ReservationService>` để `LoanService` và `ReservationService` gọi nhau không bị vòng phụ thuộc (21/11/2026)

**Data và Models**
- [x] `Data/MongoDbSettings.cs`, `MongoDbContext.cs` (21/11/2026)
- [x] `Data/MongoConventions.cs`: camelCase, enum lưu dạng string, bỏ qua field thừa (21/11/2026)
- [x] `Data/DbInitializer.cs`: tạo đủ index ở mục 5.4 (phần seed đầy đủ giao cho B) (đã kiểm tra bằng mongosh: đủ index, chạy lần hai không lỗi) (21/11/2026)
- [x] `Models/`: `User`, `Reader`, `Book`, `Category`, `Loan` (gồm `LoanItem`, `RenewalRequest`), `Reservation`, `Counter`, `Enums` (21/11/2026)
- [x] Hàm tính ngày theo giờ Việt Nam dùng chung (ví dụ `DateHelper.TodayVn()`) (`Services/DateHelper.cs`, quy ước lưu ngày ở mục 6.4 thiết kế kỹ thuật) (21/11/2026)

**Service dùng chung**
- [x] `Services/ServiceResult.cs` (21/11/2026)
- [x] `Services/CounterService.cs`: sinh mã `DG000001`, `PM000001` bằng `FindOneAndUpdate` + `$inc` (21/11/2026)
- [x] Tạo các class service rỗng: `AuthService`, `ReaderService`, `LoanService`, `BookService`, `CategoryService`, `ReservationService`, `SearchService`, `ReportService` (21/11/2026)

**5 hàm giao kèo (tạm trả giá trị giả)**
- [x] `ReservationService.CompleteForLoanAsync(session, readerId, bookIds, loanId)` (B viết thật) (21/11/2026)
- [x] `ReservationService.GetWaitingListAsync(bookIds)` (B viết thật) (21/11/2026)
- [x] `ReservationService.HasWaitingAsync(bookIds)` (B viết thật) (21/11/2026)
- [x] `LoanService.IsBorrowingAsync(readerId, bookId)` (A viết thật ở đợt 2) (21/11/2026)
- [x] `LoanService.HasOpenLoanForBookAsync(bookId)` (A viết thật ở đợt 2) (21/11/2026)

**Giao diện khung**
- [ ] Chốt bảng màu với B (tham khảo `_AdminLayout` của Shop-Flower, đổi màu cho hợp thư viện). Đã áp dụng navy `#1e3a5f` + vàng đồng `#c8963e` ở đầu `site.css` ngày 21/11/2026, chờ B đồng ý
- [x] `Views/Shared/_Layout.cshtml`: sidebar + navbar, chọn menu theo role (21/11/2026). Làm lại theo mục 6.5 thiết kế kỹ thuật ngày 21/11/2026: Bootstrap Icons, nút ☰ thu gọn sidebar, dropdown tài khoản, thanh cảnh báo `CardNotice`, tiêu đề trang + `@section PageActions`, trang chưa đăng nhập nền navy. Đã xem bằng trang mẫu tĩnh (máy tính và điện thoại); xem lại trên ứng dụng thật khi có trang đăng nhập
- [x] `Views/Shared/_LibrarianMenu.cshtml`: tạo sẵn **tất cả** link của Thủ thư, kể cả phần của B (21/11/2026). Sửa tô sáng mục đang mở theo cả controller, action và `type` (21/11/2026)
- [x] `Views/Shared/_ReaderMenu.cshtml`: tạo sẵn link `Catalog`, `MyLoans`, `MyReservations` (21/11/2026)
- [x] `Views/Shared/_Alert.cshtml`: hiện `TempData` thành công / lỗi (21/11/2026)
- [x] Badge trạng thái dùng chung trong CSS (thẻ, phiếu, gia hạn, đặt trước) (21/11/2026)
- [x] `HomeController`: chuyển hướng theo role sau đăng nhập (21/11/2026)

**Seed tối thiểu**
- [x] 1 admin (`admin` / `admin123`), 2 thể loại, 5 sách, 2 độc giả kèm tài khoản (21/11/2026)

**Bàn giao**
- [ ] Chạy được ứng dụng, Compass thấy đủ collection và index (ứng dụng chạy được và đã kiểm tra đủ 7 collection, index bằng mongosh ngày 21/11/2026; còn chờ cài Compass)
- [x] Đẩy lên `dev` (21/11/2026, commit `39bc567`), báo B bắt đầu (A báo trực tiếp; B tạo nhánh `member2` từ `origin/dev`)
- [x] Xuất dữ liệu mẫu ra `Data/MongoData/` kèm `import.js`, `export.js` để B nhập nhanh (21/11/2026, đã thử nhập vào database tạm: giống hệt bản gốc)

## Đợt 1: Tài khoản và độc giả

### UC01–UC03 Tài khoản (nhánh `uc01-03-tai-khoan`)
- [x] `AuthService`: kiểm tra đăng nhập bằng BCrypt, đổi mật khẩu (21/11/2026)
- [x] `ViewModels/Account`: `LoginVM`, `ChangePasswordVM` (21/11/2026)
- [x] `AccountController`: `Login` (GET, POST), `Logout` (POST), `ChangePassword` (GET, POST), `AccessDenied` (21/11/2026)
- [x] Lưu claim `NameIdentifier`, `Name`, `Role`, và `ReaderId` với độc giả (21/11/2026)
- [x] Thẻ bị khóa hoặc hết hạn: thêm claim `CardNotice` chứa câu thông báo để `_Layout` hiện thanh cảnh báo (mục 6.3) (21/11/2026)
- [x] Views: `Login` (không có link đăng ký, quên mật khẩu), `ChangePassword`, `AccessDenied` (21/11/2026)
- [x] Thêm `Field` vào `ServiceResult` để báo lỗi tại trường tương ứng (B dùng được, ví dụ CCCD trùng ở UC06) (21/11/2026)
- [ ] Kiểm thử UC01: bỏ trống trường (3a), sai mật khẩu (4a), thẻ khóa hoặc hết hạn vẫn đăng nhập được và có thông báo (5a). Đã thử 3a, 4a, đăng nhập Thủ thư và Độc giả trên Chrome ngày 21/11/2026; còn 5a (seed chưa có thẻ khóa / hết hạn)
- [ ] Kiểm thử UC02: phiên hết hạn thì chuyển về trang đăng nhập (1a). Đã thử đăng xuất bình thường ngày 21/11/2026; còn 1a
- [ ] Kiểm thử UC03: sai mật khẩu hiện tại (4a); mật khẩu mới dưới 6 ký tự, trùng mật khẩu cũ, xác nhận không khớp (4b). Đã thử 4a và mật khẩu ngắn ngày 21/11/2026; còn trùng mật khẩu cũ, xác nhận không khớp, đổi thành công
- [ ] Gộp vào `dev`

### UC06 Quản lý độc giả (nhánh `uc06-quan-ly-doc-gia`)
- [ ] `ReaderService`: danh sách, thêm, sửa, khóa (kèm lý do), mở khóa, gia hạn thẻ, đặt lại mật khẩu
- [ ] Thêm độc giả tạo đồng thời hồ sơ `readers` và tài khoản `users` (tên đăng nhập = mã thẻ, mật khẩu = `DefaultPassword`)
- [ ] `ViewModels/Readers`: `ReaderFormVM`, `ReaderListVM`
- [ ] `ReadersController`: `Index`, `Create`, `Edit`, `Lock`, `Unlock`, `ExtendCard`, `ResetPassword`
- [ ] Views: danh sách (mã thẻ, họ tên, SĐT, ngày hết hạn, trạng thái), form thêm / sửa, màn hiện tài khoản vừa cấp
- [ ] Trạng thái "hết hạn" tính khi truy vấn, không lưu vào database
- [ ] Kiểm thử: trường bắt buộc trống, email sai định dạng, CCCD trùng (6a); khóa, mở khóa, gia hạn thẻ, đặt lại mật khẩu (3b–3e)
- [ ] Gộp vào `dev`

## Đợt 2: Mượn trả

### UC07 Lập phiếu mượn (nhánh `uc07-lap-phieu-muon`)
- [ ] `LoanService.CreateAsync`: kiểm tra thẻ, phiếu quá hạn, số sách đang mượn
- [ ] Trong transaction: trừ `availableQuantity` bằng update có điều kiện `> 0`, tạo loan, gọi `CompleteForLoanAsync`
- [ ] `LoansController`: `Create` (GET, POST), `CheckReader` (AJAX)
- [ ] `ViewModels/Loans`: `CreateLoanVM`
- [ ] View: ô nhập mã thẻ, khung thông tin độc giả (đang mượn x/5, phiếu quá hạn, hạn thẻ), chọn sách, hạn trả đề xuất
- [ ] Kiểm thử: mã thẻ không có (2a); thẻ khóa, hết hạn, có phiếu quá hạn (3a); sách hết (5a); vượt 5 cuốn (5b); đặt trước chuyển *Hoàn tất* (5c); hạn trả không sau ngày mượn (7a)
- [ ] Gộp vào `dev`

### UC08 Nhận trả sách (nhánh `uc08-nhan-tra-sach`)
- [ ] `LoanService.ReturnAsync`: tính phạt quá hạn theo ngày giờ Việt Nam, hư hỏng theo mức nhập, mất bằng giá sách
- [ ] Trong transaction: cập nhật từng item bằng `arrayFilters`; sách không mất thì `availableQuantity +1`, sách mất thì `totalQuantity -1`; cập nhật `totalFine`; chuyển `Returned` khi trả hết
- [ ] Sau transaction: gọi `GetWaitingListAsync`, nhận `List<WaitingReader>` (đã có họ tên, SĐT, email, sắp người đặt sớm nhất lên đầu) và hiện danh sách người chờ
- [ ] `LoansController`: `Return` (GET, POST)
- [ ] `ViewModels/Loans`: `ReturnLoanVM`
- [ ] View: tìm theo mã thẻ hoặc mã phiếu, bảng sách chưa trả có chọn tình trạng và cột tiền phạt, tổng phạt
- [ ] Kiểm thử: không có phiếu đang mượn (3a); trả đúng hạn phạt 0 (5a); có người chờ (7a); trả từng phần, phiếu vẫn *Đang mượn* (8a)
- [ ] Gộp vào `dev`

### Hàm giao kèo cho B
- [ ] Viết thật `IsBorrowingAsync(readerId, bookId)`
- [ ] Viết thật `HasOpenLoanForBookAsync(bookId)`
- [ ] Báo B đã có hàm thật

## Đợt 3: Phía độc giả và gia hạn

### UC14 Sách đang mượn và lịch sử (nhánh `uc14-sach-cua-toi`)
- [ ] `LoanService`: lấy phiếu đang mượn và lịch sử theo `ReaderId` lấy từ claim
- [ ] `MyLoansController`: `Index`, `History`
- [ ] Views: tab "Đang mượn" (tên sách, ngày mượn, hạn trả, số lần gia hạn, đánh dấu quá hạn), tab "Lịch sử" (ngày trả, tiền phạt)
- [ ] Kiểm thử: chưa mượn sách nào (2a), chưa có lịch sử (4a), không xem được phiếu của độc giả khác
- [ ] Gộp vào `dev`

### UC15 Yêu cầu gia hạn (nhánh `uc15-yeu-cau-gia-han`)
- [ ] `LoanService.RequestRenewalAsync`: kiểm tra thẻ, quá hạn, số lần gia hạn, chưa có yêu cầu đang chờ; `$push` vào `renewalRequests`
- [ ] `MyLoansController.RequestRenewal` (POST)
- [ ] View: hiện hạn trả hiện tại và hạn trả dự kiến, nút "Gửi yêu cầu" / "Hủy"
- [ ] Kiểm thử: không đủ điều kiện thì hiện lý do (2a), nhấn Hủy (4a), gửi hai lần liên tiếp
- [ ] Gộp vào `dev`

### UC09 Gia hạn phiếu mượn (nhánh `uc09-gia-han`)
- [ ] `LoanService`: duyệt, từ chối, gia hạn tại quầy; kiểm tra điều kiện gồm gọi `HasWaitingAsync`
- [ ] Duyệt: `$inc renewalCount`, cộng 7 ngày vào `dueDate`, cập nhật yêu cầu bằng positional `$`
- [ ] `RenewalsController`: `Index`, `Approve`, `Reject`, `RenewAtCounter`
- [ ] `ViewModels/Loans`: `RenewalVM`
- [ ] View: danh sách yêu cầu *Chờ duyệt* (độc giả, mã phiếu, hạn trả, số lần đã gia hạn), nút Duyệt / Từ chối kèm lý do
- [ ] Kiểm thử: gia hạn tại quầy (1a), không đủ điều kiện chỉ được từ chối (4a), từ chối kèm lý do (5a), đã gia hạn 2 lần
- [ ] Gộp vào `dev`

## Đợt 4: Kiểm thử chéo phần của B

- [ ] UC04 Quản lý sách, đặc biệt xóa sách đang có phiếu mở
- [ ] UC05 Quản lý thể loại
- [ ] UC13 Tìm kiếm sách
- [ ] UC10, UC16 Đặt trước, đặc biệt tự chuyển *Hoàn tất* khi mượn
- [ ] UC11 Tìm kiếm
- [ ] UC12 Thống kê
- [ ] Ghi lỗi tìm được và gửi B

## Quy tắc mỗi ngày

- `git fetch origin` rồi `git merge origin/dev` vào nhánh `member1` trước khi code
- Chỉ sửa file của A. B cần thêm field hay link thì A sửa
- Đổi nghiệp vụ hoặc schema: sửa `Doc/` trước, báo B, rồi mới sửa code
