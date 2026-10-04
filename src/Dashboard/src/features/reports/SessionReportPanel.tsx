import { useEffect, useState } from 'react';
import { getSessionReport, type SessionReportPage } from '../../services/sessionReportService';

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

function stateLabel(value: string) {
  return ({
    Active: 'فعال',
    Ended: 'پایان‌یافته',
    Completed: 'تسویه‌شده',
    Cancelled: 'لغوشده',
  } as Record<string, string>)[value] ?? value;
}

function windowFor(period: Period, range: Props['range']) {
  const now = Date.now();
  if (range) return range;
  const start = new Date(now);
  if (period === 'month') start.setDate(start.getDate() - 30);
  else if (period === 'sixMonths') start.setDate(start.getDate() - 180);
  else if (period === 'year') start.setDate(start.getDate() - 365);
  else start.setDate(start.getDate() - 6);
  return { start: start.getTime(), end: now };
}

function exportCsv(report: SessionReportPage | null) {
  if (!report) return;
  const lines = [
    ['تاریخ شروع', 'ایستگاه', 'منطقه', 'مشتری', 'اپراتور', 'وضعیت', 'مدت', 'مبلغ'],
    ...report.items.map(item => [
      new Date(item.startAt).toLocaleString('fa-IR'),
      item.stationName,
      item.zone,
      item.customerName,
      item.operator,
      stateLabel(item.state),
      minutes(item.billableMinutes),
      item.totalAmount,
    ]),
  ];
  const csv = lines
    .map(line => line.map(value => '"' + String(value).replace(/"/g, '""') + '"').join(','))
    .join('\r\n');
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' }));
  link.download = 'gamenet-sessions-report.csv';
  link.click();
  URL.revokeObjectURL(link.href);
}

export function SessionReportPanel({ period, range }: Props) {
  const [station, setStation] = useState('');
  const [zone, setZone] = useState('');
  const [operator, setOperator] = useState('');
  const [state, setState] = useState('');
  const [customerSearch, setCustomerSearch] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<SessionReportPage | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const window = windowFor(period, range);
    setLoading(true);
    setError('');

    void getSessionReport({
      from: new Date(window.start),
      to: new Date(window.end),
      station,
      zone,
      operator,
      state: state as SessionReportState,
      customerSearch,
      page,
      pageSize: 50,
    })
      .then(setResult)
      .catch(reason => setError(reason instanceof Error ? reason.message : 'دریافت گزارش جلسات انجام نشد'))
      .finally(() => setLoading(false));
  }, [period, range, station, zone, operator, state, customerSearch, page]);

  const totalPages = Math.max(1, Math.ceil((result?.total ?? 0) / (result?.pageSize ?? 50)));

  return <>
    {error && (
      <div className="user-error-banner network">
        <div className="user-error-icon">!</div>
        <div className="user-error-copy">
          <strong>دریافت گزارش جلسات کامل نشد</strong>
          <span>{error}</span>
        </div>
        <button type="button" className="btn sm" onClick={() => setPage(value => value)}>تلاش مجدد</button>
      </div>
    )}

    <section className="report-filter-grid report-session-filter-grid">
      <label>ایستگاه
        <input value={station} onChange={event => { setStation(event.target.value); setPage(1); }} placeholder="مثلاً PC ۰۱" />
      </label>
      <label>منطقه
        <select value={zone} onChange={event => { setZone(event.target.value); setPage(1); }}>
          <option value="">همه</option>
          <option value="pc">رایانه</option>
          <option value="console">کنسول</option>
          <option value="table">میز</option>
        </select>
      </label>
      <label>اپراتور
        <input value={operator} onChange={event => { setOperator(event.target.value); setPage(1); }} placeholder="نام یا نام کاربری" />
      </label>
      <label>وضعیت
        <select value={state} onChange={event => { setState(event.target.value); setPage(1); }}>
          <option value="">همه وضعیت‌ها</option>
          <option value="Completed">تسویه‌شده</option>
          <option value="Ended">پایان‌یافته</option>
          <option value="Active">فعال</option>
          <option value="Cancelled">لغوشده</option>
        </select>
      </label>
      <label>مشتری
        <input value={customerSearch} onChange={event => { setCustomerSearch(event.target.value); setPage(1); }} placeholder="نام، کد یا نام کاربری" />
      </label>
      <button
        type="button"
        className="btn"
        onClick={() => {
          setStation('');
          setZone('');
          setOperator('');
          setState('');
          setCustomerSearch('');
          setPage(1);
        }}
      >
        پاک کردن فیلتر
      </button>
    </section>

    <div className="summary-grid">
      {[
        ['تعداد جلسات', result?.summary.sessionCount ?? 0, 'blue'],
        ['زمان استفاده', minutes(result?.summary.billableMinutes ?? 0), 'purple'],
        ['درآمد جلسات', money(result?.summary.revenue ?? 0) + ' تومان', 'green'],
        ['میانگین جلسه', minutes(result?.summary.averageMinutes ?? 0), 'orange'],
      ].map(item => (
        <div className="summary-card" key={String(item[0])}>
          <div className="label">{item[0]}</div>
          <div className={'value ' + item[2]}>
            {typeof item[1] === 'number' ? count(item[1]) : item[1]}
          </div>
        </div>
      ))}
    </div>

    <section className="report-grid">
      <div className="chart-box">
        <h3>ایستگاه‌های پرتکرار</h3>
        <div className="report-kpi-list">
          {(result?.summary.stations ?? []).slice(0, 8).map(item => (
            <div key={item.stationId}>
              <span>{item.stationName}</span>
              <strong>{count(item.sessionCount)} جلسه · {money(item.revenue)} ت</strong>
            </div>
          ))}
          {!loading && !(result?.summary.stations.length) && <div><span>رکوردی وجود ندارد</span><strong>—</strong></div>}
        </div>
      </div>
      <div className="chart-box">
        <h3>وضعیت گزارش</h3>
        <div className="report-kpi-list">
          <div><span>رکورد این صفحه</span><strong>{count(result?.items.length ?? 0)}</strong></div>
          <div><span>کل رکوردهای بازه</span><strong>{count(result?.total ?? 0)}</strong></div>
          <div><span>وضعیت داده</span><strong>{loading ? 'در حال دریافت…' : 'سرور'}</strong></div>
        </div>
      </div>
    </section>

    <div className="report-actions" style={{ margin: '0 22px 10px' }}>
      <button type="button" className="btn" onClick={() => exportCsv(result)}>📤 خروجی جلسات</button>
      <span className="page-meta"><span>{count(result?.total ?? 0)} جلسه در بازه</span></span>
    </div>

    <div className="table-wrap" data-testid="session-report">
      <table className="data-table">
        <thead>
          <tr>
            <th>تاریخ شروع</th>
            <th>ایستگاه</th>
            <th>مشتری</th>
            <th>اپراتور</th>
            <th>وضعیت</th>
            <th>مدت</th>
            <th>مبلغ</th>
          </tr>
        </thead>
        <tbody>
          {(result?.items ?? []).map(item => (
            <tr key={item.id} data-testid="session-report-row">
              <td>{new Date(item.startAt).toLocaleString('fa-IR')}</td>
              <td>{item.stationName}</td>
              <td>
                {item.customerName}
                {item.customerCode ? <small className="muted-line">{item.customerCode}</small> : null}
              </td>
              <td>{item.operator}</td>
              <td>{stateLabel(item.state)}</td>
              <td>{minutes(item.billableMinutes)}</td>
              <td>{money(item.totalAmount)} ت</td>
            </tr>
          ))}
          {!loading && !(result?.items.length) && (
            <tr><td colSpan={7}>جلسه‌ای برای این فیلتر پیدا نشد.</td></tr>
          )}
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

type SessionReportState = 'Active' | 'Ended' | 'Completed' | 'Cancelled' | '';
