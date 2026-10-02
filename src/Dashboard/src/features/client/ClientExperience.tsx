import { useEffect, useMemo, useRef, useState } from 'react';
import './ClientExperience.css';
import { mockService } from '../../services/mockService';
import { authenticateCustomer, readCustomerState, releaseCustomerLogin } from '../../services/customerAuthService';
import { getClientIdentity } from '../../services/clientIdentityService';

type Game = { id: string; name: string; category: string; icon: string; requiresAccount: boolean; description: string };
type ContextMenu = { x: number; y: number } | null;
type Panel = 'apps' | 'buffet' | 'account' | 'operator' | null;
type ViewMode = 'card' | 'compact' | 'list';

const games: Game[] = [
  { id: 'cs2', name: 'Counter-Strike 2', category: 'FPS · آنلاین', icon: '🎯', requiresAccount: true, description: 'رقابت تیمی و بازی رتبه‌ای' },
  { id: 'valorant', name: 'Valorant', category: 'FPS · آنلاین', icon: '⚡', requiresAccount: true, description: 'نبرد تاکتیکی ۵ در برابر ۵' },
  { id: 'fc25', name: 'EA SPORTS FC 25', category: 'ورزشی', icon: '⚽', requiresAccount: false, description: 'مسابقه فوتبال دونفره' },
  { id: 'fortnite', name: 'Fortnite', category: 'Battle Royale', icon: '🪂', requiresAccount: true, description: 'بازی گروهی و رقابتی' },
  { id: 'minecraft', name: 'Minecraft', category: 'ماجراجویی', icon: '🧱', requiresAccount: false, description: 'جهان باز و ساخت‌وساز' },
  { id: 'rocket', name: 'Rocket League', category: 'ورزشی', icon: '🚗', requiresAccount: true, description: 'فوتبال با ماشین‌های راکتی' },
];

const buffetItems = [
  { name: 'نوشابه', price: 35000, icon: '🥤' },
  { name: 'قهوه فوری', price: 45000, icon: '☕' },
  { name: 'پاپ‌کورن', price: 35000, icon: '🍿' },
  { name: 'ساندویچ سرد', price: 90000, icon: '🥪' },
];

const clientApps = [
  { icon: '🌐', name: 'Chrome', description: 'مرورگر مجاز' },
  { icon: '💬', name: 'Discord', description: 'گفت‌وگوی تیمی' },
  { icon: '✈️', name: 'Telegram', description: 'پیام‌رسان' },
  { icon: '🎵', name: 'موزیک', description: 'پخش موسیقی' },
  { icon: '🎥', name: 'OBS', description: 'ضبط و پخش' },
  { icon: '🧮', name: 'ماشین حساب', description: 'ابزار عمومی' },
];

