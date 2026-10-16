(() => {
    const section = document.getElementById('room-services');
    if (!section) return;
    section.querySelectorAll('[data-service-toggle]').forEach(check => {
        check.addEventListener('change',async () => {
            const form = document.getElementById(check.dataset.stateForm);
            if (!form) return;
            const selected=check.checked;
            const row=check.closest('.rc-service-row');
            let status=row.querySelector('.re-service-save-status');
            if(!status){status=document.createElement('small');status.className='re-service-save-status';status.setAttribute('role','status');row.querySelector('.re-service-name').append(status);}
            check.disabled=true;status.textContent='Đang lưu…';
            try {
                const data=new FormData(form);data.set('enabled',String(selected));
                const response=await fetch(form.action,{method:'POST',body:data});
                if(!response.ok)throw new Error('Không lưu được dịch vụ. Vui lòng thử lại.');
                const page=new DOMParser().parseFromString(await response.text(),'text/html');
                const error=page.querySelector('#room-services .alert-danger');
                const success=page.querySelector('#room-services .alert-success');
                if(error || !success)throw new Error(error?.textContent.trim() || 'Không xác nhận được việc lưu. Vui lòng tải lại trang.');
                form.querySelector('[name="enabled"]').value=String(!selected);
                status.textContent=selected?'Đã bật dịch vụ.':'Đã ghi nhận tắt từ kỳ sau.';
                const button=row.querySelector('.room-service-actions button');if(button)button.hidden=true;
            } catch(error){check.checked=!selected;status.textContent=error.message;}
            finally{check.disabled=false;}
        });
    });
})();

(() => {
    const select = document.querySelector('.room-edit-page select[name="TrangThai"]');
    if (!select) return;
    const picker = document.createElement('div'); picker.className = 're-state-picker';
    select.before(picker); picker.append(select); select.classList.add('re-state-native'); select.tabIndex = -1;
    const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 're-state-trigger'; trigger.setAttribute('aria-label','Trạng thái hiện tại'); trigger.setAttribute('aria-expanded','false');
    trigger.setAttribute('data-validation-for', select.name);
    const menu = document.createElement('div'); menu.className = 're-state-menu'; menu.hidden = true;
    const close = () => { menu.hidden = true; trigger.setAttribute('aria-expanded','false'); };
    const sync = () => { trigger.textContent = select.selectedOptions[0]?.textContent || 'Chọn trạng thái'; menu.querySelectorAll('button').forEach(button => button.setAttribute('aria-pressed',String(button.dataset.value === select.value))); };
    [...select.options].forEach(option => {
        const button = document.createElement('button'); button.type = 'button'; button.className = 're-state-option'; button.textContent = option.textContent; button.dataset.value = option.value;
        button.addEventListener('click',()=>{select.value=option.value; select.dispatchEvent(new Event('change',{bubbles:true})); close(); trigger.focus();}); menu.append(button);
    });
    picker.append(trigger,menu); sync(); select.addEventListener('change',sync);
    trigger.addEventListener('click',()=>{ const open=menu.hidden; menu.hidden=!open; trigger.setAttribute('aria-expanded',String(open)); if(open) (menu.querySelector('[aria-pressed="true"]') || menu.querySelector('button'))?.focus(); });
    picker.addEventListener('keydown',event=>{
        if(event.key==='Escape'){event.preventDefault();close();trigger.focus();}
        if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){
            event.preventDefault();
            const buttons=[...menu.querySelectorAll('button')].filter(button=>!button.disabled);
            if(menu.hidden){menu.hidden=false;trigger.setAttribute('aria-expanded','true');}
            const index=buttons.indexOf(document.activeElement);
            const selected=buttons.findIndex(button=>button.dataset.value===select.value);
            const next=event.key==='Home'?0:event.key==='End'?buttons.length-1:index<0?Math.max(0,selected):(index+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length;
            buttons[next]?.focus();
        }
    });
    document.addEventListener('click',event=>{if(!picker.contains(event.target))close();});
    picker.addEventListener('focusout',event=>{if(!picker.contains(event.relatedTarget))close();});
})();

