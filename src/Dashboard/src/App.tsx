import { useEffect, useMemo, useState } from 'react';
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
import { UserErrorBanner } from './components/UserErrorBanner';
import { LoginPage } from './pages/LoginPage';
import { getCurrentUser, logout, hasPermission } from './services/authService';
import type { AppUserRecord } from './types';
import { SectionLockDialog } from './components/SectionLockDialog';
import { readPageLocks } from './services/securityService';
import type { PageLockMap } from './types';
import type { AgentStatusDto, DashboardSnapshotDto, PageKey, ServerInfoDto, StationDto } from './types';
import { normalizeDashboardSnapshot } from './services/dashboardAdapter';
import { mockService } from './services/mockService';
import { getAgentStatuses } from './services/agentService';

type HubState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
type DemoRole = 'operator' | 'manager' | 'owner';
const roleLabels: Record<DemoRole, string> = { operator: 'اپراتور', manager: 'مدیر', owner: 'صاحب' };
function mapRole(role: string): DemoRole {
  if (role.toLowerCase() === 'owner' || role.toLowerCase() === 'admin') return 'owner';
  if (role.toLowerCase() === 'manager') return 'manager';
  return 'operator';
}

function formatTime(dateString: string) {
  const date = new Date(dateString);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' });
}

function formatPersianDate(date = new Date()) {
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(date);
}

const pagePermissions: Partial<Record<PageKey, string[]>> = {
  customers: ['customer.manage', 'customer.wallet', 'customer.debt'],
  buffet: ['buffet.sell', 'buffet.inventory'],
  tariffs: ['tariff.manage'],
  games: ['game.manage'],
  'client-shell': ['client.control'],
  accounts: ['account.manage'],
  reports: ['finance.view'],
  users: ['user.manage', 'shift.manage', 'payroll.view', 'payroll.manage', 'approval.decide'],
  settings: ['user.manage'],
};

function canOpenPage(user: AppUserRecord, page: PageKey): boolean {
  if (page === 'dashboard' || page === 'operations') return true;
  const required = pagePermissions[page];
  return !required || required.some(permission => hasPermission(user, permission));
}

