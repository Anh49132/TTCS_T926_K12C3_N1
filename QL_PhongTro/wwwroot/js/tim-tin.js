(() => {
    const form = document.getElementById('tim-tin-form');
    const filterForm = document.querySelector('.timtin-sidebar-form');
    const status = document.getElementById('tim-tin-loading');
    if (!status) return;

    const showLoading = () => {
        status.hidden = false;
        document.querySelector('main')?.setAttribute('aria-busy', 'true');
    };
    [form, filterForm].filter(Boolean).forEach(searchForm => {
        searchForm.addEventListener('submit', event => {
            if (!event.defaultPrevented && searchForm.checkValidity()) showLoading();
        });
    });
    const sort = document.getElementById('timtin-sort');
    const sortValue = document.getElementById('timtin-sort-value');
    sort?.addEventListener('change', () => {
        if (sortValue) sortValue.value = sort.value;
        form?.requestSubmit();
    });
    document.querySelector('.timtin-advanced-toggle')?.addEventListener('click', event => {
        const button = event.currentTarget;
        const sidebar = document.getElementById('timtin-sidebar');
        if (!sidebar) return;
        if (!window.matchMedia('(max-width: 640px)').matches) {
            sidebar.scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }
        const isOpen = sidebar.classList.toggle('is-open');
        button.setAttribute('aria-expanded', String(isOpen));
        sidebar.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
    document.querySelectorAll('nav[aria-label="Chuyển trang kết quả"] a, #tim-tin-price-suggestion').forEach(link => {
        link.addEventListener('click', event => {
            if (!event.defaultPrevented && event.button === 0 &&
                !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) showLoading();
        });
    });
    // Back/forward may restore the old page from the browser cache.
    window.addEventListener('pageshow', () => {
        status.hidden = true;
        document.querySelector('main')?.removeAttribute('aria-busy');
    });
})();
