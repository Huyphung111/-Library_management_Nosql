# Đặc tả Use Case: Hệ thống quản lý mượn trả sách

## 1. Quy định nghiệp vụ

| Mã | Quy định |
|---|---|
| QĐ01 | Thẻ thư viện có hiệu lực 1 năm kể từ ngày cấp. Thủ thư có thể gia hạn thẻ thêm 1 năm. |
| QĐ02 | Mỗi độc giả mượn tối đa 5 cuốn cùng lúc. |
| QĐ03 | Hạn trả mặc định là ngày mượn + 14 ngày. |
| QĐ04 | Mỗi phiếu được gia hạn tối đa 2 lần, mỗi lần thêm 7 ngày. Chỉ gia hạn khi phiếu chưa quá hạn, không ai đang đặt trước sách trong phiếu và thẻ đang hoạt động. Gia hạn online do Thủ thư duyệt. |
| QĐ05 | Phạt quá hạn 5.000đ / ngày / cuốn. |
| QĐ06 | Sách hư hỏng: Thủ thư nhập mức phạt. Sách mất: bồi thường bằng giá sách. |
| QĐ07 | Không được mượn khi thẻ bị khóa, thẻ hết hạn hoặc đang có phiếu quá hạn. |
| QĐ08 | Chỉ đặt trước được sách đang hết. Mỗi độc giả chỉ có 1 yêu cầu còn hiệu lực (*Chờ duyệt* hoặc *Đã duyệt*) cho mỗi đầu sách. Hệ thống không giữ sách riêng: khi sách về, Thủ thư liên hệ người đặt sớm nhất, ai đến quầy trước thì mượn trước. |
| QĐ09 | Chỉ xóa thể loại khi không còn sách thuộc thể loại đó. Chỉ xóa sách khi không có phiếu mượn đang mở chứa sách đó và không còn yêu cầu đặt trước còn hiệu lực (*Chờ duyệt*, *Đã duyệt*, đang giữ). Không giảm số lượng tổng xuống dưới số đang cho mượn và đang giữ. |
| QĐ10 | Tài khoản độc giả do Thủ thư cấp, không có tự đăng ký. Mật khẩu tối thiểu 6 ký tự. |

**Trạng thái dùng trong hệ thống**

- Thẻ độc giả: *Hoạt động*, *Bị khóa*. Thẻ hết hạn được tính từ ngày hết hạn.
- Phiếu mượn: *Đang mượn*, *Đã trả*. Phiếu quá hạn là phiếu *Đang mượn* có hạn trả nhỏ hơn ngày hiện tại.
- Yêu cầu gia hạn: *Chờ duyệt*, *Đã duyệt*, *Từ chối*.
- Yêu cầu đặt trước: *Chờ duyệt*, *Đã duyệt*, *Từ chối*, *Hoàn tất*, *Đã hủy*.

## 2. Mô tả quy trình nghiệp vụ

### QT1. Cấp thẻ và tài khoản độc giả
Người muốn làm thẻ đến quầy và cung cấp thông tin cá nhân. Thủ thư nhập vào hệ thống. Hệ thống tạo hồ sơ độc giả với mã thẻ, ngày cấp, ngày hết hạn, đồng thời tạo tài khoản đăng nhập với tên đăng nhập là mã thẻ và mật khẩu mặc định. Độc giả đăng nhập lần đầu và đổi mật khẩu. Khi độc giả vi phạm, Thủ thư khóa thẻ. Khi vi phạm đã được xử lý, Thủ thư mở khóa. Thẻ bị khóa vẫn đăng nhập được để xem thông tin, nhưng không mượn, gia hạn hay đặt trước được.

### QT2. Quản lý danh mục sách và thể loại
Thủ thư tạo các thể loại trước. Khi nhập sách mới, Thủ thư điền thông tin sách, chọn thể loại từ danh mục và nhập số lượng tổng. Số còn lại lúc đầu bằng số lượng tổng. Mỗi lần cho mượn, số còn lại giảm. Mỗi lần nhận trả, số còn lại tăng.

### QT3. Mượn sách
Độc giả mang sách và thẻ đến quầy. Thủ thư nhập mã thẻ. Hệ thống kiểm tra thẻ, phiếu quá hạn, số sách đang mượn và tồn kho. Đủ điều kiện thì hệ thống lập phiếu mượn với hạn trả 14 ngày và trừ số còn lại.

