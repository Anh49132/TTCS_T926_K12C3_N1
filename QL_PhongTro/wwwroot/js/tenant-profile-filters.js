(() => {
    document.querySelectorAll('.tenant-profiles-bootstrap .op-filters select').forEach(select => {
        const wrapper = document.createElement('div');
        wrapper.className = 'dropdown profile-filter-dropdown';
        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'btn dropdown-toggle profile-filter-trigger';
        trigger.id = select.id + '-trigger';
        trigger.setAttribute('data-bs-toggle', 'dropdown');
        trigger.setAttribute('aria-expanded', 'false');
        trigger.setAttribute('aria-label', select.id === 'profile-building' ? 'Tòa nhà' : 'Tình trạng thuê');
        const caption = document.createElement('span');
        trigger.append(caption);
        const menu = document.createElement('ul');
        menu.className = 'dropdown-menu w-100';
        menu.setAttribute('aria-labelledby', trigger.id);
        [...select.options].forEach(option => {
            const li = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'dropdown-item';
            button.textContent = option.textContent;
            button.dataset.value = option.value;
            button.addEventListener('click', () => {
                select.value = option.value;
                select.dispatchEvent(new Event('change', { bubbles: true }));
            });
            li.append(button); menu.append(li);
        });
        const sync = () => {
            caption.textContent = select.selectedOptions[0]?.textContent || '';
            menu.querySelectorAll('button').forEach(button => {
                const active = button.dataset.value === select.value;
                button.classList.toggle('active', active);
                if (active) button.setAttribute('aria-current', 'true');
                else button.removeAttribute('aria-current');
            });
        };
        select.before(wrapper); wrapper.append(trigger, menu);
        select.hidden = true;
        select.labels[0]?.setAttribute('for', trigger.id);
        select.addEventListener('change', sync); sync();
    });
})();
