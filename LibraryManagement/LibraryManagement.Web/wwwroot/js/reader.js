// Phía Độc giả (nạp trong _Layout khi đăng nhập là Độc giả):
//  1. libToast(msg, isError): thông báo nhỏ góc dưới phải.
//  2. Nút yêu thích: phần tử có [data-fav-toggle][data-book-id], aria-pressed="true|false".
//     Gửi POST /MyFavorites/Add hoặc /Remove, cập nhật mọi nút cùng sách, số trong menu tài khoản (#ddFavCount),
//     rồi phát sự kiện "fav:changed" (detail: { bookId, favorite, count }) để từng trang xử lý thêm.
//  3. Đặt giữ / đặt trước dạng hộp: link a[data-reserve] (href = /MyReservations/Create?bookId=...) mở hộp xác nhận
//     thay vì chuyển trang. Không có JS thì link vẫn mở trang xác nhận như cũ.
(function () {
  const AJAX_HEADERS = { "X-Requested-With": "XMLHttpRequest" };
  const token = () => document.querySelector("#libAfToken input[name='__RequestVerificationToken']")?.value ?? "";

  // ---------- 1. Toast ----------
  let toastEl, toastTimer;
  window.libToast = function (msg, isError) {
    if (!toastEl) {
      toastEl = document.createElement("div");
      toastEl.className = "toast-msg";
      toastEl.setAttribute("role", "status");
      document.body.appendChild(toastEl);
    }
    toastEl.textContent = msg;
    toastEl.classList.toggle("error", !!isError);
    toastEl.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toastEl.classList.remove("show"), 2400);
  };

  // ---------- 2. Yêu thích ----------
  function applyFavState(bookId, favorite) {
    document.querySelectorAll(`[data-fav-toggle][data-book-id="${CSS.escape(bookId)}"]`).forEach(btn => {
      btn.setAttribute("aria-pressed", favorite ? "true" : "false");
      const icon = btn.querySelector("[data-fav-icon]");
      if (icon) {
        icon.classList.toggle("bi-heart-fill", favorite);
        icon.classList.toggle("bi-heart", !favorite);
      }
      const scope = btn.closest("[data-fav-scope]");
      if (scope) {
        scope.classList.toggle("is-fav", favorite);
        scope.querySelectorAll("[data-label-on]").forEach(l => {
          l.textContent = favorite ? l.dataset.labelOn : l.dataset.labelOff;
        });
      }
    });
  }

  document.addEventListener("click", async e => {
    const btn = e.target.closest("[data-fav-toggle]");
    if (!btn) return;
    e.preventDefault();
    e.stopPropagation();
    if (btn.disabled) return;

    const bookId = btn.dataset.bookId;
    const title = btn.dataset.bookTitle || "Sách";
    const willFav = btn.getAttribute("aria-pressed") !== "true";
    const body = new FormData();
    body.append("bookId", bookId);
    body.append("__RequestVerificationToken", token());

    btn.disabled = true;
    try {
      const res = await fetch(`/MyFavorites/${willFav ? "Add" : "Remove"}`, { method: "POST", headers: AJAX_HEADERS, body });
      const data = await res.json().catch(() => ({}));
      if (!res.ok || !data.success) {
        libToast(data.message || "Không cập nhật được sách yêu thích, vui lòng thử lại.", true);
        return;
      }
      applyFavState(bookId, willFav);
      const counter = document.getElementById("ddFavCount");
      if (counter && typeof data.count === "number") counter.textContent = data.count;
      libToast(willFav ? `Đã thêm "${title}" vào danh sách yêu thích` : `Đã xóa "${title}" khỏi danh sách yêu thích`);
      document.dispatchEvent(new CustomEvent("fav:changed", { detail: { bookId, favorite: willFav, count: data.count } }));
    } catch {
      libToast("Mất kết nối tới máy chủ, vui lòng thử lại.", true);
    } finally {
      btn.disabled = false;
    }
  });

  // ---------- 3. Hộp xác nhận đặt giữ / đặt trước ----------
  let modalEl, modal;
  function ensureModal() {
    if (modal) return;
    modalEl = document.createElement("div");
    modalEl.className = "modal fade";
    modalEl.id = "reserveModal";
    modalEl.tabIndex = -1;
    modalEl.setAttribute("aria-labelledby", "reserveModalTitle");
    modalEl.setAttribute("aria-hidden", "true");
    modalEl.innerHTML = `
      <div class="modal-dialog modal-dialog-centered modal-lg">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="reserveModalTitle">Xác nhận</h5>
            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
          </div>
          <div class="modal-body"></div>
        </div>
      </div>`;
    document.body.appendChild(modalEl);
    modal = new bootstrap.Modal(modalEl);
  }

  document.addEventListener("click", async e => {
    const link = e.target.closest("a[data-reserve]");
    if (!link || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;
    e.preventDefault();

    ensureModal();
    const bodyEl = modalEl.querySelector(".modal-body");
    modalEl.querySelector(".modal-title").textContent = "Đang tải...";
    bodyEl.innerHTML = `<div class="reserve-loading"><div class="spinner-border spinner-border-sm me-2"></div>Đang tải thông tin sách...</div>`;
    modal.show();

    try {
      const res = await fetch(link.href, { headers: AJAX_HEADERS });
      if (!res.ok) throw new Error();
      bodyEl.innerHTML = await res.text();
      const root = bodyEl.querySelector("[data-reserve-title]");
      modalEl.querySelector(".modal-title").textContent = root ? root.dataset.reserveTitle : "Xác nhận";
    } catch {
      modal.hide();
      libToast("Không tải được thông tin sách, vui lòng thử lại.", true);
    }
  });

  // Bấm "Xác nhận" trong hộp: gửi ngay, thành công thì tải lại trang (số sách còn, thông báo được cập nhật)
  document.addEventListener("submit", async e => {
    const form = e.target.closest("form[data-reserve-form]");
    if (!form || !modalEl?.contains(form)) return;
    e.preventDefault();

    const submitBtn = form.querySelector("[type=submit]");
    const errorBox = modalEl.querySelector("[data-reserve-error]");
    submitBtn.disabled = true;
    try {
      const res = await fetch(form.action, { method: "POST", headers: AJAX_HEADERS, body: new FormData(form) });
      const data = await res.json().catch(() => ({}));
      if (res.ok && data.success) {
        window.location.reload();
        return;
      }
      if (errorBox) {
        errorBox.textContent = data.message || "Không gửi được yêu cầu, vui lòng thử lại.";
        errorBox.classList.remove("d-none");
      }
    } catch {
      if (errorBox) {
        errorBox.textContent = "Mất kết nối tới máy chủ, vui lòng thử lại.";
        errorBox.classList.remove("d-none");
      }
    }
    submitBtn.disabled = false;
  });
})();
