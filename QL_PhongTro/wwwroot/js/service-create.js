(() => {
    const page = document.querySelector('.service-create-page');
    if (!page) return;
    const get = id => page.querySelector('#' + id);
    const set = (key, value) => { const node = page.querySelector('[data-sc-' + key + ']'); if (node) node.textContent = value; };
    const update = () => {
        const type = page.querySelector('input[name="CachTinh"]:checked')?.value;
        const modes = {
            THEO_CHI_SO: ['Theo tiêu thụ', 'Thành tiền = (chỉ số cuối − chỉ số đầu) × đơn giá.', 'Ghi nhận chỉ số điện, nước mỗi kỳ và tính theo lượng tiêu thụ.'],
            THEO_NGUOI: ['Theo đầu người', 'Thành tiền = số người × đơn giá tháng.', 'Áp dụng theo số người sử dụng dịch vụ, thu cùng kỳ tiền thuê.'],
            CO_DINH: ['Cố định', 'Thành tiền = số phòng đăng ký × đơn giá tháng.', 'Áp dụng theo số phòng đăng ký; không phụ thuộc chỉ số công tơ.']
        };
        const mode = modes[type];
        set('status', get('sc-status')?.selectedOptions[0]?.textContent || 'Đang áp dụng');
        set('name', get('TenDichVu')?.value.trim() || 'Chưa nhập tên dịch vụ');
        set('type', mode?.[0] || 'Chưa chọn cách tính');
        const raw = get('DonGia')?.value || '';
        set('price', /^\d+$/.test(raw) ? BigInt(raw).toLocaleString('vi-VN') + ' đ' : '— đ');
        set('unit', get('DonViTinh')?.value.trim() || '—');
        const building = get('ToaNhaIds');
        set('building', building?.selectedOptions.length ? [...building.selectedOptions].map(option => option.textContent).join(', ') : '—');
        set('scope', get('ApDungMacDinh')?.checked ? 'Mặc định cho phòng mới' : 'Chưa gán mặc định');
        set('formula', mode?.[1] || 'Chọn cách tính để xem công thức.');
        set('explanation', mode?.[2] || 'Chọn cách tính phí để xem giải thích tương ứng.');
    };
    page.addEventListener('input', update);
    page.addEventListener('change', update);
    update();
})();

(() => {
    const input = document.querySelector('.service-create-page #DonViTinh');
    const source = document.getElementById('sc-units');
    if (!input || !source) return;
    const wrapper = document.createElement('div');
    wrapper.className = 'sc-unit-picker';
    input.before(wrapper);
    wrapper.append(input);
    input.removeAttribute('list');
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', 'sc-unit-menu');
    const menu = document.createElement('div');
    menu.id = 'sc-unit-menu';
    menu.className = 'sc-unit-menu';
    menu.setAttribute('role', 'listbox');
    menu.setAttribute('aria-label', 'Đơn vị tính');
    menu.hidden = true;
    wrapper.append(menu);
    const choices = Array.from(source.options).map((option, index) => {
        const item = document.createElement('button');
        item.type = 'button';
        item.id = 'sc-unit-choice-' + index;
        item.setAttribute('role', 'option');
        item.tabIndex = -1;
        item.textContent = option.value;
        item.addEventListener('pointerdown', event => event.preventDefault());
        item.addEventListener('click', () => {
            input.value = option.value;
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            toggle(false);
            input.focus();
        });
        menu.append(item);
        return item;
    });
    let active = -1;
    const sync = () => choices.forEach(item => item.setAttribute('aria-selected', String(item.textContent === input.value)));
    const toggle = open => {
        menu.hidden = !open;
        input.setAttribute('aria-expanded', String(open));
        active = -1;
        input.removeAttribute('aria-activedescendant');
        choices.forEach(item => item.classList.remove('is-active'));
        sync();
    };
    input.addEventListener('click', () => toggle(menu.hidden));
    input.addEventListener('input', () => toggle(true));
    input.addEventListener('keydown', event => {
        if (event.key === 'Escape') { toggle(false); event.preventDefault(); }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (menu.hidden) toggle(true);
            active = Math.max(0, Math.min(choices.length - 1, active + (event.key === 'ArrowDown' ? 1 : -1)));
            choices.forEach((item, index) => item.classList.toggle('is-active', index === active));
            input.setAttribute('aria-activedescendant', choices[active].id);
        }
        if (event.key === 'Enter' && !menu.hidden && active >= 0) { event.preventDefault(); choices[active].click(); }
        if (event.key === 'Tab') toggle(false);
    });
    input.addEventListener('blur', () => toggle(false));
    document.addEventListener('pointerdown', event => { if (!wrapper.contains(event.target)) toggle(false); });
    toggle(false);
})();

