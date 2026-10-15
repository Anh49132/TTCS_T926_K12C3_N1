(() => {
 const page=document.querySelector('.listing-manage-page');if(!page)return;
 const rows=[...page.querySelectorAll('[data-lm-row]')];let current=1;
 const get=id=>document.getElementById(id);
 const render=()=>{
 const query=get('lm-search').value.trim().toLocaleLowerCase('vi');const status=get('lm-status').value;const building=get('lm-building').value;
 const matches=rows.filter(row=>(!query||row.dataset.search.includes(query))&&(!status||row.dataset.status===status)&&(!building||row.dataset.building===building));
 matches.sort((a,b)=>get('lm-sort').value==='price'? (BigInt(a.dataset.price)<BigInt(b.dataset.price)?-1:BigInt(a.dataset.price)>BigInt(b.dataset.price)?1:0):(BigInt(a.dataset.date)>BigInt(b.dataset.date)?-1:BigInt(a.dataset.date)<BigInt(b.dataset.date)?1:0));
 const size=Number(get('lm-size').value);const pages=Math.max(1,Math.ceil(matches.length/size));current=Math.min(current,pages);
 rows.forEach(row=>row.hidden=true);const body=page.querySelector('tbody');matches.forEach((row,i)=>{body.append(row);row.hidden=i<(current-1)*size||i>=current*size;});
 get('lm-empty').hidden=matches.length>0;get('lm-count').textContent=matches.length?'Hiển thị '+((current-1)*size+1)+'–'+Math.min(current*size,matches.length)+' trong '+matches.length+' phòng / tin':'0 kết quả';
 get('lm-page').textContent=current+' / '+pages;get('lm-prev').disabled=current<=1;get('lm-next').disabled=current>=pages;
 page.querySelectorAll('.lm-tabs [data-lm-tab]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.lmTab===status)));
 };
 ['lm-search','lm-building','lm-status','lm-sort','lm-size'].forEach(id=>get(id).addEventListener(id==='lm-search'?'input':'change',()=>{current=1;render();}));
 page.querySelectorAll('[data-lm-tab]').forEach(button=>button.addEventListener('click',()=>{get('lm-status').value=button.dataset.lmTab;current=1;render();}));
 get('lm-prev').addEventListener('click',()=>{current--;render();});get('lm-next').addEventListener('click',()=>{current++;render();});
 render();
})();

(() => {
 const page=document.querySelector('.listing-manage-page');if(!page)return;
 ['lm-building','lm-status','lm-sort'].forEach(id=>{
  const select=document.getElementById(id);if(!select)return;
  const wrap=document.createElement('div');wrap.className='lm-select-picker';select.before(wrap);wrap.append(select);
  select.tabIndex=-1;select.setAttribute('aria-hidden','true');
  const trigger=document.createElement('button');trigger.type='button';trigger.className='lm-select-trigger';trigger.setAttribute('aria-label',select.getAttribute('aria-label'));trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-controls',id+'-menu');
  const value=document.createElement('span'); if(id==='lm-sort'){trigger.classList.add('lm-sort-trigger');trigger.insertAdjacentHTML('beforeend','<svg class="lm-sort-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M7 4v15m-3-3 3 3 3-3M12 5h8m-8 4h6m-6 4h4"/></svg>');} trigger.append(value);trigger.insertAdjacentHTML('beforeend','<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
  const menu=document.createElement('div');menu.className='lm-select-menu';menu.id=id+'-menu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label',select.getAttribute('aria-label'));menu.hidden=true;wrap.append(trigger,menu);
  const setOpen=open=>{menu.hidden=!open;trigger.setAttribute('aria-expanded',String(open));if(open)choices[Math.max(0,select.selectedIndex)]?.focus();};
  const choices=Array.from(select.options).map((option,index)=>{
   const button=document.createElement('button');button.type='button';button.className='lm-select-option';button.setAttribute('role','option');button.textContent=option.textContent;
   button.addEventListener('click',()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));setOpen(false);trigger.focus();});
   button.addEventListener('keydown',event=>{
    if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?choices.length-1:(index+(event.key==='ArrowDown'?1:-1)+choices.length)%choices.length;choices[next].focus();}
    if(event.key==='Escape'){event.preventDefault();setOpen(false);trigger.focus();}
   });menu.append(button);return button;
  });
  const sync=()=>{value.textContent=select.selectedOptions[0]?.textContent||'';choices.forEach((choice,index)=>choice.setAttribute('aria-selected',String(index===select.selectedIndex)));};
  trigger.addEventListener('click',()=>setOpen(menu.hidden));trigger.addEventListener('keydown',event=>{if(event.key==='ArrowDown'||event.key==='ArrowUp'){event.preventDefault();setOpen(true);}});
  document.addEventListener('click',event=>{if(!wrap.contains(event.target))setOpen(false);});wrap.addEventListener('focusout',event=>{if(!wrap.contains(event.relatedTarget))setOpen(false);});
  select.addEventListener('change',sync);page.querySelectorAll('[data-lm-tab],[data-lm-create]').forEach(button=>button.addEventListener('click',sync));sync();setOpen(false);
 });
})();

(() => {const menus=[...document.querySelectorAll('.lm-action-menu')];menus.forEach(menu=>menu.addEventListener('toggle',()=>{if(menu.open)menus.forEach(other=>{if(other!==menu)other.open=false;});}));document.addEventListener('click',event=>menus.forEach(menu=>{if(!menu.contains(event.target))menu.open=false;}));document.addEventListener('keydown',event=>{if(event.key==='Escape')menus.forEach(menu=>{if(menu.open){menu.open=false;menu.querySelector('summary').focus();}});});})();

// Prevent accidental repeated clicks; the server also handles repeated requests.
document.querySelectorAll('.lm-publish-form').forEach(form=>form.addEventListener('submit',()=>{const button=form.querySelector('button[type=submit]');if(button){button.disabled=true;button.textContent='Đang đăng…';}}));
window.addEventListener('pageshow',()=>document.querySelectorAll('.lm-publish-button').forEach(button=>{button.disabled=false;button.textContent='Đăng tin';}));
