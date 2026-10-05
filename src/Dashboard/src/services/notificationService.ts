export type NotificationLevel = 'Info' | 'Warning' | 'Critical';

export type NotificationRecord = {
  id: string;
  appUserId?: string | null;
  category: string;
  title: string;
  detail: string;
  level: NotificationLevel | string;
  entityName?: string | null;
  entityId?: string | null;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
};

export type NotificationInbox = {
  items: NotificationRecord[];
  unreadCount: number;
};

export async function getNotifications(take = 50): Promise<NotificationInbox> {
  const response = await fetch('/api/notifications?take=' + encodeURIComponent(String(take)));
  const payload = await response.json().catch(() => null) as NotificationInbox & { message?: string } | null;
  if (!response.ok)
    throw new Error(payload?.message || 'دریافت اعلان‌ها انجام نشد');
  return payload ?? { items: [], unreadCount: 0 };
}

export async function markNotificationRead(id: string): Promise<void> {
  const response = await fetch('/api/notifications/' + encodeURIComponent(id) + '/read', {
    method: 'POST',
  });
  if (!response.ok)
    throw new Error('خوانده‌شدن اعلان انجام نشد');
}

export async function markAllNotificationsRead(): Promise<void> {
  const response = await fetch('/api/notifications/read-all', {
    method: 'POST',
  });
  if (!response.ok)
    throw new Error('خوانده‌شدن اعلان‌ها انجام نشد');
}
