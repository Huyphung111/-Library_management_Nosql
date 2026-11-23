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

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
    "Accept": "application/json, text/plain, */*",
    "Accept-Language": "vi-VN,vi;q=0.9,en-US;q=0.8",
}

# (index, vn_title, mode, tiki_query, must_contain_norm, openlib_title, openlib_author)
FIX_LIST = [
    (1, "Lập Trình Viên Thực Dụng (The Pragmatic Programmer)", "openlib", None, None, "The Pragmatic Programmer", "Hunt"),
    (2, "Tái Cấu Trúc Mã Nguồn (Refactoring)", "openlib", None, None, "Refactoring: Improving the Design of Existing Code", "Martin Fowler"),
    (3, "Nhập Môn Thuật Toán (Introduction to Algorithms)", "openlib", None, None, "Introduction to Algorithms", "Cormen"),
    (5, "Lập Trình Python Cơ Bản", "openlib", None, None, "Python Crash Course", "Eric Matthes"),
    (6, "Mạng Máy Tính: Cách Tiếp Cận Từ Trên Xuống", "openlib", None, None, "Computer Networking: A Top-Down Approach", "Kurose"),
    (12, "Số Đỏ", "tiki_then_ol", "Số Đỏ Vũ Trọng Phụng", "so do", "Số đỏ", "Vũ Trọng Phụng"),
    (21, "Từ Tốt Đến Vĩ Đại", "tiki_then_ol", "Từ Tốt Đến Vĩ Đại Jim Collins", "tu tot den vi dai", "Good to Great", "Jim Collins"),
    (22, "Nhà Đầu Tư Thông Minh", "tiki_then_ol", "Nhà Đầu Tư Thông Minh Benjamin Graham", "nha dau tu thong minh", "The Intelligent Investor", "Benjamin Graham"),
    (23, "Kinh Tế Học Hài Hước (Freakonomics)", "tiki_then_ol", "Kinh Tế Học Hài Hước", "kinh te hoc hai huoc", "Freakonomics", "Steven Levitt"),
    (24, "Bí Mật Tư Duy Triệu Phú", "tiki_then_ol", "Bí Mật Tư Duy Triệu Phú", "bi mat tu duy trieu phu", "Secrets of the Millionaire Mind", "T. Harv Eker"),
    (25, "Vũ Trụ (Cosmos)", "tiki_then_ol", "Vũ Trụ Carl Sagan", "vu tru", "Cosmos", "Carl Sagan"),
    (26, "Gen Vị Kỷ", "tiki_then_ol", "Gen Vị Kỷ Richard Dawkins", "gen vi ky", "The Selfish Gene", "Richard Dawkins"),
    (27, "Lược Sử Vạn Vật", "tiki_then_ol", "Lược Sử Vạn Vật Bill Bryson", "luoc su van vat", "A Short History of Nearly Everything", "Bill Bryson"),
    (28, "Thế Giới Như Tôi Thấy", "tiki_then_ol", "Thế Giới Như Tôi Thấy Albert Einstein", "the gioi nhu toi thay", "The World As I See It", "Albert Einstein"),
    (31, "Cộng Hòa", "tiki_then_ol", "Cộng Hòa Plato", "cong hoa", "The Republic", "Plato"),
    (32, "Thế Giới Phẳng", "tiki_then_ol", "Thế Giới Phẳng Thomas Friedman", "the gioi phang", "The World Is Flat", "Thomas Friedman"),
]


def normalize_text(s: str) -> str:
    s = s.replace("đ", "d").replace("Đ", "D")
    s = unicodedata.normalize("NFD", s)
    s = "".join(ch.lower() for ch in s if unicodedata.category(ch) != "Mn")
    return re.sub(r"[^a-z0-9\s]+", " ", s).strip()


def http_get_json(url: str, timeout: int = 15):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8", errors="ignore"))


def http_get_bytes(url: str, timeout: int = 18) -> bytes:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def search_tiki_strict(query: str, must_contain: str):
    q = urllib.parse.quote(query)
    url = f"https://tiki.vn/api/v2/products?limit=12&category=8322&q={q}"
    try:
        data = http_get_json(url)
        for item in data.get("data", []):
            name = item.get("name", "")
            norm = normalize_text(name)
            if "combo" in norm or "trung so doc dac" in norm:
                continue
            if must_contain in norm and item.get("thumbnail_url"):
                thumb = item["thumbnail_url"]
                hi = thumb.replace("/cache/280x280/", "/cache/750x750/")
                return (hi, thumb, name, "Tiki")
    except Exception as e:
        print(f"  [Tiki err] {e}")
    return None


def search_openlibrary(title: str, author: str):
    params = urllib.parse.urlencode({"title": title, "author": author, "limit": 8})
    url = f"https://openlibrary.org/search.json?{params}"
    try:
        data = http_get_json(url)
        for doc in data.get("docs", []):
            cover_i = doc.get("cover_i")
            if cover_i:
                img_url = f"https://covers.openlibrary.org/b/id/{cover_i}-L.jpg"
                return (img_url, img_url, doc.get("title", title), "OpenLibrary")
    except Exception as e:
        print(f"  [OpenLib err] {e}")
    return None


def download_jpg(url: str, fallback_url: str, out_path: str):
    last_err = None
    for u in [url, fallback_url]:
        if not u:
            continue
        try:
            raw = http_get_bytes(u)
            img = Image.open(BytesIO(raw))
            if img.width < 120 or img.height < 120:
                last_err = f"Too small {img.size}"
                continue
            if img.mode != "RGB":
                img = img.convert("RGB")
            img.thumbnail((600, 900), Image.Resampling.LANCZOS)
            img.save(out_path, "JPEG", quality=88, optimize=True)
            return img.size, u
        except Exception as e:
            last_err = str(e)
    raise RuntimeError(last_err or "Failed")


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
        print(f"Rebuilt contact sheet: {out_sheet}")


def main():
    manifest_path = os.path.join(OUT_DIR, "_manifest.json")
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    by_idx = {m["index"]: m for m in manifest}

    for idx, title, mode, tiki_q, must_c, ol_title, ol_author in FIX_LIST:
        entry = by_idx[idx]
        out_path = os.path.join(OUT_DIR, entry["filename"])
        print(f"[Fix #{idx:02d}] {title}")
        res = None
        if mode == "tiki_then_ol":
            res = search_tiki_strict(tiki_q, must_c)
            time.sleep(1.3)
        if not res:
            res = search_openlibrary(ol_title, ol_author)
            time.sleep(0.5)

        if not res:
            print(f"  !! STILL NOT FOUND: {title}")
            continue

        hi, fb, matched, src = res
        try:
            size, used_url = download_jpg(hi, fb, out_path)
            entry["source"] = src
            entry["matched"] = matched
            entry["size"] = f"{size[0]}x{size[1]}"
            entry["url"] = used_url
            print(f"  OK ({src}, {size[0]}x{size[1]}): {matched}")
        except Exception as e:
            print(f"  !! ERR: {e}")

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    build_contact_sheets(manifest)
    ok_cnt = sum(1 for m in manifest if m["source"] not in ("MISSING", "ERROR"))
    print(f"\nTOTAL OK: {ok_cnt}/{len(manifest)}")


if __name__ == "__main__":
    main()
