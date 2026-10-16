(() => {
    const createChoice = document.getElementById('rm-create-choice');
    const addRoom = document.getElementById('rm-add-room');
    addRoom?.addEventListener('click', () => createChoice.showModal());
    createChoice?.querySelectorAll('.rm-choice-close, .rm-choice-cancel').forEach(button =>
        button.addEventListener('click', () => createChoice.close()));
    createChoice?.addEventListener('click', event => {
        if (event.target !== createChoice) return;
        const bounds = createChoice.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) createChoice.close();
    });
    createChoice?.addEventListener('close', () => addRoom.focus());
document.querySelectorAll('.rm-filter-select select').forEach(buildingSelect=>{
if(buildingSelect){
 const wrapper=buildingSelect.closest('.rm-filter-select');
 const trigger=document.createElement('button');trigger.type='button';trigger.className='rm-custom-trigger';trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-expanded','false');
 const menu=document.createElement('div');menu.className='rm-custom-menu';menu.hidden=true;menu.setAttribute('role','listbox');menu.setAttribute('aria-label',buildingSelect.getAttribute('aria-label')||'Bộ lọc');
 const options=[...buildingSelect.options];
 const buttons=options.map(option=>{const item=document.createElement('button');item.type='button';item.setAttribute('role','option');item.setAttribute('aria-selected',String(option.selected));item.textContent=option.text;item.addEventListener('click',()=>{buildingSelect.value=option.value;close();buildingSelect.form.requestSubmit();});menu.append(item);return item;});
 trigger.textContent=buildingSelect.selectedOptions[0]?.text||'Tất cả cơ sở';
 trigger.setAttribute('aria-label',(buildingSelect.getAttribute('aria-label')||'Bộ lọc')+': '+trigger.textContent);
 buildingSelect.hidden=true;wrapper.append(trigger,menu);wrapper.classList.add('rm-custom-select');
 function close(){menu.hidden=true;trigger.setAttribute('aria-expanded','false');}
 function open(){menu.hidden=false;trigger.setAttribute('aria-expanded','true');buttons[Math.max(0,buildingSelect.selectedIndex)].focus();}
 trigger.addEventListener('click',()=>menu.hidden?open():close());
 trigger.addEventListener('keydown',e=>{if(e.key==='ArrowDown'){e.preventDefault();open();}});
 menu.addEventListener('keydown',e=>{const index=buttons.indexOf(document.activeElement);if(e.key==='Escape'){e.preventDefault();close();trigger.focus();}else if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();buttons[(index+(e.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length].focus();}});
 document.addEventListener('click',e=>{if(!wrapper.contains(e.target))close();});
 wrapper.addEventListener('focusout',()=>setTimeout(()=>{if(!wrapper.contains(document.activeElement))close();},0));
}
});
const body=document.querySelector('.rm-table tbody'); if(!body)return;
let rows=[...body.rows],page=1;const size=document.getElementById('rm-page-size'),pages=document.getElementById('rm-pages');
function render(){
 const n=Number(size.value),count=Math.max(1,Math.ceil(rows.length/n));page=Math.max(1,Math.min(page,count));
 rows.forEach((row,i)=>row.hidden=i<(page-1)*n||i>=page*n);
 document.getElementById('rm-count').textContent=`Hiển thị ${rows.length? (page-1)*n+1:0}–${Math.min(page*n,rows.length)} trong ${rows.length} phòng`;
 document.getElementById('rm-empty').hidden=rows.length>0;
 const restoreFocus=pages.contains(document.activeElement);
 pages.replaceChildren();
 const visible=[...new Set([1,page-1,page,page+1,count])].filter(i=>i>=1&&i<=count).sort((a,b)=>a-b);
 let previous=0;
 for(const i of visible){
  if(previous && i>previous+1){const gap=document.createElement('span');gap.textContent='…';pages.append(gap);}
  const b=document.createElement('button');b.type='button';b.textContent=i;b.setAttribute('aria-label','Trang '+i);
  if(i===page)b.setAttribute('aria-current','page');
  b.addEventListener('click',()=>{page=i;render();});pages.append(b);previous=i;
 }
 if(restoreFocus)pages.querySelector('[aria-current="page"]')?.focus();
}
size.addEventListener('change',()=>{page=1;render();});document.getElementById('rm-sort').addEventListener('click',()=>{rows.sort((a,b)=>a.dataset.code.localeCompare(b.dataset.code,'vi',{numeric:true}));rows.forEach(row=>body.append(row));page=1;render();});
document.querySelectorAll('[data-view]').forEach(button=>button.addEventListener('click',()=>{document.querySelector('.rm-panel').classList.toggle('rm-grid',button.dataset.view==='grid');document.querySelectorAll('[data-view]').forEach(item=>item.setAttribute('aria-pressed',String(item===button)));}));
document.addEventListener('click',e=>document.querySelectorAll('.rm-actions details[open]').forEach(menu=>{if(!menu.contains(e.target))menu.open=false;}));
document.getElementById('rm-export').addEventListener('click',()=>{const quote=value=>'"'+String(value).replace(/^[=+@-]/,"'$&").replaceAll('"','""')+'"';const data=[['Phòng','Cơ sở','Khách thuê','Giá thuê / tháng','Tình trạng'],...rows.map(row=>[...row.cells].slice(0,5).map(cell=>cell.innerText.replaceAll('\n',' ')))];const url=URL.createObjectURL(new Blob(['\uFEFF'+data.map(row=>row.map(quote).join(',')).join('\r\n')],{type:'text/csv;charset=utf-8'}));const link=document.createElement('a');link.href=url;link.download='danh-sach-phong.csv';link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});render();
})();
