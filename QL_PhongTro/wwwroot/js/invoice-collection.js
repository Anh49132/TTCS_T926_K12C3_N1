(() => {
    'use strict';
    const page = document.getElementById('invoice-collection');
    if (!page) return;
    const form = document.getElementById('ic-form'), dialog = document.getElementById('ic-dialog');
    const open = document.getElementById('ic-open');
    const money = value => new Intl.NumberFormat('vi-VN', {maximumFractionDigits: 0}).format(value) + ' đ';
    const date = value => value ? value.slice(0, 10).split('-').reverse().join('/') : 'Chưa có thông tin';
    const method = value => ({TIEN_MAT:'Tiền mặt', CHUYEN_KHOAN:'Chuyển khoản', KHAC:'Khác'})[value] || 'Chưa có thông tin';
    let busy = false, uncertain = false, submitted = null, returnFocus = null;
    function render(summary) {
        page.querySelectorAll('[data-ic-total]').forEach(e => e.textContent = money(summary.tongHoaDon));
        page.querySelectorAll('[data-ic-paid]').forEach(e => e.textContent = money(summary.tongDaThu));
        page.querySelectorAll('[data-ic-remaining]').forEach(e => e.textContent = money(summary.conPhaiTra));
        page.querySelectorAll('[data-ic-status]').forEach(e => {
            e.textContent = summary.tenTrangThai;
            e.className = 'ic-status ' + (summary.trangThai === 'DA_THANH_TOAN' ? 'paid' : summary.trangThai === 'THANH_TOAN_MOT_PHAN' ? 'partial' : summary.trangThai === 'DA_HUY' ? 'canceled' : 'unpaid');
        });
        if (open) open.hidden = !summary.choPhepGhiThu;
        if (form) form.dataset.remaining = String(summary.conPhaiTra);
        const ratio = Math.max(0, Math.min(100, summary.tongHoaDon > 0 ? summary.tongDaThu / summary.tongHoaDon * 100 : 0));
        const progress = document.getElementById('ic-progress-fill');
        progress.style.width = ratio + '%'; progress.textContent = Math.round(ratio) + '%';
        progress.parentElement.setAttribute('aria-valuenow', String(Math.round(ratio)));
        const rows = document.getElementById('ic-history-body'); rows.replaceChildren();
        summary.lanThus.forEach((receipt, index) => {
            const row = document.createElement('tr');
            const time = receipt.thoiDiemGhi ? new Intl.DateTimeFormat('vi-VN', {timeZone:'Asia/Ho_Chi_Minh', day:'2-digit', month:'2-digit', year:'numeric', hour:'2-digit', minute:'2-digit', hourCycle:'h23'}).format(new Date(receipt.thoiDiemGhi.replace(/Z$/, '') + 'Z')) : 'Chưa có thông tin';
            [index + 1, money(receipt.soTien), date(receipt.ngayThu), method(receipt.hinhThuc), receipt.ghiChu || '—', receipt.nguoiXacNhan, time].forEach((value, i) => {
                const cell = document.createElement('td'); cell.textContent = String(value);
                if (i === 1) cell.className = 'money';
                if (i === 3) { const badge = document.createElement('span'); badge.className = 'ic-method'; badge.textContent = String(value); cell.replaceChildren(badge); }
                row.append(cell);
            }); rows.append(row);
        });
        document.getElementById('ic-count').textContent = '(' + summary.lanThus.length + ')';
        document.getElementById('ic-empty').hidden = summary.lanThus.length !== 0;
        document.getElementById('ic-history').hidden = summary.lanThus.length === 0;
    }
    document.querySelector('#ic-success button')?.addEventListener('click', () => document.getElementById('ic-success').hidden = true);
    if (!form || !dialog || !open) return;
    const amount = form.elements.SoTien, submit = document.getElementById('ic-submit');
    function errors(values) {
        form.querySelectorAll('.ic-error').forEach(e => e.textContent = '');
        form.querySelectorAll('[aria-invalid]').forEach(e => e.removeAttribute('aria-invalid'));
        const global = document.getElementById('ic-form-error'); global.hidden = true;
        Object.entries(values || {}).forEach(([field, messages]) => {
            const element = document.getElementById('ic-error-' + field);
            if (element) { element.textContent = messages.join(' '); form.elements[field]?.setAttribute('aria-invalid','true'); }
            else { global.textContent = messages.join(' '); global.hidden = false; }
        });
    }
    function lock(value) {
        busy = value;
        form.querySelectorAll('input:not([type=hidden]),select,textarea').forEach(e => e.disabled = value || uncertain);
        submit.disabled = value;
        submit.textContent = value ? 'Đang ghi thu…' : uncertain ? 'Thử lại cùng lần thu' : 'Xác nhận ghi thu';
        dialog.querySelectorAll('[data-ic-close]').forEach(e => e.disabled = value || uncertain);
    }
    open.addEventListener('click', async () => {
        returnFocus = document.activeElement;
        if (!uncertain) {
            errors({}); amount.value = new Intl.NumberFormat('vi-VN').format(Number(form.dataset.remaining));
            form.elements.NgayThu.value = form.dataset.today;
            form.elements.KhoaChongTrung.value = crypto.randomUUID().replaceAll('-','');
            form.elements.GhiChu.value = ''; document.getElementById('ic-note-length').textContent = '0';
        }
        dialog.showModal(); amount.focus();
        try { const response = await fetch(page.dataset.summaryUrl); if (response.ok) { const summary = await response.json(); render(summary); if (!uncertain) amount.value = new Intl.NumberFormat('vi-VN').format(summary.conPhaiTra); } } catch { /* Submit validates the balance again. */ }
    });
    dialog.querySelectorAll('[data-ic-close]').forEach(e => e.addEventListener('click', () => { if (!busy && !uncertain) dialog.close(); }));
    dialog.addEventListener('cancel', event => { if (busy || uncertain) event.preventDefault(); });
    dialog.addEventListener('close', () => returnFocus?.focus());
    form.elements.GhiChu.addEventListener('input', () => document.getElementById('ic-note-length').textContent = String(form.elements.GhiChu.value.length));
    form.addEventListener('submit', async event => {
        event.preventDefault(); if (busy) return;
        errors({});
        if (!uncertain) {
            // Accept plain digits or correctly grouped Vietnamese thousands; reject decimals, signs and malformed separators.
            const raw = amount.value.trim();
            const valid = /^(?:[0-9]+|[1-9][0-9]{0,2}(?:\.[0-9]{3})+)$/.test(raw);
            const integer = raw.replaceAll('.', '');
            const validation = {};
            if (!valid || !Number.isSafeInteger(Number(integer)) || Number(integer) <= 0) validation.SoTien = ['Số tiền phải là số nguyên đồng lớn hơn 0.'];
            else if (BigInt(integer) > BigInt(form.dataset.remaining)) validation.SoTien = ['Số tiền vượt quá số còn phải trả. Tối đa được thu ' + money(Number(form.dataset.remaining)) + '.'];
            if (!form.elements.NgayThu.value || form.elements.NgayThu.value < form.elements.NgayThu.min || form.elements.NgayThu.value > form.elements.NgayThu.max) validation.NgayThu = ['Chọn ngày thu trong khoảng 30 ngày đến hôm nay.'];
            if (!form.elements.HinhThuc.value) validation.HinhThuc = ['Vui lòng chọn hình thức thu.'];
            if (Object.keys(validation).length) { errors(validation); form.querySelector('[aria-invalid=true]')?.focus(); return; }
            submitted = new FormData(form); submitted.set('SoTien',integer);
        }
        lock(true);
        try {
            const response = await fetch(form.action, {method:'POST', body: submitted, headers:{'X-Requested-With':'XMLHttpRequest'}});
            if (response.redirected || response.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại rồi mở hóa đơn để kiểm tra lần thu.');
            if (response.status === 403) { uncertain = false; errors({'':['Bạn không có quyền ghi thu hóa đơn này.']}); return; }
            const body = await response.json();
            if (!response.ok) { uncertain = response.status >= 500; errors(body.errors || {'':['Chưa thể ghi thu. Hãy thử lại cùng lần xác nhận.']}); return; }
            uncertain = false; render(body); dialog.close();
            document.getElementById('ic-success-text').textContent = '✓ Đã ghi nhận ' + money(Number(submitted.get('SoTien'))) + '. Số còn phải trả đã được cập nhật.';
            document.getElementById('ic-success').hidden = false; submitted = null;
        } catch (error) {
            uncertain = true;
            errors({'':['Chưa nhận được kết quả xác nhận. Giữ nguyên lần thu và bấm “Thử lại cùng lần thu” để kiểm tra an toàn. Nếu tải lại trang, hãy kiểm tra lịch sử trước khi tạo lần thu mới.']});
        } finally { lock(false); }
    });
})();
