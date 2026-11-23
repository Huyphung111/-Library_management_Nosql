import json
import os
import re
import time
import unicodedata
import urllib.parse
import urllib.request
from io import BytesIO
from PIL import Image, ImageDraw, ImageFont

OUT_DIR = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\BookCovers"
ANTIGRAVITY_DIR = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity"

# 32 cuốn sách mới thêm trong MongoDB (extra-00 .. extra-31)
NEW_BOOKS = [
    ("Lập Trình Viên Thực Dụng (The Pragmatic Programmer)", "David Thomas, Andrew Hunt", "The Pragmatic Programmer"),
    ("Tái Cấu Trúc Mã Nguồn (Refactoring)", "Martin Fowler", "Refactoring Martin Fowler"),
    ("Nhập Môn Thuật Toán (Introduction to Algorithms)", "Thomas H. Cormen", "Introduction to Algorithms Cormen"),
    ("Cấu Trúc Dữ Liệu và Giải Thuật", "Đỗ Xuân Lôi", "Cấu Trúc Dữ Liệu và Giải Thuật Đỗ Xuân Lôi"),
    ("Lập Trình Python Cơ Bản", "Eric Matthes", "Python Crash Course Eric Matthes"),
    ("Mạng Máy Tính: Cách Tiếp Cận Từ Trên Xuống", "James F. Kurose, Keith W. Ross", "Computer Networking A Top-Down Approach Kurose"),
    ("Nghĩ Giàu Làm Giàu", "Napoleon Hill", "Nghĩ Giàu Làm Giàu Napoleon Hill"),
    ("Sức Mạnh Của Thói Quen", "Charles Duhigg", "Sức Mạnh Của Thói Quen Charles Duhigg"),
    ("Đời Ngắn Đừng Ngủ Dài", "Robin Sharma", "Đời Ngắn Đừng Ngủ Dài Robin Sharma"),
    ("Quẳng Gánh Lo Đi Mà Vui Sống", "Dale Carnegie", "Quẳng Gánh Lo Đi Mà Vui Sống Dale Carnegie"),
    ("Nhà Lãnh Đạo Không Chức Danh", "Robin Sharma", "Nhà Lãnh Đạo Không Chức Danh Robin Sharma"),
    ("Số Đỏ", "Vũ Trọng Phụng", "Số Đỏ Vũ Trọng Phụng"),
    ("Chí Phèo", "Nam Cao", "Chí Phèo Nam Cao"),
    ("Truyện Kiều", "Nguyễn Du", "Truyện Kiều Nguyễn Du"),
    ("Tắt Đèn", "Ngô Tất Tố", "Tắt Đèn Ngô Tất Tố"),
    ("Nỗi Buồn Chiến Tranh", "Bảo Ninh", "Nỗi Buồn Chiến Tranh Bảo Ninh"),
    ("Đất Rừng Phương Nam", "Đoàn Giỏi", "Đất Rừng Phương Nam Đoàn Giỏi"),
    ("Tôi Thấy Hoa Vàng Trên Cỏ Xanh", "Nguyễn Nhật Ánh", "Tôi Thấy Hoa Vàng Trên Cỏ Xanh Nguyễn Nhật Ánh"),
    ("Hoàng Tử Bé", "Antoine de Saint-Exupéry", "Hoàng Tử Bé"),
    ("Ông Già và Biển Cả", "Ernest Hemingway", "Ông Già và Biển Cả Ernest Hemingway"),
    ("Từ Tốt Đến Vĩ Đại", "Jim Collins", "Từ Tốt Đến Vĩ Đại Jim Collins"),
    ("Nhà Đầu Tư Thông Minh", "Benjamin Graham", "Nhà Đầu Tư Thông Minh Benjamin Graham"),
    ("Kinh Tế Học Hài Hước (Freakonomics)", "Steven D. Levitt, Stephen J. Dubner", "Kinh Tế Học Hài Hước Freakonomics"),
    ("Bí Mật Tư Duy Triệu Phú", "T. Harv Eker", "Bí Mật Tư Duy Triệu Phú T. Harv Eker"),
    ("Vũ Trụ (Cosmos)", "Carl Sagan", "Vũ Trụ Carl Sagan"),
    ("Gen Vị Kỷ", "Richard Dawkins", "Gen Vị Kỷ Richard Dawkins"),
    ("Lược Sử Vạn Vật", "Bill Bryson", "Lược Sử Vạn Vật Bill Bryson"),
    ("Thế Giới Như Tôi Thấy", "Albert Einstein", "Thế Giới Như Tôi Thấy Albert Einstein"),
    ("Súng, Vi Trùng và Thép", "Jared Diamond", "Súng Vi Trùng và Thép Jared Diamond"),
    ("Việt Nam Sử Lược", "Trần Trọng Kim", "Việt Nam Sử Lược Trần Trọng Kim"),
    ("Cộng Hòa", "Plato", "Cộng Hòa Plato"),
    ("Thế Giới Phẳng", "Thomas L. Friedman", "Thế Giới Phẳng Thomas Friedman"),
]

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
    "Accept": "application/json, text/plain, */*",
    "Accept-Language": "vi-VN,vi;q=0.9,en-US;q=0.8",
}


