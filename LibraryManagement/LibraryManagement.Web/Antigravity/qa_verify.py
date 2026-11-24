"""
Script kiểm thử tổng hợp cho LibraryManagement.Web:
  1. Kiểm tra toàn vẹn dữ liệu & index trong MongoDB (chỉ đọc, không sửa DB thật).
  2. Kiểm tra live HTTP (chỉ đọc + bảo mật RBAC/CSRF/edge cases) trên http://localhost:5101.
Lưu kết quả JSON vào Antigravity/qa_results.json.
"""
import http.cookiejar
import json
import re
import subprocess
import urllib.error
import urllib.parse
import urllib.request

BASE_URL = "http://localhost:5101"
MONGO_URI = "mongodb://localhost:27017/library_management?replicaSet=rs0"
OUT_JSON = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_results.json"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def make_opener(follow_redirects=True):
    cj = http.cookiejar.CookieJar()
    handlers = [urllib.request.HTTPCookieProcessor(cj)]
    if not follow_redirects:
        handlers.append(NoRedirect())
    return urllib.request.build_opener(*handlers), cj


def fetch(opener, path, method="GET", data=None, headers=None):
    url = path if path.startswith("http") else BASE_URL + path
    encoded = urllib.parse.urlencode(data).encode("utf-8") if isinstance(data, dict) else data
    req = urllib.request.Request(url, data=encoded, method=method, headers=headers or {})
    try:
        with opener.open(req, timeout=10) as resp:
            return resp.status, resp.geturl(), dict(resp.headers), resp.read().decode("utf-8", errors="ignore")
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore") if e.fp else ""
        return e.code, e.geturl(), dict(e.headers), body
    except Exception as e:
        return -1, url, {}, str(e)


def extract_af_token(html: str) -> str:
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else ""


