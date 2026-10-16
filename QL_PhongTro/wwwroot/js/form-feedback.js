(() => {
    let nextId = 0;
    document.querySelectorAll('form').forEach(form => {
        const fields = [...form.querySelectorAll('input, select, textarea')];
        const proxies = [...form.querySelectorAll('[data-validation-for]')];
        form.querySelectorAll('[data-valmsg-for]').forEach(message => {
            const name = message.getAttribute('data-valmsg-for');
            const targets = fields.filter(field => field.name === name && field.type !== 'hidden')
                .concat(proxies.filter(control => control.getAttribute('data-validation-for') === name));
            if (!targets.length) return;
            if (!message.id) {
                let id;
                do { id = `form-feedback-${++nextId}`; } while (document.getElementById(id));
                message.id = id;
            }
            const marked = new Set();
            targets.forEach(field => {
                const descriptions = new Set((field.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean));
                descriptions.add(message.id);
                field.setAttribute('aria-describedby', [...descriptions].join(' '));
            });
            const sync = () => {
                const hasError = Boolean(message.textContent.trim());
                targets.forEach(field => {
                    if (hasError) { field.setAttribute('aria-invalid', 'true'); marked.add(field); }
                    else if (marked.has(field)) { field.removeAttribute('aria-invalid'); marked.delete(field); }
                });
            };
            sync();
            new MutationObserver(sync).observe(message, { childList: true, subtree: true, characterData: true });
        });
    });
})();
