# Test case UC15 Yêu cầu gia hạn (Độc giả)

Code: `MyLoansController.RequestRenewal` (GET: kiểm tra và xác nhận, POST: gửi), `LoanService.GetRenewalPreviewAsync`, `LoanService.RequestRenewalAsync` (`$push` vào `renewalRequests`, filter bảo đảm chưa có yêu cầu *Chờ duyệt*).

Điều kiện ở bước 2: thẻ *Hoạt động* và chưa hết hạn, phiếu chưa quá hạn, đã gia hạn ít hơn 2 lần, chưa có yêu cầu đang chờ. Điều kiện "có người đặt trước" do Thủ thư xét khi duyệt (UC09 bước 4).

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC15-01 | 3 | Xem hạn trả dự kiến | `DG000003` bấm "Gia hạn" ở `PM000013` | Hiện hạn trả hiện tại và hạn dự kiến = hạn hiện tại + 7 ngày | Hạn hiện tại 06/12/2026, dự kiến 13/12/2026 (thêm 7 ngày), đã gia hạn 0/2, có nút "Gửi yêu cầu" | Đạt | 22/11/2026 |
| TC15-02 | Chính | Gửi yêu cầu | Bấm "Gửi yêu cầu" | Thông báo đã gửi; phiếu có phần tử `renewalRequests` *Chờ duyệt*, nguồn *Online*; hạn trả **chưa** đổi | "Đã gửi yêu cầu gia hạn phiếu PM000013..."; phần tử `Pending`, `Online`; `dueDate`, `renewalCount` không đổi; tab "Đang mượn" hiện badge "Gia hạn chờ duyệt" | Đạt | 22/11/2026 |
| TC15-03 | 4a | Hủy | Bấm "Hủy" ở bước xác nhận | Quay lại tab "Đang mượn", không tạo yêu cầu | Nút "Hủy" là link về `/MyLoans`; mở trang xác nhận không tạo yêu cầu nào | Đạt | 22/11/2026 |
| TC15-04 | 2a | Đã có yêu cầu đang chờ | Gửi lần 2 cho cùng phiếu | Báo đã có yêu cầu đang chờ duyệt; không tạo thêm phần tử | Trang xác nhận báo "Phiếu đã có yêu cầu gia hạn đang chờ duyệt.", ẩn nút gửi; gửi POST thẳng cũng bị từ chối; vẫn 1 phần tử | Đạt | 22/11/2026 |
| TC15-05 | 2a | Phiếu quá hạn | `DG000005`, `PM000012` hạn 16/11/2026 | Báo lý do, không gửi được | "Phiếu đã quá hạn 6 ngày (hạn trả 16/11/2026)." | Đạt | 22/11/2026 |
| TC15-06 | 2a | Đã gia hạn 2 lần | `PM000015` sửa `renewalCount` = 2 | Báo lý do, không gửi được | "Phiếu đã gia hạn đủ 2 lần." | Đạt | 22/11/2026 |
| TC15-07 | 2a | Thẻ bị khóa hoặc hết hạn | Khóa tạm thẻ `DG000008` (có `PM000016`) | Báo lý do, không gửi được | "Thẻ đang bị khóa (lý do: Test UC15)."; POST thẳng cũng không tạo yêu cầu. Thử xong đã mở khóa | Đạt | 22/11/2026 |
| TC15-08 | Bảo mật | Gửi yêu cầu cho phiếu của người khác | Đăng nhập `DG000002`, gửi POST với id phiếu của độc giả khác | Bị từ chối, không tạo yêu cầu | "Không tìm thấy phiếu mượn của bạn."; mở trang xác nhận phiếu người khác cũng bị chuyển về | Đạt | 22/11/2026 |

## Ghi chú

- Thêm ngoài danh sách (Đạt): gửi **cùng lúc** 3 POST cho `PM000016` chỉ tạo đúng 1 yêu cầu *Chờ duyệt*, nhờ filter `renewalRequests` không có phần tử `Pending` nằm ngay trong lệnh update.
