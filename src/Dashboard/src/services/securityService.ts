import type { PageKey, PageLockMap } from '../types';

const STORAGE_KEY = 'gamenet-page-locks-v1';

export const protectedPageLabels: Partial<Record<PageKey, string>> = {
  customers: 'مشتریان',
  accounts: 'اکانت‌ها',
  settings: 'تنظیمات',
  users: 'کاربران و شیفت',
  reports: 'گزارش‌ها',
  games: 'بازی‌ها',
  tariffs: 'تعرفه‌ها',
  'client-shell': 'کلاینت‌ها',
  buffet: 'بوفه',
};

export function readPageLocks(): PageLockMap {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) as PageLockMap : {};
  } catch {
    return {};
  }
}

export function writePageLocks(value: PageLockMap) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  window.dispatchEvent(new CustomEvent('gamenet-page-locks-changed', { detail: value }));
}

export async function hashPin(pin: string): Promise<string> {
  const bytes = new TextEncoder().encode(pin);
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest)).map(value => value.toString(16).padStart(2, '0')).join('');
}

export async function verifyPagePin(pin: string, expectedHash: string): Promise<boolean> {
  return (await hashPin(pin)) === expectedHash;
}
