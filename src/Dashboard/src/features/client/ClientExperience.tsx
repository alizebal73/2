import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import './ClientExperience.css';
import { mockService } from '../../services/mockService';

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
  const [loginLimit, setLoginLimit] = useState(1);
  const [activeLoginCount, setActiveLoginCount] = useState(0);
  const deviceId = useMemo(() => {
    const key = 'gamenet-client-device-id';
    const saved = localStorage.getItem(key);
    if (saved) return saved;
    const created = crypto.randomUUID();
    localStorage.setItem(key, created);
    return created;
  }, []);
  const wallet = 450000;
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
  const [adminDialog, setAdminDialog] = useState<'operator' | 'release' | null>(null);
  const [pin, setPin] = useState('');
  const [locked, setLocked] = useState(false);
  const [internet, setInternet] = useState(true);
  const [myGamesOnly, setMyGamesOnly] = useState(false);

  const submitAdminAction = useCallback(() => {
    if (!pin.trim()) { notify('PIN یا رمز مدیر را وارد کنید'); return; }
    if (adminDialog === 'operator') {
      if (pin !== '2468') { notify('PIN اپراتور دمو نادرست است'); return; }
      setCustomerName('اپراتور'); setLoggedIn(true); setRemainingSeconds(24 * 3600); setLocked(false);
      notify('ورود اپراتور با زمان نامحدود انجام شد');
    } else {
      if (pin !== '2020') { notify('رمز مدیر دمو نادرست است'); return; }
      setLoggedIn(false); setActiveGame(null); setLocked(false);
      notify('آزادسازی سیستم تأیید شد؛ Restart/Shutdown برای Agent واقعی باقی است');
    }
    setAdminDialog(null); setPin('');
  }, [adminDialog, pin]);

  const visibleCommands = useMemo(() => commands.filter(item => item.title.includes(paletteQuery.trim())), [paletteQuery]);

  useEffect(() => {
    const interval = window.setInterval(() => {
      setNow(Date.now());
      if (loggedIn && !locked) setRemainingSeconds(value => Math.max(0, value - 1));
    }, 1000);
    return () => window.clearInterval(interval);
  }, [loggedIn, locked]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault(); setPaletteOpen(value => !value); setPaletteQuery('');
      } else if (event.key === 'Escape') {
        setPaletteOpen(false); setContext(null); setPanel(null); setDockHoverPanel(null); setAdminDialog(null); setAccountGame(null);
      } else if (event.altKey && event.key === 'F6') {
        event.preventDefault(); setAdminDialog('operator');
      } else if (event.altKey && event.key === 'F7') {
        event.preventDefault(); setAdminDialog('release');
      } else if (event.ctrlKey && event.key.toLowerCase() === 'i') {
        event.preventDefault(); setInternet(value => !value); notify('وضعیت اینترنت تغییر کرد');
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
      } else if (event.key === 'Enter' && adminDialog) {
        event.preventDefault(); submitAdminAction();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [adminDialog, loggedIn, submitAdminAction]);

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
    } else {
      const id = customerCode.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).trim();
      if (id !== '1050' || password.trim() !== '2020') {
        setLoginError('در نسخه دمو، شناسه ۱۰۵۰ و رمز ۲۰۲۰ است');
        return;
      }
      const state = await mockService.getCustomerLoginState('c1', 1);
      setLoginLimit(state.limit);
      setActiveLoginCount(state.active);
      const acquired = await mockService.acquireCustomerLogin('c1', deviceId, 1);
      if (!acquired) {
        setLoginError('این مشتری در حال حاضر به سقف ورود هم‌زمان رسیده است. ابتدا از دستگاه دیگر خارج شوید.');
        return;
      }
      setCustomerName('رضا محمدی');
      setActiveLoginCount(state.active + 1);
    }
    setLoginError('');
    setLoggedIn(true);
    setLocked(false);
    setPanel(null);
    setRemainingSeconds(3 * 3600 + 45 * 60 + 12);
    notify(guest ? 'ورود مهمان انجام شد' : 'ورود موفق بود');
  }

  function signOut() {
    void mockService.releaseCustomerLogin('c1', deviceId);
    setLoggedIn(false); setActiveGame(null); setLocked(false); setPanel(null);
    setCustomerCode(''); setPassword('');
    notify('جلسه مشتری بسته شد و حساب‌ها آزاد شدند');
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
    if (action === 'internet') { setInternet(value => !value); notify(internet ? 'حالت Offline / LAN فعال شد' : 'حالت Online فعال شد'); }
    else if (action === 'move') notify('درخواست جابه‌جایی شناسه به اپراتور ارسال شد');
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
  const timerClass = remainingSeconds < 300 ? 'critical' : remainingSeconds < 900 ? 'warning' : '';
  const sessionLocked = locked || (loggedIn && remainingSeconds === 0);
  const recentGames = myGamesOnly ? games.filter(game => ['cs2', 'fc25'].includes(game.id)) : games;
  const gameSize = Math.round(190 * zoom / 100);

  return <div className="client-app" dir="rtl" onClick={() => { if (context) setContext(null); }} onContextMenu={event => { if ((event.target as HTMLElement).closest('.client-game-card')) { event.preventDefault(); setContext({ x: Math.min(event.clientX, window.innerWidth - 260), y: Math.min(event.clientY, window.innerHeight - 300) }); } }}>
    <header className="client-topbar">
      <div className="client-logo">گ</div><div className="client-system"><b>PC ۱۲</b> — گیم‌نت منیجر</div><div className="client-spacer" />
      {loggedIn && <><div className="client-pill balance">👛 مانده: <b>{money(wallet)}</b> ت</div><div className="client-pill"><span className={`client-timer ${timerClass}`}>⏱ {String(hours).padStart(2, '۰')}:{String(minutes).padStart(2, '۰')}:{String(seconds).padStart(2, '۰')}</span></div><div className="client-segment" aria-label="نوع نمایش بازی‌ها">{(['card', 'compact', 'list'] as ViewMode[]).map((mode, index) => <button key={mode} className={view === mode ? 'active' : ''} title={['کارتی', 'فشرده', 'لیستی'][index]} onClick={() => setView(mode)}>{['▦', '▤', '☰'][index]}</button>)}</div><div className="client-zoom"><button onClick={() => setZoom(value => Math.max(70, value - 10))}>−</button><span>{money(zoom)}٪</span><button onClick={() => setZoom(value => Math.min(130, value + 10))}>＋</button></div><button className="client-user-pill" onClick={() => setPanel(panel === 'account' ? null : 'account')}><span className="client-avatar">{customerName.slice(0, 1)}</span>{customerName}</button></>}
      {!loggedIn && <span className="client-offline-pill">● {internet ? 'آنلاین' : 'آفلاین / LAN'}</span>}
    </header>

    {!loggedIn ? <main className="client-login-stage"><section className="client-login-panel"><div className="client-login-logo">گ</div><h1>گیم‌نت منیجر</h1><p>برای شروع بازی وارد حساب خود شوید</p><form onSubmit={event => { event.preventDefault(); signIn(); }}><input id="client-login-id" autoFocus value={customerCode} onChange={event => setCustomerCode(event.target.value)} placeholder="کد کاربری — مثلاً ۱۰۵۰" /><input type="password" value={password} onChange={event => setPassword(event.target.value)} placeholder="رمز عبور (برای مهمان خالی بگذار)" /><div className="client-login-error">{loginError}</div><button className="client-login-submit">ورود به سیستم</button></form><button className="client-guest" onClick={() => signIn(true)}>ورود مهمان</button><div className="client-login-separator" /><button className="client-operator" onClick={() => setAdminDialog('operator')}>🧑‍💼 ورود اپراتور با زمان نامحدود <kbd>Alt+F6</kbd></button><button className="client-release" onClick={() => setAdminDialog('release')}>🔓 آزادسازی سیستم / ورود به Windows <kbd>Alt+F7</kbd></button></section><span className="client-login-foot">شناسه نمونه: ۱۰۵۰ · دسترسی مهمان بدون حساب</span></main> : <>
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
        <button className="dock-spacer" onClick={() => { setPanel('operator'); setDockHoverPanel(null); setContext({ x: 14, y: window.innerHeight - 350 }); }}>🛠 ابزار اپراتور <kbd>Alt+F6</kbd></button>
        <button onClick={() => setAdVisible(value => !value)}>📢 تبلیغات</button>
      </nav>
    </>}

    {panel && panel !== 'operator' && panel !== 'apps' && <section className="client-panel"><header><b>{panel === 'buffet' ? 'منوی بوفه' : 'حساب من'}</b><button onClick={() => setPanel(null)}>×</button></header>{panel === 'buffet' ? <div className="client-buffet-list">{buffetItems.map(item => <div key={item.name}><span>{item.icon} {item.name}</span><b>{money(item.price)} ت</b><button onClick={() => notify(`درخواست ${item.name} برای اپراتور ثبت شد`)}>+</button></div>)}</div> : <div className="client-account-info"><div><span>نام کاربری</span><b>{customerCode || 'مهمان'}</b></div><div><span>کیف پول</span><b>{money(wallet)} تومان</b></div><div><span>اعتبار رایگان</span><b>۱۲۰٬۰۰۰ تومان</b></div><div><span>بدهی</span><b>۰ تومان</b></div><div><span>ورود هم‌زمان</span><b>{activeLoginCount.toLocaleString('fa-IR')} / {loginLimit.toLocaleString('fa-IR')}</b></div><button className="client-button" onClick={signOut}>خروج مشتری</button></div>}</section>}

    {context && <section className="client-context-menu" style={{ left: context.x, top: context.y }} onClick={event => event.stopPropagation()}><strong>{activeGame ? `بازی: ${games.find(game => game.id === activeGame)?.name}` : 'ابزار کلاینت'}</strong><button onClick={() => doContextAction('internet')}>🌐 تغییر اینترنت / Online · Offline <kbd>Ctrl+I</kbd></button><button onClick={() => doContextAction('move')}>🔀 جابه‌جایی شناسه به سیستم دیگر <kbd>Ctrl+M</kbd></button><button onClick={() => doContextAction('login')}>🔄 ورود / خروج با شناسه <kbd>Ctrl+L</kbd></button><button onClick={() => doContextAction('charge')}>💰 درخواست شارژ از اپراتور <kbd>Ctrl+R</kbd></button><button onClick={() => doContextAction('message')}>💬 ارسال پیام به اپراتور <kbd>Ctrl+P</kbd></button>{activeGame && <button onClick={() => doContextAction('stop-game')}>■ توقف بازی</button>}<button onClick={() => doContextAction('lock')}>🔒 قفل کردن سیستم <kbd>Win+L</kbd></button><button onClick={() => doContextAction('logout')}>🚪 خروج مشتری و بستن وقت</button><button onClick={() => { setContext(null); setAdminDialog('operator'); }}>🧑‍💼 ورود اپراتور / زمان نامحدود</button><button onClick={() => { setContext(null); setAdminDialog('release'); }}>🔓 آزادسازی Windows · Restart · Shutdown</button></section>}

    {paletteOpen && <div className="client-overlay" onMouseDown={event => event.target === event.currentTarget && setPaletteOpen(false)}><section className="client-palette" role="dialog" aria-modal="true"><input autoFocus value={paletteQuery} onChange={event => setPaletteQuery(event.target.value)} onKeyDown={event => event.key === 'Enter' && visibleCommands[0] && runCommand(visibleCommands[0].key)} placeholder="جست‌وجوی فرمان یا صفحه…" /><div>{visibleCommands.map(item => <button key={item.key} onClick={() => runCommand(item.key)}><span>{item.icon} {item.title}</span><kbd>Enter</kbd></button>)}</div><small>Ctrl+K باز کردن · Esc بستن</small></section></div>}

    {accountGame && <div className="client-overlay" onMouseDown={event => event.target === event.currentTarget && setAccountGame(null)}><section className="client-account-picker" role="dialog" aria-modal="true"><header><div><b>{accountGame.icon} {accountGame.name}</b><small>روش ورود به بازی را انتخاب کنید</small></div><button onClick={() => setAccountGame(null)}>×</button></header><button className="account-option" onClick={() => launchGame(accountGame, 'own')}><span>👤</span><div><b>اکانت خودم</b><small>با حساب شخصی خود وارد شوید</small></div></button><button className="account-option pool" onClick={() => launchGame(accountGame, 'pool')}><span>🎮</span><div><b>اکانت GameNet</b><small>از Account Pool سرور تخصیص داده شود</small></div></button><p>اطلاعات ورود اکانت‌های مجموعه به مشتری نمایش داده نمی‌شود.</p></section></div>}

    {adminDialog && <div className="client-overlay"><section className="client-admin-dialog" role="dialog" aria-modal="true"><header><b>{adminDialog === 'operator' ? 'ورود اپراتور با زمان نامحدود' : 'آزادسازی سیستم'}</b><button onClick={() => { setAdminDialog(null); setPin(''); }}>×</button></header><p>{adminDialog === 'operator' ? 'PIN اپراتور را وارد کنید.' : 'رمز مدیر برای آزادسازی Windows، Restart یا Shutdown لازم است.'}</p><input autoFocus type="password" value={pin} onChange={event => setPin(event.target.value)} placeholder={adminDialog === 'operator' ? 'PIN اپراتور' : 'رمز مدیر'} /><div className="client-admin-actions"><button onClick={() => { setAdminDialog(null); setPin(''); }}>لغو</button>{adminDialog === 'operator' ? <button className="primary" onClick={submitAdminAction}>ورود اپراتور</button> : <><button className="primary" onClick={submitAdminAction}>آزادسازی Windows</button><button onClick={() => { submitAdminAction(); notify('Restart ثبت شد'); }}>Restart</button><button className="danger" onClick={() => { submitAdminAction(); notify('Shutdown ثبت شد'); }}>Shutdown</button></>}</div><small>این عملیات دمو است و اجرای واقعی به Agent کلاینت نیاز دارد.</small></section></div>}

    {notice && <div className="client-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    <span className="client-clock" aria-hidden="true">{new Date(now).toLocaleTimeString('fa-IR')}</span>
  </div>;
}