const commands = [
  { title: 'نرم‌افزارها', key: 'apps', icon: '🧩' },
  { title: 'منوی بوفه', key: 'buffet', icon: '🛒' },
  { title: 'مسابقات', key: 'tournaments', icon: '🏆' },
  { title: 'بازی‌های من', key: 'my-games', icon: '🎮' },
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
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [loginId, setLoginId] = useState<string | null>(null);
  const [loginLimit, setLoginLimit] = useState(1);
  const [activeLoginCount, setActiveLoginCount] = useState(0);
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
  const [remainingSeconds, setRemainingSeconds] = useState(3 * 3600 + 45 * 60 + 12);
  const [now, setNow] = useState(Date.now());
  const [view, setView] = useState<ViewMode>('card');
  const [zoom, setZoom] = useState(100);
  const [activeGame, setActiveGame] = useState<string | null>(null);
  const [accountGame, setAccountGame] = useState<Game | null>(null);
  const [panel, setPanel] = useState<Panel>(null);
  const [dockHoverPanel, setDockHoverPanel] = useState<'apps' | null>(null);
  const dockHoverTimer = useRef<number | null>(null);
  const [context, setContext] = useState<ContextMenu>(null);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [paletteQuery, setPaletteQuery] = useState('');
  const [notice, setNotice] = useState('');
  const [adVisible, setAdVisible] = useState(true);
  const [locked, setLocked] = useState(false);
  const [myGamesOnly, setMyGamesOnly] = useState(false);

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
        setSessionEndAt(state.session?.endAt ?? null);
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
        event.preventDefault(); setPaletteOpen(value => !value); setPaletteQuery('');
      } else if (event.key === 'Escape') {
        setPaletteOpen(false); setContext(null); setPanel(null); setDockHoverPanel(null); setAccountGame(null);

      } else if (event.ctrlKey && event.key.toLowerCase() === 'm') {
        event.preventDefault(); notify('درخواست جابه‌جایی برای اپراتور ارسال شد');
      } else if (event.ctrlKey && event.key.toLowerCase() === 'l') {
        event.preventDefault();
        if (loggedIn) signOut();
        else document.getElementById('client-login-id')?.focus();
      } else if (event.ctrlKey && event.key.toLowerCase() === 'r') {
        event.preventDefault(); notify('درخواست شارژ برای اپراتور ارسال شد');
      } else if (event.ctrlKey && event.key.toLowerCase() === 'p') {
        event.preventDefault(); askMessage();

      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [loggedIn]);

  useEffect(() => {
    if (!notice) return;
    const timeout = window.setTimeout(() => setNotice(''), 3000);
    return () => window.clearTimeout(timeout);
  }, [notice]);

  function notify(message: string) {
    setNotice(message);
  }

  function scheduleDockHover(target: 'apps' | null) {
    if (dockHoverTimer.current !== null) window.clearTimeout(dockHoverTimer.current);
    const delay = target ? 220 : 160;
    dockHoverTimer.current = window.setTimeout(() => setDockHoverPanel(target), delay);
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
        setLoginLimit(result.limit);
        setActiveLoginCount(result.activeCount);
        setSessionEndAt(null);
        setSessionState(null);
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

  function signOut(skipServerRelease = false) {
    if (!skipServerRelease && customerId) {
      void releaseCustomerLogin(customerId, deviceId).catch(() => undefined);
    }
    setLoggedIn(false);
    setActiveGame(null);
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

  function askMessage() {
    const message = window.prompt('پیام برای اپراتور');
    if (message?.trim()) notify('پیام برای اپراتور ارسال شد');
  }

  function runCommand(key: string) {
    setPaletteOpen(false);
    if (key === 'apps') { setPanel('apps'); setDockHoverPanel('apps'); }
    else if (key === 'buffet') setPanel('buffet');
    else if (key === 'account') setPanel('account');
    else if (key === 'my-games') { setMyGamesOnly(true); notify('بازی‌های اخیراً اجراشده نمایش داده شدند'); }
    else if (key === 'tournaments') notify('مسابقات دمو: جام CS2 · ثبت‌نام از صندوق');
    else if (key === 'charge') notify('درخواست شارژ برای اپراتور ارسال شد');
    else if (key === 'message') askMessage();
    else if (key === 'advertisement') { setAdVisible(value => !value); notify('وضعیت تبلیغ تغییر کرد'); }
  }

  function launchGame(game: Game, accountMode?: 'own' | 'pool') {
    if (game.requiresAccount && !accountMode) { setAccountGame(game); return; }
    setAccountGame(null); setActiveGame(game.id); setMyGamesOnly(false);
    notify(accountMode === 'pool' ? `اکانت GameNet برای ${game.name} تخصیص یافت` : `${game.name} اجرا شد`);
  }

  function doContextAction(action: string) {
    setContext(null);
    if (action === 'move') notify('درخواست جابه‌جایی شناسه به اپراتور ارسال شد');
    else if (action === 'login') {
      if (loggedIn) signOut();
      else document.getElementById('client-login-id')?.focus();
    }
    else if (action === 'charge') notify('درخواست شارژ برای اپراتور ارسال شد');
    else if (action === 'message') askMessage();
    else if (action === 'lock') { setLocked(true); setActiveGame(null); notify('سیستم قفل شد'); }
    else if (action === 'logout') signOut();
    else if (action === 'stop-game') { setActiveGame(null); notify('بازی متوقف شد و اکانت آزاد شد'); }
  }

  const hours = Math.floor(remainingSeconds / 3600);
  const minutes = Math.floor(remainingSeconds % 3600 / 60);
  const seconds = remainingSeconds % 60;
  const timerClass = sessionEndAt
    ? remainingSeconds < 300 ? 'critical' : remainingSeconds < 900 ? 'warning' : ''
    : '';
  const sessionLocked = locked || Boolean(loggedIn && sessionEndAt && remainingSeconds === 0);
  const recentGames = myGamesOnly ? games.filter(game => ['cs2', 'fc25'].includes(game.id)) : games;
  const gameSize = Math.round(190 * zoom / 100);

  return <div className="client-app" dir="rtl" onClick={() => { if (context) setContext(null); }} onContextMenu={event => { if ((event.target as HTMLElement).closest('.client-game-card')) { event.preventDefault(); setContext({ x: Math.min(event.clientX, window.innerWidth - 260), y: Math.min(event.clientY, window.innerHeight - 300) }); } }}>
    <header className="client-topbar">
      <div className="client-logo">گ</div><div className="client-system"><b>PC ۱۲</b> — گیم‌نت منیجر</div><div className="client-spacer" />
      {loggedIn && <><div className="client-pill balance">👛 مانده: <b>{money(wallet)}</b> ت</div><div className="client-pill"><span className={`client-timer ${timerClass}`}>⏱ {sessionEndAt ? String(hours).padStart(2, '۰') + ':' + String(minutes).padStart(2, '۰') + ':' + String(seconds).padStart(2, '۰') : sessionState === 'Active' ? 'جلسه فعال' : 'در انتظار شروع جلسه'}</span></div><div className="client-segment" aria-label="نوع نمایش بازی‌ها">{(['card', 'compact', 'list'] as ViewMode[]).map((mode, index) => <button key={mode} className={view === mode ? 'active' : ''} title={['کارتی', 'فشرده', 'لیستی'][index]} onClick={() => setView(mode)}>{['▦', '▤', '☰'][index]}</button>)}</div><div className="client-zoom"><button onClick={() => setZoom(value => Math.max(70, value - 10))}>−</button><span>{money(zoom)}٪</span><button onClick={() => setZoom(value => Math.min(130, value + 10))}>＋</button></div><button className="client-user-pill" onClick={() => setPanel(panel === 'account' ? null : 'account')}><span className="client-avatar">{customerName.slice(0, 1)}</span>{customerName}</button></>}
      {!loggedIn && <span className="client-offline-pill">● متصل به GameNet</span>}
    </header>

    {!loggedIn ? <main className="client-login-stage"><section className="client-login-panel"><div className="client-login-logo">گ</div><h1>گیم‌نت منیجر</h1><p>برای شروع بازی وارد حساب خود شوید</p><form onSubmit={event => { event.preventDefault(); signIn(); }}><input id="client-login-id" autoFocus value={customerCode} onChange={event => setCustomerCode(event.target.value)} placeholder="کد کاربری — مثلاً ۱۰۵۰" /><input type="password" value={password} onChange={event => setPassword(event.target.value)} placeholder="رمز عبور (برای مهمان خالی بگذار)" /><div className="client-login-error">{loginError}</div><button className="client-login-submit" disabled={!clientIdentityReady}>ورود به سیستم</button></form><button className="client-guest" onClick={() => signIn(true)}>ورود مهمان</button><div className="client-login-separator" /><p className="client-login-footnote">کنترل‌های مدیریتی و آزادسازی سیستم فقط از طریق Agent و Dashboard انجام می‌شوند.</p></section><span className="client-login-foot">ورود با شناسه و رمز واقعی مشتری · دسترسی مهمان بدون حساب</span></main> : <>
      {sessionLocked ? <main className="client-lock-screen"><div className="client-lock-icon">🔒</div><h1>سیستم قفل است</h1><p>برای ادامه، به اپراتور مراجعه کنید.</p><button className="client-button primary" onClick={() => notify('درخواست بازکردن قفل برای اپراتور ارسال شد')}>درخواست بازگشایی</button></main> : <main className="client-desktop">
        <div className="client-toolbar"><span>{myGamesOnly ? 'بازی‌های من' : 'بازی‌های در دسترس'}</span><div className="client-spacer" /><span className={`client-network ${internet ? '' : 'offline'}`}>● {internet ? 'Online' : 'Offline / LAN'}</span></div>
        <div className={`client-game-grid ${view}`} style={{ '--game-size': `${gameSize}px`, '--game-zoom': zoom / 100 } as React.CSSProperties}>
          {recentGames.map(game => <button key={game.id} className={`client-game-card ${view} ${activeGame === game.id ? 'running' : ''}`} onClick={() => launchGame(game, game.requiresAccount ? undefined : 'own')} onContextMenu={event => { event.preventDefault(); setContext({ x: Math.min(event.clientX, window.innerWidth - 260), y: Math.min(event.clientY, window.innerHeight - 300) }); }}>
            <span className="client-game-art">{game.icon}</span><span className="client-game-info"><b>{game.name}</b><small>{game.category}</small><small>{game.description}</small></span>{activeGame === game.id && <span className="client-running-badge">در حال اجرا</span>}
          </button>)}
        </div>
        <aside className="client-side-column">{activeGame && <section className="client-preview"><div className="client-preview-art">{games.find(game => game.id === activeGame)?.icon}</div><div className="client-preview-body"><b>{games.find(game => game.id === activeGame)?.name}</b><p>بازی فعال · اکانت تخصیص‌یافته در زمان توقف آزاد می‌شود.</p><button className="client-button" onClick={() => doContextAction('stop-game')}>■ توقف بازی</button></div></section>}{adVisible && <section className="client-ad"><button className="client-ad-close" onClick={() => setAdVisible(false)} aria-label="بستن تبلیغ">×</button><div className="client-ad-media">🏆</div><div className="client-ad-body"><b>پکیج VIP GameNet</b><p>تخفیف بازی و زمان بیشتر برای اعضای VIP</p><button onClick={() => notify('درخواست فعال‌سازی پکیج VIP ثبت شد')}>درخواست فعال‌سازی</button></div></section>}</aside>
      </main>}

      <nav className="client-dock">
        <button className="hot" onClick={() => { setDockHoverPanel(null); setPanel(null); setPaletteOpen(true); setPaletteQuery(''); }}>⌘ قلنک <kbd>Ctrl+K</kbd></button>

        <div className="client-dock-item client-dock-apps"
          onMouseEnter={() => scheduleDockHover('apps')}
          onMouseLeave={() => scheduleDockHover(null)}>
          <button
            aria-expanded={dockHoverPanel === 'apps' || panel === 'apps'}
            onClick={() => {
              const open = panel !== 'apps';
              setPanel(open ? 'apps' : null);
              setDockHoverPanel(open ? 'apps' : null);
            }}
          >🧩 نرم‌افزارها</button>

          {(dockHoverPanel === 'apps' || panel === 'apps') && (
            <section
              className="client-dock-flyout"
              onMouseEnter={() => scheduleDockHover('apps')}
              onMouseLeave={() => scheduleDockHover(null)}
              onClick={event => event.stopPropagation()}
            >
              <header>
                <div>
                  <b>نرم‌افزارهای مجاز</b>
                  <small>اجرا فقط از فهرست تأییدشده کلاینت</small>
                </div>
                <span>۶ مورد</span>
              </header>
              <div className="client-dock-app-grid">
                {clientApps.map(app => (
                  <button
                    key={app.name}
                    className="client-dock-app"
                    onClick={() => notify(`درخواست اجرای ${app.name} برای Agent ثبت شد`)}
                  >
                    <span className="client-dock-app-icon">{app.icon}</span>
                    <span><b>{app.name}</b><small>{app.description}</small></span>
                  </button>
                ))}
              </div>
            </section>
          )}
        </div>

        <button onClick={() => { setDockHoverPanel(null); setPanel(panel === 'buffet' ? null : 'buffet'); }}>🛒 منوی بوفه</button>
        <button onClick={() => notify('مسابقات دمو: جام CS2 · ثبت‌نام از صندوق')}>🏆 مسابقات</button>
        <button onClick={() => { setMyGamesOnly(true); setPanel(null); setDockHoverPanel(null); }}>🎮 بازی‌های من</button>
        <button onClick={() => { setDockHoverPanel(null); setPanel(panel === 'account' ? null : 'account'); }}>👤 حساب من</button>
        <button className="dock-spacer" onClick={() => notify('کنترل‌های اپراتور فقط از Dashboard مدیریت انجام می‌شوند.')}>🛡️ کنترل اپراتور</button>
        <button onClick={() => setAdVisible(value => !value)}>📢 تبلیغات</button>
      </nav>
    </>}

    {panel && panel !== 'operator' && panel !== 'apps' && <section className="client-panel"><header><b>{panel === 'buffet' ? 'منوی بوفه' : 'حساب من'}</b><button onClick={() => setPanel(null)}>×</button></header>{panel === 'buffet' ? <div className="client-buffet-list">{buffetItems.map(item => <div key={item.name}><span>{item.icon} {item.name}</span><b>{money(item.price)} ت</b><button onClick={() => notify(`درخواست ${item.name} برای اپراتور ثبت شد`)}>+</button></div>)}</div> : <div className="client-account-info"><div><span>نام کاربری</span><b>{customerCode || 'مهمان'}</b></div><div><span>کیف پول</span><b>{money(wallet)} تومان</b></div><div><span>اعتبار رایگان</span><b>۱۲۰٬۰۰۰ تومان</b></div><div><span>بدهی</span><b>۰ تومان</b></div><div><span>ورود هم‌زمان</span><b>{activeLoginCount.toLocaleString('fa-IR')} / {loginLimit.toLocaleString('fa-IR')}</b></div><button className="client-button" onClick={signOut}>خروج مشتری</button></div>}</section>}

    {context && <section className="client-context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}><strong>{activeGame ? `بازی: ${games.find(game => game.id === activeGame)?.name}` : 'ابزار کلاینت'}</strong><button onClick={() => doContextAction('move')}>🔀 جابه‌جایی شناسه به سیستم دیگر <kbd>Ctrl+M</kbd></button><button onClick={() => doContextAction('login')}>🔄 ورود / خروج با شناسه <kbd>Ctrl+L</kbd></button><button onClick={() => doContextAction('charge')}>💰 درخواست شارژ از اپراتور <kbd>Ctrl+R</kbd></button><button onClick={() => doContextAction('message')}>💬 ارسال پیام به اپراتور <kbd>Ctrl+P</kbd></button>{activeGame && <button onClick={() => doContextAction('stop-game')}>■ توقف بازی</button>}<button onClick={() => doContextAction('lock')}>🔒 قفل کردن سیستم <kbd>Win+L</kbd></button><button onClick={() => doContextAction('logout')}>🚪 خروج مشتری و بستن وقت</button></section>}

    {paletteOpen && <div className="client-overlay" onMouseDown={event => event.target === event.currentTarget && setPaletteOpen(false)}><section className="client-palette" role="dialog" aria-modal="true"><input autoFocus value={paletteQuery} onChange={event => setPaletteQuery(event.target.value)} onKeyDown={event => event.key === 'Enter' && visibleCommands[0] && runCommand(visibleCommands[0].key)} placeholder="جست‌وجوی فرمان یا صفحه…" /><div>{visibleCommands.map(item => <button key={item.key} onClick={() => runCommand(item.key)}><span>{item.icon} {item.title}</span><kbd>Enter</kbd></button>)}</div><small>Ctrl+K باز کردن · Esc بستن</small></section></div>}

    {accountGame && <div className="client-overlay" onMouseDown={event => event.target === event.currentTarget && setAccountGame(null)}><section className="client-account-picker" role="dialog" aria-modal="true"><header><div><b>{accountGame.icon} {accountGame.name}</b><small>روش ورود به بازی را انتخاب کنید</small></div><button onClick={() => setAccountGame(null)}>×</button></header><button className="account-option" onClick={() => launchGame(accountGame, 'own')}><span>👤</span><div><b>اکانت خودم</b><small>با حساب شخصی خود وارد شوید</small></div></button><button className="account-option pool" onClick={() => launchGame(accountGame, 'pool')}><span>🎮</span><div><b>اکانت GameNet</b><small>از Account Pool سرور تخصیص داده شود</small></div></button><p>اطلاعات ورود اکانت‌های مجموعه به مشتری نمایش داده نمی‌شود.</p></section></div>}

    {notice && <div className="client-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    <span className="client-clock" aria-hidden="true">{new Date(now).toLocaleTimeString('fa-IR')}</span>
  </div>;
}
