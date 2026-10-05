import { useEffect, useState } from 'react';
import { downloadReportCsv } from '../../services/reportExportService';
import { getCustomerVipReport, type CustomerVipReportPage } from '../../services/customerVipReportService';

type Period = 'week' | 'month' | 'sixMonths' | 'year' | 'custom';

type Props = {
  period: Period;
  range: { start: number; end: number } | null;
};

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(Math.round(value));
}

function count(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

function minutes(value: number) {
  const rounded = Math.max(0, Math.round(value));
  const hours = Math.floor(rounded / 60);
  const mins = rounded % 60;
  if (!hours) return `${count(mins)} دقیقه`;
  return `${count(hours)} ساعت و ${count(mins)} دقیقه`;
}

function vipLabel(value: string) {
  return ({ gold: 'طلایی', silver: 'نقره‌ای', bronze: 'برنزی', custom: 'سفارشی', none: 'عادی' } as Record<string, string>)[value] ?? value;
}

function statusLabel(value: string) {
  return ({
    'vip-active': 'VIP فعال',
    'vip-expired': 'VIP منقضی',
    debtor: 'بدهکار',
    credit: 'دارای اعتبار',
    active: 'فعال',
  } as Record<string, string>)[value] ?? value;
}

function getWindow(period: Period, range: Props['range']) {
  const now = Date.now();
  if (range) return range;
  const start = new Date(now);
  if (period === 'month') start.setDate(start.getDate() - 30);
  else if (period === 'sixMonths') start.setDate(start.getDate() - 180);
  else if (period === 'year') start.setDate(start.getDate() - 365);
  else start.setDate(start.getDate() - 6);
  return { start: start.getTime(), end: now };
}

function exportCsv(report: CustomerVipReportPage | null) {
  if (!report) return;
  const lines = [
    ['کد', 'نام مشتری', 'VIP', 'پکیج', 'انقضا', 'باقی‌مانده امروز', 'باقی‌مانده کل', 'کیف پول', 'بدهی', 'تعداد جلسات', 'درآمد جلسات'],
    ...report.items.map(item => [
      item.code,
      item.name,
      vipLabel(item.vipTier),
      item.packageName ?? '—',
      item.vipExpiresAt ? new Date(item.vipExpiresAt).toLocaleDateString('fa-IR') : '—',
      minutes(item.remainingTodayMinutes),
      minutes(item.remainingTotalMinutes),
      item.walletBalance,
      item.debt,
      item.sessionCount,
      item.sessionRevenue,
    ]),
  ];
  const csv = lines.map(line => line.map(value => '"' + String(value).replace(/"/g, '""') + '"').join(',')).join('\r\n');
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
  link.download = 'gamenet-customer-vip-report.csv';
  link.click();
  URL.revokeObjectURL(link.href);
}

export function CustomerVipReportPanel({ period, range, canExport }: Props & { canExport: boolean }) {
  const [search, setSearch] = useState('');
  const [vip, setVip] = useState('all');
  const [debt, setDebt] = useState('all');
  const [packageName, setPackageName] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<CustomerVipReportPage | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const current = getWindow(period, range);
    setLoading(true);
    setError('');

    void getCustomerVipReport({
      from: new Date(current.start),
      to: new Date(current.end),
      search,
      vip: vip as 'all' | 'vip' | 'active' | 'expired' | 'none',
      debt: debt as 'all' | 'debtor' | 'clear',
      package: packageName,
      page,
      pageSize: 50,
    })
      .then(setResult)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'دریافت گزارش مشتری و VIP انجام نشد'))
      .finally(() => setLoading(false));
  }, [period, range, search, vip, debt, packageName, page]);

  const totalPages = Math.max(1, Math.ceil((result?.total ?? 0) / (result?.pageSize ?? 50)));

  return <>
    {error && (
      <div className="user-error-banner network">
        <div className="user-error-icon">!</div>
        <div className="user-error-copy">
          <strong>گزارش مشتری و VIP کامل نشد</strong>
          <span>{error}</span>
        </div>
        <button type="button" className="btn sm" onClick={() => setPage(value => value)}>تلاش مجدد</button>
      </div>
    )}

    <section className="report-filter-grid report-customer-filter-grid">
      <label>جستجوی مشتری
        <input value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} placeholder="کد، نام، موبایل یا نام کاربری" />
      </label>
      <label>وضعیت VIP
        <select value={vip} onChange={event => { setVip(event.target.value); setPage(1); }}>
          <option value="all">همه</option>
          <option value="vip">همه VIPها</option>
          <option value="active">VIP فعال</option>
          <option value="expired">VIP منقضی</option>
          <option value="none">بدون VIP</option>
        </select>
      </label>
      <label>بدهی
        <select value={debt} onChange={event => { setDebt(event.target.value); setPage(1); }}>
          <option value="all">همه</option>
          <option value="debtor">بدهکار</option>
          <option value="clear">بدون بدهی</option>
        </select>
      </label>
      <label>نام پکیج
        <input value={packageName} onChange={event => { setPackageName(event.target.value); setPage(1); }} placeholder="مثلاً Gold" />
      </label>
      <button type="button" className="btn" onClick={() => { setSearch(''); setVip('all'); setDebt('all'); setPackageName(''); setPage(1); }}>
        پاک کردن فیلتر
      </button>
    </section>

    <div className="summary-grid">
      {[
        ['مشتریان', result?.summary.customerCount ?? 0, 'blue'],
        ['VIP فعال', result?.summary.activeVipCount ?? 0, 'orange'],
        ['بدهکاران', result?.summary.debtorCount ?? 0, 'red'],
        ['کیف پول', money(result?.summary.walletTotal ?? 0) + ' تومان', 'green'],
        ['درآمد جلسات', money(result?.summary.sessionRevenue ?? 0) + ' تومان', 'purple'],
        ['تعداد جلسات', result?.summary.sessionCount ?? 0, 'blue'],
      ].map(item => (
        <div className="summary-card" key={String(item[0])}>
          <div className="label">{item[0]}</div>
          <div className={'value ' + item[2]}>{typeof item[1] === 'number' ? count(item[1]) : item[1]}</div>
        </div>
      ))}
    </div>

    <div className="report-actions" style={{ margin: '0 22px 10px' }}>
      <button type="button" className="btn" disabled={!canExport} title={!canExport ? 'دسترسی خروجی گزارش ندارید' : undefined} onClick={() => { if (!canExport) return; const current = getWindow(period, range); void downloadReportCsv('customers', { from: new Date(current.start).toISOString(), to: new Date(current.end).toISOString(), search, vip, debt, package: packageName, page, pageSize: 50 }).catch(reason => setError(reason instanceof Error ? reason.message : 'خروجی گزارش مشتری و VIP انجام نشد')); }}>📤 خروجی مشتری و VIP</button>
      <span className="page-meta"><span>{count(result?.total ?? 0)} مشتری در بازه</span></span>
    </div>

    <div className="table-wrap" data-testid="customer-vip-report">
      <table className="data-table">
        <thead>
          <tr>
            <th>مشتری</th>
            <th>VIP</th>
            <th>مصرف VIP</th>
            <th>کیف پول</th>
            <th>بدهی</th>
            <th>جلسات</th>
            <th>درآمد جلسات</th>
            <th>آخرین جلسه</th>
            <th>وضعیت</th>
          </tr>
        </thead>
        <tbody>
          {(result?.items ?? []).map(item => (
            <tr key={item.customerId} data-testid="customer-vip-row">
              <td>
                <strong>{item.name}</strong>
                <small className="muted-line">{item.code} · @{item.username || '—'}</small>
              </td>
              <td>
                <strong>{vipLabel(item.vipTier)}</strong>
                <small className="muted-line">{item.packageName ?? 'بدون پکیج'}</small>
              </td>
              <td>
                <strong>{item.vipTier !== 'none' ? minutes(item.remainingTodayMinutes) : '—'}</strong>
                <small className="muted-line">{item.vipTier !== 'none' ? 'باقی‌مانده امروز' : '—'}</small>
              </td>
              <td>{money(item.walletBalance)} ت</td>
              <td>{money(item.debt)} ت</td>
              <td>{count(item.sessionCount)}</td>
              <td>{money(item.sessionRevenue)} ت</td>
              <td>{item.lastSessionAt ? new Date(item.lastSessionAt).toLocaleDateString('fa-IR') : '—'}</td>
              <td>{statusLabel(item.status)}</td>
            </tr>
          ))}
          {!loading && !(result?.items.length) && <tr><td colSpan={9}>مشتری‌ای برای این فیلتر پیدا نشد.</td></tr>}
        </tbody>
      </table>
    </div>

    <div className="report-pagination">
      <button type="button" className="btn sm" disabled={loading || page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>قبلی</button>
      <span>صفحه {count(page)} از {count(totalPages)}</span>
      <button type="button" className="btn sm" disabled={loading || page >= totalPages} onClick={() => setPage(value => value + 1)}>بعدی</button>
    </div>
  </>;
}
