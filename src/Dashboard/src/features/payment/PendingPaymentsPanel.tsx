import { useMemo, useState } from 'react';
import type { PendingSettlementAccount } from '../../types';

type ViewMode = 'v-card' | 'v-compact' | 'v-list';
type SortKey = 'customer' | 'station' | 'amount' | 'waiting' | 'charges' | 'buffet' | 'status';
type SortDirection = 'asc' | 'desc';

type Props = {
  payments: PendingSettlementAccount[];
  money: (value: number) => string;
  onPay: (invoiceId: string, method: 'cash' | 'card' | 'wallet') => void;
};

function compare(a: PendingSettlementAccount, b: PendingSettlementAccount, key: SortKey) {
  if (key === 'customer') return a.customerName.localeCompare(b.customerName, 'fa', { numeric: true });
  if (key === 'station') return a.stationName.localeCompare(b.stationName, 'fa', { numeric: true });
  if (key === 'amount') return a.amountDue - b.amountDue;
  if (key === 'waiting') return a.waitingMinutes - b.waitingMinutes;
  if (key === 'charges') return a.charges.length - b.charges.length;
  if (key === 'buffet') return a.buffetItems.reduce((s, x) => s + x.quantity, 0) - b.buffetItems.reduce((s, x) => s + x.quantity, 0);
  return 0;
}

