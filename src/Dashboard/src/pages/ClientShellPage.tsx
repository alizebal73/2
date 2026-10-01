import { useEffect, useMemo, useState } from 'react';
import type { ClientRecord } from '../types';
import { mockService } from '../services/mockService';

type ContextMenu = { x: number; y: number; client: ClientRecord } | null;
type ClientSettings = Pick<ClientRecord, 'ip' | 'dns1' | 'dns2' | 'systemNumber' | 'serverAddress' | 'shell' | 'network' | 'bootMode'>;

export function ClientShellPage() {
  const [clients, setClients] = useState<ClientRecord[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [lastIndex, setLastIndex] = useState<number | null>(null);
  const [context, setContext] = useState<ContextMenu>(null);
  const [settingsTarget, setSettingsTarget] = useState<string[]>([]);
  const [settings, setSettings] = useState<ClientSettings | null>(null);
  const [query, setQuery] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => { void mockService.getClients().then(setClients); }, []);
  useEffect(() => {
    const onSelect = (event: Event) => {
      const name = (event as CustomEvent<string>).detail;
      if (!name) return;
      setTimeout(() => {
        const client = clients.find(item => item.name === name);
        if (client) openSettings([client.id]);
      }, 0);
    };
    window.addEventListener('gamenet-select-client', onSelect);
    return () => window.removeEventListener('gamenet-select-client', onSelect);
  }, [clients]);


  const visible = useMemo(() => clients.filter(client => client.name.toLowerCase().includes(query.toLowerCase()) || client.ip.includes(query)), [clients, query]);
  const onlineCount = clients.filter(client => client.online).length;
  const busyCount = clients.filter(client => client.user).length;
  const pendingCount = clients.filter(client => client.updatePending).length;

  function updateClients(ids: string[], update: Partial<ClientRecord>) {
    setClients(current => current.map(client => ids.includes(client.id) ? { ...client, ...update } : client));
  }

  function selectClient(event: React.MouseEvent, client: ClientRecord, index: number) {
    if (event.shiftKey && lastIndex !== null) {
      const start = Math.min(lastIndex, index);
      const end = Math.max(lastIndex, index);
      setSelected(current => new Set([...current, ...visible.slice(start, end + 1).map(item => item.id)]));
    } else if (event.ctrlKey || event.metaKey) {
      setSelected(current => {
        const next = new Set(current);
        if (next.has(client.id)) next.delete(client.id); else next.add(client.id);
        return next;
      });
      setLastIndex(index);
    } else {
      setSelected(new Set([client.id]));
      setLastIndex(index);
    }
  }

  function openSettings(ids: string[]) {
    const client = clients.find(item => item.id === ids[0]);
    if (!client) return;
    setSettingsTarget(ids);
    setSettings({ ip: client.ip, dns1: client.dns1, dns2: client.dns2, systemNumber: client.systemNumber, serverAddress: client.serverAddress, shell: client.shell, network: client.network, bootMode: client.bootMode });
    setContext(null);
  }

  function action(kind: string) {
    const ids = selected.has(context?.client.id ?? '') ? [...selected] : context ? [context.client.id] : [...selected];
    setContext(null);
    if (!ids.length) return;
    const clientNames = clients.filter(client => ids.includes(client.id)).map(client => client.name).join('، ');
    if (kind === 'settings') { openSettings(ids); return; }
    if (kind === 'switch-network') {
      setClients(current => current.map(client => ids.includes(client.id) ? { ...client, network: client.network === 'internet1' ? 'internet2' : 'internet1' } : client));
    } else if (kind === 'toggle-internet') {
      setClients(current => current.map(client => ids.includes(client.id) ? { ...client, internetEnabled: !client.internetEnabled, network: client.internetEnabled ? 'lan' : 'internet1' } : client));
    } else if (kind === 'logout') {
      updateClients(ids, { user: '', game: '', locked: true });
    } else if (kind === 'login') {
      const code = window.prompt('شناسه مشتری را وارد کنید');
      if (code) updateClients(ids, { user: code, locked: false });
    } else if (kind === 'message') {
      const text = window.prompt('پیام برای مشتری');
      if (text) setNotice(`پیام برای ${clientNames} ثبت شد: ${text}`);
      return;
    } else if (kind === 'restart-shell') updateClients(ids, { shell: true });
    else if (kind === 'restart' || kind === 'shutdown') updateClients(ids, { online: false, user: '', game: '' });
    else if (kind === 'screenshot') { setNotice(`درخواست Screenshot برای ${ids.length} کلاینت در صف قرار گرفت`); return; }
    else if (kind === 'move-user') {
      const destination = window.prompt('شماره کلاینت مقصد، برای نمونه ۱۲');
      if (destination) setNotice(`انتقال کاربر از ${clientNames} به PC ${destination} ثبت شد`);
      return;
    }
    setNotice(`${kind} برای ${ids.length} کلاینت اجرا شد`);
  }

  function showContext(event: React.MouseEvent, client: ClientRecord) {
    event.preventDefault();
    if (!selected.has(client.id)) setSelected(new Set([client.id]));
    const width = 260;
    const height = 390;
    setContext({ x: Math.max(8, Math.min(event.clientX, window.innerWidth - width - 8)), y: Math.max(8, Math.min(event.clientY, window.innerHeight - height - 8)), client });
  }

  function saveSettings() {
    if (!settings) return;
    updateClients(settingsTarget, settings);
    setNotice(`تنظیمات برای ${settingsTarget.length} کلاینت ذخیره و ارسال شد`);
    setSettings(null);
  }

  return <>
    <div className="page-header"><div><p>مدیریت شبکه و سیستم‌ها</p><h1>کلاینت‌ها · ۴۰ رایانه</h1></div></div>
    <div className="summary-grid">
      <div className="summary-card"><div className="label">آنلاین</div><div className="value green">{onlineCount}</div></div>
      <div className="summary-card"><div className="label">آفلاین</div><div className="value red">{clients.length - onlineCount}</div></div>
      <div className="summary-card"><div className="label">در حال بازی</div><div className="value blue">{busyCount}</div></div>
      <div className="summary-card"><div className="label">آپدیت معلق</div><div className="value orange">{pendingCount}</div></div>
    </div>
    <div className="toolbar client-toolbar">
      <div className="search-box"><input aria-label="جستجوی کلاینت" value={query} onChange={event => setQuery(event.target.value)} placeholder="جست‌وجوی نام یا IP کلاینت…" /></div>
      <button className="btn" onClick={() => setNotice(`Wake-on-LAN برای ${clients.length} کلاینت در صف قرار گرفت`)}>📡 Wake-on-LAN همه</button>
      <button className="btn" onClick={() => openSettings(selected.size ? [...selected] : clients.map(item => item.id))}>⚙ تنظیم گروهی</button>
      <button className="btn primary" onClick={() => { updateClients(clients.map(item => item.id), { dns1: '178.22.122.100', dns2: '185.51.200.2' }); setNotice('DNS پیش‌فرض برای همه کلاینت‌ها ارسال شد'); }}>📤 ارسال DNS به همه</button>
    </div>
    {selected.size > 0 && <div className="selection-bar"><strong>{selected.size} کلاینت انتخاب شده</strong><span>راست‌کلیک روی دستگاه انتخابی، عملیات را برای همه اعمال می‌کند</span><button className="btn sm" onClick={() => { setSelected(new Set()); setLastIndex(null); }}>پاک کردن انتخاب‌ها</button></div>}
    <div className="client-hint">کلیک: انتخاب · Ctrl+کلیک: چندانتخاب · Shift+کلیک: انتخاب بازه · دابل‌کلیک: تنظیمات کلاینت · راست‌کلیک: عملیات</div>
    <div className="client-grid">
      {visible.map((client, index) => <article key={client.id} className={`client-card ${selected.has(client.id) ? 'selected' : ''} ${client.online ? '' : 'offline'}`} onClick={event => selectClient(event, client, index)} onDoubleClick={() => openSettings([client.id])} onContextMenu={event => showContext(event, client)}>
        <div className="client-card-head"><b>{client.name}</b><span className={`status-pill ${client.online ? 'online' : 'offline'}`}>{client.online ? 'آنلاین' : 'آفلاین'}</span></div>
        <div className="meta ltr">IP {client.ip} · DNS {client.dns1}</div>
        <div className="meta">{client.user ? `یوزر: ${client.user} · ${client.game || 'بدون بازی'}` : client.locked ? 'سیستم قفل است' : 'بدون کاربر'}</div>
        <div className="meta">شبکه: {client.network === 'internet1' ? 'اینترنت ۱' : client.network === 'internet2' ? 'اینترنت ۲' : 'فقط LAN'} · Shell: {client.shell ? 'فعال' : 'غیرفعال'}</div>
        <div className="client-flags">{client.updatePending && <span className="status-pill pending">Update pending</span>}<span className="meta">Sync: {client.lastSync}</span></div>
      </article>)}
    </div>
    {context && <div className="context-menu client-context" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}><strong>{selected.size > 1 ? `${selected.size} کلاینت انتخابی` : context.client.name}</strong><button onClick={() => action('switch-network')}>🌐 تغییر اینترنت ۱ ↔ ۲</button><button onClick={() => action('toggle-internet')}>🔌 قطع / وصل اینترنت</button><button onClick={() => action('move-user')}>🔀 جابه‌جایی یوزر</button><button onClick={() => action('logout')}>🚪 خروج یوزر و قفل</button><button onClick={() => action('login')}>🔑 ورود با شناسه</button><button onClick={() => action('message')}>💬 ارسال پیام</button><button onClick={() => action('screenshot')}>📸 Screenshot</button><button onClick={() => action('restart-shell')}>🔄 Restart Shell</button><button onClick={() => action('restart')}>⏻ Restart Windows</button><button onClick={() => action('shutdown')}>⛔ Shutdown</button><button onClick={() => action('settings')}>⚙ تنظیمات کامل کلاینت</button></div>}
    {settings && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setSettings(null)}><section className="operation-modal wide" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setSettings(null)}>×</button><h2>تنظیمات {settingsTarget.length > 1 ? `${settingsTarget.length} کلاینت` : clients.find(item => item.id === settingsTarget[0])?.name}</h2><div className="settings-form-grid">
      {([['ip', 'IP محلی'], ['systemNumber', 'شماره سیستم'], ['dns1', 'DNS ۱'], ['dns2', 'DNS ۲'], ['serverAddress', 'آدرس سرور']] as const).map(([field, label]) => <label key={field}>{label}<input value={settings[field]} onChange={event => setSettings({ ...settings, [field]: field === 'systemNumber' ? Number(event.target.value) : event.target.value })} /></label>)}
      <label>شل هنگام بوت<select value={settings.shell ? 'on' : 'off'} onChange={event => setSettings({ ...settings, shell: event.target.value === 'on' })}><option value="on">فعال</option><option value="off">غیرفعال</option></select></label>
      <label>شبکه<select value={settings.network} onChange={event => setSettings({ ...settings, network: event.target.value as ClientSettings['network'] })}><option value="internet1">اینترنت ۱</option><option value="internet2">اینترنت ۲</option><option value="lan">فقط LAN</option></select></label>
      <label>Boot<select value={settings.bootMode} onChange={event => setSettings({ ...settings, bootMode: event.target.value as ClientSettings['bootMode'] })}><option value="normal">عادی</option><option value="ccboot">CCBOOT</option><option value="pxe">PXE</option></select></label>
    </div><div className="modal-actions"><button className="btn primary" onClick={saveSettings}>ذخیره و ارسال</button><button className="btn" onClick={() => setNotice('درخواست Remote ثبت شد')}>Remote</button><button className="btn danger" onClick={() => { updateClients(settingsTarget, { online: false }); setSettings(null); setNotice('Restart ثبت شد'); }}>Restart</button></div></section></div>}
    {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
