(() => {
    const page = document.querySelector('[data-tenant-repairs]');
    if (!page) return;
    const rows = [...page.querySelectorAll('[data-repair-row]')];
    const search = page.querySelector('#trl-search');
    const status = page.querySelector('#trl-status');
    const priority = page.querySelector('#trl-priority');
    const empty = page.querySelector('[data-repair-empty]');
    const reset = page.querySelector('[data-reset-filters]');
    const normalize = text => text.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    const searchable = rows.map(row => normalize(row.cells[0].textContent + ' ' + row.cells[1].textContent));
    const count = page.querySelector('[data-repair-count]');
    const suffix = count.textContent.includes('trên trang') ? ' trên trang' : '';
    function filter() {
        const query = normalize(search.value.trim());
        let visible = 0;
        rows.forEach((row, index) => {
            row.hidden = !(searchable[index].includes(query) && (!status.value || row.dataset.status === status.value) && (!priority.value || row.dataset.priority === priority.value));
            if (!row.hidden) visible++;
        });
        empty.hidden = visible > 0;
        const filtered = !!(query || status.value || priority.value);
        reset.hidden = !filtered;
        if (rows.length) {
            page.querySelector('[data-empty-title]').textContent = 'Không có báo hỏng phù hợp.';
            page.querySelector('[data-empty-help]').textContent = 'Thử từ khóa khác hoặc xóa bộ lọc để xem lại danh sách' + suffix + '.';
        }
        count.textContent = `Hiển thị ${visible} / ${rows.length} báo hỏng${suffix}`;
        page.querySelector('[data-filter-state]').textContent = filtered ? 'Đang áp dụng bộ lọc' : 'Đang hiển thị tất cả' + suffix;
    }
    page.querySelector('[data-repair-filters]').addEventListener('submit', event => {
        event.preventDefault();
        filter();
    });
    reset.addEventListener('click', () => { search.value = ''; status.value = ''; priority.value = ''; filter(); search.focus(); });
    page.querySelector('[data-repair-filters]').hidden = false;
})();
