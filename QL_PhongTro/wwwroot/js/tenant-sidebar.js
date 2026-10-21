(() => {
    const sidebar = document.getElementById('tenant-sidebar');
    const toggle = document.querySelector('.tenant-menu-toggle');
    if (!sidebar || !toggle) return;
    const close = () => sidebar.close();
    toggle.addEventListener('click', () => {
        sidebar.showModal();
        toggle.setAttribute('aria-expanded', 'true');
        document.body.classList.add('tenant-sidebar-open');
    });
    sidebar.querySelector('.tenant-sidebar-close').addEventListener('click', close);
    sidebar.querySelectorAll('a').forEach(link => link.addEventListener('click', close));
    sidebar.addEventListener('click', event => {
        const bounds = sidebar.getBoundingClientRect();
        if (event.target === sidebar && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) close();
    });
    sidebar.addEventListener('close', () => {
        toggle.setAttribute('aria-expanded', 'false');
        document.body.classList.remove('tenant-sidebar-open');
        toggle.focus();
    });
})();