def login_as(username: str, password: str):
    opener, cj = make_opener(follow_redirects=True)
    st, url, hdrs, html = fetch(opener, "/Account/Login")
    token = extract_af_token(html)
    st2, url2, hdrs2, html2 = fetch(
        opener,
        "/Account/Login",
        method="POST",
        data={
            "Username": username,
            "Password": password,
            "__RequestVerificationToken": token,
        },
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    return opener, cj, st2, url2, html2


def run_mongo_audit():
    js = r"""
    const res = { counts: {}, indexes: {}, issues: [], warnings: [], sampleIds: {} };
    const cols = ['users','readers','categories','books','loans','reservations','counters','covers.files','covers.chunks'];
    cols.forEach(c => res.counts[c] = db.getCollection(c).countDocuments());

    ['users','readers','categories','books','loans','reservations'].forEach(c => {
      res.indexes[c] = db.getCollection(c).getIndexes().map(i => i.name);
    });

    const catIds = new Set(db.categories.find({}, {_id:1}).toArray().map(c => c._id.toString()));
    const coverIds = new Set(db.getCollection('covers.files').find({}, {_id:1}).toArray().map(c => c._id.toString()));
    const readerIds = new Set(db.readers.find({}, {_id:1}).toArray().map(r => r._id.toString()));
    const books = db.books.find().toArray();
    const bookMap = {};
    books.forEach(b => bookMap[b._id.toString()] = b);
    if (books.length > 0) {
      res.sampleIds.bookId = books[0]._id.toString();
      res.sampleIds.coverId = books[0].coverImageId ? books[0].coverImageId.toString() : null;
    }

    // 1. Tính số đang mượn chưa trả & đang giữ (Held) cho từng sách
    const unreturnedByBook = {};
    const loans = db.loans.find().toArray();
    loans.forEach(l => {
      let itemFineSum = 0;
      let openItems = 0;
      (l.items || []).forEach(it => {
        itemFineSum += Number(it.fine || 0);
        if (it.returnDate == null) {
          openItems++;
          if (l.status === 'Borrowing') {
            const bid = it.bookId.toString();
            unreturnedByBook[bid] = (unreturnedByBook[bid] || 0) + 1;
          }
        }
      });
      if (l.status === 'Returned' && openItems > 0)
        res.issues.push('Loan ' + l.loanCode + ' status=Returned nhưng còn ' + openItems + ' cuốn chưa trả');
      if (l.status === 'Borrowing' && openItems === 0)
        res.issues.push('Loan ' + l.loanCode + ' status=Borrowing nhưng không còn cuốn nào chưa trả');
      if (Number(l.totalFine || 0) !== itemFineSum)
        res.issues.push('Loan ' + l.loanCode + ' totalFine (' + l.totalFine + ') != sum(items.fine) (' + itemFineSum + ')');
      const pendingRen = (l.renewalRequests || []).filter(r => r.status === 'Pending').length;
      if (pendingRen > 1)
        res.issues.push('Loan ' + l.loanCode + ' có ' + pendingRen + ' yêu cầu gia hạn Pending cùng lúc');
      if (l.status === 'Returned' && pendingRen > 0)
        res.warnings.push('Loan ' + l.loanCode + ' đã Returned nhưng vẫn còn RenewalRequest Pending');
    });

    const heldByBook = {};
    const activeResKey = new Set();
    const nowUtc = new Date();
    const startTodayVn = new Date(Date.UTC(nowUtc.getUTCFullYear(), nowUtc.getUTCMonth(), nowUtc.getUTCDate()) - 7*3600*1000);
    db.reservations.find().toArray().forEach(r => {
      const bid = r.bookId.toString();
      const rid = r.readerId.toString();
      if (!bookMap[bid]) res.issues.push('Reservation ' + r._id + ' trỏ tới bookId không tồn tại: ' + bid);
      if (!readerIds.has(rid)) res.issues.push('Reservation ' + r._id + ' trỏ tới readerId không tồn tại: ' + rid);
      if (r.status === 'Held') {
        heldByBook[bid] = (heldByBook[bid] || 0) + 1;
        if (r.holdUntil && r.holdUntil < startTodayVn)
          res.warnings.push('Reservation Held ' + r._id + ' đã quá hạn nhận holdUntil=' + r.holdUntil.toISOString());
      }
      if (['Pending','Approved','Held'].includes(r.status)) {
        const k = rid + ':' + bid;
        if (activeResKey.has(k))
          res.issues.push('Vi phạm QĐ08: Độc giả ' + rid + ' có >1 yêu cầu còn hiệu lực cho sách ' + bid);
        activeResKey.add(k);
      }
    });

    // 2. Kiểm tra từng sách: tồn kho, thể loại, ảnh bìa
    let stockMismatch = 0;
    let missingCover = 0;
    books.forEach(b => {
      const bid = b._id.toString();
      if (b.availableQuantity < 0 || b.availableQuantity > b.totalQuantity || b.totalQuantity <= 0)
        res.issues.push('Sách "' + b.title + '" sai số lượng: avail=' + b.availableQuantity + ', total=' + b.totalQuantity);
      const expectedOut = (unreturnedByBook[bid] || 0) + (heldByBook[bid] || 0);
      const actualOut = b.totalQuantity - b.availableQuantity;
      if (actualOut !== expectedOut) {
        stockMismatch++;
        res.warnings.push('Lệch kho sách "' + b.title + '": total-avail=' + actualOut + ' vs (mượn=' + (unreturnedByBook[bid]||0) + ' + giữ=' + (heldByBook[bid]||0) + ')');
      }
      if (!b.categoryIds || b.categoryIds.length === 0)
        res.issues.push('Sách "' + b.title + '" không có thể loại');
      (b.categoryIds || []).forEach(cid => {
        if (!cid || !catIds.has(cid.toString()))
          res.issues.push('Sách "' + b.title + '" có categoryId lỗi/không tồn tại: ' + cid);
      });
      if (!b.coverImageId || !coverIds.has(b.coverImageId.toString())) missingCover++;
    });
    res.stockMismatchCount = stockMismatch;
    res.missingCoverCount = missingCover;

    // 3. Kiểm tra users & readers
    db.users.find({role: 'Reader'}).forEach(u => {
      if (!u.readerId || !readerIds.has(u.readerId.toString()))
        res.issues.push('User ' + u.username + ' trỏ tới readerId không tồn tại');
    });
    db.readers.find().forEach(r => {
      (r.favoriteBookIds || []).forEach(fid => {
        if (!bookMap[fid.toString()])
          res.warnings.push('Reader ' + r.cardNumber + ' có favoriteBookId không tồn tại: ' + fid);
      });
    });

    print(JSON.stringify(res));
    """
    proc = subprocess.run(
        ["mongosh", MONGO_URI, "--quiet", "--eval", js],
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=20,
    )
    if proc.returncode != 0:
        return {"error": proc.stderr or proc.stdout}
    return json.loads(proc.stdout.strip().splitlines()[-1])


def run_http_tests(sample_book_id: str, sample_cover_id: str):
    results = []

    def record(group, name, passed, detail):
        results.append({"group": group, "name": name, "passed": passed, "detail": detail})
        status = "PASS" if passed else "FAIL"
        print(f"[{status}] [{group}] {name}: {detail}")

    # 1. Guest / Public
    guest_no_redir, _ = make_opener(follow_redirects=False)
    st, url, hdrs, body = fetch(guest_no_redir, "/")
    record("Guest", "Trang chủ công khai GET /", st == 200 and "DANH MỤC SÁCH" in body, f"HTTP {st}")

    if sample_cover_id:
        st, url, hdrs, _ = fetch(guest_no_redir, f"/Covers/{sample_cover_id}")
        ct = hdrs.get("Content-Type", hdrs.get("content-type", ""))
        record("Guest", "Ảnh bìa công khai /Covers/{id}", st == 200 and ct.startswith("image/"), f"HTTP {st}, {ct}")

    for protected in ["/Dashboard", "/Books", "/Loans/Create", "/MyLoans", "/MyFavorites", "/Catalog"]:
        st, url, hdrs, _ = fetch(guest_no_redir, protected)
        loc = hdrs.get("Location", hdrs.get("location", ""))
        record("RBAC", f"Khách bị chặn {protected}", st == 302 and "/Account/Login" in loc, f"HTTP {st} -> {loc}")

    # 2. Login Librarian
    lib_opener, lib_cj, st, url, body = login_as("admin", "admin123")
    record("Auth", "Đăng nhập Thủ thư (admin)", st == 200 and "/Dashboard" in url, f"HTTP {st} -> {url}")

    # Check Librarian routes (200 OK)
    lib_routes = [
        "/Dashboard",
        "/Books",
        "/Books/Create",
        "/Books?search=(test[+",
        "/Categories",
        "/Readers",
        "/Readers/Create",
        "/Loans/Create",
        "/Loans/CheckReader?cardNumber=DG000001",
        "/Loans/CheckReader?cardNumber=KHONGTONTAI",
        "/Loans/Return",
        "/Loans/Return?keyword=DG000001",
        "/Renewals",
        "/Reservations",
        "/Reservations?tab=held",
        "/Reservations?tab=ready",
        "/Search?type=book&keyword=(test[+",
        "/Search?type=reader&keyword=DG",
        "/Search?type=loan&loanStatus=overdue",
        "/Reports/TopBooks",
        "/Reports/Overdue",
        "/Reports/ByMonth",
        "/Reports/ByCategory",
        "/Reports/Fines",
    ]
    for r in lib_routes:
        st, u, h, b = fetch(lib_opener, r)
        ok = st == 200 and "An unhandled exception" not in b
        record("LibrarianPages", f"GET {r}", ok, f"HTTP {st}")

    # Librarian blocked from Reader routes
    lib_no_redir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(lib_cj), NoRedirect())
    for r in ["/Catalog", "/MyLoans", "/MyReservations", "/MyFavorites"]:
        st, u, h, _ = fetch(lib_no_redir, r)
        loc = h.get("Location", h.get("location", ""))
        record("RBAC", f"Thủ thư bị chặn {r}", st in (302, 403) and ("AccessDenied" in loc or st == 403), f"HTTP {st} -> {loc}")

    # Malformed ObjectId resilience
    for bad in ["/Books/Edit/bad-id", "/Categories/Edit/bad-id", "/Readers/Details/bad-id", "/Loans/Details/bad-id", "/Covers/bad-id"]:
        st, u, h, b = fetch(lib_opener, bad)
        record("Resilience", f"ObjectId sai định dạng {bad}", st in (200, 404) and "An unhandled exception" not in b, f"HTTP {st} -> {u}")

    # 3. Login Reader (Active: DG000001)
    rd_opener, rd_cj, st, url, body = login_as("DG000001", "123456")
    record("Auth", "Đăng nhập Độc giả hoạt động (DG000001)", st == 200 and "/Catalog" in url, f"HTTP {st} -> {url}")

    reader_routes = [
        "/Catalog",
        f"/Catalog/Details/{sample_book_id}",
        "/Catalog/Details/bad-id",
        "/MyLoans",
        "/MyLoans/History",
        "/MyReservations",
        f"/MyReservations/Create?bookId={sample_book_id}",
        "/MyFavorites",
        "/Account/ChangePassword",
    ]
    for r in reader_routes:
        st, u, h, b = fetch(rd_opener, r)
        ok = st == 200 and "An unhandled exception" not in b
        record("ReaderPages", f"GET {r}", ok, f"HTTP {st}")

    # Reader AJAX confirm box
    st, u, h, b = fetch(rd_opener, f"/MyReservations/Create?bookId={sample_book_id}", headers={"X-Requested-With": "XMLHttpRequest"})
    record("ReaderPages", "AJAX hộp xác nhận đặt giữ/đặt trước", st == 200 and "data-reserve-form" in b, f"HTTP {st}")

    # Reader blocked from Librarian routes
    rd_no_redir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(rd_cj), NoRedirect())
    for r in ["/Dashboard", "/Books", "/Readers", "/Loans/Create", "/Renewals", "/Reservations", "/Reports/TopBooks"]:
        st, u, h, _ = fetch(rd_no_redir, r)
        loc = h.get("Location", h.get("location", ""))
        record("RBAC", f"Độc giả bị chặn {r}", st in (302, 403) and ("AccessDenied" in loc or st == 403), f"HTTP {st} -> {loc}")

    # CSRF check: POST without token must return 400
    st, u, h, _ = fetch(rd_no_redir, "/MyFavorites/Add", method="POST", data={"bookId": sample_book_id})
    record("Security", "Chặn POST /MyFavorites/Add thiếu CSRF token", st == 400, f"HTTP {st}")

    # Wrong password check
    _, _, st, u, b = login_as("DG000001", "sai-mat-khau")
    record("Auth", "Từ chối sai mật khẩu (thông báo chung)", st == 200 and "không đúng" in b, f"HTTP {st}")

    # Locked reader (DG000009) & Expired reader (DG000010)
    _, _, st, u, b = login_as("DG000009", "123456")
    record("Auth", "Thẻ bị khóa (DG000009) vẫn vào được + có CardNotice", st == 200 and "card-notice" in b and "bị khóa" in b, f"HTTP {st} -> {u}")

    _, _, st, u, b = login_as("DG000010", "123456")
    record("Auth", "Thẻ hết hạn (DG000010) vẫn vào được + có CardNotice", st == 200 and "card-notice" in b and "hết hạn" in b, f"HTTP {st} -> {u}")

    return results


def main():
    print("=== 1. KIỂM TRA TOÀN VẸN MONGODB ===")
    mongo = run_mongo_audit()
    print(json.dumps(mongo, ensure_ascii=False, indent=2))

    print("\n=== 2. KIỂM TRA LIVE HTTP (READ-ONLY + SECURITY) ===")
    sample_book_id = mongo.get("sampleIds", {}).get("bookId", "")
    sample_cover_id = mongo.get("sampleIds", {}).get("coverId", "")
    http_res = run_http_tests(sample_book_id, sample_cover_id)

    passed = sum(1 for x in http_res if x["passed"])
    total = len(http_res)
    print(f"\nHTTP SUMMARY: {passed}/{total} PASSED")

    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"mongo": mongo, "http": http_res, "summary": {"passed": passed, "total": total}}, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
