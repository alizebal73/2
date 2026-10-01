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

      <div className="accounts-grid">
        {visible.map((account) => (
          <div key={account.id} className="account-card">
            <div className="account-card-heading"><b>{account.title}</b><span>{account.platform}</span></div>
            <div className="meta">بازی‌های مجاز: {account.allowedGames.join('، ') || 'تعریف نشده'}</div>
            <div className="info-row"><span>وضعیت</span><strong className={account.status === 'free' ? 'positive' : account.status === 'locked' ? 'negative' : ''}>{account.status === 'free' ? 'آزاد' : account.status === 'in-use' ? 'در استفاده' : 'قفل‌شده'}</strong></div>
            <div className="info-row"><span>در اختیار</span><strong>{account.assignedClient || '—'}</strong></div>
            <div className="info-row"><span>Guard / 2FA</span><strong>{account.guardStatus}</strong></div>
            <div className="meta">مالک: {account.owner} · انقضا: {account.expiresAt || '—'}</div>
            <div className="account-card-actions"><button className="btn sm" onClick={() => setDraft({ ...account })}>ویرایش</button>{account.status === 'locked' && <button className="btn sm" onClick={() => void unlock(account)}>رفع قفل</button>}<button className="btn sm" onClick={() => void showLogs()}>لاگ</button></div>
          </div>
        ))}
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
