import { useEffect, useState } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import './App.css';
import { TopNavigation } from './components/TopNavigation';
import { DashboardPage } from './pages/DashboardPage';
import { CustomersPage } from './pages/CustomersPage';
import { BuffetPage } from './pages/BuffetPage';
import { ReportsPage } from './pages/ReportsPage';
import { UsersPage } from './pages/UsersPage';
import { SettingsPage } from './pages/SettingsPage';
import { TariffsPage } from './pages/TariffsPage';
import { GamesPage } from './pages/GamesPage';
import { AccountsPage } from './pages/AccountsPage';
import { ClientShellPage } from './pages/ClientShellPage';
import { ClientExperience } from './features/client/ClientExperience';
import { GlobalCommandCenter } from './features/search/GlobalCommandCenter';
import { OperationsPage } from './pages/OperationsPage';
import type { DashboardSnapshotDto, PageKey, ServerInfoDto } from './types';
import { normalizeDashboardSnapshot } from './services/dashboardAdapter';

type HubState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
type DemoRole = 'operator' | 'manager' | 'owner';

const roleLabels: Record<DemoRole, string> = { operator: 'اپراتور', manager: 'مدیر', owner: 'صاحب' };

function formatTime(dateString: string) {
  const date = new Date(dateString);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' });
}

function formatPersianDate(date = new Date()) {
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(date);
}

