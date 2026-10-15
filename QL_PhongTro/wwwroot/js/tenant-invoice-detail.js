(() => {
    const page = document.querySelector(".tenant-invoice-detail");
    if (!page) return;

    const loading = document.getElementById("tenant-invoice-loading");
    const error = document.getElementById("tenant-invoice-error");
    const content = document.getElementById("tenant-invoice-content");
    const body = document.querySelector("#tenant-invoice-lines tbody");
    const period = document.getElementById("tenant-invoice-period");
    const room = document.getElementById("tenant-invoice-room");
    const total = document.getElementById("tenant-invoice-total");
    const paid = document.getElementById("tenant-invoice-paid");
    const remaining = document.getElementById("tenant-invoice-remaining");
    const due = document.getElementById("tenant-invoice-due");
    const overdue = document.getElementById("tenant-invoice-overdue");
    const reportForm = document.getElementById("tenant-payment-report-form");
    const reportInvoiceId = document.getElementById("tenant-payment-report-invoice-id");
    const reportAmount = document.getElementById("tenant-payment-report-amount");
    const reportLimit = document.getElementById("tenant-payment-report-limit");
    const reportDate = document.getElementById("tenant-payment-report-date");
    const pendingReport = document.getElementById("tenant-payment-report-pending");
    const vnd = new Intl.NumberFormat("vi-VN");

    const cell = (value, className) => {
        const element = document.createElement("td");
        element.textContent = value;
        if (className) element.className = className;
        return element;
    };

    const showError = (message) => {
        loading.hidden = true;
        content.hidden = true;
        error.textContent = message;
        error.hidden = false;
    };

    const load = async () => {
        loading.hidden = false;
        content.hidden = true;
        error.hidden = true;
        try {
            const response = await fetch(page.dataset.invoiceUrl, {
                headers: { Accept: "application/json" },
                cache: "no-store",
                credentials: "same-origin"
            });
            if (response.status === 404) {
                showError("Không tìm thấy hóa đơn.");
                return;
            }
            if (!response.ok) throw new Error("Invoice request failed.");

            const invoice = await response.json();
            period.textContent = `${invoice.thang}/${invoice.nam}`;
            room.textContent = `Phòng ${invoice.maPhong}`;
            body.replaceChildren();
            for (const line of invoice.chiTiet) {
                const row = document.createElement("tr");
                const unit = line.donViTinh ? ` ${line.donViTinh}` : "";
                row.append(
                    cell(line.tenKhoan),
                    cell(line.chiSoDau === null ? "—" : `${line.chiSoDau}${unit}`),
                    cell(line.chiSoCuoi === null ? "—" : `${line.chiSoCuoi}${unit}`),
                    cell(`${line.soLuongTieuThu}${unit}`),
                    cell(`${line.donGia} đ${line.donViTinh ? `/${line.donViTinh}` : ""}`, "text-end"),
                    cell(`${line.loaiKhoan === "GIAM_TRU" ? "−" : ""}${line.thanhTien} đ`, "text-end fw-semibold")
                );
                body.append(row);
            }
            total.textContent = `${vnd.format(invoice.tongCong)} đ`;
            paid.textContent = `${vnd.format(invoice.soDaThanhToan)} đ`;
            remaining.textContent = `${vnd.format(invoice.soConPhaiTra)} đ`;
            due.textContent = invoice.hanThanhToan;
            overdue.textContent = invoice.quaHan ? `Quá hạn ${invoice.soNgayTre} ngày` : "";
            overdue.hidden = !invoice.quaHan;
            reportForm.hidden = !invoice.choPhepBaoCaoThanhToan;
            if (invoice.choPhepBaoCaoThanhToan) {
                reportInvoiceId.value = invoice.hoaDonId;
                reportAmount.max = invoice.soConPhaiTra;
                reportDate.value = invoice.homNay;
                reportLimit.textContent = `Nhập số nguyên đồng, tối đa ${vnd.format(invoice.soConPhaiTra)} đ còn phải trả.`;
            }
            pendingReport.hidden = !invoice.choXacNhan;
            if (invoice.choXacNhan) {
                const method = invoice.hinhThucBaoCaoChoXacNhan === "TIEN_MAT" ? "Tiền mặt" : "Chuyển khoản";
                pendingReport.textContent = `Báo cáo thanh toán ${vnd.format(invoice.soTienBaoCaoChoXacNhan)} đ, ngày ${invoice.ngayBaoCaoChoXacNhan}, hình thức ${method} đang chờ xác nhận. Bạn không thể sửa báo cáo đã gửi.`;
            }
            loading.hidden = true;
            content.hidden = false;
        } catch {
            showError("Không thể tải hóa đơn. Vui lòng thử lại.");
        }
    };

    load();
})();
