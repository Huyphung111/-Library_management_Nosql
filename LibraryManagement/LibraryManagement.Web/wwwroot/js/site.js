// Nút ☰ thu gọn / mở sidebar (giống #sidebarCollapse của Shop-Flower).
// Máy tính: ẩn hẳn sidebar, nhớ lựa chọn. Màn hình nhỏ: sidebar trượt ra đè lên nội dung.
(function () {
    const body = document.body;
    const mobile = window.matchMedia("(max-width: 991.98px)");
    const storageKey = "sidebarCollapsed";

    try {
        if (localStorage.getItem(storageKey) === "1") body.classList.add("sidebar-collapsed");
    } catch { /* trình duyệt chặn localStorage: bỏ qua */ }

    document.querySelectorAll("[data-sidebar-toggle]").forEach(el =>
        el.addEventListener("click", () => {
            if (mobile.matches) {
                body.classList.toggle("sidebar-open");
                return;
            }
            const collapsed = body.classList.toggle("sidebar-collapsed");
            try { localStorage.setItem(storageKey, collapsed ? "1" : "0"); } catch { }
        }));

    // Bấm vào một mục menu trên màn hình nhỏ thì đóng sidebar
    document.querySelectorAll(".sidebar .nav-link").forEach(link =>
        link.addEventListener("click", () => body.classList.remove("sidebar-open")));
})();