### QT4. Trả sách và tính phạt
Độc giả mang sách đến trả. Thủ thư ghi tình trạng từng cuốn. Hệ thống tính tiền phạt quá hạn, hư hỏng hoặc mất, rồi cộng sách lại vào kho. Nếu sách vừa trả đang có người đặt trước, hệ thống hiển thị danh sách người chờ để Thủ thư liên hệ. Phiếu chuyển sang *Đã trả* khi đã trả hết sách.

### QT5. Gia hạn
Độc giả gửi yêu cầu gia hạn online hoặc nhờ Thủ thư gia hạn tại quầy. Thủ thư xem yêu cầu và duyệt hoặc từ chối. Khi được duyệt, hạn trả cộng thêm 7 ngày.

### QT6. Đặt trước
Khi sách đang hết, độc giả gửi yêu cầu đặt trước. Thủ thư duyệt thì độc giả vào danh sách chờ, xếp theo ngày đặt. Khi có sách trả về, sách được cộng lại vào kho như bình thường, và hệ thống nhắc Thủ thư danh sách người đang chờ để liên hệ người đặt sớm nhất. Khi người đặt trước đến mượn, yêu cầu của họ tự chuyển sang *Hoàn tất*. Độc giả có thể tự hủy yêu cầu; Thủ thư cũng có thể hủy khi không liên hệ được độc giả.

### QT7. Tra cứu và thống kê
Độc giả tra cứu sách. Thủ thư tra cứu sách, độc giả, phiếu mượn và xem các báo cáo thống kê.

## 3. Danh sách Use Case

| Mã | Use case | Tác nhân |
|---|---|---|
| UC01 | Đăng nhập | Thủ thư, Độc giả |
| UC02 | Đăng xuất | Thủ thư, Độc giả |
| UC03 | Đổi mật khẩu | Thủ thư, Độc giả |
| UC04 | Quản lý sách | Thủ thư |
| UC05 | Quản lý thể loại | Thủ thư |
| UC06 | Quản lý độc giả | Thủ thư |
| UC07 | Lập phiếu mượn | Thủ thư |
| UC08 | Nhận trả sách | Thủ thư |
| UC09 | Gia hạn phiếu mượn | Thủ thư |
| UC10 | Xử lý đặt trước | Thủ thư |
| UC11 | Tìm kiếm | Thủ thư |
| UC12 | Thống kê | Thủ thư |
| UC13 | Tìm kiếm sách | Độc giả |
| UC14 | Xem sách đang mượn và lịch sử mượn | Độc giả |
| UC15 | Yêu cầu gia hạn | Độc giả |
| UC16 | Đặt trước sách | Độc giả |

## 4. Đặc tả Use Case

**Bảng 2.1. Use Case Đăng nhập**

| **Tên use case** | Đăng nhập |
|---|---|
| **Tóm tắt** | Thủ thư hoặc độc giả đăng nhập bằng tên đăng nhập và mật khẩu để dùng các chức năng theo vai trò của mình. |
| **Tác nhân** | Thủ thư, Độc giả |
| **Use case liên quan** | Không |
| **Dòng sự kiện chính** | 1. Người dùng mở trang đăng nhập.<br>2. Hệ thống hiển thị form gồm tên đăng nhập và mật khẩu.<br>3. Người dùng nhập thông tin và nhấn "Đăng nhập".<br>4. Hệ thống kiểm tra tên đăng nhập có tồn tại và mật khẩu có khớp không. Tên đăng nhập của Độc giả là mã thẻ, không phân biệt chữ hoa/thường (gõ `dg000001` cũng như `DG000001`).<br>5. Hệ thống xác định vai trò của tài khoản và chuyển đến trang chính của Thủ thư hoặc Độc giả. |
| **Dòng sự kiện phụ** | 3a. Người dùng bỏ trống tên đăng nhập hoặc mật khẩu: hệ thống báo lỗi tại trường tương ứng, quay lại bước 3.<br>4a. Sai tên đăng nhập hoặc mật khẩu: hệ thống báo "Tên đăng nhập hoặc mật khẩu không đúng", quay lại bước 3.<br>5a. Độc giả có thẻ bị khóa hoặc hết hạn: hệ thống vẫn cho đăng nhập, hiển thị thông báo tình trạng thẻ và chỉ cho xem thông tin. |
| **Điều kiện tiên quyết** | Người dùng đã có tài khoản do Thủ thư cấp. |
| **Hậu điều kiện** | Người dùng đăng nhập thành công, phiên làm việc được tạo theo đúng vai trò. |

**Bảng 2.2. Use Case Đăng xuất**

