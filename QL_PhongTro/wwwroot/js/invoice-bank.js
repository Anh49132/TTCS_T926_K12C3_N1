(() => {
    document.querySelectorAll('[data-invoice-bank]').forEach(block => {
        const form = block.closest('form'), method = form.elements.hinhThucThanhToan;
        const message = block.querySelector('[data-bank-message]'), details = block.querySelector('[data-bank-details]');
        const image = block.querySelector('[data-bank-qr]'), copy = block.querySelector('[data-bank-copy]');
        let account = '', requestId = 0;
        image.addEventListener('error', () => { image.hidden = true; message.textContent = 'Chưa tải được ảnh QR. Bạn có thể dùng số tài khoản bên dưới hoặc liên hệ chủ nhà.'; });
        copy.addEventListener('click', async () => {
            try { await navigator.clipboard.writeText(account); message.textContent = 'Đã sao chép số tài khoản.'; }
            catch { message.textContent = 'Không thể sao chép tự động. Hãy chọn và sao chép số tài khoản bên dưới.'; }
        });
        const update = async () => {
            const current = ++requestId;
            block.hidden = method.value !== 'CHUYEN_KHOAN';
            if (block.hidden) return;
            details.hidden = true; image.hidden = true; image.removeAttribute('src'); account = '';
            const invoiceId = form.elements.hoaDonId.value;
            if (!/^\d+$/.test(invoiceId)) { message.textContent = 'Đang tải thông tin hóa đơn. Vui lòng thử lại.'; return; }
            message.textContent = 'Đang tải thông tin nhận chuyển khoản…';
            try {
                const response = await fetch(`/api/ngan-hang/hoa-don/${invoiceId}`, { credentials:'same-origin', cache:'no-store', headers:{ Accept:'application/json' } });
                if (!response.ok) throw new Error();
                const result = await response.json();
                if (current !== requestId) return;
                if (!result.bank) { message.textContent = 'Chủ nhà chưa cập nhật thông tin nhận chuyển khoản. Vui lòng liên hệ chủ nhà.'; return; }
                const bank = result.bank; account = bank.soTaiKhoan;
                block.querySelector('[data-bank-name]').textContent = bank.tenNganHang;
                block.querySelector('[data-bank-number]').textContent = account;
                message.textContent = bank.coQr ? '' : 'Chủ nhà chưa tải ảnh QR. Bạn có thể chuyển khoản theo số tài khoản bên dưới.';
                if (bank.coQr) { image.src = `/api/ngan-hang/hoa-don/${invoiceId}/qr?v=${encodeURIComponent(bank.phienBan)}`; image.hidden = false; }
                details.hidden = false;
            } catch { if (current === requestId) message.textContent = 'Không thể tải thông tin ngân hàng. Vui lòng thử lại hoặc liên hệ chủ nhà.'; }
        };
        form.querySelectorAll('[name=hinhThucThanhToan]').forEach(input => input.addEventListener('change', update));
        // Refresh current configuration each time the payment form is opened.
        form.addEventListener('bank:open', update);
        update();
    });
})();
