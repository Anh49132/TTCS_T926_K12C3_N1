(() => {
    const form = document.getElementById('checkout-input');
    const target = document.getElementById('checkout-estimate');
    if (!form || !target) return;
    let timer, pending;
    const money = value => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(BigInt(value)) + ' đ';
    async function update() {
        if (pending) pending.abort();
        pending = new AbortController();
        const fields = ['Input.NgayTra', 'Input.ChiSoDien', 'Input.ChiSoNuoc'];
        if (fields.some(name => !form.elements.namedItem(name)?.value)) {
            target.textContent = 'Nhập ngày trả và hai chỉ số cuối để xem dự tính.';
            return;
        }
        target.textContent = 'Đang tính dự toán…';
        try {
            const response = await fetch(form.dataset.preview, { method: 'POST', body: new FormData(form), signal: pending.signal });
            const data = await response.json();
            target.replaceChildren();
            if (!response.ok) { target.textContent = data.error || 'Chưa thể tính dự toán. Hãy kiểm tra dữ liệu.'; return; }
            const facts = document.createElement('dl'); facts.className = 'checkout-facts';
            data.lines.forEach(line => {
                const row = document.createElement('div');
                const title = document.createElement('dt'); const amount = document.createElement('dd');
                title.textContent = line.name + (line.rent ? ` (${line.days}/${line.monthDays} ngày)` : ` (${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(line.quantity)} ${line.unit})`);
                amount.textContent = money(line.amount); row.append(title, amount); facts.append(row);
            });
            const total = document.createElement('div'); total.className = 'checkout-total';
            const label = document.createElement('strong'); label.textContent = 'Tổng dự tính';
            const amount = document.createElement('strong'); amount.textContent = money(data.total);
            total.append(label, amount); target.append(facts, total);
        } catch (error) { if (error.name !== 'AbortError') target.textContent = 'Chưa thể tải dự toán. Bạn vẫn có thể nhấn tính hóa đơn để kiểm tra.'; }
    }
    form.addEventListener('input', () => { clearTimeout(timer); if (pending) pending.abort(); timer = setTimeout(update, 350); });
    update();
})();
