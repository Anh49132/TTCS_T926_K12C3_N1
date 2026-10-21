(() => {
    const page = document.querySelector('.tenant-account');
    if (!page) return;
    const status = page.querySelector('.ta-copy-status');
    page.querySelectorAll('[data-copy-account]').forEach(button => button.addEventListener('click', async () => {
        const id = button.dataset.copyAccount;
        try {
            if (!id || !navigator.clipboard) throw new Error('Clipboard unavailable');
            await navigator.clipboard.writeText(id);
            status.textContent = 'Đã sao chép ID tài khoản.';
        } catch {
            status.textContent = 'Chưa sao chép được. Bạn có thể chọn ID tài khoản và sao chép thủ công.';
        }
    }));
    page.querySelectorAll('.ta-tabs a').forEach(link => link.addEventListener('click', () => {
        page.querySelectorAll('.ta-tabs a').forEach(item => item.classList.toggle('is-active', item === link));
    }));
})();
