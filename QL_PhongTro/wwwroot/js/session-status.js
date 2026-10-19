(() => {
    let checking = false;
    async function checkSession() {
        if (checking) return;
        checking = true;
        try {
            const response = await fetch('/Account/SessionStatus', { cache: 'no-store', credentials: 'same-origin' });
            if (response.status === 401) { window.location.replace('/Account/Login?timeout=true'); return; }
            // A temporary server failure does not mean the user's cookie expired.
            if (!response.ok) return;
            const state = await response.json();
            if (state.authenticated === false) window.location.replace('/Account/Login?timeout=true');
            else if (state.mustChangePassword && !location.pathname.toLowerCase().startsWith('/account/changepassword'))
                window.location.replace('/Account/ChangePassword');
        } catch { /* The server still checks authorization on every data request. */ }
        finally { checking = false; }
    }
    setInterval(checkSession, 20000);
    window.addEventListener('focus', checkSession);
})();
