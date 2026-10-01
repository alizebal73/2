import type { PageKey } from '../types';

const navItems: Array<{ key: PageKey; label: string }> = [
  { key: 'dashboard', label: 'داشبورد' },
  { key: 'customers', label: 'مشتریان' },
  { key: 'buffet', label: 'بوفه' },
  { key: 'tariffs', label: 'تعرفه‌ها' },
  { key: 'games', label: 'بازی‌ها' },
  { key: 'client-shell', label: 'کلاینت‌ها' },
  { key: 'accounts', label: 'اکانت‌ها' },
  { key: 'reports', label: 'گزارش‌ها' },
  { key: 'users', label: 'کاربران و شیفت' },
  { key: 'settings', label: 'تنظیمات' },
];

type Props = { activePage: PageKey; onChange: (page: PageKey) => void };

export function TopNavigation({ activePage, onChange }: Props) {
  return (
    <nav className="top-nav" aria-label="ناوبری اصلی">
      {navItems.map((item) => (
        <button
          key={item.key}
          type="button"
          className={activePage === item.key ? 'nav-item active' : 'nav-item'}
          onClick={() => onChange(item.key)}
        >
          {item.label}
        </button>
      ))}
    </nav>
  );
}