(() => {
    document.querySelectorAll("[data-vnd-input]").forEach(display => {
        const formatThousands = digits => digits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");

        const updateValue = formatDisplay => {
            const raw = display.value.trim();
            const isPlainInteger = /^\d+$/.test(raw);
            const isGroupedInteger = /^\d{1,3}(?:\.\d{3})+$/.test(raw);
            const digits = isGroupedInteger ? raw.replaceAll(".", "") : isPlainInteger ? raw : "";
            if (formatDisplay && digits) display.value = formatThousands(digits);
        };

        display.addEventListener("input", () => updateValue(false));
        display.addEventListener("blur", () => updateValue(true));
        const initialValue = display.value.trim();
        if (/^\d+$/.test(initialValue)) display.value = formatThousands(initialValue);
    });
})();
(() => {
    const building = document.querySelector('[data-room-building]');
    const location = document.querySelector('[data-room-location]');
    if (!building || !location) return;
    const updateLocation = () => {
        location.textContent = building.selectedOptions[0]?.dataset.location || '';
    };
    building.addEventListener('change', updateLocation);
    updateLocation();
})();

// Trang trí lựa chọn cơ sở; select gốc vẫn giữ giá trị và validation của form.
(() => {
    const select = document.querySelector('.room-create-page [data-room-building], .room-create-page select#ToaNhaId');
    if (!select || select.disabled) return;
    const wrapper = document.createElement('div');
    wrapper.className = 'rc-building-picker';
    select.before(wrapper);
    wrapper.append(select);
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.className = 'rc-building-trigger';
    trigger.setAttribute('data-validation-for', select.name);
    trigger.setAttribute('aria-haspopup', 'listbox');
    trigger.setAttribute('aria-labelledby', 'rc-building-label rc-building-value');
    const label = document.querySelector('label[for="' + select.id + '"]');
    if (label) label.id = 'rc-building-label';
    const value = document.createElement('span');
    value.id = 'rc-building-value';
    const prefix = document.createElement('span');
    prefix.className = 'rc-building-prefix';
    prefix.textContent = 'Cơ sở:';
    prefix.setAttribute('aria-hidden', 'true');
    trigger.append(prefix, value);
    const list = document.createElement('div');
    list.className = 'rc-building-options';
    list.id = 'rc-building-options';
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', 'Cơ sở');
    list.hidden = true;
    trigger.setAttribute('aria-controls', list.id);
    wrapper.append(trigger, list);
    const choices = Array.from(select.options).map(option => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'rc-building-option';
        button.setAttribute('role', 'option');
        button.textContent = option.textContent;
        button.disabled = option.disabled;
        button.addEventListener('click', () => {
            select.value = option.value;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            setOpen(false);
            trigger.focus();
        });
        list.append(button);
        return button;
    });
    const sync = () => {
        value.textContent = select.selectedOptions[0]?.textContent || 'Chọn cơ sở';
        trigger.classList.toggle('is-placeholder', !select.value);
        choices.forEach((button, index) => button.setAttribute('aria-selected', String(index === select.selectedIndex)));
    };
    const setOpen = open => {
        list.hidden = !open;
        trigger.setAttribute('aria-expanded', String(open));
        if (open) {
            const selected = choices[select.selectedIndex];
            (selected && !selected.disabled ? selected : choices.find(button => !button.disabled))?.focus();
        }
    };
    trigger.addEventListener('click', () => setOpen(list.hidden));
    wrapper.addEventListener('keydown', event => {
        if (event.key === 'Escape') { setOpen(false); trigger.focus(); event.preventDefault(); }
        if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
            event.preventDefault();
            if (list.hidden) {
                setOpen(true);
                if (event.key !== 'Home' && event.key !== 'End') return;
            }
            const enabled = choices.filter(button => !button.disabled);
            const index = enabled.indexOf(document.activeElement);
            const next = event.key === 'Home' ? 0 : event.key === 'End' ? enabled.length - 1
                : Math.max(0, Math.min(enabled.length - 1, index + (event.key === 'ArrowDown' ? 1 : -1)));
            enabled[next]?.focus();
        }
    });
    wrapper.addEventListener('focusout', event => { if (!wrapper.contains(event.relatedTarget)) setOpen(false); });
    document.addEventListener('pointerdown', event => { if (!wrapper.contains(event.target)) setOpen(false); });
    select.addEventListener('change', sync);
    select.form?.addEventListener('reset', () => setTimeout(sync, 0));
    select.classList.add('rc-native-select');
    select.tabIndex = -1;
    select.setAttribute('aria-hidden', 'true');
    select.addEventListener('focus', () => trigger.focus());
    sync();
    setOpen(false);
})();