| **Tên use case** | Đăng xuất |
|---|---|
| **Tóm tắt** | Người dùng kết thúc phiên làm việc trên hệ thống. |
| **Tác nhân** | Thủ thư, Độc giả |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Người dùng chọn "Đăng xuất" trong menu tài khoản.<br>2. Hệ thống hủy phiên làm việc.<br>3. Hệ thống chuyển về trang đăng nhập. |
| **Dòng sự kiện phụ** | 1a. Phiên làm việc đã hết hạn trước đó: hệ thống chuyển thẳng về trang đăng nhập. |
| **Điều kiện tiên quyết** | Người dùng đã đăng nhập. |
| **Hậu điều kiện** | Phiên làm việc kết thúc, người dùng phải đăng nhập lại để dùng hệ thống. |

**Bảng 2.3. Use Case Đổi mật khẩu**

| **Tên use case** | Đổi mật khẩu |
|---|---|
| **Tóm tắt** | Người dùng thay mật khẩu đăng nhập của mình, ví dụ đổi mật khẩu mặc định sau khi được cấp tài khoản. |
| **Tác nhân** | Thủ thư, Độc giả |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Người dùng chọn "Đổi mật khẩu" trong menu tài khoản.<br>2. Hệ thống hiển thị form gồm mật khẩu hiện tại, mật khẩu mới và xác nhận mật khẩu mới.<br>3. Người dùng nhập thông tin và nhấn "Lưu".<br>4. Hệ thống kiểm tra: mật khẩu hiện tại đúng, mật khẩu mới có ít nhất 6 ký tự và khác mật khẩu hiện tại, xác nhận trùng với mật khẩu mới.<br>5. Hệ thống lưu mật khẩu mới (đã băm) và thông báo đổi mật khẩu thành công. |
| **Dòng sự kiện phụ** | 4a. Mật khẩu hiện tại không đúng: hệ thống báo lỗi, quay lại bước 3.<br>4b. Mật khẩu mới không hợp lệ hoặc xác nhận không khớp: hệ thống báo lỗi tại trường tương ứng, quay lại bước 3. |
| **Điều kiện tiên quyết** | Người dùng đã đăng nhập. |
| **Hậu điều kiện** | Mật khẩu mới được lưu và dùng cho các lần đăng nhập sau. |

**Bảng 2.4. Use Case Quản lý sách**

| **Tên use case** | Quản lý sách |
|---|---|
| **Tóm tắt** | Thủ thư thêm, sửa, xóa và xem danh sách đầu sách. Mỗi sách gồm tên, tác giả, nhà xuất bản, năm xuất bản, giá, thể loại, số lượng tổng và số còn lại. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Quản lý sách".<br>2. Hệ thống hiển thị danh sách đầu sách gồm tên, tác giả, thể loại, số lượng tổng và số còn lại.<br>3. Thủ thư chọn "Thêm sách".<br>4. Hệ thống hiển thị form nhập sách, thể loại được chọn từ danh mục có sẵn.<br>5. Thủ thư nhập thông tin và nhấn "Lưu".<br>6. Hệ thống kiểm tra: tên sách không trống, có ít nhất 1 tác giả và 1 thể loại, giá và số lượng tổng là số dương.<br>7. Hệ thống lưu sách với số còn lại bằng số lượng tổng và thông báo thêm thành công. |
| **Dòng sự kiện phụ** | 3a. Thủ thư chọn sửa một sách: hệ thống hiển thị form với thông tin hiện tại. Thủ thư sửa và nhấn "Lưu". Hệ thống kiểm tra như bước 6 và kiểm tra số lượng tổng mới không nhỏ hơn số đang cho mượn và đang giữ, rồi cập nhật sách. Số còn lại thay đổi theo mức chênh lệch của số lượng tổng.<br>3b. Thủ thư chọn xóa một sách và xác nhận: nếu sách không nằm trong phiếu mượn đang mở và không còn yêu cầu đặt trước còn hiệu lực, hệ thống xóa sách. Ngược lại, hệ thống báo không thể xóa và nêu lý do.<br>6a. Dữ liệu không hợp lệ: hệ thống báo lỗi tại trường tương ứng, quay lại bước 5. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. Đã có ít nhất 1 thể loại trong danh mục. |
| **Hậu điều kiện** | Danh mục sách được cập nhật. |

**Bảng 2.5. Use Case Quản lý thể loại**

