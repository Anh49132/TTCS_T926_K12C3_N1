document.querySelectorAll('.manager-repair-detail .mrd-preview img').forEach(image => {
    const showFallback = () => {
        image.hidden = true;
        image.nextElementSibling.hidden = false;
        image.closest('.mrd-photo').querySelector('.mrd-photo-note').textContent = 'Không thể hiển thị ảnh xem trước';
    };
    image.addEventListener('error', showFallback);
    if (image.complete && image.naturalWidth === 0) showFallback();
});
