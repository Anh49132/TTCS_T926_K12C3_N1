(() => {
  const form = document.querySelector('[data-repair-create]'); if (!form) return;
  const set = (key, value) => { const node = document.querySelector('[data-summary-' + key + ']'); if (node) node.textContent = value; };
  const update = () => {
    const room = form.querySelector('[name="Input.HopDongId"]')?.selectedOptions[0];
    set('room', room?.dataset.room || 'Chưa chọn'); set('building', room?.dataset.building || 'Chưa chọn');
    set('device', form.querySelector('[name="Input.LoaiThietBi"]').value.trim() || 'Chưa nhập');
    set('priority', form.querySelector('[name="Input.MucDo"]:checked')?.value === 'GAP' ? 'Gấp' : 'Thường');
    set('images', (form.querySelector('#repair-images')?.files.length || 0) + '/3');
    form.querySelector('[data-description-count]').textContent = form.querySelector('[name="Input.MoTa"]').value.length + '/5000';
  };
  form.addEventListener('input', update); form.addEventListener('change', update); form.addEventListener('repairimageschanged', update); update();
})();

// Bootstrap room picker; the original select remains the submitted value.
(() => {
  const form = document.querySelector('[data-repair-create]');
  const select = form?.querySelector('[name="Input.HopDongId"]');
  if (!select || !window.bootstrap?.Dropdown) return;
  const wrapper = document.createElement('div');
  wrapper.className = 'dropdown repair-room-picker';
  const trigger = document.createElement('button');
  trigger.type = 'button';
  trigger.id = select.id + '-picker';
  trigger.className = 'btn dropdown-toggle repair-room-toggle';
  trigger.setAttribute('data-bs-toggle', 'dropdown');
  trigger.setAttribute('aria-expanded', 'false');
  trigger.setAttribute('aria-required', 'true');
  const caption = document.createElement('span');
  trigger.append(caption);
  const menu = document.createElement('ul');
  menu.className = 'dropdown-menu repair-room-menu';
  menu.setAttribute('aria-labelledby', trigger.id);
  const choices = [];
  for (const option of select.options) {
    const item = document.createElement('li');
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'dropdown-item';
    button.textContent = option.textContent;
    button.disabled = option.disabled;
    button.dataset.value = option.value;
    button.addEventListener('click', () => {
      select.value = option.value;
      select.dispatchEvent(new Event('change', { bubbles: true }));
    });
    item.append(button); menu.append(item); choices.push(button);
  }
  select.after(wrapper); wrapper.append(trigger, menu);
  select.classList.add('repair-room-native');
  select.tabIndex = -1;
  const label = form.querySelector('label[for="' + select.id + '"]');
  if (label) label.htmlFor = trigger.id;
  function sync() {
    caption.textContent = select.selectedOptions[0]?.textContent || 'Chọn phòng và hợp đồng';
    choices.forEach(button => {
      const active = button.dataset.value === select.value;
      button.classList.toggle('active', active);
      if (active) button.setAttribute('aria-current', 'true');
      else button.removeAttribute('aria-current');
    });
    trigger.removeAttribute('aria-invalid');
  }
  select.addEventListener('change', sync);
  select.addEventListener('invalid', event => {
    event.preventDefault(); trigger.setAttribute('aria-invalid', 'true');
    trigger.focus(); bootstrap.Dropdown.getOrCreateInstance(trigger).show();
  });
  form.addEventListener('reset', () => setTimeout(sync, 0));
  sync();
})();
