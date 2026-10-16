(() => {
    const root = document.querySelector('[data-bank-settings]');
    if (!root) return;
    const form = root.querySelector('[data-bank-form]'), read = root.querySelector('[data-bank-read]');
    const status = root.querySelector('[data-bank-status]'), edit = root.querySelector('[data-bank-edit]');
    const preview = root.querySelector('[data-bank-preview]'), qr = root.querySelector('[data-bank-qr]');
    const file = form.elements.AnhQr;
    form.noValidate = true;
    let bank = null, objectUrl = null, busy = false;
    const clearPreview = () => { if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = null; preview.removeAttribute('src'); preview.hidden = true; };
    const render = () => {
        read.hidden = false; form.hidden = true; edit.disabled = false;
        root.querySelector('[data-bank-name]').textContent = bank?.tenNganHang || 'Chưa cập nhật';
        root.querySelector('[data-bank-number]').textContent = bank?.soTaiKhoan || 'Chưa cập nhật';
        qr.hidden = !bank?.coQr; qr.removeAttribute('src');
        if (bank?.coQr) qr.src = `/api/ngan-hang/cua-toi/qr?v=${encodeURIComponent(bank.phienBan)}`;
        clearPreview();
    };
    const errors = (values = {}) => {
        root.querySelectorAll('[data-bank-error]').forEach(el => { el.textContent = (values[el.dataset.bankError] || []).join(' '); });
        ['TenNganHang','SoTaiKhoan','AnhQr'].forEach(name => form.elements[name].setAttribute('aria-invalid', values[name] ? 'true' : 'false'));
    };
    edit.addEventListener('click', () => {
        errors(); form.reset(); clearPreview();
        form.elements.TenNganHang.value = bank?.tenNganHang || '';
        form.elements.SoTaiKhoan.value = bank?.soTaiKhoan || '';
        form.elements.PhienBan.value = bank?.phienBan || '';
        form.elements.XoaQr.value = 'false';
        if (bank?.coQr) { preview.src = qr.src; preview.hidden = false; }
        read.hidden = true; form.hidden = false; status.textContent = ''; form.elements.TenNganHang.focus();
    });
    root.querySelector('[data-bank-cancel]').addEventListener('click', () => { if (!busy) { render(); status.textContent = ''; } });
    root.querySelector('[data-bank-replace]').addEventListener('click', () => file.click());
    root.querySelector('[data-bank-remove]').addEventListener('click', () => {
        file.value = ''; file.setCustomValidity(''); form.elements.XoaQr.value = 'true'; clearPreview(); errors();
    });
    file.addEventListener('change', () => {
        clearPreview(); file.setCustomValidity('');
        const image = file.files[0];
        if (!image) { if (bank?.coQr && form.elements.XoaQr.value !== 'true') { preview.src = qr.src; preview.hidden = false; } return; }
        const error = image.size > 5 * 1024 * 1024 ? 'Ảnh QR tối đa 5 MiB.' : !/\.(jpe?g|png)$/i.test(image.name) ? 'Chỉ nhận ảnh JPG/JPEG hoặc PNG.' : '';
        errors(error ? { AnhQr: [error] } : {}); file.setCustomValidity(error);
        if (!error) { objectUrl = URL.createObjectURL(image); preview.src = objectUrl; preview.hidden = false; form.elements.XoaQr.value = 'false'; }
    });
    [qr,preview].forEach(image => image.addEventListener('error', () => { image.hidden = true; status.textContent = 'Không thể tải ảnh QR. Bạn có thể chọn thay ảnh.'; }));
    form.addEventListener('submit', async event => {
        event.preventDefault(); if (busy) return;
        const invalid = {};
        if (!form.elements.TenNganHang.value.trim()) invalid.TenNganHang = ['Vui lòng nhập tên ngân hàng.'];
        if (!/^[A-Za-z0-9]{1,50}$/.test(form.elements.SoTaiKhoan.value.trim())) invalid.SoTaiKhoan = ['Nhập số tài khoản từ 1 đến 50 chữ cái hoặc chữ số, không có khoảng trắng.'];
        if (!file.checkValidity()) invalid.AnhQr = [file.validationMessage];
        if (Object.keys(invalid).length) { errors(invalid); form.elements[Object.keys(invalid)[0]].focus(); return; }
        busy = true; errors();
        const payload = new FormData(form);
        form.querySelectorAll('button,input').forEach(el => el.disabled = true);
        try {
            const response = await fetch('/api/ngan-hang/cua-toi', { method:'POST', body:payload, credentials:'same-origin', cache:'no-store', headers:{ Accept:'application/json' } });
            const result = await response.json();
            if (!response.ok) { errors(result.errors || { '': ['Không thể lưu thông tin. Vui lòng đăng nhập lại hoặc tải lại trang.'] }); return; }
            bank = result.bank; render(); status.textContent = 'Đã cập nhật thông tin nhận chuyển khoản.';
        } catch { errors({ '': ['Không thể kết nối. Hãy tải lại thông tin trước khi thử lưu lại.'] }); }
        finally { busy = false; form.querySelectorAll('button,input').forEach(el => el.disabled = false); }
    });
    fetch('/api/ngan-hang/cua-toi', { credentials:'same-origin', cache:'no-store', headers:{ Accept:'application/json' } })
        .then(response => { if (!response.ok) throw new Error(); return response.json(); })
        .then(result => { bank = result.bank; render(); status.textContent = bank ? '' : 'Bạn chưa cập nhật thông tin nhận chuyển khoản.'; })
        .catch(() => { status.textContent = 'Không thể tải thông tin ngân hàng. Vui lòng tải lại trang.'; });
})();
