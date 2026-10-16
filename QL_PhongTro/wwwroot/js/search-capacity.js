(() => {
    const dropdown = document.querySelector('.capacity-dropdown');
    if (!dropdown) return;
    dropdown.querySelectorAll('[data-capacity]').forEach(option => {
        option.addEventListener('click', () => {
            dropdown.querySelector('input').value = option.dataset.capacity;
            dropdown.querySelector('[data-capacity-label]').textContent = option.textContent;
            dropdown.querySelectorAll('[data-capacity]').forEach(item => {
                item.classList.toggle('active', item === option);
                item.setAttribute('aria-pressed', String(item === option));
            });
            dropdown.querySelector('#capacity-toggle').focus();
        });
    });
})();

// Preserve native select values and change handlers while presenting Bootstrap menus.
(() => {
    const fields = [
        ['#timtin-sort', 'Sắp xếp'],
        ['#tim-tin-form select[name="PhuongXa"]', 'Phường/xã'],
        ['#tim-tin-form select[name="GiaToiDa"]', 'Mức giá']
    ];
    fields.forEach(([selector, label], index) => {
        const select = document.querySelector(selector);
        if (!select) return;
        const wrapper = document.createElement('span');
        wrapper.className = 'dropdown search-select-dropdown';
        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'search-select-toggle dropdown-toggle';
        toggle.id = `search-select-${index}`;
        toggle.dataset.bsToggle = 'dropdown';
        toggle.setAttribute('aria-expanded', 'false');
        toggle.setAttribute('aria-label', label);
        const menu = document.createElement('ul');
        menu.className = 'dropdown-menu';
        menu.setAttribute('aria-labelledby', toggle.id);
        const options = [];
        const render = () => {
            toggle.textContent = select.selectedOptions[0]?.textContent ?? label;
            options.forEach(([button, option]) => {
                button.classList.toggle('active', option.selected);
                button.setAttribute('aria-pressed', String(option.selected));
            });
        };
        [...select.options].forEach(option => {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'dropdown-item';
            button.textContent = option.textContent;
            button.disabled = option.disabled;
            button.dataset.selectValue = option.value;
            options.push([button, option]);
            button.addEventListener('click', () => {
                select.value = option.value;
                render();
                select.dispatchEvent(new Event('change', { bubbles: true }));
                toggle.focus();
            });
            item.append(button);
            menu.append(item);
        });
        select.before(wrapper);
        wrapper.append(toggle, menu);
        select.hidden = true;
        select.addEventListener('change', render);
        select.form?.addEventListener('reset', () => setTimeout(render, 0));
        render();
    });
})();
