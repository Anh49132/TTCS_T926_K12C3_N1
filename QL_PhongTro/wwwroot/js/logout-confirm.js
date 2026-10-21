// S1-02: hỏi xác nhận trước khi đăng xuất.
(function () {
    const form = document.getElementById('logoutForm');
    if (!form) return;

    let overlay = null;
    let confirmButton = null;
    let cancelButton = null;
    let restoreFocusTo = null;

    function close() {
        if (!overlay) return;
        overlay.remove();
        overlay = null;
        confirmButton = null;
        cancelButton = null;
        if (restoreFocusTo) restoreFocusTo.focus();
        restoreFocusTo = null;
    }

    function onKeydown(event) {
        if (event.key === 'Escape') {
            event.preventDefault();
            close();
            return;
        }
        if (event.key !== 'Tab' || !confirmButton || !cancelButton) return;
        // Giữ tiêu điểm bàn phím bên trong hộp thoại.
        const first = cancelButton;
        const last = confirmButton;
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    function open() {
        if (overlay) return;
        restoreFocusTo = document.activeElement;

        overlay = document.createElement('div');
        overlay.className = 'confirm-overlay';

        const dialog = document.createElement('div');
        dialog.className = 'confirm-dialog';
        dialog.setAttribute('role', 'alertdialog');
        dialog.setAttribute('aria-modal', 'true');
        dialog.setAttribute('aria-labelledby', 'confirmLogoutTitle');
        dialog.setAttribute('aria-describedby', 'confirmLogoutText');

        const title = document.createElement('h2');
        title.id = 'confirmLogoutTitle';
        title.textContent = 'Đăng xuất';

        const text = document.createElement('p');
        text.id = 'confirmLogoutText';
        text.textContent = 'Bạn có chắc muốn đăng xuất?';

        const actions = document.createElement('div');
        actions.className = 'confirm-actions';

        cancelButton = document.createElement('button');
        cancelButton.type = 'button';
        cancelButton.className = 'btn btn-outline-primary';
        cancelButton.textContent = 'Huỷ';
        cancelButton.addEventListener('click', close);

        confirmButton = document.createElement('button');
        confirmButton.type = 'button';
        confirmButton.className = 'btn btn-primary';
        confirmButton.textContent = 'Đăng xuất';
        confirmButton.addEventListener('click', async () => {
            close();
            // S1-02: thu hoi refresh token o may chu truoc khi xoa phien cookie,
            // de token da copy khong con dung duoc nua.
            const refreshToken = localStorage.getItem('refreshToken');
            const accessToken = localStorage.getItem('accessToken');
            if (refreshToken) {
                const headers = { 'Content-Type': 'application/json' };
                if (accessToken) headers['Authorization'] = 'Bearer ' + accessToken;
                try {
                    await fetch('/api/auth/logout', {
                        method: 'POST',
                        headers: headers,
                        body: JSON.stringify({ RefreshToken: refreshToken }),
                        keepalive: true
                    });
                } catch (err) {
                    // Mat ket noi van tiep tuc dang xuot khoi he thong.
                }
            }
            if (typeof window.authClearTokens === 'function') window.authClearTokens();
            form.submit();
        });

        actions.appendChild(cancelButton);
        actions.appendChild(confirmButton);
        dialog.appendChild(title);
        dialog.appendChild(text);
        dialog.appendChild(actions);
        overlay.appendChild(dialog);

        overlay.addEventListener('mousedown', event => {
            if (event.target === overlay) close();
        });
        document.addEventListener('keydown', onKeydown);
        document.body.appendChild(overlay);
        cancelButton.focus();
    }

    form.addEventListener('submit', event => {
        event.preventDefault();
        open();
    });
})();
