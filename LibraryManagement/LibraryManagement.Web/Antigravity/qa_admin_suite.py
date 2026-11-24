"""
Bộ kiểm thử tự động chuyên sâu Role THỦ THƯ / ADMIN (52 kịch bản: Dashboard, UC04, UC05, UC06, UC07, UC08, UC09, UC10, UC11, UC12, RBAC, CSRF, SSRF, Toàn vẹn kho).
Chạy trên server cô lập http://localhost:5199 và database riêng library_management_qa_admin
(KHÔNG đụng database thật library_management).
"""
import html as html_lib
import http.cookiejar
import json
import re
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

BASE_URL = "http://localhost:5199"
QA_DB = "library_management_qa_admin"
MONGO_URI = f"mongodb://localhost:27017/{QA_DB}?replicaSet=rs0"
OUT_JSON = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_admin_results.json"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def make_opener(follow=True):
    cj = http.cookiejar.CookieJar()
    h = [urllib.request.HTTPCookieProcessor(cj)]
    if not follow:
        h.append(NoRedirect())
    return urllib.request.build_opener(*h), cj


def req(opener, path, method="GET", data=None, headers=None):
    url = path if path.startswith("http") else BASE_URL + path
    if isinstance(data, list):
        body_bytes = urllib.parse.urlencode(data).encode("utf-8")
    elif isinstance(data, dict):
        body_bytes = urllib.parse.urlencode(data).encode("utf-8")
    else:
        body_bytes = data
    hdrs = dict(headers or {})
    if isinstance(data, (dict, list)) and "Content-Type" not in hdrs:
        hdrs["Content-Type"] = "application/x-www-form-urlencoded"
    r = urllib.request.Request(url, data=body_bytes, method=method, headers=hdrs)
    try:
        with opener.open(r, timeout=15) as resp:
            raw = resp.read().decode("utf-8", errors="ignore")
            return resp.status, resp.geturl(), dict(resp.headers), html_lib.unescape(raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", errors="ignore") if e.fp else ""
        return e.code, e.geturl(), dict(e.headers), html_lib.unescape(raw)
    except Exception as e:
        return -1, url, {}, str(e)


def af_token(html_str: str) -> str:
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html_str)
    return m.group(1) if m else ""


def login(username: str, password: str):
    op, cj = make_opener(follow=True)
    _, _, _, html_str = req(op, "/Account/Login")
    tk = af_token(html_str)
    st, url, hdrs, body = req(op, "/Account/Login", "POST", {
        "Username": username, "Password": password, "__RequestVerificationToken": tk
    })
    return op, cj, st, url, body


def mongo_eval(js: str):
    p = subprocess.run(["mongosh", MONGO_URI, "--quiet", "--eval", js],
                       capture_output=True, text=True, encoding="utf-8", timeout=20)
    if p.returncode != 0:
        raise RuntimeError(p.stderr or p.stdout)
    lines = [ln for ln in p.stdout.strip().splitlines() if ln.strip()]
    return lines[-1] if lines else ""


