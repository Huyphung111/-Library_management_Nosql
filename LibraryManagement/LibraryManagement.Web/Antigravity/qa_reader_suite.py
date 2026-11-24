"""
Bộ kiểm thử tự động chuyên sâu Role ĐỘC GIẢ (47 kịch bản: UC01-03, UC13-16, Yêu thích, Đặt giữ/Đặt trước, RBAC/IDOR/CSRF).
Chạy trên server cô lập http://localhost:5199 và database riêng library_management_qa_reader
(KHÔNG đụng database thật library_management).
"""
import html as html_lib
import http.cookiejar
import json
import os
import re
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

BASE_URL = "http://localhost:5199"
QA_DB = "library_management_qa_reader"
MONGO_URI = f"mongodb://localhost:27017/{QA_DB}?replicaSet=rs0"
OUT_JSON = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_reader_results.json"


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
    body_bytes = urllib.parse.urlencode(data).encode("utf-8") if isinstance(data, dict) else data
    hdrs = dict(headers or {})
    if isinstance(data, dict) and "Content-Type" not in hdrs:
        hdrs["Content-Type"] = "application/x-www-form-urlencoded"
    r = urllib.request.Request(url, data=body_bytes, method=method, headers=hdrs)
    try:
        with opener.open(r, timeout=12) as resp:
            raw = resp.read().decode("utf-8", errors="ignore")
            return resp.status, resp.geturl(), dict(resp.headers), html_lib.unescape(raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", errors="ignore") if e.fp else ""
        return e.code, e.geturl(), dict(e.headers), html_lib.unescape(raw)
    except Exception as e:
        return -1, url, {}, str(e)


def af_token(html: str) -> str:
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else ""


def login(username: str, password: str):
    op, cj = make_opener(follow=True)
    _, _, _, html = req(op, "/Account/Login")
    tk = af_token(html)
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

    # Đợi server sẵn sàng
    for _ in range(25):
        op, _ = make_opener(False)
        st, _, _, _ = req(op, "/Account/Login")
        if st == 200:
            break
        time.sleep(0.5)
    else:
        raise RuntimeError("Test server on :5199 did not start!")

    # Lấy dữ liệu nền từ DB test
    seed = json.loads(mongo_eval(r"""
    const books = db.books.find().toArray();
    const readers = db.readers.find().toArray();
    const loans = db.loans.find().toArray();
    const resv = db.reservations.find().toArray();
    const cats = db.categories.find().toArray();
    const inStock = books.filter(b => b.availableQuantity >= 2);
    const outStock = books.filter(b => b.availableQuantity === 0);
    const rMap = {}; readers.forEach(r => rMap[r.cardNumber] = r._id.toString());
    print(JSON.stringify({
      readerIds: rMap,
      inStockIds: inStock.slice(0, 6).map(b => ({id: b._id.toString(), title: b.title, avail: b.availableQuantity})),
      outStockIds: outStock.map(b => ({id: b._id.toString(), title: b.title})),
      catId: cats[0]._id.toString(),
      coverId: books.find(b => b.coverImageId).coverImageId.toString(),
      loans: loans.map(l => ({
        id: l._id.toString(), code: l.loanCode, card: l.reader.cardNumber,
        status: l.status, due: l.dueDate.toISOString(), ren: l.renewalCount,
        items: l.items.map(i => ({bookId: i.bookId.toString(), ret: i.returnDate != null}))
      })),
      reservations: resv.map(r => ({id: r._id.toString(), readerId: r.readerId.toString(), bookId: r.bookId.toString(), status: r.status}))
    }));
    """))

    # ============ NHÓM 1: AUTH & PASSWORD (UC01-03) ============
    op0, _ = make_opener(True)
    _, _, _, lhtml = req(op0, "/Account/Login")
    st, url, _, body = req(op0, "/Account/Login", "POST", {"Username": "", "Password": "", "__RequestVerificationToken": af_token(lhtml)})
    check("R01", "Auth", "Bỏ trống username/password bị chặn", st == 200 and "/Account/Login" in url, f"HTTP {st}, url={url}")

    _, _, st, url, body = login("DG000001", "wrongpass")
    check("R02", "Auth", "Sai mật khẩu báo thông báo chung", st == 200 and "không đúng" in body, f"HTTP {st}")

    op1, cj1, st, url, body = login("DG000001", "123456")
    check("R03", "Auth", "Đăng nhập DG000001 thành công -> /Catalog", st == 200 and "/Catalog" in url and "card-notice" not in body, f"url={url}")

    _, _, st, url, _ = login("  DG000001  ", "123456")
    check("R04", "Auth", "Đăng nhập username có khoảng trắng đầu/cuối", st == 200 and "/Catalog" in url, f"url={url}")

    _, _, st, url, body = login("dg000001", "123456")
    check("R05", "Auth", "Đăng nhập mã thẻ viết chữ thường (dg000001)", st == 200 and "/Catalog" in url,
          f"url={url} (nếu FAIL: username đang phân biệt hoa/thường)", severity="MINOR")

    op_lock, cj_lock, st, url, body = login("DG000009", "123456")
    check("R06", "Auth", "Thẻ bị khóa (DG000009) vẫn đăng nhập + có CardNotice", st == 200 and "/Catalog" in url and "card-notice" in body and "bị khóa" in body, f"url={url}")

    op_exp, cj_exp, st, url, body = login("DG000010", "123456")
    check("R07", "Auth", "Thẻ hết hạn (DG000010) vẫn đăng nhập + có CardNotice", st == 200 and "/Catalog" in url and "card-notice" in body and "hết hạn" in body, f"url={url}")

    # UC03 Đổi mật khẩu trên DG000008 (không đụng DG000001)
    op8, _, _, _, _ = login("DG000008", "123456")
    _, _, _, cphtml = req(op8, "/Account/ChangePassword")
    tk_cp = af_token(cphtml)
    st, _, _, b = req(op8, "/Account/ChangePassword", "POST", {
        "CurrentPassword": "wrong", "NewPassword": "newpass1", "ConfirmPassword": "newpass1", "__RequestVerificationToken": tk_cp
    })
    check("R08", "Auth", "Đổi MK: sai mật khẩu hiện tại bị chặn", "không đúng" in b, f"HTTP {st}")

    st, _, _, b = req(op8, "/Account/ChangePassword", "POST", {
        "CurrentPassword": "123456", "NewPassword": "123", "ConfirmPassword": "123", "__RequestVerificationToken": tk_cp
    })
    check("R09", "Auth", "Đổi MK: mật khẩu mới < 6 ký tự bị chặn", "6" in b or "ít nhất" in b, f"HTTP {st}")

    st, _, _, b = req(op8, "/Account/ChangePassword", "POST", {
        "CurrentPassword": "123456", "NewPassword": "123456", "ConfirmPassword": "123456", "__RequestVerificationToken": tk_cp
    })
    check("R10", "Auth", "Đổi MK: trùng mật khẩu cũ bị chặn", "khác" in b, f"HTTP {st}")

    st, _, _, b = req(op8, "/Account/ChangePassword", "POST", {
        "CurrentPassword": "123456", "NewPassword": "abcdef", "ConfirmPassword": "abcdeg", "__RequestVerificationToken": tk_cp
    })
    check("R11", "Auth", "Đổi MK: xác nhận không khớp bị chặn", "không khớp" in b or "khớp" in b, f"HTTP {st}")

    st, _, _, b = req(op8, "/Account/ChangePassword", "POST", {
        "CurrentPassword": "123456", "NewPassword": "newpass99", "ConfirmPassword": "newpass99", "__RequestVerificationToken": tk_cp
    })
    _, _, st_old, u_old, _ = login("DG000008", "123456")
    _, _, st_new, u_new, _ = login("DG000008", "newpass99")
    check("R12", "Auth", "Đổi MK thành công: MK cũ bị chặn, MK mới vào được",
          "/Account/Login" in u_old and "/Catalog" in u_new, f"old={u_old}, new={u_new}")

    # UC02 Logout khi hết phiên (không có cookie/token) -> không trả 400
    guest_no, _ = make_opener(False)
    st, _, hdrs, _ = req(guest_no, "/Account/Logout", "POST", {})
    check("R13", "Auth", "Logout khi hết phiên (không token) không bị 400", st == 302, f"HTTP {st} -> {hdrs.get('Location','')}")

    # ============ NHÓM 2: RBAC, IDOR, CSRF ============
    op1_nr = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj1), NoRedirect())
    blocked_all = True
    bad_routes = []
    for r in ["/Dashboard", "/Books", "/Categories", "/Readers", "/Loans/Create", "/Loans/Return", "/Renewals", "/Reservations", "/Search", "/Reports/TopBooks"]:
        st, _, h, _ = req(op1_nr, r)
        if not (st in (302, 403) and ("AccessDenied" in h.get("Location", "") or st == 403)):
            blocked_all = False
            bad_routes.append(f"{r}:{st}")
    check("R14", "Security", "RBAC: Độc giả bị chặn toàn bộ 10 trang Thủ thư", blocked_all, "All 10 blocked" if blocked_all else str(bad_routes))

    st, _, h, _ = req(op1_nr, "/Renewals/Approve", "POST", {"loanId": seed["loans"][0]["id"]})
    check("R15", "Security", "RBAC: Độc giả bị chặn POST endpoint Thủ thư", st in (302, 403, 400), f"HTTP {st} -> {h.get('Location','')}")

    # IDOR Gia hạn: tìm phiếu Borrowing của độc giả KHÁC DG000001
    other_loan = next((l for l in seed["loans"] if l["card"] != "DG000001" and l["status"] == "Borrowing"), seed["loans"][0])
    _, _, _, cat_html = req(op1, "/Catalog")
    tk1 = af_token(cat_html)
    st, u, _, b = req(op1, f"/MyLoans/RequestRenewal/{other_loan['id']}")
    check("R16", "Security", "Anti-IDOR: DG000001 không xem/gia hạn được phiếu của độc giả khác",
          "/MyLoans" in u and "Không tìm thấy phiếu mượn của bạn" in b, f"url={u}")

    # IDOR Hủy đặt trước: tạo 1 reservation cho DG000004 (thẻ sạch, không quá hạn) rồi DG000001 cố hủy
    op4, cj4, _, _, c4_html = login("DG000004", "123456")
    tk4 = af_token(c4_html)
    out_book = seed["outStockIds"][0]
    req(op4, "/MyReservations/Create", "POST", {"bookId": out_book["id"], "confirmed": "true", "__RequestVerificationToken": tk4})
    r4_id = mongo_eval(f'var x=db.reservations.findOne({{readerId:ObjectId("{seed["readerIds"]["DG000004"]}"),bookId:ObjectId("{out_book["id"]}"),status:"Pending"}}); print(x?x._id.toString():"");')
    st, u, _, b = req(op1, "/MyReservations/Cancel", "POST", {"id": r4_id, "__RequestVerificationToken": tk1})
    still_pending = mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{r4_id}")}}).status)')
    check("R17", "Security", "Anti-IDOR: DG000001 không hủy được đặt trước của DG000004",
          still_pending == "Pending" and "Không tìm thấy" in b, f"status={still_pending}")

    st, _, _, _ = req(op1_nr, "/MyFavorites/Add", "POST", {"bookId": seed["inStockIds"][0]["id"]}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R18", "Security", "Anti-CSRF: Chặn POST /MyFavorites/Add thiếu token", st == 400, f"HTTP {st}")

    # ============ NHÓM 3: TRA CỨU SÁCH (UC13) ============
    st1, _, _, b1 = req(op1, "/Catalog?search=(test[+")
    st2, _, _, b2 = req(op1, f"/Catalog?categoryId={seed['catId']}")
    st3, _, _, b3 = req(op1, "/Catalog?categoryId=bad-id")
    check("R19", "Catalog", "Tìm sách ký tự đặc biệt & lọc thể loại hợp lệ", st1 == 200 and st2 == 200, f"HTTP {st1}, {st2}")
    check("R20", "Catalog", "Lọc categoryId sai định dạng không gây 500", st3 == 200, f"HTTP {st3}")

    st_det, _, _, b_det = req(op1, f"/Catalog/Details/{seed['inStockIds'][0]['id']}")
    st_bad, u_bad, _, _ = req(op1, "/Catalog/Details/bad-id")
    st_cov, _, h_cov, _ = req(op1, f"/Covers/{seed['coverId']}")
    check("R21", "Catalog", "Chi tiết sách, bad-id và ảnh bìa GridFS",
          st_det == 200 and st_bad == 200 and "/Catalog" in u_bad and st_cov == 200 and h_cov.get("Content-Type", "").startswith("image/"),
          f"det={st_det}, bad={st_bad}->{u_bad}, cov={st_cov}")

    # ============ NHÓM 4: SÁCH YÊU THÍCH ============
    b_fav = seed["inStockIds"][0]
    st, _, _, body = req(op1, "/MyFavorites/Add", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk1}, headers={"X-Requested-With": "XMLHttpRequest"})
    j = json.loads(body) if body.startswith("{") else {}
    check("R22", "Favorites", "Thêm sách yêu thích (AJAX JSON)", st == 200 and j.get("success") is True and j.get("count") == 1, f"HTTP {st}, {j}")

    st, _, _, body = req(op1, "/MyFavorites/Add", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk1}, headers={"X-Requested-With": "XMLHttpRequest"})
    j2 = json.loads(body) if body.startswith("{") else {}
    check("R23", "Favorites", "Thêm trùng cùng 1 sách không tạo bản sao ($addToSet)", st == 200 and j2.get("count") == 1, f"count={j2.get('count')}")

    st, _, _, body = req(op1, "/MyFavorites/Add", "POST", {"bookId": "bad-id", "__RequestVerificationToken": tk1}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R24", "Favorites", "Thêm yêu thích bookId sai định dạng bị chặn 400", st == 400, f"HTTP {st}")

    st, _, _, fav_html = req(op1, "/MyFavorites")
    check("R25", "Favorites", "Trang /MyFavorites hiển thị đúng sách vừa thêm", st == 200 and b_fav["title"] in fav_html, f"HTTP {st}")

    st, _, _, body = req(op1, "/MyFavorites/Remove", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk1}, headers={"X-Requested-With": "XMLHttpRequest"})
    j3 = json.loads(body) if body.startswith("{") else {}
    st_r2, _, _, _ = req(op1, "/MyFavorites/Remove", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk1}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R26", "Favorites", "Xóa yêu thích (idempotent, count về 0)", st == 200 and j3.get("count") == 0 and st_r2 == 200, f"count={j3.get('count')}, retry={st_r2}")

    # Thẻ bị khóa thử thêm yêu thích
    _, _, _, lock_html = req(op_lock, "/Catalog")
    tk_lock = af_token(lock_html)
    st_lf, _, _, body_lf = req(op_lock, "/MyFavorites/Add", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk_lock}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R27", "Favorites", "Thẻ bị khóa (DG000009) gọi /MyFavorites/Add",
          True, f"HTTP {st_lf} -> {'CHO PHÉP (xem xét có đúng chủ ý CardNotice chỉ xem không)' if st_lf == 200 else 'BỊ CHẶN'}")

    # ============ NHÓM 5: ĐẶT GIỮ & ĐẶT TRƯỚC (UC16) ============
    # Dùng DG000006 (sạch, đang hoạt động, chưa mượn gì, không quá hạn) để test chuẩn xác
    op6, _, _, _, c6 = login("DG000006", "123456")
    tk6 = af_token(c6)
    # Bảo đảm chọn 3 sách còn hàng & KHÔNG có hàng chờ Pending/Approved
    clean_books = json.loads(mongo_eval(r"""
    const busy = new Set(db.reservations.find({status:{$in:['Pending','Approved']}}).toArray().map(r => r.bookId.toString()));
    const list = db.books.find({availableQuantity: {$gte: 2}}).toArray()
      .filter(b => !busy.has(b._id.toString()))
      .slice(0, 4)
      .map(b => ({id: b._id.toString(), title: b.title, avail: b.availableQuantity}));
    print(JSON.stringify(list));
    """))
    bk1, bk2, bk3 = clean_books[0], clean_books[1], clean_books[2]

    st_box, _, _, box_html = req(op6, f"/MyReservations/Create?bookId={bk1['id']}", headers={"X-Requested-With": "XMLHttpRequest"})
    st_h1, _, _, b_h1 = req(op6, "/MyReservations/Create", "POST", {"bookId": bk1["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    avail_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bk1["id"]}")}}).availableQuantity)'))
    check("R28", "Reservations", "Đặt giữ online (sách còn): tạo Held + trừ kho đúng 1",
          st_box == 200 and st_h1 == 200 and avail_after == bk1["avail"] - 1,
          f"avail: {bk1['avail']} -> {avail_after}")

    # Đặt trùng cuốn đang giữ
    st_dup, _, _, b_dup = req(op6, "/MyReservations/Create", "POST", {"bookId": bk1["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    avail_dup = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bk1["id"]}")}}).availableQuantity)'))
    check("R29", "Reservations", "Chặn đặt trùng cuốn đang giữ (QĐ08)", st_dup == 400 and avail_dup == avail_after, f"HTTP {st_dup}, avail={avail_dup}")

    # Cuốn giữ thứ 2 OK, cuốn thứ 3 bị chặn MaxHolds=2
    st_h2, _, _, _ = req(op6, "/MyReservations/Create", "POST", {"bookId": bk2["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    st_h3, _, _, b_h3 = req(op6, "/MyReservations/Create", "POST", {"bookId": bk3["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R30", "Reservations", "Giới hạn tối đa 2 cuốn đặt giữ (MaxHolds=2)", st_h2 == 200 and st_h3 == 400 and "tối đa 2" in b_h3, f"h2={st_h2}, h3={st_h3}")

    # Tự hủy bản giữ bk1 -> kho cộng lại 1
    h1_id = mongo_eval(f'print(db.reservations.findOne({{readerId:ObjectId("{seed["readerIds"]["DG000006"]}"),bookId:ObjectId("{bk1["id"]}"),status:"Held"}})._id.toString())')
    req(op6, "/MyReservations/Cancel", "POST", {"id": h1_id, "__RequestVerificationToken": tk6})
    avail_restored = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bk1["id"]}")}}).availableQuantity)'))
    check("R31", "Reservations", "Độc giả tự hủy đặt giữ (Held) -> hoàn trả đúng 1 cuốn vào kho",
          avail_restored == bk1["avail"], f"avail: {avail_after} -> {avail_restored}")

    # Đặt trước sách hết hàng -> Pending, kho vẫn 0
    out_b = seed["outStockIds"][-1]
    st_pre, _, _, b_pre = req(op6, "/MyReservations/Create", "POST", {"bookId": out_b["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    pre_status = mongo_eval(f'var arr=db.reservations.find({{readerId:ObjectId("{seed["readerIds"]["DG000006"]}"),bookId:ObjectId("{out_b["id"]}")}}).sort({{reservedAt:-1}}).limit(1).toArray(); print(arr.length?arr[0].status:"");')
    check("R32", "Reservations", "Đặt trước sách hết hàng -> tạo Pending", st_pre == 200 and pre_status == "Pending", f"HTTP {st_pre}, status={pre_status}")

    # Chống chen hàng (HasQueue): sách còn hàng NHƯNG đã có Pending của DG000005 -> DG000006 phải vào Pending chứ không được Held
    mongo_eval(f'db.reservations.insertOne({{readerId:ObjectId("{seed["readerIds"]["DG000005"]}"),bookId:ObjectId("{bk3["id"]}"),bookTitle:"test",reservedAt:new Date(),status:"Pending"}});')
    avail_bk3_before = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bk3["id"]}")}}).availableQuantity)'))
    st_q, _, _, b_q = req(op6, "/MyReservations/Create", "POST", {"bookId": bk3["id"], "confirmed": "true", "__RequestVerificationToken": tk6}, headers={"X-Requested-With": "XMLHttpRequest"})
    q_status = mongo_eval(f'print(db.reservations.findOne({{readerId:ObjectId("{seed["readerIds"]["DG000006"]}"),bookId:ObjectId("{bk3["id"]}")}}).status)')
    avail_bk3_after = int(mongo_eval(f'print(db.books.findOne({{_id:ObjectId("{bk3["id"]}")}}).availableQuantity)'))
    check("R33", "Reservations", "Chống chen hàng: sách còn nhưng có hàng chờ -> vào Pending (không trừ kho)",
          st_q == 200 and q_status == "Pending" and avail_bk3_after == avail_bk3_before, f"status={q_status}, avail={avail_bk3_after}")

    # Hủy đặt trước Pending
    q_id = mongo_eval(f'print(db.reservations.findOne({{readerId:ObjectId("{seed["readerIds"]["DG000006"]}"),bookId:ObjectId("{bk3["id"]}")}})._id.toString())')
    req(op6, "/MyReservations/Cancel", "POST", {"id": q_id, "__RequestVerificationToken": tk6})
    q_after = mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{q_id}")}}).status)')
    check("R34", "Reservations", "Độc giả tự hủy đặt trước Pending -> Cancelled", q_after == "Cancelled", f"status={q_after}")

    # Chặn đặt cuốn đang mượn (IsBorrowingAsync) — chọn phiếu đang mượn CHƯA quá hạn (tránh bị chặn Overdue ở bước trước)
    borr_info = json.loads(mongo_eval(r"""
    const l = db.loans.findOne({status:'Borrowing', dueDate:{$gte: new Date()}});
    const it = l.items.find(i => i.returnDate == null);
    print(JSON.stringify({card: l.reader.cardNumber, bookId: it.bookId.toString()}));
    """))
    op_b, _, _, _, cb = login(borr_info["card"], "123456")
    tk_b = af_token(cb)
    st_sb, _, _, b_sb = req(op_b, "/MyReservations/Create", "POST", {"bookId": borr_info["bookId"], "confirmed": "true", "__RequestVerificationToken": tk_b}, headers={"X-Requested-With": "XMLHttpRequest"})
    try:
        sb_msg = json.loads(b_sb).get("message", b_sb)
    except Exception:
        sb_msg = b_sb
    check("R35", "Reservations", "Chặn đặt cuốn đang mượn (IsBorrowingAsync)", st_sb == 400 and "đang mượn" in sb_msg.lower(), f"HTTP {st_sb}, msg={sb_msg[:120]}")

    # Độc giả có phiếu quá hạn: thử đặt giữ sách còn vs đặt trước sách hết!
    overdue_card = mongo_eval(r"""
    const now = new Date();
    const l = db.loans.findOne({status:'Borrowing', dueDate:{$lt: now}});
    print(l ? l.reader.cardNumber : '');
    """)
    if overdue_card:
        op_od, _, _, _, cod = login(overdue_card, "123456")
        tk_od = af_token(cod)
        st_od_h, _, _, b_od_h = req(op_od, "/MyReservations/Create", "POST", {"bookId": bk1["id"], "confirmed": "true", "__RequestVerificationToken": tk_od}, headers={"X-Requested-With": "XMLHttpRequest"})
        check("R36", "Reservations", f"Độc giả quá hạn ({overdue_card}) bị chặn đặt giữ sách còn",
              st_od_h == 400 and "quá hạn" in b_od_h, f"HTTP {st_od_h}")
        # Edge case: độc giả quá hạn thử đặt trước sách HẾT hàng (đã sửa R36b -> phải bị chặn 400)
        st_od_p, _, _, b_od_p = req(op_od, "/MyReservations/Create", "POST", {"bookId": out_b["id"], "confirmed": "true", "__RequestVerificationToken": tk_od}, headers={"X-Requested-With": "XMLHttpRequest"})
        check("R36b", "Reservations", f"Độc giả quá hạn ({overdue_card}) bị chặn đặt trước sách hết hàng (fix R36b)",
              st_od_p == 400 and "quá hạn" in b_od_p, f"HTTP {st_od_p} -> {'BỊ CHẶN OK' if st_od_p == 400 else 'LỌT CHO VÀO HÀNG CHỜ'}")

    # Thẻ bị khóa & thẻ hết hạn bị chặn đặt giữ & đặt trước
    st_lk, _, _, b_lk = req(op_lock, "/MyReservations/Create", "POST", {"bookId": bk1["id"], "confirmed": "true", "__RequestVerificationToken": tk_lock}, headers={"X-Requested-With": "XMLHttpRequest"})
    _, _, _, exp_html = req(op_exp, "/Catalog")
    tk_exp = af_token(exp_html)
    st_ex, _, _, b_ex = req(op_exp, "/MyReservations/Create", "POST", {"bookId": bk1["id"], "confirmed": "true", "__RequestVerificationToken": tk_exp}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("R37", "Reservations", "Thẻ bị khóa (DG000009) & hết hạn (DG000010) bị chặn đặt sách",
          st_lk == 400 and st_ex == 400, f"locked={st_lk}, expired={st_ex}")

    # Kiểm tra BUG-05 thực tế: đặt bản giữ cuối về quá hạn, độc giả khác mở hộp xác nhận
    # Chỉnh bk1 còn 0 và gán 1 bản Held của DG000007 có holdUntil = hôm qua
    mongo_eval(f"""
    db.books.updateOne({{_id:ObjectId("{bk1["id"]}")}}, {{$set:{{availableQuantity:0}}}});
    db.reservations.insertOne({{
      readerId: ObjectId("{seed["readerIds"]["DG000007"]}"),
      bookId: ObjectId("{bk1["id"]}"),
      bookTitle: "{bk1["title"]}",
      reservedAt: new Date(Date.now() - 4*86400000),
      status: "Held",
      holdUntil: new Date(Date.now() - 2*86400000)
    }});
    """)
    st_b5, _, _, html_b5 = req(op6, f"/MyReservations/Create?bookId={bk1['id']}", headers={"X-Requested-With": "XMLHttpRequest"})
    # Nếu ExpireHoldsAsync được gọi thì sách sẽ tự hoàn về 1 và hiện "Xác nhận đặt giữ sách"
    check("R38", "Reservations", "BUG-05: Hộp xác nhận có tự ExpireHoldsAsync bản giữ quá hạn không",
          "Xác nhận đặt giữ sách" in html_b5,
          "Tự trả kho OK" if "Xác nhận đặt giữ sách" in html_b5 else "KHÔNG tự trả kho (hiện Xác nhận đặt trước sách vì chưa gọi ExpireHoldsAsync)",
          severity="MEDIUM")
    # Khôi phục bk1 về đúng thực tế
    mongo_eval(f"""
    db.reservations.deleteMany({{readerId:ObjectId("{seed["readerIds"]["DG000007"]}"), bookId:ObjectId("{bk1["id"]}")}});
    db.books.updateOne({{_id:ObjectId("{bk1["id"]}")}}, {{$set:{{availableQuantity:{bk1["avail"]}}}}});
    """)

    # ============ NHÓM 6: UC14 & UC15 (MỚI CẬP NHẬT!) ============
    # Chuẩn bị 1 phiếu mượn sạch cho DG000006 (chưa quá hạn, 0 gia hạn) để test toàn bộ UC14 + UC15
    loan6_id = mongo_eval(f"""
    const now = new Date();
    const due = new Date(now.getTime() + 10*86400000);
    const ins = db.loans.insertOne({{
      loanCode: "PM999001",
      reader: {{ readerId: ObjectId("{seed["readerIds"]["DG000006"]}"), cardNumber: "DG000006", fullName: "Đỗ Thị Mai" }},
      borrowDate: now,
      dueDate: due,
      status: "Borrowing",
      renewalCount: 0,
      items: [{{ bookId: ObjectId("{bk1["id"]}"), title: "{bk1["title"]}", categoryIds: [], returnDate: null, condition: null, fine: NumberLong(0) }}],
      totalFine: NumberLong(0),
      renewalRequests: [{{
        requestedAt: new Date(now.getTime() - 3600000),
        source: "Online",
        status: "Rejected",
        processedAt: now,
        rejectReason: "Kiểm thử lý do từ chối gia hạn"
      }}]
    }});
    db.books.updateOne({{_id:ObjectId("{bk1["id"]}")}}, {{$inc:{{availableQuantity:-1}}}});
    print(ins.insertedId.toString());
    """)

    st_ml, _, _, html_ml = req(op6, "/MyLoans")
    check("R39", "MyLoans", "UC14: Tab Đang mượn hiển thị đúng phiếu PM999001",
          st_ml == 200 and "PM999001" in html_ml and bk1["title"] in html_ml, f"HTTP {st_ml}")

    st_mh, _, _, html_mh = req(op6, "/MyLoans/History")
    check("R40", "MyLoans", "UC14 (mới sửa): Mục Lịch sử mượn hiện cả sách Đang mượn + Tổng phạt",
          st_mh == 200 and "PM999001" in html_mh and "Chưa trả" in html_mh and "Tổng tiền phạt" in html_mh, f"HTTP {st_mh}")

    st_rh, _, _, html_rh = req(op6, "/MyLoans/RenewalHistory")
    check("R41", "MyLoans", "UC14 (mới thêm): Tab Lịch sử gia hạn hiển thị dòng Bị từ chối + modal lý do",
          st_rh == 200 and "PM999001" in html_rh and "Kiểm thử lý do từ chối gia hạn" in html_rh and "reasonModal" in html_rh, f"HTTP {st_rh}")

    # UC15: Xem trước & Gửi yêu cầu gia hạn hợp lệ
    st_rp, _, _, html_rp = req(op6, f"/MyLoans/RequestRenewal/{loan6_id}")
    tk6_rp = af_token(html_rp)
    st_rs, u_rs, _, b_rs = req(op6, f"/MyLoans/RequestRenewal/{loan6_id}", "POST", {"__RequestVerificationToken": tk6_rp})
    check("R42", "Renewal", "UC15: Gửi yêu cầu gia hạn hợp lệ thành công",
          st_rp == 200 and st_rs == 200 and "Đã gửi yêu cầu gia hạn" in b_rs, f"preview={st_rp}, post={st_rs}")

    # Gửi trùng lần 2 -> phải chặn
    st_rs2, _, _, b_rs2 = req(op6, f"/MyLoans/RequestRenewal/{loan6_id}", "POST", {"__RequestVerificationToken": tk6_rp})
    check("R43", "Renewal", "UC15: Chặn gửi trùng gia hạn khi đã có Pending",
          "chờ duyệt" in b_rs2, f"HTTP {st_rs2}")

    # Chặn gia hạn phiếu quá hạn
    od_loan = json.loads(mongo_eval(r"""
    const l = db.loans.findOne({status:'Borrowing', dueDate:{$lt: new Date()}});
    print(l ? JSON.stringify({id:l._id.toString(), card:l.reader.cardNumber}) : '{}');
    """))
    if od_loan.get("id"):
        op_od2, _, _, _, cod2 = login(od_loan["card"], "123456")
        tk_od2 = af_token(cod2)
        st_rod, _, _, b_rod = req(op_od2, f"/MyLoans/RequestRenewal/{od_loan['id']}", "POST", {"__RequestVerificationToken": tk_od2})
        check("R44", "Renewal", "UC15: Chặn gia hạn phiếu đã quá hạn", "quá hạn" in b_rod, f"HTTP {st_rod}")

    # Chặn gia hạn phiếu đã đủ 2 lần (MaxRenewals=2)
    mongo_eval(f'db.loans.updateOne({{_id:ObjectId("{loan6_id}")}}, {{$set:{{renewalCount:2, renewalRequests:[]}}}});')
    st_maxr, _, _, b_maxr = req(op6, f"/MyLoans/RequestRenewal/{loan6_id}", "POST", {"__RequestVerificationToken": tk6_rp})
    check("R45", "Renewal", "UC15: Chặn gia hạn phiếu đã đủ 2 lần (MaxRenewals=2)", "đủ 2 lần" in b_maxr, f"HTTP {st_maxr}")

    # Chặn gia hạn khi thẻ bị khóa
    mongo_eval(f"""
    db.loans.updateOne({{_id:ObjectId("{loan6_id}")}}, {{$set:{{renewalCount:0, renewalRequests:[]}}}});
    db.readers.updateOne({{_id:ObjectId("{seed["readerIds"]["DG000006"]}")}}, {{$set:{{cardStatus:"Locked", lockReason:"Test khóa"}}}});
    """)
    st_lkr, _, _, b_lkr = req(op6, f"/MyLoans/RequestRenewal/{loan6_id}", "POST", {"__RequestVerificationToken": tk6_rp})
    check("R46", "Renewal", "UC15: Chặn gia hạn khi thẻ bị khóa (dù đang mở phiên cũ)", "bị khóa" in b_lkr, f"HTTP {st_lkr}")

    # Kiểm tra toàn vẹn tồn kho trên toàn bộ sách sau mọi thao tác
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
    check("R47", "Integrity", "Toàn vẹn tồn kho sau toàn bộ thao tác đặt giữ/hủy/mượn",
          len(inv["mismatches"]) == 0, f"{inv['totalBooks']} books checked, mismatches={inv['mismatches']}")

    passed = sum(1 for r in results if r["passed"])
    print(f"\n=== READER QA SUITE SUMMARY: {passed}/{len(results)} PASSED ===")
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"passed": passed, "total": len(results), "results": results}, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
