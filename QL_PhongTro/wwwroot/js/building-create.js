(() => {
 const form = document.getElementById('building-create-form'); if (!form) return;
 const field = id => form.querySelector('#' + id);
 const show = (name, value) => form.querySelector('[data-bc="' + name + '"]').textContent = value;
 const sync = () => {
  show('name', field('TenToaNha').value.trim() || 'Tên tòa nhà');
  const type = form.querySelector('input[name=LoaiHinhXemTruoc]:checked');
  show('type', 'Mã tạo sau khi lưu · ' + (type?.value === 'PHONG_TRO' ? 'Phòng trọ' : 'Căn hộ'));
  show('floors', field('SoTang').value || '—');
  show('address', ['DiaChi','PhuongXa','QuanHuyen','TinhThanh'].map(id => field(id).value.trim()).filter(Boolean).join(', ') || 'Chưa nhập địa chỉ');
  show('manager', field('QuanLyId').selectedOptions[0]?.textContent || 'Chưa phân công');
 };
 form.addEventListener('input',sync); form.addEventListener('change',sync); sync();
})();
