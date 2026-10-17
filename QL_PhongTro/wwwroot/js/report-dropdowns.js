(() => {
  const page = document.querySelector('.report-bootstrap');
  if (!page || !window.bootstrap?.Dropdown) return;
  for (const id of ['toaNhaId', 'tuThang', 'denThang']) {
    const select = page.querySelector('#' + id);
    if (!select) continue;
    const wrapper = document.createElement('div');
    wrapper.className = 'dropdown report-select-dropdown';
    const trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.id = id + '-dropdown';
    trigger.className = 'btn dropdown-toggle report-select-toggle';
    trigger.setAttribute('data-bs-toggle', 'dropdown');
    trigger.setAttribute('aria-expanded', 'false');
    trigger.setAttribute('aria-label', id === 'toaNhaId' ? 'Tòa nhà' : id === 'tuThang' ? 'Từ tháng' : 'Đến tháng');
    const label = page.querySelector('label[for="' + id + '"]');
    if (label) label.htmlFor = trigger.id;
    const caption = document.createElement('span');
    trigger.append(caption);
    const menu = document.createElement('ul');
    menu.className = 'dropdown-menu report-select-menu';
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
