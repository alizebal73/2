import { useCallback, useEffect, useMemo, useState } from 'react';
import type { CSSProperties, MouseEvent } from 'react';
import type { CustomerRecord, DashboardSnapshotDto, ServerInfoDto, StationDto, StationState, ZoneKey } from '../types';
import { mockService } from '../services/mockService';

const zoneLabels: Record<ZoneKey, string> = { all: 'همه', pc: 'رایانه‌ها (۴۰)', console: 'کنسول‌ها (۱۶)', table: 'میزها (۵)' };
const stateLabels: Record<StationState, string> = { free: 'آزاد', busy: 'در حال بازی', reserved: 'رزرو', off: 'خارج از سرویس' };
const emptyStations: StationDto[] = [];
type ViewMode = 'v-card' | 'v-compact' | 'v-list';
type ModalKind = 'start' | 'flow' | 'charge' | 'settle' | null;
type Invoice = { station: string; total: number; payment: string; closedAt: string };

function money(value: number) { return new Intl.NumberFormat('fa-IR').format(Math.round(value)); }
function number(value: string) { return Number(value.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).replace(/[٬,\s]/g, '')) || 0; }

type Props = {
  snapshot: DashboardSnapshotDto | null;
  apiState: 'loading' | 'online' | 'offline';
  serverInfo: ServerInfoDto | null;
  error: string;
  onNavigate: (page: 'client-shell') => void;
};

type ContextMenu = { x: number; y: number; station: StationDto } | null;

