(() => {
    const settings = document.getElementById('review-settings');
    const openButtons = [...document.querySelectorAll('[data-review-open]')];
    const dialog = document.getElementById('issue-confirmation');
    const issue = document.getElementById('issue-date');
    const due = document.getElementById('due-date');
    const dateTexts = new Map();
    const displayDate = value => /^\d{4}-\d{2}-\d{2}$/.test(value) ? value.split('-').reverse().join('/') : '';
    // Native pickers follow the OS locale. Keep the editable display consistently dd/MM/yyyy.
    [issue, due].filter(Boolean).forEach(native => {
        const wrapper = document.createElement('div'); wrapper.className = 'mr-date-control';
        native.before(wrapper); wrapper.appendChild(native); native.classList.add('mr-date-native'); native.tabIndex = -1;
        const input = document.createElement('input'); input.type = 'text'; input.id = native.id + '-text'; input.className = 'form-control';
        input.placeholder = 'dd/MM/yyyy'; input.required = true; input.value = displayDate(native.value); input.inputMode = 'text';
        const label = document.querySelector(`label[for="${native.id}"]`); if (label) label.htmlFor = input.id;
        const button = document.createElement('button'); button.type = 'button'; button.className = 'mr-date-picker'; button.textContent = '▦';
        button.setAttribute('aria-label', native === issue ? 'Chọn ngày phát hành' : 'Chọn hạn thanh toán');
        button.addEventListener('click', () => { if (native.showPicker) native.showPicker(); else native.focus(); });
        wrapper.append(input, button, native); dateTexts.set(native, input);
        input.addEventListener('input', () => {
            const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(input.value.trim());
            let value = '';
            if (match) {
                const candidate = `${match[3]}-${match[2]}-${match[1]}`;
                const parsed = new Date(candidate + 'T00:00:00Z');
                if (!Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0,10) === candidate && Number(match[3]) >= 1900 && Number(match[3]) <= 9998) value = candidate;
            }
            native.value = value;
            input.setCustomValidity(value ? '' : 'Nhập ngày hợp lệ theo định dạng dd/MM/yyyy.');
            native.dispatchEvent(new Event('input', { bubbles: true }));
        });
        native.addEventListener('change', () => { input.value = displayDate(native.value); input.setCustomValidity(''); native.dispatchEvent(new Event('input', { bubbles: true })); });
    });
    let dirty = false, manualDue = false;
    const plusSeven = value => {
        const date = new Date(value + 'T00:00:00Z');
        if (Number.isNaN(date.getTime())) return '';
        date.setUTCDate(date.getUTCDate() + 7);
        return date.toISOString().slice(0, 10);
    };
    if (issue && due) {
        manualDue = due.value !== plusSeven(issue.value);
        due.addEventListener('input', () => { manualDue = true; });
        issue.addEventListener('input', () => {
            if (!manualDue) { due.value = plusSeven(issue.value); const text = dateTexts.get(due); text.value = displayDate(due.value); text.setCustomValidity(due.value ? '' : 'Nhập ngày phát hành hợp lệ trước.'); }
            due.min = issue.value;
        });
        const validateDue = () => { if (due.value && issue.value) dateTexts.get(due).setCustomValidity(due.value < issue.value ? 'Hạn thanh toán phải từ ngày phát hành trở đi.' : ''); };
        issue.addEventListener('input', validateDue); due.addEventListener('input', validateDue);
    }
    settings?.addEventListener('input', () => {
        dirty = true;
        openButtons.forEach(button => { button.disabled = true; });
        const warning = document.getElementById('review-dirty');
        if (warning) warning.hidden = false;
    });
    const rows = [...document.querySelectorAll('#ready-table tbody tr')];
    const checks = rows.map(row => row.querySelector('.room-select'));
    const updateSelection = () => {
        if (!rows.length) return;
        const chosen = rows.filter(row => row.querySelector('.room-select').checked);
        const total = chosen.reduce((sum, row) => sum + BigInt(row.dataset.total), 0n);
        document.querySelectorAll('[data-selected-count]').forEach(x => { x.textContent = chosen.length; });
        document.querySelectorAll('[data-selected-total]').forEach(x => { x.textContent = new Intl.NumberFormat('vi-VN').format(total) + ' đ'; });
        openButtons.forEach(button => { button.disabled = dirty || chosen.length === 0 || button.dataset.canWrite === 'false' || !document.querySelector('input[name="reviewToken"]')?.value; });
        const visible = rows.filter(row => !row.hidden);
        const all = document.getElementById('select-visible');
        if (all) { all.checked = visible.length > 0 && visible.every(row => row.querySelector('.room-select').checked); all.indeterminate = visible.some(row => row.querySelector('.room-select').checked) && !all.checked; }
    };
    checks.forEach(check => check.addEventListener('change', updateSelection));
    document.getElementById('select-visible')?.addEventListener('change', event => {
        rows.filter(row => !row.hidden).forEach(row => { row.querySelector('.room-select').checked = event.target.checked; });
        updateSelection();
    });
    const pageSize = 15;
    let page = 1;
    const search = document.getElementById('room-search'), floor = document.getElementById('floor-filter'), sort = document.getElementById('room-sort');
    const renderRows = () => {
        if (!search) return;
        const term = search.value.toLocaleLowerCase('vi').trim();
        const filtered = rows.filter(row => (row.dataset.room + ' ' + row.dataset.tenant).toLocaleLowerCase('vi').includes(term) && (!floor.value || row.dataset.floor === floor.value));
        filtered.sort((a, b) => {
            if (sort.value === 'room') return a.dataset.room.localeCompare(b.dataset.room, 'vi', { numeric: true });
            const difference = BigInt(a.dataset.total) - BigInt(b.dataset.total);
            return (difference > 0n ? 1 : difference < 0n ? -1 : 0) * (sort.value === 'total-desc' ? -1 : 1);
        });
        const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
        page = Math.min(page, pages);
        rows.forEach(row => { row.hidden = true; });
        const tbody = document.querySelector('#ready-table tbody');
        filtered.forEach((row, index) => { tbody.appendChild(row); row.hidden = index < (page - 1) * pageSize || index >= page * pageSize; });
        document.getElementById('room-page-info').textContent = filtered.length ? `Hiển thị ${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, filtered.length)} trên ${filtered.length} phòng` : 'Không có phòng phù hợp';
        const pagination = document.getElementById('room-pages');
        pagination.replaceChildren();
        for (let number = 1; number <= pages; number++) {
            const button = document.createElement('button'); button.type = 'button'; button.textContent = number;
            button.setAttribute('aria-label', `Trang ${number}`);
            if (number === page) button.setAttribute('aria-current', 'page');
            button.addEventListener('click', () => { page = number; renderRows(); }); pagination.appendChild(button);
        }
        updateSelection();
    };
    [search, floor, sort].filter(Boolean).forEach(input => input.addEventListener('input', () => { page = 1; renderRows(); }));
    document.querySelectorAll('[data-review-tab]').forEach(button => button.addEventListener('click', () => {
        const selected = button.dataset.reviewTab;
        document.getElementById('ready-panel').hidden = selected !== 'ready';
        document.getElementById('skipped-panel').hidden = selected !== 'skipped';
        document.querySelectorAll('.mr-tabs [data-review-tab]').forEach(tab => tab.setAttribute('aria-selected', String(tab.dataset.reviewTab === selected)));
    }));
    const skipped = document.getElementById('skipped-panel'); if (skipped) skipped.hidden = true;
    openButtons.forEach(button => button.addEventListener('click', () => {
        if (dirty || (rows.length && !checks.some(check => check.checked))) return;
        const confirm = dialog.querySelector('[name="xacNhan"]'); confirm.checked = false;
        dialog.showModal();
    }));
    document.querySelector('[data-review-close]')?.addEventListener('click', () => { dialog.close(); });
    document.getElementById('export-review')?.addEventListener('click', () => {
        const cell = value => '"' + (/^[=+@-]/.test(value.trim()) ? "'" : '') + value.replaceAll('"', '""') + '"';
        const csv = [['Phòng','Khách thuê','Kết quả','Tổng tiền','Lý do'], ...rows.map(row => [row.dataset.room,row.dataset.tenant,'Sẵn sàng',row.dataset.total,'']), ...[...document.querySelectorAll('#skipped-table tbody tr')].map(row => [row.cells[0].textContent.trim(),'','Bị bỏ qua','',row.cells[1].textContent.trim()])].map(line => line.map(cell).join(',')).join('\r\n');
        const url = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
        const link = document.createElement('a'); link.href = url; link.download = 'kiem-tra-hoa-don.csv'; link.click(); URL.revokeObjectURL(url);
    });
    document.querySelectorAll('table[data-page-size]').forEach(table => {
        const tableRows = [...table.querySelectorAll('tbody tr')];
        const size = Number(table.dataset.pageSize);
        if (tableRows.length <= size) return;
        let currentPage = 1;
        const pageCount = Math.ceil(tableRows.length / size);
        const navigation = document.createElement('nav');
        navigation.className = 'mr-pagination';
        navigation.setAttribute('aria-label', 'Phân trang danh sách hóa đơn');
        const info = document.createElement('span');
        info.setAttribute('aria-live', 'polite');
        const buttons = document.createElement('div');
        navigation.append(info, buttons);
        table.closest('.table-responsive').after(navigation);
        const render = () => {
            const start = (currentPage - 1) * size;
            tableRows.forEach((row, index) => { row.hidden = index < start || index >= start + size; });
            info.textContent = `Hiển thị ${start + 1}–${Math.min(start + size, tableRows.length)} trên ${tableRows.length} bản ghi`;
            buttons.replaceChildren();
            for (let number = 1; number <= pageCount; number++) {
                const button = document.createElement('button');
                button.type = 'button';
                button.textContent = number;
                button.setAttribute('aria-label', `Trang ${number}`);
                if (number === currentPage) button.setAttribute('aria-current', 'page');
                button.addEventListener('click', () => { currentPage = number; render(); });
                buttons.appendChild(button);
            }
        };
        render();
    });
    renderRows();
})();

