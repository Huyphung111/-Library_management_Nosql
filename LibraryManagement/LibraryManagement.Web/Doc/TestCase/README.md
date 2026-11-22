# Test case các use case của thành viên A

Mỗi file là bộ test case thủ công của một use case, viết theo dòng sự kiện chính và phụ trong `Doc/dac-ta-use-case.md`. Mã test case dạng `TC07-03`: UC07, test thứ 3. Cột "Dòng SK" ghi bước tương ứng trong đặc tả (ví dụ `3a`).

| File | Use case | Tình trạng code | Đã thử |
|---|---|---|---|
| [UC01-dang-nhap.md](UC01-dang-nhap.md) | UC01 Đăng nhập | Xong | 10/10 (Đạt hết) |
| [UC02-dang-xuat.md](UC02-dang-xuat.md) | UC02 Đăng xuất | Xong | 3/3 (Đạt hết) |
| [UC03-doi-mat-khau.md](UC03-doi-mat-khau.md) | UC03 Đổi mật khẩu | Xong | 7/7 (Đạt hết) |
| [UC06-quan-ly-doc-gia.md](UC06-quan-ly-doc-gia.md) | UC06 Quản lý độc giả | Xong | 13/13 (Đạt hết) |
| [UC07-lap-phieu-muon.md](UC07-lap-phieu-muon.md) | UC07 Lập phiếu mượn | Xong | 12/12 (Đạt hết) |
| [UC08-nhan-tra-sach.md](UC08-nhan-tra-sach.md) | UC08 Nhận trả sách | Xong | 11/11 (Đạt hết) |
| [UC09-gia-han-phieu-muon.md](UC09-gia-han-phieu-muon.md) | UC09 Gia hạn phiếu mượn | Xong | 9/9 (Đạt hết) |
| [UC14-sach-dang-muon-lich-su.md](UC14-sach-dang-muon-lich-su.md) | UC14 Sách đang mượn, lịch sử | Xong | 6/6 (Đạt hết) |
| [UC15-yeu-cau-gia-han.md](UC15-yeu-cau-gia-han.md) | UC15 Yêu cầu gia hạn | Xong | 8/8 (Đạt hết) |

## Cách ghi kết quả

- Trạng thái: **Đạt**, **Lỗi** (ghi mô tả lỗi ở cột "Kết quả thực tế"), **Chưa thử**.
- Ghi ngày thử ở cột "Ngày". Khi sửa lỗi xong thì thử lại và cập nhật dòng đó, không xóa lịch sử lỗi quan trọng (chép xuống mục "Ghi chú" cuối file).
- Cập nhật cột "Đã thử" ở bảng trên và dòng kiểm thử tương ứng trong `../CheckPoint_Member1/checklist-thanh-vien-a.md`.

## Kiểm thử hồi quy toàn bộ (22/11/2026)

Sau khi xong cả 9 UC, chạy lại toàn bộ phần A trên một database mới tinh (seed đầy đủ của B): 74/74 kiểm tra Đạt, gồm các luồng chính của từng UC, 2 hàm giao kèo cho B (gọi qua UC04 xóa sách và UC16 đặt trước), mọi trang của A trả HTTP 200, log ứng dụng không có lỗi, và kiểm tra nhất quán kho: với mọi sách, `(totalQuantity - availableQuantity) - số cuốn chưa trả trong phiếu Borrowing` không đổi trước và sau mọi thao tác.

Cách thử: chạy ứng dụng với biến môi trường `MongoDb__DatabaseName=library_management_test` (database riêng, không đụng dữ liệu thật), gửi request bằng curl (giữ cookie, kèm token chống giả mạo) và kiểm tra dữ liệu bằng mongosh.

## Dữ liệu dùng chung

Dữ liệu seed tối thiểu cũ (trước khi B làm seed đầy đủ). Từ 22/11/2026 dùng seed đầy đủ của B, xem `Data/MongoData/README.md` (10 độc giả, `DG000009` bị khóa, `DG000010` hết hạn, 28 sách, 9 phiếu mượn):

| Loại | Dữ liệu |
|---|---|
| Thủ thư | `admin` / `admin123` |
| Độc giả | `DG000001` Nguyễn Văn An, `DG000002` Trần Thị Bình; mật khẩu mặc định `LibraryRules.DefaultPassword`; thẻ *Hoạt động*, hết hạn 20/11/2027 |
| Thể loại | Kỹ năng sống, Văn học |
| Sách | Đắc Nhân Tâm (3/3), Nhà Giả Kim (2/2), Tuổi Trẻ Đáng Giá Bao Nhiêu (2/2), Dế Mèn Phiêu Lưu Ký (4/4), Cho Tôi Xin Một Vé Đi Tuổi Thơ (1/1) |

Test cần trạng thái đặc biệt (thẻ khóa, thẻ hết hạn, phiếu quá hạn...) thì sửa tạm dữ liệu bằng mongosh, thử xong trả lại như cũ. Lệnh mẫu ghi trong từng test case.

Chạy ứng dụng: `dotnet run --launch-profile http` trong `LibraryManagement.Web/`, mở http://localhost:5101.
