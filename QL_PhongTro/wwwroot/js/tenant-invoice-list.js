(() => {
    const page = document.querySelector('.tenant-invoice-list');
    if (!page) return;
    const form = page.querySelector('form'), rows = page.querySelector('#tenant-invoice-list-rows');
    const find = selector => page.querySelector(selector), money = new Intl.NumberFormat('vi-VN');
    const params = new URLSearchParams(location.search);
    const pageUrl = (changes = {}) => {
        const url = new URL(page.dataset.pageUrl, location.origin);
        for (const [key, value] of new FormData(form)) if (value) url.searchParams.set(key, value);
        for (const [key, value] of Object.entries(changes)) url.searchParams.set(key, value);
        return url.href;
    };
    const node = (tag, text, className) => { const el = document.createElement(tag); el.textContent = text; if (className) el.className = className; return el; };
    const pagination = (current, pages) => {
        const wrap = node('div', '', 'tenant-invoice-pagination');
        const link = (label, target, disabled = false) => {
            const el = node(disabled ? 'span' : 'a', label, disabled ? 'disabled' : target === current ? 'current' : '');
            if (!disabled) el.href = pageUrl({ page: target }); else el.setAttribute('aria-disabled', 'true');
            if (target === current && !disabled) el.setAttribute('aria-current', 'page');
            el.setAttribute('aria-label', label === '‹' ? 'Trang trước' : label === '›' ? 'Trang sau' : `Trang ${target}`); wrap.append(el);
        };
        link('‹', current - 1, current <= 1);
        let previous = 0;
        for (const number of [...new Set([1, current - 1, current, current + 1, pages])].filter(x => x >= 1 && x <= pages).sort((a,b) => a-b)) {
            if (number - previous > 1) wrap.append(node('span', '…'));
            link(String(number), number); previous = number;
        }
        link('›', current + 1, current >= pages);
        find('#tenant-invoice-list-pagination').replaceChildren(wrap);
    };
    const render = data => {
        const overview = data.tongQuan;
        find('[data-total]').textContent = money.format(overview.tongSo);
        find('[data-unpaid]').textContent = `${overview.canThanhToan} hóa đơn`;
        find('[data-balance]').textContent = `${money.format(overview.tongConPhaiTra)} đ`;
        find('[data-overdue]').textContent = `${overview.quaHan} hóa đơn`;
        find('[data-tab-total]').textContent = overview.tongSo;
        find('[data-tab-unpaid]').textContent = overview.canThanhToan;
        find('[data-tab-paid]').textContent = overview.daThanhToan;
        page.querySelectorAll('[data-group]').forEach(tab => {
            const active = tab.dataset.group === form.elements.nhom.value;
            tab.href = pageUrl({ nhom: tab.dataset.group, page: 1 }); tab.classList.toggle('active', active);
            if (active) tab.setAttribute('aria-current', 'page');
        });
        find('#tenant-invoice-list-loading').hidden = true;
        find('#tenant-invoice-list-empty').hidden = data.tongSoHoaDon !== 0;
        find('#tenant-invoice-list-no-match').hidden = data.tongSoHoaDon === 0 || data.soKetQua !== 0;
        find('#tenant-invoice-list-results').hidden = false;
        find('#tenant-invoice-list-count').textContent = `${data.soKetQua} hóa đơn`;
        rows.replaceChildren();
        for (const invoice of data.hoaDons) {
            const row = document.createElement('tr');
            const url = `${page.dataset.detailUrl.replace(/\/$/, '')}/${invoice.id}`;
            const code = node('th', ''); code.scope = 'row';
            const anchor = node('a', invoice.maHoaDon); anchor.href = url;
            code.append(anchor, node('small', `Phòng ${invoice.maPhong}`)); row.append(code);
            const cell = (label, text, className = '') => { const td = node('td', text, className); td.dataset.label = label; row.append(td); return td; };
            cell('Kỳ', `${String(invoice.thang).padStart(2,'0')}/${invoice.nam}`);
            cell('Tổng hóa đơn', `${money.format(invoice.tongCong)} đ`, 'til-money');
            const paid = cell('Đã thanh toán', `${money.format(invoice.soDaThanhToan)} đ`, 'til-money');
            if (invoice.soDaCanTru > 0) paid.append(node('small', `Cấn cọc ${money.format(invoice.soDaCanTru)} đ`));
            cell('Còn phải trả', `${money.format(invoice.soConPhaiTra)} đ`, 'til-money til-debt');
            const due = cell('Hạn thanh toán', invoice.hanThanhToan);
            if (invoice.soNgayTre > 0) due.append(node('small', `Quá hạn ${invoice.soNgayTre} ngày`, 'til-late'));
            const status = cell('Trạng thái', '');
            const settled = invoice.soConPhaiTra === 0, partial = !settled && (invoice.soDaThanhToan > 0 || invoice.soDaCanTru > 0);
            status.append(node('span', settled ? 'Đã thanh toán' : partial ? 'Trả một phần' : invoice.soNgayTre > 0 ? 'Quá hạn' : 'Chưa thanh toán',
                `til-status ${settled ? 'paid' : partial ? 'partial' : invoice.soNgayTre > 0 ? 'late' : ''}`));
            if (invoice.choXacNhan) status.append(node('small', `Chờ xác nhận ${money.format(invoice.soTienChoXacNhan)} đ`, 'til-report'));
            const action = cell('Thao tác', '', 'til-action'), detail = node('a', 'Xem chi tiết', 'til-view');
            detail.href = url; detail.setAttribute('aria-label', `Xem chi tiết hóa đơn ${invoice.maHoaDon}`); action.append(detail);
            rows.append(row);
        }
        const start = data.soKetQua ? (data.trang - 1) * data.pageSize + 1 : 0;
        find('[data-result-range]').textContent = `Hiển thị ${start}–${Math.min(data.trang * data.pageSize, data.soKetQua)} trong ${data.soKetQua} hóa đơn`;
        pagination(data.trang, data.soTrang);
    };
    find('#til-page-size').addEventListener('change', event => { location.assign(pageUrl({ pageSize: event.target.value, page: 1 })); });
    form.addEventListener('submit', () => { form.querySelector('button[type=submit]').disabled = true; });
    (async () => {
        try {
            const url = new URL(page.dataset.listUrl, location.origin);
            for (const [key,value] of new FormData(form)) url.searchParams.set(key,value);
            url.searchParams.set('page', page.dataset.page || params.get('page') || 1);
            const response = await fetch(url, { credentials:'same-origin', cache:'no-store', headers:{ Accept:'application/json' } });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || 'Không thể tải danh sách hóa đơn.');
            render(data);
        } catch (failure) {
            find('#tenant-invoice-list-loading').hidden = true;
            find('#tenant-invoice-list-results').hidden = true;
            find('#tenant-invoice-list-error').textContent = failure.message || 'Không thể tải danh sách hóa đơn. Vui lòng thử lại.';
            find('#tenant-invoice-list-error').hidden = false;
        }
    })();
})();