| **Tên use case** | Quản lý thể loại |
|---|---|
| **Tóm tắt** | Thủ thư thêm, sửa, xóa và xem danh sách thể loại sách. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Quản lý thể loại".<br>2. Hệ thống hiển thị danh sách thể loại gồm tên, mô tả và số đầu sách thuộc thể loại.<br>3. Thủ thư chọn "Thêm thể loại".<br>4. Hệ thống hiển thị form gồm tên và mô tả.<br>5. Thủ thư nhập thông tin và nhấn "Lưu".<br>6. Hệ thống kiểm tra tên thể loại không trống và không trùng với thể loại khác.<br>7. Hệ thống lưu thể loại và thông báo thêm thành công. |
| **Dòng sự kiện phụ** | 3a. Thủ thư chọn sửa một thể loại: hệ thống hiển thị form với thông tin hiện tại. Thủ thư sửa và nhấn "Lưu". Hệ thống kiểm tra như bước 6 rồi cập nhật.<br>3b. Thủ thư chọn xóa một thể loại và xác nhận: nếu không còn sách nào thuộc thể loại này, hệ thống xóa thể loại. Ngược lại, hệ thống báo không thể xóa và cho biết số sách còn thuộc thể loại.<br>6a. Tên trống hoặc đã tồn tại: hệ thống báo lỗi, quay lại bước 5. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. |
| **Hậu điều kiện** | Danh mục thể loại được cập nhật và có thể chọn khi thêm hoặc sửa sách. |

**Bảng 2.6. Use Case Quản lý độc giả**

| **Tên use case** | Quản lý độc giả |
|---|---|
| **Tóm tắt** | Thủ thư thêm độc giả và cấp tài khoản, sửa thông tin, khóa và mở khóa thẻ, gia hạn thẻ, đặt lại mật khẩu cho độc giả. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Quản lý độc giả".<br>2. Hệ thống hiển thị danh sách độc giả gồm mã thẻ, họ tên, số điện thoại, ngày hết hạn thẻ và trạng thái thẻ.<br>3. Thủ thư chọn "Thêm độc giả".<br>4. Hệ thống hiển thị form gồm họ tên, ngày sinh, CCCD, số điện thoại, email và địa chỉ.<br>5. Thủ thư nhập thông tin và nhấn "Lưu".<br>6. Hệ thống kiểm tra: họ tên, CCCD, số điện thoại không trống; email đúng định dạng; CCCD chưa được dùng cho độc giả khác.<br>7. Hệ thống tạo hồ sơ độc giả với mã thẻ mới, ngày cấp là hôm nay, ngày hết hạn sau 1 năm, trạng thái *Hoạt động*. Đồng thời tạo tài khoản vai trò độc giả với tên đăng nhập là mã thẻ và mật khẩu mặc định.<br>8. Hệ thống hiển thị thông tin tài khoản vừa cấp và thông báo thêm thành công. |
| **Dòng sự kiện phụ** | 3a. Thủ thư chọn sửa thông tin một độc giả: hệ thống hiển thị form với thông tin hiện tại. Thủ thư sửa và nhấn "Lưu". Hệ thống kiểm tra như bước 6 rồi cập nhật.<br>3b. Thủ thư chọn khóa thẻ và nhập lý do: hệ thống chuyển thẻ sang *Bị khóa* và lưu lý do.<br>3c. Thủ thư chọn mở khóa thẻ: hệ thống chuyển thẻ về *Hoạt động*.<br>3d. Thủ thư chọn gia hạn thẻ: hệ thống cộng thêm 1 năm vào ngày hết hạn.<br>3e. Thủ thư chọn đặt lại mật khẩu: hệ thống đặt mật khẩu của độc giả về mặc định.<br>6a. Dữ liệu không hợp lệ hoặc CCCD đã tồn tại: hệ thống báo lỗi tại trường tương ứng, quay lại bước 5. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. |
| **Hậu điều kiện** | Hồ sơ độc giả và tài khoản đăng nhập được tạo hoặc cập nhật. |

**Bảng 2.7. Use Case Lập phiếu mượn**

