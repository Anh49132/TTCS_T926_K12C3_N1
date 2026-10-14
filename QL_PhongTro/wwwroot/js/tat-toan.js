(() => {
    const form = document.getElementById('settlement-form');
    if (!form) return;
    const body = document.getElementById('st-damage-rows'), template = document.getElementById('st-row-template');
    let timer, pending;
    const error = document.getElementById('st-estimate-error');
    const money = value => new Intl.NumberFormat('vi-VN').format(BigInt(value)) + ' đ';
    function reindex() {
        Array.from(body.rows).forEach((row, index) => row.querySelectorAll('input').forEach(input => {
            const field = input.dataset.field || input.name.split('.').pop();
            input.dataset.field = field; input.name = `Input.KhauTru[${index}].${field}`;
            input.removeAttribute('id');
        }));
    }
    function schedule() { clearTimeout(timer); if (pending) pending.abort(); timer = setTimeout(estimate, 350); }
    async function estimate() {
        if (pending) pending.abort(); pending = new AbortController();
        error.textContent = '';
        try {
            const response = await fetch(form.dataset.estimate, { method: 'POST', body: new FormData(form), signal: pending.signal });
            const data = await response.json();
            if (!response.ok) { error.textContent = data.error || 'Chưa thể tính dự toán. Vui lòng kiểm tra lại.'; return; }
            document.getElementById('st-damage-total').textContent = money(data.huHong);
            const debit = BigInt(data.traThem) > 0n, refund = BigInt(data.hoan) > 0n;
            document.getElementById('st-result').className = 'st-result ' + (debit ? 'is-debit' : 'is-credit');
            document.getElementById('st-result-label').textContent = debit ? 'Khách còn phải trả thêm' : refund ? 'Số tiền hoàn cho khách' : 'Hai bên vừa đủ, không thu/hoàn thêm';
            document.getElementById('st-result-value').textContent = money(debit ? data.traThem : data.hoan);
        } catch (e) { if (e.name !== 'AbortError') error.textContent = 'Chưa thể tải dự toán. Chọn “Xem dự tính” để đối chiếu.'; }
    }
    document.getElementById('st-add').addEventListener('click', () => {
        if (body.rows.length >= 100) { error.textContent = 'Tối đa 100 khoản hư hỏng.'; return; }
        body.append(template.content.cloneNode(true)); reindex(); body.lastElementChild.querySelector('input').focus();
    });
    body.addEventListener('click', event => { const button = event.target.closest('.st-remove'); if (button) { button.closest('tr').remove(); reindex(); schedule(); } });
    form.addEventListener('input', schedule); form.addEventListener('submit', reindex); reindex();
})();
