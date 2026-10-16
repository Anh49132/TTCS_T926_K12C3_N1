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