(() => {
    const building = document.querySelector('[data-room-building]');
    const suggestions = document.getElementById('rc-floor-options');
    if (!building || !suggestions) return;
    const updateFloors = () => {
        suggestions.replaceChildren();
        const count = Number(building.selectedOptions[0]?.dataset.floors);
        if (!Number.isSafeInteger(count) || count <= 0) return;
        for (let floor = 0; floor <= Math.min(count, 1000); floor++) {
            const option = document.createElement('option');
            option.value = String(floor);
            suggestions.append(option);
        }
    };
    building.addEventListener('change', updateFloors);
    updateFloors();
})();

(() => {
    const amenities = document.querySelector('.rc-amenities');
    if (!amenities) return;
    const update = () => amenities.querySelector('.rc-amenity-count span').textContent = amenities.querySelectorAll('input[type="checkbox"]:checked').length;
    amenities.querySelectorAll('input[type="checkbox"]').forEach(input => input.addEventListener('change', update));
    update();
})();

(() => {
    const notes = document.getElementById('rc-room-notes-preview');
    const count = document.getElementById('rc-notes-count');
    if (notes && count) notes.addEventListener('input', () => count.textContent = notes.value.length);
    const input = document.getElementById('rc-photo-input');
    if (!input) return;
    const card = input.closest('.rc-photo-card');
    const drop = card.querySelector('.rc-photo-drop');
    const preview = card.querySelector('.rc-photo-previews');
    const error = card.querySelector('.rc-photo-error');
    let files = [], urls = [];
    const render = () => {
        urls.forEach(url => URL.revokeObjectURL(url)); urls = [];
        preview.replaceChildren();
        const transfer = new DataTransfer(); files.forEach(file => transfer.items.add(file)); input.files = transfer.files;
        card.querySelector('.rc-photo-meta b').textContent = files.length;
        files.forEach((file, index) => {
            const item = document.createElement('div'); item.className = 'rc-photo-preview';
            const img = document.createElement('img'); img.src = URL.createObjectURL(file); urls.push(img.src); img.alt = file.name;
            const remove = document.createElement('button'); remove.type = 'button'; remove.textContent = '×'; remove.setAttribute('aria-label', 'Xóa ảnh ' + file.name);
            remove.addEventListener('click', () => { files.splice(index, 1); render(); });
            item.append(img, remove);
            if (index === 0) { const tag = document.createElement('span'); tag.textContent = 'Ảnh đại diện'; item.append(tag); }
            preview.append(item);
        });
    };
    const add = incoming => {
        const selected = [...incoming];
        const invalid = selected.some(file => !['image/jpeg','image/png'].includes(file.type) || file.size > 5 * 1024 * 1024);
        if (invalid || files.length + selected.length > 8) {
            error.textContent = invalid ? 'Chọn ảnh JPG/PNG, tối đa 5 MB mỗi ảnh.' : 'Mỗi phòng tối đa 8 ảnh.';
            error.hidden = false; render(); return;
        }
        error.hidden = true; files.push(...selected); render();
    };
    input.addEventListener('change', () => add(input.files));
    ['dragenter','dragover'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.add('is-dragging'); }));
    drop.addEventListener('dragleave', () => drop.classList.remove('is-dragging'));
    drop.addEventListener('drop', event => { event.preventDefault(); drop.classList.remove('is-dragging'); add(event.dataTransfer.files); });
    window.addEventListener('pagehide', () => urls.forEach(url => URL.revokeObjectURL(url)));
})();