export function PendingPaymentsPanel({ payments, money, onPay }: Props) {
  const [view, setView] = useState<ViewMode>('v-card');
  const [sort, setSort] = useState<{ key: SortKey; direction: SortDirection }>({ key: 'waiting', direction: 'asc' });
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const labels: Record<SortKey, string> = {
    customer: 'مشتری',
    station: 'رایانه',
    amount: 'مبلغ نهایی',
    waiting: 'زمان انتظار',
    charges: 'تعداد شارژ',
    buffet: 'تعداد بوفه',
    status: 'وضعیت',
  };

  function toggle(key: SortKey) {
    setSort(current => current.key === key
      ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' }
      : { key, direction: 'asc' });
  }

  function toggleBranch(invoiceId: string, branch: 'charges' | 'buffet' | 'calc') {
    const key = invoiceId + ':' + branch;
    setExpanded(current => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  const sorted = useMemo(() => {
    const direction = sort.direction === 'asc' ? 1 : -1;
    return [...payments].sort((a, b) => compare(a, b, sort.key) * direction || a.customerName.localeCompare(b.customerName, 'fa'));
  }, [payments, sort]);

  function branches(item: PendingSettlementAccount) {
    const chargeOpen = expanded.has(item.invoiceId + ':charges');
    const buffetOpen = expanded.has(item.invoiceId + ':buffet');
    const calcOpen = expanded.has(item.invoiceId + ':calc');
    const buffetCount = item.buffetItems.reduce((sum, row) => sum + row.quantity, 0);

    return <div className="pending-payment-branches">
      <button type="button" className="pending-branch-toggle" onClick={() => toggleBranch(item.invoiceId, 'charges')}>
        <span>شارژ {item.charges.length.toLocaleString('fa-IR')} مورد</span><strong>{money(item.prepaidTotal)}</strong><b>{chargeOpen ? '−' : '+'}</b>
      </button>
      {chargeOpen && <div className="pending-branch-details">
        {item.charges.map(charge => <div key={charge.id}><span>{money(charge.amount)} تومان</span><small>{charge.method === 'cash' ? 'نقدی' : charge.method === 'card' ? 'کارتخوان' : 'کیف پول'} · {new Date(charge.createdAt).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' })}</small></div>)}
      </div>}
      <button type="button" className="pending-branch-toggle" onClick={() => toggleBranch(item.invoiceId, 'buffet')}>
        <span>بوفه {buffetCount.toLocaleString('fa-IR')} عدد / {item.buffetItems.length.toLocaleString('fa-IR')} قلم</span><strong>{money(item.buffetTotal)}</strong><b>{buffetOpen ? '−' : '+'}</b>
      </button>
      {buffetOpen && <div className="pending-branch-details">
        {item.buffetItems.length ? item.buffetItems.map(row => <div key={row.productId}><span>{row.productName} × {row.quantity.toLocaleString('fa-IR')}</span><small>{money(row.amount)} تومان</small></div>) : <div><span>بدون خرید بوفه</span><small>۰ تومان</small></div>}
      </div>}
      <button type="button" className="pending-branch-toggle" onClick={() => toggleBranch(item.invoiceId, 'calc')}>
        <span>جزئیات محاسبه</span><strong>{money(item.grossAmount)} تومان</strong><b>{calcOpen ? '−' : '+'}</b>
      </button>
      {calcOpen && <div className="pending-branch-details pending-calc-details">
        <div><span>هزینه بازی</span><small>{money(item.timeAmount)}</small></div>
        <div><span>بوفه</span><small>{money(item.buffetTotal)}</small></div>
        {item.creditOrBenefitReduction > 0 && <div><span>اعتبارات/مزایا</span><small>− {money(item.creditOrBenefitReduction)}</small></div>}
        <div><span>شارژ مصرف‌شده</span><small>− {money(item.prepaidApplied)}</small></div>
        {item.prepaidRemaining > 0 && <div><span>اعتبار شارژ باقی‌مانده</span><small>{money(item.prepaidRemaining)}</small></div>}
        <div className="pending-calc-total"><span>قابل پرداخت</span><strong>{money(item.amountDue)}</strong></div>
      </div>}
    </div>;
  }

  if (!payments.length) return <section className="sidebar-section pending-payments-section">
    <div className="sidebar-section-title"><strong>پرداخت‌های در انتظار</strong><span>۰</span></div>
    <div className="sidebar-empty">فعلاً حساب بازِ قابل پرداختی وجود ندارد.</div>
  </section>;

  return <section className="sidebar-section pending-payments-section">
    <div className="sidebar-section-title pending-payment-section-head">
      <div><strong>پرداخت‌های در انتظار</strong><small>{payments.length.toLocaleString('fa-IR')} مشتری</small></div>
      <span>{payments.length.toLocaleString('fa-IR')}</span>
    </div>
    <div className="pending-payment-toolbar">
      <div className="view-switch pending-payment-view-switch" role="group" aria-label="حالت نمایش پرداخت‌های در انتظار">
        {(['v-card', 'v-compact', 'v-list'] as ViewMode[]).map((mode, index) => (
          <button key={mode} type="button" className={view === mode ? 'active' : ''} aria-pressed={view === mode} onClick={() => setView(mode)}>
            <span>{['▦', '▤', '☰'][index]}</span><span>{['کارت', 'فشرده', 'لیست'][index]}</span>
          </button>
        ))}
      </div>
    </div>
    <div className="pending-payment-sort">
      {(Object.keys(labels) as SortKey[]).map(key => {
        const active = sort.key === key;
        return <button key={key} type="button" className={active ? 'active' : ''} aria-pressed={active} onClick={() => toggle(key)}>{labels[key]}{active && ' ' + (sort.direction === 'asc' ? '↑' : '↓')}</button>;
      })}
    </div>
    <div className={'pending-payment-list ' + view}>
      {sorted.map(item => {
        const buffetCount = item.buffetItems.reduce((sum, row) => sum + row.quantity, 0);
        const summary = <div className="pending-payment-summary"><span>شارژ {item.charges.length.toLocaleString('fa-IR')}</span><span>بوفه {buffetCount.toLocaleString('fa-IR')}</span><span>{item.waitingMinutes.toLocaleString('fa-IR')} دقیقه انتظار</span></div>;
        if (view === 'v-list') return <div className="pending-payment-row" key={item.invoiceId}>
          <div className="pending-payment-row-main"><strong>{item.customerName}</strong><small>{item.customerCode || item.username || 'مهمان'} · {item.stationName}</small></div>
          <div>{summary}</div>
          <strong className="pending-payment-row-amount">{money(item.amountDue)} تومان</strong>
          <div className="pending-payment-row-actions">
            {item.amountDue > 0 ? <>
              <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>نقد</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'card')}>کارت</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'wallet')}>کیف</button>
            </> : <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>بستن</button>}
          </div>
          <div className="pending-payment-row-details">{branches(item)}</div>
        </div>;
        if (view === 'v-compact') return <div className="pending-payment-compact-row" key={item.invoiceId}>
          <div className="pending-payment-row-main"><strong>{item.customerName}</strong><small>{item.stationName} · {item.customerCode || item.username || 'مهمان'}</small></div>
          <div className="pending-payment-compact-summary">{summary}</div>
          <strong className="pending-payment-row-amount">{money(item.amountDue)} تومان</strong>
          <div className="pending-payment-row-actions">
            {item.amountDue > 0 ? <>
              <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>نقد</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'card')}>کارت</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'wallet')}>کیف</button>
            </> : <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>بستن</button>}
          </div>
          <div className="pending-payment-row-details">{branches(item)}</div>
        </div>;
        return <article className="sidebar-payment-card pending-payment-card" key={item.invoiceId}>
          <div className="sidebar-payment-head"><div><strong>{item.customerName}</strong><small>{item.customerCode || item.username || 'مهمان'} · {item.stationName}</small></div><b>{money(item.amountDue)} تومان</b></div>
          <small className="sidebar-payment-time">در انتظار {item.waitingMinutes.toLocaleString('fa-IR')} دقیقه · پایان بازی {new Date(item.closedAt).toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' })}</small>
          {summary}
          {branches(item)}
          <div className="pending-payment-actions">
            {item.amountDue > 0 ? <>
              <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>نقد</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'card')}>کارت</button>
              <button type="button" className="btn sm" onClick={() => onPay(item.invoiceId, 'wallet')}>کیف</button>
            </> : <button type="button" className="btn primary sm" onClick={() => onPay(item.invoiceId, 'cash')}>بستن حساب</button>}
          </div>
        </article>;
      })}
    </div>
  </section>;
}
