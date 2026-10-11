(() => {
    const monthPicker = document.querySelector('.building-month-dropdown');
    monthPicker?.querySelectorAll('[data-month]').forEach(button => button.addEventListener('click', () => {
        monthPicker.querySelector('.building-month-label').textContent = button.querySelector('span').textContent;
        monthPicker.querySelectorAll('[data-month]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
        monthPicker.dataset.month = button.dataset.month;
        monthPicker.open = false;
        monthPicker.querySelector('summary').focus();
    }));
    document.addEventListener('click', event => {
        if (monthPicker && !monthPicker.contains(event.target)) monthPicker.open = false;
    });
    monthPicker?.addEventListener('keydown', event => {
        if (event.key === 'Escape') { monthPicker.open = false; monthPicker.querySelector('summary').focus(); }
    });
    const panel = document.querySelector('.building-list-panel');
    if (!panel) return;
    const rows = [...panel.querySelectorAll('tr[data-building-id]')];
    const search = panel.querySelector('#building-search');
    const area = panel.querySelector('#building-area');
    const status = panel.querySelector('#building-status');
    const sort = panel.querySelector('.building-sort');
    const normalize = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').toLocaleLowerCase('vi');
    const filter = () => {
        rows.forEach(row => row.hidden = !normalize(row.dataset.search).includes(normalize(search.value.trim())) ||
            (area.value && row.dataset.area !== area.value) || (status.value && row.dataset.status !== status.value));
        panel.querySelector('.building-filter-empty').hidden = !rows.length || rows.some(row => !row.hidden);
    };
    [search, area, status].forEach(input => input.addEventListener('input', filter));
    [area, status].forEach(select => {
        const dropdown = document.createElement('details');
        dropdown.className = 'building-filter-dropdown';
        const summary = document.createElement('summary');
        summary.setAttribute('aria-label', select.getAttribute('aria-label'));
        const caption = document.createElement('span');
        caption.textContent = select.selectedOptions[0].textContent;
        const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        arrow.setAttribute('viewBox', '0 0 16 16');
        arrow.setAttribute('aria-hidden', 'true');
        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', 'm4 6 4 4 4-4');
        arrow.append(path);
        summary.append(caption, arrow);
        const menu = document.createElement('div');
        menu.className = 'building-filter-menu';
        const choices = [...select.options].map(option => {
            const button = document.createElement('button');
            button.type = 'button';
            button.setAttribute('aria-pressed', String(option.selected));
            const label = document.createElement('span');
            label.textContent = option.textContent;
            const check = document.createElement('span');
            check.className = 'building-filter-check';
            check.textContent = '✓';
            check.setAttribute('aria-hidden', 'true');
            button.append(label, check);
            button.addEventListener('click', () => {
                select.value = option.value;
                caption.textContent = option.textContent;
                choices.forEach(choice => choice.setAttribute('aria-pressed', String(choice === button)));
                select.dispatchEvent(new Event('input', { bubbles: true }));
                dropdown.open = false;
                summary.focus();
            });
            menu.append(button);
            return button;
        });
        dropdown.append(summary, menu);
        select.before(dropdown);
        select.hidden = true;
        dropdown.addEventListener('toggle', () => {
            if (dropdown.open) panel.querySelectorAll('.building-filter-dropdown').forEach(other => {
                if (other !== dropdown) other.open = false;
            });
        });
        dropdown.addEventListener('keydown', event => {
            if (event.key === 'Escape') { dropdown.open = false; summary.focus(); }
        });
        document.addEventListener('click', event => {
            if (!dropdown.contains(event.target)) dropdown.open = false;
        });
    });
    const order = () => {
        const ordered = [...rows].sort((a, b) => sort.dataset.sort === 'newest' ? Number(b.dataset.buildingId) - Number(a.dataset.buildingId) : a.dataset.search.localeCompare(b.dataset.search, 'vi'));
        ordered.forEach(row => row.parentElement.append(row));
    };
    sort.addEventListener('click', () => {
        sort.dataset.sort = sort.dataset.sort === 'newest' ? 'name' : 'newest';
        sort.querySelector('span').textContent = sort.dataset.sort === 'newest' ? 'Mới nhất' : 'Tên A–Z';
        order();
    });
    panel.querySelectorAll('[data-view]').forEach(button => button.addEventListener('click', () => {
        panel.classList.toggle('building-grid-view', button.dataset.view === 'grid');
        panel.querySelectorAll('[data-view]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
    }));
    filter(); order();
})();
