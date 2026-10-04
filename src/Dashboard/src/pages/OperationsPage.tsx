import { useEffect, useState } from 'react';
import { getServerAccountPool } from '../services/accountPoolService';
import { getAgentStatuses } from '../services/agentService';
import { getServerCustomers } from '../services/customerService';
import { getServerGames } from '../services/gameService';
import { getServerProducts } from '../services/buffetService';
import type { AccountRecord, AgentStatusDto, CustomerRecord, GameRecord, ProductRecord } from '../types';

type LoadState = 'loading' | 'ready' | 'error';

export function OperationsPage() {
  const [state, setState] = useState<LoadState>('loading');
  const [error, setError] = useState('');
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [products, setProducts] = useState<ProductRecord[]>([]);
  const [games, setGames] = useState<GameRecord[]>([]);
  const [accounts, setAccounts] = useState<AccountRecord[]>([]);
  const [agents, setAgents] = useState<AgentStatusDto[]>([]);

  async function load() {
    setState('loading');
    setError('');
    try {
      const [nextCustomers, nextProducts, nextGames, nextAccounts, nextAgents] = await Promise.all([
        getServerCustomers(),
        getServerProducts(),
        getServerGames(),
        getServerAccountPool(),
        getAgentStatuses(),
      ]);
      setCustomers(nextCustomers);
      setProducts(nextProducts);
      setGames(nextGames);
      setAccounts(nextAccounts);
      setAgents(nextAgents);
      setState('ready');
    } catch (err) {
      setState('error');
      setError(err instanceof Error ? err.message : 'اطلاعات عملیات از سرور دریافت نشد.');
    }
  }

  useEffect(() => { void load(); }, []);

  if (state === 'loading') {
    return <section className="page-panel"><div className="global-search-empty">در حال دریافت وضعیت واقعی عملیات از سرور…</div></section>;
  }

  if (state === 'error') {
    return (
      <section className="page-panel">
        <div className="global-search-empty error">
          <strong>مرکز عملیات قابل بارگذاری نیست</strong>
          <span>{error}</span>
          <button type="button" className="btn primary" onClick={() => void load()}>تلاش مجدد</button>
        </div>
      </section>
    );
  }

  const onlineAgents = agents.filter(item => item.isOnline);

  return (
    <div className="page-content">
      <div className="page-header">
        <div>
          <p>Server-backed</p>
          <h1>مرکز عملیات</h1>
        </div>
        <button type="button" className="btn" onClick={() => void load()}>↻ به‌روزرسانی</button>
      </div>

      <div className="stats-grid" style={{ padding: '0 22px 14px' }}>
        <div className="stat-card"><span>مشتری</span><strong>{customers.length.toLocaleString('fa-IR')}</strong></div>
        <div className="stat-card"><span>کالا</span><strong>{products.length.toLocaleString('fa-IR')}</strong></div>
        <div className="stat-card"><span>بازی</span><strong>{games.length.toLocaleString('fa-IR')}</strong></div>
        <div className="stat-card"><span>اکانت Pool</span><strong>{accounts.length.toLocaleString('fa-IR')}</strong></div>
        <div className="stat-card"><span>Agent آنلاین</span><strong>{onlineAgents.length.toLocaleString('fa-IR')}</strong></div>
      </div>

      <section className="card-panel" style={{ margin: '0 22px 14px', padding: 14 }}>
        <div className="section-toolbar">
          <h3 style={{ margin: 0 }}>وضعیت Agentهای واقعی</h3>
          <span>{agents.length.toLocaleString('fa-IR')} دستگاه</span>
        </div>
        <div className="table-wrap">
          <table>
            <thead><tr><th>دستگاه</th><th>ایستگاه</th><th>اتصال</th><th>قفل</th><th>نسخه</th><th>Lifecycle</th></tr></thead>
            <tbody>
              {agents.length === 0
                ? <tr><td colSpan={6}>هنوز Agent واقعی ثبت نشده است.</td></tr>
                : agents.map(agent => (
                  <tr key={agent.agentId}>
                    <td>{agent.name}<div className="muted" dir="ltr">{agent.deviceId}</div></td>
                    <td>{agent.stationName || '—'}</td>
                    <td>{agent.isOnline ? 'آنلاین' : 'آفلاین'}</td>
                    <td>{agent.isLocked ? 'قفل' : 'باز'}</td>
                    <td dir="ltr">{agent.agentVersion || '—'}</td>
                    <td>{agent.lifecycleState}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="card-panel" style={{ margin: '0 22px 30px', padding: 14 }}>
        <h3>مرز قابلیت‌های مرکز عملیات</h3>
        <p className="muted">
          این صفحه دیگر داده یا عملیات ساختگی ندارد. مشتری، کالا، بازی، Account Pool و Agent از Server واقعی خوانده می‌شوند.
          رزرو/صف، هزینه‌های مدیریتی و Event/Tournament به‌عنوان دامنه‌های بعدی فقط پس از قرارداد Server واقعی وارد این صفحه خواهند شد.
        </p>
      </section>
    </div>
  );
}
