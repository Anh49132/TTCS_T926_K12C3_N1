(() => {
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
