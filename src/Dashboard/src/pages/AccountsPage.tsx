import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import type { AccountRecord } from '../types';

type Platform = 'all' | AccountRecord['platform'];

export function AccountsPage() {
  const [accounts, setAccounts] = useState<AccountRecord[]>([]);
  const [filter, setFilter] = useState<Platform>('all');
  const [draft, setDraft] = useState<AccountRecord | null>(null);
  const [logs, setLogs] = useState<string[] | null>(null);
  const [notice, setNotice] = useState('');
  const [selectedAccountId, setSelectedAccountId] = useState<string | null>(null);

  useEffect(() => {
    void mockService.getAccounts().then(setAccounts);
  }, []);

  const visible = useMemo(() => accounts.filter(account => filter === 'all' || account.platform === filter), [accounts, filter]);

  function createAccount() {
    setDraft({ id: crypto.randomUUID(), title: '', platform: 'Steam', status: 'free', owner: 'مجموعه', expiresAt: '', allowedGames: [], assignedClient: '', guardStatus: 'محافظت‌شده' });
  }

  async function saveAccount() {
    if (!draft?.title.trim()) { setNotice('نام یا شناسه اکانت را وارد کنید'); return; }
    await mockService.saveAccount(draft);
    setAccounts(await mockService.getAccounts()); setDraft(null); setNotice('اکانت ذخیره شد');
  }

  async function unlock(account: AccountRecord) {
    await mockService.unlockAccount(account.id);
    setAccounts(await mockService.getAccounts()); setNotice(`${account.title} آزاد شد`);
  }

  async function showLogs() {
    setLogs(await mockService.getAccountLogs());
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
              <div className="account-card-actions"><button className="btn primary" onClick={() => setDraft({ ...selected })}>ویرایش</button>{selected.status === 'locked' && <button className="btn" onClick={() => void unlock(selected)}>رفع قفل</button>}<button className="btn" onClick={() => void showLogs()}>لاگ استخر</button></div>
            </>;
          })()}
        </section>
      </div>
      {draft && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setDraft(null)}><section className="operation-modal" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setDraft(null)}>×</button><h2>{accounts.some(item => item.id === draft.id) ? 'ویرایش اکانت' : 'اکانت جدید'}</h2>
        <label>نام / شناسه<input value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} /></label>
        <label>Platform<select value={draft.platform} onChange={event => setDraft({ ...draft, platform: event.target.value as AccountRecord['platform'] })}><option>Steam</option><option>Battle.net</option><option>Riot</option><option>Epic</option></select></label>
        <label>بازی‌های مجاز (با ویرگول جدا شود)<input value={draft.allowedGames.join(', ')} onChange={event => setDraft({ ...draft, allowedGames: event.target.value.split(',').map(item => item.trim()).filter(Boolean) })} /></label>
        <label>وضعیت<select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value as AccountRecord['status'] })}><option value="free">آزاد</option><option value="in-use">در استفاده</option><option value="locked">قفل‌شده</option></select></label>
        <label>کلاینت تخصیص‌یافته<input value={draft.assignedClient} onChange={event => setDraft({ ...draft, assignedClient: event.target.value })} /></label>
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
