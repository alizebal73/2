
import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import type { AuditLogRecord, ExpenseRecord, ManagementInvoiceRecord, ReservationRecord, StationManagementRecord, VipPackageRecord, ProductRecord } from '../types';

type Tab = 'stations' | 'reservations' | 'inventory' | 'vip' | 'expenses' | 'audit' | 'invoices';

const money = (value: number) => new Intl.NumberFormat('fa-IR').format(value);
const dt = (value: string) => new Date(value).toLocaleString('fa-IR', { dateStyle: 'short', timeStyle: 'short' });

export function OperationsPage() {
  const [tab, setTab] = useState<Tab>('stations');
  const [stations, setStations] = useState<StationManagementRecord[]>([]);
  const [reservations, setReservations] = useState<ReservationRecord[]>([]);
  const [products, setProducts] = useState<ProductRecord[]>([]);
  const [packages, setPackages] = useState<VipPackageRecord[]>([]);
  const [expenses, setExpenses] = useState<ExpenseRecord[]>([]);
  const [audits, setAudits] = useState<AuditLogRecord[]>([]);
  const [invoices, setInvoices] = useState<ManagementInvoiceRecord[]>([]);
  const [waitlist, setWaitlist] = useState<Array<{ id: string; customerCode: string; customerName: string; stationType: string; createdAt: string; status: 'waiting' | 'assigned' }>>([]);
  const [notice, setNotice] = useState('');

  const [reservation, setReservation] = useState({ stationId: 'station-4', customerCode: '1050', customerName: 'رضا محمدی', minutes: '120', note: '' });
  const [expense, setExpense] = useState({ title: '', amount: '', category: 'خرید/تأمین' });
  const [stockAdd, setStockAdd] = useState<Record<string, string>>({});
  const [transfer, setTransfer] = useState({ from: 'PC ۰۴', to: 'PC ۰۵' });
  const [stationFilter, setStationFilter] = useState('');
  const [auditFilter, setAuditFilter] = useState('');

  async function loadAll() {
    const result = await Promise.all([
      mockService.getManagedStations(),
      mockService.getReservations(),
      mockService.getProducts(),
      mockService.getVipPackages(),
      mockService.getManagementExpenses(),
      mockService.getAuditLogs(),
      mockService.getManagementInvoices(),
      mockService.getWaitlist(),
    ]);
    setStations(result[0]); setReservations(result[1]); setProducts(result[2]); setPackages(result[3]);
    setExpenses(result[4]); setAudits(result[5]); setInvoices(result[6]); setWaitlist(result[7]);
  }
  useEffect(() => { void loadAll(); }, []);

  const filteredStations = useMemo(() => stations.filter(item => !stationFilter || item.name.includes(stationFilter) || item.type.includes(stationFilter)), [stations, stationFilter]);
  const filteredAudits = useMemo(() => audits.filter(item => !auditFilter || item.operator.includes(auditFilter) || item.action.includes(auditFilter) || item.target.includes(auditFilter)), [audits, auditFilter]);

  async function toggleStation(item: StationManagementRecord) {
    await mockService.toggleStationOutOfService(item.id);
    await loadAll();
    setNotice(item.status === 'off' ? 'ایستگاه فعال شد' : 'ایستگاه خارج از سرویس شد');
  }
  async function createReservation() {
    const station = stations.find(item => item.id === reservation.stationId);
    if (!station) return;
    await mockService.saveReservation({ id: crypto.randomUUID(), stationId: station.id, stationName: station.name, customerCode: reservation.customerCode, customerName: reservation.customerName, reservedAt: new Date(Date.now() + 60 * 60000).toISOString(), durationMinutes: Number(reservation.minutes) || 60, status: 'confirmed', note: reservation.note });
    await loadAll(); setNotice('رزرو ثبت شد');
  }
  async function addExpense() {
    const amount = Number(expense.amount);
    if (!expense.title || !amount) return;
    await mockService.addExpense({ title: expense.title, amount, category: expense.category, operator: 'علی محمدی' });
    setExpense({ title: '', amount: '', category: 'خرید/تأمین' });
    await loadAll(); setNotice('هزینه ثبت شد');
  }
  async function addStock(product: ProductRecord) {
    const quantity = Number(stockAdd[product.id] || 0);
    if (quantity <= 0) return;
    await mockService.addStock(product.id, quantity);
    setStockAdd(current => ({ ...current, [product.id]: '' }));
    await loadAll(); setNotice('ورودی انبار ثبت شد');
  }
  async function doTransfer() {
    await mockService.transferSession(transfer.from, transfer.to);
    await loadAll(); setNotice('انتقال جلسه در Audit ثبت شد');
  }
  async function assignWait(id: string) {
    await mockService.assignWaitlist(id, transfer.to);
    await loadAll(); setNotice('نفر صف به ایستگاه تخصیص داده شد');
  }

  const tabs: Array<[Tab, string]> = [
    ['stations', '🖥 ایستگاه‌ها'], ['reservations', '📅 رزرو و صف'], ['inventory', '📦 کالا و انبار'],
    ['vip', '⭐ پکیج VIP'], ['expenses', '💳 هزینه‌ها'], ['audit', '🧾 Audit Log'], ['invoices', '🧮 فاکتورها و جلسات'],
  ];

  return <>
    <div className="page-header">
      <div><p>تکمیل Stage 1B</p><h1>مرکز مدیریت</h1></div>
      {notice && <div className="operation-toast" onClick={() => setNotice('')}>{notice}</div>}
    </div>
    <section className="card-panel" style={{ margin: '0 22px 14px', padding: 10, overflowX: 'auto' }}>
      <div style={{ display: 'flex', gap: 8, minWidth: 900 }}>
        {tabs.map(([key, label]) => <button type="button" className={tab === key ? 'btn primary' : 'btn'} key={key} onClick={() => setTab(key)}>{label}</button>)}
      </div>
    </section>

    {tab === 'stations' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <div className="section-toolbar"><input value={stationFilter} onChange={e => setStationFilter(e.target.value)} placeholder="جست‌وجوی ایستگاه…" /><span>{filteredStations.length} ایستگاه</span></div>
      <div className="table-wrap"><table><thead><tr><th>ایستگاه</th><th>نوع</th><th>زون</th><th>تعرفه/ساعت</th><th>وضعیت</th><th>IP</th><th>عملیات</th></tr></thead><tbody>
        {filteredStations.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.type}</td><td>{item.zone}</td><td>{money(item.ratePerHour)}</td><td>{item.status === 'off' ? 'خارج از سرویس' : item.status === 'reserved' ? 'رزرو' : 'فعال'}</td><td dir="ltr">{item.ip || '—'}</td><td><button className="btn" onClick={() => void toggleStation(item)}>{item.status === 'off' ? 'فعال‌سازی' : 'خارج از سرویس'}</button></td></tr>)}
      </tbody></table></div>
    </section>}

    {tab === 'reservations' && <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, padding: '0 22px 30px' }}>
      <section className="card-panel" style={{ padding: 14 }}><h3>رزرو جدید</h3>
        <label>ایستگاه<select value={reservation.stationId} onChange={e => setReservation(v => ({ ...v, stationId: e.target.value }))}>{stations.filter(s => s.status !== 'off').map(s => <option value={s.id} key={s.id}>{s.name}</option>)}</select></label>
        <label>کد مشتری<input value={reservation.customerCode} onChange={e => setReservation(v => ({ ...v, customerCode: e.target.value }))} /></label>
        <label>نام<input value={reservation.customerName} onChange={e => setReservation(v => ({ ...v, customerName: e.target.value }))} /></label>
        <label>مدت (دقیقه)<input type="number" value={reservation.minutes} onChange={e => setReservation(v => ({ ...v, minutes: e.target.value }))} /></label>
        <label>یادداشت<input value={reservation.note} onChange={e => setReservation(v => ({ ...v, note: e.target.value }))} /></label>
        <button className="btn primary" onClick={() => void createReservation()}>ثبت رزرو</button>
      </section>
      <section className="card-panel" style={{ padding: 14 }}><h3>صف انتظار و انتقال جلسه</h3>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}><input value={transfer.from} onChange={e => setTransfer(v => ({ ...v, from: e.target.value }))} placeholder="مبدأ" /><input value={transfer.to} onChange={e => setTransfer(v => ({ ...v, to: e.target.value }))} placeholder="مقصد" /></div>
        <button className="btn" onClick={() => void doTransfer()}>↔ انتقال جلسه</button>
        {waitlist.map(item => <div className="list-row" key={item.id}><span>{item.customerName} · {item.stationType}</span><button className="btn" disabled={item.status !== 'waiting'} onClick={() => void assignWait(item.id)}>{item.status === 'waiting' ? 'تخصیص' : 'تخصیص شد'}</button></div>)}
      </section>
      <section className="card-panel" style={{ padding: 14, gridColumn: '1 / -1' }}><h3>رزروها</h3>
        {reservations.map(item => <div className="list-row" key={item.id}><span>{item.stationName} · {item.customerName} ({item.customerCode}) · {dt(item.reservedAt)} · {item.durationMinutes} دقیقه · {item.status}</span>{item.status !== 'cancelled' && <button className="btn danger" onClick={async () => { await mockService.cancelReservation(item.id); await loadAll(); setNotice('رزرو لغو شد'); }}>لغو</button>}</div>)}
      </section>
    </div>}

    {tab === 'inventory' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <h3>کالا و ورودی انبار</h3><div className="table-wrap"><table><thead><tr><th>کالا</th><th>دسته</th><th>خرید</th><th>فروش</th><th>موجودی</th><th>ورودی</th></tr></thead><tbody>
      {products.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.category}</td><td>{money(item.buyPrice)}</td><td>{money(item.price)}</td><td>{item.stock} / {item.maxStock}</td><td><div style={{ display: 'flex', gap: 6 }}><input style={{ width: 90 }} type="number" min="0" value={stockAdd[item.id] || ''} onChange={e => setStockAdd(v => ({ ...v, [item.id]: e.target.value }))} /><button className="btn" onClick={() => void addStock(item)}>ثبت ورود</button></div></td></tr>)}
      </tbody></table></div>
    </section>}

    {tab === 'vip' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <div className="table-wrap"><table><thead><tr><th>پکیج</th><th>سطح</th><th>قیمت</th><th>سقف روزانه</th><th>کل زمان</th><th>تخفیف</th><th>فعال</th></tr></thead><tbody>
      {packages.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.tier}</td><td>{money(item.price)}</td><td>{item.dailyMinutes === 1440 ? '۲۴ ساعت' : item.dailyMinutes + ' دقیقه'}</td><td>{item.totalMinutes} دقیقه</td><td>{item.discount}%</td><td><input type="checkbox" checked={item.active} onChange={async e => { await mockService.saveVipPackage({ ...item, active: e.target.checked }); await loadAll(); }} /></td></tr>)}
      </tbody></table></div>
    </section>}

    {tab === 'expenses' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1fr auto', gap: 8, alignItems: 'end' }}><label>عنوان<input value={expense.title} onChange={e => setExpense(v => ({ ...v, title: e.target.value }))} /></label><label>مبلغ<input type="number" value={expense.amount} onChange={e => setExpense(v => ({ ...v, amount: e.target.value }))} /></label><label>دسته<select value={expense.category} onChange={e => setExpense(v => ({ ...v, category: e.target.value }))}><option>خرید/تأمین</option><option>قبض و اینترنت</option><option>حقوق</option><option>تعمیرات</option><option>سایر</option></select></label><button className="btn primary" onClick={() => void addExpense()}>ثبت هزینه</button></div>
      <div className="table-wrap" style={{ marginTop: 14 }}><table><thead><tr><th>عنوان</th><th>دسته</th><th>مبلغ</th><th>ثبت‌کننده</th><th>زمان</th></tr></thead><tbody>{expenses.map(item => <tr key={item.id}><td>{item.title}</td><td>{item.category}</td><td>{money(item.amount)}</td><td>{item.operator}</td><td>{dt(item.createdAt)}</td></tr>)}</tbody></table></div>
    </section>}

    {tab === 'audit' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <input value={auditFilter} onChange={e => setAuditFilter(e.target.value)} placeholder="جست‌وجوی اپراتور، عملیات یا هدف…" /><div style={{ marginTop: 12 }}>{filteredAudits.map(item => <div className="list-row" key={item.id}><span>{dt(item.createdAt)} · <b>{item.operator}</b> · {item.action} · {item.target}</span><small>{item.details}</small></div>)}</div>
    </section>}

    {tab === 'invoices' && <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
      <div className="table-wrap"><table><thead><tr><th>ایستگاه</th><th>مشتری</th><th>زمان</th><th>بوفه</th><th>جمع</th><th>پرداخت</th><th>اپراتور</th><th>وضعیت</th></tr></thead><tbody>{invoices.length === 0 ? <tr><td colSpan={8}>هنوز فاکتور واقعی در این نشست ثبت نشده است؛ پس از تسویه از داشبورد در اینجا نمایش داده می‌شود.</td></tr> : invoices.map(item => <tr key={item.id}><td>{item.stationName}</td><td>{item.customerCode}</td><td>{item.durationMinutes} دقیقه</td><td>{money(item.buffetAmount)}</td><td>{money(item.totalAmount)}</td><td>{item.paymentMethod}</td><td>{item.operator}</td><td>{item.status}</td></tr>)}</tbody></table></div>
    </section>}
  </>;
}
