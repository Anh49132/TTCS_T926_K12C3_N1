(() => {
    const table = document.getElementById('or-table');
    if (!table) return;
    const rows = [...table.tBodies[0].rows];
    const search = document.getElementById('or-search');
    const month = document.getElementById('or-month');
    let page = 1;
    const size = 10;
    const matching = () => rows.filter(row => row.textContent.toLocaleLowerCase('vi').includes(search.value.trim().toLocaleLowerCase('vi')) && (!month.value || row.dataset.month === month.value));
    function render() {
        const filtered = matching();
        const pages = Math.max(1, Math.ceil(filtered.length / size));
        page = Math.min(page, pages);
        rows.forEach(row => { row.hidden = true; });
        filtered.slice((page - 1) * size, page * size).forEach(row => { row.hidden = false; });
        document.getElementById('or-empty').hidden = filtered.length > 0;
        document.getElementById('or-count').textContent = filtered.length ? `Hiển thị ${(page - 1) * size + 1}–${Math.min(page * size, filtered.length)} trong ${filtered.length} yêu cầu` : '0 yêu cầu';
        const nav = document.getElementById('or-pages'); nav.replaceChildren();
        const previous = document.createElement('button'); previous.type = 'button'; previous.className = 'or-button'; previous.textContent = '‹'; previous.setAttribute('aria-label','Trang trước'); previous.disabled = page === 1; previous.onclick = () => { page--; render(); }; nav.append(previous);
        for (let i = 1; i <= pages; i++) { const button = document.createElement('button'); button.type = 'button'; button.textContent = i; button.className = 'or-button' + (i === page ? ' primary' : ''); button.setAttribute('aria-label', `Trang ${i}`); if (i === page) button.setAttribute('aria-current', 'page'); button.onclick = () => { page = i; render(); }; nav.append(button); }
        const next = document.createElement('button'); next.type = 'button'; next.className = 'or-button'; next.textContent = '›'; next.setAttribute('aria-label','Trang sau'); next.disabled = page === pages; next.onclick = () => { page++; render(); }; nav.append(next);
    }
    search.addEventListener('input', () => { page = 1; render(); });
    month.addEventListener('change', () => { page = 1; render(); });
    document.getElementById('or-export').onclick = () => {
        const escape = value => '"' + (/^[=+@\-\t\r]/.test(value.trimStart()) ? "'" : '') + value.replaceAll('"', '""') + '"';
        const lines = [['Khách thuê','Phòng / Tòa nhà','Ngày gửi','Trạng thái'], ...matching().map(row => [...row.cells].slice(0,4).map(cell => cell.innerText.replaceAll('\n',' ')))];
        const url = URL.createObjectURL(new Blob(['\uFEFF' + lines.map(line => line.map(escape).join(',')).join('\r\n')], {type:'text/csv;charset=utf-8'}));
        const a = document.createElement('a'); a.href = url; a.download = 'yeu-cau-khach.csv'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    };
    render();
})();

(() => {
    const rows = [...document.querySelectorAll('.or-reference tr[data-request-id]')];
    const target = document.getElementById('or-selected-detail');
    if (!target) return;
    function select(row) {
        const template = document.getElementById(`or-detail-${row.dataset.requestId}`);
        if (!template) return;
        rows.forEach(item => item.classList.toggle('is-selected', item === row));
        target.replaceChildren(template.content.cloneNode(true));
        const detailLink = document.getElementById('or-open-detail');
        const sourceLink = row.querySelector('a');
        if (detailLink && sourceLink) { detailLink.href = sourceLink.href; detailLink.hidden = false; }
    }
    rows.forEach(row => {
        row.addEventListener('click', event => { if (!event.target.closest('a')) select(row); });
        row.addEventListener('keydown', event => { if (event.target === row && ['Enter',' '].includes(event.key)) { event.preventDefault(); select(row); } });
    });
    if (rows.length) select(rows[0]);
})();

