(() => {
    const editor = document.getElementById('draft-editor');
    const dialog = document.getElementById('publish-confirmation');
    const open = document.getElementById('publish-open');
    const form = document.getElementById('publish-draft-form');
    if (!editor || !dialog || !open || !form) return;
    let dirty = false;
    const changed = () => {
        dirty = true;
        open.disabled = true;
        document.getElementById('publish-dirty').hidden = false;
    };
    editor.addEventListener('input', changed);
    editor.addEventListener('change', changed);
    open.addEventListener('click', () => { if (!dirty) dialog.showModal(); });
    document.getElementById('publish-close').addEventListener('click', () => dialog.close());
    const date = document.getElementById('publish-date');
    const due = document.getElementById('publish-due');
    date.addEventListener('change', () => { due.min = date.value; });
    due.min = date.value;
    form.addEventListener('submit', event => {
        if (dirty) { event.preventDefault(); dialog.close(); return; }
        form.querySelector('button[type="submit"]').disabled = true;
    });
})();

(() => {
 const form=document.getElementById('draft-editor'), list=document.getElementById('draft-adjustments');
 if(!form || !list) return;
 let index=list.querySelectorAll('fieldset').length;
 const dirty=()=>form.dispatchEvent(new Event('input',{bubbles:true}));
 const remove=button=>button.addEventListener('click',()=>{
  const field=button.closest('fieldset');
  if(button.dataset.removeAdjustment){const input=document.createElement('input');input.type='hidden';input.name='XoaKhoanIds';input.value=button.dataset.removeAdjustment;form.append(input);}
  field.querySelectorAll('input,textarea').forEach(x=>x.disabled=true);field.hidden=true;dirty();
 });
 list.querySelectorAll('[data-remove-adjustment]').forEach(remove);
 form.querySelectorAll('[data-add-adjustment]').forEach(button=>button.addEventListener('click',()=>{
  const i=index++,type=button.dataset.addAdjustment;
  const field=document.createElement('fieldset');field.className='mb-3';
  field.innerHTML='<legend class="fs-6">'+(type==='GIAM_TRU'?'Giảm trừ':'Phát sinh')+'</legend>';
  const idx=document.createElement('input');idx.type='hidden';idx.name='Khoan.Index';idx.value=i;field.append(idx);
  const hidden=document.createElement('input');hidden.type='hidden';hidden.name='Khoan['+i+'].LoaiKhoan';hidden.value=type;field.append(hidden);
  [['TenKhoan','Nội dung','text'],['SoTien','Số tiền (đ)','number'],['GhiChu','Ghi chú bắt buộc','text']].forEach(([key,label,type])=>{
   const l=document.createElement('label'),input=document.createElement('input');l.textContent=label;input.name='Khoan['+i+'].'+key;input.type=type;input.id='adjustment-'+i+'-'+key;l.htmlFor=input.id;input.className='form-control';input.required=true;
   if(type==='number'){input.min='1';input.step='1';}else input.maxLength=key==='TenKhoan'?200:1000;
   field.append(l,input);
  });
  const del=document.createElement('button');del.type='button';del.className='btn btn-outline-danger';del.textContent='Xóa khoản';remove(del);field.append(del);list.append(field);dirty();
 }));
 // Explicit collection indices keep model binding correct after removing any row.
 list.querySelectorAll('fieldset').forEach((field,i)=>{const input=document.createElement('input');input.type='hidden';input.name='Khoan.Index';input.value=i;field.append(input);});
})();
