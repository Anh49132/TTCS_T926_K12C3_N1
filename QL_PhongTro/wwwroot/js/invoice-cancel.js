(() => {
    const open = document.getElementById('cancel-open');
    const dialog = document.getElementById('cancel-confirmation');
    const form = document.getElementById('cancel-invoice-form');
    if (!open || !dialog || !form) return;
    open.addEventListener('click', () => dialog.showModal());
    document.getElementById('cancel-close').addEventListener('click', () => dialog.close());
    const reason = document.getElementById('cancel-reason');
    reason.addEventListener('input', () => reason.setCustomValidity(reason.value.trim() ? '' : 'Nhập lý do hủy.'));
    form.addEventListener('submit', () => { form.querySelector('button[type="submit"]').disabled = true; });
})();
