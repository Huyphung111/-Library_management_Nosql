# Test case UC09 Gia hạn phiếu mượn (Thủ thư)

Chưa code. Code dự kiến: `RenewalsController` (`Index`, `Approve`, `Reject`, `RenewAtCounter`), `LoanService`, gọi `ReservationService.HasWaitingAsync`.

Điều kiện gia hạn (QĐ04): phiếu chưa quá hạn, đã gia hạn ít hơn 2 lần (`MaxRenewals`), không ai đặt trước sách trong phiếu, thẻ *Hoạt động*. Mỗi lần cộng 7 ngày (`RenewalDays`).

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC09-01 | 2 | Danh sách yêu cầu chờ duyệt | Có yêu cầu online từ UC15 | Hiện độc giả, mã phiếu, hạn trả hiện tại, số lần đã gia hạn; chỉ yêu cầu *Chờ duyệt* | | Chưa thử | |
| TC09-02 | Chính | Duyệt yêu cầu hợp lệ | Duyệt yêu cầu của `DG000001` | Hạn trả +7 ngày; `renewalCount` +1; yêu cầu *Đã duyệt*, có `processedAt`, `processedBy` | | Chưa thử | |
| TC09-03 | 1a | Gia hạn tại quầy | Nhập mã phiếu hợp lệ | Gia hạn như TC09-02, yêu cầu ghi nguồn *Tại quầy* | | Chưa thử | |
| TC09-04 | 4a | Phiếu quá hạn | Phiếu có `dueDate` trong quá khứ | Hiện lý do, nút Duyệt bị khóa, chỉ Từ chối được | | Chưa thử | |
| TC09-05 | 4a | Đã gia hạn 2 lần | `renewalCount` = 2 | Hiện lý do, chỉ Từ chối được | | Chưa thử | |
| TC09-06 | 4a | Có người đặt trước sách trong phiếu | Một sách trong phiếu có đặt trước *Chờ duyệt* hoặc *Đã duyệt* | Hiện lý do, chỉ Từ chối được | | Chưa thử | |
| TC09-07 | 4a | Thẻ bị khóa | Khóa tạm thẻ của độc giả | Hiện lý do, chỉ Từ chối được | | Chưa thử | |
| TC09-08 | 5a | Từ chối kèm lý do | Từ chối, nhập lý do | Yêu cầu *Từ chối*, lưu `rejectReason`; hạn trả giữ nguyên | | Chưa thử | |
| TC09-09 | 5a | Từ chối không nhập lý do | Bấm Từ chối, để trống lý do | Báo phải nhập lý do | | Chưa thử | |