| **Tên use case** | Lập phiếu mượn |
|---|---|
| **Tóm tắt** | Thủ thư lập phiếu mượn cho độc giả tại quầy sau khi hệ thống kiểm tra đủ điều kiện mượn. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Lập phiếu mượn".<br>2. Thủ thư nhập mã thẻ của độc giả.<br>3. Hệ thống hiển thị thông tin độc giả, số sách đang mượn, và kiểm tra: thẻ đang *Hoạt động*, thẻ chưa hết hạn, độc giả không có phiếu quá hạn.<br>4. Thủ thư tìm và chọn các sách độc giả muốn mượn.<br>5. Hệ thống kiểm tra: mỗi sách còn ít nhất 1 cuốn, tổng số sách đang mượn và mượn thêm không vượt quá 5.<br>6. Hệ thống đề xuất hạn trả là ngày mượn + 14 ngày.<br>7. Thủ thư xác nhận hoặc chỉnh hạn trả rồi nhấn "Lập phiếu".<br>8. Hệ thống tạo phiếu mượn với trạng thái *Đang mượn*, trừ số còn lại của từng sách và thông báo lập phiếu thành công. |
| **Dòng sự kiện phụ** | 2a. Không tìm thấy mã thẻ: hệ thống báo lỗi, quay lại bước 2.<br>3a. Thẻ bị khóa, thẻ hết hạn hoặc độc giả có phiếu quá hạn: hệ thống báo lý do không được mượn, kết thúc use case.<br>5a. Có sách đã hết: hệ thống báo sách hết và bỏ sách đó khỏi danh sách, quay lại bước 4.<br>5b. Vượt quá 5 cuốn: hệ thống báo số cuốn độc giả còn được mượn, quay lại bước 4.<br>5c. Độc giả đang có yêu cầu đặt trước *Chờ duyệt* hoặc *Đã duyệt* cho sách được mượn: ở bước 8, hệ thống chuyển yêu cầu đó sang *Hoàn tất*.<br>7a. Hạn trả không sau ngày mượn: hệ thống báo lỗi, quay lại bước 7. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. Độc giả có mặt tại quầy cùng sách muốn mượn. |
| **Hậu điều kiện** | Phiếu mượn mới được lưu, số còn lại của các sách giảm tương ứng. |

**Bảng 2.8. Use Case Nhận trả sách**

| **Tên use case** | Nhận trả sách |
|---|---|
| **Tóm tắt** | Thủ thư nhận sách độc giả trả, ghi tình trạng sách, tính tiền phạt và cộng sách lại vào kho. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Nhận trả sách".<br>2. Thủ thư nhập mã thẻ độc giả hoặc mã phiếu mượn.<br>3. Hệ thống hiển thị các phiếu *Đang mượn* cùng danh sách sách chưa trả và hạn trả.<br>4. Thủ thư chọn các sách được trả và ghi tình trạng từng cuốn: *Bình thường*, *Hư hỏng* hoặc *Mất*.<br>5. Hệ thống tính tiền phạt cho từng cuốn: quá hạn tính 5.000đ cho mỗi ngày trễ, hư hỏng theo mức Thủ thư nhập, mất bằng giá sách. Hệ thống hiển thị tổng tiền phạt.<br>6. Thủ thư xác nhận sau khi độc giả đã nộp phạt (nếu có).<br>7. Hệ thống ghi ngày trả, tình trạng và tiền phạt của từng cuốn. Sách *Bình thường* và *Hư hỏng* được cộng lại vào số còn lại. Sách *Mất* bị trừ khỏi số lượng tổng.<br>8. Nếu mọi sách trong phiếu đã trả, hệ thống chuyển phiếu sang *Đã trả*; yêu cầu gia hạn đang *Chờ duyệt* của phiếu (nếu có) tự chuyển *Từ chối* với lý do "Phiếu đã trả hết sách trước khi được duyệt". Hệ thống thông báo nhận trả thành công. |
| **Dòng sự kiện phụ** | 3a. Độc giả không có phiếu nào đang mượn: hệ thống báo không có sách cần trả, kết thúc use case.<br>5a. Sách trả đúng hạn và tình trạng bình thường: tiền phạt bằng 0.<br>7a. Sách vừa trả đang có yêu cầu đặt trước *Đã duyệt*: hệ thống hiển thị danh sách người chờ theo ngày đặt (họ tên, số điện thoại) để Thủ thư liên hệ người sớm nhất.<br>8a. Phiếu còn sách chưa trả: phiếu giữ trạng thái *Đang mượn*. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. Độc giả có phiếu mượn đang mở. |
| **Hậu điều kiện** | Thông tin trả sách và tiền phạt được lưu vào phiếu mượn, số lượng sách trong kho được cập nhật. |

**Bảng 2.9. Use Case Gia hạn phiếu mượn**

