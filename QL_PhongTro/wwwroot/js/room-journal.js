(() => {
    const rows=[...document.querySelectorAll('.rj-event')];
    const search=document.getElementById('rj-search');
    const kind=document.getElementById('rj-kind');
    function filter(){const value=search.value.trim().toLocaleLowerCase('vi');rows.forEach(row=>row.hidden=!row.textContent.toLocaleLowerCase('vi').includes(value)||Boolean(kind.value&&row.dataset.kind!==kind.value));document.querySelectorAll('.rj-event-tabs button[data-kind]').forEach(button=>button.classList.toggle('active',button.dataset.kind===kind.value));}
    search?.addEventListener('input',filter);kind?.addEventListener('change',filter);
    document.querySelectorAll('.rj-event-tabs button[data-kind]').forEach(button=>button.addEventListener('click',()=>{kind.value=button.dataset.kind;filter();}));
    document.getElementById('rj-export')?.addEventListener('click',()=>{
        const escape=value=>'"'+(/^[=+@\-\t\r]/.test(value.trimStart())?"'":'')+value.replaceAll('"','""')+'"';
        const lines=[['Ngày giờ','Hoạt động','Chi tiết'],...rows.filter(row=>!row.hidden).map(row=>[row.querySelector('time').textContent.trim(),row.querySelector('h4').textContent.trim(),row.querySelector('.rj-event-content').textContent.trim()])];
        const url=URL.createObjectURL(new Blob(['\uFEFF'+lines.map(line=>line.map(escape).join(',')).join('\r\n')],{type:'text/csv;charset=utf-8'}));const a=document.createElement('a');a.href=url;a.download='nhat-ky-phong-trang-hien-tai.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
    });
})();

(() => {
    const select=document.getElementById('rj-kind');if(!select)return;
    const picker=document.createElement('div');picker.className='rj-kind-picker';select.before(picker);picker.append(select);select.tabIndex=-1;select.setAttribute('aria-hidden','true');
    const trigger=document.createElement('button');trigger.type='button';trigger.className='rj-kind-trigger';trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-expanded','false');trigger.setAttribute('aria-label','Loại sự kiện');
    const label=document.createElement('span');trigger.append(label);trigger.insertAdjacentHTML('beforeend','<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
    const menu=document.createElement('div');menu.className='rj-kind-menu';menu.hidden=true;menu.setAttribute('role','listbox');menu.setAttribute('aria-label','Loại sự kiện');picker.append(trigger,menu);
    const close=()=>{menu.hidden=true;trigger.setAttribute('aria-expanded','false');};
    function update(){label.textContent=select.selectedOptions[0]?.textContent||'';menu.replaceChildren();[...select.options].forEach(option=>{const button=document.createElement('button');button.type='button';button.className='rj-kind-option';button.textContent=option.textContent;button.setAttribute('role','option');button.setAttribute('aria-selected',String(option.selected));button.onclick=()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));close();trigger.focus();};button.onkeydown=event=>{const items=[...menu.children];const index=items.indexOf(button);if(['ArrowDown','ArrowUp'].includes(event.key)){event.preventDefault();items[(index+(event.key==='ArrowDown'?1:items.length-1))%items.length].focus();}if(event.key==='Escape'){close();trigger.focus();}};menu.append(button);});}
    trigger.onclick=()=>{const open=menu.hidden;menu.hidden=!open;trigger.setAttribute('aria-expanded',String(open));if(open)menu.querySelector('[aria-selected="true"]')?.focus();};
    trigger.onkeydown=event=>{if(event.key==='ArrowDown'){event.preventDefault();if(menu.hidden)trigger.click();}};
    select.addEventListener('change',update);
    document.querySelectorAll('.rj-event-tabs button[data-kind]').forEach(button=>button.addEventListener('click',update));
    document.addEventListener('click',event=>{if(!picker.contains(event.target))close();});picker.addEventListener('keydown',event=>{if(event.key==='Escape'){close();trigger.focus();}});update();
})();
