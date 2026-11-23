import json
import os
import urllib.parse
import urllib.request
from io import BytesIO
from PIL import Image, ImageDraw, ImageFont

OUT_DIR = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity\BookCovers"
ANTIGRAVITY_DIR = r"D:\HocKi1Nam4\Nosql\DoAn\LibraryManagement\LibraryManagement.Web\Antigravity"

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0.0.0 Safari/537.36"
}


def http_get_bytes(url: str, timeout: int = 18) -> bytes:
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def http_get_json(url: str, timeout: int = 15):
    return json.loads(http_get_bytes(url, timeout).decode("utf-8", errors="ignore"))


def try_save_jpg(url: str, out_path: str):
    raw = http_get_bytes(url)
    img = Image.open(BytesIO(raw))
    if img.width < 120 or img.height < 120:
        raise ValueError(f"Too small: {img.size}")
    if img.mode != "RGB":
        img = img.convert("RGB")
    img.thumbnail((600, 900), Image.Resampling.LANCZOS)
    img.save(out_path, "JPEG", quality=88, optimize=True)
    return img.size


def try_isbns(isbns, out_path):
    for isbn in isbns:
        url = f"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg?default=false"
        try:
            sz = try_save_jpg(url, out_path)
            return sz, url, f"OpenLibrary ISBN {isbn}"
        except Exception as e:
            print(f"    ISBN {isbn} failed: {e}")
    return None


def try_google_books(query: str, out_path: str, skip_first: int = 0):
    q = urllib.parse.quote(query)
    url = f"https://www.googleapis.com/books/v1/volumes?q={q}&maxResults=8"
    try:
        data = http_get_json(url)
        seen = 0
        for item in data.get("items", []):
            info = item.get("volumeInfo", {})
            links = info.get("imageLinks", {})
            thumb = links.get("thumbnail") or links.get("smallThumbnail")
            if not thumb:
                continue
            if seen < skip_first:
                seen += 1
                continue
            # Yêu cầu zoom=1 hoặc zoom=2 để ảnh rõ hơn
            hi = thumb.replace("http://", "https://").replace("&edge=curl", "")
            hi = hi.replace("zoom=1", "zoom=2")
            try:
                sz = try_save_jpg(hi, out_path)
                return sz, hi, f"GoogleBooks: {info.get('title', '')}"
            except Exception:
                try:
                    sz = try_save_jpg(thumb.replace("http://", "https://"), out_path)
                    return sz, thumb, f"GoogleBooks: {info.get('title', '')}"
                except Exception as e:
                    print(f"    GBook thumb err: {e}")
    except Exception as e:
        print(f"    GBook API err: {e}")
    return None


def try_tiki_direct(query: str, must_words: list[str], ban_words: list[str], out_path: str):
    q = urllib.parse.quote(query)
    url = f"https://tiki.vn/api/v2/products?limit=15&q={q}"
    try:
        data = http_get_json(url)
        for item in data.get("data", []):
            name = (item.get("name") or "").lower()
            if any(b in name for b in ban_words):
                continue
            if all(w in name for w in must_words) and item.get("thumbnail_url"):
                u = item["thumbnail_url"].replace("/cache/280x280/", "/cache/750x750/")
                sz = try_save_jpg(u, out_path)
                return sz, u, f"Tiki: {item.get('name')}"
    except Exception as e:
        print(f"    Tiki err: {e}")
    return None


def main():
    manifest_path = os.path.join(OUT_DIR, "_manifest.json")
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)
    by_idx = {m["index"]: m for m in manifest}

    targets = [
        (2, "isbns", ["9780134757599", "9780201485677", "0201485672"], "Refactoring Martin Fowler"),
        (6, "isbns", ["9780133594140", "9780132856201", "9780136681557"], "Computer Networking A Top-Down Approach Kurose"),
        (12, "vn", (["số đỏ"], ["trúng số", "combo"]), "Số Đỏ Vũ Trọng Phụng"),
        (30, "vn", (["việt nam sử lược"], ["combo"]), "Việt Nam Sử Lược Trần Trọng Kim"),
        (31, "isbns", ["9780140455113", "9780486411217", "9780140449143"], "The Republic Plato Penguin Classics"),
        (32, "isbns", ["9780374292881", "9780312425074", "9780374292799"], "The World Is Flat Thomas Friedman"),
    ]

    fixed_entries = []
    for idx, kind, arg, gb_query in targets:
        entry = by_idx[idx]
        out_path = os.path.join(OUT_DIR, entry["filename"])
        print(f"[Fix #{idx:02d}] {entry['title']}")
        res = None
        if kind == "isbns":
            res = try_isbns(arg, out_path)
            if not res:
                res = try_google_books(gb_query, out_path)
        else:
            must_w, ban_w = arg
            res = try_tiki_direct(gb_query, must_w, ban_w, out_path)
            if not res:
                res = try_google_books(gb_query, out_path)

        if res:
            sz, url, src = res
            entry["source"] = src.split(":")[0]
            entry["matched"] = src
            entry["size"] = f"{sz[0]}x{sz[1]}"
            entry["url"] = url
            print(f"  OK ({sz[0]}x{sz[1]}): {src}")
        else:
            print(f"  !! FAILED #{idx}")
        fixed_entries.append(entry)

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)

    # Tạo ảnh đối chiếu riêng cho 6 cuốn vừa sửa
    cols, rows = 3, 2
    cell_w, cell_h = 280, 380
    canvas = Image.new("RGB", (cols * cell_w, rows * cell_h), (245, 246, 250))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 14)
        font_sm = ImageFont.truetype(r"C:\Windows\Fonts\arial.ttf", 12)
    except Exception:
        font = font_sm = ImageFont.load_default()

    for i, entry in enumerate(fixed_entries):
        r, c = i // cols, i % cols
        x0, y0 = c * cell_w, r * cell_h
        draw.rectangle([x0 + 6, y0 + 6, x0 + cell_w - 6, y0 + cell_h - 6], fill=(255, 255, 255), outline=(200, 205, 215))
        fp = os.path.join(OUT_DIR, entry["filename"])
        if os.path.exists(fp):
            im = Image.open(fp)
            im.thumbnail((220, 285), Image.Resampling.LANCZOS)
            canvas.paste(im, (x0 + (cell_w - im.width) // 2, y0 + 14 + (285 - im.height) // 2))
        lbl = f"#{entry['index']}: {entry['title']}"
        if len(lbl) > 36:
            lbl = lbl[:34] + "..."
        draw.text((x0 + 12, y0 + 310), lbl, fill=(20, 40, 70), font=font)
        draw.text((x0 + 12, y0 + 335), f"[{entry['source']}] {entry['size']}", fill=(100, 110, 125), font=font_sm)

    out_check = os.path.join(ANTIGRAVITY_DIR, "verify_fixed_6.jpg")
    canvas.save(out_check, "JPEG", quality=88)
    print(f"Saved: {out_check}")


if __name__ == "__main__":
    main()
