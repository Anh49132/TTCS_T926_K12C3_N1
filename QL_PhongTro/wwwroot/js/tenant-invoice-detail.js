(() => {
    const page = document.querySelector(".tenant-invoice-detail");
    if (!page) return;

    const loading = document.getElementById("tenant-invoice-loading");
    const error = document.getElementById("tenant-invoice-error");
    const retry = document.getElementById("tenant-invoice-retry");
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
    const reportMethod = document.getElementById("tenant-payment-report-method");
    const reportProof = document.getElementById("tenant-payment-report-proof");
    const reportProofError = document.getElementById("tenant-payment-report-proof-error");
    const reportProofRequirement = document.getElementById("tenant-payment-report-proof-requirement");
    const pendingReport = document.getElementById("tenant-payment-report-pending");
    const cancelReportForm = document.getElementById("tenant-payment-report-cancel-form");
    const cancelReportInvoiceId = document.getElementById("tenant-payment-report-cancel-invoice-id");
    const cancelReportButton = document.getElementById("tenant-payment-report-cancel-button");
    const vnd = new Intl.NumberFormat("vi-VN");

    const updateProofRequirement = () => {
        const required = reportMethod.value === "CHUYEN_KHOAN";
        reportProof.setAttribute("aria-required", String(required));
        reportProofRequirement.textContent = required ? "(bắt buộc khi chuyển khoản)" : "(không bắt buộc)";
        if (!reportProof.files.length) reportProofError.hidden = true;
    };

    const validateProof = () => {
        reportProofError.hidden = true;
        if (!reportProof.files.length) {
            if (reportMethod.value !== "CHUYEN_KHOAN") return true;
            reportProofError.textContent = "Vui lòng tải ảnh chứng từ khi chọn Chuyển khoản.";
            reportProofError.hidden = false;
            return false;
        }
        const file = reportProof.files[0];
        const extension = file.name.split(".").pop().toLowerCase();
        const acceptedExtension = ["jpg", "jpeg", "png"].includes(extension);
        if (!acceptedExtension) reportProofError.textContent = "Chỉ nhận ảnh JPG/JPEG hoặc PNG.";
        else if (file.size > 5 * 1024 * 1024) reportProofError.textContent = "Ảnh chứng từ vượt quá dung lượng tối đa 5 MiB (5.242.880 byte).";
        else return true;
        reportProofError.hidden = false;
        return false;
    };

    reportMethod.addEventListener("change", updateProofRequirement);
    updateProofRequirement();
    reportProof.addEventListener("change", validateProof);
    reportForm.addEventListener("submit", event => {
        if (!validateProof()) {
            event.preventDefault();
            reportProof.focus();
        }
    });
    cancelReportButton.addEventListener("click", event => {
        if (!window.confirm("Bạn có chắc muốn hủy báo cáo thanh toán này?")) event.preventDefault();
    });

    const cell = (value, className) => {
        const element = document.createElement("td");
        element.textContent = value;
        if (className) element.className = className;
        return element;
    };

    const showError = (message, canRetry = true) => {
        loading.hidden = true;
        content.hidden = true;
        error.textContent = message;
        error.hidden = false;
        if (retry) retry.hidden = !canRetry;
    };

    const load = async () => {
        if (retry) { retry.disabled = true; retry.hidden = true; }
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
                showError("Không tìm thấy hóa đơn.", false);
                return;
            }
            if (response.status === 401 || response.status === 403) {
                showError("Phiên đăng nhập đã hết hạn hoặc bạn không có quyền xem hóa đơn. Vui lòng đăng nhập lại.", false);
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
            cancelReportForm.hidden = !invoice.choXacNhan;
            cancelReportInvoiceId.value = invoice.hoaDonId;
            if (invoice.choPhepBaoCaoThanhToan) {
                reportInvoiceId.value = invoice.hoaDonId;
                reportAmount.max = invoice.soConPhaiTra;
                reportDate.value = invoice.homNay;
                reportLimit.textContent = `Nhập số nguyên đồng, tối đa ${vnd.format(invoice.soConPhaiTra)} đ còn phải trả.`;
            }
            pendingReport.hidden = !invoice.choXacNhan;
            if (invoice.choXacNhan) {
                const method = invoice.hinhThucBaoCaoChoXacNhan === "TIEN_MAT" ? "Tiền mặt" : "Chuyển khoản";
                pendingReport.replaceChildren();
                const summary = document.createElement("p");
                summary.className = "mb-2";
                summary.textContent = `Báo cáo thanh toán ${vnd.format(invoice.soTienBaoCaoChoXacNhan)} đ, ngày ${invoice.ngayBaoCaoChoXacNhan}, hình thức ${method} đang chờ xác nhận. Bạn không thể sửa báo cáo đã gửi.`;
                pendingReport.append(summary, cancelReportForm);
            }
            loading.hidden = true;
            content.hidden = false;
        } catch {
            showError("Không thể tải hóa đơn. Vui lòng thử lại.");
        } finally {
            if (retry) retry.disabled = false;
        }
    };

    retry?.addEventListener("click", load);
    load();
})();
