function revealMeterTarget(target) {
    if (!target) return;
    const sheet = target.closest('dialog');
    document.querySelectorAll('[data-warning-dialog][open]').forEach(dialog => {
        if (dialog !== sheet) dialog.close();
    });
    const floor = target.closest('.meter-floor');
    if (floor) {
        floor.querySelector('.meter-floor-rooms').hidden = false;
        floor.querySelector('[data-floor-toggle]').setAttribute('aria-expanded', 'true');
    }
    target.focus({ preventScroll: true });
    if (!sheet) target.scrollIntoView({ block: target.matches('[data-meter-input], [data-open-warning]') ? 'center' : 'start', behavior: 'auto' });
}

document.querySelectorAll('[data-meter-form]').forEach(form => {
    function validate(input) {
        const baseline = document.getElementById(input.dataset.baselineId);
        const previous = input.dataset.previous || baseline?.value;
        const value = input.value;
        let error = '';
        if (previous == null || previous === '' || (baseline && !baseline.validity.valid)) error = 'Nhập chỉ số đầu kỳ hợp lệ trước khi lưu.';
        else if (!value || !input.validity.valid || !/^\d+(?:\.\d{1,3})?$/.test(value))
            error = 'Nhập chỉ số không âm, tối đa 3 chữ số thập phân.';
        else if (Number(value) < Number(previous)) error = 'Chỉ số mới không được nhỏ hơn chỉ số kỳ trước.';
        input.classList.toggle('is-invalid', Boolean(error));
        input.setAttribute('aria-invalid', error ? 'true' : 'false');
        document.getElementById(input.id + '-error').textContent = error;
        return !error;
    }
    form.querySelectorAll('[data-meter-input]').forEach(input => input.addEventListener('input', () => {
        const wasInvalid = input.getAttribute('aria-invalid') === 'true';
        if (!validate(input) && !wasInvalid) revealMeterTarget(input);
    }));
    form.querySelectorAll('[data-meter-baseline]').forEach(input => input.addEventListener('input', () => {
        form.querySelectorAll('[data-meter-input]').forEach(validate);
    }));
    // Use our field errors for submit too, instead of native validation hiding
    // another room's error above the current viewport. Constraints stay intact.
    form.noValidate = true;
    form.addEventListener('submit', event => {
        const inputs = [...form.querySelectorAll('[data-meter-input]')];
        const results = inputs.map(validate);
        if (results.includes(false)) {
            event.preventDefault();
            revealMeterTarget(inputs[results.indexOf(false)]);
        }
    });
});

document.querySelectorAll('[data-warning-dialog]').forEach(dialog => {
    if (typeof dialog.showModal !== 'function') return;
    dialog.dataset.sheetReady = 'true';
    const form = dialog.closest('[data-meter-form]');
    const opener = form.querySelector('[data-open-warning]');
    const open = () => {
        if (!dialog.open) dialog.showModal();
        dialog.querySelector('[data-usage-warning]').focus({ preventScroll: true });
    };
    opener.addEventListener('click', open);
    dialog.querySelectorAll('[data-close-warning]').forEach(button => button.addEventListener('click', () => dialog.close()));
    dialog.addEventListener('close', () => revealMeterTarget(opener));
    open();
});