| **Tên use case** | Gia hạn phiếu mượn |
|---|---|
| **Tóm tắt** | Thủ thư duyệt hoặc từ chối yêu cầu gia hạn do độc giả gửi online, hoặc gia hạn trực tiếp tại quầy. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include). Xử lý các yêu cầu được tạo từ use case Yêu cầu gia hạn. |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Gia hạn phiếu mượn".<br>2. Hệ thống hiển thị danh sách yêu cầu gia hạn *Chờ duyệt* gồm độc giả, mã phiếu, hạn trả hiện tại và số lần đã gia hạn.<br>3. Thủ thư chọn một yêu cầu.<br>4. Hệ thống kiểm tra: phiếu chưa quá hạn, số lần đã gia hạn nhỏ hơn 2, không ai đang đặt trước sách trong phiếu, thẻ độc giả đang *Hoạt động*.<br>5. Thủ thư nhấn "Duyệt".<br>6. Hệ thống cộng thêm 7 ngày vào hạn trả, tăng số lần gia hạn thêm 1, chuyển yêu cầu sang *Đã duyệt* và thông báo thành công. |
| **Dòng sự kiện phụ** | 1a. Độc giả nhờ gia hạn tại quầy: Thủ thư nhập mã phiếu thay vì chọn yêu cầu, tiếp tục từ bước 4.<br>4a. Phiếu không đủ điều kiện: hệ thống hiển thị lý do, Thủ thư chỉ có thể từ chối (bước 5a).<br>5a. Thủ thư nhấn "Từ chối" và nhập lý do: hệ thống chuyển yêu cầu sang *Từ chối*, hạn trả giữ nguyên. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. Có yêu cầu gia hạn đang chờ, hoặc độc giả có mặt tại quầy. |
| **Hậu điều kiện** | Hạn trả và số lần gia hạn của phiếu được cập nhật, hoặc yêu cầu bị từ chối kèm lý do. |

**Bảng 2.10. Use Case Xử lý đặt trước**

| **Tên use case** | Xử lý đặt trước |
|---|---|
| **Tóm tắt** | Thủ thư duyệt, từ chối hoặc hủy các yêu cầu đặt trước, và theo dõi những người cần liên hệ khi sách đã về. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include). Xử lý các yêu cầu được tạo từ use case Đặt trước sách. |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Xử lý đặt trước".<br>2. Hệ thống hiển thị danh sách yêu cầu theo trạng thái, gồm độc giả, sách và ngày đặt. Với các yêu cầu *Đã duyệt*, hệ thống đánh dấu những sách hiện đã có cuốn trong kho.<br>3. Thủ thư chọn một yêu cầu *Chờ duyệt*.<br>4. Hệ thống hiển thị chi tiết: độc giả, số điện thoại, tình trạng thẻ, sách, ngày đặt và số còn lại của sách.<br>5. Thủ thư nhấn "Duyệt".<br>6. Hệ thống chuyển yêu cầu sang *Đã duyệt* và thông báo thành công. |
| **Dòng sự kiện phụ** | 3a. Thủ thư chọn một yêu cầu *Đã duyệt* có sách đã về kho: hệ thống hiển thị số điện thoại và email của độc giả để Thủ thư liên hệ.<br>3b. Thủ thư chọn hủy một yêu cầu *Đã duyệt* (ví dụ không liên hệ được độc giả) và nhập lý do: hệ thống chuyển yêu cầu sang *Đã hủy*.<br>4a. Thẻ độc giả bị khóa hoặc hết hạn: hệ thống cảnh báo và không cho duyệt; Thủ thư từ chối kèm lý do (bước 5a).<br>5a. Thủ thư nhấn "Từ chối" và nhập lý do: hệ thống chuyển yêu cầu sang *Từ chối*.<br>5b. Yêu cầu vừa được xử lý ở nơi khác (độc giả tự hủy, Thủ thư khác đã xử lý): hệ thống báo yêu cầu đã thay đổi trạng thái, không ghi đè. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. |
| **Hậu điều kiện** | Trạng thái các yêu cầu đặt trước được cập nhật. |

**Bảng 2.11. Use Case Tìm kiếm**

| **Tên use case** | Tìm kiếm |
|---|---|
| **Tóm tắt** | Thủ thư tìm sách, độc giả hoặc phiếu mượn theo từ khóa và bộ lọc. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Tìm kiếm".<br>2. Thủ thư chọn đối tượng cần tìm: Sách, Độc giả hoặc Phiếu mượn.<br>3. Hệ thống hiển thị ô từ khóa và bộ lọc tương ứng: sách theo tên, tác giả, thể loại; độc giả theo mã thẻ, họ tên, số điện thoại, CCCD; phiếu mượn theo mã phiếu, mã thẻ, trạng thái, khoảng ngày mượn.<br>4. Thủ thư nhập từ khóa, chọn bộ lọc và nhấn "Tìm".<br>5. Hệ thống hiển thị danh sách kết quả.<br>6. Thủ thư chọn một kết quả, hệ thống hiển thị chi tiết. |
| **Dòng sự kiện phụ** | 5a. Không có kết quả phù hợp: hệ thống báo "Không tìm thấy kết quả", quay lại bước 4. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. |
| **Hậu điều kiện** | Kết quả tìm kiếm được hiển thị, dữ liệu không thay đổi. |

