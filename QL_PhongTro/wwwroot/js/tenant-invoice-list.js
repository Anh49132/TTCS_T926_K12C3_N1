(() => {
    const page = document.querySelector(".tenant-invoice-list");
    if (!page) return;

    const form = page.querySelector(".tenant-invoice-filters");
    const loading = document.getElementById("tenant-invoice-list-loading");
    const error = document.getElementById("tenant-invoice-list-error");
    const retry = document.getElementById("tenant-invoice-list-retry");
    const empty = document.getElementById("tenant-invoice-list-empty");
    const noMatch = document.getElementById("tenant-invoice-list-no-match");
    const results = document.getElementById("tenant-invoice-list-results");
    const count = document.getElementById("tenant-invoice-list-count");
    const rows = document.getElementById("tenant-invoice-list-rows");
    const pagination = document.getElementById("tenant-invoice-list-pagination");
    const money = new Intl.NumberFormat("vi-VN");
    const params = new URLSearchParams(window.location.search);
    const selectedPage = Number(page.dataset.page || params.get("page") || 1);
    let currentPage = selectedPage;
    const apiUrl = new URL(page.dataset.listUrl, window.location.origin);
    const statusLabels = {
        CHUA_THANH_TOAN: "Chưa thanh toán",
        THANH_TOAN_MOT_PHAN: "Thanh toán một phần",
        DA_THANH_TOAN: "Đã thanh toán",
        QUA_HAN: "Quá hạn",
        CHO_XAC_NHAN: "Chờ xác nhận"
    };

    const invoiceUrl = (invoice) => {
        const url = new URL(page.dataset.detailUrl, window.location.origin);
        url.searchParams.set("maHoaDon", invoice.maHoaDon);
        const period = form.elements.namedItem("ky").value;
        const status = form.elements.namedItem("trangThai").value;
        if (period) url.searchParams.set("ky", period);
        if (status !== "TAT_CA") url.searchParams.set("trangThai", status);
        if (currentPage > 1) url.searchParams.set("page", currentPage);
        return url.toString();
    };

    const setError = (message, canRetry = true) => {
        loading.hidden = true;
        results.hidden = true;
        empty.hidden = true;
        noMatch.hidden = true;
        error.textContent = message;
        error.hidden = false;
        if (retry) retry.hidden = !canRetry;
    };

    const buildPagination = (current, pages) => {
        pagination.replaceChildren();
        if (pages <= 1) return;
        const list = document.createElement("div");
        list.className = "tenant-invoice-pagination";
        const addLink = (label, target, disabled, accessibleLabel) => {
            const link = document.createElement(disabled ? "span" : "a");
            link.className = `btn ${disabled ? "btn-outline-secondary disabled" : "btn-outline-dark"}`;
            link.textContent = label;
            if (disabled) {
                link.setAttribute("aria-disabled", "true");
            } else {
                const url = new URL(page.dataset.pageUrl, window.location.origin);
                const period = form.elements.namedItem("ky").value;
                const status = form.elements.namedItem("trangThai").value;
                if (period) url.searchParams.set("ky", period);
                if (status !== "TAT_CA") url.searchParams.set("trangThai", status);
                url.searchParams.set("page", target);
                link.href = url.toString();
            }
            link.setAttribute("aria-label", accessibleLabel);
            list.append(link);
        };
        addLink("Trước", current - 1, current <= 1, "Trang trước");
        const indicator = document.createElement("span");
        indicator.setAttribute("aria-current", "page");
        indicator.textContent = `Trang ${current} / ${pages}`;
        list.append(indicator);
        addLink("Sau", current + 1, current >= pages, "Trang sau");
        pagination.append(list);
    };

    const render = (data) => {
        currentPage = data.trang;
        loading.hidden = true;
        error.hidden = true;
        results.hidden = true;
        empty.hidden = true;
        noMatch.hidden = true;
        rows.replaceChildren();
        if (data.tongSoHoaDon === 0) {
            empty.hidden = false;
            return;
        }
        if (data.hoaDons.length === 0) {
            noMatch.hidden = false;
            return;
        }

        for (const invoice of data.hoaDons) {
            const row = document.createElement("tr");
            row.tabIndex = 0;
            row.setAttribute("role", "link");
            row.setAttribute("aria-label", `Mở hóa đơn ${invoice.maHoaDon}, kỳ ${invoice.thang}/${invoice.nam}`);
            row.dataset.href = invoiceUrl(invoice);
            const code = document.createElement("th");
            code.scope = "row";
            const codeLink = document.createElement("a");
            codeLink.href = row.dataset.href;
            codeLink.textContent = invoice.maHoaDon;
            code.append(codeLink);
            const period = document.createElement("td");
            period.textContent = `${String(invoice.thang).padStart(2, "0")}/${invoice.nam}`;
            const total = document.createElement("td");
            total.className = "text-end";
            total.textContent = `${money.format(invoice.tongCong)} đ`;
            const remaining = document.createElement("td");
            remaining.className = "text-end";
            remaining.textContent = `${money.format(invoice.soConPhaiTra)} đ`;
            const due = document.createElement("td");
            due.textContent = invoice.hanThanhToan;
            const status = document.createElement("td");
            const badge = document.createElement("span");
            badge.className = `badge ${invoice.trangThai === "CHO_XAC_NHAN" ? "text-bg-warning" : invoice.quaHan ? "text-bg-danger" : invoice.trangThai === "DA_THANH_TOAN" ? "text-bg-success" : invoice.trangThai === "THANH_TOAN_MOT_PHAN" ? "text-bg-warning" : "text-bg-secondary"}`;
            badge.textContent = statusLabels[invoice.trangThai] || invoice.tenTrangThai;
            status.append(badge);
            if (invoice.quaHan) {
                const overdue = document.createElement("span");
                overdue.className = "tenant-invoice-overdue";
                overdue.setAttribute("role", "status");
                overdue.textContent = `Quá hạn ${invoice.soNgayTre} ngày`;
                status.append(overdue);
            }
            row.append(code, period, total, remaining, due, status);
            row.addEventListener("click", (event) => {
                if (event.target.closest("a")) return;
                window.location.assign(row.dataset.href);
            });
            row.addEventListener("keydown", (event) => {
                if (event.key === "Enter") window.location.assign(row.dataset.href);
            });
            rows.append(row);
        }
        const hasFilter = form.elements.namedItem("ky").value || form.elements.namedItem("trangThai").value !== "TAT_CA";
        count.textContent = hasFilter
            ? `Có ${data.soKetQua} hóa đơn khớp bộ lọc.`
            : `Có ${data.tongSoHoaDon} hóa đơn của bạn.`;
        buildPagination(data.trang, data.soTrang);
        results.hidden = false;
    };

    form.addEventListener("submit", () => {
        const submit = form.querySelector('button[type="submit"]');
        if (submit) submit.disabled = true;
    });

    const load = async () => {
        if (retry) { retry.disabled = true; retry.hidden = true; }
        error.hidden = true;
        loading.hidden = false;
        try {
            const period = params.get("ky") || "";
            const status = params.get("trangThai") || "TAT_CA";
            apiUrl.searchParams.set("ky", period);
            apiUrl.searchParams.set("trangThai", status);
            apiUrl.searchParams.set("page", selectedPage);
            const response = await fetch(apiUrl, {
                headers: { Accept: "application/json" },
                cache: "no-store",
                credentials: "same-origin"
            });
            if (!response.ok) {
                const body = await response.json().catch(() => null);
                setError(body?.message || "Không thể tải danh sách hóa đơn. Vui lòng thử lại.", response.status !== 401 && response.status !== 403 && response.status !== 404);
                return;
            }
            render(await response.json());
        } catch (requestError) {
            console.error("Không tải được danh sách hóa đơn.", requestError);
            setError("Không thể tải danh sách hóa đơn. Vui lòng thử lại.");
        } finally {
            if (retry) retry.disabled = false;
        }
    };
    retry?.addEventListener("click", load);
    load();
})();
