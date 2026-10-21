(() => {
    document.querySelectorAll("[data-payment-report]").forEach(form => {
        const method = form.elements.hinhThucThanhToan;
        const proof = form.elements.anhChungTu;
        const validate = () => {
            proof.required = method.value === "CHUYEN_KHOAN";
            const marker = form.querySelector('[data-proof-required]');
            if (marker) marker.hidden = !proof.required;
            proof.setCustomValidity(proof.files[0]?.size > 5 * 1024 * 1024
                ? "Ảnh chứng từ tối đa 5 MiB." : "");
        };
        form.querySelectorAll('[name=hinhThucThanhToan]').forEach(input => input.addEventListener('change', validate));
        proof.addEventListener("change", validate);
        form.addEventListener("submit", event => {
            validate();
            if (!form.reportValidity()) event.preventDefault();
            else form.querySelector("button[type=submit]").disabled = true;
        });
        validate();
    });
    document.querySelectorAll("[data-payment-proof-link]").forEach(button => {
        button.addEventListener("click", async () => {
            const error = button.parentElement.querySelector("[data-payment-proof-error]");
            const image = button.parentElement.querySelector("#ic-payment-proof-image");
            error.hidden = true;
            button.disabled = true;
            try {
                const response = await fetch(button.dataset.paymentProofLink, {
                    headers: { Accept: "application/json" },
                    credentials: "same-origin",
                    cache: "no-store"
                });
                if (!response.ok) throw new Error("Không thể cấp liên kết xem ảnh chứng từ. Hãy tải lại trang hoặc đăng nhập lại.");
                const link = await response.json();
                image.src = link.url;
                image.hidden = false;
            } catch (failure) {
                error.textContent = failure.message || "Không thể tải ảnh chứng từ.";
                error.hidden = false;
            } finally {
                button.disabled = false;
            }
        });
    });
})();
