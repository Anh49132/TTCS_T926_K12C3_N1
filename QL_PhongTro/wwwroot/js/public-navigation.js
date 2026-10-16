(() => {
    const navigation = document.getElementById('public-navigation');
    const toggle = document.querySelector('.public-menu-toggle');
    const Collapse = window.bootstrap?.Collapse;
    if (!navigation || !toggle || !Collapse) return;
    const mobile = window.matchMedia('(max-width: 991.98px)');
    const collapse = Collapse.getOrCreateInstance(navigation, { toggle: false });
    let focusFirstLink = false;
    const firstLink = () => navigation.querySelector('a[href]')?.focus();
    toggle.addEventListener('keydown', event => {
        if (!mobile.matches || event.key !== 'ArrowDown') return;
        event.preventDefault();
        if (navigation.classList.contains('show')) firstLink();
        else { focusFirstLink = true; collapse.show(); }
    });
    navigation.addEventListener('shown.bs.collapse', () => {
        if (focusFirstLink && mobile.matches) firstLink();
        focusFirstLink = false;
    });
    navigation.addEventListener('hidden.bs.collapse', () => {
        if (mobile.matches && navigation.contains(document.activeElement)) toggle.focus();
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || !mobile.matches || !navigation.classList.contains('show')) return;
        if (event.target !== toggle && !navigation.contains(event.target)) return;
        event.preventDefault();
        focusFirstLink = false;
        collapse.hide();
        toggle.focus();
    });
    document.addEventListener('pointerdown', event => {
        if (mobile.matches && navigation.classList.contains('show') && !navigation.contains(event.target) && !toggle.contains(event.target)) {
            focusFirstLink = false;
            collapse.hide();
        }
    });
})();