(() => {
    const selects = [...document.querySelectorAll('.or-reference .or-filters select')];
    const closeAll = () => document.querySelectorAll('.or-filter-picker').forEach(picker => { picker.querySelector('.or-filter-menu').hidden = true; picker.querySelector('.or-filter-trigger').setAttribute('aria-expanded', 'false'); });
    selects.forEach(select => {
        const picker = document.createElement('div'); picker.className = 'or-filter-picker';
        select.before(picker); picker.append(select); select.classList.add('or-filter-native'); select.tabIndex = -1; select.setAttribute('aria-hidden','true');
        const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'or-filter-trigger'; trigger.setAttribute('aria-haspopup','listbox'); trigger.setAttribute('aria-expanded','false'); trigger.setAttribute('aria-label',select.getAttribute('aria-label'));
        const label = document.createElement('span'); trigger.append(label);
        trigger.insertAdjacentHTML('beforeend','<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
        const menu = document.createElement('div'); menu.className = 'or-filter-menu'; menu.hidden = true; menu.setAttribute('role','listbox'); menu.setAttribute('aria-label',select.getAttribute('aria-label'));
        picker.append(trigger,menu);
        function update() {
            label.textContent = select.selectedOptions[0]?.textContent || '';
            menu.replaceChildren();
            [...select.options].forEach(option => {
                const button = document.createElement('button'); button.type = 'button'; button.className = 'or-filter-option'; button.setAttribute('role','option'); button.setAttribute('aria-selected',String(option.selected)); button.disabled = option.disabled; button.textContent = option.textContent;
                button.onclick = () => { select.value = option.value; select.dispatchEvent(new Event('change',{bubbles:true})); closeAll(); trigger.focus(); };
                button.addEventListener('keydown',event => {
                    const items = [...menu.querySelectorAll('button:not(:disabled)')]; const index = items.indexOf(button);
                    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); items[(index + (event.key === 'ArrowDown' ? 1 : items.length - 1)) % items.length]?.focus(); }
                    if (event.key === 'Escape') { closeAll(); trigger.focus(); }
                }); menu.append(button);
            });
        }
        trigger.onclick = () => { const open = menu.hidden; closeAll(); menu.hidden = !open; trigger.setAttribute('aria-expanded',String(open)); if (open) menu.querySelector('[aria-selected="true"]')?.focus(); };
        trigger.addEventListener('keydown',event => { if (event.key === 'ArrowDown') {event.preventDefault(); if(menu.hidden) trigger.click();} });
        select.addEventListener('change',update); update();
    });
    document.addEventListener('click',event => {if(!event.target.closest('.or-filter-picker')) closeAll();});
    document.addEventListener('keydown',event => {if(event.key==='Escape') closeAll();});
})();

(() => {
    const input = document.getElementById('or-month');
    if (!input) return;
    const field = input.closest('.or-month-field');
    field.classList.add('or-month-picker');
    const label = document.createElement('span'); label.className = 'or-month-label'; label.setAttribute('aria-hidden','true');
    const icon = document.createElement('span'); icon.className = 'or-month-icon'; icon.setAttribute('aria-hidden','true');
    icon.innerHTML = '<svg viewBox="0 0 24 24"><rect x="4" y="5" width="16" height="16" rx="2"/><path d="M8 3v4m8-4v4M4 10h16m-12 4h1m3 0h1m3 0h1m-9 3h1m3 0h1"/></svg>';
    field.append(label,icon);
    function update() {
        if (input.value) { const [year,month] = input.value.split('-'); label.textContent = `Tháng ${Number(month)}, ${year}`; }
        else { const parts = new Intl.DateTimeFormat('en',{timeZone:'Asia/Ho_Chi_Minh',month:'numeric',year:'numeric'}).formatToParts(new Date()); label.textContent = `Tháng ${parts.find(part=>part.type==='month').value}, ${parts.find(part=>part.type==='year').value}`; }
    }
    input.addEventListener('change',update); update();
})();

(() => {
    const input = document.getElementById('or-month');
    if (!input) return;
    const field = input.closest('.or-month-picker');
    input.tabIndex = -1; input.setAttribute('aria-hidden','true'); field.classList.add('or-month-vietnamese');
    const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'or-month-trigger'; trigger.setAttribute('aria-label','Chọn tháng gửi yêu cầu'); trigger.setAttribute('aria-haspopup','dialog'); trigger.setAttribute('aria-expanded','false');
    trigger.append(field.querySelector('.or-month-label'),field.querySelector('.or-month-icon')); field.append(trigger);
    const menu = document.createElement('div'); menu.className = 'or-month-menu'; menu.hidden = true; menu.setAttribute('role','dialog'); menu.setAttribute('aria-label','Chọn tháng và năm'); field.append(menu);
    let year = Number(new Intl.DateTimeFormat('en',{timeZone:'Asia/Ho_Chi_Minh',year:'numeric'}).format(new Date()));
    const close = () => {menu.hidden=true; trigger.setAttribute('aria-expanded','false');};
    function choose(value) {input.value=value;input.dispatchEvent(new Event('change',{bubbles:true}));close();trigger.focus();}
    function render() {
        menu.replaceChildren();
        const heading = document.createElement('div'); heading.className='or-month-year';
        const previous=document.createElement('button'); previous.type='button';previous.textContent='‹';previous.setAttribute('aria-label','Năm trước');previous.onclick=()=>{year--;render();};
        const title=document.createElement('strong');title.textContent=`Năm ${year}`;
        const next=document.createElement('button');next.type='button';next.textContent='›';next.setAttribute('aria-label','Năm sau');next.onclick=()=>{year++;render();};heading.append(previous,title,next);menu.append(heading);
        const grid=document.createElement('div');grid.className='or-month-grid';
        for(let month=1;month<=12;month++){const button=document.createElement('button');button.type='button';button.textContent=`Tháng ${month}`;const value=`${year}-${String(month).padStart(2,'0')}`;button.classList.toggle('is-selected',input.value===value);button.setAttribute('aria-pressed',String(input.value===value));button.onclick=()=>choose(value);grid.append(button);}menu.append(grid);
        const reset=document.createElement('button');reset.type='button';reset.className='or-month-reset';reset.textContent='Xóa bộ lọc';reset.onclick=()=>choose('');menu.append(reset);
    }
    trigger.onclick=()=>{const open=menu.hidden;if(open){if(input.value)year=Number(input.value.split('-')[0]);render();}menu.hidden=!open;trigger.setAttribute('aria-expanded',String(open));};
    document.addEventListener('click',event=>{if(!field.contains(event.target))close();});
    field.addEventListener('keydown',event=>{if(event.key==='Escape'){close();trigger.focus();}});
})();

