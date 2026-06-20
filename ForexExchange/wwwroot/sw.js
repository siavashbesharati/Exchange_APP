// Service Worker for Push Notifications
// Web Worker برای اعلان‌های فشاری

const CACHE_NAME = 'taban-forex-v2';
const NOTIFICATION_TAG = 'taban-notification';

// Install service worker
self.addEventListener('install', event => {
    self.skipWaiting();

    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            return cache.addAll([
                '/',
                '/css/site.css',
                '/css/modern-notifications.css',
                '/js/admin-notifications.js',
                '/favicon/favicon-32x32.png',
                '/favicon/apple-touch-icon.png'
            ]);
        })
    );
});

// Activate service worker
self.addEventListener('activate', event => {
    event.waitUntil(
        Promise.all([
            caches.keys().then(cacheNames => {
                return Promise.all(
                    cacheNames.map(cache => {
                        if (cache !== CACHE_NAME) {
                            return caches.delete(cache);
                        }
                    })
                );
            }),
            self.clients.claim()
        ])
    );
});

// Handle push events
self.addEventListener('push', event => {
    
    let data = {
        title: 'سامانه معاملات اکسورا',
        body: 'اعلان جدید دریافت شد',
        icon: '/favicon/apple-touch-icon.png',
        badge: '/favicon/favicon-32x32.png',
        tag: NOTIFICATION_TAG,
        data: {}
    };

    if (event.data) {
        try {
            const pushData = event.data.json();
            data = {
                ...data,
                title: pushData.title || data.title,
                body: pushData.message || pushData.body || data.body,
                icon: pushData.icon || data.icon,
                tag: pushData.tag || data.tag,
                data: pushData.data || {},
                badge: data.badge,
                requireInteraction: pushData.type === 'error' || pushData.type === 'warning',
                silent: false,
                timestamp: Date.now(),
                actions: [
                    {
                        action: 'view',
                        title: 'مشاهده',
                        icon: '/favicon/favicon-32x32.png'
                    },
                    {
                        action: 'dismiss',
                        title: 'بستن'
                    }
                ]
            };

            // Add emoji based on notification type
            if (pushData.type) {
                switch (pushData.type) {
                    case 'success':
                        data.body = '✅ ' + data.body;
                        break;
                    case 'warning':
                        data.body = '⚠️ ' + data.body;
                        break;
                    case 'error':
                        data.body = '❌ ' + data.body;
                        break;
                    case 'info':
                        data.body = 'ℹ️ ' + data.body;
                        break;
                }
            }
        } catch (e) {
        }
    }

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: data.icon,
            badge: data.badge,
            tag: data.tag,
            data: data.data,
            requireInteraction: data.requireInteraction,
            silent: data.silent,
            timestamp: data.timestamp,
            actions: data.actions,
            dir: 'rtl',
            lang: 'fa'
        })
    );
});

// Handle notification click
self.addEventListener('notificationclick', event => {
    
    event.notification.close();

    if (event.action === 'dismiss') {
        return;
    }

    // Extract notification data
    const data = event.notification.data;

    // Handle notification click
    event.waitUntil(
        clients.matchAll({
            type: 'window',
            includeUncontrolled: true
        }).then(clientList => {
            
            // Use URL from notification data (set by NotificationHub)
            let url = '/';
            if (data) {
                // First priority: use the explicit URL from notification context
                if (data.url && data.url !== '/') {
                    url = data.url;
                }
                // Fallback: determine URL based on entity data
                else if (data.contextData) {
                    const contextData = data.contextData;
                    if (contextData.orderId) {
                        url = `/Orders/Details/${contextData.orderId}`;
                    } else if (contextData.customerId && data.type === 'customer') {
                        url = `/Customers/Details/${contextData.customerId}`;
                    } else if (contextData.documentId) {
                        url = `/AccountingDocuments/Details/${contextData.documentId}`;
                    } else if (contextData.bankAccountId) {
                        url = `/BankAccount/Details/${contextData.bankAccountId}`;
                    }
                }
                // Legacy fallback for old notification format
                else if (data.orderId) {
                    url = `/Orders/Details/${data.orderId}`;
                } else if (data.customerId) {
                    url = `/Customers/Details/${data.customerId}`;
                } else if (data.documentId) {
                    url = `/AccountingDocuments/Details/${data.documentId}`;
                } else if (data.bankAccountId) {
                    url = `/BankAccount/Details/${data.bankAccountId}`;
                }
            }
            


            // Check if app is already open
            for (const client of clientList) {
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    client.focus();
                    // Navigate the existing client to the new URL
                    client.postMessage({
                        type: 'NOTIFICATION_CLICK',
                        url: url,
                        data: data
                    });
                    return;
                }
            }

            // Open new window with the proper URL
            if (clients.openWindow) {
                // Ensure URL is properly formatted
                const fullUrl = url.startsWith('http') ? url : self.location.origin + url;
                return clients.openWindow(fullUrl);
            }
        })
    );
});

// Handle notification close
self.addEventListener('notificationclose', event => {
    
    // Optional: Track notification dismissal
    event.waitUntil(
        clients.matchAll({
            type: 'window',
            includeUncontrolled: true
        }).then(clientList => {
            clientList.forEach(client => {
                client.postMessage({
                    type: 'NOTIFICATION_CLOSED',
                    data: event.notification.data
                });
            });
        })
    );
});

// Handle background sync (for offline support)
self.addEventListener('sync', event => {
    
    if (event.tag === 'background-sync') {
        event.waitUntil(
            // Perform background tasks here
            Promise.resolve()
        );
    }
});

// Handle messages from main thread
self.addEventListener('message', event => {
    
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
