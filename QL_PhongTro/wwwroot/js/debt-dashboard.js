(() => {
  const page = document.querySelector('.debt-page'); if (!page) return;
  const status = page.querySelector('[data-debt-status]');
  const tell = (message, error = false) => { status.textContent = message; status.hidden = false; status.className = error ? 'debt-status-error' : 'debt-status-success'; };
  const form = page.querySelector('[data-debt-filter]');
  const validate = () => {
    for (const field of form.querySelectorAll('input[inputmode=numeric]')) {
      const value = field.value.trim(); field.setCustomValidity('');
      if (value && !/^\d+$/.test(value)) field.setCustomValidity('Nhập số nguyên không âm, không có dấu phân cách.');
      else if (value && BigInt(value) > (field.name.startsWith('quaHan') ? 2147483647n : 9223372036854775807n)) field.setCustomValidity('Giá trị vượt giới hạn cho phép.');
    }
    for (const [from, to] of [['quaHanTu', 'quaHanDen'], ['tienTu', 'tienDen']]) {
      const a = form.elements[from], b = form.elements[to];
      if (/^\d+$/.test(a.value) && /^\d+$/.test(b.value) && BigInt(a.value) > BigInt(b.value)) b.setCustomValidity('Giá trị đến phải lớn hơn hoặc bằng giá trị từ.');
    }
  };
  form.addEventListener('input', validate);
  form.addEventListener('submit', event => { validate(); if (!form.reportValidity()) event.preventDefault(); });
  page.querySelector('[data-debt-export]')?.addEventListener('click', async event => {
    const button = event.currentTarget; button.disabled = true; tell('Đang chuẩn bị tệp CSV...');
    try {
      const response = await fetch(button.dataset.debtExport, { headers: { Accept: 'text/csv' }, signal: AbortSignal.timeout(30000) });
      if (!response.ok || !(response.headers.get('content-type') || '').includes('text/csv')) throw new Error();
      const blob = await response.blob(); const disposition = response.headers.get('content-disposition') || '';
      const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i); const plain = disposition.match(/filename="?([^";]+)"?/i);
      const name = encoded ? decodeURIComponent(encoded[1]) : plain ? plain[1].trim() : 'cong-no.csv';
      const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = name; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000);
      tell('Xuất CSV thành công. Đã gửi tệp “' + name + '” tới trình duyệt để tải xuống.');
    } catch { tell('Xuất CSV thất bại. Kiểm tra kết nối, phiên đăng nhập và bộ lọc rồi thử lại.', true); }
    finally { button.disabled = false; }
  });
  const dialog = page.querySelector('[data-debt-dialog]'), content = dialog.querySelector('[data-debt-content]');
  let request;
  page.querySelectorAll('[data-debt-room]').forEach(button => button.addEventListener('click', async () => {
    request?.abort(); request = new AbortController(); const current = request;
    content.textContent = 'Đang tải chi tiết công nợ...'; dialog.showModal();
    try {
      const response = await fetch('/CongNo/Phong/' + encodeURIComponent(button.dataset.debtRoom), { signal: current.signal });
      const html = await response.text();
      if (!response.ok || !html.includes('debt-room-title')) throw new Error();
      if (!current.signal.aborted) content.innerHTML = html;
    } catch { if (!current.signal.aborted) content.textContent = 'Không thể tải chi tiết. Kiểm tra kết nối/quyền truy cập, đóng hộp và thử lại.'; }
  }));
  dialog.querySelector('[data-debt-close]').addEventListener('click', () => dialog.close());
  dialog.addEventListener('close', () => request?.abort());
})();
