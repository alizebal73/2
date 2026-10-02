import { useEffect, useMemo, useState } from 'react';
import { getFinanceExpenses, getFinanceSummary, getFinanceTransactions, createShiftExpense } from '../services/financeService';
import { getCurrentShift } from '../services/shiftService';
import { getServerBuffetProfit } from '../services/buffetService';
import type { BuffetProfitReport } from '../types';

function money(value: number) { return new Intl.NumberFormat('fa-IR').format(Math.round(value)); }
function parsePersianDate(value: string): Date | null {
  const parts = value.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).replace(/-/g, '/').split('/').map(Number);
  if (parts.length !== 3 || parts.some(Number.isNaN)) return null;
  const [py, pm, pd] = parts;
  const formatter = new Intl.DateTimeFormat('en-u-ca-persian-nu-latn', { year: 'numeric', month: 'numeric', day: 'numeric', timeZone: 'UTC' });
  for (let t = Date.UTC(py + 621, 0, 1); t <= Date.UTC(py + 622, 11, 31); t += 86400000) {
    const date = new Date(t);
    const p = formatter.formatToParts(date);
    if (Number(p.find(x => x.type === 'year')?.value) === py && Number(p.find(x => x.type === 'month')?.value) === pm && Number(p.find(x => x.type === 'day')?.value) === pd) return date;
  }
  return null;
}
function amount(value: string) { return Number(value.replace(/[۰-۹]/g, d => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace(/[٬,\s]/g, '')) || 0; }

type Period = 'week' | 'month' | 'sixMonths' | 'year' | 'custom';
type ReportCategory = 'finance' | 'sessions' | 'customers' | 'buffet' | 'users' | 'audit';
type Role = 'operator' | 'manager' | 'owner';

export function ReportsPage() {
  const [period, setPeriod] = useState<Period>('week');
  const [reportCategory, setReportCategory] = useState<ReportCategory>('finance');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [fromTime, setFromTime] = useState('00:00');
  const [toTime, setToTime] = useState('23:59');
  const [range, setRange] = useState<{ start: number; end: number } | null>(null);
  const [station, setStation] = useState('all');
  const [operator, setOperator] = useState('all');
  const [method, setMethod] = useState('all');
  const [type, setType] = useState('all');
  const [role, setRole] = useState<Role>('operator');
  const [rows, setRows] = useState<any[]>([]);
  const [expenses, setExpenses] = useState<any[]>([]);
  const [notice, setNotice] = useState('');
  const [financeSummary, setFinanceSummary] = useState<{ revenue: number; expense: number; operatingProfit: number; source: 'server' } | null>(null);
  const [financeError, setFinanceError] = useState('');
  const [buffetProfit, setBuffetProfit] = useState<BuffetProfitReport | null>(null);
  const [buffetProfitError, setBuffetProfitError] = useState('');

  useEffect(() => {
    setFinanceError('');
    void Promise.all([getFinanceTransactions(), getFinanceSummary(), getFinanceExpenses()])
      .then(([transactions, finance, costs]) => {
        const items = transactions.filter(item => item.status === 'Paid').map(item => ({
          id: item.id,
          closedAt: item.closedAt,
          station: item.description,
          timeAmount: item.amount,
          buffet: 0,
          packageAmount: 0,
          amount: item.amount,
          method: item.method,
          operator: 'سرور',
          type: 'time',
        }));
        setRows(items);
        setFinanceSummary(finance);
        setExpenses(costs.map(item => ({ ...item, title: item.description ?? item.category, operator: 'سرور' })));
      })
      .catch(error => setFinanceError(error instanceof Error ? error.message : 'دریافت اطلاعات مالی انجام نشد'));
    const onRole = (event: Event) => setRole((event as CustomEvent<Role>).detail);
    window.addEventListener('gamenet-role-change', onRole);
    return () => window.removeEventListener('gamenet-role-change', onRole);
  }, []);

  useEffect(() => {
    if (reportCategory !== 'buffet') return;
    const now = Date.now();
    const start = range
      ? range.start
      : period === 'month' ? now - 30 * 86400000
      : period === 'sixMonths' ? now - 180 * 86400000
      : period === 'year' ? now - 365 * 86400000
      : now - 6 * 86400000;
    const end = range?.end ?? now;
    setBuffetProfitError('');
    setBuffetProfit(null);
    void getServerBuffetProfit(new Date(start).toISOString(), new Date(end).toISOString())
      .then(setBuffetProfit)
      .catch(error => setBuffetProfitError(error instanceof Error ? error.message : 'گزارش سود بوفه دریافت نشد'));
  }, [reportCategory, period, range]);

  const visibleRows = useMemo(() => {
    const now = Date.now();
    const start = range
      ? range.start
      : period === 'month' ? now - 30 * 86400000
      : period === 'sixMonths' ? now - 180 * 86400000
      : period === 'year' ? now - 365 * 86400000
      : now - 6 * 86400000;
    const end = range?.end ?? now;
    return rows.filter(row => {
      if (new Date(row.closedAt).getTime() < start || new Date(row.closedAt).getTime() > end) return false;
      if (role === 'operator' && row.operator !== 'علی محمدی' && row.operator !== 'سرور') return false;
      if (operator !== 'all' && row.operator !== operator) return false;
      if (method !== 'all' && row.method !== method) return false;
      if (type === 'expense') return false;
      if (type !== 'all' && row.type !== type) return false;
      if (station !== 'all') {
        const zone = row.station.startsWith('PS') ? 'console' : row.station.startsWith('میز') ? 'table' : 'pc';
        if (zone !== station) return false;
      }
      return true;
    });
  }, [rows, range, period, role, operator, method, type, station]);

  const visibleExpenses = useMemo(() => {
    if (type !== 'expense' && type !== 'all') return [];
    const now = Date.now();
    const start = range
      ? range.start
      : period === 'month' ? now - 30 * 86400000
      : period === 'sixMonths' ? now - 180 * 86400000
      : period === 'year' ? now - 365 * 86400000
      : now - 6 * 86400000;
    const end = range?.end ?? now;
    return expenses.filter(item => {
      if (new Date(item.createdAt).getTime() < start || new Date(item.createdAt).getTime() > end) return false;
      if (role === 'operator' && item.operator !== 'علی محمدی' && item.operator !== 'سرور') return false;
      return operator === 'all' || item.operator === operator;
    });
  }, [expenses, range, period, role, operator]);

  const totals = {
    time: visibleRows.reduce((s, r) => s + r.timeAmount, 0),
    buffet: visibleRows.reduce((s, r) => s + r.buffet, 0),
    packageAmount: visibleRows.reduce((s, r) => s + r.packageAmount, 0),
    cash: visibleRows.filter(r => r.method === 'cash').reduce((s, r) => s + r.amount, 0),
    card: visibleRows.filter(r => r.method === 'card').reduce((s, r) => s + r.amount, 0),
    wallet: visibleRows.filter(r => r.method === 'wallet').reduce((s, r) => s + r.amount, 0),
    gift: visibleRows.filter(r => r.method === 'gift').reduce((s, r) => s + r.amount, 0),
    expense: visibleExpenses.reduce((s, r) => s + r.amount, 0),
  };
  const net = financeSummary ? financeSummary.operatingProfit : totals.time + totals.buffet + totals.packageAmount - totals.expense;
  const reportRevenue = financeSummary ? financeSummary.revenue : totals.time + totals.buffet + totals.packageAmount;
  const reportExpense = financeSummary ? financeSummary.expense : totals.expense;

  function preset(name: string) {
    const end = new Date(); const start = new Date(end);
    if (name === 'امروز') { /* همان روز */ }
    else if (name === 'دیروز') { start.setDate(start.getDate() - 1); end.setDate(end.getDate() - 1); }
    else if (name === 'این هفته') start.setDate(start.getDate() - 6);
    else if (name === 'ماه جاری') start.setDate(1);
    else if (name === 'ماه قبل') { start.setMonth(start.getMonth() - 1, 1); end.setDate(0); }
    else if (name === '۹۰ روز اخیر') start.setDate(start.getDate() - 89);
    else if (name === 'امسال') start.setMonth(0, 1);
    setFrom(start.toLocaleDateString('fa-IR-u-ca-persian').replaceAll('-', '/'));
    setTo(end.toLocaleDateString('fa-IR-u-ca-persian').replaceAll('-', '/'));
    setRange({ start: new Date(start.setHours(0,0,0,0)).getTime(), end: new Date(end.setHours(23,59,59,999)).getTime() });
    setPeriod('custom');
  }

  function applyRange() {
    const start = parsePersianDate(from), end = parsePersianDate(to);
    if (!start || !end) { setNotice('تاریخ شمسی معتبر نیست'); return; }
    const [sh, sm] = fromTime.split(':').map(Number);
    const [eh, em] = toTime.split(':').map(Number);
    const startMs = Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate(), sh, sm);
    const endMs = Date.UTC(end.getUTCFullYear(), end.getUTCMonth(), end.getUTCDate(), eh, em, 59, 999);
    if (startMs > endMs) { setNotice('ساعت پایان باید بعد از شروع باشد'); return; }
    setRange({ start: startMs, end: endMs }); setPeriod('custom'); setNotice('بازه گزارش اعمال شد');
  }

  function exportCsv() {
    const lines = [['تاریخ','ایستگاه','زمان','بوفه','پکیج','مبلغ','روش پرداخت','اپراتور'],
      ...visibleRows.map(row => [new Date(row.closedAt).toLocaleString('fa-IR'), row.station, row.timeAmount, row.buffet, row.packageAmount, row.amount, row.method, row.operator]),
      ...visibleExpenses.map(row => [new Date(row.createdAt).toLocaleString('fa-IR'), 'هزینه: ' + row.title, 0, 0, 0, -row.amount, 'expense', row.operator])];
    const csv = lines.map(line => line.map(value => '"' + String(value).replace(/"/g, '""') + '"').join(',')).join('\r\n');
    const link = document.createElement('a'); link.href = URL.createObjectURL(new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8' })); link.download = 'gamenet-report.csv'; link.click(); URL.revokeObjectURL(link.href);
  }

  async function registerExpense() {
    const title = window.prompt('شرح هزینه'); if (!title) return;
    const value = amount(window.prompt('مبلغ هزینه به تومان', '100000') ?? ''); if (!value) { setNotice('مبلغ معتبر نیست'); return; }
    try {
      const current = await getCurrentShift();
      if (!current) { setNotice('برای ثبت هزینه ابتدا یک شیفت باز کنید'); return; }
      await createShiftExpense(current.id, { amount: value, category: 'سایر', description: title });
      const costs = await getFinanceExpenses();
      setExpenses(costs);
      setNotice('هزینه روی شیفت سرور ثبت شد');
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'ثبت هزینه انجام نشد');
    }
  }

  return <>
    {role === 'operator' && <div className="operation-toast" style={{ position:'relative', inset:'auto', margin:'8px 22px' }}>🔒 اپراتور فقط گزارش شیفت خودش را می‌بیند.</div>}
    {financeError && <div className="user-error-banner network"><div className="user-error-icon">!</div><div className="user-error-copy"><strong>دریافت اطلاعات مالی کامل نشد</strong><span>{financeError}</span></div><button type="button" className="btn sm" onClick={() => window.location.reload()}>تلاش مجدد</button></div>}
    <div className="page-header"><div><p>مرکز گزارش</p><h1>گزارش‌ها</h1></div><div className="page-meta"><span>{visibleRows.length} تراکنش</span><span>{role === 'operator' ? 'شیفت شخصی' : 'گزارش کامل'}</span></div></div>

    <section className="report-center-head">
      <div className="report-categories">
        {([
          ['finance','مالی'],
          ['sessions','جلسات و ایستگاه‌ها'],
          ['customers','مشتری و VIP'],
          ['buffet','بوفه و موجودی'],
          ['users','کاربران و شیفت'],
          ['audit','Audit']
        ] as Array<[ReportCategory,string]>).map(([key,label]) => <button key={key} className={reportCategory === key ? 'active' : ''} onClick={() => setReportCategory(key)}>{label}</button>)}
      </div>
      <div className="report-periods">
        {([
          ['week','۷ روز اخیر'],
          ['month','۳۰ روز اخیر'],
          ['sixMonths','۶ ماه اخیر'],
          ['year','امسال'],
          ['custom','بازه دلخواه']
        ] as Array<[Period,string]>).map(([key,label]) => <button key={key} className={period === key ? 'active' : ''} onClick={() => { setPeriod(key); if (key !== 'custom') setRange(null); }}>{label}</button>)}
      </div>
      <div className="report-actions"><button className="btn" onClick={exportCsv}>📤 خروجی</button><button className="btn" onClick={() => window.print()}>🖨 چاپ</button><button className="btn" onClick={() => void registerExpense()}>➖ ثبت هزینه</button></div>
    </section>

    {period === 'custom' && <section className="card-panel report-range-panel"><div className="report-range-grid"><label>از تاریخ<input value={from} onChange={e=>setFrom(e.target.value)} placeholder="۱۴۰۵/۰۷/۰۱"/></label><label>تا تاریخ<input value={to} onChange={e=>setTo(e.target.value)} placeholder="۱۴۰۵/۰۷/۰۹"/></label><label>از ساعت<input type="time" value={fromTime} onChange={e=>setFromTime(e.target.value)}/></label><label>تا ساعت<input type="time" value={toTime} onChange={e=>setToTime(e.target.value)}/></label><button className="btn primary" onClick={applyRange}>اعمال بازه</button><button className="btn" onClick={()=>{setRange(null);setPeriod('week')}}>بازنشانی</button></div><div className="report-presets">{['امروز','دیروز','این هفته','ماه جاری','ماه قبل','۹۰ روز اخیر','امسال'].map(name=><button className="btn sm" key={name} onClick={()=>preset(name)}>{name}</button>)}</div></section>}

    {reportCategory === 'finance' ? <>
      <section className="report-filter-grid"><select value={station} onChange={e=>setStation(e.target.value)}><option value="all">همه ایستگاه‌ها</option><option value="pc">رایانه‌ها</option><option value="console">کنسول‌ها</option><option value="table">میزها</option></select><select value={operator} onChange={e=>setOperator(e.target.value)}><option value="all">همه اپراتورها</option><option>علی محمدی</option><option>سارا احمدی</option><option>رضا کاظمی</option></select><select value={method} onChange={e=>setMethod(e.target.value)}><option value="all">همه پرداخت‌ها</option><option value="cash">نقدی</option><option value="card">کارت</option><option value="wallet">کیف پول</option><option value="gift">اعتبار رایگان</option></select><select value={type} onChange={e=>setType(e.target.value)}><option value="all">همه تراکنش‌ها</option><option value="time">زمان</option><option value="buffet">بوفه</option><option value="package">پکیج</option><option value="expense">هزینه</option></select></section>
      <div className="summary-grid">{[['درآمد ثبت‌شده',reportRevenue,'blue'],['درآمد زمان',totals.time,'blue'],['فروش بوفه',totals.buffet,'orange'],['پکیج',totals.packageAmount,'purple'],['هزینه ثبت‌شده',reportExpense,'red'],['سود عملیاتی',net,'green'],['نقد',totals.cash,'orange'],['کارت',totals.card,'blue'],['کیف پول',totals.wallet,'purple'],['اعتبار رایگان',totals.gift,'blue']].map(item=><div className="summary-card" key={String(item[0])}><div className="label">{item[0]}</div><div className={'value '+item[2]}>{money(Number(item[1]))} تومان</div></div>)}</div>
      <div className="report-grid"><div className="chart-box"><h3>تفکیک پرداخت</h3><div className="report-kpi-list"><div><span>نقد</span><strong>{money(totals.cash)} تومان</strong></div><div><span>کارت</span><strong>{money(totals.card)} تومان</strong></div><div><span>کیف پول</span><strong>{money(totals.wallet)} تومان</strong></div><div><span>اعتبار رایگان</span><strong>{money(totals.gift)} تومان</strong></div></div></div><div className="chart-box"><h3>ایستگاه‌ها</h3>{visibleRows.slice(0,8).map(row=><div key={row.id} className="info-row"><span>{row.station}</span><strong>{money(row.amount)} ت</strong></div>)}</div></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>تاریخ</th><th>شرح</th><th>مبلغ</th><th>روش</th><th>اپراتور</th></tr></thead><tbody>{visibleRows.map(row=><tr key={row.id}><td>{new Date(row.closedAt).toLocaleString('fa-IR')}</td><td>{row.station}</td><td>{money(row.amount)} ت</td><td>{row.method}</td><td>{row.operator}</td></tr>)}{visibleExpenses.map(row=><tr key={row.id}><td>{new Date(row.createdAt).toLocaleString('fa-IR')}</td><td>هزینه: {row.title}</td><td>−{money(row.amount)} ت</td><td>هزینه</td><td>{row.operator}</td></tr>)}</tbody></table></div>
    </> : reportCategory === 'buffet' ? <>
      {buffetProfitError && <div className="user-error-banner network"><div className="user-error-icon">!</div><div className="user-error-copy"><strong>گزارش سود بوفه دریافت نشد</strong><span>{buffetProfitError}</span></div></div>}
      {buffetProfit && <><div className="summary-grid">
        {[
          ['فروش بوفه', buffetProfit.totals.salesRevenue, 'orange'],
          ['بهای کالای فروخته‌شده', buffetProfit.totals.salesCost, 'red'],
          ['سود ناخالص', buffetProfit.totals.grossProfit, 'green'],
          ['خرید ثبت‌شده', buffetProfit.totals.purchaseCost, 'blue'],
          ['هزینه ضایعات', buffetProfit.totals.wasteCost, 'red'],
          ['اثر مرجوعی', buffetProfit.totals.returnRevenue - buffetProfit.totals.returnCost, 'purple'],
        ].map(item => <div className="summary-card" key={String(item[0])}><div className="label">{item[0]}</div><div className={'value ' + item[2]}>{money(Number(item[1]))} تومان</div></div>)}
      </div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>کالا</th><th>فروش</th><th>درآمد</th><th>بهای تمام‌شده</th><th>مرجوعی</th><th>سود ناخالص</th></tr></thead><tbody>
        {buffetProfit.products.filter(item => item.salesQuantity || item.purchaseQuantity || item.wasteQuantity || item.returnQuantity).map(item =>
          <tr key={item.productId}>
            <td>{item.productName}</td>
            <td>{item.salesQuantity}</td>
            <td>{money(item.salesRevenue)} ت</td>
            <td>{money(item.salesCost)} ت</td>
            <td>{item.returnQuantity}</td>
            <td>{money(item.grossProfit)} ت</td>
          </tr>
        )}
      </tbody></table></div></>}
    </> : <section className="report-placeholder"><strong>{({sessions:'جلسات و ایستگاه‌ها',customers:'مشتری و VIP',users:'کاربران و شیفت',audit:'Audit'} as Record<string,string>)[reportCategory]}</strong><span>ساختار این گزارش آماده شده است؛ اتصال منبع داده این دامنه باید قبل از نمایش عدد انجام شود.</span></section>

    {notice && <div className="operation-toast">{notice}<button onClick={()=>setNotice('')}>×</button></div>}
  </>;
}
