(() => {
    if (window.paymentNoticesInitialized) return;
    window.paymentNoticesInitialized = true;
    const bell = document.querySelector('[data-payment-bell]'), inbox = document.querySelector('[data-payment-inbox]');
    if (!bell && !inbox) return;
    const roots = [bell,inbox].filter(Boolean);
    const node = (tag,text,className) => { const el=document.createElement(tag);el.textContent=text;if(className)el.className=className;return el; };
    const csrf = () => document.querySelector('[data-payment-notice-csrf] [name=__RequestVerificationToken]').value;
    const post = async path => { const r=await fetch(path,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams({__RequestVerificationToken:csrf()})});if(!r.ok)throw new Error(); };
    const updateCount = async () => {
        if (!bell) return;
        try { const r=await fetch('/api/thong-bao/chua-doc',{credentials:'same-origin',cache:'no-store'});if(!r.ok)return;const d=await r.json();const count=bell.querySelector('[data-payment-unread]');count.textContent=d.count>99?'99+':d.count;count.hidden=d.count===0;bell.querySelector('button').setAttribute('aria-label',`Thông báo, ${d.count} chưa đọc`); } catch {}
    };
    const load = async (root, page=1, append=false) => {
        const list=root.querySelector('[data-payment-notice-list]');
        try {
            const response=await fetch(`/api/thong-bao/danh-sach?page=${page}&pageSize=${root===bell?6:20}`,{credentials:'same-origin',cache:'no-store'});
            if(!response.ok)throw new Error(); const {items}=await response.json(); if(!append)list.replaceChildren();
            if(!items.length && !append)list.append(node('p','Chưa có thông báo.','payment-notice-empty'));
            for(const item of items){
                const a=node('a','',`payment-notice ${item.daDoc?'':'unread'}`);
                const target=new URL(item.duongDan,location.origin);if(target.origin!==location.origin)continue;
                a.href=target.href;a.append(node('strong',item.tieuDe),node('p',item.noiDung),node('small',item.ngayTao),node('span',item.source==='PAYMENT'?'Kiểm tra ngay':'Xem chi tiết','payment-notice-action'));
                a.addEventListener('click',async event=>{event.preventDefault();try{await post(item.source==='PAYMENT'?`/api/thong-bao/thanh-toan/${item.id}/da-doc`:`/api/thong-bao/${item.id}/da-doc`);}catch{}location.assign(a.href);});
                list.append(a);
            }
            const more=root.querySelector('[data-payment-load-more]');if(more){more.hidden=items.length<20;more.dataset.nextPage=page+1;}
        } catch {if(!append)list.replaceChildren(node('p','Không thể tải thông báo. Vui lòng thử lại.','payment-notice-empty'));}
    };
    for(const root of roots){root.querySelector('[data-payment-read-all]').addEventListener('click',async()=>{try{await post('/api/thong-bao/tat-ca-da-doc');await load(root);await updateCount();}catch{root.querySelector('[data-payment-notice-list]').prepend(node('p','Không thể đánh dấu đã đọc. Vui lòng thử lại.','payment-notice-empty'));}});}
    if(bell){const button=bell.querySelector('.payment-bell-toggle'),panel=bell.querySelector('.payment-bell-panel');button.addEventListener('click',()=>{panel.hidden=!panel.hidden;button.setAttribute('aria-expanded',String(!panel.hidden));if(!panel.hidden)load(bell);});document.addEventListener('click',e=>{if(!bell.contains(e.target)){panel.hidden=true;button.setAttribute('aria-expanded','false');}});document.addEventListener('keydown',e=>{if(e.key==='Escape'){panel.hidden=true;button.setAttribute('aria-expanded','false');}});}
    if(inbox){load(inbox);inbox.querySelector('[data-payment-load-more]').addEventListener('click',e=>load(inbox,Number(e.target.dataset.nextPage||2),true));}
    updateCount();setInterval(()=>{if(!document.hidden){updateCount();if(bell && !bell.querySelector('.payment-bell-panel').hidden)load(bell);}},30000);
})();
