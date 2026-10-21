(() => {
    const page = document.querySelector('[data-tenant-repairs]');
    if (!page) return;
    if(page.classList.contains('owner-repair-list') && window.bootstrap?.Dropdown){
        for(const id of ['trl-status','trl-priority']){
            const select=page.querySelector('#'+id);if(!select)continue;
            const wrapper=document.createElement('div');wrapper.className='dropdown repair-select-dropdown';
            const trigger=document.createElement('button');trigger.type='button';trigger.id=id+'-dropdown';trigger.className='btn dropdown-toggle repair-select-toggle';trigger.setAttribute('data-bs-toggle','dropdown');trigger.setAttribute('aria-expanded','false');trigger.setAttribute('aria-label',id==='trl-status'?'Lọc trạng thái':'Lọc mức độ');
            const caption=document.createElement('span');trigger.append(caption);
            const menu=document.createElement('ul');menu.className='dropdown-menu repair-select-menu';menu.setAttribute('aria-labelledby',trigger.id);const choices=[];
            function sync(){caption.textContent=select.selectedOptions[0]?.textContent || '';choices.forEach(button=>{const current=button.dataset.value===select.value;button.classList.toggle('active',current);if(current)button.setAttribute('aria-current','true');else button.removeAttribute('aria-current');});}
            for(const option of select.options){const item=document.createElement('li');const button=document.createElement('button');button.type='button';button.className='dropdown-item';button.dataset.value=option.value;button.textContent=option.textContent;button.addEventListener('click',()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));});item.append(button);menu.append(item);choices.push(button);}
            select.after(wrapper);wrapper.append(trigger,menu);select.hidden=true;select.tabIndex=-1;select.addEventListener('change',sync);sync();
            page.querySelector('[data-reset-filters]')?.addEventListener('click',()=>setTimeout(sync,0));
            select.form?.addEventListener('reset',()=>setTimeout(sync,0));
        }
    }
    const rows = [...page.querySelectorAll('[data-repair-row]')];
    const search = page.querySelector('#trl-search');
    const status = page.querySelector('#trl-status');
    const priority = page.querySelector('#trl-priority');
    const empty = page.querySelector('[data-repair-empty]');
    const reset = page.querySelector('[data-reset-filters]');
    const normalize = text => text.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    const searchable = rows.map(row => normalize(row.cells[0].textContent + ' ' + row.cells[1].textContent));
    const count = page.querySelector('[data-repair-count]');
    const suffix = count.textContent.includes('trên trang') ? ' trên trang' : '';
    function filter() {
        const query = normalize(search.value.trim());
        let visible = 0;
        rows.forEach((row, index) => {
            row.hidden = !(searchable[index].includes(query) && (!status.value || row.dataset.status === status.value) && (!priority.value || row.dataset.priority === priority.value));
            if (!row.hidden) visible++;
        });
        empty.hidden = visible > 0;
        const filtered = !!(query || status.value || priority.value);
        reset.hidden = !filtered;
        if (rows.length) {
            page.querySelector('[data-empty-title]').textContent = 'Không có báo hỏng phù hợp.';
            page.querySelector('[data-empty-help]').textContent = 'Thử từ khóa khác hoặc xóa bộ lọc để xem lại danh sách' + suffix + '.';
        }
        count.textContent = `Hiển thị ${visible} / ${rows.length} báo hỏng${suffix}`;
        page.querySelector('[data-filter-state]').textContent = filtered ? 'Đang áp dụng bộ lọc' : 'Đang hiển thị tất cả' + suffix;
    }
    page.querySelector('[data-repair-filters]').addEventListener('submit', event => {
        event.preventDefault();
        filter();
    });
    reset.addEventListener('click', () => { search.value = ''; status.value = ''; priority.value = ''; filter(); search.focus(); });
    page.querySelector('[data-repair-filters]').hidden = false;
})();
