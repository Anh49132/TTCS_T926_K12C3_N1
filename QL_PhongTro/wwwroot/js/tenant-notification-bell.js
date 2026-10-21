// Notification bell functionality for tenant header
(function () {
    'use strict';

    const bell = document.getElementById('notification-bell');
    const badge = document.getElementById('notification-badge');
    const dropdown = document.getElementById('notification-dropdown');
    const list = document.getElementById('notification-list');
    const markAllBtn = document.getElementById('mark-all-read-btn');

    if (!bell || !dropdown || !list) return;

    let unreadCount = 0;
    let isLoading = false;

    // Format date for display
    function formatDate(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diff = now - date;
        const minutes = Math.floor(diff / 60000);
        const hours = Math.floor(diff / 3600000);
        const days = Math.floor(diff / 86400000);

        if (minutes < 1) return 'Vừa xong';
        if (minutes < 60) return `${minutes} phút trước`;
        if (hours < 24) return `${hours} giờ trước`;
        if (days < 7) return `${days} ngày trước`;
        return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    // Get notification icon based on type
    function getNotificationIcon(type) {
        switch (type) {
            case 'HOA_DON_MOI': return '📄';
            case 'SAP_DEN_HAN': return '⏰';
            case 'QUA_HAN': return '⚠️';
            default: return '🔔';
        }
    }

    // Render notification item
    function renderNotification(item) {
        const div = document.createElement('div');
        div.className = `notification-item ${!item.DaDoc ? 'unread' : ''}`;
        div.dataset.id = item.Id;
        div.dataset.url = item.DuongDan;
        div.role = 'listitem';

        div.innerHTML = `
            <div class="notification-icon">${getNotificationIcon(item.LoaiThongBao)}</div>
            <div class="notification-content">
                <div class="notification-title">${escapeHtml(item.TieuDe)}</div>
                <div class="notification-body">${escapeHtml(item.NoiDung)}</div>
                <div class="notification-time">${formatDate(item.NgayTao)}</div>
            </div>
            ${!item.DaDoc ? '<span class="unread-dot" aria-hidden="true"></span>' : ''}
        `;

        div.addEventListener('click', function (e) {
            if (e.target.closest('.notification-actions')) return;
            handleNotificationClick(item);
        });

        return div;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    async function handleNotificationClick(item) {
        // Mark as read if unread
        if (!item.DaDoc) {
            try {
                await markAsRead(item.Id);
            } catch (err) {
                console.error('Failed to mark as read:', err);
            }
        }

        // Navigate to the link
        if (item.DuongDan) {
            window.location.href = item.DuongDan;
        }
        closeDropdown();
    }

    async function loadNotifications() {
        if (isLoading) return;
        isLoading = true;

        try {
            const response = await fetch('/api/thong-bao/danh-sach?page=1&pageSize=10');
            if (!response.ok) throw new Error('Failed to load');
            const data = await response.json();
            renderList(data.items || []);
        } catch (err) {
            console.error('Load notifications error:', err);
            list.innerHTML = '<div class="notification-error">Không thể tải thông báo</div>';
        } finally {
            isLoading = false;
        }
    }

    function renderList(items) {
        if (!items || items.length === 0) {
            list.innerHTML = '<div class="notification-empty">Không có thông báo nào</div>';
            return;
        }

        list.innerHTML = '';
        items.forEach(item => {
            list.appendChild(renderNotification(item));
        });
    }

    async function updateUnreadCount() {
        try {
            const response = await fetch('/api/thong-bao/chua-doc');
            if (!response.ok) throw new Error('Failed to load count');
            const data = await response.json();
            unreadCount = data.count || 0;
            updateBadge();
        } catch (err) {
            console.error('Update unread count error:', err);
        }
    }

    function updateBadge() {
        if (unreadCount > 0) {
            badge.textContent = unreadCount > 99 ? '99+' : unreadCount.toString();
            badge.hidden = false;
        } else {
            badge.hidden = true;
        }
    }

    async function markAsRead(id) {
        try {
            const response = await fetch(`/api/thong-bao/${id}/da-doc`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            if (!response.ok) throw new Error('Failed to mark as read');
            const data = await response.json();
            unreadCount = data.unreadCount;
            updateBadge();
            // Update UI
            const item = list.querySelector(`[data-id="${id}"]`);
            if (item) {
                item.classList.remove('unread');
                const dot = item.querySelector('.unread-dot');
                if (dot) dot.remove();
            }
        } catch (err) {
            console.error('Mark as read error:', err);
        }
    }

    async function markAllAsRead() {
        try {
            const response = await fetch('/api/thong-bao/tat-ca-da-doc', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });
            if (!response.ok) throw new Error('Failed to mark all as read');
            const data = await response.json();
            unreadCount = data.unreadCount || 0;
            updateBadge();
            // Update all items
            list.querySelectorAll('.notification-item.unread').forEach(item => {
                item.classList.remove('unread');
                const dot = item.querySelector('.unread-dot');
                if (dot) dot.remove();
            });
        } catch (err) {
            console.error('Mark all as read error:', err);
        }
    }

    function getAntiForgeryToken() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }

    function openDropdown() {
        dropdown.hidden = false;
        bell.setAttribute('aria-expanded', 'true');
        loadNotifications();
    }

    function closeDropdown() {
        dropdown.hidden = true;
        bell.setAttribute('aria-expanded', 'false');
    }

    function toggleDropdown(e) {
        e.stopPropagation();
        if (dropdown.hidden) {
            openDropdown();
        } else {
            closeDropdown();
        }
    }

    // Event listeners
    bell.addEventListener('click', toggleDropdown);

    if (markAllBtn) {
        markAllBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            markAllAsRead();
        });
    }

    // Close dropdown when clicking outside
    document.addEventListener('click', function (e) {
        if (!dropdown.hidden && !dropdown.contains(e.target) && e.target !== bell) {
            closeDropdown();
        }
    });

    // Close on Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !dropdown.hidden) {
            closeDropdown();
        }
    });

    // Initial load
    updateUnreadCount();

    // Poll for updates every 30 seconds
    setInterval(updateUnreadCount, 30000);

    // Expose for testing
    window.NotificationBell = {
        updateUnreadCount,
        loadNotifications,
        unreadCount: () => unreadCount
    };
})();