(() => {
    const rows = [...document.querySelectorAll('#result-table tbody tr')];
    const search = document.getElementById('result-search'), floor = document.getElementById('result-floor');
    let group = rows.some(row => row.dataset.group === 'issued') ? 'issued' : rows.some(row => row.dataset.group === 'existing') ? 'existing' : rows.some(row => row.dataset.group === 'missing') ? 'missing' : 'other', page = 1;
    const render = () => {
        const term = search.value.trim().toLocaleLowerCase('vi');
        const matches = rows.filter(row => row.dataset.group === group && (row.dataset.room + ' ' + row.dataset.tenant).toLocaleLowerCase('vi').includes(term) && (!floor.value || row.dataset.floor === floor.value));
        const count = Math.max(1, Math.ceil(matches.length / 5)); page = Math.min(page, count);
        rows.forEach(row => { row.hidden = true; });
        matches.slice((page-1)*5, page*5).forEach(row => { row.hidden = false; });
        document.querySelectorAll('[data-result-tab]').forEach(button => button.setAttribute('aria-selected', String(button.dataset.resultTab === group)));
        document.getElementById('result-empty').hidden = matches.length > 0;
        document.getElementById('result-page-info').textContent = matches.length ? `Hiển thị ${(page-1)*5+1}–${Math.min(page*5,matches.length)} trên ${matches.length} phòng` : '0 phòng';
        const pagination = document.getElementById('result-pages'); pagination.replaceChildren();
        for (let number=1; number<=count; number++) {
            const button = document.createElement('button'); button.type='button'; button.textContent=number;
            button.setAttribute('aria-label', `Trang ${number}`); if(number===page) button.setAttribute('aria-current','page');
            button.addEventListener('click',()=>{page=number;render();});pagination.appendChild(button);
        }
    };
    [search,floor].forEach(input=>input.addEventListener('input',()=>{page=1;render();}));
    document.querySelectorAll('[data-result-tab]').forEach(button=>button.addEventListener('click',()=>{group=button.dataset.resultTab;page=1;floor.value='';render();}));
    render();
})();