// Hiển thị tòa đang chọn dưới dạng thẻ; giữ select và giá trị gốc của form.
(() => {
    const select = document.querySelector('.service-create-page #ToaNhaId');
    if (!select) return;
    const wrapper = document.createElement('div');
    wrapper.className = 'sc-building-picker';
    select.before(wrapper);
    wrapper.append(select);
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.className = 'sc-building-trigger';
    trigger.setAttribute('aria-haspopup', 'listbox');
    trigger.setAttribute('aria-label', 'Chọn tòa nhà áp dụng');
    const chip = document.createElement('span');
    chip.className = 'sc-building-chip';
    trigger.append(chip);
    const menu = document.createElement('div');
    menu.className = 'sc-building-menu';
    menu.id = 'sc-building-menu';
    menu.setAttribute('role', 'listbox');
    menu.setAttribute('aria-label', 'Tòa nhà áp dụng');
    menu.hidden = true;
    trigger.setAttribute('aria-controls', menu.id);
    wrapper.append(trigger, menu);
    const items = Array.from(select.options).map(option => {
        const item = document.createElement('button');
        item.type = 'button';
        item.setAttribute('role', 'option');
        item.textContent = option.textContent;
        item.addEventListener('click', () => {
            select.value = option.value;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            open(false);
            trigger.focus();
        });
        menu.append(item);
        return item;
    });
    const sync = () => {
        chip.textContent = select.selectedOptions[0]?.textContent || 'Chọn tòa nhà';
        chip.classList.toggle('is-placeholder', !select.value);
        items.forEach((item, index) => item.setAttribute('aria-selected', String(index === select.selectedIndex)));
    };
    const open = value => {
        menu.hidden = !value;
        trigger.setAttribute('aria-expanded', String(value));
        if (value) items[Math.max(0, select.selectedIndex)]?.focus();
    };
    trigger.addEventListener('click', () => open(menu.hidden));
    wrapper.addEventListener('keydown', event => {
        if (event.key === 'Escape') { open(false); trigger.focus(); event.preventDefault(); }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (menu.hidden) { open(true); return; }
            const next = Math.max(0, Math.min(items.length - 1, items.indexOf(document.activeElement) + (event.key === 'ArrowDown' ? 1 : -1)));
            items[next]?.focus();
        }
    });
    wrapper.addEventListener('focusout', event => { if (!wrapper.contains(event.relatedTarget)) open(false); });
    document.addEventListener('pointerdown', event => { if (!wrapper.contains(event.target)) open(false); });
    select.addEventListener('change', sync);
    select.classList.add('sc-building-native');
    select.tabIndex = -1;
    select.setAttribute('aria-hidden', 'true');
    select.addEventListener('focus', () => trigger.focus());
    sync();
    open(false);
})();

