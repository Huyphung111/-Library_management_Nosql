# Test case các use case của thành viên A

Mỗi file là bộ test case thủ công của một use case, viết theo dòng sự kiện chính và phụ trong `Doc/dac-ta-use-case.md`. Mã test case dạng `TC07-03`: UC07, test thứ 3. Cột "Dòng SK" ghi bước tương ứng trong đặc tả (ví dụ `3a`).

| File | Use case | Tình trạng code | Đã thử |
|---|---|---|---|
| [UC01-dang-nhap.md](UC01-dang-nhap.md) | UC01 Đăng nhập | Xong | 4/10 |
| [UC02-dang-xuat.md](UC02-dang-xuat.md) | UC02 Đăng xuất | Xong | 1/3 |
| [UC03-doi-mat-khau.md](UC03-doi-mat-khau.md) | UC03 Đổi mật khẩu | Xong | 2/7 |
| [UC06-quan-ly-doc-gia.md](UC06-quan-ly-doc-gia.md) | UC06 Quản lý độc giả | Chưa làm | 0/13 |
| [UC07-lap-phieu-muon.md](UC07-lap-phieu-muon.md) | UC07 Lập phiếu mượn | Chưa làm | 0/12 |
| [UC08-nhan-tra-sach.md](UC08-nhan-tra-sach.md) | UC08 Nhận trả sách | Chưa làm | 0/11 |
| [UC09-gia-han-phieu-muon.md](UC09-gia-han-phieu-muon.md) | UC09 Gia hạn phiếu mượn | Chưa làm | 0/9 |
| [UC14-sach-dang-muon-lich-su.md](UC14-sach-dang-muon-lich-su.md) | UC14 Sách đang mượn, lịch sử | Chưa làm | 0/6 |
| [UC15-yeu-cau-gia-han.md](UC15-yeu-cau-gia-han.md) | UC15 Yêu cầu gia hạn | Chưa làm | 0/8 |

## Cách ghi kết quả

- Trạng thái: **Đạt**, **Lỗi** (ghi mô tả lỗi ở cột "Kết quả thực tế"), **Chưa thử**.
- Ghi ngày thử ở cột "Ngày". Khi sửa lỗi xong thì thử lại và cập nhật dòng đó, không xóa lịch sử lỗi quan trọng (chép xuống mục "Ghi chú" cuối file).
- Cập nhật cột "Đã thử" ở bảng trên và dòng kiểm thử tương ứng trong `../CheckPoint_Member1/checklist-thanh-vien-a.md`.

## Dữ liệu dùng chung

Dữ liệu seed tối thiểu (tạo khi collection `users` rỗng, xem `Data/DbInitializer.cs`):

| Loại | Dữ liệu |
|---|---|
| Thủ thư | `admin` / `admin123` |
| Độc giả | `DG000001` Nguyễn Văn An, `DG000002` Trần Thị Bình; mật khẩu mặc định `LibraryRules.DefaultPassword`; thẻ *Hoạt động*, hết hạn 20/11/2027 |
| Thể loại | Kỹ năng sống, Văn học |
| Sách | Đắc Nhân Tâm (3/3), Nhà Giả Kim (2/2), Tuổi Trẻ Đáng Giá Bao Nhiêu (2/2), Dế Mèn Phiêu Lưu Ký (4/4), Cho Tôi Xin Một Vé Đi Tuổi Thơ (1/1) |

Test cần trạng thái đặc biệt (thẻ khóa, thẻ hết hạn, phiếu quá hạn...) thì sửa tạm dữ liệu bằng mongosh, thử xong trả lại như cũ. Lệnh mẫu ghi trong từng test case.

Chạy ứng dụng: `dotnet run --launch-profile http` trong `LibraryManagement.Web/`, mở http://localhost:5101.
