(() => {
    const bell = document.querySelector('[data-manager-bell]');
    if (!bell) return;
    const button = bell.querySelector('button'), panel = bell.querySelector('section');
    const list = bell.querySelector('[data-manager-notice-list]'), badge = bell.querySelector('[data-manager-pending]');
    const node = (tag, text, css) => { const el = document.createElement(tag); el.textContent = text; if (css) el.className = css; return el; };
    let loading = false;
    const load = async () => {
        if (loading) return;
        loading = true;
        try {
            const response = await fetch('/ThongBaoQuanLy/TomTat', { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) throw new Error();
            const data = await response.json();
            badge.textContent = data.canTiepNhan > 99 ? '99+' : data.canTiepNhan;
            badge.hidden = data.canTiepNhan === 0;
            button.setAttribute('aria-label', `Thông báo báo hỏng, ${data.canTiepNhan} cần tiếp nhận`);
            list.replaceChildren();
            if (!data.items.length) list.append(node('p', 'Chưa có thông báo báo hỏng.', 'payment-notice-empty'));
            for (const item of data.items) {
                const target = new URL(item.duongDan, location.origin);
                if (target.origin !== location.origin) continue;
                const a = node('a', '', 'payment-notice'); a.href = target.href;
                a.append(node('strong', item.tieuDe), node('p', item.noiDung), node('small', `${item.ngayTao} · ${item.trangThai}`), node('span', 'Kiểm tra ngay', 'payment-notice-action'));
                list.append(a);
            }
        } catch { list.replaceChildren(node('p', 'Không thể tải thông báo. Bấm chuông để thử lại.', 'payment-notice-empty')); }
        finally { loading = false; }
    };
    const close = () => { panel.hidden = true; button.setAttribute('aria-expanded', 'false'); };
    button.addEventListener('click', () => { panel.hidden = !panel.hidden; button.setAttribute('aria-expanded', String(!panel.hidden)); if (!panel.hidden) load(); });
    document.addEventListener('click', event => { if (!bell.contains(event.target)) close(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && !panel.hidden) { close(); button.focus(); } });
    load();
    setInterval(() => { if (!document.hidden) load(); }, 30000);
})();