const meterPage = document.querySelector('.meter-page');
if (meterPage) {
    const monthSelect = meterPage.querySelector('#meter-month');
    if (monthSelect) {
        const host = monthSelect.parentElement;
        const trigger = document.createElement('button');
        trigger.type = 'button'; trigger.className = 'meter-month-trigger';
        trigger.textContent = monthSelect.selectedOptions[0]?.textContent || 'Chọn tháng';
        trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-expanded', 'false');
        const menu = document.createElement('div');
        menu.className = 'meter-building-menu meter-month-menu'; menu.id = 'meter-month-options'; menu.hidden = true;
        menu.setAttribute('role', 'listbox'); menu.setAttribute('aria-label', 'Tháng ghi chỉ số');
        trigger.setAttribute('aria-controls', menu.id);
        const close = () => { menu.hidden = true; trigger.setAttribute('aria-expanded', 'false'); };
        [...monthSelect.options].forEach(option => {
            const item = document.createElement('button'); item.type = 'button';
            item.textContent = option.textContent; item.setAttribute('role', 'option');
            item.setAttribute('aria-selected', String(option.selected));
            item.addEventListener('click', () => {
                monthSelect.value = option.value; trigger.textContent = option.textContent;
                close(); monthSelect.dispatchEvent(new Event('change', {bubbles:true}));
            });
            menu.append(item);
        });
        const open = () => {
            menu.hidden = false; trigger.setAttribute('aria-expanded', 'true');
            const selected = menu.querySelector('[aria-selected="true"]');
            selected?.focus(); selected?.scrollIntoView({block:'nearest'});
        };
        trigger.addEventListener('click', () => menu.hidden ? open() : close());
        trigger.addEventListener('keydown', event => {
            if (['ArrowDown','ArrowUp'].includes(event.key)) {event.preventDefault(); open();}
        });
        menu.addEventListener('keydown', event => {
            const items = [...menu.querySelectorAll('button')];
            const position = items.indexOf(document.activeElement);
            if (event.key === 'Escape') {event.preventDefault(); close(); trigger.focus();}
            else if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) {
                event.preventDefault();
                const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length-1
                    : (position + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
                items[next]?.focus();
            }
        });
        monthSelect.hidden = true; host.append(trigger, menu);
        document.addEventListener('click', event => {if (!host.contains(event.target)) close();});
        host.addEventListener('focusout', () => setTimeout(() => {if (!host.contains(document.activeElement)) close();}, 0));
    }
    const buildingSelect = meterPage.querySelector('#toaNhaId');
    if (buildingSelect) {
        buildingSelect.addEventListener('change', () => buildingSelect.form.requestSubmit());
        const submit = buildingSelect.form.querySelector('.meter-building-submit');
        if (submit) submit.hidden = true;
        const wrapper = buildingSelect.closest('.meter-building');
        const trigger = document.createElement('button');
        trigger.type = 'button'; trigger.className = 'meter-building-trigger';
        trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-expanded', 'false');
        trigger.setAttribute('aria-label', 'Chọn nhà / khu');
        trigger.textContent = buildingSelect.selectedOptions[0]?.textContent || 'Chọn nhà / khu';
        const list = document.createElement('div');
        list.className = 'meter-building-menu'; list.id = 'meter-building-options'; list.hidden = true;
        list.setAttribute('role', 'listbox'); list.setAttribute('aria-label', 'Nhà / khu');
        trigger.setAttribute('aria-controls', list.id);
        const close = () => { list.hidden = true; trigger.setAttribute('aria-expanded', 'false'); };
        [...buildingSelect.options].forEach(option => {
            const item = document.createElement('button');
            item.type = 'button'; item.textContent = option.textContent; item.disabled = option.disabled;
            item.setAttribute('role', 'option'); item.setAttribute('aria-selected', String(option.selected));
            item.addEventListener('click', () => {
                buildingSelect.value = option.value; trigger.textContent = option.textContent;
                close(); buildingSelect.dispatchEvent(new Event('change', {bubbles:true}));
            });
            list.append(item);
        });
        buildingSelect.hidden = true;
        wrapper.append(trigger, list);
        const open = () => {
            list.hidden = false; trigger.setAttribute('aria-expanded', 'true');
            (list.querySelector('[aria-selected="true"]') || list.querySelector('button:not(:disabled)'))?.focus();
        };
        trigger.addEventListener('click', () => list.hidden ? open() : close());
        trigger.addEventListener('keydown', event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); open(); }
        });
        list.addEventListener('keydown', event => {
            const items = [...list.querySelectorAll('button:not(:disabled)')];
            const position = items.indexOf(document.activeElement);
            if (event.key === 'Escape') { event.preventDefault(); close(); trigger.focus(); }
            else if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) {
                event.preventDefault();
                const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length-1
                    : (position + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
                items[next]?.focus();
            }
        });
        document.addEventListener('click', event => { if (!wrapper.contains(event.target)) close(); });
        wrapper.addEventListener('focusout', () => setTimeout(() => { if (!wrapper.contains(document.activeElement)) close(); }, 0));
    }
    const rooms = [...meterPage.querySelectorAll('[data-meter-room]')];
    rooms.forEach(room => {
        const edit = room.querySelector('[data-edit-room]');
        const form = room.querySelector('[data-meter-form]');
        if (!edit || !form) return;
        edit.hidden = false;
        if (room.dataset.meterActive !== 'true') room.classList.add('is-viewing');
        edit.addEventListener('click', () => {
            room.classList.remove('is-viewing');
            revealMeterTarget(form.querySelector('[data-meter-input]:not([readonly])') || room);
        });
    });
    const search = meterPage.querySelector('[data-room-search]');
    const floorFilter = meterPage.querySelector('[data-floor-filter]');
    const stateSelect = meterPage.querySelector('[data-state-select]');
    const tabs = [...meterPage.querySelectorAll('[data-state-filter]')];
    let state = 'all';
    function filterRooms() {
        const query = (search?.value || '').trim().toLocaleLowerCase('vi-VN');
        rooms.forEach(room => {
            const matchesState = state === 'all' || (state === 'warning' ? room.dataset.roomWarning === 'true' : room.dataset.roomState === state);
            const matchesFloor = !floorFilter || floorFilter.value === 'all' || room.closest('[data-floor]').dataset.floor === floorFilter.value;
            room.hidden = !(matchesState && matchesFloor && room.dataset.roomCode.toLocaleLowerCase('vi-VN').includes(query));
        });
        meterPage.querySelectorAll('[data-floor]').forEach(floor => {
            floor.hidden = ![...floor.querySelectorAll('[data-meter-room]')].some(room => !room.hidden);
        });
        const empty = meterPage.querySelector('[data-empty-filter]');
        if (empty) empty.hidden = rooms.length === 0 || rooms.some(room => !room.hidden);
        tabs.forEach(tab => {
            const selected = tab.dataset.stateFilter === state;
            tab.classList.toggle('is-active', selected);
            tab.setAttribute('aria-pressed', String(selected));
        });
    }
    tabs.forEach(tab => tab.addEventListener('click', () => { state = tab.dataset.stateFilter; filterRooms(); }));
    search?.addEventListener('input', filterRooms);
    floorFilter?.addEventListener('change', filterRooms);
    stateSelect?.addEventListener('change', () => { state = stateSelect.value; filterRooms(); });
    [floorFilter, stateSelect].filter(Boolean).forEach(select => {
        const host = select.parentElement;
        host.classList.add('meter-filter-dropdown');
        const trigger = document.createElement('button');
        trigger.type = 'button'; trigger.className = 'meter-filter-trigger';
        trigger.textContent = select.selectedOptions[0]?.textContent;
        trigger.setAttribute('aria-haspopup', 'listbox'); trigger.setAttribute('aria-expanded', 'false');
        trigger.setAttribute('aria-label', select === floorFilter ? 'Lọc tầng' : 'Lọc trạng thái');
        const menu = document.createElement('div');
        menu.className = 'meter-building-menu meter-filter-menu'; menu.hidden = true;
        menu.id = select === floorFilter ? 'meter-floor-options' : 'meter-state-options';
        menu.setAttribute('role', 'listbox'); menu.setAttribute('aria-label', trigger.getAttribute('aria-label'));
        trigger.setAttribute('aria-controls', menu.id);
        const close = () => { menu.hidden = true; trigger.setAttribute('aria-expanded', 'false'); };
        [...select.options].forEach(option => {
            const item = document.createElement('button'); item.type = 'button';
            item.textContent = option.textContent; item.setAttribute('role', 'option');
            item.setAttribute('aria-selected', String(option.selected));
            item.addEventListener('click', () => {
                select.value = option.value;
                trigger.textContent = option.textContent;
                menu.querySelectorAll('button').forEach(button => button.setAttribute('aria-selected', String(button === item)));
                close(); trigger.focus(); select.dispatchEvent(new Event('change', {bubbles:true}));
            });
            menu.append(item);
        });
        const open = () => {
            menu.hidden = false; trigger.setAttribute('aria-expanded', 'true');
            menu.querySelector('[aria-selected="true"]')?.focus();
        };
        trigger.addEventListener('click', () => menu.hidden ? open() : close());
        trigger.addEventListener('keydown', event => {
            if (['ArrowDown','ArrowUp'].includes(event.key)) {event.preventDefault(); open();}
        });
        menu.addEventListener('keydown', event => {
            const items = [...menu.querySelectorAll('button')];
            const position = items.indexOf(document.activeElement);
            if (event.key === 'Escape') {event.preventDefault(); close(); trigger.focus();}
            else if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) {
                event.preventDefault();
                const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length-1
                    : (position + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
                items[next]?.focus();
            }
        });
        select.hidden = true; host.append(trigger, menu);
        document.addEventListener('click', event => {if (!host.contains(event.target)) close();});
        host.addEventListener('focusout', () => setTimeout(() => {if (!host.contains(document.activeElement)) close();}, 0));
    });
    meterPage.querySelectorAll('[data-floor-toggle]').forEach(button => button.addEventListener('click', () => {
        const expanded = button.getAttribute('aria-expanded') === 'true';
        document.getElementById(button.getAttribute('aria-controls')).hidden = expanded;
        button.setAttribute('aria-expanded', String(!expanded));
    }));
    let currentRoom = window.location.hash.slice(1);
    meterPage.addEventListener('focusin', event => {
        const room = event.target.closest('[data-meter-room]');
        if (room) currentRoom = room.id;
    });
    const next = meterPage.querySelector('[data-next-room]');
    const pending = rooms.filter(room => room.dataset.roomState === 'pending');
    if (next) {
        next.disabled = pending.length === 0;
        next.addEventListener('click', () => {
            const position = rooms.findIndex(room => room.id === currentRoom);
            const target = rooms.slice(position + 1).find(room => room.dataset.roomState === 'pending') || pending[0];
            if (!target) return;
            state = 'all';
            if (search) search.value = '';
            if (floorFilter) floorFilter.value = 'all';
            filterRooms();
            revealMeterTarget(target.querySelector('[data-meter-input]:not([readonly])') || target);
        });
    }
    meterPage.querySelectorAll('[data-meter-filters]').forEach(element => { element.hidden = false; });
    filterRooms();
}

