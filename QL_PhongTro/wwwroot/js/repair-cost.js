(() => {
    // This is immediate form feedback only. The service and SQLite remain authoritative.
    document.querySelectorAll('[data-repair-cost-form]').forEach(form => {
        const amount = form.elements.namedItem('ChiPhiInput.SoTien');
        const payer = form.elements.namedItem('ChiPhiInput.BenChiuPhi');
        const guidance = form.querySelector('[data-cost-guidance]');
        const maxVnd = '9223372036854775807';
        function showError(field, message) {
            const span = form.querySelector(`[data-valmsg-for="${field.name}"]`);
            if (span) {
                span.textContent = message;
                span.classList.toggle('field-validation-error', !!message);
                span.classList.toggle('field-validation-valid', !message);
            }
            field.classList.toggle('input-validation-error', !!message);
            field.setAttribute('aria-invalid', message ? 'true' : 'false');
            field.setCustomValidity(message);
            return !message;
        }
        function validateAmount() {
            const value = amount.value;
            const normalized = value.replace(/^0+/, '');
            const valid = /^[0-9]+$/.test(value) && normalized.length > 0 &&
                (normalized.length < maxVnd.length || normalized.length === maxVnd.length && normalized <= maxVnd);
            return showError(amount, valid ? '' : !value ? 'Nhập số tiền sửa chữa.' :
                'Số tiền phải là số nguyên VND từ 1 đến 9.223.372.036.854.775.807 đồng.');
        }
        function validatePayer() {
            return showError(payer, ['CHU_NHA', 'KHACH_THUE'].includes(payer.value) ? '' : 'Chọn bên chịu chi phí.');
        }
        function updateGuidance() {
            guidance.textContent = payer.value === 'KHACH_THUE' ? guidance.dataset.tenant :
                payer.value === 'CHU_NHA' ? guidance.dataset.owner : guidance.dataset.empty;
        }
        amount.addEventListener('input', validateAmount);
        payer.addEventListener('change', () => { validatePayer(); updateGuidance(); });
        form.addEventListener('submit', event => {
            const validAmount = validateAmount();
            const validPayer = validatePayer();
            if (!validAmount || !validPayer) { event.preventDefault(); form.reportValidity(); }
        });
        // Reconcile server field errors with the rendered values after a failed POST.
        // Leave unrelated state, permission and concurrency errors in the summary intact.
        if (amount.value) validateAmount();
        if (payer.value) validatePayer();
        updateGuidance();
    });
})();
