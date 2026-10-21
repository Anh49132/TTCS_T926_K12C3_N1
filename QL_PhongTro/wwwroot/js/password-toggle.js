document.addEventListener('click', event => {
    const button = event.target.closest('.toggle-password');
    if (!button) return;
    const input = document.getElementById(button.dataset.target);
    if (!input) return;
    const visible = input.type === 'text';
    input.type = visible ? 'password' : 'text';
    const label = visible ? 'Hiện mật khẩu' : 'Ẩn mật khẩu';
    button.setAttribute('aria-pressed', String(!visible));
    button.setAttribute('aria-label', label);
    button.title = label;
});
