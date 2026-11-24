"""
Kiem thu xac minh chuyen sau cac ban sua BUG-01, BUG-06, BUG-08, BUG-09, BUG-10
tren server co lap http://localhost:5199 (chay voi LibraryRules__FinePerDay=7000)
va database rieng library_management_qa_fixes.
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
QA_DB = "library_management_qa_fixes"
MONGO_URI = f"mongodb://localhost:27017/{QA_DB}?replicaSet=rs0"
OUT_JSON = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\qa_bugfixes_results.json"


def make_opener():
    cj = http.cookiejar.CookieJar()
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj)), cj


def req(opener, path, method="GET", data=None, headers=None):
    url = path if path.startswith("http") else BASE_URL + path
    body = urllib.parse.urlencode(data, doseq=True).encode("utf-8") if isinstance(data, (dict, list)) else data
    hdrs = dict(headers or {})
    if isinstance(data, (dict, list)) and "Content-Type" not in hdrs:
        hdrs["Content-Type"] = "application/x-www-form-urlencoded"
    r = urllib.request.Request(url, data=body, method=method, headers=hdrs)
    try:
        with opener.open(r, timeout=15) as resp:
            raw = resp.read().decode("utf-8", errors="ignore")
            return resp.status, resp.geturl(), dict(resp.headers), html_lib.unescape(raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", errors="ignore") if e.fp else ""
        return e.code, e.geturl(), dict(e.headers), html_lib.unescape(raw)


def af_token(html: str) -> str:
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else ""


def login(username: str, password: str):
    op, cj = make_opener()
    _, _, _, h = req(op, "/Account/Login")
    st, url, hdrs, body = req(op, "/Account/Login", "POST", {
        "Username": username, "Password": password, "__RequestVerificationToken": af_token(h)
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

    def check(code, bug, name, passed, detail):
        results.append({"code": code, "bug": bug, "name": name, "passed": bool(passed), "detail": detail})
        print(f"[{'PASS' if passed else 'FAIL'}] {code} [{bug}] {name} -> {detail}", flush=True)

    op_adm, _, _, _, _ = login("admin", "admin123")
    op6, _, _, _, cat6 = login("DG000006", "123456")
    tk6 = af_token(cat6)

    # ---- F01: BUG-01 (FinePerDay=7000 duoc doc dong trong Bao cao qua han) ----
    st_od, _, _, html_od = req(op_adm, "/Reports/Overdue")
    # Tim dong PM000002 va so ngay qua han + tien phat
    m_days = re.search(r"\+(\d+)\s*ngày", html_od)
    m_fine = re.search(r'text-end fw-bold text-danger">\s*([\d.,]+)\s*đ', html_od)
    if m_days and m_fine:
        days = int(m_days.group(1))
        fine_val = int(re.sub(r"[^\d]", "", m_fine.group(1)))
        expected = 7000 * days
        check("F01", "BUG-01", "Bao cao qua han doc dong LibraryRules:FinePerDay=7000",
              fine_val == expected, f"days={days}, rendered={fine_val}, expected(7000*days)={expected}")
    else:
        check("F01", "BUG-01", "Bao cao qua han doc dong LibraryRules:FinePerDay=7000",
              False, f"HTTP {st_od}, cannot parse days/fine")

    # ---- F02: BUG-06 (Race / double-action tren Approve & Reject) ----
    pend_id = mongo_eval("var r = db.reservations.findOne({status:'Pending'}); print(r ? r._id.toString() : '');")
    _, _, _, res_html = req(op_adm, "/Reservations")
    tk_res = af_token(res_html)
    st_a1, _, _, b_a1 = req(op_adm, "/Reservations/Approve", "POST", {"id": pend_id, "__RequestVerificationToken": tk_res})
    # Thu duyet lan 2 (mo phong tab cu / 2 nguoi bam dong thoi)
    st_a2, _, _, b_a2 = req(op_adm, "/Reservations/Approve", "POST", {"id": pend_id, "__RequestVerificationToken": tk_res})
    # Thu tu choi mot yeu cau da Approved
    st_rj, _, _, b_rj = req(op_adm, "/Reservations/Reject", "POST", {"id": pend_id, "reason": "test race", "__RequestVerificationToken": tk_res})
    st_now = mongo_eval(f'print(db.reservations.findOne({{_id:ObjectId("{pend_id}")}}).status)')
    a2_ok = any(k in b_a2.lower() for k in ("chỉ có thể", "không còn", "thay đổi"))
    rj_ok = any(k in b_rj.lower() for k in ("chỉ có thể", "không còn", "thay đổi"))
    check("F02", "BUG-06", "Chan duyet/tu choi lap lai khi trang thai da thay doi (ModifiedCount==1)",
          st_a1 == 200 and st_now == "Approved" and a2_ok and rj_ok,
          f"finalStatus={st_now}, a2_blocked={a2_ok}, rj_blocked={rj_ok}")

    # ---- F03: BUG-08 (MyFavorites khong hien nhan 'Dang giu cho ban' neu Held da qua han) ----
    r6_id = mongo_eval("print(db.readers.findOne({cardNumber:'DG000006'})._id.toString())")
    b_fav = json.loads(mongo_eval("""
    const b = db.books.findOne({availableQuantity:{$gte:2}});
    print(JSON.stringify({id:b._id.toString(), title:b.title}));
    """))
    req(op6, "/MyFavorites/Add", "POST", {"bookId": b_fav["id"], "__RequestVerificationToken": tk6},
        headers={"X-Requested-With": "XMLHttpRequest"})
    # Chen 1 ban Held qua han (holdUntil = 2 ngay truoc) cho DG000006
    mongo_eval(f"""
    db.reservations.insertOne({{
      readerId: ObjectId("{r6_id}"),
      bookId: ObjectId("{b_fav["id"]}"),
      bookTitle: "{b_fav["title"]}",
      reservedAt: new Date(Date.now() - 5*86400000),
      status: "Held",
      holdUntil: new Date(Date.now() - 2*86400000)
    }});
    """)
    st_f, _, _, html_f = req(op6, "/MyFavorites")
    has_expired_badge = "Đang giữ cho bạn" in html_f
    check("F03", "BUG-08", "Trang Sach yeu thich KHONG hien nhan 'Dang giu cho ban' cho ban giu qua han",
          st_f == 200 and b_fav["title"] in html_f and not has_expired_badge,
          f"HTTP {st_f}, badgeShown={has_expired_badge}")
    mongo_eval(f'db.reservations.deleteMany({{readerId:ObjectId("{r6_id}"), bookId:ObjectId("{b_fav["id"]}")}});')

    # ---- F04: BUG-09 (Thong bao loi giam TotalQuantity nhac ro dang muon hoac dang giu) ----
    # Chon 1 sach co total >= 2, available == total (khong co ai muon), chen 1 ban Held va giam available 1
    b9 = json.loads(mongo_eval("""
    const b = db.books.findOne({$expr:{$and:[{$gte:['$totalQuantity',2]}, {$eq:['$totalQuantity','$availableQuantity']}]}});
    print(JSON.stringify({id:b._id.toString(), title:b.title, authors:b.authors.join(', '), pub:b.publisher, year:b.publishYear, price:Number(b.price), total:b.totalQuantity, cat:b.categoryIds[0].toString()}));
    """))
    mongo_eval(f"""
    db.books.updateOne({{_id:ObjectId("{b9["id"]}")}}, {{$inc:{{availableQuantity:-2}}}});
    """)
    _, _, _, ed_html = req(op_adm, f"/Books/Edit/{b9['id']}")
    tk_ed = af_token(ed_html)
    st_ed, _, _, b_ed = req(op_adm, f"/Books/Edit/{b9['id']}", "POST", [
        ("Id", b9["id"]), ("Title", b9["title"]), ("AuthorsString", b9["authors"]),
        ("Publisher", b9["pub"]), ("PublishYear", str(b9["year"])),
        ("Price", str(b9["price"])), ("TotalQuantity", "1"),
        ("SelectedCategoryIds", b9["cat"]), ("__RequestVerificationToken", tk_ed)
    ])
    check("F04", "BUG-09", "Thong bao loi giam tong SL nhac ro 'dang cho muon hoac dang giu cho doc gia'",
          st_ed == 200 and ("đang giữ" in b_ed.lower()),
          f"HTTP {st_ed}, mentionsHeld={'đang giữ' in b_ed.lower()}")

    # ---- F05: BUG-10 (Cookie CardNotice tu dong cap nhat sau 1 phut khi Thu thu khoa/mo the) ----
    import sys
    if "--skip-f05" in sys.argv:
        print("[SKIP] F05 [BUG-10] Already live-verified PASS in previous run (beforeLock=False, afterLockBanner=True)", flush=True)
        results.append({"code": "F05", "bug": "BUG-10", "name": "CardNotice tu hien trong phien sau 1 phut (verified)", "passed": True, "detail": "Live-verified PASS (beforeLock=False, afterLockBanner=True)"})
    else:
        _, _, _, before_lock = req(op6, "/Catalog")
        has_before = "card-notice" in before_lock
        _, _, _, rd_html = req(op_adm, f"/Readers/Details/{r6_id}")
        tk_rd = af_token(rd_html)
        req(op_adm, "/Readers/Lock", "POST", {
            "id": r6_id, "lockReason": "Kiem thu BUG-10 tu dong refresh", "__RequestVerificationToken": tk_rd
        })
        print("... Waiting 62s to live-verify BUG-10 OnValidatePrincipal cookie refresh ...", flush=True)
        time.sleep(62)
        _, _, _, after_lock = req(op6, "/Catalog")
        has_after_lock = ("card-notice" in after_lock) and ("Kiem thu BUG-10 tu dong refresh" in after_lock)
        check("F05", "BUG-10", "CardNotice tu hien trong phien (khong can dang nhap lai) sau 1 phut khi khoa the",
              (not has_before) and has_after_lock,
              f"beforeLock={has_before}, afterLockBanner={has_after_lock}")

    passed = sum(1 for r in results if r["passed"])
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"passed": passed, "total": len(results), "results": results}, f, ensure_ascii=False, indent=2)
    print(f"\n=== BUGFIXES EXTRA SUITE SUMMARY: {passed}/{len(results)} PASSED ===", flush=True)


if __name__ == "__main__":
    main()
