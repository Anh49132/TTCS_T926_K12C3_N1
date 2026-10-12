(() => {
    const contractPage = document.querySelector('.create-reference');
    const header = document.querySelector('.dashboard-header');
    if (contractPage && header) {
        const alignSummary = () => contractPage.style.setProperty('--contract-summary-offset', `${Math.ceil(header.getBoundingClientRect().height) + 16}px`);
        alignSummary();
        new ResizeObserver(alignSummary).observe(header);
    }
    let closeOpenMenu = null;
    for (const id of ['NgayChot', 'SoThang', 'contract-building', 'request']) {
        const select = document.getElementById(id);
        if (!select) continue;
        const wrapper = document.createElement('div');
        wrapper.className = 'contract-select-menu';
        select.before(wrapper);
        wrapper.append(select);
        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'contract-select-trigger';
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');
        const label = document.querySelector(`label[for="${id}"]`);
        if (label) {
            label.id = `${id}-label`;
            trigger.setAttribute('aria-labelledby', `${label.id} ${id}-value`);
        }
        const value = document.createElement('span');
        value.id = `${id}-value`;
        const arrow = document.createElement('span');
        arrow.className = 'contract-select-arrow';
        arrow.setAttribute('aria-hidden', 'true');
        trigger.append(value, arrow);
        const menu = document.createElement('div');
        menu.id = `${id}-menu`;
        menu.className = 'contract-select-options';
        menu.setAttribute('role', 'listbox');
        menu.setAttribute('aria-label', label?.textContent.trim() || id);
        menu.hidden = true;
        trigger.setAttribute('aria-controls', menu.id);
        const buttons = [];
        const close = () => {
            menu.hidden = true;
            trigger.setAttribute('aria-expanded', 'false');
            if (closeOpenMenu === close) closeOpenMenu = null;
        };
        const sync = () => {
            value.textContent = select.selectedOptions[0]?.textContent || 'Chọn';
            buttons.forEach(button => {
                const selected = button.dataset.value === select.value;
                button.setAttribute('aria-selected', String(selected));
                button.classList.toggle('is-selected', selected);
            });
        };
        const renderOptions = () => {
        buttons.length = 0;
        menu.replaceChildren();
        for (const option of select.options) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'contract-select-option';
            button.setAttribute('role', 'option');
            button.tabIndex = -1;
            button.dataset.value = option.value;
            button.textContent = option.textContent;
            button.addEventListener('click', () => {
                select.value = option.value;
                sync();
                close();
                trigger.focus();
                select.dispatchEvent(new Event('input', { bubbles: true }));
                select.dispatchEvent(new Event('change', { bubbles: true }));
            });
            menu.append(button);
            buttons.push(button);
        }
        sync();
        };
        renderOptions();
        const open = () => {
            closeOpenMenu?.();
            menu.hidden = false;
            trigger.setAttribute('aria-expanded', 'true');
            closeOpenMenu = close;
            const current = buttons.find(button => button.dataset.value === select.value) || buttons[0];
            if (current) {
                menu.scrollTop = Math.max(0, current.offsetTop - (menu.clientHeight - current.offsetHeight) / 2);
                current.focus({ preventScroll: true });
            }
        };
        trigger.addEventListener('click', () => menu.hidden ? open() : close());
        trigger.addEventListener('keydown', event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); open(); }
        });
        menu.addEventListener('keydown', event => {
            const index = buttons.indexOf(document.activeElement);
            let next;
            if (event.key === 'ArrowDown') next = Math.min(index + 1, buttons.length - 1);
            if (event.key === 'ArrowUp') next = Math.max(index - 1, 0);
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = buttons.length - 1;
            if (next !== undefined) { event.preventDefault(); buttons[next]?.focus(); }
            if (event.key === 'Escape') { event.preventDefault(); close(); trigger.focus(); }
            if (event.key === 'Tab') { close(); trigger.focus(); }
        });
        wrapper.addEventListener('focusout', event => { if (!wrapper.contains(event.relatedTarget)) close(); });
        document.addEventListener('click', event => { if (!wrapper.contains(event.target)) close(); });
        select.addEventListener('change', sync);
        select.addEventListener('contract-options-updated', () => {
            close();
            renderOptions();
        });
        select.addEventListener('invalid', event => {
            event.preventDefault();
            trigger.focus();
        });
        select.classList.add('contract-select-native');
        select.tabIndex = -1;
        wrapper.append(trigger, menu);
        sync();
    }
    const building = document.getElementById('contract-building');
    const room = document.getElementById('request');
    if (building && room) {
        const options = Array.from(room.options).slice(1);
        function filterRooms(reset) {
            const selected = reset ? '' : room.value;
            room.replaceChildren(room.options[0], ...options.filter(option => !building.value || option.dataset.building === building.value));
            room.value = selected;
            room.dispatchEvent(new Event('contract-options-updated'));
        }
        building.addEventListener('change', () => {
            filterRooms(true);
            // A different room must reload its tenant and contract defaults before saving.
            document.getElementById('YeuCauId').value = '';
            document.querySelectorAll('#contract-form button[type="submit"], .create-summary-actions button').forEach(button => button.disabled = true);
        });
        filterRooms(false);
    }
    const start = document.getElementById('NgayBatDau');
    const months = document.getElementById('SoThang');
    const end = document.getElementById('end-date');
    if (!start || !months || !end) return;
    const display = value => value ? value.split('-').reverse().join('/') : '—';
    function update() {
        end.value = '';
        const parts = start.value.split('-').map(Number), count = Number(months.value);
        if (parts.length === 3 && Number.isInteger(count) && count >= 1 && count <= 120 && parts[0] <= 9988) {
            const date = new Date(Date.UTC(parts[0], parts[1] - 1 + count, 1));
            const last = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
            date.setUTCDate(Math.min(parts[2], last));
            date.setUTCDate(date.getUTCDate() - 1);
            end.value = date.toISOString().slice(0, 10);
        }
        const startPreview = document.getElementById('start-preview');
        if (startPreview) startPreview.textContent = display(start.value);
        const endPreview = document.getElementById('end-preview');
        if (endPreview) endPreview.textContent = display(end.value);
        const summaryStart = document.getElementById('summary-start');
        if (summaryStart) summaryStart.textContent = display(start.value);
        const summaryEnd = document.getElementById('summary-end');
        if (summaryEnd) summaryEnd.textContent = display(end.value);
        const term = document.getElementById('term-preview');
        if (term) term.textContent = months.value ? months.value + ' tháng' : '—';
        const validityTerm = document.getElementById('validity-term');
        if (validityTerm) validityTerm.textContent = term?.textContent || '—';
    }
    start.addEventListener('input', update); months.addEventListener('input', update);
    const rent = document.getElementById('GiaThue');
    const deposit = document.getElementById('TienCoc');
    let depositEdited = false;
    function updateDeposit() {
        if (/^\d+$/.test(rent.value)) {
            deposit.max = (BigInt(rent.value) * 3n).toString();
            if (!depositEdited) deposit.value = rent.value;
        } else deposit.removeAttribute('max');
        const preview = document.getElementById('deposit-preview');
        if (preview) preview.textContent = /^\d+$/.test(deposit.value) ? BigInt(deposit.value).toLocaleString('vi-VN') + ' đ' : '—';
        const rentPreview = document.getElementById('rent-preview');
        if (rentPreview) rentPreview.textContent = /^\d+$/.test(rent.value) ? BigInt(rent.value).toLocaleString('vi-VN') + ' đ' : '—';
        const summary = document.querySelector('[data-fixed-fees]');
        if (summary) {
            const fees = BigInt(summary.dataset.fixedFees);
            const validRent = /^\d+$/.test(rent.value);
            const validDeposit = /^\d+$/.test(deposit.value);
            document.getElementById('monthly-total-preview').textContent = validRent ? (BigInt(rent.value) + fees).toLocaleString('vi-VN') + ' đ' : '—';
            document.getElementById('first-total-preview').textContent = validRent && validDeposit ? (BigInt(rent.value) + fees + BigInt(deposit.value)).toLocaleString('vi-VN') + ' đ' : '—';
        }
    }
    deposit.addEventListener('input', () => { depositEdited = true; updateDeposit(); });
    rent.addEventListener('input', updateDeposit);
    const depositMonths = document.getElementById('deposit-months');
    depositMonths?.addEventListener('change', () => {
        if (depositMonths.value !== '' && /^\d+$/.test(rent.value)) {
            deposit.value = (BigInt(rent.value) * BigInt(depositMonths.value)).toString();
            deposit.dispatchEvent(new Event('input', { bubbles: true }));
        }
    });
    rent.addEventListener('input', () => {
        if (depositMonths?.value && /^\d+$/.test(rent.value)) {
            deposit.value = (BigInt(rent.value) * BigInt(depositMonths.value)).toString();
            deposit.dispatchEvent(new Event('input', { bubbles: true }));
        }
    });
    deposit.addEventListener('input', () => {
        if (!depositMonths) return;
        const option = Array.from(depositMonths.options).find(o => o.value !== '' && /^\d+$/.test(rent.value) && /^\d+$/.test(deposit.value) && BigInt(rent.value) * BigInt(o.value) === BigInt(deposit.value));
        depositMonths.value = option?.value ?? '';
    });
    const form = document.getElementById('contract-form');
    const completion = document.getElementById('completion-status');
    function updateCompletion() {
        if (!form || !completion) return;
        const fields = Array.from(form.querySelectorAll('[required]'));
        const count = fields.filter(field => field.value !== '' && field.validity.valid).length;
        const selected = !!document.getElementById('YeuCauId').value;
        completion.textContent = `Đã hoàn tất ${count + Number(selected)}/${fields.length + 1} trường bắt buộc`;
    }
    form?.addEventListener('input', updateCompletion);
    form?.addEventListener('change', updateCompletion);
    updateCompletion();
    // Preserve values of existing drafts and failed submissions on initial load.
    depositEdited = true; updateDeposit(); depositEdited = deposit.value !== rent.value;
    update();
    const billingDay = document.getElementById('NgayChot');
    const billingPreview = document.getElementById('billing-day-preview');
    if (billingPreview) billingDay.addEventListener('change', () => {
        billingPreview.textContent = billingDay.value ? 'Ngày ' + billingDay.value.padStart(2, '0') + ' · Hằng tháng' : '—';
    });
    const previewDialog = document.getElementById('contract-preview-dialog');
    document.getElementById('preview-contract')?.addEventListener('click', () => {
        const summary = document.querySelector('.lease-create-summary');
        const content = document.getElementById('contract-preview-content');
        content.replaceChildren();
        for (const element of summary.querySelectorAll('.create-room, .contract-generated, .create-summary-dates, .create-summary-total, .create-summary-footnote')) {
            const copy = element.cloneNode(true);
            copy.removeAttribute('id');
            copy.querySelectorAll('[id]').forEach(node => node.removeAttribute('id'));
            content.append(copy);
        }
        const terms = document.createElement('p');
        terms.style.whiteSpace = 'pre-wrap';
        terms.textContent = 'Điều khoản bổ sung: ' + (document.getElementById('contract-extra-terms')?.value || 'Không có');
        content.append(terms);
        const files = document.getElementById('contract-file-list');
        if (files?.children.length) content.append(files.cloneNode(true));
        previewDialog.showModal();
    });
    document.getElementById('close-contract-preview')?.addEventListener('click', () => previewDialog.close());
    const status = document.getElementById('overlap-status'), save = document.getElementById('save-contract');
    const request = document.getElementById('YeuCauId').value;
    let timer, abort;
    async function checkOverlap() {
        if (abort) abort.abort();
        status.classList.add('is-pending');
        if (!request || !start.value || !start.validity.valid || !months.validity.valid || !end.value) {
            save.disabled = true;
            status.textContent = 'Vui lòng chọn ngày bắt đầu từ hôm nay và kỳ hạn hợp lệ.';
            return;
        }
        abort = new AbortController();
        status.textContent = 'Đang kiểm tra khoảng thời gian thuê…';
        save.disabled = true;
        try {
            const query = new URLSearchParams({ yeuCauId: request, ngayBatDau: start.value, soThang: months.value });
            const response = await fetch('/HopDong/KiemTra?' + query, { signal: abort.signal });
            if (!response.ok) throw new Error('Không thể kiểm tra');
            const conflicts = await response.json();
            status.replaceChildren();
            status.classList.remove('is-pending');
            status.classList.toggle('has-conflict', conflicts.length > 0);
            const title = document.createElement('strong');
            title.textContent = conflicts.length ? 'Không thể lưu — có hợp đồng chồng lấn:' : 'Không có hợp đồng hiệu lực chồng lấn trong khoảng thời gian đã chọn.';
            status.append(title);
            if (conflicts.length) {
                const list = document.createElement('ul');
                for (const item of conflicts) { const li = document.createElement('li'); li.textContent = `${item.ma}: ${item.tuNgay} – ${item.denNgay}`; list.append(li); }
                status.append(list);
            }
            save.disabled = conflicts.length > 0;
        } catch (error) {
            if (error.name === 'AbortError') return;
            status.textContent = 'Chưa kiểm tra được. Hệ thống sẽ kiểm tra lại trên máy chủ khi lưu.';
            save.disabled = false;
        }
    }
    function scheduleCheck() { if (abort) abort.abort(); clearTimeout(timer); timer = setTimeout(checkOverlap, 250); }
    start.addEventListener('input', scheduleCheck); months.addEventListener('input', scheduleCheck);
    if (request) checkOverlap();
})();

const contractFiles = document.querySelector('#TaiLieus');
if (contractFiles) {
    contractFiles.addEventListener('change', () => {
        const list = document.getElementById('contract-file-list');
        list.replaceChildren();
        for (const file of contractFiles.files) {
            const item = document.createElement('li');
            item.textContent = `${file.name} · ${(file.size / 1024).toLocaleString('vi-VN', {maximumFractionDigits:0})} KB`;
            list.append(item);
        }
        contractFiles.setCustomValidity(contractFiles.files.length > 5 ? 'Chỉ đính kèm tối đa 5 tệp.'
            : [...contractFiles.files].some(file => file.size === 0 || file.size > 10 * 1024 * 1024) ? 'Mỗi tệp phải lớn hơn 0 và không quá 10 MB.' : '');
    });
}
