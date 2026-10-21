(() => {
    const dropdown = document.querySelector('.home-district-dropdown');
    if (!dropdown) return;
    const input = dropdown.querySelector('input');
    const toggle = dropdown.querySelector('.home-district-toggle');
    dropdown.querySelectorAll('[data-district]').forEach(option => {
        option.addEventListener('click', () => {
            input.value = option.dataset.district;
            dropdown.querySelector('[data-district-label]').textContent = option.textContent;
            dropdown.querySelectorAll('[data-district]').forEach(item => {
                item.classList.toggle('active', item === option);
                item.setAttribute('aria-pressed', String(item === option));
            });
            toggle.focus();
        });
    });
})();
