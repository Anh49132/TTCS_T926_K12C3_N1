(function () {
    // Luu lai fetch goc truoc khi bi patch. moi yeu cau noi bo (refresh token) phai dung
    // ban goc, neu dung window.fetch da patch se tao vong lap doi va treo.
    const originalFetch = window.fetch.bind(window);
    let isRefreshing = false;
    let failedQueue = [];
    let sessionRestored = false;

    const AUTH_ENDPOINTS = ['/api/auth/refresh', '/api/auth/login'];

    const isAuthEndpoint = url =>
        typeof url === 'string' && AUTH_ENDPOINTS.some(path => url.includes(path));

    const processQueue = (error, token = null) => {
        failedQueue.forEach(prom => {
            if (error) {
                prom.reject(error);
            } else {
                prom.resolve(token);
            }
        });
        failedQueue = [];
    };

    const refreshAccessToken = async () => {
        const refreshToken = localStorage.getItem('refreshToken');
        if (!refreshToken) throw new Error('No refresh token');

        const response = await originalFetch('/api/auth/refresh', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ RefreshToken: refreshToken })
        });

        if (!response.ok) {
            const error = new Error('Refresh failed');
            error.status = response.status;
            throw error;
        }

        const data = await response.json();
        localStorage.setItem('accessToken', data.accessToken);
        if (data.refreshToken) localStorage.setItem('refreshToken', data.refreshToken);
        return data.accessToken;
    };

    // Check if access token is expired (with 30 second buffer)
    const isAccessTokenExpired = () => {
        const accessToken = localStorage.getItem('accessToken');
        if (!accessToken) return true;
        try {
            const payload = JSON.parse(atob(accessToken.split('.')[1]));
            const exp = payload.exp * 1000; // convert to milliseconds
            return Date.now() >= (exp - 30000); // 30 second buffer
        } catch {
            return true;
        }
    };

    const clearTokens = () => {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        window.dispatchEvent(new Event('authChanged'));
    };

    const goToLogin = async () => {
        // MVC cookie and API tokens have separate lifetimes. A rejected refresh
        // token (e.g. another tab logged in) must not end a valid MVC session.
        try {
            const response = await originalFetch('/Account/SessionStatus', {
                cache: 'no-store', credentials: 'same-origin'
            });
            if (response.status !== 401) {
                if (!response.ok) return;
                const state = await response.json();
                if (state.authenticated !== false) return;
            }
        } catch { return; }
        if (!/^\/account\/login/i.test(window.location.pathname)) {
            window.location.replace('/Account/Login');
        }
    };

    // Restore session on page load: if access token expired but refresh token exists, try to refresh
    const restoreSession = async () => {
        if (sessionRestored) return;
        sessionRestored = true;

        const accessToken = localStorage.getItem('accessToken');
        const refreshToken = localStorage.getItem('refreshToken');

        if (!accessToken || !refreshToken) return;

        if (isAccessTokenExpired()) {
            try {
                await refreshAccessToken();
                console.log('Session restored via refresh token');
            } catch (err) {
                console.warn('Session restore failed');
                if (err.status !== 401) return;
                clearTokens();
                // S1-02: ca access token va refresh token deu het han -> ve man hinh dang nhap.
                await goToLogin();
            }
        }
    };

    // Call restore on page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restoreSession);
    } else {
        restoreSession();
    }

    window.fetch = async (url, options = {}) => {
        const accessToken = localStorage.getItem('accessToken');
        if (accessToken && !options.headers?.Authorization) {
            options.headers = {
                ...options.headers,
                'Authorization': `Bearer ${accessToken}`
            };
        }

        const response = await originalFetch(url, options);

        if (response.status === 401 && !isAuthEndpoint(url) && localStorage.getItem('refreshToken')) {
            if (!isRefreshing) {
                isRefreshing = true;
                try {
                    const newAccessToken = await refreshAccessToken();
                    isRefreshing = false;
                    processQueue(null, newAccessToken);
                } catch (err) {
                    isRefreshing = false;
                    processQueue(err);
                    if (err.status === 401) {
                        clearTokens();
                        await goToLogin();
                    }
                    throw err;
                }
            } else {
                const newAccessToken = await new Promise((resolve, reject) => {
                    failedQueue.push({ resolve, reject });
                });
                options.headers = {
                    ...options.headers,
                    'Authorization': `Bearer ${newAccessToken}`
                };
                return originalFetch(url, options);
            }

            options.headers = {
                ...options.headers,
                'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
            };
            return originalFetch(url, options);
        }

        return response;
    };

    window.authSetTokens = (accessToken, refreshToken) => {
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', refreshToken);
        window.dispatchEvent(new Event('authChanged'));
    };

    window.authClearTokens = () => {
        clearTokens();
    };
})();
