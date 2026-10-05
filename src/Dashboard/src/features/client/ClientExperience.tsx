import { useEffect, useMemo, useRef, useState } from 'react';
import './ClientExperience.css';
import { authenticateCustomer, readCustomerState, releaseCustomerLogin } from '../../services/customerAuthService';
import { createClientRequest, launchClientGame, lockClient, logoutAndLockClient, stopClientGame } from '../../services/clientExperienceService';
import { getClientCatalog, type ClientCatalogBuffetItem, type ClientCatalogGame } from '../../services/clientCatalogService';
import { getClientIdentity } from '../../services/clientIdentityService';

type ContextMenu = { x: number; y: number } | null;
type Panel = 'buffet' | 'account' | null;
type ViewMode = 'card' | 'compact' | 'list';

type Game = ClientCatalogGame;

const commands = [
  { title: 'منوی بوفه', key: 'buffet', icon: '🛒' },
  { title: 'حساب من', key: 'account', icon: '👤' },
  { title: 'درخواست شارژ', key: 'charge', icon: '💳' },
  { title: 'پیام به اپراتور', key: 'message', icon: '💬' },
  { title: 'تبلیغات', key: 'advertisement', icon: '📢' },
];

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(value);
}

export function ClientExperience() {
  const [customerCode, setCustomerCode] = useState('');
  const [password, setPassword] = useState('');
  const [loggedIn, setLoggedIn] = useState(false);
  const [loginError, setLoginError] = useState('');
  const [customerName, setCustomerName] = useState('مهمان');
  const [stationName, setStationName] = useState('GameNet');
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [loginId, setLoginId] = useState<string | null>(null);
  const [loginLimit, setLoginLimit] = useState(1);
  const [activeLoginCount, setActiveLoginCount] = useState(0);
  const [freeMoney, setFreeMoney] = useState(0);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [sessionEndAt, setSessionEndAt] = useState<string | null>(null);
  const [sessionState, setSessionState] = useState<string | null>(null);
  const browserClientKey = useMemo(() => {
    const key = 'gamenet-client-browser-key';
    const saved = localStorage.getItem(key);
    if (saved) return saved;
    const created = crypto.randomUUID();
    localStorage.setItem(key, created);
    return created;
  }, []);
  const [deviceId, setDeviceId] = useState(browserClientKey);
  const [clientIdentityReady, setClientIdentityReady] = useState(false);
  const [wallet, setWallet] = useState(0);
  const [remainingSeconds, setRemainingSeconds] = useState(0);
  const [now, setNow] = useState(Date.now());
  const [view, setView] = useState<ViewMode>('card');
  const [zoom, setZoom] = useState(100);
  const [activeGame, setActiveGame] = useState<string | null>(null);
  const [panel, setPanel] = useState<Panel>(null);
  const [context, setContext] = useState<ContextMenu>(null);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [paletteQuery, setPaletteQuery] = useState('');
  const [notice, setNotice] = useState('');
  const [adVisible, setAdVisible] = useState(true);
  const [locked, setLocked] = useState(false);
  const [games, setGames] = useState<Game[]>([]);
  const [buffetItems, setBuffetItems] = useState<ClientCatalogBuffetItem[]>([]);

  const visibleCommands = useMemo(() => commands.filter(item => item.title.includes(paletteQuery.trim())), [paletteQuery]);

  useEffect(() => {
    let active = true;
    void getClientIdentity()
      .then(identity => {
        if (!active) return;
        setDeviceId(identity.deviceId);
        setClientIdentityReady(true);
      })
      .catch(() => {
        if (active) setClientIdentityReady(false);
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!clientIdentityReady) return;
    let active = true;
    void getClientCatalog()
      .then(catalog => {
        if (!active) return;
        setGames(catalog.games);
        setBuffetItems(catalog.buffet.filter(item => item.available));
      })
      .catch(error => {
        if (active) {
          setGames([]);
          setBuffetItems([]);
          notify(error instanceof Error ? error.message : 'اطلاعات کلاینت دریافت نشد.');
        }
      });
    return () => {
      active = false;
    };
  }, [clientIdentityReady]);

  useEffect(() => {
    const interval = window.setInterval(() => {
      const currentNow = Date.now();
      setNow(currentNow);
      if (loggedIn && sessionEndAt && !locked) {
        setRemainingSeconds(Math.max(0, Math.ceil((new Date(sessionEndAt).getTime() - currentNow) / 1000)));
      }
    }, 1000);
    return () => window.clearInterval(interval);
  }, [loggedIn, locked, sessionEndAt]);

  useEffect(() => {
    if (!loggedIn || !customerId || !loginId) return;
    let active = true;

    const poll = async () => {
      try {
        const state = await readCustomerState(customerId, loginId, deviceId);
        if (!active) return;
        if (!state.authenticated) {
          signOut(true);
          notify('ورود مشتری توسط Agent پایان یافت.');
          return;
        }
        setCustomerName(state.fullName);
        setWallet(state.balance);
        setSessionState(state.session?.state ?? null);
        setSessionId(state.session?.id ?? null);
        setActiveGame(state.session?.gameId ?? null);
        setSessionEndAt(state.session?.endAt ?? null);
        setFreeMoney(state.freeMoney);
        setStationName(state.session?.stationName ?? stationName);
        if (state.session?.endAt) {
          setRemainingSeconds(Math.max(0, Math.ceil((new Date(state.session.endAt).getTime() - Date.now()) / 1000)));
        }
      } catch {
        // UI remains usable; next poll retries.
      }
    };

    void poll();
    const interval = window.setInterval(() => { void poll(); }, 2000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [loggedIn, customerId, loginId, deviceId]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setPaletteOpen(value => !value);
        setPaletteQuery('');
      } else if (event.key === 'Escape') {
        setPaletteOpen(false);
        setContext(null);
        setPanel(null);
      } else if (event.ctrlKey && event.key.toLowerCase() === 'm') {
        event.preventDefault();
        void requestOperator('move');
      } else if (event.ctrlKey && event.key.toLowerCase() === 'l') {
        event.preventDefault();
        if (loggedIn) void logoutCustomer();
        else document.getElementById('client-login-id')?.focus();
      } else if (event.ctrlKey && event.key.toLowerCase() === 'r') {
        event.preventDefault();
        void requestOperator('charge');
      } else if (event.ctrlKey && event.key.toLowerCase() === 'p') {
        event.preventDefault();
        void askMessage();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [loggedIn, customerId, loginId]);

  useEffect(() => {
    if (!notice) return;
    const timeout = window.setTimeout(() => setNotice(''), 3000);
    return () => window.clearTimeout(timeout);
  }, [notice]);

  function notify(message: string) {
    setNotice(message);
  }

  async function signIn(guest = false) {
    if (guest) {
      if (password.trim()) { setLoginError('برای مهمان رمز را خالی بگذارید'); return; }
      setCustomerName('مهمان');
      setCustomerId(null);
      setLoginId(null);
      setWallet(0);
      setSessionEndAt(null);
      setSessionState(null);
    } else {
      if (!clientIdentityReady) {
        setLoginError('در حال شناسایی Agent این رایانه هستیم. چند لحظه بعد دوباره تلاش کنید.');
        return;
      }
      const id = customerCode.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).trim();
      if (!id || !password.trim()) {
        setLoginError('شناسه و رمز عبور مشتری را وارد کنید.');
        return;
      }

      try {
        const result = await authenticateCustomer(id, password, deviceId);
        setCustomerId(result.customerId);
        setLoginId(result.loginId);
        setCustomerName(result.fullName);
        setWallet(result.balance);
        setFreeMoney(result.freeMoney);
        setLoginLimit(result.limit);
        setActiveLoginCount(result.activeCount);
        setSessionEndAt(null);
        setSessionState(null);
        setSessionId(null);
        setActiveGame(null);
      } catch (error) {
        setLoginError(error instanceof Error ? error.message : 'ورود مشتری انجام نشد.');
        return;
      }
    }

    setLoginError('');
    setLoggedIn(true);
    setLocked(false);
    setPanel(null);
    setRemainingSeconds(0);
    notify(guest ? 'ورود مهمان انجام شد' : 'ورود مشتری از سرور تأیید شد');
  }

  async function logoutCustomer() {
    if (customerId && loginId) {
      try {
        await logoutAndLockClient(customerId, loginId);
        signOut(true);
      } catch (error) {
        notify(error instanceof Error ? error.message : 'خروج مشتری انجام نشد.');
      }
    } else {
      signOut();
    }
  }

  function signOut(skipServerRelease = false) {
    if (!skipServerRelease && customerId) {
      void releaseCustomerLogin(customerId, deviceId).catch(() => undefined);
    }
    setLoggedIn(false);
    setActiveGame(null);
    setSessionId(null);
    setFreeMoney(0);
    setLocked(false);
    setPanel(null);
    setCustomerCode('');
    setPassword('');
    setCustomerId(null);
    setLoginId(null);
    setCustomerName('مهمان');
    setWallet(0);
    setSessionEndAt(null);
    setSessionState(null);
    notify('جلسه مشتری بسته شد و ورود سروری آزاد شد');
  }

  async function askMessage() {
    if (!customerId || !loginId) {
      notify('برای ارسال پیام ابتدا وارد حساب مشتری شوید.');
      return;
    }
    const message = window.prompt('پیام برای اپراتور');
    if (!message?.trim()) return;
    try {
      await createClientRequest({ customerId, loginId, kind: 'message', message: message.trim() });
      notify('پیام برای اپراتور ارسال شد.');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'ارسال پیام انجام نشد.');
    }
  }

  async function requestOperator(kind: 'charge' | 'move' | 'unlock') {
    if (!customerId || !loginId) {
      notify('برای ثبت درخواست ابتدا وارد حساب مشتری شوید.');
      return;
    }
    try {
      await createClientRequest({ customerId, loginId, kind });
      notify('درخواست برای اپراتور ارسال شد.');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'ثبت درخواست انجام نشد.');
    }
  }

  async function requestBuffet(productId: string) {
    if (!customerId || !loginId) {
      notify('برای درخواست بوفه ابتدا وارد حساب مشتری شوید.');
      return;
    }
    try {
      await createClientRequest({ customerId, loginId, kind: 'buffet', productId, quantity: 1 });
      notify('درخواست بوفه برای اپراتور ارسال شد.');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'ثبت درخواست بوفه انجام نشد.');
    }
  }

  function runCommand(key: string) {
    setPaletteOpen(false);
    if (key === 'buffet') setPanel('buffet');
    else if (key === 'account') setPanel('account');
    else if (key === 'charge') void requestOperator('charge');
    else if (key === 'message') void askMessage();
    else if (key === 'advertisement') setAdVisible(value => !value);
  }

  async function launchGame(game: Game) {
    if (!customerId || !loginId || !sessionId) {
      notify('برای اجرای بازی ابتدا باید یک Session فعال روی همین رایانه داشته باشید.');
      return;
    }
    try {
      await launchClientGame({ customerId, loginId, sessionId, gameId: game.id });
      setActiveGame(game.id);
      notify(game.name + ' برای Agent ارسال شد و در حال اجراست.');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'اجرای بازی انجام نشد.');
    }
  }

  async function stopGame(gameId: string) {
    if (!customerId || !loginId || !sessionId) {
      notify('Session فعال برای توقف بازی پیدا نشد.');
      return;
    }
    try {
      await stopClientGame({ customerId, loginId, sessionId, gameId });
      setActiveGame(null);
      notify('فرمان توقف بازی برای Client ارسال شد.');
    } catch (error) {
      notify(error instanceof Error ? error.message : 'توقف بازی انجام نشد.');
    }
  }

  async function doContextAction(action: string) {
    setContext(null);
    if (action === 'move') await requestOperator('move');
    else if (action === 'login') {
      if (loggedIn) await logoutCustomer();
      else document.getElementById('client-login-id')?.focus();
    }
    else if (action === 'charge') await requestOperator('charge');
    else if (action === 'message') await askMessage();
    else if (action === 'lock') {
      if (!customerId || !loginId) {
        notify('برای قفل کردن، ابتدا وارد حساب مشتری شوید.');
        return;
      }
      try {
        await lockClient(customerId, loginId);
        setLocked(true);
        setActiveGame(null);
        notify('فرمان قفل برای Agent ارسال شد.');
      } catch (error) {
        notify(error instanceof Error ? error.message : 'قفل کردن سیستم انجام نشد.');
      }
    }
    else if (action === 'logout') await logoutCustomer();
    else if (action === 'stop-game' && activeGame) await stopGame(activeGame);
  }

  const hours = Math.floor(remainingSeconds / 3600);
  const minutes = Math.floor(remainingSeconds % 3600 / 60);
  const seconds = remainingSeconds % 60;
  const timerClass = sessionEndAt
    ? remainingSeconds < 300 ? 'critical' : remainingSeconds < 900 ? 'warning' : ''
    : '';
  const sessionLocked = locked || Boolean(loggedIn && sessionEndAt && remainingSeconds === 0);
  const recentGames = games;
  const gameSize = Math.round(190 * zoom / 100);

  return <div className="client-app" dir="rtl" onClick={() => { if (context) setContext(null); }} onContextMenu={event => { if ((event.target as HTMLElement).closest('.client-game-card')) { event.preventDefault(); setContext({ x: Math.min(event.clientX, window.innerWidth - 260), y: Math.min(event.clientY, window.innerHeight - 300) }); } }}>
    <header className="client-topbar">
      <div className="client-logo">گ</div><div className="client-system"><b>{stationName}</b> — گیم‌نت منیجر</div><div className="client-spacer" />
      {loggedIn && <><div className="client-pill balance">👛 مانده: <b>{money(wallet)}</b> ت</div><div className="client-pill"><span className={`client-timer ${timerClass}`}>⏱ {sessionEndAt ? String(hours).padStart(2, '۰') + ':' + String(minutes).padStart(2, '۰') + ':' + String(seconds).padStart(2, '۰') : sessionState === 'Active' ? 'جلسه فعال' : 'در انتظار شروع جلسه'}</span></div><div className="client-segment" aria-label="نوع نمایش بازی‌ها">{(['card', 'compact', 'list'] as ViewMode[]).map((mode, index) => <button key={mode} className={view === mode ? 'active' : ''} title={['کارتی', 'فشرده', 'لیستی'][index]} onClick={() => setView(mode)}>{['▦', '▤', '☰'][index]}</button>)}</div><div className="client-zoom"><button onClick={() => setZoom(value => Math.max(70, value - 10))}>−</button><span>{money(zoom)}٪</span><button onClick={() => setZoom(value => Math.min(130, value + 10))}>＋</button></div><button className="client-user-pill" onClick={() => setPanel(panel === 'account' ? null : 'account')}><span className="client-avatar">{customerName.slice(0, 1)}</span>{customerName}</button></>}
      {!loggedIn && <span className="client-offline-pill">● متصل به GameNet</span>}
    </header>

    {!loggedIn ? <main className="client-login-stage"><section className="client-login-panel"><div className="client-login-logo">گ</div><h1>گیم‌نت منیجر</h1><p>برای شروع بازی وارد حساب خود شوید</p><form onSubmit={event => { event.preventDefault(); signIn(); }}><input id="client-login-id" autoFocus value={customerCode} onChange={event => setCustomerCode(event.target.value)} placeholder="کد کاربری — مثلاً ۱۰۵۰" /><input type="password" value={password} onChange={event => setPassword(event.target.value)} placeholder="رمز عبور (برای مهمان خالی بگذار)" /><div className="client-login-error">{loginError}</div><button className="client-login-submit" disabled={!clientIdentityReady}>ورود به سیستم</button></form><button className="client-guest" onClick={() => signIn(true)}>ورود مهمان</button><div className="client-login-separator" /><p className="client-login-footnote">کنترل‌های مدیریتی و آزادسازی سیستم فقط از طریق Agent و Dashboard انجام می‌شوند.</p></section><span className="client-login-foot">ورود با شناسه و رمز واقعی مشتری · دسترسی مهمان بدون حساب</span></main> : <>
      {sessionLocked ? <main className="client-lock-screen"><div className="client-lock-icon">🔒</div><h1>سیستم قفل است</h1><p>برای ادامه، به اپراتور مراجعه کنید.</p><button className="client-button primary" onClick={() => void requestOperator('unlock')}>درخواست بازگشایی</button></main> : <main className="client-desktop">
        <div className="client-toolbar"><span>بازی‌های در دسترس</span><div className="client-spacer" /><span className="client-network">● متصل به GameNet</span></div>
        <div className={`client-game-grid ${view}`} style={{ '--game-size': `${gameSize}px`, '--game-zoom': zoom / 100 } as React.CSSProperties}>
          {recentGames.map(game => <button key={game.id} className={`client-game-card ${view} ${activeGame === game.id ? 'running' : ''}`} onClick={() => void launchGame(game)} onContextMenu={event => { event.preventDefault(); setContext({ x: Math.min(event.clientX, window.innerWidth - 260), y: Math.min(event.clientY, window.innerHeight - 300) }); }}>
            <span className="client-game-art">{game.icon}</span><span className="client-game-info"><b>{game.name}</b><small>{game.category}</small><small>{game.description}</small></span>{activeGame === game.id && <span className="client-running-badge">در حال اجرا</span>}
          </button>)}
        </div>
        <aside className="client-side-column">{activeGame && <section className="client-preview"><div className="client-preview-art">{games.find(game => game.id === activeGame)?.icon}</div><div className="client-preview-body"><b>{games.find(game => game.id === activeGame)?.name}</b><p>بازی فعال · اکانت تخصیص‌یافته در زمان توقف آزاد می‌شود.</p><button className="client-button" onClick={() => doContextAction('stop-game')}>■ توقف بازی</button></div></section>}{adVisible && <section className="client-ad"><button className="client-ad-close" onClick={() => setAdVisible(false)} aria-label="بستن تبلیغ">×</button><div className="client-ad-media">🏆</div><div className="client-ad-body"><b>پکیج VIP GameNet</b><p>تخفیف بازی و زمان بیشتر برای اعضای VIP</p><button onClick={() => setAdVisible(false)}>بعداً</button></div></section>}</aside>
      </main>}

      <nav className="client-dock">
        <button className="hot" onClick={() => { setPanel(null); setPaletteOpen(true); setPaletteQuery(''); }}>⌘ قلنک <kbd>Ctrl+K</kbd></button>
        <button onClick={() => setPanel(panel === 'buffet' ? null : 'buffet')}>🛒 منوی بوفه</button>
        <button onClick={() => setPanel(panel === 'account' ? null : 'account')}>👤 حساب من</button>
        <button onClick={() => void requestOperator('charge')}>💳 درخواست شارژ</button>
        <button onClick={() => void askMessage()}>💬 پیام به اپراتور</button>
        <button onClick={() => setAdVisible(value => !value)}>📢 تبلیغات</button>
      </nav>    </>}

    {panel && <section className="client-panel"><header><b>{panel === 'buffet' ? 'منوی بوفه' : 'حساب من'}</b><button onClick={() => setPanel(null)}>×</button></header>{panel === 'buffet' ? <div className="client-buffet-list">{buffetItems.map(item => <div key={item.id}><span>{item.icon} {item.name}</span><b>{money(item.price)} ت</b><button onClick={() => void requestBuffet(item.id)}>+</button></div>)}</div> : <div className="client-account-info"><div><span>نام کاربری</span><b>{customerCode || 'مهمان'}</b></div><div><span>کیف پول</span><b>{money(wallet)} تومان</b></div><div><span>اعتبار رایگان</span><b>{money(freeMoney)} تومان</b></div><div><span>ورود هم‌زمان</span><b>{activeLoginCount.toLocaleString('fa-IR')} / {loginLimit.toLocaleString('fa-IR')}</b></div><button className="client-button" onClick={() => void logoutCustomer()}>خروج مشتری و بستن وقت</button></div>}</section>}

    {context && <section className="client-context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}><strong>{activeGame ? 'بازی فعال' : 'ابزار کلاینت'}</strong><button onClick={() => void doContextAction('move')}>🔀 جابه‌جایی شناسه به سیستم دیگر <kbd>Ctrl+M</kbd></button><button onClick={() => void doContextAction('login')}>🔄 ورود / خروج با شناسه <kbd>Ctrl+L</kbd></button><button onClick={() => void doContextAction('charge')}>💰 درخواست شارژ از اپراتور <kbd>Ctrl+R</kbd></button><button onClick={() => void doContextAction('message')}>💬 ارسال پیام به اپراتور <kbd>Ctrl+P</kbd></button>{activeGame && <button onClick={() => void doContextAction('stop-game')}>■ توقف بازی</button>}<button onClick={() => void doContextAction('lock')}>🔒 قفل کردن سیستم</button><button onClick={() => void doContextAction('logout')}>🚪 خروج مشتری و بستن وقت</button></section>}

    {paletteOpen && <div className="client-overlay" onMouseDown={event => event.target === event.currentTarget && setPaletteOpen(false)}><section className="client-palette" role="dialog" aria-modal="true"><input autoFocus value={paletteQuery} onChange={event => setPaletteQuery(event.target.value)} onKeyDown={event => event.key === 'Enter' && visibleCommands[0] && runCommand(visibleCommands[0].key)} placeholder="جست‌وجوی فرمان یا صفحه…" /><div>{visibleCommands.map(item => <button key={item.key} onClick={() => runCommand(item.key)}><span>{item.icon} {item.title}</span><kbd>Enter</kbd></button>)}</div><small>Ctrl+K باز کردن · Esc بستن</small></section></div>}

    {notice && <div className="client-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    <span className="client-clock" aria-hidden="true">{new Date(now).toLocaleTimeString('fa-IR')}</span>
  </div>;
}
