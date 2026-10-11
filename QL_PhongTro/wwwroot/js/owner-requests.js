(() => {
    const table = document.getElementById('or-table');
    if (!table) return;
    const rows = [...table.tBodies[0].rows];
    const search = document.getElementById('or-search');
    const type = document.getElementById('or-type');
    let page = 1;
    const size = 10;
    const matching = () => rows.filter(row => row.textContent.toLocaleLowerCase('vi').includes(search.value.trim().toLocaleLowerCase('vi')) && (!type.value || row.dataset.type === type.value));
    function render() {
        const filtered = matching();
        const pages = Math.max(1, Math.ceil(filtered.length / size));
        page = Math.min(page, pages);
        rows.forEach(row => { row.hidden = true; });
        filtered.slice((page - 1) * size, page * size).forEach(row => { row.hidden = false; });
        document.getElementById('or-empty').hidden = filtered.length > 0;
        document.getElementById('or-count').textContent = filtered.length ? `Hiển thị ${(page - 1) * size + 1}–${Math.min(page * size, filtered.length)} trong ${filtered.length} yêu cầu` : '0 yêu cầu';
        const nav = document.getElementById('or-pages'); nav.replaceChildren();
        for (let i = 1; i <= pages; i++) { const button = document.createElement('button'); button.type = 'button'; button.textContent = i; button.className = 'or-button' + (i === page ? ' primary' : ''); button.setAttribute('aria-label', `Trang ${i}`); if (i === page) button.setAttribute('aria-current', 'page'); button.onclick = () => { page = i; render(); }; nav.append(button); }
    }
    search.addEventListener('input', () => { page = 1; render(); });
    type.addEventListener('change', () => { page = 1; render(); });
    document.getElementById('or-export').onclick = () => {
        const escape = value => '"' + (/^[=+@\-\t\r]/.test(value.trimStart()) ? "'" : '') + value.replaceAll('"', '""') + '"';
        const lines = [['Yêu cầu','Khách thuê','Phòng / Tòa nhà','Loại','Ngày mong muốn','Trạng thái','Gửi lúc'], ...matching().map(row => [...row.cells].slice(0,7).map(cell => cell.innerText.replaceAll('\n',' ')))];
        const url = URL.createObjectURL(new Blob(['\uFEFF' + lines.map(line => line.map(escape).join(',')).join('\r\n')], {type:'text/csv;charset=utf-8'}));
        const a = document.createElement('a'); a.href = url; a.download = 'yeu-cau-khach.csv'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    };
    render();
})();
