# Phân công công việc (2 người)

Nguyên tắc: **mỗi file chỉ có một người sửa**. Người A lo phần con người và mượn trả, người B lo phần sách, đặt trước và báo cáo. Hai người chỉ đụng nhau ở phần nền, nên phần đó làm xong trước khi tách nhánh.

Đặc tả use case xem `dac-ta-use-case.md`, thiết kế kỹ thuật xem `thiet-ke-ky-thuat.md`.

## 1. Bước 0: Dựng nền (A làm, xong mới tách nhánh)

Các file này cả hai đều dùng. A làm một lần, đẩy lên `dev`, rồi B mới bắt đầu. (Đã đẩy lên `dev` ngày 21/11/2026, commit `39bc567`.)

- Solution, `Program.cs` (đăng ký sẵn **tất cả** service), `appsettings.json`
- Toàn bộ `Data/`, `Models/`, `Options/`, `ServiceResult`, `CounterService`
- `_Layout`, `_LibrarianMenu`, `_ReaderMenu` (tạo sẵn **tất cả** link), `_Alert`
- Các class Service rỗng, kèm 5 hàm giao kèo ở mục 3 (tạm trả về giá trị giả)
- Seed tối thiểu: 1 admin, 2 thể loại, 5 sách, 2 độc giả, để không ai phải chờ ai

Trong lúc đó B cài môi trường (.NET 10, MongoDB, replica set, Compass) và thử trước các aggregation thống kê trong Compass.

## 2. Phân công

| | Người A | Người B |
|---|---|---|
| Use case | UC01–03 Tài khoản<br>UC06 Quản lý độc giả<br>UC07 Lập phiếu mượn<br>UC08 Nhận trả sách<br>UC09, UC15 Gia hạn<br>UC14 Sách đang mượn, lịch sử | UC04 Quản lý sách<br>UC05 Quản lý thể loại<br>UC13 Tìm kiếm sách<br>UC10, UC16 Đặt trước<br>UC11 Tìm kiếm<br>UC12 Thống kê |
| Controller | `Account`, `Readers`, `Loans`, `Renewals`, `MyLoans` | `Books`, `Categories`, `Catalog`, `Reservations`, `MyReservations`, `Search`, `Reports`, `Dashboard` |
| Service | `AuthService`, `ReaderService`, `LoanService` | `BookService`, `CategoryService`, `ReservationService`, `SearchService`, `ReportService` |
| Việc khác | Dựng nền, transaction | Seed dữ liệu đầy đủ, Chart.js |

Thư mục `Views/` và `ViewModels/` đi theo controller tương ứng.

A giữ phần khó nhất (UC07, UC08 có transaction). B có nhiều màn hình hơn và giữ phần aggregation. Nếu A bị quá tải, chuyển UC06 sang B vì nó không dính tới phiếu mượn.

## 3. Năm hàm giao kèo

Mượn trả và đặt trước dính nhau ở vài chỗ. Chốt tên hàm từ bước 0, sau đó chỉ gọi hàm của nhau, không sửa file của nhau.

| Hàm | Kiểu trả về | Người viết | Người gọi |
|---|---|---|---|
| `ReservationService.CompleteForLoanAsync(IClientSessionHandle session, string readerId, IEnumerable<string> bookIds, string loanId)` | `Task<long>` (số yêu cầu đã chuyển *Hoàn tất*) | B | A, trong UC07 |
| `ReservationService.GetWaitingListAsync(IEnumerable<string> bookIds)` | `Task<List<Reservation>>` (yêu cầu `Approved`, sắp theo `reservedAt`) | B | A, trong UC08 |
| `ReservationService.HasWaitingAsync(IEnumerable<string> bookIds)` | `Task<bool>` | B | A, trong UC09 và UC15 |
| `LoanService.IsBorrowingAsync(string readerId, string bookId)` | `Task<bool>` | A | B, trong UC16 |
| `LoanService.HasOpenLoanForBookAsync(string bookId)` | `Task<bool>` | A | B, khi xóa sách ở UC04 |

Từ bước 0, các hàm này đã có trong code và tạm trả giá trị giả (`0`, danh sách rỗng, `false`). Người viết thật thì chỉ thay phần thân, **không đổi chữ ký**.

Hai service gọi lẫn nhau nên tránh vòng phụ thuộc: `ReservationService` inject `LoanService` trực tiếp, còn `LoanService` nhận `Lazy<ReservationService>` (đã đăng ký trong `Program.cs`) và gọi qua `_reservations.Value`. Đừng đổi constructor của hai class này.

## 4. Thứ tự làm song song

| Đợt | Người A | Người B |
|---|---|---|
| 1 | UC01–03, UC06 | UC05, UC04, UC13 |
| 2 | UC07, UC08 | UC16, UC10 và 3 hàm giao kèo |
| 3 | UC14, UC15, UC09 | UC11, UC12 |
| 4 | Kiểm thử chéo phần của B | Seed đầy đủ, kiểm thử chéo phần của A |

## 5. Quy tắc tránh xung đột

**Nhánh Git**

| Nhánh | Dùng để | Ai đẩy lên |
|---|---|---|
| `main` | Bản ổn định để nộp. Chỉ gộp từ `dev` khi cả nhóm đã kiểm thử xong một mốc | Cả nhóm thống nhất rồi mới gộp |
| `dev` | Nhánh tích hợp: nơi gộp các UC đã xong. Luôn phải build và chạy được | A và B gộp UC đã xong của mình vào |
| `member1` | Nhánh làm việc của A, tạo từ `dev` | A |
| `member2` | Nhánh làm việc của B, tạo từ `dev` | B |

- Tạo nhánh làm việc lần đầu: `git fetch origin` rồi `git switch -c member2 origin/dev` (A dùng `member1`). Nếu muốn tách riêng từng UC thì tạo nhánh UC từ nhánh làm việc, ví dụ `uc07-lap-phieu-muon`.
- Xong UC nào (code chạy được, test case của UC đó đã thử) thì gộp vào `dev` ngay UC đó, rồi `git push origin dev`.
- Không commit thẳng lên `main`.
- File dùng chung chỉ A sửa: `Program.cs`, `Models/`, `appsettings.json`, layout, menu. B cần thêm field hay link thì nhắn A.
- `DbInitializer`: A viết phần index ở bước 0, sau đó giao hẳn cho B viết seed.
- Đổi nghiệp vụ hoặc schema: sửa tài liệu trong `Doc/` trước, báo người kia, rồi mới sửa code.
- Trước khi bắt đầu làm mỗi ngày: `git fetch origin` rồi `git merge origin/dev` vào nhánh đang làm, để có phần mới của người kia.
- Dữ liệu mẫu dùng chung nằm trong `Data/MongoData/` (xem `README.md` trong đó). Nhập bằng `import.js` để hai máy có cùng dữ liệu; ai đổi dữ liệu mẫu thì xuất lại bằng `export.js` và commit.
