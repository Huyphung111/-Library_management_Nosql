# Test case UC15 Yêu cầu gia hạn (Độc giả)

Chưa code. Code dự kiến: `MyLoansController.RequestRenewal` (POST), `LoanService.RequestRenewalAsync` (`$push` vào `renewalRequests`, filter bảo đảm chưa có yêu cầu *Chờ duyệt*).

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC15-01 | 3 | Xem hạn trả dự kiến | Bấm "Gia hạn" ở phiếu hợp lệ | Hiện hạn trả hiện tại và hạn dự kiến = hạn hiện tại + 7 ngày | | Chưa thử | |
| TC15-02 | Chính | Gửi yêu cầu | Bấm "Gửi yêu cầu" | Thông báo đã gửi; phiếu có phần tử `renewalRequests` *Chờ duyệt*, nguồn *Online*; hạn trả **chưa** đổi | | Chưa thử | |
| TC15-03 | 4a | Hủy | Bấm "Hủy" ở bước xác nhận | Quay lại tab "Đang mượn", không tạo yêu cầu | | Chưa thử | |
| TC15-04 | 2a | Đã có yêu cầu đang chờ | Gửi lần 2 cho cùng phiếu | Báo đã có yêu cầu đang chờ duyệt; không tạo thêm phần tử | | Chưa thử | |
| TC15-05 | 2a | Phiếu quá hạn | Phiếu có `dueDate` trong quá khứ | Báo lý do, không gửi được | | Chưa thử | |
| TC15-06 | 2a | Đã gia hạn 2 lần | `renewalCount` = 2 | Báo lý do, không gửi được | | Chưa thử | |
| TC15-07 | 2a | Thẻ bị khóa hoặc hết hạn | Khóa tạm thẻ | Báo lý do, không gửi được | | Chưa thử | |
| TC15-08 | Bảo mật | Gửi yêu cầu cho phiếu của người khác | Đăng nhập `DG000002`, gửi POST với id phiếu của `DG000001` | Bị từ chối, không tạo yêu cầu | | Chưa thử | |
