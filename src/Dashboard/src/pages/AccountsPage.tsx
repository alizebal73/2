import { useEffect, useMemo, useState } from 'react';
import { createServerAccount, getServerAccountClients, getServerAccountLogs, getServerAccounts, leaseServerAccount, lockServerAccount, releaseServerAccount, unlockServerAccount, updateServerAccount } from '../services/accountPoolService';
import type { AccountRecord, GameRecord } from '../types';
import { getServerGames } from '../services/gameLibraryService';

type Platform = 'all' | AccountRecord['platform'];

export function AccountsPage() {
  const [accounts, setAccounts] = useState<AccountRecord[]>([]);
  const [filter, setFilter] = useState<Platform>('all');
  const [draft, setDraft] = useState<AccountRecord | null>(null);
  const [logs, setLogs] = useState<string[] | null>(null);
  const [notice, setNotice] = useState('');
  const [selectedAccountId, setSelectedAccountId] = useState<string | null>(null);
  const [games, setGames] = useState<GameRecord[]>([]);
  const [clients, setClients] = useState<Array<{ id: string; name: string; isOnline: boolean; lifecycleState: string }>>([]);
  const [leaseGameId, setLeaseGameId] = useState('');
  const [leaseClientId, setLeaseClientId] = useState('');

  useEffect(() => {
    void Promise.all([getServerAccounts(), getServerGames()]).then(([accountRows, gameRows]) => {
      setAccounts(accountRows);
      setGames(gameRows);
      void getServerAccountClients().then(setClients).catch(() => setClients([]));
    }).catch(error => setNotice(error instanceof Error ? error.message : 'دریافت Account Pool انجام نشد'));
  }, []);

  const visible = useMemo(() => accounts.filter(account => filter === 'all' || account.platform === filter), [accounts, filter]);

  function createAccount() {
    setDraft({ id: crypto.randomUUID(), title: '', platform: 'Steam', status: 'free', owner: 'مجموعه', expiresAt: '', allowedGames: [], allowedGameIds: [], assignedClient: '', guardStatus: 'محافظت‌شده', launcher: '', login: '', password: '' });
  }

  async function saveAccount() {
    if (!draft?.title.trim()) { setNotice('نام یا شناسه اکانت را وارد کنید'); return; }
    try {
      const exists = accounts.some(account => account.id === draft.id);
      const saved = exists ? await updateServerAccount(draft) : await createServerAccount(draft);
      setAccounts(current => exists ? current.map(account => account.id === saved.id ? saved : account) : [...current, saved]);
      setDraft(null); setNotice('اکانت ذخیره شد');
    } catch (error) { setNotice(error instanceof Error ? error.message : 'ذخیره اکانت انجام نشد'); }
  }

  async function unlock(account: AccountRecord) {
    try { const saved = await unlockServerAccount(account.id); setAccounts(current => current.map(item => item.id === saved.id ? saved : item)); setNotice(account.title + ' آزاد شد'); }
    catch (error) { setNotice(error instanceof Error ? error.message : 'رفع قفل اکانت انجام نشد'); }
  }

  async function showLogs() {
    try { setLogs((await getServerAccountLogs()).map(row => [row.operator, row.details || row.action].filter(Boolean).join(' · '))); }
    catch (error) { setNotice(error instanceof Error ? error.message : 'دریافت لاگ انجام نشد'); }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>حساب‌های بازی</p>
          <h1>حساب‌ها</h1>
        </div>
      </div>

      <div className="summary-grid">
        <div className="summary-card"><div className="label">آزاد</div><div className="value green">{accounts.filter(item => item.status === 'free').length}</div></div>
        <div className="summary-card"><div className="label">در استفاده</div><div className="value blue">{accounts.filter(item => item.status === 'in-use').length}</div></div>
        <div className="summary-card"><div className="label">قفل‌شده</div><div className="value red">{accounts.filter(item => item.status === 'locked').length}</div></div>
        <div className="summary-card"><div className="label">کل استخر</div><div className="value orange">{accounts.length}</div></div>
      </div>
      <div className="toolbar">
        <div className="view-switch">{(['all', 'Steam', 'Battle.net', 'Riot', 'Epic'] as Platform[]).map(item => <button key={item} className={filter === item ? 'active' : ''} onClick={() => setFilter(item)}>{item === 'all' ? 'همه' : item}</button>)}</div>
        <button className="btn" onClick={() => void showLogs()}>📜 لاگ اکانت‌ها</button>
        <button type="button" className="btn primary" onClick={createAccount}>+ اکانت جدید</button>
      </div>
      <p className="account-security-note">رمزهای اکانت فقط در سمت سرور نگهداری می‌شوند و هرگز به UI مشتری ارسال نمی‌شوند.</p>

      <div className="accounts-master-detail">
        <aside className="account-platform-nav">
          <div className="account-platform-title">استخر اکانت‌ها</div>
          {(['all', 'Steam', 'Battle.net', 'Riot', 'Epic'] as Platform[]).map(platform => {
            const count = platform === 'all' ? accounts.length : accounts.filter(item => item.platform === platform).length;
            return <button type="button" key={platform} className={filter === platform ? 'active' : ''} onClick={() => { setFilter(platform); setSelectedAccountId(null); }}>
              <strong>{platform === 'all' ? 'همه پلتفرم‌ها' : platform}</strong><span>{count.toLocaleString('fa-IR')}</span>
            </button>;
          })}
        </aside>
        <section className="account-list-panel">
          <div className="account-list-head"><strong>{filter === 'all' ? 'همه اکانت‌ها' : 'اکانت‌های ' + filter}</strong><span>{visible.length.toLocaleString('fa-IR')} مورد</span></div>
          <div className="account-list">
            {visible.map(account => <button type="button" key={account.id} className={'account-list-row ' + (selectedAccountId === account.id ? 'active' : '')} onClick={() => setSelectedAccountId(account.id)}>
              <span className="account-platform-badge">{account.platform.slice(0,1)}</span>
              <span><strong>{account.title}</strong><small>{account.allowedGames.length ? account.allowedGames.join('، ') : 'بازی مشخص نشده'}</small></span>
              <em className={account.status}>{account.status === 'free' ? 'آزاد' : account.status === 'in-use' ? 'در استفاده' : 'قفل'}</em>
            </button>)}
          </div>
        </section>
        <section className="account-detail-panel">
          {(() => {
            const selected = accounts.find(item => item.id === selectedAccountId) ?? visible[0];
            if (!selected) return <div className="games-empty">پلتفرم یا اکانتی برای نمایش وجود ندارد.</div>;
            return <><div className="account-detail-head"><div><span className="game-detail-kicker">{selected.platform}</span><h2>{selected.title}</h2><p>{selected.owner} · {selected.expiresAt || 'بدون انقضا'}</p></div><strong className={'account-detail-status ' + selected.status}>{selected.status === 'free' ? 'آزاد' : selected.status === 'in-use' ? 'در استفاده' : 'قفل‌شده'}</strong></div>
              <div className="account-detail-grid">
                <div className="info-row"><span>بازی‌های مجاز</span><strong>{selected.allowedGames.join('، ') || 'تعریف نشده'}</strong></div>
                <div className="info-row"><span>کلاینت</span><strong>{selected.assignedClient || '—'}</strong></div>
                <div className="info-row"><span>Guard / 2FA</span><strong>{selected.guardStatus}</strong></div>
              </div>
              {selected.status === 'free' && <div className="account-lease-controls">
                <select value={leaseGameId} onChange={event => setLeaseGameId(event.target.value)} aria-label="بازی برای تخصیص">
                  <option value="">بازی را انتخاب کنید</option>{games.filter(game => selected.allowedGameIds.includes(game.id) && game.active).map(game => <option key={game.id} value={game.id}>{game.name}</option>)}
                </select>
                <select value={leaseClientId} onChange={event => setLeaseClientId(event.target.value)} aria-label="کلاینت برای تخصیص">
                  <option value="">کلاینت آنلاین را انتخاب کنید</option>{clients.filter(client => client.isOnline).map(client => <option key={client.id} value={client.id}>{client.name}</option>)}
                </select>
                <button className="btn primary" disabled={!leaseGameId || !leaseClientId} onClick={() => void (async () => {
                  try { const saved = await leaseServerAccount(selected.id, leaseGameId, leaseClientId); setAccounts(current => current.map(item => item.id === saved.id ? saved : item)); setLeaseGameId(''); setLeaseClientId(''); setNotice('اکانت به کلاینت تخصیص یافت'); }
                  catch (error) { setNotice(error instanceof Error ? error.message : 'تخصیص اکانت انجام نشد'); }
                })()}>تخصیص به کلاینت</button>
              </div>}
              {selected.status === 'in-use' && <div className="account-lease-controls">
                <div className="info-row"><span>بازی فعال</span><strong>{selected.assignedGame || 'نامشخص'} · {selected.assignedClient || 'کلاینت نامشخص'}</strong></div>
                <button className="btn" onClick={() => void (async () => {
                  try { const saved = await releaseServerAccount(selected.id); setAccounts(current => current.map(item => item.id === saved.id ? saved : item)); setNotice('اکانت آزاد شد'); }
                  catch (error) { setNotice(error instanceof Error ? error.message : 'آزادسازی اکانت انجام نشد'); }
                })()}>آزادسازی حساب</button>
              </div>}
              <div className="account-card-actions"><button className="btn primary" onClick={() => setDraft({ ...selected })}>ویرایش</button>{selected.status === 'locked' && <button className="btn" onClick={() => void unlock(selected)}>رفع قفل</button>}{selected.status === 'free' && <button className="btn" onClick={() => void (async () => { try { const saved = await lockServerAccount(selected.id); setAccounts(current => current.map(item => item.id === saved.id ? saved : item)); setNotice('اکانت قفل شد'); } catch (error) { setNotice(error instanceof Error ? error.message : 'قفل اکانت انجام نشد'); } })()}>قفل اکانت</button>}<button className="btn" onClick={() => void showLogs()}>لاگ استخر</button></div>
            </>;
          })()}
        </section>
      </div>
      {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{accounts.some(item => item.id === draft.id) ? 'ویرایش اکانت' : 'اکانت جدید'}</h2>
        <label>نام / شناسه<input value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} /></label>
        <label>Platform<select value={draft.platform} onChange={event => setDraft({ ...draft, platform: event.target.value as AccountRecord['platform'] })}><option>Steam</option><option>Battle.net</option><option>Riot</option><option>Epic</option></select></label>
        <label>Launcher<input value={draft.launcher || ''} onChange={event => setDraft({ ...draft, launcher: event.target.value })} /></label>
        <label>Login<input className="ltr" value={draft.login || ''} onChange={event => setDraft({ ...draft, login: event.target.value })} /></label>
        <label>رمز اکانت<input className="ltr" type="password" value={draft.password || ''} onChange={event => setDraft({ ...draft, password: event.target.value })} placeholder={accounts.some(item => item.id === draft.id) ? 'در صورت تغییر وارد شود' : ''} /></label>
        <div><strong>بازی‌های مجاز</strong>{games.length === 0 ? <p>بازی فعالی ثبت نشده است.</p> : <div className="account-game-checks">{games.filter(game => game.active).map(game => <label key={game.id}><input type="checkbox" checked={draft.allowedGameIds.includes(game.id)} onChange={event => { const ids = event.target.checked ? [...draft.allowedGameIds, game.id] : draft.allowedGameIds.filter(id => id !== game.id); setDraft({ ...draft, allowedGameIds: ids, allowedGames: games.filter(item => ids.includes(item.id)).map(item => item.name) }); }} />{game.name}</label>)}</div>}</div>
        <div className="info-row"><span>وضعیت</span><strong>{draft.status === 'free' ? 'آزاد' : draft.status === 'in-use' ? 'در استفاده' : 'قفل‌شده'} · فقط با عملیات Pool تغییر می‌کند</strong></div>
        <div className="info-row"><span>کلاینت تخصیص‌یافته</span><strong>{draft.assignedClient || '—'}</strong></div>
        <label>وضعیت Guard / 2FA<select value={draft.guardStatus} onChange={event => setDraft({ ...draft, guardStatus: event.target.value as AccountRecord['guardStatus'] })}><option>2FA</option><option>محافظت‌شده</option><option>نیازمند بررسی</option></select></label>
        <label>مالک<input value={draft.owner} onChange={event => setDraft({ ...draft, owner: event.target.value })} /></label>
        <label>تاریخ انقضا<input value={draft.expiresAt} onChange={event => setDraft({ ...draft, expiresAt: event.target.value })} /></label>
        <div className="modal-actions"><button className="btn primary" onClick={() => void saveAccount()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>لغو</button></div>
      </section></div>}
      {logs && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setLogs(null)}><section className="operation-modal" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setLogs(null)}>×</button><h2>لاگ Account Pool</h2>{logs.map((item, index) => <div className="info-row" key={`${item}-${index}`}><span>{item}</span><strong>ثبت‌شده</strong></div>)}</section></div>}
      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
