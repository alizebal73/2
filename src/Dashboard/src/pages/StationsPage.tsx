import { useEffect, useState } from 'react';
import { getTariffs, type ServerTariff } from '../services/tariffService';
import { createStation, getStations, provisionStations, updateStation, type StationRecord } from '../services/stationService';

type FormState = { name: string; zone: string; type: string; ratePerHour: number; network: number; tariffId: string };
const emptyForm: FormState = { name: '', zone: 'pc', type: 'PC', ratePerHour: 95000, network: 1, tariffId: '' };

export function StationsPage() {
  const [stations, setStations] = useState<StationRecord[]>([]);
  const [tariffs, setTariffs] = useState<ServerTariff[]>([]);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [prefix, setPrefix] = useState('PC');
  const [startNumber, setStartNumber] = useState(1);
  const [count, setCount] = useState(2);
  const [bulkRate, setBulkRate] = useState(95000);
  const [bulkNetwork, setBulkNetwork] = useState(1);
  const [notice, setNotice] = useState('');
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    try {
      const [s, t] = await Promise.all([getStations(), getTariffs()]);
      setStations(s); setTariffs(t.filter(item => item.isActive));
    } catch (error) { setNotice(error instanceof Error ? error.message : 'دریافت ایستگاه‌ها انجام نشد.'); }
    finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, []);

  function clearForm() { setEditingId(null); setForm(emptyForm); }
  function edit(station: StationRecord) {
    setEditingId(station.id);
    setForm({ name: station.name, zone: station.zone, type: station.type, ratePerHour: station.ratePerHour, network: station.network, tariffId: station.tariffId || '' });
  }
  async function save() {
    try {
      if (editingId) {
        await updateStation(editingId, { ...form, tariffId: form.tariffId || null, isActive: true });
        setNotice('ایستگاه ویرایش شد.');
      } else {
        await createStation({ ...form, tariffId: form.tariffId || null });
        setNotice('ایستگاه ایجاد شد.');
      }
      clearForm(); await load();
    } catch (error) { setNotice(error instanceof Error ? error.message : 'ذخیره ایستگاه انجام نشد.'); }
  }
  async function archive(station: StationRecord) {
    if (!window.confirm('این ایستگاه غیرفعال شود؟')) return;
    try {
      await updateStation(station.id, { name: station.name, zone: station.zone, type: station.type, ratePerHour: station.ratePerHour, network: station.network, tariffId: station.tariffId || null, isActive: false });
      setNotice('ایستگاه غیرفعال شد.'); await load();
    } catch (error) { setNotice(error instanceof Error ? error.message : 'غیرفعال‌سازی انجام نشد.'); }
  }
  async function provision() {
    try {
      const result = await provisionStations({ prefix, startNumber, count, zone: 'pc', type: 'PC', ratePerHour: bulkRate, network: bulkNetwork, tariffId: null });
      setNotice(String(result.created) + ' ایستگاه ساخته شد: ' + result.names.join('، ')); await load();
    } catch (error) { setNotice(error instanceof Error ? error.message : 'Provision ایستگاه‌ها انجام نشد.'); }
  }

  const activeCount = stations.filter(item => item.isActive).length;
  const agentCount = stations.filter(item => item.agentDeviceId).length;

  return <div className="page-content">
    <div className="page-header"><div><p>Server-backed</p><h1>ایستگاه‌ها</h1></div><button type="button" className="btn" onClick={() => void load()}>↻ به‌روزرسانی</button></div>
    <div className="stats-grid" style={{ padding: '0 22px 14px' }}>
      <div className="stat-card"><span>کل</span><strong>{stations.length.toLocaleString('fa-IR')}</strong></div>
      <div className="stat-card"><span>فعال</span><strong>{activeCount.toLocaleString('fa-IR')}</strong></div>
      <div className="stat-card"><span>Agent متصل</span><strong>{agentCount.toLocaleString('fa-IR')}</strong></div>
    </div>
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(340px,1fr))', gap: 14, padding: '0 22px 14px' }}>
      <section className="card-panel" style={{ padding: 14 }}>
        <h3>{editingId ? 'ویرایش ایستگاه' : 'ایجاد ایستگاه'}</h3>
        <div className="form-grid">
          <label>نام<input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} placeholder="PC-01" /></label>
          <label>زون<select value={form.zone} onChange={e => setForm({ ...form, zone: e.target.value })}><option value="pc">PC</option><option value="console">Console</option><option value="table">Table</option></select></label>
          <label>نوع<input value={form.type} onChange={e => setForm({ ...form, type: e.target.value })} /></label>
          <label>نرخ ساعتی<input type="number" min={1} value={form.ratePerHour} onChange={e => setForm({ ...form, ratePerHour: Number(e.target.value) })} /></label>
          <label>اینترنت<select value={form.network} onChange={e => setForm({ ...form, network: Number(e.target.value) })}><option value={1}>اینترنت ۱</option><option value={2}>اینترنت ۲</option></select></label>
          <label>تعرفه<select value={form.tariffId} onChange={e => setForm({ ...form, tariffId: e.target.value })}><option value="">بدون تعرفه</option>{tariffs.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        </div>
        <div className="modal-actions"><button type="button" className="btn primary" onClick={() => void save()}>{editingId ? 'ذخیره' : 'ایجاد'}</button>{editingId && <button type="button" className="btn" onClick={clearForm}>لغو</button>}</div>
      </section>
      <section className="card-panel" style={{ padding: 14 }}>
        <h3>راه‌اندازی گروهی PC</h3><p className="muted">مثلاً PC-01 تا PC-40؛ نام تکراری ایجاد نمی‌شود.</p>
        <div className="form-grid">
          <label>پیشوند<input value={prefix} onChange={e => setPrefix(e.target.value)} /></label>
          <label>شماره شروع<input type="number" min={0} max={9999} value={startNumber} onChange={e => setStartNumber(Number(e.target.value))} /></label>
          <label>تعداد<input type="number" min={1} max={200} value={count} onChange={e => setCount(Number(e.target.value))} /></label>
          <label>نرخ ساعتی<input type="number" min={1} value={bulkRate} onChange={e => setBulkRate(Number(e.target.value))} /></label>
          <label>اینترنت<select value={bulkNetwork} onChange={e => setBulkNetwork(Number(e.target.value))}><option value={1}>اینترنت ۱</option><option value={2}>اینترنت ۲</option></select></label>
        </div>
        <div className="modal-actions"><button type="button" className="btn primary" onClick={() => void provision()}>ساخت گروه</button></div>
      </section>
    </div>
    <section className="card-panel" style={{ margin: '0 22px 30px', padding: 14 }}>
      <div className="section-toolbar"><h3 style={{ margin: 0 }}>لیست ایستگاه‌ها</h3><span>{loading ? 'در حال بارگذاری…' : stations.length.toLocaleString('fa-IR') + ' ایستگاه'}</span></div>
      <div className="table-wrap"><table><thead><tr><th>نام</th><th>زون</th><th>نوع</th><th>اینترنت</th><th>نرخ</th><th>وضعیت</th><th>Agent</th><th>عملیات</th></tr></thead>
      <tbody>{stations.map(station => <tr key={station.id}><td><strong>{station.name}</strong><div className="muted ltr">{station.id}</div></td><td>{station.zone}</td><td>{station.type}</td><td>اینترنت {station.network}</td><td>{station.ratePerHour.toLocaleString('fa-IR')}</td><td>{station.isActive ? station.state : 'آرشیو'}</td><td>{station.agentDeviceName || '—'}</td><td><button type="button" className="btn sm" onClick={() => edit(station)}>ویرایش</button>{station.isActive && <button type="button" className="btn sm danger" onClick={() => void archive(station)}>غیرفعال</button>}</td></tr>)}</tbody></table></div>
    </section>
    {notice && <div className="operation-toast">{notice}<button type="button" onClick={() => setNotice('')}>×</button></div>}
  </div>;
}
