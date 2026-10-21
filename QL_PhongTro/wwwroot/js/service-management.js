(() => {
    const button = document.querySelector('[data-service-sort]');
    const body = document.querySelector('.sm-table tbody');
    if (!button || !body) return;
    let ascending = true;
    const sort = () => {
        const rows = [...body.querySelectorAll('[data-service-code]')];
        rows.sort((a, b) => a.dataset.serviceCode.localeCompare(b.dataset.serviceCode, 'vi', { numeric: true, sensitivity: 'base' }) * (ascending ? 1 : -1));
        body.append(...rows);
        button.textContent = '⇵ Mã dịch vụ ' + (ascending ? 'tăng dần' : 'giảm dần');
        button.setAttribute('aria-label', 'Đang sắp mã dịch vụ ' + (ascending ? 'tăng dần. Đổi sang giảm dần' : 'giảm dần. Đổi sang tăng dần'));
    };
    button.addEventListener('click', () => { ascending = !ascending; sort(); });
    sort();
    const method = document.querySelector('[data-service-method]');
    let pageNumber = 1;
    const size = document.querySelector('[data-service-page-size]');
    const prev = document.querySelector('[data-service-prev]');
    const next = document.querySelector('[data-service-next]');
    const filter = (reset = true) => {
        if (reset) pageNumber = 1;
        const rows = [...body.querySelectorAll('[data-service-code]')];
        const value = {TheoChiSo:'THEO_CHI_SO',TheoNguoi:'THEO_NGUOI',CoDinh:'CO_DINH'}[method?.value] || method?.value;
        const matching = rows.filter(row => !method || method.value === 'all' || row.dataset.serviceMethodValue === value);
        const pageSize = Number(size?.value || 10);
        const pages = Math.max(1, Math.ceil(matching.length / pageSize));
        pageNumber = Math.max(1, Math.min(pageNumber, pages));
        const first = (pageNumber - 1) * pageSize;
        const visible = new Set(matching.slice(first, first + pageSize));
        rows.forEach(row => row.hidden = !visible.has(row));
        const output = document.querySelector('[data-service-count]');
        if (output) output.textContent = `Hiển thị ${matching.length ? first + 1 : 0}–${Math.min(first + pageSize, matching.length)} trên ${matching.length} dịch vụ`;
        const empty = document.querySelector('[data-service-filter-empty]');
        if (empty) empty.hidden = matching.length !== 0 || rows.length === 0;
        const number = document.querySelector('[data-service-page]');
        if (number) number.textContent = pageNumber;
        if (prev) prev.disabled = pageNumber <= 1;
        if (next) next.disabled = pageNumber >= pages;
    };
    method?.addEventListener('change', () => filter());
    size?.addEventListener('change', () => filter());
    prev?.addEventListener('click', () => { pageNumber--; filter(false); });
    next?.addEventListener('click', () => { pageNumber++; filter(false); });
    button.addEventListener('click', () => filter());
    filter();
})();

(() => {
    document.querySelectorAll('.sm-filters select:not([disabled])').forEach((select, index) => {
        const wrapper = document.createElement('div');
        wrapper.className = 'sm-select-picker';
        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'sm-select-trigger';
        trigger.setAttribute('aria-label', select.getAttribute('aria-label') || 'Chọn mục');
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');
        const menu = document.createElement('div');
        menu.className = 'sm-select-menu';
        menu.id = 'sm-select-menu-' + index;
        menu.setAttribute('role', 'listbox');
        menu.setAttribute('aria-label', trigger.getAttribute('aria-label'));
        menu.hidden = true;
        trigger.setAttribute('aria-controls', menu.id);
        select.before(wrapper);
        wrapper.append(select, trigger, menu);
        select.hidden = true;
        const buttons = [...select.options].map(option => {
            const button = document.createElement('button');
            button.type = 'button';
            button.setAttribute('role', 'option');
            button.textContent = option.textContent;
            button.disabled = option.disabled;
            button.dataset.value = option.value;
            button.addEventListener('click', () => {
                select.value = option.value;
                sync();
                close();
                trigger.focus();
                select.dispatchEvent(new Event('change', { bubbles: true }));
            });
            menu.append(button);
            return button;
        });
        const sync = () => {
            trigger.textContent = select.selectedOptions[0]?.textContent || 'Chọn mục';
            buttons.forEach(button => button.setAttribute('aria-selected', String(button.dataset.value === select.value)));
        };
        const close = () => { menu.hidden = true; trigger.setAttribute('aria-expanded', 'false'); };
        const open = () => {
            menu.hidden = false;
            trigger.setAttribute('aria-expanded', 'true');
            (buttons.find(button => button.dataset.value === select.value && !button.disabled) || buttons.find(button => !button.disabled))?.focus();
        };
        trigger.addEventListener('click', () => menu.hidden ? open() : close());
        wrapper.addEventListener('keydown', event => {
            if (event.key === 'Escape') { event.preventDefault(); close(); trigger.focus(); }
            else if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
                event.preventDefault();
                if (menu.hidden) { open(); return; }
                const enabled = buttons.filter(button => !button.disabled);
                if (!enabled.length) return;
                let next = enabled.indexOf(document.activeElement);
                if (event.key === 'Home') next = 0;
                else if (event.key === 'End') next = enabled.length - 1;
                else next = (next + (event.key === 'ArrowDown' ? 1 : -1) + enabled.length) % enabled.length;
                enabled[next].focus();
            }
        });
        wrapper.addEventListener('focusout', event => { if (!wrapper.contains(event.relatedTarget)) close(); });
        document.addEventListener('click', event => { if (!wrapper.contains(event.target)) close(); });
        select.addEventListener('change', sync);
        sync();
    });
})();

(() => {
    document.querySelector('[data-select-building]')?.addEventListener('click', () => {
        const field = document.querySelector('.sm-filters select[name="toaNhaId"]');
        const trigger = field?.parentElement.querySelector('.sm-select-trigger');
        if (trigger) { trigger.focus(); if (trigger.getAttribute('aria-expanded') !== 'true') trigger.click(); }
        else field?.focus();
    });
})();

(() => {
 const menus=[...document.querySelectorAll('.sm-row-actions')];
 menus.forEach(menu=>menu.addEventListener('toggle',()=>{if(menu.open)menus.forEach(other=>{if(other!==menu)other.open=false;});}));
 document.addEventListener('click',event=>menus.forEach(menu=>{if(!menu.contains(event.target))menu.open=false;}));
 document.addEventListener('keydown',event=>{if(event.key==='Escape')menus.forEach(menu=>{if(menu.open){menu.open=false;menu.querySelector('summary').focus();}});});
})();
