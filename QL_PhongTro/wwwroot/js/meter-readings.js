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
if (activeRoom) {
    revealMeterTarget(activeRoom.querySelector('[data-meter-input][aria-invalid="true"]')
        || activeRoom.querySelector('[data-room-error]')
        || activeRoom.querySelector('[data-usage-warning]') || activeRoom);
} else if (/^#meter-room-\d+$/.test(window.location.hash)) {
    revealMeterTarget(document.getElementById(window.location.hash.slice(1)));
}
