(() => {
 const page=document.querySelector('[data-tenant-requests]'); if(!page)return;
 const rows=[...page.querySelectorAll('tbody tr')], tabs=[...page.querySelectorAll('[data-request-type]')].filter(x=>x.tagName==='BUTTON'), search=page.querySelector('[data-request-search]'), status=page.querySelector('[data-request-status-filter]'); let type='';
 const normalize=value=>value.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d');
 function filter(){let count=0;const term=normalize(search.value.trim());rows.forEach(row=>{const visible=(!type||row.dataset.requestType===type)&&(!status.value||row.dataset.requestStatus===status.value)&&normalize(row.dataset.search).includes(term);row.hidden=!visible;if(visible)count++;});page.querySelector('[data-request-empty]').hidden=count>0;page.querySelector('[data-filter-count]').textContent=count+' yêu cầu hiển thị.';}
 tabs.forEach(tab=>tab.addEventListener('click',()=>{type=tab.dataset.requestType;tabs.forEach(x=>x.setAttribute('aria-pressed',String(x===tab)));filter();}));search.addEventListener('input',filter);status.addEventListener('change',filter);filter();
})();

(() => {
  const page = document.querySelector('[data-tenant-requests]');
  if (!page || !window.bootstrap?.Dropdown) return;
  for (const id of ['trangThai']) {
    const select = page.querySelector('#' + id);
    if (!select) continue;
    const wrapper = document.createElement('div');
    wrapper.className = 'dropdown request-status-dropdown';
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.id = id + '-dropdown';
    trigger.className = 'btn dropdown-toggle request-status-toggle';
    trigger.setAttribute('data-bs-toggle', 'dropdown');
    trigger.setAttribute('aria-expanded', 'false');
    trigger.setAttribute('aria-label', 'Trạng thái');
    const label = page.querySelector('label[for="' + id + '"]');
    if (label) label.htmlFor = trigger.id;
    const caption = document.createElement('span');
    trigger.append(caption);
    const menu = document.createElement('ul');
    menu.className = 'dropdown-menu request-status-menu';
    menu.setAttribute('aria-labelledby', trigger.id);
    const choices = [];
    function sync() {
      caption.textContent = select.selectedOptions[0]?.textContent || '';
      choices.forEach(button => {
        const active = button.dataset.value === select.value;
        button.classList.toggle('active', active);
        if (active) button.setAttribute('aria-current', 'true');
        else button.removeAttribute('aria-current');
      });
    }
    for (const option of select.options) {
      const item = document.createElement('li');
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'dropdown-item';
      button.dataset.value = option.value;
      button.textContent = option.textContent;
      button.disabled = option.disabled;
      button.addEventListener('click', () => {
        select.value = option.value;
        select.dispatchEvent(new Event('change', { bubbles: true }));
      });
      item.append(button); menu.append(item); choices.push(button);
    }
    select.after(wrapper); wrapper.append(trigger, menu);
    select.hidden = true; select.tabIndex = -1;
    select.addEventListener('change', sync);
    select.form?.addEventListener('reset', () => setTimeout(sync, 0));
    sync();
    trigger.addEventListener('shown.bs.dropdown', () => menu.querySelector('.active')?.scrollIntoView({ block: 'nearest' }));
  }
})();
