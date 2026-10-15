(() => {
    const sidebar = document.getElementById('app-sidebar');
    const toggle = document.getElementById('sidebar-toggle');
    const backdrop = document.getElementById('sidebar-backdrop');
    const main = document.getElementById('dashboard-main');
    const account = document.getElementById('account-menu');
    const accountToggle = document.getElementById('account-toggle');
    const popover = document.getElementById('account-popover');
    if (!sidebar || !toggle) return;
    const mobile = window.matchMedia('(max-width: 767px)');
    let collapsed = false;
    let mobileOpen = false;
    const closeAccount = (focus = false) => {
        if (!popover) return;
        popover.hidden = true;
        accountToggle.setAttribute('aria-expanded', 'false');
        if (focus) accountToggle.focus();
    };
    const render = () => {
        const open = mobile.matches ? mobileOpen : !collapsed;
        document.body.classList.toggle('sidebar-collapsed', !mobile.matches && collapsed);
        document.body.classList.toggle('sidebar-open', mobile.matches && mobileOpen);
        sidebar.inert = !open;
        main.inert = mobile.matches && mobileOpen;
        backdrop.hidden = !(mobile.matches && mobileOpen);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Thu gọn thanh điều hướng' : 'Mở thanh điều hướng');
    };
    toggle.addEventListener('click', () => {
        closeAccount();
        if (mobile.matches) mobileOpen = !mobileOpen;
        else collapsed = !collapsed;
        render();
        if (mobile.matches && mobileOpen) sidebar.querySelector('a, button').focus();
    });
    const closeMobile = () => { mobileOpen = false; closeAccount(); render(); toggle.focus(); };
    backdrop.addEventListener('click', closeMobile);
    accountToggle?.addEventListener('click', () => {
        const open = popover.hidden;
        popover.hidden = !open;
        accountToggle.setAttribute('aria-expanded', String(open));
        if (open) popover.querySelector('a,button')?.focus();
    });
    document.addEventListener('click', event => {
        if (account && !account.contains(event.target)) closeAccount();
    });
    document.addEventListener('focusin', event => {
        if (account && !account.contains(event.target)) closeAccount();
    });
    document.addEventListener('keydown', event => {
        if (document.querySelector('.confirm-overlay') || document.querySelector('dialog[open]')) return;
        if (event.key === 'Escape') {
            if (popover && !popover.hidden) { event.preventDefault(); closeAccount(true); }
            else if (mobile.matches && mobileOpen) { event.preventDefault(); closeMobile(); }
        }
        if (event.key === 'Tab' && mobile.matches && mobileOpen) {
            const items = [...sidebar.querySelectorAll('a,button')].filter(el => el.getClientRects().length && !el.disabled);
            const first = items[0], last = items[items.length - 1];
            if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
        }
    });
    mobile.addEventListener('change', () => { mobileOpen = false; closeAccount(); render(); if (sidebar.contains(document.activeElement) && sidebar.inert) toggle.focus(); });
    render();
})();

(() => {
    document.querySelectorAll('[data-app-toast]').forEach(toast => {
        const timer = toast.dataset.toastPersistent === 'true'
            ? null
            : window.setTimeout(() => toast.remove(), 3000);
        toast.querySelector('[data-toast-close]')?.addEventListener('click', () => {
            if (timer !== null) window.clearTimeout(timer);
            toast.remove();
        });
    });
})();

(() => {
    const dropdowns = [...document.querySelectorAll('.tenant-header details')];
    if (!dropdowns.length) return;
    document.addEventListener('click', event => {
        dropdowns.forEach(item => {
            if (item.open && !item.contains(event.target)) item.open = false;
        });
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        dropdowns.forEach(item => item.open = false);
    });
    dropdowns.forEach(item => item.addEventListener('toggle', () => {
        if (!item.open) return;
        dropdowns.filter(other => other !== item).forEach(other => other.open = false);
    }));
})();

(() => {
    const donut = document.querySelector('.role-donut');
    const rows = [...document.querySelectorAll('.role-distribution p')];
    if (!donut || !rows.length) return;
    const counts = rows.map(row => Number(row.querySelector('strong')?.textContent) || 0);
    const total = counts.reduce((sum, count) => sum + count, 0);
    const colors = ['#3E2718', '#785135', '#DECBB6', '#EFE4D6'];
    let offset = 0;
    const segments = counts.map((count, index) => {
        const end = offset + (total ? count / total * 360 : 0);
        const segment = `${colors[index % colors.length]} ${offset}deg ${end}deg`;
        rows[index].style.color = colors[index % colors.length];
        offset = end;
        return segment;
    });
    donut.style.background = total ? `conic-gradient(${segments.join(',')})` : '#EFE4D6';
})();
