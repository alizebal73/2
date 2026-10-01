import type { ReactNode } from 'react';

export type UserErrorKind = 'network' | 'permission' | 'validation' | 'conflict' | 'not-found' | 'generic';

type Props = {
  kind?: UserErrorKind;
  title?: string;
  detail?: string;
  actionLabel?: string;
  onAction?: () => void;
  children?: ReactNode;
};

const defaults: Record<UserErrorKind, { title: string; detail: string; action: string }> = {
  network: {
    title: 'ارتباط با سرور برقرار نشد',
    detail: 'دادهٔ جدید از سرور دریافت نشد؛ ممکن است اتصال شبکه یا خود سرویس موقتاً در دسترس نباشد.',
    action: 'تلاش مجدد',
  },
  permission: {
    title: 'اجازهٔ انجام این عملیات را ندارید',
    detail: 'این عملیات برای نقش یا سطح دسترسی فعلی فعال نیست.',
    action: 'بازگشت',
  },
  validation: {
    title: 'اطلاعات واردشده کامل یا معتبر نیست',
    detail: 'یکی از مقدارهای لازم اشتباه یا خالی است. اطلاعات را بررسی کنید.',
    action: 'بررسی اطلاعات',
  },
  conflict: {
    title: 'وضعیت موردنظر تغییر کرده است',
    detail: 'عملیات روی داده‌ای انجام شد که هم‌زمان توسط بخش دیگری تغییر کرده است.',
    action: 'دریافت وضعیت جدید',
  },
  'not-found': {
    title: 'مورد موردنظر پیدا نشد',
    detail: 'رکورد ممکن است حذف، جابه‌جا یا دیگر در دسترس نباشد.',
    action: 'بررسی دوباره',
  },
  generic: {
    title: 'عملیات انجام نشد',
    detail: 'برنامه نتوانست عملیات را کامل کند. وضعیت را بررسی کنید و دوباره امتحان کنید.',
    action: 'تلاش دوباره',
  },
};

export function UserErrorBanner({
  kind = 'generic',
  title,
  detail,
  actionLabel,
  onAction,
  children,
}: Props) {
  const preset = defaults[kind];
  return (
    <div className={'user-error-banner ' + kind} role="alert">
      <div className="user-error-icon">!</div>
      <div className="user-error-copy">
        <strong>{title ?? preset.title}</strong>
        <span>{detail ?? preset.detail}</span>
        {children}
      </div>
      {onAction && <button type="button" className="btn sm" onClick={onAction}>{actionLabel ?? preset.action}</button>}
    </div>
  );
}
