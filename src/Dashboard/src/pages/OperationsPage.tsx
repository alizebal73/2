import { useEffect, useMemo, useState } from 'react';
import { useEffect, useMemo, useState } from 'react';
import { getServerCustomers, type CustomerRecord } from '../services/customerService';
import {
  addExpense as addServerExpense,
  createReservation as createServerReservation,
  deleteVipPackage,
  getAuditLogs,
  getManagedStations,
  getManagementExpenses,
  getManagementInvoices,
  getReservations,
  getServerProducts,
  getVipPackagesForOperations,
  getWaitlist,
  saveManagedStation,
  saveVipPackage,
  toggleStation,
  transitionReservation,
  adjustServerStock,
  createServerProduct,
  updateServerProduct,
} from '../services/operationsService';
import { getServerActiveSessions, transferServerSession } from '../services/sessionService';
import type {
  AuditLogRecord,
  ExpenseRecord,
  ManagementInvoiceRecord,
  ReservationRecord,
  StationManagementRecord,
  VipPackageRecord,
  ProductRecord,
} from '../types';

type Tab = 'stations' | 'reservations' | 'events' | 'inventory' | 'vip' | 'expenses' | 'audit' | 'invoices';

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
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [events, setEvents] = useState<Awaited<ReturnType<typeof getEvents>>>([]);
  const [eventForm, setEventForm] = useState({ name: '', kind: 'tournament', minutes: '180', maxParticipants: '16', note: '' });
  const [notice, setNotice] = useState('');
  const [stationFilter, setStationFilter] = useState('');
  const [auditFilter, setAuditFilter] = useState('');
  const [stockAdd, setStockAdd] = useState<Record<string, string>>({});

  const [reservation, setReservation] = useState({
    stationId: 'station-4',
    customerCode: '1050',
    customerName: 'رضا محمدی',
    minutes: '120',
    note: '',
  });
  const [expense, setExpense] = useState({ title: '', amount: '', category: 'خرید/تأمین' });
  const [transfer, setTransfer] = useState({ from: 'PC ۰۴', to: 'PC ۰۵' });
  const [stationForm, setStationForm] = useState<StationManagementRecord>({
    id: '', name: '', zone: 'pc', type: 'PC', ratePerHour: 95000, status: 'active', ip: '', note: '',
  });
  const [productForm, setProductForm] = useState<ProductRecord>({
    id: '', name: '', category: 'نوشیدنی', price: 0, buyPrice: 0, stock: 0, minimumStock: 0, unit: 'عدد', lowStock: false, maxStock: 20,
  });
  const [packageForm, setPackageForm] = useState<VipPackageRecord>({
    id: '', name: '', tier: 'silver', price: 0, dailyMinutes: 120, totalMinutes: 3000, discount: 10, active: true,
  });

  async function loadAll() {
    const [s, r, p, v, e, a, i, w, cst, castEvents] = await Promise.all([
      getManagedStations(),
      getReservations(),
      getServerProducts(),
      getVipPackagesForOperations(),
      getManagementExpenses(),
      getAuditLogs(),
      getManagementInvoices(),
      getWaitlist(),
      getServerCustomers(),
      getEvents(),
    ]);
    setStations(s);
    setReservations(r);
    setProducts(p);
    setPackages(v);
    setExpenses(e);
    setAudits(a);
    setInvoices(i);
    setWaitlist(w);
    setCustomers(cst);
    setEvents(castEvents);
    setReservation(current => ({
      ...current,
      stationId: current.stationId === 'station-4' ? (s.find(item => item.status !== 'off')?.id || current.stationId) : current.stationId,
    }));
  }

  useEffect(() => { void loadAll(); }, []);

  const filteredStations = useMemo(
    () => stations.filter(item => !stationFilter || item.name.includes(stationFilter) || item.type.includes(stationFilter)),
    [stations, stationFilter],
  );
  const filteredAudits = useMemo(
    () => audits.filter(item => !auditFilter || item.operator.includes(auditFilter) || item.action.includes(auditFilter) || item.target.includes(auditFilter)),
    [audits, auditFilter],
  );

  async function saveEvent() {
    if (!eventForm.name.trim()) return;
    await createEvent({
      name: eventForm.name,
      kind: eventForm.kind,
      startAt: new Date(Date.now() + 60 * 60000).toISOString(),
      durationMinutes: Number(eventForm.minutes) || 180,
      maxParticipants: Math.max(0, Number(eventForm.maxParticipants) || 0),
      notes: eventForm.note,
    });
    setEventForm({ name: '', kind: 'tournament', minutes: '180', maxParticipants: '16', note: '' });
    await loadAll();
    setNotice('Event روی سرور ثبت شد');
  }

  async function saveStation() {
    if (!stationForm.name.trim() || stationForm.ratePerHour <= 0) return;
    const first = stations.find(item => item.stationTypeId);
    const stationTypeId = stationForm.stationTypeId || first?.stationTypeId;
    if (!stationTypeId) { setNotice('نوع ایستگاه معتبر سروری پیدا نشد'); return; }

    await saveManagedStation(
      { ...stationForm, id: stationForm.id || '' },
      { stationTypeId, tariffId: stationForm.tariffId ?? first?.tariffId ?? null },
    );
    setStationForm({ id: '', name: '', zone: 'pc', type: 'PC', ratePerHour: 95000, status: 'active', ip: '', note: 'internet1', networkRoute: 'internet1' });
    await loadAll();
    setNotice('ایستگاه در سرور ذخیره شد');
  }

  async function deleteStation(id: string) {
    const current = stations.find(item => item.id === id);
    if (!current) return;
    await toggleStation(id, false, {
      stationTypeId: current.stationTypeId || stations.find(item => item.stationTypeId)?.stationTypeId || '',
      tariffId: current.tariffId ?? null,
    }, current);
    await loadAll();
    setNotice('ایستگاه به‌جای حذف دائمی، غیرفعال شد');
  }

  async function toggleStationState(item: StationManagementRecord) {
    const stationTypeId = item.stationTypeId || stations.find(row => row.stationTypeId)?.stationTypeId;
    if (!stationTypeId) { setNotice('نوع ایستگاه معتبر پیدا نشد'); return; }
    await toggleStation(item.id, item.status === 'off', {
      stationTypeId,
      tariffId: item.tariffId ?? null,
    }, item);
    await loadAll();
    setNotice(item.status === 'off' ? 'ایستگاه فعال شد' : 'ایستگاه خارج از سرویس شد');
  }

  async function createReservation() {
    const station = stations.find(item => item.id === reservation.stationId);
    const customer = customers.find(item => (item.code || '').trim() === reservation.customerCode.trim());
    if (!station || !customer) { setNotice('ایستگاه یا مشتری معتبر پیدا نشد'); return; }

    await createServerReservation({
      customerId: customer.id,
      stationId: station.id,
      startAt: new Date(Date.now() + 60 * 60000).toISOString(),
      durationMinutes: Number(reservation.minutes) || 60,
      notes: reservation.note,
    });
    await loadAll();
    setNotice('رزرو در سرور ثبت شد');
  }

  async function addExpense() {
    const amount = Number(expense.amount);
    if (!expense.title || !amount) return;
    await addServerExpense({ title: expense.title, amount, category: expense.category });
    setExpense({ title: '', amount: '', category: 'خرید/تأمین' });
    await loadAll();
    setNotice('هزینه روی شیفت سرور ثبت شد');
  }

  async function saveProduct() {
    if (!productForm.name.trim() || productForm.price <= 0) return;
    if (productForm.id) {
      await updateServerProduct(productForm.id, {
        name: productForm.name,
        category: productForm.category,
        price: productForm.price,
        buyPrice: productForm.buyPrice,
        minimumStock: productForm.minimumStock,
        unit: productForm.unit,
        active: true,
      });
    } else {
      await createServerProduct({
        name: productForm.name,
        category: productForm.category,
        price: productForm.price,
        buyPrice: productForm.buyPrice,
        initialStock: productForm.stock,
        minimumStock: productForm.minimumStock,
        unit: productForm.unit,
      });
    }
    setProductForm({ id: '', name: '', category: 'نوشیدنی', price: 0, buyPrice: 0, stock: 0, minimumStock: 0, unit: 'عدد', lowStock: false, maxStock: 20 });
    await loadAll();
    setNotice('کالا روی سرور ذخیره شد');
  }

  async function addStock(product: ProductRecord) {
    const quantity = Number(stockAdd[product.id] || 0);
    if (quantity <= 0) return;
    await adjustServerStock(product.id, quantity, 'in', 'ورود انبار از مرکز مدیریت', 'Purchase');
    setStockAdd(current => ({ ...current, [product.id]: '' }));
    await loadAll();
    setNotice('ورودی انبار ثبت شد');
  }

  async function savePackage() {
    if (!packageForm.name.trim() || packageForm.price <= 0) return;
    await saveVipPackage(packageForm);
    setPackageForm({ id: '', name: '', tier: 'silver', price: 0, dailyMinutes: 120, totalMinutes: 3000, discount: 10, active: true });
    await loadAll();
    setNotice('پکیج VIP روی سرور ذخیره شد');
  }

  async function doTransfer() {
    const active = await getServerActiveSessions();
    const source = active.find(item => item.stationName === transfer.from);
    const target = stations.find(item => item.name === transfer.to);
    if (!source || !target) { setNotice('جلسه فعال یا ایستگاه مقصد پیدا نشد'); return; }
    await transferServerSession(source.id, target.id);
    await loadAll();
    setNotice('انتقال جلسه در سرور و Audit ثبت شد');
  }

  async function assignWait(id: string) {
    const target = stations.find(item => item.name === transfer.to);
    if (!target) { setNotice('ایستگاه مقصد پیدا نشد'); return; }
    await transitionReservation(id, 'assign', target.id);
    await loadAll();
    setNotice('نفر صف در سرور به ایستگاه تخصیص داده شد');
  }

  const tabs: Array<[Tab, string]> = [
    ['stations', '🖥 ایستگاه‌ها'],
    ['reservations', '📅 رزرو و صف'],
    ['events', '🏆 Event / Tournament'],
    ['inventory', '📦 کالا و انبار'],
    ['vip', '⭐ پکیج VIP'],
    ['expenses', '💳 هزینه‌ها'],
    ['audit', '🧾 Audit Log'],
    ['invoices', '🧮 فاکتورها و جلسات'],
  ];

  return (
    <>
      <div className="page-header">
        <div><p>Operations / Stage 14</p><h1>مرکز عملیات</h1></div>
        {notice && <div className="operation-toast" onClick={() => setNotice('')}>{notice}</div>}
      </div>

      <section className="card-panel" style={{ margin: '0 22px 14px', padding: 10, overflowX: 'auto' }}>
        <div style={{ display: 'flex', gap: 8, minWidth: 900 }}>
          {tabs.map(([key, label]) => (
            <button type="button" className={tab === key ? 'btn primary' : 'btn'} key={key} onClick={() => setTab(key)}>{label}</button>
          ))}
        </div>
      </section>

      {tab === 'stations' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1fr 1fr 1.4fr auto', gap: 8, alignItems: 'end', marginBottom: 14 }}>
            <label>نام<input value={stationForm.name} onChange={e => setStationForm(v => ({ ...v, name: e.target.value }))} placeholder="PC ۰۱" /></label>
            <label>نوع<select value={stationForm.type} onChange={e => setStationForm(v => ({ ...v, type: e.target.value }))}><option>PC</option><option>PS5</option><option>PS4</option><option>فوتبال‌دستی</option></select></label>
            <label>زون<select value={stationForm.zone} onChange={e => setStationForm(v => ({ ...v, zone: e.target.value as StationManagementRecord['zone'] }))}><option value="pc">PC</option><option value="console">کنسول</option><option value="table">فوتبال‌دستی</option></select></label>
            <label>نرخ/ساعت<input type="number" value={stationForm.ratePerHour} onChange={e => setStationForm(v => ({ ...v, ratePerHour: Number(e.target.value) }))} /></label>
            <label>مسیر شبکه<select value={stationForm.networkRoute || stationForm.note || 'internet1'} onChange={e => setStationForm(v => ({ ...v, note: e.target.value, networkRoute: e.target.value }))}><option value="internet1">اینترنت ۱</option><option value="internet2">اینترنت ۲</option><option value="lan">LAN</option></select></label>
            <button className="btn primary" onClick={() => void saveStation()}>{stationForm.id ? 'ویرایش' : 'افزودن'}</button>
          </div>
          <div className="section-toolbar"><input value={stationFilter} onChange={e => setStationFilter(e.target.value)} placeholder="جست‌وجوی ایستگاه…" /><span>{filteredStations.length} ایستگاه</span></div>
          <div className="table-wrap"><table><thead><tr><th>ایستگاه</th><th>نوع</th><th>زون</th><th>تعرفه/ساعت</th><th>وضعیت</th><th>شبکه</th><th>عملیات</th></tr></thead><tbody>
            {filteredStations.map(item => (
              <tr key={item.id}>
                <td>{item.name}</td><td>{item.type}</td><td>{item.zone}</td><td>{money(item.ratePerHour)}</td>
                <td>{item.status === 'off' ? 'خارج از سرویس' : item.status === 'reserved' ? 'رزرو' : 'فعال'}</td>
                <td>{item.networkRoute || item.note || 'internet1'}</td>
                <td>
                  <button className="btn" onClick={() => void toggleStationState(item)}>{item.status === 'off' ? 'فعال‌سازی' : 'خارج از سرویس'}</button>
                  <button className="btn" onClick={() => setStationForm({ ...item })}>ویرایش</button>
                  <button className="btn danger" onClick={() => void deleteStation(item.id)}>غیرفعال‌سازی</button>
                </td>
              </tr>
            ))}
          </tbody></table></div>
        </section>
      )}

      {tab === 'reservations' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, padding: '0 22px 30px' }}>
          <section className="card-panel" style={{ padding: 14 }}><h3>رزرو جدید</h3>
            <label>ایستگاه<select value={reservation.stationId} onChange={e => setReservation(v => ({ ...v, stationId: e.target.value }))}>{stations.filter(s => s.status !== 'off').map(s => <option value={s.id} key={s.id}>{s.name}</option>)}</select></label>
            <label>کد مشتری<input value={reservation.customerCode} onChange={e => {
  const code = e.target.value;
  const found = customers.find(item => (item.code || '').trim() === code.trim());
  setReservation(v => ({ ...v, customerCode: code, customerName: found?.name ?? '' }));
}} /></label>
            <label>نام مشتری<input value={reservation.customerName} readOnly /></label>
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
            {reservations.map(item => <div className="list-row" key={item.id}><span>{item.stationName} · {item.customerName} ({item.customerCode}) · {dt(item.reservedAt)} · {item.durationMinutes} دقیقه · {item.status}</span>{item.status !== 'cancelled' && <button className="btn danger" onClick={async () => { await transitionReservation(item.id, 'cancel'); await loadAll(); setNotice('رزرو لغو شد'); }}>لغو</button>}</div>)}
          </section>
        </div>
      )}

      {tab === 'events' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <h3>Event / Tournament Mode</h3>
          <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1fr 1fr auto', gap: 8, alignItems: 'end', marginBottom: 14 }}>
            <label>نام Event<input value={eventForm.name} onChange={e => setEventForm(v => ({ ...v, name: e.target.value }))} placeholder="مسابقات EA FC" /></label>
            <label>نوع<select value={eventForm.kind} onChange={e => setEventForm(v => ({ ...v, kind: e.target.value }))}><option value="tournament">Tournament</option><option value="event">Event</option><option value="league">League</option></select></label>
            <label>مدت (دقیقه)<input type="number" value={eventForm.minutes} onChange={e => setEventForm(v => ({ ...v, minutes: e.target.value }))} /></label>
            <label>ظرفیت<input type="number" min="0" value={eventForm.maxParticipants} onChange={e => setEventForm(v => ({ ...v, maxParticipants: e.target.value }))} /></label>
            <button className="btn primary" onClick={() => void saveEvent()}>ثبت Event</button>
          </div>
          <div className="table-wrap"><table><thead><tr><th>نام</th><th>نوع</th><th>شروع</th><th>ظرفیت</th><th>ثبت‌نام</th><th>وضعیت</th><th>عملیات</th></tr></thead><tbody>
            {events.map(event => <tr key={event.id}>
              <td>{event.name}</td><td>{event.kind}</td><td>{dt(event.startAt)}</td><td>{event.maxParticipants || 'آزاد'}</td><td>{event.participantCount}</td><td>{event.status}</td>
              <td>
                {event.status === 'Scheduled' && <button className="btn" onClick={async()=>{await transitionEvent(event.id,'start');await loadAll();setNotice('Event شروع شد')}}>شروع</button>}
                {event.status === 'Running' && <button className="btn" onClick={async()=>{await transitionEvent(event.id,'complete');await loadAll();setNotice('Event تکمیل شد')}}>اتمام</button>}
                {event.status !== 'Completed' && event.status !== 'Cancelled' && <button className="btn danger" onClick={async()=>{await transitionEvent(event.id,'cancel');await loadAll();setNotice('Event لغو شد')}}>لغو</button>}
              </td>
            </tr>)}
            {events.length === 0 && <tr><td colSpan={7}>Event فعالی ثبت نشده است.</td></tr>}
          </tbody></table></div>
        </section>
      )}

      {tab === 'inventory' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr 1fr 1fr 1fr auto', gap: 8, alignItems: 'end', marginBottom: 14 }}>
            <label>کالا<input value={productForm.name} onChange={e => setProductForm(v => ({ ...v, name: e.target.value }))} /></label>
            <label>دسته<input value={productForm.category} onChange={e => setProductForm(v => ({ ...v, category: e.target.value }))} /></label>
            <label>خرید<input type="number" value={productForm.buyPrice} onChange={e => setProductForm(v => ({ ...v, buyPrice: Number(e.target.value) }))} /></label>
            <label>فروش<input type="number" value={productForm.price} onChange={e => setProductForm(v => ({ ...v, price: Number(e.target.value) }))} /></label>
            <label>موجودی<input type="number" value={productForm.stock} onChange={e => setProductForm(v => ({ ...v, stock: Number(e.target.value) }))} /></label>
            <label>سقف<input type="number" value={productForm.maxStock} onChange={e => setProductForm(v => ({ ...v, maxStock: Number(e.target.value) }))} /></label>
            <button className="btn primary" onClick={() => void saveProduct()}>{productForm.id ? 'ویرایش' : 'افزودن'}</button>
          </div>
          <h3>کالا و ورودی انبار</h3>
          <div className="table-wrap"><table><thead><tr><th>کالا</th><th>دسته</th><th>خرید</th><th>فروش</th><th>موجودی</th><th>ویرایش</th><th>ورودی</th></tr></thead><tbody>
            {products.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.category}</td><td>{money(item.buyPrice)}</td><td>{money(item.price)}</td><td>{item.stock} / {item.maxStock}</td><td><button className="btn" onClick={() => setProductForm({ ...item })}>ویرایش</button></td><td><div style={{ display: 'flex', gap: 6 }}><input style={{ width: 90 }} type="number" min="0" value={stockAdd[item.id] || ''} onChange={e => setStockAdd(v => ({ ...v, [item.id]: e.target.value }))} /><button className="btn" onClick={() => void addStock(item)}>ثبت ورود</button></div></td></tr>)}
          </tbody></table></div>
        </section>
      )}

      {tab === 'vip' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr 1fr 1fr 1fr auto', gap: 8, alignItems: 'end', marginBottom: 14 }}>
            <label>نام پکیج<input value={packageForm.name} onChange={e => setPackageForm(v => ({ ...v, name: e.target.value }))} /></label>
            <label>سطح<select value={packageForm.tier} onChange={e => setPackageForm(v => ({ ...v, tier: e.target.value as VipPackageRecord['tier'] }))}><option value="bronze">برنز</option><option value="silver">نقره‌ای</option><option value="gold">طلایی</option><option value="custom">سفارشی</option></select></label>
            <label>قیمت<input type="number" value={packageForm.price} onChange={e => setPackageForm(v => ({ ...v, price: Number(e.target.value) }))} /></label>
            <label>سقف روزانه<input type="number" value={packageForm.dailyMinutes} onChange={e => setPackageForm(v => ({ ...v, dailyMinutes: Number(e.target.value) }))} /></label>
            <label>کل دقیقه<input type="number" value={packageForm.totalMinutes} onChange={e => setPackageForm(v => ({ ...v, totalMinutes: Number(e.target.value) }))} /></label>
            <label>تخفیف %<input type="number" value={packageForm.discount} onChange={e => setPackageForm(v => ({ ...v, discount: Number(e.target.value) }))} /></label>
            <button className="btn primary" onClick={() => void savePackage()}>{packageForm.id ? 'ویرایش' : 'افزودن'}</button>
          </div>
          <div className="table-wrap"><table><thead><tr><th>پکیج</th><th>سطح</th><th>قیمت</th><th>سقف روزانه</th><th>کل زمان</th><th>تخفیف</th><th>عملیات</th></tr></thead><tbody>
            {packages.map(item => <tr key={item.id}><td>{item.name}</td><td>{item.tier}</td><td>{money(item.price)}</td><td>{item.dailyMinutes === 1440 ? '۲۴ ساعت' : item.dailyMinutes + ' دقیقه'}</td><td>{item.totalMinutes} دقیقه</td><td>{item.discount}%</td><td><input type="checkbox" checked={item.active} onChange={async e => { await saveVipPackage({ ...item, active: e.target.checked }); await loadAll(); }} /> <button className="btn" onClick={() => setPackageForm({ ...item })}>ویرایش</button> <button className="btn danger" onClick={async () => { await deleteVipPackage(item.id); await loadAll(); setNotice('پکیج غیرفعال شد'); }}>حذف</button></td></tr>)}
          </tbody></table></div>
        </section>
      )}

      {tab === 'expenses' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr 1fr auto', gap: 8, alignItems: 'end' }}>
            <label>عنوان<input value={expense.title} onChange={e => setExpense(v => ({ ...v, title: e.target.value }))} /></label>
            <label>مبلغ<input type="number" value={expense.amount} onChange={e => setExpense(v => ({ ...v, amount: e.target.value }))} /></label>
            <label>دسته<select value={expense.category} onChange={e => setExpense(v => ({ ...v, category: e.target.value }))}><option>خرید/تأمین</option><option>قبض و اینترنت</option><option>حقوق</option><option>تعمیرات</option><option>سایر</option></select></label>
            <button className="btn primary" onClick={() => void addExpense()}>ثبت هزینه</button>
          </div>
          <div className="table-wrap" style={{ marginTop: 14 }}><table><thead><tr><th>عنوان</th><th>دسته</th><th>مبلغ</th><th>ثبت‌کننده</th><th>زمان</th></tr></thead><tbody>{expenses.map(item => <tr key={item.id}><td>{item.title}</td><td>{item.category}</td><td>{money(item.amount)}</td><td>{item.operator}</td><td>{dt(item.createdAt)}</td></tr>)}</tbody></table></div>
        </section>
      )}

      {tab === 'audit' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <input value={auditFilter} onChange={e => setAuditFilter(e.target.value)} placeholder="جست‌وجوی اپراتور، عملیات یا هدف…" />
          <div style={{ marginTop: 12 }}>{filteredAudits.map(item => <div className="list-row" key={item.id}><span>{dt(item.createdAt)} · <b>{item.operator}</b> · {item.action} · {item.target}</span><small>{item.details}</small></div>)}</div>
        </section>
      )}

      {tab === 'invoices' && (
        <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
          <div className="table-wrap"><table><thead><tr><th>ایستگاه</th><th>مشتری</th><th>زمان</th><th>بوفه</th><th>جمع</th><th>پرداخت</th><th>اپراتور</th><th>وضعیت</th></tr></thead><tbody>
            {invoices.length === 0
              ? <tr><td colSpan={8}>هنوز فاکتور واقعی در این نشست ثبت نشده است؛ پس از تسویه از داشبورد در اینجا نمایش داده می‌شود.</td></tr>
              : invoices.map(item => <tr key={item.id}><td>{item.stationName}</td><td>{item.customerCode}</td><td>{item.durationMinutes} دقیقه</td><td>{money(item.buffetAmount)}</td><td>{money(item.totalAmount)}</td><td>{item.paymentMethod}</td><td>{item.operator}</td><td>{item.status}</td></tr>)}
          </tbody></table></div>
        </section>
      )}
    </>
  );
}
