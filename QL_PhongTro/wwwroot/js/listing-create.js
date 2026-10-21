(() => {
 const form=document.getElementById('listing-create-form');if(!form)return;
 const title=document.getElementById('TieuDe'),description=document.getElementById('NoiDung');
 const sync=()=>{document.getElementById('lc-preview-title').textContent=title.value.trim()||'Tiêu đề tin đăng';document.getElementById('lc-preview-description').textContent=description.value||'Mô tả phòng sẽ hiển thị tại đây.';document.getElementById('lc-description-count').textContent=description.value.length.toLocaleString('vi-VN')+' / 4.000 ký tự';};
 title.addEventListener('input',sync);description.addEventListener('input',sync);sync();
})();

(() => {
 const form=document.getElementById('listing-create-form');if(!form)return;
 const building=document.getElementById('lc-building'),room=document.getElementById('lc-room');
 const originalRoom=room.value,originalBuilding=building.value;
 const title=document.getElementById('TieuDe'),description=document.getElementById('NoiDung');const originalTitle=title.value,originalDescription=description.value;
 const filter=()=>Array.from(room.options).forEach(option=>{option.hidden=option.value!==''&&option.dataset.building!==building.value;option.disabled=option.value===''||option.hidden;});
 const navigate=id=>{
  if((title.value!==originalTitle||description.value!==originalDescription)&&!confirm('Đổi phòng sẽ bỏ nội dung chưa lưu của tin này. Tiếp tục?')){building.value=originalBuilding;room.value=originalRoom;filter();return;}
  window.location.assign('/TinDang/Tao?phongId='+encodeURIComponent(id));
 };
 building.addEventListener('change',()=>{filter();room.value='';form.querySelectorAll('.lc-draft,.lc-submit').forEach(button=>button.disabled=true);});
 room.addEventListener('change',()=>{if(room.value)navigate(room.value);});filter();
})();

(() => {
 const button=document.getElementById('lc-preview-more'),description=document.getElementById('lc-preview-description');if(!button||!description)return;
 button.addEventListener('click',()=>{const expanded=description.classList.toggle('is-expanded');button.setAttribute('aria-expanded',String(expanded));button.textContent=expanded?'Thu gọn':'Xem thêm';});
})();

(() => {
 ['lc-building','lc-room'].forEach(id=>{
 const select=document.getElementById(id);if(!select)return;
 const wrapper=document.createElement('div');wrapper.className='lc-select-picker';select.before(wrapper);wrapper.append(select);select.tabIndex=-1;select.setAttribute('aria-hidden','true');
 const trigger=document.createElement('button');trigger.type='button';trigger.className='lc-select-trigger';trigger.setAttribute('aria-label',select.getAttribute('aria-label'));trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-controls',id+'-options');
 const value=document.createElement('span');trigger.append(value);trigger.insertAdjacentHTML('beforeend','<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>');
 const menu=document.createElement('div');menu.id=id+'-options';menu.className='lc-select-menu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label',select.getAttribute('aria-label'));menu.hidden=true;wrapper.append(trigger,menu);
 const setOpen=open=>{menu.hidden=!open;trigger.setAttribute('aria-expanded',String(open));if(open)buttons.find(button=>button.getAttribute('aria-selected')==='true')?.focus();};
 const buttons=Array.from(select.options).map(option=>{
 const button=document.createElement('button');button.type='button';button.className='lc-select-option';button.setAttribute('role','option');button.textContent=option.textContent;
 button.addEventListener('click',()=>{select.value=option.value;setOpen(false);select.dispatchEvent(new Event('change',{bubbles:true}));sync();trigger.focus();});
 button.addEventListener('keydown',event=>{const visible=buttons.filter(choice=>!choice.hidden&&!choice.disabled);const index=visible.indexOf(button);if(['ArrowDown','ArrowUp','Home','End'].includes(event.key)){event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?visible.length-1:(index+(event.key==='ArrowDown'?1:-1)+visible.length)%visible.length;visible[next]?.focus();}if(event.key==='Escape'){event.preventDefault();setOpen(false);trigger.focus();}});menu.append(button);return button;
 });
 const sync=()=>{value.textContent=select.selectedOptions[0]?.textContent||'Chọn phòng';buttons.forEach((button,index)=>{const option=select.options[index];button.hidden=option.hidden;button.disabled=option.disabled;button.setAttribute('aria-selected',String(index===select.selectedIndex));});};
 trigger.addEventListener('click',()=>{sync();setOpen(menu.hidden);});trigger.addEventListener('keydown',event=>{if(event.key==='ArrowDown'||event.key==='ArrowUp'){event.preventDefault();sync();setOpen(true);}});
 document.addEventListener('click',event=>{if(!wrapper.contains(event.target))setOpen(false);});wrapper.addEventListener('focusout',event=>{if(!wrapper.contains(event.relatedTarget))setOpen(false);});select.addEventListener('change',sync);if(id==='lc-room')document.getElementById('lc-building').addEventListener('change',sync);sync();setOpen(false);
 });
})();

(() => {
 const select=document.getElementById('lc-duration');if(!select||select.tagName!=='SELECT')return;
 const wrap=document.createElement('div');wrap.className='lc-duration-picker';select.before(wrap);wrap.append(select);select.tabIndex=-1;select.setAttribute('aria-hidden','true');
 const trigger=document.createElement('button');trigger.type='button';trigger.className='lc-duration-trigger';trigger.setAttribute('aria-label','Thời hạn hiển thị');trigger.setAttribute('aria-haspopup','listbox');trigger.setAttribute('aria-controls','lc-duration-menu');trigger.innerHTML='<span>30 ngày</span><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5"/></svg>';
 const menu=document.createElement('div');menu.id='lc-duration-menu';menu.className='lc-duration-menu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label','Thời hạn hiển thị');menu.hidden=true;
 const option=document.createElement('button');option.type='button';option.className='lc-duration-option';option.setAttribute('role','option');option.setAttribute('aria-selected','true');option.textContent='30 ngày';menu.append(option);wrap.append(trigger,menu);
 const open=value=>{menu.hidden=!value;trigger.setAttribute('aria-expanded',String(value));if(value)option.focus();};
 trigger.addEventListener('click',()=>open(menu.hidden));trigger.addEventListener('keydown',event=>{if(event.key==='ArrowDown'){event.preventDefault();open(true);}});option.addEventListener('click',()=>{open(false);trigger.focus();});option.addEventListener('keydown',event=>{if(event.key==='Escape'){event.preventDefault();open(false);trigger.focus();}});
 document.addEventListener('click',event=>{if(!wrap.contains(event.target))open(false);});wrap.addEventListener('focusout',event=>{if(!wrap.contains(event.relatedTarget))open(false);});open(false);
})();
