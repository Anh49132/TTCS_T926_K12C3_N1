(() => {
    const form = document.getElementById('checkout-input'), target = document.getElementById('checkout-estimate');
    if (!form || !target) return;
    let timer, pending;
    const money = value => new Intl.NumberFormat('vi-VN').format(BigInt(value)) + ' đ';
    async function update() {
        if (pending) pending.abort(); pending = new AbortController();
        if (['Input.NgayTra', 'Input.ChiSoDien', 'Input.ChiSoNuoc'].some(name => !form.elements.namedItem(name)?.value)) { target.textContent = 'Nhập ngày trả và hai chỉ số cuối để xem dự tính.'; return; }
        try {
            const response = await fetch(form.dataset.preview, { method: 'POST', body: new FormData(form), signal: pending.signal });
            const data = await response.json(); target.replaceChildren();
            if (!response.ok) { target.textContent = data.error || 'Chưa thể tính dự toán.'; return; }
            data.lines.forEach(line => { const row = document.createElement('p'); row.textContent = line.name + ': ' + money(line.amount); target.append(row); });
            const total = document.createElement('strong'); total.textContent = 'Tổng dự tính: ' + money(data.total); target.append(total);
        } catch (e) { if (e.name !== 'AbortError') target.textContent = 'Chọn tính hóa đơn để kiểm tra dự toán.'; }
    }
    form.addEventListener('input', () => { clearTimeout(timer); if (pending) pending.abort(); timer = setTimeout(update, 350); }); update();
})();