**Bảng 2.12. Use Case Thống kê**

| **Tên use case** | Thống kê |
|---|---|
| **Tóm tắt** | Thủ thư xem các báo cáo thống kê về hoạt động mượn trả và tiền phạt. |
| **Tác nhân** | Thủ thư |
| **Use case liên quan** | Đăng nhập (include) |
| **Dòng sự kiện chính** | 1. Thủ thư chọn "Thống kê".<br>2. Thủ thư chọn loại thống kê: top sách mượn nhiều, phiếu quá hạn, lượt mượn theo tháng, lượt mượn theo thể loại, tổng tiền phạt.<br>3. Thủ thư chọn khoảng thời gian và nhấn "Xem". Thống kê tiền phạt tính theo ngày trả sách (ngày phát sinh tiền phạt); các thống kê lượt mượn tính theo ngày mượn.<br>4. Hệ thống tổng hợp dữ liệu và hiển thị kết quả dạng bảng và biểu đồ. |
| **Dòng sự kiện phụ** | 3a. Ngày bắt đầu sau ngày kết thúc: hệ thống báo lỗi, quay lại bước 3.<br>4a. Không có dữ liệu trong khoảng thời gian đã chọn: hệ thống báo không có dữ liệu. |
| **Điều kiện tiên quyết** | Thủ thư đã đăng nhập. |
| **Hậu điều kiện** | Kết quả thống kê được hiển thị, dữ liệu không thay đổi. |

**Bảng 2.13. Use Case Tìm kiếm sách**

| **Tên use case** | Tìm kiếm sách |
|---|---|
| **Tóm tắt** | Độc giả tìm sách theo tên, tác giả, thể loại và xem chi tiết sách cùng tình trạng còn hay hết. |
| **Tác nhân** | Độc giả |
| **Use case liên quan** | Đăng nhập (include), được mở rộng (extend) bởi Đặt trước sách |
| **Dòng sự kiện chính** | 1. Độc giả chọn "Tìm sách".<br>2. Độc giả nhập tên sách hoặc tác giả, có thể chọn thể loại, rồi nhấn "Tìm".<br>3. Hệ thống hiển thị danh sách sách gồm tên, tác giả, thể loại và tình trạng *Còn* hoặc *Hết*.<br>4. Độc giả chọn một sách.<br>5. Hệ thống hiển thị chi tiết sách: tên, tác giả, thể loại, nhà xuất bản, năm xuất bản và số cuốn còn lại. |
| **Dòng sự kiện phụ** | 3a. Không có sách phù hợp: hệ thống báo "Không tìm thấy sách", quay lại bước 2.<br>5a. Sách đang hết và độc giả chọn "Đặt trước": thực hiện use case Đặt trước sách. |
| **Điều kiện tiên quyết** | Độc giả đã đăng nhập. |
| **Hậu điều kiện** | Độc giả xem được thông tin và tình trạng sách. |

**Bảng 2.14. Use Case Xem sách đang mượn và lịch sử mượn**

| **Tên use case** | Xem sách đang mượn và lịch sử mượn |
|---|---|
| **Tóm tắt** | Độc giả xem các sách đang mượn cùng hạn trả, kết quả các yêu cầu gia hạn, và lịch sử các lần mượn trước. |
| **Tác nhân** | Độc giả |
| **Use case liên quan** | Đăng nhập (include), được mở rộng (extend) bởi Yêu cầu gia hạn |
| **Dòng sự kiện chính** | 1. Độc giả chọn "Sách đang mượn".<br>2. Hệ thống hiển thị tab "Đang mượn" gồm tên sách, ngày mượn, hạn trả, số lần đã gia hạn. Phiếu quá hạn được đánh dấu nổi bật.<br>3. Độc giả chọn tab "Lịch sử gia hạn".<br>4. Hệ thống hiển thị các yêu cầu gia hạn của độc giả gồm mã phiếu, tên sách, ngày gửi, hình thức (online / tại quầy) và kết quả (*Chờ duyệt*, *Đã duyệt*, *Từ chối*).<br>5. Độc giả chọn "Lịch sử mượn" ở menu.<br>6. Hệ thống hiển thị mọi cuốn sách độc giả đã mượn (cả cuốn đang mượn và cuốn đã trả), mới mượn nhất lên đầu, gồm tên sách, mã phiếu, ngày mượn, hạn trả (đã cộng các lần gia hạn được duyệt, ghi kèm số lần gia hạn), ngày trả (cuốn chưa trả ghi "Chưa trả"), tình trạng (*Đang mượn* / *Quá hạn* với cuốn chưa trả; tình trạng sách khi trả với cuốn đã trả) và tiền phạt (nếu có). |
| **Dòng sự kiện phụ** | 2a. Độc giả không có sách đang mượn: hệ thống báo "Bạn chưa mượn sách nào".<br>2b. Độc giả chọn "Gia hạn" ở một phiếu: thực hiện use case Yêu cầu gia hạn.<br>4a. Chưa có yêu cầu gia hạn: hệ thống báo "Bạn chưa gửi yêu cầu gia hạn nào".<br>4b. Độc giả chọn một yêu cầu bị *Từ chối*: hệ thống hiển thị lý do từ chối do Thủ thư nhập và ngày xử lý.<br>6a. Chưa có lịch sử mượn: hệ thống báo "Chưa có lịch sử mượn". |
| **Điều kiện tiên quyết** | Độc giả đã đăng nhập. |
| **Hậu điều kiện** | Độc giả xem được tình hình mượn sách của mình, dữ liệu không thay đổi. |