function DashboardApp({ user, onLogout }: { user: AppUserRecord; onLogout: () => void }) {
  const [activePage, setActivePage] = useState<PageKey>('dashboard');
  const [snapshot, setSnapshot] = useState<DashboardSnapshotDto | null>(null);
  const [agentStatuses, setAgentStatuses] = useState<AgentStatusDto[]>([]);
  const [serverInfo, setServerInfo] = useState<ServerInfoDto | null>(null);
  const [apiState, setApiState] = useState<'loading' | 'online' | 'offline'>('loading');
  const [hubState, setHubState] = useState<HubState>('connecting');
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const role = mapRole(user.role);
  const [clock, setClock] = useState(() => new Date().toLocaleTimeString('fa-IR'));
  const [commandOpen, setCommandOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [pageLocks, setPageLocks] = useState<PageLockMap>(() => readPageLocks());
  const [lockedPage, setLockedPage] = useState<PageKey | null>(null);
  const [unlockedPages, setUnlockedPages] = useState<PageKey[]>(['dashboard']);
  const [notifications, setNotifications] = useState([
    { id: 'n1', title: 'درخواست بوفه', detail: 'PC ۰۴ درخواست فروش بوفه دارد', level: 'info', read: false },
    { id: 'n2', title: 'به‌روزرسانی کلاینت', detail: '۲ ایستگاه به‌روزرسانی معلق دارند', level: 'warning', read: false },
    { id: 'n3', title: 'رزرو نزدیک', detail: 'رزرو PC ۰۷ تا ۱۵ دقیقه دیگر شروع می‌شود', level: 'info', read: false },
  ]);

  useEffect(() => {
    const onNavigate = (event: Event) => {
      const page = (event as CustomEvent<PageKey>).detail;
      if (page) requestNavigation(page);
    };
    window.addEventListener('gamenet-navigate', onNavigate);
    return () => window.removeEventListener('gamenet-navigate', onNavigate);
  }, []);

  useEffect(() => {
    const timer = window.setInterval(() => setClock(new Date().toLocaleTimeString('fa-IR')), 500);
    return () => window.clearInterval(timer);
  }, []);

  function requestNavigation(page: PageKey) {
    if (!canOpenPage(user, page)) {
      setError('برای مشاهده این بخش دسترسی لازم را ندارید.');
      return;
    }
    const rule = pageLocks[page];
    if (rule?.enabled && !unlockedPages.includes(page)) { setLockedPage(page); return; }
    setActivePage(page);
  }

  useEffect(() => {
    const onLocksChanged = (event: Event) => setPageLocks((event as CustomEvent<PageLockMap>).detail || readPageLocks());
    window.addEventListener('gamenet-page-locks-changed', onLocksChanged);
    return () => window.removeEventListener('gamenet-page-locks-changed', onLocksChanged);
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
      } else if (event.key === 'Escape') {
        setCommandOpen(false);
        setNotificationsOpen(false);
      } else if (event.key.toUpperCase() === reportKey) requestNavigation('reports');
      else if (event.key.toUpperCase() === buffetKey) requestNavigation('buffet');
      else if (event.key.toUpperCase() === closeShiftKey) requestNavigation('users');
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);


  useEffect(() => {
    if (!hasPermission(user, 'client.control')) {
      setAgentStatuses([]);
      return;
    }

    let active = true;
    const loadAgentStatuses = async () => {
      try {
        const data = await getAgentStatuses();
        if (active) setAgentStatuses(data);
      } catch {
        if (active) setAgentStatuses([]);
      }
    };

    void loadAgentStatuses();
    const interval = window.setInterval(() => void loadAgentStatuses(), 5000);

    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [user, retry]);

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
      } catch {
        if (!active) return;

        setApiState('offline');
        setError('ارتباط با سرور برقرار نشد');

        // Development-only visual fallback: keep the UI browsable while
        // the real Server is unavailable. Production never uses mock data.
        if (import.meta.env.DEV) {
          try {
            const managedStations = await mockService.getManagedStations();
            const previewStations: StationDto[] = managedStations.map((station) => ({
              id: station.id,
              name: station.name,
              zone: station.zone,
              type: station.type,
              ratePerHour: station.ratePerHour,
              state: station.status === 'off'
                ? 'off'
                : station.status === 'reserved'
                  ? 'reserved'
                  : 'free',
            }));

            setSnapshot({
              totalStations: previewStations.length,
              stations: previewStations,
              generatedAt: new Date().toISOString(),
            });
          } catch {
            setSnapshot(null);
          }
        }
      }
    };

    void loadSnapshot();

    return () => {
      active = false;
      void connection.stop();
    };
  }, [retry]);

  const agentByStationId = useMemo(
    () => new Map(agentStatuses.filter(item => item.stationId).map(item => [item.stationId!, item])),
    [agentStatuses],
  );

  const dashboardSnapshot = useMemo<DashboardSnapshotDto | null>(() => {
    if (!snapshot) return null;
    return {
      ...snapshot,
      stations: snapshot.stations.map(station => {
        const agent = agentByStationId.get(station.id);
        return agent
          ? {
              ...station,
              agentId: agent.agentId,
              agentOnline: agent.isOnline,
              agentLastSeenAt: agent.lastSeenAt ?? null,
              agentVersion: agent.agentVersion ?? null,
              agentLocked: agent.isLocked,
            }
          : station;
      }),
    };
  }, [snapshot, agentByStationId]);

  return (
    <div className="app-shell" dir="rtl">
      <header className="topbar">
        <div className="brand-mark">گ</div>
        <div className="brand-copy">
          <strong>گیم‌نت منیجر</strong>
          <span>داشبورد مدیریت</span>
        </div>

        <TopNavigation activePage={activePage} onChange={requestNavigation} user={user} />

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

        <button type="button" className="user-pill role-switch" onClick={onLogout} title="خروج از حساب">
          <span className="user-dot" />
          <span>{roleLabels[role]}: {user.fullName}</span>
        </button>
        <button type="button" className="refresh-button" onClick={() => setNotificationsOpen(open => !open)} aria-label="اعلان‌ها">🔔 {notifications.filter(item => !item.read).length}</button>
      </header>

      {notificationsOpen && <div className="notification-popover"><div style={{display:'flex',justifyContent:'space-between',alignItems:'center',gap:12}}><strong>اعلان‌ها</strong><button type="button" className="btn sm" onClick={() => setNotifications(current => current.map(item => ({ ...item, read: true })))}>خوانده‌شده</button></div>{notifications.map(item => <button type="button" key={item.id} className={`notification-item ${item.read ? 'read' : ''}`} onClick={() => setNotifications(current => current.map(row => row.id === item.id ? { ...row, read: true } : row))}><strong>{item.title}</strong><span>{item.detail}</span></button>)}</div>}

      {error && (
        <UserErrorBanner
          kind="network"
          title="ارتباط با سرور برقرار نشد"
          detail="دادهٔ جدید از سرور دریافت نشد. اتصال شبکه یا خود سرویس را بررسی کنید."
          onAction={() => setRetry(value => value + 1)}
        />
      )}

      <div className="page-shell">
        <div hidden={activePage !== 'dashboard'}><DashboardPage snapshot={dashboardSnapshot} apiState={apiState} serverInfo={serverInfo} error={error} onNavigate={requestNavigation} role={role} user={user} /></div>
        <div hidden={activePage !== 'games'}><GamesPage /></div>
        <div hidden={activePage !== 'client-shell'}><ClientShellPage /></div>
        <div hidden={activePage !== 'customers'}><CustomersPage user={user} /></div>
        <div hidden={activePage !== 'tariffs'}><TariffsPage /></div>
        <div hidden={activePage !== 'accounts'}><AccountsPage /></div>
        <div hidden={activePage !== 'buffet'}><BuffetPage user={user} /></div>
        <div hidden={activePage !== 'reports'}><ReportsPage user={user} /></div>
        <div hidden={activePage !== 'users'}><UsersPage user={user} /></div>
        <div hidden={activePage !== 'settings'}><SettingsPage /></div>
        <div hidden={activePage !== 'operations'}><OperationsPage /></div>
      </div>

      <GlobalCommandCenter open={commandOpen} stations={dashboardSnapshot?.stations ?? []} onNavigate={requestNavigation} onClose={() => setCommandOpen(false)} />

      <SectionLockDialog page={lockedPage} onClose={() => setLockedPage(null)} onUnlock={page => { setUnlockedPages(current => current.includes(page) ? current : [...current, page]); setActivePage(page); setLockedPage(null); }} />

      <footer className="status-footer">
        {(snapshot && snapshot.generatedAt ? `آخرین به‌روزرسانی ${formatTime(snapshot.generatedAt)}` : 'در انتظار دریافت داده')} · {serverInfo?.environment ?? 'Development'}
      </footer>
    </div>
  );
}

function App() {
  const path = window.location.pathname.replace(/\/+$/, '') || '/';
  const [user, setUser] = useState<AppUserRecord | null>(null);
  const [loadingAuth, setLoadingAuth] = useState(true);

  useEffect(() => {
    if (path === '/client') {
      setLoadingAuth(false);
      return;
    }
    void getCurrentUser()
      .then(setUser)
      .finally(() => setLoadingAuth(false));
  }, [path]);

  if (path === '/client') return <ClientExperience />;
  if (loadingAuth) return <main className="app-shell" dir="rtl" style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>در حال بررسی دسترسی…</main>;
  if (!user) return <LoginPage onLoggedIn={setUser} />;
  return <DashboardApp user={user} onLogout={() => void logout().finally(() => setUser(null))} />;
}

export default App;