export function DashboardPage({ snapshot, apiState, serverInfo, onNavigate }: Props) {
  const [stationOverrides, setStationOverrides] = useState<StationDto[] | null>(null);
  const [zone, setZone] = useState<ZoneKey>('all');
  const [query, setQuery] = useState('');
  const [view, setView] = useState<ViewMode>('v-card');
  const [zoom, setZoom] = useState(100);
  const [now, setNow] = useState(Date.now());
  const [modal, setModal] = useState<ModalKind>(null);
  const [activeStation, setActiveStation] = useState<StationDto | null>(null);
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [customerCode, setCustomerCode] = useState('');
  const [persons, setPersons] = useState(2);
  const [paymentMode, setPaymentMode] = useState<'settle-later' | 'prepaid'>('settle-later');
  const [amount, setAmount] = useState('');
  const [context, setContext] = useState<ContextMenu>(null);
  const [message, setMessage] = useState('');
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [flowStep, setFlowStep] = useState<1 | 2>(1);

  const stations = stationOverrides ?? snapshot?.stations ?? emptyStations;
  const updateStation = useCallback((id: string, update: Partial<StationDto>) => {
    setStationOverrides(items => (items ?? snapshot?.stations ?? emptyStations).map(item => item.id === id ? { ...item, ...update } : item));
  }, [snapshot?.stations]);
  const duration = useCallback((station: StationDto) => {
    const elapsed = station.startedAt ? Math.max(0, now - new Date(station.startedAt).getTime()) / 60000 : station.sessionMinutes ?? 0;
    return elapsed;
  }, [now]);
  const applyFlow = useCallback((action: string) => {
    const value = number(amount);
    if (!value) { setMessage('مبلغ معتبر وارد کنید'); return; }
    const customer = customers.find(item => item.username === customerCode || item.mobile === customerCode || item.id === customerCode || item.name.includes(customerCode));
    if (action === 'F5') setMessage(`شارژ مستقیم ${money(value)} تومان ثبت شد`);
    if (action === 'F6') setMessage(`بدهی ${money(value)} تومان ثبت شد`);
    if (action === 'F7') {
      if (customer && customer.wallet < value) { setMessage('موجودی کیف پول کافی نیست'); return; }
      setMessage(`${money(value)} تومان از کیف پول کسر شد`);
    }
    if (action === 'F8') setMessage(`برداشت کیف پول و ثبت مازاد ${money(value)} تومان انجام شد`);
    setModal(null);
  }, [amount, customers, customerCode]);
  useEffect(() => { void mockService.getCustomers().then(setCustomers); }, []);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  useEffect(() => {
    if (!message) return;
    const timer = window.setTimeout(() => setMessage(''), 3200);
    return () => window.clearTimeout(timer);
  }, [message]);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') { setModal(null); setContext(null); }
      if (event.key === 'F1') { event.preventDefault(); setFlowStep(1); setModal('flow'); }
      if (modal === 'flow' && flowStep === 2 && ['F5', 'F6', 'F7', 'F8'].includes(event.key)) {
        event.preventDefault(); applyFlow(event.key);
      }
      if (modal === 'flow' && event.key === 'F4') document.getElementById('flow-amount')?.focus();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [modal, flowStep, applyFlow]);
  useEffect(() => {
    const onCommand = (event: Event) => {
      const command = (event as CustomEvent<string>).detail;
      if (command === 'start-session') open('start', stations.find(item => item.state === 'free') ?? null);
      if (command === 'quick-charge') open('charge', stations.find(item => item.state === 'busy') ?? null);
      if (command === 'flow') { setFlowStep(1); open('flow', stations.find(item => item.state === 'busy') ?? null); }
      if (command === 'extend-session') {
        const station = stations.find(item => item.state === 'busy');
        if (station) {
          const minutes = duration(station) + 30;
          updateStation(station.id, { sessionMinutes: minutes, startedAt: new Date(Date.now() - minutes * 60000).toISOString() });
          setMessage('۳۰ دقیقه به جلسه فعال اضافه شد');
        } else setMessage('جلسه فعالی وجود ندارد');
      }
    };
    window.addEventListener('gamenet-command', onCommand);
    return () => window.removeEventListener('gamenet-command', onCommand);
  }, [stations, duration, updateStation]);
  useEffect(() => {
    const onBuffetSale = (event: Event) => {
      const detail = (event as CustomEvent<{ total: number }>).detail;
      const station = stations.find(item => item.state === 'busy');
      if (station) {
        updateStation(station.id, { buffetTotal: (station.buffetTotal ?? 0) + detail.total });
        setMessage(`فروش ${money(detail.total)} تومان به فاکتور ${station.name} اضافه شد`);
      } else setMessage('جلسه فعالی نیست؛ فروش مستقل ثبت کنید');
    };
    window.addEventListener('gamenet-buffet-sale', onBuffetSale);
    return () => window.removeEventListener('gamenet-buffet-sale', onBuffetSale);
  }, [stations, updateStation]);

  const visibleStations = useMemo(() => stations.filter(station =>
    (zone === 'all' || station.zone === zone) && station.name.toLowerCase().includes(query.trim().toLowerCase())), [stations, zone, query]);
  const counts = useMemo(() => ({
    free: stations.filter(item => item.state === 'free').length,
    busy: stations.filter(item => item.state === 'busy').length,
    reserved: stations.filter(item => item.state === 'reserved').length,
    off: stations.filter(item => item.state === 'off').length,
  }), [stations]);

  function open(kind: ModalKind, station: StationDto | null = null) {
    setActiveStation(station);
    setAmount('');
    setCustomerCode(station?.customerCode ?? '');
    setContext(null);
    setModal(kind);
  }
  function startSession() {
    if (!activeStation || activeStation.state !== 'free') { setMessage('این ایستگاه دیگر آزاد نیست'); return; }
    updateStation(activeStation.id, { state: 'busy', startedAt: new Date().toISOString(), sessionMinutes: 0, sessionRate: activeStation.ratePerHour, amountSoFar: 0, persons, customerCode, buffetTotal: 0 });
    setModal(null);
    setMessage(`جلسه ${activeStation.name} شروع شد`);
  }
  function finishSession(method: string) {
    if (!activeStation) return;
    const elapsed = duration(activeStation);
    const timeCost = (activeStation.sessionRate ?? activeStation.ratePerHour) * elapsed / 60;
    const total = Math.max(0, Math.round(timeCost + (activeStation.buffetTotal ?? 0)));
    setInvoices(items => [{ station: activeStation.name, total, payment: method, closedAt: new Date().toISOString() }, ...items]);
    updateStation(activeStation.id, { state: 'free', startedAt: undefined, sessionMinutes: undefined, sessionRate: undefined, amountSoFar: undefined, customerCode: undefined, persons: undefined, buffetTotal: undefined });
    setModal(null);
    setMessage(`تسویه ${money(total)} تومان ثبت شد؛ فاکتور در تاریخچه باقی ماند`);
  }
  function applyCharge(method: string) {
    const value = number(amount);
    if (!value || !activeStation) { setMessage('مبلغ معتبر وارد کنید'); return; }
    if (method === 'debt') { setMessage(`بدهی ${money(value)} تومان ثبت شد`); }
    else if (activeStation.state === 'busy') {
      const extraMinutes = value / ((activeStation.sessionRate ?? activeStation.ratePerHour) / 60);
      updateStation(activeStation.id, { sessionMinutes: duration(activeStation) + extraMinutes, startedAt: new Date(Date.now() - (duration(activeStation) + extraMinutes) * 60000).toISOString() });
      setMessage(`${money(value)} تومان به زمان جلسه اضافه شد`);
    } else setMessage(`شارژ ${money(value)} تومان ثبت شد`);
    setModal(null);
  }
  function showContext(event: MouseEvent<HTMLElement>, station: StationDto) {
    event.preventDefault();
    const width = 280;
    const height = 430;
    setContext({ x: Math.max(8, Math.min(event.clientX, window.innerWidth - width - 8)), y: Math.max(8, Math.min(event.clientY, window.innerHeight - height - 8)), station });
  }
  function contextAction(action: string) {
    const station = context?.station;
    setContext(null);
    if (!station) return;
    if (action === 'settle') { open('settle', station); return; }
    if (action === 'offline') {
      updateStation(station.id, { state: station.state === 'off' ? 'free' : 'off', outOfServiceReason: station.state === 'off' ? undefined : 'تعمیر و نگهداری' });
      setMessage(station.state === 'off' ? 'ایستگاه فعال شد' : 'ایستگاه خارج از سرویس شد'); return;
    }
    if (action === 'settings') { onNavigate('client-shell'); return; }
    if (action === 'switch-net') { updateStation(station.id, { network: station.network === 1 ? 2 : 1 }); setMessage(`شبکه به اینترنت ${station.network === 1 ? '۲' : '۱'} تغییر کرد`); return; }
    setMessage(`${action} برای ${station.name} در صف دمو ثبت شد`);
  }

  function renderStation(station: StationDto) {
    const minutes = duration(station);
    const elapsedCost = station.state === 'busy' ? (station.sessionRate ?? station.ratePerHour) * minutes / 60 : 0;
    const style = { '--zoom': zoom / 100 } as CSSProperties;
    return <article key={station.id} style={style} className={`station-card ${station.state} ${view}`} onClick={() => {
      if (station.state === 'free') open('start', station);
      else if (station.state === 'busy') { setCustomerCode(station.customerCode ?? ''); setActiveStation(station); setFlowStep(1); setModal('flow'); }
      else setMessage(station.state === 'reserved' ? 'رزرو ساعت ۱۸:۰۰ — هنوز مشتری وارد نشده' : station.outOfServiceReason ?? 'این دستگاه خارج از سرویس است');
    }} onDoubleClick={() => station.state === 'busy' && open('charge', station)} onContextMenu={event => showContext(event, station)}>
      <div className="top"><div className="name">{station.name}</div><span className={`status-badge ${station.state}`}>{stateLabels[station.state as StationState] ?? station.state}</span></div>
      <span className="type">{station.type} · شبکه {station.network ?? 1}</span>
      <div className="time">{station.state === 'busy' ? `${money(Math.floor(minutes / 60)).padStart(2, '۰')}:${money(Math.floor(minutes % 60)).padStart(2, '۰')}` : station.state === 'reserved' ? 'رزرو ۱۸:۰۰' : station.state === 'off' ? '⛔' : '--:--'}</div>
      <div className="price">{station.state === 'busy' ? `هزینه ${money(elapsedCost)} تومان` : `از ${money(station.ratePerHour)} تومان / ساعت`}</div>
      {station.state === 'busy' && <><div className="person-dots">{'● '.repeat(station.persons ?? 1)}</div><div className="progress-bar"><span style={{ width: `${Math.min(100, minutes % 60 / 60 * 100)}%` }} /></div><span className="pulse" /></>}
      {station.state === 'off' && <small>{station.outOfServiceReason ?? 'در تعمیر'}</small>}
    </article>;
  }

  const groups: Array<[ZoneKey, string]> = [['pc', 'رایانه‌ها — ۴۰ دستگاه'], ['console', 'کنسول‌ها — PS5 / PS4'], ['table', 'میزها — فوتبال‌دستی']];
  return <>
    <div className="page-header"><div><p>وضعیت زنده · {serverInfo?.name ?? 'GameNet Manager'}</p><h1>داشبورد</h1></div><div className="page-meta"><span>{apiState === 'online' ? 'API متصل' : apiState === 'loading' ? 'در حال اتصال' : 'API قطع'}</span><span>{snapshot ? `آخرین دریافت ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار داده'}</span></div></div>
    <div className="summary-grid">
      {[["ایستگاه آزاد", counts.free, 'green'], ['در حال بازی', counts.busy, 'red'], ['رزرو امروز', counts.reserved, 'blue'], ['درآمد امروز', invoices.reduce((sum, item) => sum + item.total, 4820000), 'orange'], ['فروش بوفه', 860000, 'orange'], ['مشتری حاضر', stations.filter(item => item.state === 'busy').reduce((sum, item) => sum + (item.persons ?? 1), 0), 'blue']].map(([label, value, color]) => <div key={label} className="summary-card"><div className="label">{label}</div><div className={`value ${color}`}>{money(Number(value))}{String(label).includes('درآمد') || String(label).includes('فروش') ? ' تومان' : ''}</div></div>)}
    </div>
    <div className="toolbar dashboard-toolbar">
      <div className="zone-filter">{Object.entries(zoneLabels).map(([key, label]) => <button key={key} type="button" className={zone === key ? 'active' : ''} onClick={() => setZone(key as ZoneKey)}>{label}</button>)}</div>
      <div className="search-box"><input aria-label="جست‌وجوی ایستگاه" value={query} onChange={event => setQuery(event.target.value)} placeholder="جست‌وجوی ایستگاه…" /></div>
      <div className="view-switch" aria-label="حالت نمایش">{(['v-card', 'v-compact', 'v-list'] as ViewMode[]).map((item, index) => <button key={item} type="button" className={view === item ? 'active' : ''} title={['کارتی', 'فشرده', 'لیستی'][index]} onClick={() => setView(item)}>{['▦', '▤', '☰'][index]}</button>)}</div>
      <label className="zoom-control">اندازه <input type="range" min="70" max="130" step="5" value={zoom} onChange={event => setZoom(Number(event.target.value))} />{money(zoom)}٪</label>
      <button type="button" className="btn" onClick={() => setMessage('۲ اعلان جدید')}>🔔 ۲</button>
      <button type="button" className="btn primary" onClick={() => open('start', stations.find(item => item.state === 'free') ?? null)}>+ شروع جلسه</button>
    </div>
    {apiState === 'loading' && <p className="empty-state">در حال دریافت DTO از سرور…</p>}
    {apiState === 'online' && !visibleStations.length && <p className="empty-state">ایستگاهی با این جست‌وجو پیدا نشد.</p>}
    {zone === 'all' ? groups.map(([key, title]) => {
      const items = visibleStations.filter(item => item.zone === key);
      if (!items.length) return null;
      return <section key={key}><div className="section-title">{title} · {items.length}</div><div className={`station-grid ${view}`} style={{ '--card-min': `${(view === 'v-compact' ? 128 : 168) * zoom / 100}px` } as CSSProperties}>{items.map(renderStation)}</div></section>;
    }) : <div className={`station-grid ${view}`} style={{ '--card-min': `${(view === 'v-compact' ? 128 : 168) * zoom / 100}px` } as CSSProperties}>{visibleStations.map(renderStation)}</div>}

    {context && <div className="context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}><strong>{context.station.name} · {stateLabels[context.station.state as StationState]}</strong><button onClick={() => contextAction('settle')}>🧾 تسویه و بستن جلسه</button><button onClick={() => contextAction('switch-net')}>🌐 تغییر اینترنت ۱ ↔ ۲</button><button onClick={() => contextAction('move-user')}>🔀 جابه‌جایی یوزر</button><button onClick={() => contextAction('logout-lock')}>🚪 خروج یوزر و قفل</button><button onClick={() => contextAction('login-id')}>🔑 ورود با شناسه</button><button onClick={() => contextAction('message')}>💬 پیام به مشتری</button><button onClick={() => contextAction('screenshot')}>📸 اسکرین‌شات</button><button onClick={() => contextAction('restart-shell')}>🔄 ری‌استارت Shell</button><button onClick={() => contextAction('restart')}>⏻ ری‌استارت Windows</button><button onClick={() => contextAction('shutdown')}>⛔ خاموش کردن</button><button onClick={() => contextAction('offline')}>🛠 خارج از سرویس / فعال‌سازی</button><button onClick={() => contextAction('settings')}>⚙ تنظیمات کامل کلاینت</button></div>}
    {modal && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setModal(null)}><section className="operation-modal" role="dialog" aria-modal="true">
      <button className="modal-close" onClick={() => setModal(null)} aria-label="بستن">×</button>
      {modal === 'start' && <><h2>شروع جلسه · {activeStation?.name ?? 'انتخاب ایستگاه آزاد'}</h2><label>ایستگاه<select value={activeStation?.id ?? ''} onChange={event => setActiveStation(stations.find(item => item.id === event.target.value) ?? null)}>{stations.filter(item => item.state === 'free').map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>مشتری<select value={customerCode} onChange={event => setCustomerCode(event.target.value)}><option value="">مهمان</option>{customers.map(item => <option key={item.id} value={item.username}>{item.code ?? item.username} · {item.name}</option>)}</select></label><label>تعرفه<select><option value="station">تعرفه ایستگاه · {money(activeStation?.ratePerHour ?? 0)} تومان</option><option value="night">تعرفه شبانه</option></select></label><div className="person-choice">{[1, 2, 3, 4].map(item => <button key={item} className={persons === item ? 'active' : ''} onClick={() => setPersons(item)}>{money(item)} نفر</button>)}</div><div className="person-choice"><button className={paymentMode === 'settle-later' ? 'active' : ''} onClick={() => setPaymentMode('settle-later')}>تسویه بعد از بازی</button><button className={paymentMode === 'prepaid' ? 'active' : ''} onClick={() => setPaymentMode('prepaid')}>پیش‌پرداخت / شارژی</button></div><div className="modal-actions"><button className="btn primary" onClick={startSession}>▶ شروع بازی</button><button className="btn" onClick={() => setModal(null)}>لغو</button></div></>}
      {modal === 'flow' && <><h2>⚡ فلوی سرعت · F1</h2>{flowStep === 1 ? <><label>شناسه مشتری<input autoFocus value={customerCode} onChange={event => setCustomerCode(event.target.value)} onKeyDown={event => event.key === 'Enter' && setFlowStep(2)} placeholder="کد، نام، لقب یا موبایل" /></label><button className="btn primary" onClick={() => setFlowStep(2)}>نمایش پروفایل</button></> : <><p>{customers.find(item => [item.username, item.mobile, item.id, item.name].some(value => value.includes(customerCode)))?.name ?? 'مشتری مهمان'} · {activeStation?.name ?? 'بدون دستگاه'}</p><label>مبلغ (تومان)<input id="flow-amount" inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label><div className="amount-presets">{[50000, 100000, 200000, 500000].map(value => <button key={value} onClick={() => setAmount(String(value))}>{money(value)}</button>)}</div><div className="modal-actions">{[['F5', 'شارژ مستقیم'], ['F6', 'ثبت بدهی'], ['F7', 'کسر از کیف پول'], ['F8', 'کسر کیف پول + بدهی']].map(([key, label]) => <button key={key} className="btn" onClick={() => applyFlow(key)}>{key} {label}</button>)}</div></>}</>}
      {modal === 'charge' && <><h2>⚡ شارژ سریع · {activeStation?.name}</h2><label>مبلغ شارژ<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} onKeyDown={event => event.key === 'Enter' && applyCharge('cash')} /></label><div className="amount-presets">{[50000, 100000, 200000, 500000].map(value => <button key={value} onClick={() => setAmount(String(value))}>{money(value)}</button>)}</div><label>هدف<select><option>شارژ زمان همین جلسه</option><option>شارژ کیف پول</option><option>شارژ + تخفیف</option></select></label><div className="modal-actions">{[['cash', 'نقد'], ['card', 'کارت'], ['wallet', 'کیف پول'], ['debt', 'ثبت در بدهی']].map(([key, label]) => <button key={key} className="btn" onClick={() => applyCharge(key)}>{label}</button>)}</div></>}
      {modal === 'settle' && activeStation && <><h2>تسویه جلسه · {activeStation.name}</h2><div className="info-row"><span>مدت جلسه</span><strong>{money(Math.floor(duration(activeStation)))} دقیقه</strong></div><div className="info-row"><span>مبلغ زمان</span><strong>{money((activeStation.sessionRate ?? activeStation.ratePerHour) * duration(activeStation) / 60)} تومان</strong></div><div className="info-row"><span>مبلغ بوفه</span><strong>{money(activeStation.buffetTotal ?? 0)} تومان</strong></div><div className="info-row"><span>مبلغ نهایی</span><strong>{money((activeStation.sessionRate ?? activeStation.ratePerHour) * duration(activeStation) / 60 + (activeStation.buffetTotal ?? 0))} تومان</strong></div><div className="modal-actions"><button className="btn" onClick={() => finishSession('cash')}>پرداخت نقدی</button><button className="btn" onClick={() => finishSession('card')}>کارتخوان</button><button className="btn" onClick={() => finishSession('wallet')}>کیف پول</button><button className="btn" onClick={() => window.print()}>چاپ فاکتور</button><button className="btn" onClick={() => { updateStation(activeStation.id, { sessionMinutes: duration(activeStation) + 30, startedAt: new Date(Date.now() - (duration(activeStation) + 30) * 60000).toISOString() }); setModal(null); setMessage('۳۰ دقیقه تمدید شد'); }}>تمدید وقت</button></div></>}
    </section></div>}
    {message && <div className="operation-toast" role="status">{message}</div>}
    <div className="status-footer">{snapshot?.generatedAt ? `آخرین به‌روزرسانی ${new Date(snapshot.generatedAt).toLocaleTimeString('fa-IR')}` : 'در انتظار دریافت داده'} · {serverInfo?.environment ?? 'Development'} · {invoices.length} فاکتور ثبت‌شده</div>
  </>;
}
