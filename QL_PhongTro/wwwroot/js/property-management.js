(() => {
    const dialog = document.getElementById('property-confirm');
    let pendingForm = null;
    document.querySelectorAll('form[data-confirm]').forEach(form => {
        form.addEventListener('submit', event => {
            if (form.dataset.confirmed === 'true') return;
            event.preventDefault();
            pendingForm = form;
            document.getElementById('property-confirm-message').textContent = form.dataset.confirm;
            dialog.returnValue = 'cancel';
            dialog.showModal();
        });
    });
    dialog.addEventListener('close', () => {
        const form = pendingForm;
        pendingForm = null;
        if (dialog.returnValue === 'confirm' && form) {
            form.dataset.confirmed = 'true';
            form.requestSubmit();
        }
    });
})();
