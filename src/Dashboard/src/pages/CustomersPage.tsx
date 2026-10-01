import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import type { CustomerRecord } from '../types';

const money = (value: number) => new Intl.NumberFormat('fa-IR').format(value);
type Filter = 'all' | 'vip' | 'debt';

export function CustomersPage() {
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [selectedId, setSelectedId] = useState('c1');
  const [filter, setFilter] = useState<Filter>('all');
  const [query, setQuery] = useState('');
  const [action, setAction] = useState('');
  const [amount, setAmount] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => { void mockService.getCustomers().then(setCustomers); }, []);
  const visible = useMemo(() => customers.filter(customer => {
    const matchesFilter = filter === 'all' || (filter === 'vip' ? customer.vip !== 'none' : customer.debt > 0);
    const search = query.trim().toLowerCase();
    const matchesQuery = !search || [customer.code, customer.name, customer.alias, customer.mobile, customer.nationalId, customer.username].some(value => value?.toLowerCase().includes(search));
    return matchesFilter && matchesQuery;
  }), [customers, filter, query]);
  const selected = customers.find(customer => customer.id === selectedId) ?? visible[0];
  const walletTotal = customers.reduce((sum, customer) => sum + customer.wallet, 0);
  const vipCount = customers.filter(customer => customer.vip !== 'none').length;
  const debtCount = customers.filter(customer => customer.debt > 0).length;
  const packageCount = customers.filter(customer => customer.packageName).length;

  function updateCustomer(id: string, patch: Partial<CustomerRecord>, history?: string) {
    setCustomers(current => current.map(customer => customer.id === id ? { ...customer, ...patch, transactionHistory: history ? [history, ...(customer.transactionHistory ?? [])] : customer.transactionHistory } : customer));
  }

  function submitAction() {
    if (action === 'new') {
      const name = window.prompt('نام کامل مشتری');
      if (!name) return;
      const code = String(Math.max(1049, ...customers.map(item => Number(item.code) || 0)) + 1);
      const customer: CustomerRecord = { id: `c${Date.now()}`, code, name, alias: '', mobile: '', nationalId: '', vip: 'none', wallet: 0, debt: 0, giftCredit: 0, discountLevel: 0, username: `user${code}`, lastSeen: 'هرگز', status: 'active', transactionHistory: [] };
      setCustomers(current => [customer, ...current]); setSelectedId(customer.id); setAction(''); setNotice(`مشتری با کد ${code} ساخته شد`); return;
    }
    if (!selected) return;
    const value = Number(amount.replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit))).replace(/[٬,\s]/g, '')) || 0;
    if (action === 'edit') {
      const name = window.prompt('نام مشتری', selected.name);
      if (name) updateCustomer(selected.id, { name }, 'اطلاعات مشتری ویرایش شد');
    } else if (action === 'password') {
      const password = window.prompt('رمز جدید مشتری');
      if (password) setNotice('اطلاعات ورود مشتری به‌روزرسانی شد');
    } else if (action === 'wallet' && value > 0) updateCustomer(selected.id, { wallet: selected.wallet + value }, `شارژ کیف پول · ${money(value)} تومان`);
    else if (action === 'debt' && value > 0) updateCustomer(selected.id, { debt: selected.debt + value }, `ثبت بدهی · ${money(value)} تومان`);
    else if (action === 'gift' && value > 0) updateCustomer(selected.id, { giftCredit: selected.giftCredit + value }, `اعتبار رایگان · ${money(value)} تومان`);
    else if (action === 'package') updateCustomer(selected.id, { packageName: selected.vip === 'silver' ? 'Silver VIP' : 'Gold VIP', dailyHourCap: selected.vip === 'silver' ? 5 : 4 }, 'پکیج VIP فعال شد');
    setAction(''); setAmount(''); setNotice('عملیات مشتری ثبت شد');
  }

  return <>
    <div className="page-header"><div><p>مدیریت مشتریان و حساب‌ها</p><h1>مشتریان</h1></div></div>
    <div className="summary-grid">
      {[['کل مشتریان', customers.length, 'blue'], ['اعضای VIP', vipCount, 'orange'], ['بدهکاران', debtCount, 'red'], ['مجموع کیف پول', `${money(walletTotal)} تومان`, 'green'], ['پکیج فعال', packageCount, 'blue']].map(([label, value, color]) => <div key={label} className="summary-card"><div className="label">{label}</div><div className={`value ${color}`}>{value}</div></div>)}
    </div>
    <div className="customer-layout">
      <section className="customer-list">
        <div className="head"><div className="search-box"><input aria-label="جستجوی مشتری" value={query} onChange={event => setQuery(event.target.value)} placeholder="کد، نام، لقب، موبایل یا کد ملی…" /></div><div className="view-switch">{([['all', 'همه'], ['vip', 'VIP'], ['debt', 'بدهکار']] as [Filter, string][]).map(([key, label]) => <button key={key} className={filter === key ? 'active' : ''} onClick={() => setFilter(key)}>{label}</button>)}</div><button className="btn primary sm" onClick={() => { setAction('new'); submitAction(); }}>+ مشتری</button></div>
        <div className="rows">{visible.map(customer => <button key={customer.id} type="button" className={`customer-row ${customer.id === selected?.id ? 'active' : ''}`} onClick={() => setSelectedId(customer.id)}><span className={`avatar ${customer.vip}`}>{customer.name.slice(0, 1)}</span><span className="main"><b>{customer.name}</b><span>کد {customer.code} · @{customer.username} · {customer.alias}</span></span><span className="fin"><span className={`vip-tag ${customer.vip}`}>{customer.vip === 'gold' ? 'Gold' : customer.vip === 'silver' ? 'Silver' : 'None'}</span><span className="value green">{money(customer.wallet)} ت</span></span></button>)}</div>
      </section>
      {selected && <section className="customer-profile">
        <div className="profile-head"><div className={`profile-avatar ${selected.vip}`}>{selected.name.slice(0, 1)}</div><div><h3>{selected.name}</h3><div className="sub">کد کاربری {selected.code} · @{selected.username} · {selected.alias}</div></div><span className={`vip-tag ${selected.vip}`}>{selected.vip.toUpperCase()}</span></div>
        <div className="profile-stats">{[['کیف پول', selected.wallet, 'green'], ['بدهی', selected.debt, 'red'], ['اعتبار رایگان', selected.giftCredit, 'blue']].map(([label, value, color]) => <div className="profile-stat" key={label}><span className="label">{label}</span><span className={`value ${color}`}>{money(Number(value))} ت</span></div>)}</div>
        <div className="profile-section"><h4>اطلاعات مشتری</h4><div className="info-row"><span>کد کاربری</span><strong>{selected.code}</strong></div><div className="info-row"><span>نام کاربری</span><strong>{selected.username}</strong></div><div className="info-row"><span>موبایل</span><strong>{selected.mobile}</strong></div><div className="info-row"><span>کد ملی</span><strong>{selected.nationalId}</strong></div><div className="info-row"><span>سطح تخفیف</span><strong>{selected.discountLevel}%</strong></div></div>
        <div className="profile-section"><h4>پکیج و محدودیت روزانه</h4><div className="package-box"><div className="title"><span>{selected.packageName ?? 'بدون پکیج فعال'}</span><span className={`vip-tag ${selected.vip}`}>{selected.vip}</span></div><div className="description">قیمت ۱٬۸۰۰٬۰۰۰ تومان · مدت ۳۰ روز · سقف روزانه {selected.dailyHourCap ?? 0} ساعت · تخفیف بوفه ۱۰٪ · مازاد نیم‌بها</div><div className="info-row"><span>مصرف امروز / باقی‌مانده</span><strong>{selected.hoursUsedToday ?? 0} / {Math.max(0, (selected.dailyHourCap ?? 0) - (selected.hoursUsedToday ?? 0))} ساعت</strong></div>{(selected.hoursUsedToday ?? 0) >= (selected.dailyHourCap ?? Infinity) && <strong className="limit-warning">لیمیت خورده · زمان مازاد نیم‌بها محاسبه می‌شود</strong>}</div></div>
        <div className="profile-section"><h4>تاریخچه تراکنش‌ها</h4>{(selected.transactionHistory ?? []).map((item, index) => <div className="info-row" key={`${item}-${index}`}><span>{item}</span><strong>ثبت‌شده</strong></div>)}</div>
        <div className="customer-actions">{[['edit', 'ویرایش'], ['wallet', 'شارژ کیف پول'], ['debt', 'ثبت بدهی'], ['gift', 'اعتبار رایگان'], ['package', 'فعال‌سازی پکیج'], ['password', 'تغییر رمز ورود']].map(([key, label]) => <button key={key} className="btn sm" onClick={() => { setAction(key); if (['wallet', 'debt', 'gift'].includes(key)) setAmount(window.prompt('مبلغ به تومان') ?? ''); else submitActionFor(key); }}>{label}</button>)}</div>
      </section>}
    </div>
    {action && action !== 'new' && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setAction('')}><section className="operation-modal" role="dialog" aria-modal="true"><button className="modal-close" onClick={() => setAction('')}>×</button><h2>{({ wallet: 'شارژ کیف پول', debt: 'ثبت بدهی', gift: 'اعطای اعتبار رایگان', package: 'فعال‌سازی پکیج VIP', edit: 'ویرایش مشتری', password: 'تغییر اطلاعات ورود' } as Record<string, string>)[action]}</h2>{['wallet', 'debt', 'gift'].includes(action) && <label>مبلغ (تومان)<input inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label>}<div className="modal-actions"><button className="btn primary" onClick={submitAction}>ثبت عملیات</button><button className="btn" onClick={() => setAction('')}>لغو</button></div></section></div>}
    {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;

  function submitActionFor(kind: string) { setAction(kind); }
}
