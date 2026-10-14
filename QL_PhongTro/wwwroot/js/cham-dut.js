(() => {
    const section = document.querySelector('.st-termination[data-end]');
    if (!section) return;
    const form = section.closest('form'), date = form.elements.namedItem('Input.NgayTra');
    const fields = document.getElementById('termination-fields'), reason = document.getElementById('Input_LyDo');
    const other = document.getElementById('termination-other'), amount = document.getElementById('termination-amount');
    function sync() {
        const early = (date?.value || section.dataset.day) < section.dataset.end;
        document.getElementById('termination-status').textContent = early ? 'Trả trước hạn' : 'Trả đúng hạn hoặc sau hạn';
        fields.hidden = !early;
        fields.querySelectorAll('input,select,textarea').forEach(el => el.disabled = !early);
        reason.required = early;
        const isOther = early && reason.value === 'KHAC';
        other.hidden = !isOther; other.querySelector('textarea').required = isOther; other.querySelector('textarea').disabled = !isOther;
        const fine = early && form.querySelector('[name="Input.ApDungPhat"]:checked')?.value === 'true';
        amount.hidden = !fine; amount.querySelector('input').required = fine; amount.querySelector('input').disabled = !fine;
    }
    form.addEventListener('input', sync); form.addEventListener('change', sync); sync();
})();