**Bảng 2.15. Use Case Yêu cầu gia hạn**

| **Tên use case** | Yêu cầu gia hạn |
|---|---|
| **Tóm tắt** | Độc giả gửi yêu cầu gia hạn phiếu mượn để Thủ thư duyệt. |
| **Tác nhân** | Độc giả |
| **Use case liên quan** | Đăng nhập (include), mở rộng (extend) use case Xem sách đang mượn và lịch sử mượn |
| **Dòng sự kiện chính** | 1. Tại tab "Đang mượn", độc giả chọn "Gia hạn" ở một phiếu.<br>2. Hệ thống kiểm tra: thẻ đang *Hoạt động*, phiếu chưa quá hạn, số lần đã gia hạn nhỏ hơn 2, phiếu chưa có yêu cầu gia hạn đang chờ.<br>3. Hệ thống hiển thị hạn trả hiện tại và hạn trả dự kiến sau khi gia hạn (thêm 7 ngày).<br>4. Độc giả nhấn "Gửi yêu cầu".<br>5. Hệ thống lưu yêu cầu với trạng thái *Chờ duyệt* và thông báo đã gửi yêu cầu. |
| **Dòng sự kiện phụ** | 2a. Không đủ điều kiện gia hạn: hệ thống hiển thị lý do, kết thúc use case.<br>4a. Độc giả nhấn "Hủy": quay lại tab "Đang mượn". |
| **Điều kiện tiên quyết** | Độc giả đã đăng nhập và có phiếu đang mượn. |
| **Hậu điều kiện** | Yêu cầu gia hạn được lưu và chờ Thủ thư xử lý trong use case Gia hạn phiếu mượn. |

**Bảng 2.16. Use Case Đặt trước sách**

| **Tên use case** | Đặt trước sách |
|---|---|
| **Tóm tắt** | Độc giả đặt trước một sách đang hết để được thư viện liên hệ khi có sách. |
| **Tác nhân** | Độc giả |
| **Use case liên quan** | Đăng nhập (include), mở rộng (extend) use case Tìm kiếm sách |
| **Dòng sự kiện chính** | 1. Tại trang chi tiết của một sách đang hết, độc giả chọn "Đặt trước".<br>2. Hệ thống kiểm tra: thẻ đang *Hoạt động* và chưa hết hạn, độc giả không có phiếu mượn quá hạn, chưa có yêu cầu còn hiệu lực cho sách này, không đang mượn sách này.<br>3. Hệ thống hiển thị thông tin sách và lưu ý: thư viện sẽ liên hệ khi có sách, sách không được giữ riêng.<br>4. Độc giả nhấn "Xác nhận".<br>5. Hệ thống tạo yêu cầu đặt trước với ngày đặt là hôm nay, trạng thái *Chờ duyệt*, và thông báo đặt trước thành công. |
| **Dòng sự kiện phụ** | 1a. Sách đang còn: hệ thống không hiển thị nút "Đặt trước" và hướng dẫn độc giả đến quầy mượn.<br>1b. Độc giả mở "Đặt trước của tôi" và chọn hủy một yêu cầu *Chờ duyệt* hoặc *Đã duyệt*: hệ thống chuyển yêu cầu sang *Đã hủy*.<br>2a. Không đủ điều kiện: hệ thống hiển thị lý do, kết thúc use case. |
| **Điều kiện tiên quyết** | Độc giả đã đăng nhập. Sách đang hết. |
| **Hậu điều kiện** | Yêu cầu đặt trước được lưu và chờ Thủ thư xử lý trong use case Xử lý đặt trước. |