const activeRoom = document.querySelector('[data-meter-room][data-meter-active="true"]');
document.querySelector('[data-meter-export]')?.addEventListener('click', () => {
    const quote = value => {
        const text = String(value);
        return '"' + (/^[\s]*[=+@-]/.test(text) ? "'" : '') + text.replace(/"/g, '""') + '"';
    };
    const rows = [['Phòng', 'Trạng thái', 'Chỉ số đã lưu / kỳ trước']];
    document.querySelectorAll('[data-meter-room]').forEach(room => {
        rows.push([room.dataset.roomCode, room.querySelector('.meter-status-badge')?.textContent.trim() || '',
            [...room.querySelectorAll('.meter-previous,.meter-saved-reading')].map(item => item.textContent.trim().replace(/\s+/g, ' ')).join(' | ')]);
    });
    const url = URL.createObjectURL(new Blob(['\uFEFF' + rows.map(row => row.map(quote).join(';')).join('\r\n')], {type:'text/csv;charset=utf-8'}));
    const link = document.createElement('a');
    link.href = url; link.download = 'bang-ke-chi-so.csv'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
});
if (activeRoom) {
    revealMeterTarget(activeRoom.querySelector('[data-meter-input][aria-invalid="true"]')
        || activeRoom.querySelector('[data-room-error]')
        || activeRoom.querySelector('[data-usage-warning]') || activeRoom);
} else if (/^#meter-room-\d+$/.test(window.location.hash)) {
    revealMeterTarget(document.getElementById(window.location.hash.slice(1)));
}
