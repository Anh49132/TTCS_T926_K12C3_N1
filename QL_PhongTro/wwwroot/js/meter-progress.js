(() => {
const page=document.querySelector('[data-meter-progress]');if(!page)return;
// Enhance the native month selector; keep it available without JavaScript.
const monthSelect=page.querySelector('#thang');
if(monthSelect && window.bootstrap?.Dropdown){
 const wrapper=document.createElement('div');wrapper.className='dropdown mp-month-dropdown';
 const trigger=document.createElement('button');trigger.type='button';trigger.className='btn dropdown-toggle mp-month-toggle';
 trigger.id='progress-month-toggle';trigger.setAttribute('data-bs-toggle','dropdown');trigger.setAttribute('aria-expanded','false');trigger.setAttribute('aria-label','Chọn kỳ ghi chỉ số');
 const caption=document.createElement('span');trigger.append(caption);
 const menu=document.createElement('ul');menu.className='dropdown-menu mp-month-menu';menu.setAttribute('aria-labelledby',trigger.id);
 const choices=[];
 function syncMonth(){caption.textContent=monthSelect.selectedOptions[0]?.textContent || 'Chọn kỳ';choices.forEach(button=>{const selected=button.dataset.value===monthSelect.value;button.classList.toggle('active',selected);if(selected)button.setAttribute('aria-current','true');else button.removeAttribute('aria-current');});}
 for(const option of monthSelect.options){const item=document.createElement('li');const button=document.createElement('button');button.type='button';button.className='dropdown-item';button.dataset.value=option.value;button.textContent=option.textContent;button.addEventListener('click',()=>{monthSelect.value=option.value;monthSelect.dispatchEvent(new Event('change',{bubbles:true}));});item.append(button);menu.append(item);choices.push(button);}
 monthSelect.parentElement.insertAdjacentElement('afterend',wrapper);wrapper.append(trigger,menu);
 monthSelect.parentElement.hidden=true;monthSelect.tabIndex=-1;
 monthSelect.addEventListener('change',syncMonth);monthSelect.form?.addEventListener('reset',()=>setTimeout(syncMonth,0));syncMonth();
 trigger.addEventListener('shown.bs.dropdown',()=>menu.querySelector('.active')?.scrollIntoView({block:'nearest'}));
}
const search=page.querySelector('[data-building-search]'),status=page.querySelector('[data-building-status]'),rows=[...page.querySelectorAll('[data-export-row]')];
const normalize=value=>value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d');
function filter(){let count=0;rows.forEach(row=>{row.hidden=!normalize(row.dataset.buildingName).includes(normalize(search.value.trim())) || (!!status.value && row.dataset.state!==status.value);if(!row.hidden)count++;});page.querySelector('[data-progress-empty]').hidden=count>0;}
search.addEventListener('input',filter);status.addEventListener('change',filter);page.querySelector('[data-show-missing]')?.addEventListener('click',()=>{status.value='missing';search.value='';filter();page.querySelector('.mp-card').scrollIntoView({behavior:'smooth',block:'start'});});
const dialog=page.querySelector('.mp-drawer'),content=dialog.querySelector('[data-missing-content]');let controller;let opener;
page.querySelectorAll('[data-open-missing]').forEach(link=>link.addEventListener('click',async event=>{if(event.ctrlKey||event.metaKey||event.shiftKey||event.altKey)return;if(!dialog.showModal)return;event.preventDefault();controller?.abort();controller=new AbortController();opener=link;content.textContent='Đang tải danh sách phòng...';if(!dialog.open)dialog.showModal();const url=new URL(link.href);url.searchParams.set('drawer','true');try{const response=await fetch(url,{signal:controller.signal,credentials:'same-origin',headers:{'X-Requested-With':'XMLHttpRequest'}});if(!response.ok||response.redirected)throw new Error();const markup=await response.text();if(!markup.includes('missing-rooms-title'))throw new Error();content.innerHTML=markup;}catch(error){if(error.name==='AbortError')return;content.textContent='Không thể tải danh sách. ';const fallback=document.createElement('a');fallback.href=link.href;fallback.textContent='Mở trang phòng còn thiếu';content.append(fallback);}}));
dialog.querySelectorAll('[data-close-drawer]').forEach(button=>button.addEventListener('click',()=>dialog.close()));dialog.addEventListener('close',()=>{controller?.abort();opener?.focus();});
page.querySelector('[data-export-progress]').addEventListener('click',()=>{const selected=rows.filter(row=>!row.hidden);const escape=value=>'"'+value.replace(/^[=+@-]/,"'").replace(/"/g,'""')+'"';const lines=[['Tòa nhà','Người phụ trách','Phòng đang thuê','Đã chốt','Tiến độ','Còn thiếu','Trạng thái kỳ'],...selected.map(row=>[...row.cells].slice(0,7).map(cell=>cell.innerText.trim()))];const csv='\ufeff'+lines.map(line=>line.map(escape).join(',')).join('\r\n');const url=URL.createObjectURL(new Blob([csv],{type:'text/csv;charset=utf-8;'}));const link=document.createElement('a');link.href=url;link.download='tien-do-chi-so-'+page.querySelector('[name=nam]').value+'-'+page.querySelector('[name=thang]').value+'.csv';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);page.querySelector('[data-export-status]').textContent='Đã xuất '+selected.length+' tòa nhà đang hiển thị.';});
})();
