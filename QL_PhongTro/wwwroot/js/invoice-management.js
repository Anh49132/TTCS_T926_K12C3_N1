(() => {
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