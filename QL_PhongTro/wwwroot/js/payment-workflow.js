(() => {
    document.querySelectorAll('[data-evidence-url]').forEach(block => {
        const image=block.querySelector('[data-evidence-image]'),error=block.querySelector('[data-evidence-error]');
        const load=async()=>{const response=await fetch(block.dataset.evidenceUrl,{credentials:'same-origin',cache:'no-store',headers:{Accept:'application/json'}});if(!response.ok)throw new Error();const data=await response.json();await new Promise((resolve,reject)=>{image.addEventListener('load',resolve,{once:true});image.addEventListener('error',reject,{once:true});image.src=data.url;});image.hidden=false;return data.url;};
        const fail=()=>{error.textContent='Không thể tải ảnh hoặc liên kết đã hết hạn. Bấm Xem ảnh để thử lại.';error.hidden=false;};
        image.addEventListener('error',()=>{image.hidden=true;fail();});
        if(block.hasAttribute('data-evidence-auto'))load().catch(fail);
        block.querySelector('[data-evidence-open]').addEventListener('click',async event=>{
            const button=event.currentTarget;button.disabled=true;error.hidden=true;
            try{const src=await load();const dialog=document.createElement('dialog');dialog.className='pw-evidence-dialog';dialog.setAttribute('aria-label','Ảnh chứng từ thanh toán');
                const close=document.createElement('button');close.type='button';close.className='btn btn-outline-secondary';close.textContent='Đóng';close.addEventListener('click',()=>dialog.close());
                const large=document.createElement('img');large.src=src;large.alt='Ảnh chứng từ thanh toán phóng to';dialog.append(close,large);document.body.append(dialog);dialog.addEventListener('close',()=>dialog.remove());dialog.addEventListener('click',e=>{if(e.target===dialog)dialog.close();});dialog.showModal();
            }catch{fail();}finally{button.disabled=false;}
        });
    });
    document.querySelectorAll('[data-payment-report]').forEach(form=>{
        const file=form.elements.anhChungTu, preview=form.querySelector('[data-proof-preview]');let objectUrl=null;
        if(!preview)return;
        const clear=()=>{if(objectUrl)URL.revokeObjectURL(objectUrl);objectUrl=null;preview.hidden=true;preview.querySelector('img').removeAttribute('src');};
        file.addEventListener('change',()=>{clear();const image=file.files[0];if(image && image.size<=5*1024*1024 && /\.(jpe?g|png)$/i.test(image.name)){objectUrl=URL.createObjectURL(image);preview.querySelector('img').src=objectUrl;preview.querySelector('[data-proof-filename]').textContent=`${image.name} · ${(image.size/1024/1024).toFixed(2)} MiB`;preview.hidden=false;}});
        preview.querySelector('[data-proof-remove]').addEventListener('click',()=>{file.value='';file.setCustomValidity('');clear();});
    });
    document.querySelectorAll('[data-cancel-payment]').forEach(form=>form.addEventListener('submit',event=>{if(!confirm('Hủy báo thanh toán này? Bạn có thể gửi lại báo cáo mới sau khi hủy.'))event.preventDefault();else form.querySelector('button[type=submit]').disabled=true;}));
    document.querySelectorAll('[data-confirm-payment]').forEach(form=>{
        const button=form.querySelector('button[type=submit]'),blocked=button.disabled,checks=[...form.querySelectorAll('input[type=checkbox]')];
        const sync=()=>{button.disabled=blocked || !checks.every(input=>input.checked);};checks.forEach(input=>input.addEventListener('change',sync));sync();
    });
    document.querySelectorAll('[data-confirm-payment],[data-reject-payment]').forEach(form=>form.addEventListener('submit',event=>{
        if(!form.reportValidity()){event.preventDefault();return;}
        const message=form.hasAttribute('data-confirm-payment')?'Bạn xác nhận đã kiểm tra và thực nhận đúng số tiền này?':'Từ chối báo thanh toán và gửi lý do cho khách?';
        if(!confirm(message)){event.preventDefault();return;}form.querySelector('button[type=submit]').disabled=true;
    }));
})();
