document.querySelectorAll('.manager-repair-detail .mrd-preview img').forEach(image => {
    const showFallback = () => {
        image.hidden = true;
        image.nextElementSibling.hidden = false;
        image.closest('.mrd-photo').querySelector('.mrd-photo-note').textContent = 'Không thể hiển thị ảnh xem trước';
    };
    image.addEventListener('error', showFallback);
    if (image.complete && image.naturalWidth === 0) showFallback();
});
document.querySelectorAll('[data-repair-target]').forEach(button => button.addEventListener('click', () => {
    const form = button.closest('form'), select = form.querySelector('[name="CapNhat.TrangThai"]');
    if (!Array.from(select.options).some(option => option.value === button.dataset.repairTarget)) return;
    select.value = button.dataset.repairTarget;
    form.querySelectorAll('[data-repair-target]').forEach(item => { const active = item === button; item.classList.toggle('is-selected', active); item.setAttribute('aria-pressed', String(active)); });
    form.querySelector('[name="CapNhat.GhiChu"]').focus();
}));
document.querySelectorAll('[data-repair-focus-reply]').forEach(button => button.addEventListener('click', () => document.querySelector('[name="PhanHoi.NoiDung"]')?.focus()));
document.querySelectorAll('.ru-thumb img,.repair-tenant-detail .mrd-photo img').forEach(image => {
    const fallback = () => { image.hidden = true; if (!image.parentElement.querySelector('[data-image-error]')) { const text = document.createElement('span'); text.dataset.imageError = ''; text.textContent = 'Ảnh chưa tải được'; image.parentElement.append(text); } };
    image.addEventListener('error', fallback); if (image.complete && image.naturalWidth === 0) fallback();
});
