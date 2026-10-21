(() => {
 const page=document.querySelector('.utility-config-page'); if(!page)return;
 const sync=()=>{
 let total=0n,valid=true;
 page.querySelectorAll('[data-utility]').forEach(section=>{
 const method=section.querySelector('[data-method]').value;
 const prefix=section.querySelector('[data-method]').name.split('.')[0];
 const input=section.querySelector('[data-price="'+method+'"] input');
 const raw=input.value.trim(); const price=/^\d+$/.test(raw)?BigInt(raw):0n;
 const count=method==='THEO_NGUOI'?2n:prefix==='Dien'?100n:5n;
 const meter=page.querySelector('[data-uc-meter='+prefix+']'); if(meter)meter.hidden=method==='THEO_NGUOI';
 const unit=method==='THEO_NGUOI'?'người / tháng':prefix==='Dien'?'kWh':'m³';
 page.querySelector('[data-uc-method-label='+prefix+']').textContent=(prefix==='Dien'?'TIỀN ĐIỆN':'TIỀN NƯỚC')+' · '+(method==='THEO_NGUOI'?'KHOÁN THEO NGƯỜI':'THEO ĐỒNG HỒ RIÊNG');
 const ok=price>0n&&price<=9223372036854775807n;valid=valid&&ok;
 page.querySelector('[data-uc-summary="'+prefix+'"]').textContent=ok?price.toLocaleString('vi-VN')+' đ / '+unit:'Chưa nhập giá';
 page.querySelector('[data-uc-formula="'+prefix+'"]').textContent=ok?count+' '+unit+' × '+price.toLocaleString('vi-VN')+' đ':'Nhập đơn giá để xem ví dụ.';
 page.querySelector('[data-uc-example="'+prefix+'"]').textContent=ok?(price*count).toLocaleString('vi-VN')+' đ':'—';
 if(ok)total+=price*count;
 });
 page.querySelector('[data-uc-total]').textContent=valid?total.toLocaleString('vi-VN')+' đ':'—';
 };
 page.addEventListener('input',sync);page.addEventListener('change',sync);sync();
})();

(() => {
 document.querySelectorAll('.utility-config-page [data-method]').forEach(select=>{
 const group=document.createElement('div');group.className='uc-method-options';group.setAttribute('role','group');group.setAttribute('aria-label',select.id.startsWith('Dien')?'Cách tính tiền điện':'Cách tính tiền nước');
 const buttons=Array.from(select.options).map(option=>{const button=document.createElement('button');button.type='button';button.textContent=option.value==='THEO_CHI_SO'?'Theo đồng hồ riêng':'Khoán theo người';button.addEventListener('click',()=>{select.value=option.value;select.dispatchEvent(new Event('change',{bubbles:true}));});group.append(button);return button;});
 const sync=()=>buttons.forEach((button,i)=>button.setAttribute('aria-pressed',String(i===select.selectedIndex)));
 select.after(group);select.hidden=true;select.addEventListener('change',sync);sync();
 });
})();

(() => {
 const section=document.querySelector('.uc-electric');if(!section)return;
 const method=section.querySelector('[data-method]');const options=section.querySelector('.uc-method-options');
 options.classList.add('uc-electric-options');
 ['Theo bậc thang','Khoán theo phòng'].forEach(text=>{const button=document.createElement('button');button.type='button';button.textContent=text;button.disabled=true;button.title='Chưa hỗ trợ cách tính này';options.append(button);});
 const group=section.querySelector('[data-price="THEO_CHI_SO"]');
 const label=group.querySelector('label');label.textContent='Đơn giá *';
 const input=group.querySelector('input');const wrap=document.createElement('div');wrap.className='uc-price-unit';input.before(wrap);wrap.append(input);const suffix=document.createElement('span');suffix.textContent='đ / kWh';wrap.append(suffix);
 const unit=document.createElement('div');unit.className='uc-reading-unit';unit.innerHTML='<label>Đơn vị ghi chỉ số</label><div>kWh (số điện)<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="10" width="12" height="11" rx="2"/><path d="M9 10V6a3 3 0 0 1 6 0v4M12 14v3"/></svg></div>';
 const row=document.createElement('div');row.className='uc-electric-price-row';group.before(row);row.append(group,unit);
 const formula=document.createElement('p');formula.className='uc-electric-formula';section.append(formula);
 const sync=()=>{unit.hidden=method.value!=='THEO_CHI_SO'; const price=section.querySelector('[data-price="'+method.value+'"] input').value;const format=/^\d+$/.test(price)?BigInt(price).toLocaleString('vi-VN'):'…';formula.textContent=method.value==='THEO_CHI_SO'?'▦ Tiền điện = (Chỉ số mới − Chỉ số cũ) × '+format+' đ / kWh':'▦ Tiền điện = Số người × '+format+' đ / người / tháng';};
 section.addEventListener('input',sync);section.addEventListener('change',sync);sync();
})();

(() => {
 const section=document.querySelector('.uc-water');if(!section)return;
 const method=section.querySelector('[data-method]');const options=section.querySelector('.uc-method-options');options.classList.add('uc-water-options');
 const disabled=document.createElement('button');disabled.type='button';disabled.textContent='Khoán theo phòng';disabled.disabled=true;disabled.title='Chưa hỗ trợ cách tính này';options.append(disabled);
 const unit=document.createElement('div');unit.className='uc-reading-unit';unit.innerHTML='<label>Đơn vị ghi chỉ số</label><div><span data-water-unit>m³ (khối nước)</span><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="10" width="12" height="11" rx="2"/><path d="M9 10V6a3 3 0 0 1 6 0v4M12 14v3"/></svg></div>';
 const row=document.createElement('div');row.className='uc-water-price-row';section.querySelector('[data-price]').before(row);
 section.querySelectorAll('[data-price]').forEach(group=>{
  const input=group.querySelector('input');group.querySelector('label').textContent='Đơn giá *';
  const wrap=document.createElement('div');wrap.className='uc-price-unit';input.before(wrap);wrap.append(input);
  const suffix=document.createElement('span');suffix.textContent=group.dataset.price==='THEO_CHI_SO'?'đ / m³':'đ / người';wrap.append(suffix);row.append(group);
 });row.append(unit);
 const formula=document.createElement('p');formula.className='uc-electric-formula';section.append(formula);
 const sync=()=>{
  const meter=method.value==='THEO_CHI_SO';unit.querySelector('[data-water-unit]').textContent=meter?'m³ (khối nước)':'người / tháng';
  const raw=section.querySelector('[data-price="'+method.value+'"] input').value;
  const price=/^\d+$/.test(raw)?BigInt(raw).toLocaleString('vi-VN'):'…';
  formula.textContent=meter?'▦ Tiền nước = (Chỉ số mới − Chỉ số cũ) × '+price+' đ / m³':'▦ Tiền nước = Số người × '+price+' đ / người / tháng';
 };section.addEventListener('input',sync);section.addEventListener('change',sync);sync();
})();