def cover_filename(title: str) -> str:
    """Khớp 100% với DbInitializer.CoverFileName(string title)."""
    s = title.replace("đ", "d").replace("Đ", "D")
    s = unicodedata.normalize("NFD", s)
    s = "".join(ch.lower() for ch in s if unicodedata.category(ch) != "Mn")
    slug = re.sub(r"[^a-z0-9]+", "-", s).strip("-")
    return slug + ".jpg"


def normalize_text(s: str) -> str:
    s = s.replace("đ", "d").replace("Đ", "D")
    s = unicodedata.normalize("NFD", s)
    s = "".join(ch.lower() for ch in s if unicodedata.category(ch) != "Mn")
    return re.sub(r"[^a-z0-9\s]+", " ", s).strip()


def http_get_json(url: str, timeout: int = 12):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8", errors="ignore"))


def http_get_bytes(url: str, timeout: int = 15) -> bytes:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def score_match(candidate_name: str, title: str, author: str) -> float:
    cand = normalize_text(candidate_name)
    # Loại trừ combo / bộ nhiều cuốn nếu có lựa chọn lẻ
    penalty = -1.5 if any(w in cand for w in ["combo", "bo 2", "bo 3", "bo 4", "bo 5", "hop sach"]) else 0.0
    t_tokens = set(normalize_text(title).split())
    a_tokens = set(normalize_text(author).split())
    c_tokens = set(cand.split())
    if not t_tokens:
        return 0.0
    title_overlap = len(t_tokens & c_tokens) / len(t_tokens)
    author_overlap = len(a_tokens & c_tokens) / max(1, len(a_tokens))
    return title_overlap * 3.0 + author_overlap * 1.0 + penalty


def search_tiki(query: str, title: str, author: str):
    q = urllib.parse.quote(query)
    # category=8322 là Nhà Sách Tiki
    url = f"https://tiki.vn/api/v2/products?limit=10&category=8322&q={q}"
    try:
        data = http_get_json(url)
        items = data.get("data", [])
        best = None
        best_score = -999.0
        for item in items:
            name = item.get("name", "")
            thumb = item.get("thumbnail_url", "")
            if not thumb:
                continue
            sc = score_match(name, title, author)
            if sc > best_score:
                best_score = sc
                # Đổi sang ảnh chất lượng cao nếu là ảnh cache 280x280
                hi_url = thumb.replace("/cache/280x280/", "/cache/750x750/")
                best = (hi_url, thumb, name, sc)
        if best and best[3] >= 0.8:
            return best
    except Exception as e:
        print(f"  [Tiki err] {e}")
    return None


def search_openlibrary(query: str, title: str, author: str):
    q = urllib.parse.quote(query)
    url = f"https://openlibrary.org/search.json?q={q}&limit=8"
    try:
        data = http_get_json(url)
        for doc in data.get("docs", []):
            cover_i = doc.get("cover_i")
            if cover_i:
                img_url = f"https://covers.openlibrary.org/b/id/{cover_i}-L.jpg"
                return (img_url, img_url, doc.get("title", query), 2.0)
    except Exception as e:
        print(f"  [OpenLib err] {e}")
    return None


