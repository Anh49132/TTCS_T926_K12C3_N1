(() => {
    const form = document.getElementById('room-bulk-form');
    if (!form) return;
    const field = id => form.querySelector('#' + id);
    const output = (name, value) => { form.querySelector('[data-preview="' + name + '"]').textContent = value; };
    const list = document.getElementById('rb-room-list');
    const validCount = value => /^\d+$/.test(value) && Number(value) >= 1 && Number(value) <= 99 ? Number(value) : 0;
    const sync = () => {
        const building = field('ToaNhaId');
        const name = building.value ? building.selectedOptions[0].textContent : 'Chưa chọn tòa nhà';
        document.getElementById('rb-building-name').textContent = name;
        output('building', building.value ? name : '—');
        const floors = validCount(field('SoTang').value);
        const rooms = validCount(field('SoPhongMoiTang').value);
        output('floors', floors ? (floors === 1 ? 'Tầng 1' : 'Tầng 1 – ' + floors) : '—');
        output('area', field('DienTich').value ? field('DienTich').value + ' m²' : '—');
        const rent = field('GiaThueDisplay').value.trim();
        output('rent', /^(\d+|\d{1,3}(\.\d{3})+)$/.test(rent) ? BigInt(rent.replaceAll('.', '')).toLocaleString('vi-VN') + ' đ / tháng' : '—');
        output('people', field('SoNguoiToiDa').value ? field('SoNguoiToiDa').value + ' người / phòng' : '1 người / phòng (mặc định)');
        const total = floors * rooms;
        output('total', total ? total.toLocaleString('vi-VN') + ' phòng' : '—');
        list.replaceChildren();
        const status = field('TrangThai');
        const state = status.value ? status.selectedOptions[0].textContent : 'Chưa chọn';
        const fragment = document.createDocumentFragment();
        for (let i = 0; i < Math.min(total, 100); i++) {
            const floor = Math.floor(i / rooms) + 1;
            const code = String(floor) + String(i % rooms + 1).padStart(2, '0');
            const row = document.createElement('li');
            const badge = document.createElement('span'); badge.className = 'rb-code'; badge.textContent = code;
            const title = document.createElement('span'); title.textContent = 'Phòng ' + code;
            const stateBadge = document.createElement('span'); stateBadge.className = 'rb-room-state'; stateBadge.textContent = state;
            row.append(badge, title, stateBadge); fragment.append(row);
        }
        list.append(fragment);
        document.getElementById('rb-preview-empty').hidden = total > 0;
        const limit = document.getElementById('rb-preview-limit'); limit.hidden = total <= 100;
        limit.textContent = 'Hiển thị 100 / ' + total.toLocaleString('vi-VN') + ' phòng. Toàn bộ loạt sẽ được tạo khi lưu.';
    };
    form.addEventListener('input', sync); form.addEventListener('change', sync);
    form.addEventListener('keydown', event => { if (event.ctrlKey && event.key === 'Enter') { event.preventDefault(); form.requestSubmit(); } });
    sync();
})();

(() => {
    const select = document.querySelector('.room-bulk-page #TrangThai');
    if (!select) return;
    const wrapper = document.createElement('div'); wrapper.className = 'rb-state-picker';
    select.before(wrapper); wrapper.append(select); select.tabIndex = -1; select.setAttribute('aria-hidden', 'true');
    const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'rb-state-trigger';
    trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-controls', 'rb-state-menu'); trigger.setAttribute('aria-label', 'Trạng thái ban đầu');
    const value = document.createElement('span');
    trigger.append(value);
    trigger.insertAdjacentHTML('beforeend', '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
    const menu = document.createElement('div'); menu.id = 'rb-state-menu'; menu.className = 'rb-state-menu'; menu.setAttribute('role', 'listbox'); menu.setAttribute('aria-label', 'Trạng thái ban đầu'); menu.hidden = true;
    wrapper.append(trigger, menu);
    const options = Array.from(select.options).map((option, index) => {
        const button = document.createElement('button'); button.type = 'button'; button.className = 'rb-state-option'; button.setAttribute('role', 'option'); button.textContent = option.textContent;
        button.addEventListener('click', () => { select.value = option.value; select.dispatchEvent(new Event('change', {bubbles:true})); setOpen(false); trigger.focus(); });
        button.addEventListener('keydown', event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); options[(index + (event.key === 'ArrowDown' ? 1 : -1) + options.length) % options.length].focus(); }
            if (event.key === 'Home' || event.key === 'End') { event.preventDefault(); options[event.key === 'Home' ? 0 : options.length - 1].focus(); }
            if (event.key === 'Escape') { event.preventDefault(); setOpen(false); trigger.focus(); }
        });
        menu.append(button); return button;
    });
    const sync = () => { value.textContent = select.selectedOptions[0]?.textContent || 'Chọn trạng thái'; options.forEach((option,index) => option.setAttribute('aria-selected', String(index === select.selectedIndex))); };
    const setOpen = open => { menu.hidden = !open; trigger.setAttribute('aria-expanded', String(open)); if(open) options[Math.max(0,select.selectedIndex)]?.focus(); };
    trigger.addEventListener('click', () => setOpen(menu.hidden));
    trigger.addEventListener('keydown', event => { if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); setOpen(true); } });
    document.addEventListener('click', event => { if (!wrapper.contains(event.target)) setOpen(false); });
    wrapper.addEventListener('focusout', event => { if (!wrapper.contains(event.relatedTarget)) setOpen(false); });
    select.addEventListener('change', sync); sync(); setOpen(false);
})();
