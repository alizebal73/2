import type { PageKey, AppUserRecord } from '../types';
import { hasPermission } from '../services/authService';

const navItems: Array<{ key: PageKey; label: string; permissions?: string[] }> = [
  { key: 'dashboard', label: 'داشبورد' },
  { key: 'stations', label: 'ایستگاه‌ها', permissions: ['station.manage'] },
  { key: 'customers', label: 'مشتریان', permissions: ['customer.manage'] },
  { key: 'buffet', label: 'بوفه', permissions: ['buffet.sell', 'buffet.inventory'] },
  { key: 'tariffs', label: 'تعرفه‌ها', permissions: ['tariff.manage'] },
  { key: 'games', label: 'بازی‌ها', permissions: ['game.manage'] },
  { key: 'client-shell', label: 'کلاینت‌ها', permissions: ['client.control'] },
  { key: 'accounts', label: 'اکانت‌ها', permissions: ['account.manage'] },
  { key: 'reports', label: 'گزارش‌ها', permissions: ['finance.view', 'audit.view'] },
  { key: 'users', label: 'کاربران و شیفت', permissions: ['user.manage', 'shift.manage'] },
  { key: 'settings', label: 'تنظیمات', permissions: ['user.manage'] },
];

type Props = { activePage: PageKey; onChange: (page: PageKey) => void; user: AppUserRecord };

export function TopNavigation({ activePage, onChange, user }: Props) {
  const visibleItems = navItems.filter(item =>
    !item.permissions || item.permissions.some(permission => hasPermission(user, permission))
  );
  return (
    <nav className="top-nav" aria-label="ناوبری اصلی">
      {visibleItems.map((item) => (
        <button
          key={item.key}
          type="button"
          className={activePage === item.key ? 'nav-item active' : 'nav-item'}
          onClick={() => onChange(item.key)}
          aria-current={activePage === item.key ? 'page' : undefined}
        >
          {item.label}
        </button>
      ))}
    </nav>
  );
}