function DashboardApp() {
  const [activePage, setActivePage] = useState<PageKey>('dashboard');
  const [snapshot, setSnapshot] = useState<DashboardSnapshotDto | null>(null);
  const [serverInfo, setServerInfo] = useState<ServerInfoDto | null>(null);
  const [apiState, setApiState] = useState<'loading' | 'online' | 'offline'>('loading');
  const [hubState, setHubState] = useState<HubState>('connecting');
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const [role, setRole] = useState<DemoRole>('operator');
  const [clock, setClock] = useState(() => new Date().toLocaleTimeString('fa-IR'));
  const [commandOpen, setCommandOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [notifications, setNotifications] = useState([
    { id: 'n1', title: 'درخواست بوفه', detail: 'PC ۰۴ درخواست فروش بوفه دارد', level: 'info', read: false },
    { id: 'n2', title: 'به‌روزرسانی کلاینت', detail: '۲ ایستگاه به‌روزرسانی معلق دارند', level: 'warning', read: false },
    { id: 'n3', title: 'رزرو نزدیک', detail: 'رزرو PC ۰۷ تا ۱۵ دقیقه دیگر شروع می‌شود', level: 'info', read: false },
  ]);

  useEffect(() => {
    window.dispatchEvent(new CustomEvent('gamenet-role-change', { detail: role }));
  }, [role]);

  useEffect(() => {
    const onNavigate = (event: Event) => {
      const page = (event as CustomEvent<PageKey>).detail;
      if (page) setActivePage(page);
    };
    window.addEventListener('gamenet-navigate', onNavigate);
    return () => window.removeEventListener('gamenet-navigate', onNavigate);
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => setClock(new Date().toLocaleTimeString('fa-IR')), 500);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      let hk: Record<string,string> = {};
      try { hk = JSON.parse(localStorage.getItem('gamenet-hotkeys-v1') || '{}'); } catch { hk = {}; }
      const reportKey = (hk.reports || 'F2').toUpperCase();
      const buffetKey = (hk.buffet || 'F3').toUpperCase();
      const closeShiftKey = (hk.closeShift || 'F9').toUpperCase();
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setCommandOpen(open => !open);
        setCommandQuery('');
      } else if (event.key === 'Escape') {
        setCommandOpen(false);
        setNotificationsOpen(false);
      } else if (event.key.toUpperCase() === reportKey) setActivePage('reports');
      else if (event.key.toUpperCase() === buffetKey) setActivePage('buffet');
      else if (event.key.toUpperCase() === closeShiftKey) setActivePage('users');
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);


  useEffect(() => {
    let active = true;
    const connection = new HubConnectionBuilder().withUrl('/hubs/dashboard').withAutomaticReconnect().build();

    connection.on('ServerReady', (info: ServerInfoDto) => {
      if (active) setServerInfo(info);
    });
    connection.onreconnecting(() => {
      if (active) setHubState('reconnecting');
    });
    connection.onreconnected(() => {
      if (active) setHubState('connected');
    });
    connection.onclose(() => {
      if (active) setHubState('disconnected');
    });

    void connection.start().catch(() => {
      if (active) setHubState('disconnected');
    });

    const loadSnapshot = async () => {
      setApiState('loading');
      setError('');
      try {
        const response = await fetch('/api/dashboard');
        if (!response.ok) throw new Error(`API returned ${response.status}`);
        const data = normalizeDashboardSnapshot((await response.json()) as DashboardSnapshotDto);
        if (active) {
          setSnapshot(data);
          setApiState('online');
        }
      } catch (cause) {
        if (active) {
          setApiState('offline');
          setError(cause instanceof Error ? cause.message : 'ارتباط با سرور ناموفق بود');
        }
      }
    };

    void loadSnapshot();

    return () => {
      active = false;
      void connection.stop();
    };
  }, [retry]);

  return (
    <div className="app-shell" dir="rtl">
      <header className="topbar">
        <div className="brand-mark">گ</div>
        <div className="brand-copy">
          <strong>گیم‌نت منیجر</strong>
          <span>داشبورد مدیریت</span>
        </div>

        <TopNavigation activePage={activePage} onChange={setActivePage} />

        <div className="connection-list" aria-live="polite">
          <span className="header-clock">🗓 {formatPersianDate()} · 🕒 {clock}</span>
          <span className={`status-chip ${apiState === 'online' ? 'online' : apiState === 'loading' ? 'loading' : 'offline'}`}>
            API {apiState === 'online' ? 'متصل' : apiState === 'loading' ? 'در حال اتصال' : 'قطع'}
          </span>
          <span className={`status-chip ${hubState === 'connected' ? 'online' : hubState === 'reconnecting' ? 'loading' : hubState === 'connecting' ? 'loading' : 'offline'}`}>
            SignalR {hubState === 'connected' ? 'متصل' : hubState === 'reconnecting' ? 'اتصال مجدد' : hubState === 'connecting' ? 'در حال اتصال' : 'قطع'}
          </span>
        </div>

        <button type="button" className="refresh-button command-trigger" onClick={() => setCommandOpen(true)} title="مرکز جست‌وجو و فرمان · Ctrl+K">⌕ جست‌وجو · Ctrl+K</button>
        <button type="button" className="refresh-button" onClick={() => setRetry((value) => value + 1)}>
          تلاش مجدد
        </button>

        <button type="button" className="user-pill role-switch" onClick={() => setRole(current => current === 'operator' ? 'manager' : current === 'manager' ? 'owner' : 'operator')} title="برای تغییر نقش دمو کلیک کنید">
          <span className="user-dot" />
          <span>{roleLabels[role]}: {role === 'operator' ? 'علی محمدی' : role === 'manager' ? 'سارا احمدی' : 'محمود رضایی'}</span>
        </button>
        <button type="button" className="refresh-button" onClick={() => setNotificationsOpen(open => !open)} aria-label="اعلان‌ها">🔔 {notifications.filter(item => !item.read).length}</button>
      </header>

      {notificationsOpen && <div className="notification-popover"><div style={{display:'flex',justifyContent:'space-between',alignItems:'center',gap:12}}><strong>اعلان‌ها</strong><button type="button" className="btn sm" onClick={() => setNotifications(current => current.map(item => ({ ...item, read: true })))}>خوانده‌شده</button></div>{notifications.map(item => <button type="button" key={item.id} className={`notification-item ${item.read ? 'read' : ''}`} onClick={() => setNotifications(current => current.map(row => row.id === item.id ? { ...row, read: true } : row))}><strong>{item.title}</strong><span>{item.detail}</span></button>)}</div>}

      {error && (
        <div className="error-banner" role="alert">
          ارتباط API برقرار نشد: {error}. سرور را روی پورت ۵۰۸۰ اجرا کنید.
        </div>
      )}

      <div className="page-shell">
        <div hidden={activePage !== 'dashboard'}><DashboardPage snapshot={snapshot} apiState={apiState} serverInfo={serverInfo} error={error} onNavigate={setActivePage} /></div>
        <div hidden={activePage !== 'games'}><GamesPage /></div>
        <div hidden={activePage !== 'client-shell'}><ClientShellPage /></div>
        <div hidden={activePage !== 'customers'}><CustomersPage /></div>
        <div hidden={activePage !== 'tariffs'}><TariffsPage /></div>
        <div hidden={activePage !== 'accounts'}><AccountsPage /></div>
        <div hidden={activePage !== 'buffet'}><BuffetPage /></div>
        <div hidden={activePage !== 'reports'}><ReportsPage /></div>
        <div hidden={activePage !== 'users'}><UsersPage /></div>
        <div hidden={activePage !== 'settings'}><SettingsPage /></div>
        <div hidden={activePage !== 'operations'}><OperationsPage /></div>
      </div>

      <GlobalCommandCenter open={commandOpen} stations={snapshot?.stations ?? []} onNavigate={setActivePage} onClose={() => setCommandOpen(false)} />

      <footer className="status-footer">
        {(snapshot && snapshot.generatedAt ? `آخرین به‌روزرسانی ${formatTime(snapshot.generatedAt)}` : 'در انتظار دریافت داده')} · {serverInfo?.environment ?? 'Development'}
      </footer>
    </div>
  );
}

function App() {
  const path = window.location.pathname.replace(/\/+$/, '') || '/';
  return path === '/client' ? <ClientExperience /> : <DashboardApp />;
}

export default App;