(() => {
 const select=document.querySelector('[data-building-multiple]'); if(!select)return;
 const wrap=document.createElement('details');wrap.className='sc-building-multiple';
 const title=document.createElement('summary');title.textContent='Chọn tòa nhà';
 const menu=document.createElement('div');menu.className='sc-building-menu';
 select.before(wrap);wrap.append(title,menu,select);select.hidden=true;
 const sync=()=>{title.textContent=[...select.selectedOptions].map(o=>o.textContent).join(', ')||'Chọn tòa nhà';};
 [...select.options].forEach(option=>{const label=document.createElement('label');const check=document.createElement('input');check.type='checkbox';check.checked=option.selected;label.append(check,document.createTextNode(option.textContent));menu.append(label);check.addEventListener('change',()=>{option.selected=check.checked;sync();select.dispatchEvent(new Event('change',{bubbles:true}));});});
 document.addEventListener('click',event=>{if(!wrap.contains(event.target))wrap.open=false;});wrap.addEventListener('keydown',event=>{if(event.key==='Escape'){wrap.open=false;title.focus();}});sync();
})();

(() => {
 const select=document.getElementById('ToaNhaIds'), mode=document.getElementById('ChonPhongCuThe'), panel=document.querySelector('[data-room-selection]');if(!select||!mode||!panel)return;
 const defaults=document.getElementById('ApDungMacDinh');
 const update=()=>{const ids=new Set([...select.selectedOptions].map(x=>x.value));panel.hidden=!mode.checked;let count=0;panel.querySelectorAll('[data-room-building]').forEach(label=>{const allowed=ids.has(label.dataset.roomBuilding);label.hidden=!allowed;const input=label.querySelector('input');input.disabled=!allowed||!mode.checked;if(!allowed)input.checked=false;if(allowed)count++;});panel.querySelector('[data-room-empty]').hidden=count>0;if(defaults){defaults.disabled=mode.checked;}const summary=document.querySelector('[data-sc-scope]');if(summary&&mode.checked)summary.textContent='Đã chọn '+panel.querySelectorAll('input:checked:not(:disabled)').length+' phòng';};
 document.querySelector('.service-create-page').addEventListener('change',update);update();
})();

(() => {
 const select=document.getElementById('sc-status');if(!select||select.disabled)return;
 const wrap=document.createElement('div');wrap.className='sc-status-picker';
 const trigger=document.createElement('button');trigger.type='button';trigger.className='sc-status-trigger';trigger.id='sc-status-trigger';trigger.setAttribute('aria-label','Trạng thái');trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-expanded','false');
 const menu=document.createElement('div');menu.className='sc-status-menu';menu.id='sc-status-menu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label','Trạng thái');menu.hidden=true;trigger.setAttribute('aria-controls',menu.id);
 select.before(wrap);wrap.append(select,trigger,menu);select.hidden=true;document.querySelector('label[for="sc-status"]')?.setAttribute('for',trigger.id);
 const close=()=>{menu.hidden=true;trigger.setAttribute('aria-expanded','false');};
 const buttons=[...select.options].map(option=>{const button=document.createElement('button');button.type='button';button.setAttribute('role','option');button.textContent=option.textContent;button.dataset.value=option.value;button.addEventListener('click',()=>{select.value=option.value;sync();close();trigger.focus();select.dispatchEvent(new Event('change',{bubbles:true}));});menu.append(button);return button;});
 const sync=()=>{trigger.textContent=select.selectedOptions[0]?.textContent;buttons.forEach(button=>button.setAttribute('aria-selected',String(button.dataset.value===select.value)));};
 const open=()=>{menu.hidden=false;trigger.setAttribute('aria-expanded','true');(buttons.find(button=>button.dataset.value===select.value)||buttons[0]).focus();};
 trigger.addEventListener('click',()=>menu.hidden?open():close());
 wrap.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();close();trigger.focus();}else if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){event.preventDefault();if(menu.hidden){open();return;}const index=buttons.indexOf(document.activeElement);buttons[event.key==='Home'?0:event.key==='End'?buttons.length-1:(index+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length].focus();}});
 wrap.addEventListener('focusout',event=>{if(!wrap.contains(event.relatedTarget))close();});document.addEventListener('click',event=>{if(!wrap.contains(event.target))close();});select.addEventListener('change',sync);sync();
})();
