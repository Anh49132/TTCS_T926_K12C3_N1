(() => {
    const root = document.querySelector('[data-ai-chat]'); if (!root) return;
    const launcher = root.querySelector('.ai-chat-launcher'), panel = root.querySelector('section');
    const form = root.querySelector('form'), input = root.querySelector('textarea'), send = form.querySelector('button');
    const messages = root.querySelector('[data-ai-messages]'), error = root.querySelector('[data-ai-error]');
    const suggestions = root.querySelector('[data-ai-suggestions]'), fresh = root.querySelector('[data-ai-new]');
    let conversationId = null, busy = false;
    // Keep the launcher and the open panel independently movable.
    const movable = [];
    const keepVisible = element => {
        const box = element.getBoundingClientRect(), gap = 8;
        const width = document.documentElement.clientWidth, height = window.innerHeight;
        element.style.left = `${Math.max(gap, Math.min(box.left, width - box.width - gap))}px`;
        element.style.top = `${Math.max(gap, Math.min(box.top, height - box.height - gap))}px`;
        element.style.right = 'auto'; element.style.bottom = 'auto';
    };
    const makeDraggable = (element, handle) => {
        let drag = null, suppressClick = false;
        movable.push(element);
        handle.addEventListener('pointerdown', event => {
            if (!event.isPrimary || event.button !== 0 || event.target.closest('button') && handle !== launcher) return;
            const box = element.getBoundingClientRect();
            drag = { id: event.pointerId, x: event.clientX, y: event.clientY, left: box.left, top: box.top, moved: false };
            suppressClick = false;
            handle.setPointerCapture(event.pointerId);
        });
        handle.addEventListener('pointermove', event => {
            if (!drag || drag.id !== event.pointerId) return;
            const dx = event.clientX - drag.x, dy = event.clientY - drag.y;
            if (!drag.moved && Math.hypot(dx, dy) < 6) return;
            drag.moved = true; element.classList.add('ai-chat-dragging');
            element.style.left = `${drag.left + dx}px`; element.style.top = `${drag.top + dy}px`;
            element.style.right = 'auto'; element.style.bottom = 'auto';
            keepVisible(element);
        });
        const finish = event => {
            if (!drag || drag.id !== event.pointerId) return;
            suppressClick = drag.moved; drag = null; element.classList.remove('ai-chat-dragging');
            if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
        };
        handle.addEventListener('pointerup', finish);
        handle.addEventListener('pointercancel', finish);
        handle.addEventListener('lostpointercapture', finish);
        handle.addEventListener('click', event => {
            if (suppressClick) { event.preventDefault(); event.stopImmediatePropagation(); suppressClick = false; }
        }, true);
    };
    makeDraggable(root, launcher);
    makeDraggable(panel, panel.querySelector('header'));
    window.addEventListener('resize', () => movable.forEach(element => {
        if (!element.hidden && element.style.left) keepVisible(element);
    }));
    const initial = messages.firstElementChild.cloneNode(true);
    const scroll = () => messages.scrollTop = messages.scrollHeight;
    const bubble = (text, role) => { const el = document.createElement('div'); el.className = `ai-chat-message ${role}`; el.textContent = text; messages.append(el); scroll(); return el; };
    const show = open => { panel.hidden = !open; launcher.setAttribute('aria-expanded', String(open)); if (open) { if (panel.style.left) keepVisible(panel); input.focus(); } else launcher.focus(); };
    launcher.addEventListener('click', () => show(panel.hidden));
    root.querySelector('[data-ai-close]').addEventListener('click', () => show(false));
    root.addEventListener('keydown', event => { if (event.key === 'Escape' && !panel.hidden) { event.preventDefault(); show(false); } });
    fresh.addEventListener('click', () => { if (busy) return; conversationId = null; messages.replaceChildren(initial.cloneNode(true)); suggestions.hidden = false; error.hidden = true; input.value = ''; input.focus(); });
    suggestions.querySelectorAll('button').forEach(button => button.addEventListener('click', () => { input.value = button.textContent; form.requestSubmit(); }));
    input.addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); if (!busy) form.requestSubmit(); } });
    form.addEventListener('submit', async event => {
        event.preventDefault(); const question = input.value.trim(); if (!question || busy) return;
        busy = true; send.disabled = true; fresh.disabled = true; suggestions.hidden = true; error.hidden = true;
        const user = bubble(question, 'user'); input.value = '';
        const pending = bubble('Đang tra cứu và soạn câu trả lời…', 'assistant pending');
        const abort = new AbortController(); const timer = setTimeout(() => abort.abort(), 60000);
        try {
            const response = await fetch('/api/chatbot', { method: 'POST', credentials: 'same-origin', signal: abort.signal,
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': form.querySelector('[name=__RequestVerificationToken]').value },
                body: JSON.stringify({ message: question, conversationId }) });
            if (response.redirected || response.status === 401 || response.status === 403) throw new Error('Phiên đăng nhập hoặc quyền truy cập đã thay đổi. Vui lòng tải lại trang và đăng nhập.');
            const data = await response.json();
            if (!response.ok) { if (response.status === 410) conversationId = null; throw new Error(data.message || 'Chưa gửi được câu hỏi. Vui lòng thử lại.'); }
            conversationId = data.conversationId; pending.textContent = data.answer; pending.classList.remove('pending');
            for (const link of data.links || []) { const url = new URL(link.url, location.origin); if (url.origin !== location.origin || !link.url.startsWith('/') || link.url.startsWith('//')) continue;
                const a = document.createElement('a'); a.href = url.href; a.textContent = link.label + ' →'; pending.append(a); }
        } catch (failure) {
            pending.remove(); user.remove(); input.value = question;
            error.textContent = failure.name === 'AbortError' ? 'AI phản hồi quá lâu. Câu hỏi đã được giữ lại để thử lại.' : failure.message;
            error.hidden = false;
        } finally { clearTimeout(timer); busy = false; send.disabled = false; fresh.disabled = false; scroll(); if (!panel.hidden) input.focus(); }
    });
})();
