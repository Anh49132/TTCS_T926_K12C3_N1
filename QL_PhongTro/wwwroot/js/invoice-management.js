(() => {
// Progressive Bootstrap dropdowns retain native GET filter values.
const filterForm=document.querySelector('.invoice-bootstrap .flow-filters');
if(filterForm && window.bootstrap?.Dropdown){
 for(const name of ['toaNhaId','thang','trangThai']){
  const select=filterForm.querySelector('select[name="'+name+'"]');if(!select)continue;
  const wrapper=document.createElement('div');wrapper.className='dropdown invoice-filter-dropdown';
  const trigger=document.createElement('button');trigger.type='button';trigger.className='btn dropdown-toggle invoice-filter-toggle';trigger.id='invoice-filter-'+name;trigger.setAttribute('data-bs-toggle','dropdown');trigger.setAttribute('aria-expanded','false');trigger.setAttribute('aria-label',select.getAttribute('aria-label') || name);
  const caption=document.createElement('span');trigger.append(caption);
  const menu=document.createElement('ul');menu.className='dropdown-menu invoice-filter-menu';menu.setAttribute('aria-labelledby',trigger.id);
  const choices=[];
  function sync(){caption.textContent=select.selectedOptions[0]?.textContent || '';choices.forEach(button=>{const current=button.dataset.value===select.value;button.classList.toggle('active',current);if(current)button.setAttribute('aria-current','true');else button.removeAttribute('aria-current');});}
  for(const option of select.options){const item=document.createElement('li');const button=document.createElement('button');button.type='button';button.className='dropdown-item';button.dataset.value=option.value;button.textContent=option.textContent;button.disabled=option.disabled;button.addEventListener('click',()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));});item.append(button);menu.append(item);choices.push(button);}
  select.insertAdjacentElement('afterend',wrapper);wrapper.append(trigger,menu);select.hidden=true;select.tabIndex=-1;select.addEventListener('change',sync);filterForm.addEventListener('reset',()=>setTimeout(sync,0));sync();
  trigger.addEventListener('shown.bs.dropdown',()=>menu.querySelector('.active')?.scrollIntoView({block:'nearest'}));
 }
}

const form=document.getElementById('management-batch'),dialog=document.getElementById('management-confirm');
if(!form || !dialog) return;
const boxes=[...form.querySelectorAll('[name="selectedInvoiceIds"]')],error=form.querySelector('[data-management-error]');
const update=()=>document.querySelectorAll('[data-management-count]').forEach(x=>x.textContent='Đã chọn '+boxes.filter(x=>x.checked).length+' hóa đơn');
boxes.forEach(x=>x.addEventListener('change',update));
form.querySelector('[data-management-all]').addEventListener('change',e=>{boxes.forEach(x=>x.checked=e.target.checked);update();});
form.querySelector('[data-management-open]').addEventListener('click',()=>{
const selected=boxes.filter(x=>x.checked);
if(!selected.length || selected.length>50){error.textContent='Chọn từ 1 đến 50 bản nháp.';return;}
const first=selected[0];
if(selected.some(x=>x.dataset.building!==first.dataset.building || x.dataset.year!==first.dataset.year || x.dataset.month!==first.dataset.month)){error.textContent='Chọn các hóa đơn cùng tòa nhà và kỳ áp dụng để phát hành một đợt.';return;}
error.textContent='';
[['toaNhaId','building'],['nam','year'],['thang','month']].forEach(([name,key])=>form.querySelector('[name="'+name+'"]').value=first.dataset[key]);
dialog.querySelector('[data-management-total]').textContent=new Intl.NumberFormat('vi-VN').format(selected.reduce((sum,x)=>sum+BigInt(x.dataset.total),0n))+' đ';
dialog.querySelector('[data-management-period]').textContent=first.dataset.month+'/'+first.dataset.year;
dialog.querySelector('[name="xacNhan"]').checked=false;dialog.showModal();
});
form.querySelector('[data-management-close]').addEventListener('click',()=>dialog.close());
const issue=form.querySelector('[name="ngayPhatHanh"]'),due=form.querySelector('[name="hanThanhToan"]');
issue.addEventListener('change',()=>{due.min=issue.value;due.setCustomValidity(due.value<issue.value?'Hạn thanh toán phải từ ngày phát hành trở đi.':'');});
due.addEventListener('change',()=>due.setCustomValidity(due.value<issue.value?'Hạn thanh toán phải từ ngày phát hành trở đi.':''));
form.addEventListener('submit',()=>form.querySelector('button[type="submit"]').disabled=true);
})();