(() => {
    const form = document.getElementById('publish-monthly-drafts');
    if (!form) return;
    const boxes = [...form.querySelectorAll('input[name="selectedInvoiceIds"]')];
    const issue = form.querySelector('#batch-issue');
    const due = form.querySelector('#batch-due');
    form.querySelector('[data-batch-select]').addEventListener('click', () => boxes.forEach((box, i) => box.checked = i < 50));
    form.querySelector('[data-batch-clear]').addEventListener('click', () => boxes.forEach(box => box.checked = false));
    issue.addEventListener('change', () => due.min = issue.value);
    form.addEventListener('submit', event => {
        const count = boxes.filter(box => box.checked).length;
        if (count < 1 || count > 50) {
            event.preventDefault();
            form.querySelector('[data-batch-status]').textContent = 'Hãy chọn từ 1 đến 50 hóa đơn để phát hành.';
            form.querySelector('[data-batch-select]').focus();
        }
    });
})();

(() => {
 const form=document.getElementById('publish-monthly-drafts'), dialog=document.getElementById('batch-confirmation');
 if(!form || !dialog) return;
 form.querySelector('[data-batch-open]').addEventListener('click',()=>{
  const selected=[...form.querySelectorAll('[name="selectedInvoiceIds"]:checked')];
  if(selected.length<1 || selected.length>50){form.querySelector('[data-batch-status]').textContent='Hãy chọn từ 1 đến 50 hóa đơn.';return;}
  const issue=form.querySelector('#batch-issue'),due=form.querySelector('#batch-due');
  if(!issue.reportValidity() || !due.reportValidity() || due.value<issue.value){form.querySelector('[data-batch-status]').textContent='Kiểm tra ngày phát hành và hạn thanh toán.';return;}
  dialog.querySelector('[data-batch-count]').textContent=selected.length;
  dialog.querySelector('[data-batch-total]').textContent=new Intl.NumberFormat('vi-VN').format(selected.reduce((sum,x)=>sum+BigInt(x.dataset.amount),0n))+' đ';
  dialog.querySelector('[data-batch-date]').textContent=issue.value.split('-').reverse().join('/');
  dialog.querySelector('[data-batch-due]').textContent=due.value.split('-').reverse().join('/');
  dialog.querySelector('[name="xacNhan"]').checked=false;dialog.showModal();
 });
 dialog.querySelector('[data-batch-close]').addEventListener('click',()=>dialog.close());
})();
