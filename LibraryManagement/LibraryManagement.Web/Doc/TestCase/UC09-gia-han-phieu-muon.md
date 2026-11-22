# Test case UC09 Gia hạn phiếu mượn (Thủ thư)

Code: `RenewalsController` (`Index` có `?loanCode=` cho gia hạn tại quầy, `Approve`, `Reject`, `RenewAtCounter`), `LoanService` (`GetPendingRenewalsAsync`, `GetCounterRenewalAsync`, `ApproveAsync`, `RejectAsync`, `RenewAtCounterAsync`), gọi `ReservationService.HasWaitingAsync`.

Điều kiện gia hạn (QĐ04): phiếu chưa quá hạn, đã gia hạn ít hơn 2 lần (`MaxRenewals`), không ai đặt trước sách trong phiếu, thẻ *Hoạt động*. Mỗi lần cộng 7 ngày (`RenewalDays`). Duyệt dùng positional operator `$` để cập nhật đúng phần tử `renewalRequests` đang *Chờ duyệt*; filter giữ đúng `renewalCount` và `dueDate` đã đọc để hai lần duyệt cùng lúc không cộng hạn hai lần.

| Mã | Dòng SK | Nội dung | Dữ liệu / các bước | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Ngày |
|---|---|---|---|---|---|---|---|
| TC09-01 | 2 | Danh sách yêu cầu chờ duyệt | Có yêu cầu online từ UC15 và seed | Hiện độc giả, mã phiếu, hạn trả hiện tại, số lần đã gia hạn; chỉ yêu cầu *Chờ duyệt* | 5 yêu cầu *Chờ duyệt*, mỗi dòng có độc giả, mã phiếu, hạn hiện tại → hạn mới, số lần x/2, thời điểm gửi, cột điều kiện | Đạt | 22/11/2026 |
| TC09-02 | Chính | Duyệt yêu cầu hợp lệ | Duyệt yêu cầu `PM000013` (`DG000003`) | Hạn trả +7 ngày; `renewalCount` +1; yêu cầu *Đã duyệt*, có `processedAt`, `processedBy` | Hạn 06/12 → 13/12/2026; `renewalCount` 0 → 1; yêu cầu `Approved` có `processedAt`, `processedBy`. Duyệt lại lần 2 báo "không có yêu cầu đang chờ" | Đạt | 22/11/2026 |
| TC09-03 | 1a | Gia hạn tại quầy | Nhập mã phiếu `PM000016` → "Gia hạn tại quầy" | Gia hạn như TC09-02, yêu cầu ghi nguồn *Tại quầy* | Hạn 06/12 → 13/12/2026, `renewalCount` 1, thêm phần tử `Approved` nguồn `Counter`. Mã phiếu không có thì báo "Không tìm thấy phiếu mượn PM000999." Phiếu đang có yêu cầu online thì gia hạn tại quầy duyệt luôn yêu cầu đó | Đạt | 22/11/2026 |
| TC09-04 | 4a | Phiếu quá hạn | `PM000012` hạn 16/11/2026, có yêu cầu chờ | Hiện lý do, nút Duyệt bị khóa, chỉ Từ chối được | Cột điều kiện "Phiếu đã quá hạn 6 ngày...", nút Duyệt bị khóa; gửi POST duyệt thẳng cũng bị từ chối, hạn trả không đổi | Đạt | 22/11/2026 |
| TC09-05 | 4a | Đã gia hạn 2 lần | `PM000015` `renewalCount` = 2, có yêu cầu chờ | Hiện lý do, chỉ Từ chối được | "Phiếu đã gia hạn đủ 2 lần.", nút Duyệt khóa, POST duyệt bị từ chối | Đạt | 22/11/2026 |
| TC09-06 | 4a | Có người đặt trước sách trong phiếu | `DG000007` đặt trước *Chờ duyệt* "Lược Sử Thời Gian" (sách trong `PM000016`) | Hiện lý do, chỉ Từ chối được | "Có độc giả khác đang đặt trước sách trong phiếu.", nút Duyệt khóa, POST duyệt bị từ chối; gia hạn tại quầy cũng không có nút | Đạt | 22/11/2026 |
| TC09-07 | 4a | Thẻ bị khóa | Khóa tạm thẻ `DG000003`, phiếu `PM000013` có yêu cầu chờ | Hiện lý do, chỉ Từ chối được | "Thẻ đang bị khóa (lý do: Test UC09).", POST duyệt bị từ chối. Thử xong đã mở khóa | Đạt | 22/11/2026 |
| TC09-08 | 5a | Từ chối kèm lý do | Từ chối `PM000016`, lý do "Có người đang đặt trước sách" | Yêu cầu *Từ chối*, lưu `rejectReason`; hạn trả giữ nguyên | Yêu cầu `Rejected`, `rejectReason` đúng, có `processedAt`, `processedBy`; hạn trả và `renewalCount` không đổi | Đạt | 22/11/2026 |
| TC09-09 | 5a | Từ chối không nhập lý do | Bấm Từ chối, để trống lý do | Báo phải nhập lý do | "Vui lòng nhập lý do từ chối.", yêu cầu vẫn *Chờ duyệt* | Đạt | 22/11/2026 |

## Ghi chú

- Thêm ngoài danh sách (Đạt): hai thủ thư **cùng lúc** duyệt một yêu cầu của `PM000016` thì hạn trả chỉ cộng 7 ngày một lần (13/12 → 20/12), `renewalCount` chỉ tăng 1.
- Seed của B có `PM000003` đã trả nhưng còn yêu cầu gia hạn *Chờ duyệt*; danh sách hiện lý do "Phiếu đã trả hết sách." và Thủ thư từ chối được.
