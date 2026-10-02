export function userErrorMessage(error: unknown, fallback: string): string {
  const raw = error instanceof Error ? error.message.toLocaleLowerCase('fa-IR') : '';
  if (raw.includes('permission') || raw.includes('forbidden') || raw.includes('دسترسی')) return 'برای انجام این عملیات دسترسی لازم را ندارید.';
  if (raw.includes('wallet') && (raw.includes('insufficient') || raw.includes('موجودی'))) return 'موجودی کیف پول برای این عملیات کافی نیست.';
  if (raw.includes('network') || raw.includes('fetch') || raw.includes('failed to fetch') || raw.includes('connection')) return 'ارتباط با سرور برقرار نشد؛ اتصال را بررسی و دوباره تلاش کنید.';
  if (raw.includes('not found') || raw.includes('پیدا نشد')) return 'اطلاعات موردنظر پیدا نشد؛ دوباره وضعیت را بررسی کنید.';
  if (raw.includes('conflict') || raw.includes('duplicate') || raw.includes('تعارض')) return 'این عملیات با وضعیت فعلی اطلاعات سازگار نیست؛ ابتدا وضعیت را تازه کنید.';
  if (raw.includes('invalid') || raw.includes('validation') || raw.includes('نامعتبر')) return 'اطلاعات واردشده معتبر نیست؛ مقدارها را بررسی کنید.';
  return fallback;
}