(() => {
    const section = document.querySelector('[data-room-services]');
    if (!section) return;
    const building = document.querySelector('[data-room-building]');
    const rows = section.querySelector('[data-service-rows]');
    const message = section.querySelector('[data-service-message]');
    const ready = section.querySelector('[data-services-ready]');
    let catalog = [], generation = 0;
    const initial = JSON.parse(section.querySelector('[data-service-state]').textContent);
    const addRow = (service, saved) => {
        if (rows.querySelector('[data-catalog="' + service.id + '"]')) return;
        const index = rows.children.length;
        const row = document.createElement('div'); row.className = 'rc-service-row'; row.dataset.catalog = service.id;
        const hidden = document.createElement('input'); hidden.type = 'hidden'; hidden.name = `DichVuMoi[${index}].CatalogId`; hidden.value = service.id;
        const label = document.createElement('label');
        const check = document.createElement('input'); check.type = 'checkbox'; check.name = `DichVuMoi[${index}].ApDung`; check.value = 'true'; check.checked = saved ? saved.ApDung : true;
        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); icon.classList.add('rc-service-icon'); icon.setAttribute('viewBox','0 0 24 24'); icon.setAttribute('aria-hidden','true');
        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('d', service.code === 'DIEN' ? 'M13 2 4 14h7l-1 8 10-13h-7l1-7Z' : service.code === 'NUOC' ? 'M12 3C9 7 5 11 5 15a7 7 0 0 0 14 0c0-4-4-8-7-12ZM8 15a4 4 0 0 0 4 4' : service.code === 'INTERNET' ? 'M2 8a16 16 0 0 1 20 0M5 12a11 11 0 0 1 14 0M8 16a6 6 0 0 1 8 0M12 20h.01' : 'M4 6h16M9 6V3h6v3M6 6l1 15h10l1-15M10 10v7M14 10v7'); icon.append(path);
        const text = document.createElement('span'); text.textContent = service.name; label.append(check, icon, text, hidden);
        const unit = document.createElement('div'); unit.className = 'rc-unit';
        const price = document.createElement('input'); price.name = `DichVuMoi[${index}].Gia`; price.inputMode = 'numeric'; price.value = saved ? saved.Gia : Number(service.price).toLocaleString('vi-VN'); price.setAttribute('aria-label','Đơn giá ' + service.name);
        price.addEventListener('input', () => { const digits = price.value.replace(/\D/g,''); price.value = digits ? BigInt(digits).toLocaleString('vi-VN') : ''; });
        const currency = document.createElement('span'); currency.textContent = 'đ'; unit.append(price,currency);
        const method = document.createElement('select'); method.name = `DichVuMoi[${index}].CachTinh`; method.setAttribute('aria-label','Cách tính ' + service.name);
        const meter = ['DIEN','NUOC'].includes(service.code);
        const options = meter ? [['THEO_CHI_SO', service.code === 'DIEN' ? 'kWh' : 'm³'],['THEO_NGUOI','người / tháng']] : [['CO_DINH','phòng / tháng'],['THEO_NGUOI','người / tháng']];
        options.forEach(([value,title]) => { const option=document.createElement('option');option.value=value;option.textContent=title;method.append(option); });
        method.value = saved ? saved.CachTinh : service.method;
        if (!method.value) method.value = options[0][0];
        const toggle = () => { price.disabled = method.disabled = !check.checked; row.classList.toggle('is-unselected', !check.checked); };
        check.addEventListener('change', toggle); row.append(label,unit,method); rows.append(row); toggle();
    };
    const load = async (restore = false) => {
        const current = ++generation; rows.replaceChildren(); ready.value='false';
        if (!building.value) { catalog=[];message.textContent='Chọn cơ sở để tải danh sách dịch vụ.';return; }
        message.textContent='Đang tải dịch vụ…';
        try {
            const response=await fetch('/PhongTro/DichVuThemPhong?toaNhaId='+encodeURIComponent(building.value));
            if (!response.ok) throw new Error();
            const result=await response.json();if(current!==generation)return;catalog=result;
            catalog.forEach(service => { const saved=restore ? initial.find(x=>x.CatalogId===service.id) : null;if(saved || (!restore && service.selected)) addRow(service,saved); });
            ready.value='true';message.textContent=catalog.length ? 'Điện, nước tính theo chỉ số công tơ hoặc đầu người; các phí còn lại thu cùng tiền thuê hàng tháng.' : 'Cơ sở chưa có dịch vụ được chốt giá. Cấu hình tại Dịch vụ & đơn giá trước khi thêm phí.';
        } catch { if(current===generation) message.textContent='Không tải được dịch vụ. Vui lòng chọn lại cơ sở.'; }
    };
    building.addEventListener('change',()=>load());
    section.closest('form').addEventListener('submit',event=>{if(building.value && ready.value!=='true'){event.preventDefault();message.textContent='Vui lòng tải dịch vụ thành công trước khi lưu phòng.';}});
    load(ready.value==='true');
})();
