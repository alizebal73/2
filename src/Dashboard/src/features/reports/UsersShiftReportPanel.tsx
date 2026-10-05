import { useEffect, useState } from 'react';
import { downloadReportCsv } from '../../services/reportExportService';
import { getUsersShiftReport, type UsersShiftReportPage } from '../../services/usersShiftReportService';

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

function roleLabel(value: string) {
  return ({ Admin: 'مدیر', Operator: 'اپراتور', Owner: 'مالک' } as Record<string, string>)[value] ?? value;
}

function exportCsv(report: UsersShiftReportPage | null) {
  if (!report) return;
  const lines = [
    ['کاربر', 'نقش', 'شیفت', 'شیفت بسته', 'فروش', 'فروش نقدی', 'هزینه', 'اختلاف صندوق', 'جلسات', 'درآمد جلسات', 'پرداخت حقوق', 'مطالبات پرسنل'],
    ...report.items.map(item => [
      item.fullName,
      roleLabel(item.role),
      item.shiftCount,
      item.closedShiftCount,
      item.shiftRevenue,
      item.shiftCashSales,
      item.shiftExpenses,
      item.shiftDifference,
      item.sessionCount,
      item.sessionRevenue,
      item.paidThisPeriod,
      item.employeePayable,
    ]),
  ];
  const csv = lines.map(line => line.map(value => '"' + String(value).replace(/"/g, '""') + '"').join(',')).join('\r\n');
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
  link.download = 'gamenet-users-shifts-report.csv';
  link.click();
  URL.revokeObjectURL(link.href);
}

export function UsersShiftReportPanel({ period, range, canExport }: Props & { canExport: boolean }) {
  const [userSearch, setUserSearch] = useState('');
  const [shiftState, setShiftState] = useState('all');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<UsersShiftReportPage | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const current = getWindow(period, range);
    setLoading(true);
    setError('');

    void getUsersShiftReport({
      from: new Date(current.start),
      to: new Date(current.end),
      userSearch,
      shiftState: shiftState as 'all' | 'open' | 'closed',
      page,
      pageSize: 50,
    })
      .then(setResult)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'دریافت گزارش کاربران و شیفت انجام نشد'))
      .finally(() => setLoading(false));
  }, [period, range, userSearch, shiftState, page]);

  const totalPages = Math.max(1, Math.ceil((result?.total ?? 0) / (result?.pageSize ?? 50)));

  return <>
    {error && (
      <div className="user-error-banner network">
        <div className="user-error-icon">!</div>
        <div className="user-error-copy">
          <strong>گزارش کاربران و شیفت کامل نشد</strong>
          <span>{error}</span>
        </div>
        <button type="button" className="btn sm" onClick={() => setPage(value => value)}>تلاش مجدد</button>
      </div>
    )}

    <section className="report-filter-grid report-users-shift-filter-grid">
      <label>کاربر
        <input value={userSearch} onChange={event => { setUserSearch(event.target.value); setPage(1); }} placeholder="نام یا نام کاربری" />
      </label>
      <label>وضعیت شیفت
        <select value={shiftState} onChange={event => { setShiftState(event.target.value); setPage(1); }}>
          <option value="all">همه شیفت‌ها</option>
          <option value="open">باز</option>
          <option value="closed">بسته‌شده</option>
        </select>
      </label>
      <button type="button" className="btn" onClick={() => { setUserSearch(''); setShiftState('all'); setPage(1); }}>
        پاک کردن فیلتر
      </button>
    </section>

    <div className="summary-grid">
      {[
        ['کاربران', result?.summary.userCount ?? 0, 'blue'],
        ['کاربران فعال', result?.summary.activeUserCount ?? 0, 'green'],
        ['شیفت‌ها', result?.summary.shiftCount ?? 0, 'purple'],
        ['فروش شیفت', money(result?.summary.shiftRevenue ?? 0) + ' تومان', 'orange'],
        ['هزینه شیفت', money(result?.summary.shiftExpenses ?? 0) + ' تومان', 'red'],
        ['اختلاف محاسباتی', money(result?.summary.shiftDifference ?? 0) + ' تومان', 'blue'],
      ].map(item => (
        <div className="summary-card" key={String(item[0])}>
          <div className="label">{item[0]}</div>
          <div className={'value ' + item[2]}>{typeof item[1] === 'number' ? count(item[1]) : item[1]}</div>
        </div>
      ))}
    </div>

    <section className="report-grid">
      <div className="chart-box">
        <h3>عملکرد کاربر</h3>
        <div className="report-kpi-list">
          {(result?.items ?? []).slice(0, 8).map(item => (
            <div key={item.userId}>
              <span>{item.fullName}</span>
              <strong>{count(item.shiftCount)} شیفت · {money(item.sessionRevenue)} ت</strong>
            </div>
          ))}
          {!loading && !(result?.items.length) && <div><span>رکوردی وجود ندارد</span><strong>—</strong></div>}
        </div>
      </div>
      <div className="chart-box">
        <h3>حقوق و مطالبات</h3>
        <div className="report-kpi-list">
          <div><span>پرداخت حقوق</span><strong>{money(result?.summary.payrollPaid ?? 0)} تومان</strong></div>
          <div><span>مطالبات پرسنل</span><strong>{money(result?.summary.payrollEmployeePayable ?? 0)} تومان</strong></div>
          <div><span>جلسات ثبت‌شده توسط کاربر</span><strong>{count(result?.summary.sessionCount ?? 0)}</strong></div>
        </div>
      </div>
    </section>

    <div className="report-actions" style={{ margin: '0 22px 10px' }}>
      <button type="button" className="btn" disabled={!canExport} title={!canExport ? 'دسترسی خروجی گزارش ندارید' : undefined} onClick={() => { if (!canExport) return; const current = getWindow(period, range); void downloadReportCsv('users-shift', { from: new Date(current.start).toISOString(), to: new Date(current.end).toISOString(), userSearch, shiftState, page, pageSize: 50 }).catch(reason => setError(reason instanceof Error ? reason.message : 'خروجی گزارش کاربران و شیفت انجام نشد')); }}>📤 خروجی کاربران و شیفت</button>
      <span className="page-meta"><span>{count(result?.total ?? 0)} کاربر در گزارش</span></span>
    </div>

    <div className="table-wrap" data-testid="users-shift-report">
      <table className="data-table">
        <thead>
          <tr>
            <th>کاربر</th>
            <th>نقش</th>
            <th>شیفت</th>
            <th>فروش شیفت</th>
            <th>هزینه</th>
            <th>اختلاف</th>
            <th>جلسات</th>
            <th>درآمد جلسات</th>
            <th>حقوق پرداخت‌شده</th>
            <th>مطالبه پرسنل</th>
          </tr>
        </thead>
        <tbody>
          {(result?.items ?? []).map(item => (
            <tr key={item.userId} data-testid="users-shift-row">
              <td>
                <strong>{item.fullName}</strong>
                <small className="muted-line">@{item.userName} · {item.isActive ? 'فعال' : 'غیرفعال'}</small>
              </td>
              <td>{roleLabel(item.role)}</td>
              <td>{count(item.shiftCount)} / {count(item.closedShiftCount)}</td>
              <td>{money(item.shiftRevenue)} ت</td>
              <td>{money(item.shiftExpenses)} ت</td>
              <td>{money(item.shiftDifference)} ت</td>
              <td>{count(item.sessionCount)}</td>
              <td>{money(item.sessionRevenue)} ت</td>
              <td>{money(item.paidThisPeriod)} ت</td>
              <td>{money(item.employeePayable)} ت</td>
            </tr>
          ))}
          {!loading && !(result?.items.length) && <tr><td colSpan={10}>کاربری برای این فیلتر پیدا نشد.</td></tr>}
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