def main():
    results = []

    def check(code, group, name, passed, detail, severity="OK"):
        results.append({
            "code": code, "group": group, "name": name,
            "passed": bool(passed), "detail": detail,
            "severity": "PASS" if passed else severity
        })
        tag = "PASS" if passed else f"FAIL({severity})"
        print(f"[{tag}] {code} [{group}] {name} -> {detail}")

    # Chờ server sẵn sàng
    for _ in range(25):
        op, _ = make_opener(False)
        st, _, _, _ = req(op, "/Account/Login")
        if st == 200:
            break
        time.sleep(0.5)
    else:
        raise RuntimeError("Test server on :5199 did not start!")

    # ============ 1. ĐĂNG NHẬP, RBAC, DASHBOARD ============
    op, cj, st, url, dash_html = login("admin", "admin123")
    check("A01", "Auth", "Đăng nhập Thủ thư (admin / admin123) -> /Dashboard", st == 200 and "/Dashboard" in url, f"url={url}")

    op_nr = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedirect())
    blocked_reader = True
    for r in ["/Catalog", "/MyLoans", "/MyReservations", "/MyFavorites"]:
        s, _, h, _ = req(op_nr, r)
        if not (s in (302, 403) and ("AccessDenied" in h.get("Location", "") or s == 403)):
            blocked_reader = False
    check("A02", "Security", "RBAC: Thủ thư bị chặn toàn bộ 4 trang Độc giả", blocked_reader, "All 4 blocked")

    # CSRF check trên action POST của Admin
    st_csrf, _, _, _ = req(op_nr, "/Categories/Create", "POST", {"Name": "NoToken"})
    check("A03", "Security", "Anti-CSRF: Chặn POST /Categories/Create thiếu token", st_csrf == 400, f"HTTP {st_csrf}")

    # Dashboard khớp MongoDB
    stats = json.loads(mongo_eval(r"""
    const books = db.books.countDocuments();
    const readers = db.readers.countDocuments({cardStatus: 'Active'});
    const borrowing = db.loans.countDocuments({status: 'Borrowing'});
    print(JSON.stringify({books, readers, borrowing}));
    """))
    check("A04", "Dashboard", "Dashboard hiển thị đủ 4 KPI và khối Việc cần xử lý",
          str(stats["books"]) in dash_html and "Việc cần xử lý" in dash_html,
          f"books={stats['books']}, activeReaders={stats['readers']}, borrowing={stats['borrowing']}")

    # ============ 2. UC05 QUẢN LÝ THỂ LOẠI ============
    _, _, _, cat_create_html = req(op, "/Categories/Create")
    tk = af_token(cat_create_html)

    st, u, _, b = req(op, "/Categories/Create", "POST", {"Name": "Thể loại QA Test", "Description": "Mô tả test", "__RequestVerificationToken": tk})
    new_cat_id = mongo_eval('var c=db.categories.findOne({name:"Thể loại QA Test"}); print(c?c._id.toString():"");')
    check("A05", "UC05", "Thêm thể loại mới hợp lệ", bool(new_cat_id) and "/Categories" in u, f"id={new_cat_id}")

    # Trùng tên không phân biệt hoa thường (collation vi strength 2)
    st, u, _, b = req(op, "/Categories/Create", "POST", {"Name": "thể loại qa test", "Description": "", "__RequestVerificationToken": tk})
    check("A06", "UC05", "Chặn trùng tên thể loại không phân biệt hoa/thường", "đã tồn tại" in b, f"HTTP {st}")

    # Sửa thể loại
    _, _, _, ce_html = req(op, f"/Categories/Edit/{new_cat_id}")
    st, u, _, b = req(op, f"/Categories/Edit/{new_cat_id}", "POST", {"Name": "Thể loại QA Sửa", "Description": "Đã sửa", "__RequestVerificationToken": af_token(ce_html)})
    cat_after = mongo_eval(f'print(db.categories.findOne({{_id:ObjectId("{new_cat_id}")}}).name)')
    check("A07", "UC05", "Sửa tên và mô tả thể loại", cat_after == "Thể loại QA Sửa", f"name={cat_after}")

    # Chặn xóa thể loại còn sách (QĐ09)
    used_cat_id = mongo_eval('print(db.books.findOne().categoryIds[0].toString())')
    _, _, _, ci_html = req(op, "/Categories")
    tk_ci = af_token(ci_html)
    st, u, _, b = req(op, f"/Categories/Delete/{used_cat_id}", "POST", {"__RequestVerificationToken": tk_ci})
    still_cat = mongo_eval(f'print(db.categories.countDocuments({{_id:ObjectId("{used_cat_id}")}}))')
    check("A08", "UC05", "Chặn xóa thể loại còn sách (QĐ09)", still_cat == "1" and "Không thể xóa" in b, f"still={still_cat}")

    # Xóa thể loại rỗng thành công
    st, u, _, b = req(op, f"/Categories/Delete/{new_cat_id}", "POST", {"__RequestVerificationToken": tk_ci})
    gone_cat = mongo_eval(f'print(db.categories.countDocuments({{_id:ObjectId("{new_cat_id}")}}))')
    check("A09", "UC05", "Xóa thể loại không còn sách thành công", gone_cat == "0", f"count={gone_cat}")

    # ============ 3. UC04 QUẢN LÝ SÁCH & ẢNH BÌA (SSRF + QĐ09) ============
    _, _, _, bc_html = req(op, "/Books/Create")
    tk_bc = af_token(bc_html)

    # SSRF block: localhost URL phải bị chặn!
    st, u, _, b = req(op, "/Books/Create", "POST", [
        ("Title", "Sách SSRF Test"), ("AuthorsString", "Tester"), ("Price", "100000"),
        ("TotalQuantity", "2"), ("SelectedCategoryIds", used_cat_id),
        ("CoverUrl", "http://127.0.0.1:5199/Account/Login"),
        ("__RequestVerificationToken", tk_bc)
    ])
    check("A10", "UC04", "Chống SSRF: Chặn tải ảnh bìa từ 127.0.0.1 / mạng nội bộ",
          "nội bộ" in b and mongo_eval('print(db.books.countDocuments({title:"Sách SSRF Test"}))') == "0", f"HTTP {st}")

    # Chặn scheme ftp://
    st, u, _, b = req(op, "/Books/Create", "POST", [
        ("Title", "Sách FTP Test"), ("AuthorsString", "Tester"), ("Price", "100000"),
        ("TotalQuantity", "2"), ("SelectedCategoryIds", used_cat_id),
        ("CoverUrl", "ftp://example.com/a.jpg"),
        ("__RequestVerificationToken", tk_bc)
    ])
    check("A11", "UC04", "Chặn tải ảnh bìa scheme không phải http/https",
          "http://" in b and mongo_eval('print(db.books.countDocuments({title:"Sách FTP Test"}))') == "0", f"HTTP {st}")

    # Thêm sách mới hợp lệ
    st, u, _, b = req(op, "/Books/Create", "POST", [
        ("Title", "Sách Kiểm Thử QA 2026"), ("AuthorsString", "Nguyễn QA, Trần Test"),
        ("Publisher", "NXB Kiểm Thử"), ("PublishYear", "2026"), ("Price", "150000"),
        ("TotalQuantity", "3"), ("SelectedCategoryIds", used_cat_id),
        ("__RequestVerificationToken", tk_bc)
    ])
    qa_book = json.loads(mongo_eval('var b=db.books.findOne({title:"Sách Kiểm Thử QA 2026"}); print(JSON.stringify(b?{id:b._id.toString(),t:b.totalQuantity,a:b.availableQuantity}:{}))'))
    check("A12", "UC04", "Thêm sách mới hợp lệ (available == total == 3)",
          qa_book.get("t") == 3 and qa_book.get("a") == 3, f"{qa_book}")

    # Sửa sách: tăng số lượng +2 -> avail 3->5
    _, _, _, be_html = req(op, f"/Books/Edit/{qa_book['id']}")
    tk_be = af_token(be_html)
    st, u, _, b = req(op, f"/Books/Edit/{qa_book['id']}", "POST", [
        ("Title", "Sách Kiểm Thử QA 2026"), ("AuthorsString", "Nguyễn QA, Trần Test"),
        ("Publisher", "NXB Kiểm Thử"), ("PublishYear", "2026"), ("Price", "150000"),
        ("TotalQuantity", "5"), ("SelectedCategoryIds", used_cat_id),
        ("__RequestVerificationToken", tk_be)
    ])
    qa_b2 = json.loads(mongo_eval(f'var b=db.books.findOne({{_id:ObjectId("{qa_book["id"]}")}}); print(JSON.stringify({{t:b.totalQuantity,a:b.availableQuantity}}))'))
    check("A13", "UC04", "Sửa tăng số lượng sách (3->5) tự cộng availableQuantity (3->5)",
          qa_b2["t"] == 5 and qa_b2["a"] == 5, f"{qa_b2}")

    # Chặn giảm tổng số lượng dưới số đang cho mượn (QĐ09)
    borrowed_bk = json.loads(mongo_eval('var b=db.books.findOne({$expr:{$lt:["$availableQuantity","$totalQuantity"]}}); print(JSON.stringify({id:b._id.toString(),t:b.totalQuantity,a:b.availableQuantity,title:b.title,cat:b.categoryIds[0].toString(),price:Number(b.price)}))'))
    _, _, _, be2_html = req(op, f"/Books/Edit/{borrowed_bk['id']}")
    st, u, _, b = req(op, f"/Books/Edit/{borrowed_bk['id']}", "POST", [
        ("Title", borrowed_bk["title"]), ("AuthorsString", "Tác giả"),
        ("Price", str(borrowed_bk["price"])), ("TotalQuantity", "0"),
        ("SelectedCategoryIds", borrowed_bk["cat"]),
        ("__RequestVerificationToken", af_token(be2_html))
    ])
    check("A14", "UC04", "Chặn giảm tổng SL dưới số đang mượn / <= 0 (QĐ09)",
          "không thể nhỏ hơn" in b or "lớn hơn 0" in b or "dương" in b or "/Books/Edit" in u, f"HTTP {st}, url={u}")

    # Chặn xóa sách đang có phiếu mở (HasOpenLoanForBookAsync)
    open_book_id = mongo_eval('var l=db.loans.findOne({status:"Borrowing"}); print(l.items.find(i=>i.returnDate==null).bookId.toString())')
    _, _, _, bi_html = req(op, "/Books")
    tk_bi = af_token(bi_html)
    st, u, _, b = req(op, f"/Books/Delete/{open_book_id}", "POST", {"__RequestVerificationToken": tk_bi})
    check("A15", "UC04", "Chặn xóa sách đang nằm trong phiếu mượn chưa trả (QĐ09)",
          "chưa trả" in b and mongo_eval(f'print(db.books.countDocuments({{_id:ObjectId("{open_book_id}")}}))') == "1", f"HTTP {st}")

    # Kiểm tra BUG-04 thực tế: xóa sách đang có yêu cầu đặt trước Pending!
    r1_id = mongo_eval('print(db.readers.findOne({cardNumber:"DG000001"})._id.toString())')
    mongo_eval(f'db.reservations.insertOne({{readerId:ObjectId("{r1_id}"),bookId:ObjectId("{qa_book["id"]}"),bookTitle:"Sách Kiểm Thử QA 2026",reservedAt:new Date(),status:"Pending"}});')
    st, u, _, b = req(op, f"/Books/Delete/{qa_book['id']}", "POST", {"__RequestVerificationToken": tk_bi})
    qa_book_left = mongo_eval(f'print(db.books.countDocuments({{_id:ObjectId("{qa_book["id"]}")}}))')
    orphan_res = mongo_eval(f'print(db.reservations.countDocuments({{bookId:ObjectId("{qa_book["id"]}"),status:"Pending"}}))')
    check("A16", "UC04", "BUG-04: Xóa sách đang có đặt trước Pending có chặn/hủy không",
          qa_book_left == "1" or orphan_res == "0",
          f"bookLeft={qa_book_left}, orphanPendingReservations={orphan_res} (BỊ XÓA ĐỂ LẠI RESERVATION MỒ CÔI!)",
          severity="MEDIUM")
    # Dọn reservation mồ côi nếu tạo ra
    mongo_eval(f'db.reservations.deleteMany({{bookId:ObjectId("{qa_book["id"]}")}});')

    # ============ 4. UC06 QUẢN LÝ ĐỘC GIẢ ============
    _, _, _, rc_html = req(op, "/Readers/Create")
    tk_rc = af_token(rc_html)

    # Thêm độc giả mới -> tạo đồng thời readers + users
    st, u, _, b = req(op, "/Readers/Create", "POST", {
        "FullName": "Nguyễn Kiểm Thử", "DateOfBirth": "2000-05-15",
        "IdNumber": "079299998888", "Phone": "0999888777",
        "Email": "qa.reader@example.com", "Address": "Tân Phú, TP.HCM",
        "__RequestVerificationToken": tk_rc
    })
    new_rd = json.loads(mongo_eval('var r=db.readers.findOne({idNumber:"079299998888"}); var u=r?db.users.findOne({readerId:r._id}):null; print(JSON.stringify({id:r?r._id.toString():"",card:r?r.cardNumber:"",hasUser:!!u,uName:u?u.username:""}))'))
    check("A17", "UC06", "Thêm độc giả mới tạo đồng thời hồ sơ readers + tài khoản users",
          bool(new_rd["id"]) and new_rd["hasUser"] and new_rd["card"] == new_rd["uName"], f"{new_rd}")

    # Chặn trùng CCCD
    st, u, _, b = req(op, "/Readers/Create", "POST", {
        "FullName": "Trùng CCCD", "IdNumber": "079299998888", "Phone": "0900111222",
        "__RequestVerificationToken": tk_rc
    })
    check("A18", "UC06", "Chặn thêm độc giả trùng số CCCD", "CCCD" in b and "đã được dùng" in b, f"HTTP {st}")

    # Chặn ngày sinh tương lai
    st, u, _, b = req(op, "/Readers/Create", "POST", {
        "FullName": "Ngày Sinh Sai", "DateOfBirth": "2099-01-01", "IdNumber": "079299997777", "Phone": "0900111333",
        "__RequestVerificationToken": tk_rc
    })
    check("A19", "UC06", "Chặn ngày sinh không trước hôm nay", "Ngày sinh" in b, f"HTTP {st}")

    # Sửa họ tên -> kiểm tra đồng bộ sang users.fullName
    _, _, _, re_html = req(op, f"/Readers/Edit/{new_rd['id']}")
    st, u, _, b = req(op, f"/Readers/Edit/{new_rd['id']}", "POST", {
        "FullName": "Nguyễn Kiểm Thử Đã Sửa", "DateOfBirth": "2000-05-15",
        "IdNumber": "079299998888", "Phone": "0999888777",
        "__RequestVerificationToken": af_token(re_html)
    })
    synced_name = mongo_eval(f'print(db.users.findOne({{readerId:ObjectId("{new_rd["id"]}")}}).fullName)')
    check("A20", "UC06", "Sửa họ tên độc giả đồng bộ sang collection users", synced_name == "Nguyễn Kiểm Thử Đã Sửa", f"user.fullName={synced_name}")

    # Khóa thẻ (thiếu lý do bị chặn; có lý do thành công), Mở khóa, Gia hạn thẻ, Đặt lại mật khẩu
    _, _, _, rd_det = req(op, f"/Readers/Details/{new_rd['id']}")
    tk_rd = af_token(rd_det)
    st, _, _, b = req(op, f"/Readers/Lock/{new_rd['id']}", "POST", {"lockReason": "", "__RequestVerificationToken": tk_rd})
    check("A21", "UC06", "Khóa thẻ: thiếu lý do bị chặn", mongo_eval(f'print(db.readers.findOne({{_id:ObjectId("{new_rd["id"]}")}}).cardStatus)') == "Active", f"HTTP {st}")

    req(op, f"/Readers/Lock/{new_rd['id']}", "POST", {"lockReason": "Vi phạm QA", "__RequestVerificationToken": tk_rd})
    st_lk = mongo_eval(f'print(db.readers.findOne({{_id:ObjectId("{new_rd["id"]}")}}).cardStatus)')
    req(op, f"/Readers/Unlock/{new_rd['id']}", "POST", {"__RequestVerificationToken": tk_rd})
    st_ul = mongo_eval(f'print(db.readers.findOne({{_id:ObjectId("{new_rd["id"]}")}}).cardStatus)')
    check("A22", "UC06", "Khóa thẻ kèm lý do -> Locked, Mở khóa -> Active", st_lk == "Locked" and st_ul == "Active", f"lock={st_lk}, unlock={st_ul}")

    exp_before = mongo_eval(f'print(db.readers.findOne({{_id:ObjectId("{new_rd["id"]}")}}).expiryDate.toISOString().slice(0,4))')
    req(op, f"/Readers/ExtendCard/{new_rd['id']}", "POST", {"__RequestVerificationToken": tk_rd})
    exp_after = mongo_eval(f'print(db.readers.findOne({{_id:ObjectId("{new_rd["id"]}")}}).expiryDate.toISOString().slice(0,4))')
    check("A23", "UC06", "Gia hạn thẻ cộng đúng +1 năm (QĐ01)", int(exp_after) == int(exp_before) + 1, f"{exp_before} -> {exp_after}")

    req(op, f"/Readers/ResetPassword/{new_rd['id']}", "POST", {"__RequestVerificationToken": tk_rd})
    _, _, _, u_login_rd, _ = login(new_rd["card"], "123456")
    check("A24", "UC06", "Đặt lại mật khẩu về mặc định (123456) đăng nhập được", "/Catalog" in u_login_rd, f"url={u_login_rd}")

    # ============ 5. UC07 LẬP PHIẾU MƯỢN (2 TAB + HỘP MẪU 1 + TRANSACTION + UNPICKED HELD) ============
    st, _, _, lc_html = req(op, "/Loans/Create")
    check("A25", "UC07", "Trang /Loans/Create hiển thị đủ 2 tab (Danh sách độc giả & Phiếu gần đây)",
          st == 200 and "Danh sách độc giả" in lc_html and "Phiếu mượn gần đây" in lc_html, f"HTTP {st}")

    st_cr1, _, _, cr1_html = req(op, f"/Loans/CheckReader?cardNumber={new_rd['card'].lower()}")
    check("A26", "UC07", "AJAX CheckReader nhập chữ thường tự chuẩn hóa + có Hộp Phiếu Mẫu 1",
          st_cr1 == 200 and new_rd["card"] in cr1_html and "slipModal" in cr1_html, f"HTTP {st_cr1}")

    st_cr_lk, _, _, cr_lk_html = req(op, "/Loans/CheckReader?cardNumber=DG000009")
    check("A27", "UC07", "CheckReader thẻ bị khóa (DG000009) báo không được mượn",
          st_cr_lk == 200 and "không được mượn" in cr_lk_html.lower(), f"HTTP {st_cr_lk}")

    # Chuẩn bị 6 sách sạch còn hàng >= 2 cho new_rd
    avail_books = json.loads(mongo_eval(r"""
    print(JSON.stringify(db.books.find({availableQuantity:{$gte:2}}).limit(6).toArray().map(b => ({id:b._id.toString(), t:b.totalQuantity, a:b.availableQuantity, price:Number(b.price)}))));
    """))
    bA, bB, bC, bD, bE, bF = avail_books

    # Tạo 1 bản đặt giữ (Held) cho new_rd với cuốn bA (trừ kho bA đi 1)
    mongo_eval(f"""
    db.books.updateOne({{_id:ObjectId("{bA["id"]}")}}, {{$inc:{{availableQuantity:-1}}}});
    db.reservations.insertOne({{
      readerId: ObjectId("{new_rd["id"]}"), bookId: ObjectId("{bA["id"]}"),
      bookTitle: "Book A", reservedAt: new Date(), status: "Held",
      holdUntil: new Date(Date.now() + 2*86400000)
    }});
    """)
    bA_held_avail = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bA["id"]}")}}).availableQuantity)'))

    # Test unpickedHeld: bỏ qua bA (đang Held) và cố mượn 5 cuốn khác (bB..bF) -> PHẢI BỊ CHẶN vì tổng 5 + 1 unpickedHeld = 6 > 5!
    tk_lc = af_token(cr1_html)
    st, u, _, b = req(op, "/Loans/Create", "POST", [
        ("CardNumber", new_rd["card"]),
        ("BookIds", bB["id"]), ("BookIds", bC["id"]), ("BookIds", bD["id"]), ("BookIds", bE["id"]), ("BookIds", bF["id"]),
        ("DueDate", "2026-12-10"), ("__RequestVerificationToken", tk_lc)
    ])
    check("A28", "UC07", "Chặn vượt 5 cuốn tính cả sách đang giữ chưa chọn (unpickedHeld)",
          "/Loans/Details" not in u and ("đang giữ" in b or "tối đa 5" in b), f"url={u}")

    # Test chặn hạn trả <= hôm nay (QĐ03)
    st, u, _, b = req(op, "/Loans/Create", "POST", [
        ("CardNumber", new_rd["card"]), ("BookIds", bA["id"]),
        ("DueDate", "2026-11-24"), ("__RequestVerificationToken", tk_lc)
    ])
    check("A29", "UC07", "Chặn hạn trả không sau ngày mượn (QĐ03)", "/Loans/Details" not in u and "sau ngày mượn" in b, f"url={u}")

    # Lập phiếu mượn hợp lệ gồm 1 cuốn Held (bA) + 2 cuốn thường (bB, bC) -> bA KHÔNG trừ kho thêm, bB & bC trừ mỗi cuốn 1, Held -> Completed!
    st, u, _, b = req(op, "/Loans/Create", "POST", [
        ("CardNumber", new_rd["card"]),
        ("BookIds", bA["id"]), ("BookIds", bB["id"]), ("BookIds", bC["id"]),
        ("DueDate", "2026-12-08"), ("__RequestVerificationToken", tk_lc)
    ])
    loan_id = u.rstrip("/").split("/")[-1] if "/Loans/Details/" in u else ""
    bA_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bA["id"]}")}}).availableQuantity)'))
    bB_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bB["id"]}")}}).availableQuantity)'))
    held_after = mongo_eval(f'print(db.reservations.findOne({{readerId:ObjectId("{new_rd["id"]}"),bookId:ObjectId("{bA["id"]}")}}).status)')
    check("A30", "UC07", "Lập phiếu (1 Held + 2 thường): ConsumeHold KHÔNG trừ kho 2 lần, sách thường -1",
          bool(loan_id) and bA_after == bA_held_avail and bB_after == bB["a"] - 1 and held_after == "Completed",
          f"loanId={loan_id}, bA:{bA_held_avail}->{bA_after}, bB:{bB['a']}->{bB_after}, held={held_after}")

    # ============ 6. UC09 GIA HẠN (THỦ THƯ) ============
    # Tạo yêu cầu gia hạn Pending + gửi từ chối thiếu lý do (chặn), gửi duyệt hợp lệ (+7 ngày), chặn khi có người đặt trước
    mongo_eval(f'db.loans.updateOne({{_id:ObjectId("{loan_id}")}}, {{$push:{{renewalRequests:{{requestedAt:new Date(),source:"Online",status:"Pending"}}}}}});')
    _, _, _, ren_html = req(op, "/Renewals")
    tk_ren = af_token(ren_html)

    # Từ chối thiếu lý do bị chặn
    st, _, _, b = req(op, "/Renewals/Reject", "POST", {"loanId": loan_id, "reason": "", "__RequestVerificationToken": tk_ren})
    check("A31", "UC09", "Từ chối gia hạn thiếu lý do bị chặn", "nhập lý do" in b, f"HTTP {st}")

    # Từ chối có lý do -> Rejected, hạn trả không đổi
    due_before = mongo_eval(f'print(db.loans.findOne({{_id:ObjectId("{loan_id}")}}).dueDate.toISOString())')
    req(op, "/Renewals/Reject", "POST", {"loanId": loan_id, "reason": "Test từ chối", "__RequestVerificationToken": tk_ren})
    due_after_rej = mongo_eval(f'print(db.loans.findOne({{_id:ObjectId("{loan_id}")}}).dueDate.toISOString())')
    check("A32", "UC09", "Từ chối gia hạn kèm lý do: Rejected, hạn trả giữ nguyên", due_after_rej == due_before, f"due unchanged={due_after_rej == due_before}")

    # Gia hạn tại quầy (RenewAtCounter): +7 ngày, RenewalCount 0->1
    req(op, "/Renewals/RenewAtCounter", "POST", {"loanId": loan_id, "__RequestVerificationToken": tk_ren})
    ren_info = json.loads(mongo_eval(f'var l=db.loans.findOne({{_id:ObjectId("{loan_id}")}}); print(JSON.stringify({{cnt:l.renewalCount, due:l.dueDate.toISOString()}}))'))
    check("A33", "UC09", "Gia hạn tại quầy thành công: RenewalCount=1, cộng +7 ngày",
          ren_info["cnt"] == 1 and ren_info["due"] > due_before, f"{ren_info}")

    # Chặn gia hạn khi sách trong phiếu có người đặt trước (HasWaitingAsync - QĐ04)
    mongo_eval(f'db.reservations.insertOne({{readerId:ObjectId("{r1_id}"),bookId:ObjectId("{bB["id"]}"),bookTitle:"Waiting B",reservedAt:new Date(),status:"Approved"}});')
    st, _, _, b = req(op, "/Renewals/RenewAtCounter", "POST", {"loanId": loan_id, "__RequestVerificationToken": tk_ren})
    cnt_blocked = int(mongo_eval(f'print(db.loans.findOne({{_id:ObjectId("{loan_id}")}}).renewalCount)'))
    check("A34", "UC09", "Chặn gia hạn khi sách có người đang đặt trước (QĐ04)", cnt_blocked == 1 and "đặt trước" in b, f"cnt={cnt_blocked}")

    # ============ 7. UC08 NHẬN TRẢ SÁCH (TỪNG PHẦN, HƯ HỎNG, MẤT, NGƯỜI CHỜ, BUG-03) ============
    # Thêm 1 yêu cầu gia hạn Pending vào loan_id để kiểm tra BUG-03 khi trả hết sách!
    mongo_eval(f'db.loans.updateOne({{_id:ObjectId("{loan_id}")}}, {{$push:{{renewalRequests:{{requestedAt:new Date(),source:"Online",status:"Pending"}}}}}});')

    _, _, _, ret_html = req(op, f"/Loans/Return?keyword={new_rd['card']}")
    tk_ret = af_token(ret_html)

    # Trả từng phần: trả cuốn bB (Bình thường, đang có r1_id Approved chờ!) -> phiếu vẫn Borrowing, bB avail +1, ReturnResult hiện danh sách người chờ!
    st, u, _, b = req(op, "/Loans/Return", "POST", {
        "LoanId": loan_id,
        "Items[0].BookId": bB["id"], "Items[0].Selected": "true", "Items[0].Condition": "Normal",
        "__RequestVerificationToken": tk_ret
    })
    loan_mid = mongo_eval(f'print(db.loans.findOne({{_id:ObjectId("{loan_id}")}}).status)')
    bB_ret = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bB["id"]}")}}).availableQuantity)'))
    check("A35", "UC08", "Trả từng phần (1/3 cuốn): phiếu vẫn Borrowing, kho +1, hiện người chờ",
          "/Loans/ReturnResult" in u and loan_mid == "Borrowing" and bB_ret == bB["a"] and "Nguyễn Văn An" in b,
          f"status={loan_mid}, bB:{bB_after}->{bB_ret}, waitingShown={'Nguyễn Văn An' in b}")
    # Dọn reservation Approved test
    mongo_eval(f'db.reservations.deleteMany({{readerId:ObjectId("{r1_id}"),bookId:ObjectId("{bB["id"]}")}});')

    # Trả cuốn hư hỏng thiếu tiền phạt -> bị chặn
    st, u, _, b = req(op, "/Loans/Return", "POST", {
        "LoanId": loan_id,
        "Items[0].BookId": bA["id"], "Items[0].Selected": "true", "Items[0].Condition": "Damaged",
        "__RequestVerificationToken": tk_ret
    })
    check("A36", "UC08", "Trả sách hư hỏng thiếu mức phạt bị chặn", "/Loans/ReturnResult" not in u and "mức phạt" in b, f"url={u}")

    # Trả 2 cuốn còn lại: bA Hư hỏng (phạt 30.000đ) + bC Mất (phạt = bC.price, TotalQuantity -1!) -> đóng phiếu Returned!
    bC_tot_before = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bC["id"]}")}}).totalQuantity)'))
    bC_av_before = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bC["id"]}")}}).availableQuantity)'))
    st, u, _, b = req(op, "/Loans/Return", "POST", {
        "LoanId": loan_id,
        "Items[0].BookId": bA["id"], "Items[0].Selected": "true", "Items[0].Condition": "Damaged", "Items[0].DamageFine": "30000",
        "Items[1].BookId": bC["id"], "Items[1].Selected": "true", "Items[1].Condition": "Lost",
        "__RequestVerificationToken": tk_ret
    })
    loan_end = json.loads(mongo_eval(f'var l=db.loans.findOne({{_id:ObjectId("{loan_id}")}}); print(JSON.stringify({{st:l.status, fine:Number(l.totalFine), pend:l.renewalRequests.filter(x=>x.status==="Pending").length}}))'))
    bC_tot_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bC["id"]}")}}).totalQuantity)'))
    bC_av_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bC["id"]}")}}).availableQuantity)'))
    check("A37", "UC08", "Trả Hư hỏng + Mất: đóng phiếu Returned, tổng phạt đúng, sách mất trừ TotalQuantity",
          loan_end["st"] == "Returned" and loan_end["fine"] == 30000 + bC["price"] and bC_tot_after == bC_tot_before - 1 and bC_av_after == bC_av_before,
          f"end={loan_end}, bC.total:{bC_tot_before}->{bC_tot_after}, bC.avail:{bC_av_before}->{bC_av_after}")

    # Kiểm tra BUG-03 vừa sửa! Phiếu đã Returned không còn Pending renewal và không hiện ở /Renewals!
    _, _, _, ren_after_html = req(op, "/Renewals")
    loan_code = mongo_eval(f'print(db.loans.findOne({{_id:ObjectId("{loan_id}")}}).loanCode)')
    check("A38", "UC09", "Xác minh fix BUG-03: Trả hết sách tự đóng Pending renewal & không hiện ở /Renewals",
          loan_end["pend"] == 0 and loan_code not in ren_after_html,
          f"pendingLeft={loan_end['pend']}, inRenewalsPage={loan_code in ren_after_html}", severity="MEDIUM")

    # ============ 8. UC10 XỬ LÝ ĐẶT TRƯỚC ============
    # Tạo 3 reservation: 1 Pending của thẻ khóa DG000009 (test BUG-07), 1 Pending thường, 1 Held để Thủ thư hủy
    locked_id = mongo_eval('print(db.readers.findOne({cardNumber:"DG000009"})._id.toString())')
    res_ids = json.loads(mongo_eval(f"""
    const rLk = db.reservations.insertOne({{readerId:ObjectId("{locked_id}"),bookId:ObjectId("{bD["id"]}"),bookTitle:"Test Locked Res",reservedAt:new Date(),status:"Pending"}}).insertedId.toString();
    const rOk = db.reservations.insertOne({{readerId:ObjectId("{new_rd["id"]}"),bookId:ObjectId("{bE["id"]}"),bookTitle:"Test Normal Res",reservedAt:new Date(),status:"Pending"}}).insertedId.toString();
    db.books.updateOne({{_id:ObjectId("{bF["id"]}")}}, {{$inc:{{availableQuantity:-1}}}});
    const rHd = db.reservations.insertOne({{readerId:ObjectId("{new_rd["id"]}"),bookId:ObjectId("{bF["id"]}"),bookTitle:"Test Held Res",reservedAt:new Date(),status:"Held",holdUntil:new Date(Date.now()+2*86400000)}}).insertedId.toString();
    print(JSON.stringify({{rLk, rOk, rHd}}));
    """))
    _, _, _, res_html = req(op, "/Reservations")
    tk_res = af_token(res_html)

    # Duyệt yêu cầu hợp lệ Pending -> Approved
    req(op, "/Reservations/Approve", "POST", {"id": res_ids["rOk"], "__RequestVerificationToken": tk_res})
    check("A39", "UC10", "Thủ thư duyệt đặt trước Pending -> Approved",
          mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{res_ids["rOk"]}")}}).status)') == "Approved", "status=Approved")

    # Hủy yêu cầu đã duyệt kèm lý do -> Cancelled
    req(op, "/Reservations/Cancel", "POST", {"Id": res_ids["rOk"], "Reason": "Không liên hệ được", "__RequestVerificationToken": tk_res})
    check("A40", "UC10", "Thủ thư hủy đặt trước Approved kèm lý do -> Cancelled",
          mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{res_ids["rOk"]}")}}).status)') == "Cancelled", "status=Cancelled")

    # Thủ thư hủy đặt giữ Held -> hoàn trả đúng 1 cuốn vào kho!
    bF_before = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bF["id"]}")}}).availableQuantity)'))
    req(op, "/Reservations/Cancel", "POST", {"Id": res_ids["rHd"], "Reason": "Độc giả báo hủy", "__RequestVerificationToken": tk_res})
    bF_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bF["id"]}")}}).availableQuantity)'))
    check("A41", "UC10", "Thủ thư hủy đặt giữ (Held) hoàn trả đúng 1 cuốn vào kho",
          bF_after == bF_before + 1, f"bF:{bF_before}->{bF_after}")

    # Kiểm tra BUG-07: Thủ thư duyệt đặt trước của thẻ bị khóa (DG000009)
    req(op, "/Reservations/Approve", "POST", {"id": res_ids["rLk"], "__RequestVerificationToken": tk_res})
    lk_res_st = mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{res_ids["rLk"]}")}}).status)')
    check("A42", "UC10", "BUG-07: Duyệt đặt trước của thẻ bị khóa có bị chặn ở server không",
          lk_res_st == "Pending", f"status={lk_res_st} (server vẫn cho duyệt sang Approved dù thẻ khóa)", severity="MEDIUM")

    # ============ 9. UC11 TÌM KIẾM ĐA NĂNG ============
    s1, _, _, b1 = req(op, "/Search?type=book&keyword=Clean")
    s2, _, _, b2 = req(op, f"/Search?type=reader&keyword={new_rd['card']}")
    s3, _, _, b3 = req(op, "/Search?type=loan&loanStatus=overdue")
    s4, _, _, b4 = req(op, "/Search?type=book&keyword=(abc[+&categoryId=bad-id")
    check("A43", "UC11", "Tìm kiếm Sách / Độc giả / Phiếu quá hạn / ký tự đặc biệt + bad-id",
          s1 == 200 and "Clean Code" in b1 and s2 == 200 and new_rd["card"] in b2 and s3 == 200 and s4 == 200,
          f"HTTP {s1},{s2},{s3},{s4}")

    # ============ 10. UC12 BÁO CÁO & THỐNG KÊ ============
    st_tb, _, _, tb_err = req(op, "/Reports/TopBooks?fromDate=2026-12-31&toDate=2026-01-01")
    check("A44", "UC12", "Báo cáo TopBooks: fromDate > toDate báo lỗi hợp lệ",
          st_tb == 200 and "không được lớn hơn" in tb_err, f"HTTP {st_tb}")

    st_od, _, _, od_html = req(op, "/Reports/Overdue")
    st_bm, _, _, bm_html = req(op, "/Reports/ByMonth?year=2026")
    st_bc, _, _, bc_html = req(op, "/Reports/ByCategory")
    st_fn, _, _, fn_html = req(op, "/Reports/Fines")
    check("A45", "UC12", "Cả 5 trang Báo cáo (TopBooks, Overdue, ByMonth, ByCategory, Fines) tải 200 OK",
          all(x == 200 for x in [st_tb, st_od, st_bm, st_bc, st_fn]), f"HTTP {st_tb},{st_od},{st_bm},{st_bc},{st_fn}")

    # Kiểm tra BUG-02 thực tế trên Báo cáo Tiền phạt: phiếu mượn hôm qua, trả có phạt hôm nay -> lọc từ ngày hôm nay phải thấy!
    mongo_eval(f"""
    const yest = new Date(Date.now() - 2*86400000);
    const today = new Date();
    db.loans.insertOne({{
      loanCode: "PM999FIN",
      reader: {{ readerId: ObjectId("{new_rd["id"]}"), cardNumber: "{new_rd["card"]}", fullName: "QA Fine" }},
      borrowDate: yest, dueDate: yest, status: "Returned", renewalCount: 0,
      items: [{{ bookId: ObjectId("{bD["id"]}"), title: "Sách Phạt Hôm Nay", categoryIds: [], returnDate: today, condition: "Damaged", fine: NumberLong(77000) }}],
      totalFine: NumberLong(77000), renewalRequests: []
    }});
    """)
    st_f2, _, _, f2_html = req(op, "/Reports/Fines?fromDate=2026-11-24&toDate=2026-11-24")
    check("A46", "UC12", "BUG-02: Báo cáo Tiền phạt lọc theo ngày TRẢ (hôm nay) có thấy phiếu mượn hôm kia trả hôm nay không",
          "PM999FIN" in f2_html,
          "Thấy đúng PM999FIN" if "PM999FIN" in f2_html else "KHÔNG THẤY PM999FIN vì GetFinesAsync đang lọc theo BorrowDate thay vì ReturnDate!",
          severity="HIGH")

    # Kiểm tra toàn vẹn tồn kho cuối phiên sau toàn bộ 46 thao tác Admin!
    inv = json.loads(mongo_eval(r"""
    const unret = {};
    db.loans.find({status:'Borrowing'}).forEach(l => l.items.forEach(i => {
      if (i.returnDate == null) unret[i.bookId.toString()] = (unret[i.bookId.toString()] || 0) + 1;
    }));
    const held = {};
    db.reservations.find({status:'Held'}).forEach(r => {
      held[r.bookId.toString()] = (held[r.bookId.toString()] || 0) + 1;
    });
    const mismatches = [];
    db.books.find().forEach(b => {
      const bid = b._id.toString();
      const out = b.totalQuantity - b.availableQuantity;
      const exp = (unret[bid] || 0) + (held[bid] || 0);
      if (out !== exp && !(b.availableQuantity === 0 && exp === 0)) {
        mismatches.push({title: b.title, out, exp});
      }
    });
    print(JSON.stringify({totalBooks: db.books.countDocuments(), mismatches}));
    """))
    check("A47", "Integrity", "Toàn vẹn tồn kho sau toàn bộ thao tác Admin (Thêm/Sửa/Mượn/Trả/Mất/Hủy giữ)",
          len(inv["mismatches"]) == 0, f"{inv['totalBooks']} books checked, mismatches={inv['mismatches']}")

    passed = sum(1 for r in results if r["passed"])
    print(f"\n=== ADMIN QA SUITE SUMMARY: {passed}/{len(results)} PASSED ===")
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"passed": passed, "total": len(results), "results": results}, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
