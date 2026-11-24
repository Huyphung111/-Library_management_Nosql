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

// Tự động đóng banner thông báo thành công sau 3.5 giây
document.addEventListener("DOMContentLoaded", function () {
    setTimeout(function () {
        document.querySelectorAll(".alert.alert-success").forEach(function (alertEl) {
            if (window.bootstrap && bootstrap.Alert) {
                bootstrap.Alert.getOrCreateInstance(alertEl).close();
            } else {
                alertEl.remove();
            }
        });
    }, 3500);
});

// Ô tích "Hiện mật khẩu": data-show-password chứa selector các ô mật khẩu cần hiện (Đăng nhập, Đổi mật khẩu).
// Khi gửi form thì đổi lại thành ô mật khẩu, để trình duyệt không lưu mật khẩu vào lịch sử nhập liệu như chữ thường.
document.querySelectorAll("[data-show-password]").forEach(function (box) {
    const inputs = document.querySelectorAll(box.dataset.showPassword);
    const apply = function (show) { inputs.forEach(function (i) { i.type = show ? "text" : "password"; }); };
    box.addEventListener("change", function () { apply(box.checked); });
    const form = box.closest("form");
    if (form) form.addEventListener("submit", function () { apply(false); box.checked = false; });
});
