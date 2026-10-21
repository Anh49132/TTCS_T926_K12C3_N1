(() => {
    const page = document.querySelector('.ld-page');
    if (!page) return;
    const status = page.querySelector('[data-action-status]');
    const save = page.querySelector('[data-save-listing]');
    const storageKey = 'nhatot.savedListings';
    const id = page.dataset.listingId;
    function readSaved() {
        const value = JSON.parse(localStorage.getItem(storageKey) || '[]');
        return Array.isArray(value) ? value.filter(item => typeof item === 'string') : [];
    }
    function showSaved(saved) {
        save.setAttribute('aria-pressed', String(saved));
        save.querySelector('span').textContent = saved ? 'Đã lưu' : 'Lưu tin';
    }
    try { showSaved(readSaved().includes(id)); } catch { /* Storage may be disabled. */ }
    save.addEventListener('click', () => {
        try {
            const saved = new Set(readSaved());
            saved.has(id) ? saved.delete(id) : saved.add(id);
            localStorage.setItem(storageKey, JSON.stringify([...saved]));
            showSaved(saved.has(id));
            status.textContent = saved.has(id) ? 'Đã lưu tin trên thiết bị này.' : 'Đã bỏ lưu tin trên thiết bị này.';
        } catch { status.textContent = 'Không thể lưu tin trên thiết bị này. Bạn có thể đánh dấu trang trong trình duyệt.'; }
    });
    page.querySelector('[data-share-listing]').addEventListener('click', async () => {
        const url = new URL(location.pathname, location.origin).href;
        try {
            if (navigator.share) await navigator.share({ title: document.title, url });
            else if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(url);
                status.textContent = 'Đã sao chép liên kết tin đăng.';
            } else { status.textContent = 'Liên kết tin đăng: ' + url; }
        } catch (error) { if (error.name !== 'AbortError') status.textContent = 'Liên kết tin đăng: ' + url; }
    });
    const dialog = page.querySelector('.ld-photo-dialog');
    page.querySelectorAll('[data-open-gallery]').forEach(button => button.addEventListener('click', () => dialog?.showModal()));
    dialog?.addEventListener('click', event => { if (event.target === dialog) {
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    } });
    page.querySelectorAll('[data-gallery-thumb]').forEach(button => button.addEventListener('click', () => {
        const main = page.querySelector('[data-gallery-main]');
        main.src = button.dataset.full;
        main.alt = button.dataset.alt;
        page.querySelectorAll('[data-gallery-thumb]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
    }));
    const expand = page.querySelector('[data-expand-description]');
    expand?.addEventListener('click', () => {
        const expanded = expand.getAttribute('aria-expanded') !== 'true';
        page.querySelector('[data-description]').classList.toggle('is-expanded', expanded);
        expand.setAttribute('aria-expanded', String(expanded));
        expand.textContent = expanded ? 'Thu gọn ⌃' : 'Xem thêm ⌄';
    });
    const submit = page.querySelector('[data-request-submit]');
    const types = page.querySelectorAll('input[name="Form.LoaiYeuCau"]');
    function updateType() {
        if (submit) submit.textContent = [...types].find(input => input.checked)?.value === 'THUE_NGAY' ? 'Gửi yêu cầu thuê ngay' : 'Gửi yêu cầu xem phòng';
    }
    types.forEach(input => input.addEventListener('change', updateType));
    updateType();
})();