def download_and_save_jpg(url: str, fallback_url: str, out_path: str):
    last_err = None
    for u in [url, fallback_url]:
        if not u:
            continue
        try:
            raw = http_get_bytes(u)
            img = Image.open(BytesIO(raw))
            if img.width < 120 or img.height < 120:
                last_err = f"Image too small: {img.size}"
                continue
            if img.mode != "RGB":
                img = img.convert("RGB")
            # Resize nhẹ nếu quá lớn (> 900px) cho gọn dung lượng
            img.thumbnail((600, 900), Image.Resampling.LANCZOS)
            img.save(out_path, "JPEG", quality=88, optimize=True)
            return img.size, u
        except Exception as e:
            last_err = str(e)
    raise RuntimeError(last_err or "Download failed")


def build_contact_sheets(manifest):
    cols, rows = 4, 4
    cell_w, cell_h = 260, 360
    img_max_w, img_max_h = 210, 270
    try:
        font = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 14)
        font_sm = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 12)
    except Exception:
        font = ImageFont.load_default()
        font_sm = font

    for sheet_idx in range(0, len(manifest), cols * rows):
        chunk = manifest[sheet_idx : sheet_idx + cols * rows]
        canvas = Image.new("RGB", (cols * cell_w, rows * cell_h), (245, 246, 250))
        draw = ImageDraw.Draw(canvas)
        for i, entry in enumerate(chunk):
            r = i // cols
            c = i % cols
            x0, y0 = c * cell_w, r * cell_h
            draw.rectangle([x0 + 6, y0 + 6, x0 + cell_w - 6, y0 + cell_h - 6], fill=(255, 255, 255), outline=(210, 215, 225))
            fp = os.path.join(OUT_DIR, entry["filename"])
            if os.path.exists(fp):
                im = Image.open(fp)
                im.thumbnail((img_max_w, img_max_h), Image.Resampling.LANCZOS)
                ix = x0 + (cell_w - im.width) // 2
                iy = y0 + 14 + (img_max_h - im.height) // 2
                canvas.paste(im, (ix, iy))
            num = entry["index"]
            label = f"#{num}: {entry['title']}"
            if len(label) > 34:
                label = label[:32] + "..."
            draw.text((x0 + 12, y0 + 295), label, fill=(20, 40, 70), font=font)
            src = f"[{entry['source']}] {entry['size']}"
            draw.text((x0 + 12, y0 + 320), src, fill=(100, 110, 125), font=font_sm)
        sheet_num = sheet_idx // (cols * rows) + 1
        out_sheet = os.path.join(ANTIGRAVITY_DIR, f"verify_sheet_{sheet_num}.jpg")
        canvas.save(out_sheet, "JPEG", quality=85)
        print(f"Saved contact sheet: {out_sheet}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    manifest = []

    for idx, (title, author, query) in enumerate(NEW_BOOKS, start=1):
        fname = cover_filename(title)
        out_path = os.path.join(OUT_DIR, fname)
        print(f"[{idx:02d}/32] {title} -> {fname}")

        res = search_tiki(query, title, author)
        source = "Tiki"
        if not res:
            res = search_tiki(title, title, author)
        if not res:
            res = search_openlibrary(query, title, author)
            source = "OpenLibrary"

        if not res:
            print(f"  !! NOT FOUND: {title}")
            manifest.append({"index": idx, "title": title, "filename": fname, "source": "MISSING", "matched": "", "size": "0x0", "url": ""})
            continue

        hi_url, fb_url, matched_name, sc = res
        try:
            size, used_url = download_and_save_jpg(hi_url, fb_url, out_path)
            print(f"  OK ({source}, {size[0]}x{size[1]}): {matched_name}")
            manifest.append({
                "index": idx,
                "title": title,
                "filename": fname,
                "source": source,
                "matched": matched_name,
                "size": f"{size[0]}x{size[1]}",
                "url": used_url,
            })
        except Exception as e:
            print(f"  !! DOWNLOAD ERR: {e}")
            manifest.append({"index": idx, "title": title, "filename": fname, "source": "ERROR", "matched": matched_name, "size": "0x0", "url": hi_url})

        time.sleep(0.25)

    with open(os.path.join(OUT_DIR, "_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    build_contact_sheets(manifest)
    ok_cnt = sum(1 for m in manifest if m["source"] not in ("MISSING", "ERROR"))
    print(f"\nDONE: {ok_cnt}/{len(manifest)} covers downloaded.")


if __name__ == "__main__":
    main()
