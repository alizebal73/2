import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';

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

type Period = 'week' | 'month' | 'year' | 'custom';
type Role = 'operator' | 'manager' | 'owner';

export function ReportsPage() {
  const [period, setPeriod] = useState<Period>('week');
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

  useEffect(() => {
    void Promise.all([mockService.getReportRows(), mockService.getExpenses()]).then(([items, costs]) => { setRows(items); setExpenses(costs); });
    const onRole = (event: Event) => setRole((event as CustomEvent<Role>).detail);
    window.addEventListener('gamenet-role-change', onRole);
    return () => window.removeEventListener('gamenet-role-change', onRole);
  }, []);

  const visibleRows = useMemo(() => {
    const now = Date.now();
    const start = range ? range.start : period === 'month' ? now - 30 * 86400000 : period === 'year' ? now - 365 * 86400000 : now - 6 * 86400000;
    const end = range?.end ?? now;
    return rows.filter(row => {
      if (new Date(row.closedAt).getTime() < start || new Date(row.closedAt).getTime() > end) return false;
      if (role === 'operator' && row.operator !== 'علی محمدی') return false;
      if (operator !== 'all' && row.operator !== operator) return false;
      if (method !== 'all' && row.method !== method) return false;
      if (type !== 'all' && row.type !== type) return false;
      if (station !== 'all') {
        const zone = row.station.startsWith('PS') ? 'console' : row.station.startsWith('میز') ? 'table' : 'pc';
        if (zone !== station) return false;
      }
      return true;
    });
  }, [rows, range, period, role, operator, method, type, station]);

  const visibleExpenses = useMemo(() => {
    const now = Date.now();
    const start = range ? range.start : period === 'month' ? now - 30 * 86400000 : period === 'year' ? now - 365 * 86400000 : now - 6 * 86400000;
    const end = range?.end ?? now;
    return expenses.filter(item => {
      if (new Date(item.createdAt).getTime() < start || new Date(item.createdAt).getTime() > end) return false;
      if (role === 'operator' && item.operator !== 'علی محمدی') return false;
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
    expense: visibleExpenses.reduce((s, r) => s + r.amount, 0),
  };
  const net = totals.time + totals.buffet + totals.packageAmount - totals.expense;

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
    await mockService.addExpense({ title, amount: value, operator: 'علی محمدی' }); setExpenses(await mockService.getExpenses()); setNotice('هزینه ثبت شد');
  }

  async function closeShift() {
    const current = await mockService.getCurrentShift();
    if (!current) { setNotice('شیفت بازی برای بستن وجود ندارد'); return; }
    const counted = amount(window.prompt('وجه نقد شمارش‌شده (تومان)', String(totals.cash)) ?? '');
    const result = await mockService.closeShift(counted);
    setNotice('شیفت بسته شد؛ اختلاف صندوق ' + money(result.difference ?? 0) + ' تومان');
  }

  return <>
    {role === 'operator' && <div className="operation-toast" style={{ position:'relative', inset:'auto', margin:'8px 22px' }}>🔒 اپراتور فقط گزارش شیفت خودش را می‌بیند.</div>}
    <div className="page-header"><div><p>گزارش مالی و کارکرد</p><h1>گزارش‌ها</h1></div><div className="page-meta"><span>{visibleRows.length} تراکنش</span><span>{role === 'operator' ? 'شیفت شخصی' : 'گزارش کامل'}</span></div></div>
    <div className="toolbar">
      <div className="view-switch">{(['week','month','year','custom'] as Period[]).map(key => <button key={key} className={period===key?'active':''} onClick={() => { setPeriod(key); if (key !== 'custom') setRange(null); }}>{key==='week'?'۷ روز اخیر':key==='month'?'ماهانه (۶ ماه)':key==='year'?'سالانه':'📅 بازه دلخواه'}</button>)}</div>
      <button className="btn" onClick={exportCsv}>📤 خروجی اکسل</button><button className="btn" onClick={() => window.print()}>🖨 چاپ گزارش</button><button className="btn primary" onClick={() => void closeShift()}>🔒 بستن شیفت امروز</button><button className="btn" onClick={() => void registerExpense()}>➖ ثبت هزینه</button>
    </div>
    {period==='custom' && <div className="card-panel" style={{margin:'0 22px 12px',padding:14}}><div style={{display:'flex',gap:8,flexWrap:'wrap',alignItems:'end'}}><label>از تاریخ (شمسی)<input value={from} onChange={e=>setFrom(e.target.value)} placeholder="۱۴۰۵/۰۷/۰۱"/></label><label>تا تاریخ<input value={to} onChange={e=>setTo(e.target.value)} placeholder="۱۴۰۵/۰۷/۰۹"/></label><label>ساعت از<input type="time" value={fromTime} onChange={e=>setFromTime(e.target.value)}/></label><label>ساعت تا<input type="time" value={toTime} onChange={e=>setToTime(e.target.value)}/></label><button className="btn primary sm" onClick={applyRange}>🔍 اعمال بازه</button><button className="btn sm" onClick={()=>{setRange(null);setPeriod('week')}}>↩️ بازنشانی</button></div><div style={{display:'flex',gap:6,flexWrap:'wrap',marginTop:10}}>{['امروز','دیروز','این هفته','ماه جاری','ماه قبل','۹۰ روز اخیر','امسال'].map(name=><button className="btn sm" key={name} onClick={()=>preset(name)}>{name}</button>)}</div></div>}
    <div className="toolbar" style={{paddingTop:4}}><select value={station} onChange={e=>setStation(e.target.value)}><option value="all">همه ایستگاه‌ها</option><option value="pc">رایانه‌ها</option><option value="console">کنسول‌ها</option><option value="table">میزها</option></select><select value={operator} onChange={e=>setOperator(e.target.value)}><option value="all">همه اپراتورها</option><option>علی محمدی</option><option>سارا احمدی</option><option>رضا کاظمی</option></select><select value={method} onChange={e=>setMethod(e.target.value)}><option value="all">همه پرداخت‌ها</option><option value="cash">نقدی</option><option value="card">کارت</option><option value="wallet">کیف پول</option></select><select value={type} onChange={e=>setType(e.target.value)}><option value="all">همه تراکنش‌ها</option><option value="time">زمان</option><option value="buffet">بوفه</option><option value="package">پکیج</option><option value="expense">هزینه</option></select></div>
    <div className="summary-grid">{[['درآمد زمان',totals.time,'blue'],['فروش بوفه',totals.buffet,'orange'],['پکیج',totals.packageAmount,'purple'],['هزینه',totals.expense,'red'],['سود خالص',net,'green'],['نقد',totals.cash,'orange'],['کارت',totals.card,'blue'],['کیف پول',totals.wallet,'purple']].map(item=><div className="summary-card" key={String(item[0])}><div className="label">{item[0]}</div><div className={'value '+item[2]}>{money(Number(item[1]))} تومان</div></div>)}</div>
    <div className="report-grid"><div className="chart-box"><h3>تفکیک نقد / کارت</h3><div className="bar-chart"><div className="bar" style={{height:'68%'}}/><div className="bar" style={{height:'52%'}}/><div className="bar" style={{height:'74%'}}/><div className="bar" style={{height:'44%'}}/></div></div><div className="chart-box"><h3>کارکرد ایستگاه‌ها</h3>{visibleRows.slice(0,8).map(row=><div key={row.id} className="info-row"><span>{row.station}</span><strong>{money(row.amount)} ت</strong></div>)}</div></div>
    <div className="table-wrap"><table className="data-table"><thead><tr><th>تاریخ</th><th>ایستگاه</th><th>مبلغ</th><th>روش</th><th>اپراتور</th></tr></thead><tbody>{visibleRows.map(row=><tr key={row.id}><td>{new Date(row.closedAt).toLocaleString('fa-IR')}</td><td>{row.station}</td><td>{money(row.amount)} ت</td><td>{row.method}</td><td>{row.operator}</td></tr>)}{visibleExpenses.map(row=><tr key={row.id}><td>{new Date(row.createdAt).toLocaleString('fa-IR')}</td><td>هزینه: {row.title}</td><td>−{money(row.amount)} ت</td><td>هزینه</td><td>{row.operator}</td></tr>)}</tbody></table></div>
    {notice && <div className="operation-toast">{notice}<button onClick={()=>setNotice('')}>×</button></div>}
  </>;
}