/* Menu lý do được tạo lại mỗi khi chọn một yêu cầu. */
(() => {
    const target = document.getElementById('or-selected-detail');
    if (!target) return;
    function enhance() {
        target.querySelectorAll('.or-reject-form select:not([data-enhanced])').forEach(select => {
            select.dataset.enhanced = 'true';
            const picker = document.createElement('div'); picker.className = 'or-reason-picker'; select.before(picker); picker.append(select);
            select.tabIndex = -1; select.setAttribute('aria-hidden','true');
            const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'or-reason-trigger'; trigger.setAttribute('aria-haspopup','listbox'); trigger.setAttribute('aria-expanded','false'); trigger.setAttribute('aria-label','Lý do từ chối');
            const label = document.createElement('span'); trigger.append(label); trigger.insertAdjacentHTML('beforeend','<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
            const menu = document.createElement('div'); menu.className = 'or-reason-menu'; menu.hidden = true; menu.setAttribute('role','listbox'); menu.setAttribute('aria-label','Lý do từ chối'); picker.append(trigger,menu);
            const close = () => {menu.hidden=true;trigger.setAttribute('aria-expanded','false');};
            function update() {
                label.textContent = select.selectedOptions[0]?.textContent || 'Chọn lý do';
                menu.replaceChildren();
                [...select.options].forEach(option => {
                    const button = document.createElement('button'); button.type='button';button.className='or-reason-option';button.textContent=option.textContent;button.setAttribute('role','option');button.setAttribute('aria-selected',String(option.selected));
                    button.onclick=()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));close();trigger.focus();};
                    button.onkeydown=event=>{const items=[...menu.querySelectorAll('button')];const index=items.indexOf(button);if(['ArrowDown','ArrowUp'].includes(event.key)){event.preventDefault();items[(index+(event.key==='ArrowDown'?1:items.length-1))%items.length].focus();}if(event.key==='Escape'){close();trigger.focus();}};
                    menu.append(button);
                });
            }
            trigger.onclick=()=>{const open=menu.hidden;menu.hidden=!open;trigger.setAttribute('aria-expanded',String(open));if(open)menu.querySelector('[aria-selected="true"]')?.focus();};
            picker.addEventListener('focusout',()=>setTimeout(()=>{if(!picker.contains(document.activeElement))close();},0));
            select.addEventListener('change',update);
            select.addEventListener('invalid',event=>{event.preventDefault();trigger.focus();trigger.setAttribute('aria-expanded','true');menu.hidden=false;});
            update();
        });
    }
    new MutationObserver(enhance).observe(target,{childList:true});
    document.addEventListener('click',event=>{target.querySelectorAll('.or-reason-picker').forEach(picker=>{if(!picker.contains(event.target)){picker.querySelector('.or-reason-menu').hidden=true;picker.querySelector('.or-reason-trigger').setAttribute('aria-expanded','false');}});});
    enhance();
})();

(() => {
    const target=document.getElementById('or-selected-detail');
    if(!target)return;
    target.addEventListener('click',event=>{
        const open=event.target.closest('.or-reject-open');
        if(open){open.closest('.or-action-pair').querySelector('.or-reject-dialog').showModal();return;}
        const cancel=event.target.closest('.or-reject-cancel');
        if(cancel)cancel.closest('dialog').close();
        if(event.target.matches('.or-reject-dialog')){const rect=event.target.getBoundingClientRect();if(event.clientX<rect.left||event.clientX>rect.right||event.clientY<rect.top||event.clientY>rect.bottom)event.target.close();}
    });
})();

(() => {
    const target = document.getElementById('or-selected-detail');
    if (!target) return;
    target.addEventListener('click', event => {
        const open = event.target.closest('.or-approve-open');
        if (open && !open.disabled) { open.closest('form').querySelector('.or-approve-dialog').showModal(); return; }
        const cancel = event.target.closest('.or-approve-cancel');
        if (cancel) cancel.closest('dialog').close();
    });
})();
