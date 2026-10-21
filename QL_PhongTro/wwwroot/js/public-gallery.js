document.querySelectorAll('[data-gallery-thumb]').forEach(button => {
    button.addEventListener('click', () => {
        const main = document.querySelector('[data-gallery-main]');
        if (!main) return;
        main.src = button.dataset.full;
        main.alt = button.dataset.alt || main.alt;
        document.querySelectorAll('[data-gallery-thumb]').forEach(item => item.setAttribute('aria-pressed', 'false'));
        button.setAttribute('aria-pressed', 'true');
    });
});