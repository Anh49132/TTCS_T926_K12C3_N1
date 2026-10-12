(() => {
    const filters = document.querySelector('.bd-filters');
    if (!filters) return;
    filters.querySelectorAll('select').forEach(select => {
        const dropdown = document.createElement('details');
        dropdown.className = 'bd-filter-dropdown';
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
        dropdown.append(summary);
        if (select.disabled) {
            dropdown.classList.add('is-disabled');
            summary.setAttribute('aria-disabled', 'true');
            summary.tabIndex = -1;
            summary.title = 'Chưa có dữ liệu thanh toán để lọc';
            summary.addEventListener('click', event => event.preventDefault());
        } else {
            const menu = document.createElement('div');
            menu.className = 'bd-filter-menu';
            [...select.options].forEach(option => {
                const button = document.createElement('button');
                button.type = 'button';
                button.setAttribute('aria-pressed', String(option.selected));
                const label = document.createElement('span');
                label.textContent = option.textContent;
                const check = document.createElement('span');
                check.className = 'bd-filter-check';
                check.textContent = '✓';
                check.setAttribute('aria-hidden', 'true');
                button.append(label, check);
                button.addEventListener('click', () => {
                    select.value = option.value;
                    caption.textContent = option.textContent;
                    menu.querySelectorAll('button').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
                    dropdown.open = false;
                    summary.focus();
                    select.dispatchEvent(new Event('change', { bubbles: true }));
                });
                menu.append(button);
            });
            dropdown.append(menu);
        }
        select.before(dropdown);
        select.hidden = true;
        dropdown.addEventListener('toggle', () => {
            if (dropdown.open) filters.querySelectorAll('.bd-filter-dropdown').forEach(other => {
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
})();
