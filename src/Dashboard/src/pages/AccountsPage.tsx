import { useEffect, useMemo, useState } from 'react';
import type { AccountRecord, GameRecord } from '../types';
import {
  allocateServerAccount,
  createServerAccount,
  getServerAccountPool,
  getServerLeases,
  getServerAccountPoolHealth,
  getServerLeaseHistory,
  releaseServerLease,
  unlockServerAccount,
} from '../services/accountPoolService';
import { getServerGames } from '../services/gameService';

type Platform = 'all' | AccountRecord['platform'];

export function AccountsPage() {
  const [accounts, setAccounts] = useState<AccountRecord[]>([]);
  const [games, setGames] = useState<GameRecord[]>([]);
  const [leases, setLeases] = useState<Array<{ leaseId: string; accountTitle: string; platform: string; gameId: string; assignedClient?: string | null }>>([]);
  const [health, setHealth] = useState<Awaited<ReturnType<typeof getServerAccountPoolHealth>> | null>(null);
  const [history, setHistory] = useState<Awaited<ReturnType<typeof getServerLeaseHistory>>>([]);
  const [filter, setFilter] = useState<Platform>('all');
  const [draft, setDraft] = useState<AccountRecord | null>(null);
  const [allowedGameNames, setAllowedGameNames] = useState('');
  const [secret, setSecret] = useState('');
  const [selectedGameId, setSelectedGameId] = useState('');
  const [notice, setNotice] = useState('');
  const [selectedAccountId, setSelectedAccountId] = useState<string | null>(null);

  async function refresh() {
    try {
      const [nextAccounts, nextGames, nextLeases, nextHealth, nextHistory] = await Promise.all([
        getServerAccountPool(),
        getServerGames(),
        getServerLeases(),
        getServerAccountPoolHealth(),
        getServerLeaseHistory(200),
      ]);

      setAccounts(nextAccounts);
      setGames(nextGames);
      setLeases(nextLeases);
      setHealth(nextHealth);
      setHistory(nextHistory);
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'دریافت اطلاعات استخر اکانت‌ها ناموفق بود');
    }
  }

  useEffect(() => { void refresh(); }, []);

  const visible = useMemo(
    () => accounts.filter(account => filter === 'all' || account.platform === filter),
    [accounts, filter],
  );

  function createAccount() {
    setDraft({ id: '', title: '', login: '', platform: 'Steam', status: 'free', owner: 'مجموعه', expiresAt: '', allowedGames: [], assignedClient: '', guardStatus: 'محافظت‌شده' });
    setAllowedGameNames('');
    setSecret('');
  }

  async function resolveAllowedGameIds(names: string[]) {
    const normalized = names.map(item => item.trim().toLocaleLowerCase()).filter(Boolean);
    return games.filter(game => normalized.includes(game.name.trim().toLocaleLowerCase())).map(game => game.id);
  }

  async function saveAccount() {
    if (!draft?.title.trim()) {
      setNotice('نام یا شناسه اکانت را وارد کنید');
      return;
    }
    try {
      const allowedGameIds = await resolveAllowedGameIds(allowedGameNames.split(','));
      if (allowedGameNames.trim() && allowedGameIds.length !== allowedGameNames.split(',').map(item => item.trim()).filter(Boolean).length) {
        setNotice('یکی از نام بازی‌ها در فهرست بازی‌های واقعی سرور پیدا نشد');
        return;
      }

      if (!draft.id) {
        await createServerAccount({
          title: draft.title,
          platform: draft.platform,
          login: draft.login,
          owner: draft.owner,
          allowedGameIds,
          secret,
        });
      } else {
        const response = await fetch('/api/account-pool/' + draft.id, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            title: draft.title,
            platform: draft.platform,
            login: draft.login || null,
            secret: secret || null,
            owner: draft.owner,
            expiresAt: null,
            allowedGameIds,
            status: draft.status === 'in-use' ? 'InUse' : draft.status === 'locked' ? 'Locked' : 'Free',
          }),
        });
        if (!response.ok) throw new Error('ویرایش اکانت در سرور انجام نشد');
      }
      setDraft(null);
      setSecret('');
      setNotice('اکانت در سرور ذخیره شد');
      await refresh();
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'ذخیره اکانت ناموفق بود');
    }
  }

  async function unlock(account: AccountRecord) {
    try {
      await unlockServerAccount(account.id);
      setNotice(account.title + ' آزاد شد');
      await refresh();
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'رفع قفل ناموفق بود');
    }
  }

  async function allocate() {
    if (!selectedGameId) {
      setNotice('ابتدا بازی را برای تخصیص انتخاب کنید');
      return;
    }
    try {
      await allocateServerAccount(selectedGameId);
      setNotice('Lease اکانت با موفقیت روی سرور ایجاد شد');
      await refresh();
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'تخصیص اکانت ناموفق بود');
    }
  }

  async function release(leaseId: string) {
    try {
      await releaseServerLease(leaseId, 'آزادسازی از داشبورد اپراتور');
      setNotice('Lease آزاد شد');
      await refresh();
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'آزادسازی Lease ناموفق بود');
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <p>حساب‌های واقعی سرور</p>
          <h1>استخر اکانت‌ها</h1>
        </div>
      </div>

      <div className="summary-grid">
        <div className="summary-card"><div className="label">کل</div><div className="value">{health?.total ?? accounts.length}</div></div>
        <div className="summary-card"><div className="label">آزاد</div><div className="value green">{accounts.filter(item => item.status === 'free').length}</div></div>
        <div className="summary-card"><div className="label">در استفاده</div><div className="value blue">{accounts.filter(item => item.status === 'in-use').length}</div></div>
        <div className="summary-card"><div className="label">قفل‌شده</div><div className="value red">{accounts.filter(item => item.status === 'locked').length}</div></div>
        <div className="summary-card"><div className="label">Lease فعال</div><div className="value orange">{health?.activeLeases ?? leases.length}</div></div>
        <div className="summary-card"><div className="label">در آستانه انقضا</div><div className="value">{health?.expiringLeases ?? 0}</div></div>
        <div className="summary-card"><div className="label">بدون Secret</div><div className="value red">{health?.missingCredential ?? 0}</div></div>
      </div>

      <div className="toolbar">
        <div className="view-switch">
          {(['all', 'Steam', 'Battle.net', 'Riot', 'Epic'] as Platform[]).map(item =>
            <button key={item} className={filter === item ? 'active' : ''} onClick={() => setFilter(item)}>
              {item === 'all' ? 'همه' : item}
            </button>,
          )}
        </div>
        <select value={selectedGameId} onChange={event => setSelectedGameId(event.target.value)} aria-label="بازی برای تخصیص">
          <option value="">بازی برای تخصیص…</option>
          {games.map(game => <option key={game.id} value={game.id}>{game.name}</option>)}
        </select>
        <button className="btn" onClick={() => void allocate()}>تخصیص اتمیک</button>
        <button type="button" className="btn primary" onClick={createAccount}>+ اکانت جدید</button>
      </div>

      <p className="account-security-note">وضعیت آزاد/درحال‌استفاده فقط از Server خوانده می‌شود. تخصیص با Lease سروری انجام می‌شود و رمز اکانت هرگز به UI برنمی‌گردد.</p>

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
              <span className="account-platform-badge">{account.platform.slice(0, 1)}</span>
              <span><strong>{account.title}</strong><small>{account.allowedGames.length ? account.allowedGames.join('، ') : 'بدون بازی مجاز'}</small></span>
              <em className={account.status}>{account.status === 'free' ? 'آزاد' : account.status === 'in-use' ? 'در استفاده' : 'قفل'}</em>
            </button>)}
          </div>
        </section>

        <section className="account-detail-panel">
          {(() => {
            const selected = accounts.find(item => item.id === selectedAccountId) ?? visible[0];
            if (!selected) return <div className="games-empty">اکانتی برای نمایش وجود ندارد.</div>;
            return <>
              <div className="account-detail-head">
                <div><span className="game-detail-kicker">{selected.platform}</span><h2>{selected.title}</h2><p>{selected.owner} · {selected.expiresAt || 'بدون انقضا'}</p></div>
                <strong className={'account-detail-status ' + selected.status}>{selected.status === 'free' ? 'آزاد' : selected.status === 'in-use' ? 'در استفاده' : 'قفل‌شده'}</strong>
              </div>
              <div className="account-detail-grid">
                <div className="info-row"><span>بازی‌های مجاز</span><strong>{selected.allowedGames.join('، ') || 'تعریف نشده'}</strong></div>
                <div className="info-row"><span>کلاینت</span><strong>{selected.assignedClient || '—'}</strong></div>
                <div className="info-row"><span>Guard / 2FA</span><strong>{selected.guardStatus}</strong></div>
              </div>
              <div className="account-card-actions">
                <button className="btn primary" onClick={() => { setDraft({ ...selected }); setAllowedGameNames(selected.allowedGames.join(', ')); setSecret(''); }}>ویرایش</button>
                {selected.status === 'locked' && <button className="btn" onClick={() => void unlock(selected)}>رفع قفل</button>}
              </div>
            </>;
          })()}
        </section>
      </div>

      <section className="operation-section">
        <div className="page-header compact"><div><p>Server-authoritative</p><h2>تاریخچه Lease</h2></div></div>
        <div className="table-wrap">
          <table><thead><tr><th>اکانت</th><th>بازی</th><th>کلاینت</th><th>شروع</th><th>پایان</th><th>وضعیت</th><th>دلیل آزادسازی</th></tr></thead>
          <tbody>{history.map(item => <tr key={item.leaseId}><td>{item.accountTitle}</td><td>{item.gameName}</td><td>{item.assignedClient || '—'}</td><td>{new Date(item.leasedAt).toLocaleString('fa-IR')}</td><td>{item.releasedAt ? new Date(item.releasedAt).toLocaleString('fa-IR') : 'فعال'}</td><td>{item.state}</td><td>{item.releaseReason || '—'}</td></tr>)}</tbody>
          </table>
        </div>
      </section>

      {leases.length > 0 && <section className="operation-section">
        <div className="page-header compact"><div><p>Server-authoritative</p><h2>Leaseهای فعال</h2></div></div>
        <div className="table-wrap">
          <table><thead><tr><th>اکانت</th><th>پلتفرم</th><th>کلاینت</th><th>عملیات</th></tr></thead>
            <tbody>{leases.map(lease => <tr key={lease.leaseId}><td>{lease.accountTitle}</td><td>{lease.platform}</td><td>{lease.assignedClient || 'بدون Agent'}</td><td><button className="btn sm" onClick={() => void release(lease.leaseId)}>آزادسازی</button></td></tr>)}</tbody>
          </table>
        </div>
      </section>}

      {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}>
        <section className="operation-modal" role="dialog" aria-modal="true">
          <button className="modal-close" onClick={() => setDraft(null)}>×</button>
          <h2>{draft.id ? 'ویرایش اکانت' : 'اکانت جدید'}</h2>
          <label>نام / شناسه<input value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} /></label>
          <label>Login / ایمیل اکانت<input className="ltr" value={draft.login} onChange={event => setDraft({ ...draft, login: event.target.value })} placeholder="نام کاربری یا ایمیل" /></label>
          <label>Platform<select value={draft.platform} onChange={event => setDraft({ ...draft, platform: event.target.value as AccountRecord['platform'] })}><option>Steam</option><option>Battle.net</option><option>Riot</option><option>Epic</option></select></label>
          <label>مالک<input value={draft.owner} onChange={event => setDraft({ ...draft, owner: event.target.value })} /></label>
          <label>بازی‌های مجاز<input value={allowedGameNames} onChange={event => setAllowedGameNames(event.target.value)} placeholder="نام بازی‌ها با ویرگول جدا شود" /></label>
          {!draft.id && <label>رمز/Secret اکانت<input type="password" value={secret} onChange={event => setSecret(event.target.value)} placeholder="فقط روی سرور ذخیره می‌شود" /></label>}
          <div className="modal-actions"><button className="btn primary" onClick={() => void saveAccount()}>ذخیره</button><button className="btn" onClick={() => setDraft(null)}>انصراف</button></div>
        </section>
      </div>}

      {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
    </>
  );
}
