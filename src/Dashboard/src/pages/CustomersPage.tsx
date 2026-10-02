import { useEffect, useMemo, useState } from 'react';
import { mockService } from '../services/mockService';
import { userErrorMessage } from '../utils/userError';
import { getWalletLedger, recordWalletTransaction, refundWalletTransaction } from '../services/walletLedgerService';
import { changeFreeBenefits, getFreeBenefits } from '../services/freeBenefitService';
import type { CustomerRecord, WalletLedgerEntry } from '../types';
import { ApprovalDialog } from '../components/ApprovalDialog';

const money = (value: number) => new Intl.NumberFormat('fa-IR').format(value);
type Filter = 'all' | 'vip' | 'debt';
type CustomerAction = '' | 'new' | 'edit' | 'wallet' | 'debt' | 'gift' | 'freeTime' | 'refund' | 'package' | 'password';

type CustomerDraft = {
  name: string;
  alias: string;
  mobile: string;
  nationalId: string;
  username: string;
  vip: CustomerRecord['vip'];
  password: string;
};

export function CustomersPage({ role = 'operator' }: { role?: 'operator' | 'manager' | 'owner' }) {
  const [customers, setCustomers] = useState<CustomerRecord[]>([]);
  const [selectedId, setSelectedId] = useState('c1');
  const [filter, setFilter] = useState<Filter>('all');
  const [query, setQuery] = useState('');
  const [action, setAction] = useState<CustomerAction>('');
  const [actionNote, setActionNote] = useState('');
  const [amount, setAmount] = useState('');
  const [notice, setNotice] = useState('');
  const [draft, setDraft] = useState<CustomerDraft>({ name: '', alias: '', mobile: '', nationalId: '', username: '', vip: 'none', password: '' });
  const [editName, setEditName] = useState('');
  const [editPassword, setEditPassword] = useState('');
  const [walletLedger, setWalletLedger] = useState<WalletLedgerEntry[]>([]);
  const [serverFreeBenefits, setServerFreeBenefits] = useState<{ freeMoney: number; freeTimeMinutes: number } | null>(null);
  const [refundApproval, setRefundApproval] = useState<{ amount: number; reason: string; sourceTransactionId?: string } | null>(null);
  const [refundSourceId, setRefundSourceId] = useState('');

  useEffect(() => { void mockService.getCustomers().then(setCustomers); }, []);
  const visible = useMemo(() => customers.filter(customer => {
    const matchesFilter = filter === 'all' || (filter === 'vip' ? customer.vip !== 'none' : customer.debt > 0);
    const search = query.trim().toLowerCase();
    const matchesQuery = !search || [customer.code, customer.name, customer.alias, customer.mobile, customer.nationalId, customer.username]
      .some(value => value?.toLowerCase().includes(search));
    return matchesFilter && matchesQuery;
  }), [customers, filter, query]);

  const selected = customers.find(customer => customer.id === selectedId) ?? visible[0];
  const walletTotal = customers.reduce((sum, customer) => sum + customer.wallet, 0);

  useEffect(() => {
    if (!selected?.id) { setWalletLedger([]); return; }
    let active = true;
    void Promise.all([
      getWalletLedger(selected.id),
      getFreeBenefits(selected.id).catch(() => null),
    ]).then(([rows, benefits]) => {
      if (!active) return;
      setWalletLedger(rows);
      if (benefits) {
        setServerFreeBenefits({ freeMoney: benefits.freeMoney, freeTimeMinutes: benefits.freeTimeMinutes });
        setCustomers(current => current.map(item => item.id === selected.id
          ? { ...item, giftCredit: benefits.freeMoney, freeTimeMinutes: benefits.freeTimeMinutes }
          : item));
      } else setServerFreeBenefits(null);
    }).catch(() => { if (active) { setWalletLedger([]); setServerFreeBenefits(null); } });
    return () => { active = false; };
  }, [selected?.id]);


  const vipCount = customers.filter(customer => customer.vip !== 'none').length;
  const debtCount = customers.filter(customer => customer.debt > 0).length;
  const packageCount = customers.filter(customer => customer.packageName).length;

  function openNewCustomer() {
    setDraft({ name: '', alias: '', mobile: '', nationalId: '', username: '', vip: 'none', password: '' });
    setActionNote('');
    setAction('new');
  }

  function openAction(nextAction: Exclude<CustomerAction, '' | 'new'>) {
    if (!selected) {
      setNotice('ابتدا یک مشتری را انتخاب کنید');
      return;
    }
    setAmount('');
    setRefundSourceId('');
    setEditName(selected.name);
    setEditPassword('');
    setAction(nextAction);
  }

  function updateCustomer(id: string, patch: Partial<CustomerRecord>, history?: string) {
    setCustomers(current => current.map(customer => customer.id === id
      ? { ...customer, ...patch, transactionHistory: history ? [history, ...(customer.transactionHistory ?? [])] : customer.transactionHistory }
      : customer));
  }

  function parseAmount(value: string) {
    return Number(value
      .replace(/[۰-۹]/g, digit => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)))
      .replace(/[٬,s]/g, '')) || 0;
  }

  async function executeRefund(refundAmount: number, reason: string, sourceTransactionId?: string) {
    if (!selected) return;
    try {
      const entry = await refundWalletTransaction(selected.id, { amount: refundAmount, reason, sourceTransactionId });
      updateCustomer(selected.id, { wallet: entry.balanceAfter }, 'بازگشت وجه · ' + money(refundAmount) + ' تومان');
      setWalletLedger(current => [entry, ...current]);
      setAction('');
      setActionNote('');
      setRefundApproval(null);
      setRefundSourceId('');
      setAmount('');
      setNotice('بازگشت وجه ثبت شد و در دفتر کیف پول باقی ماند');
    } catch (error) {
      setRefundApproval(null);
      setNotice(userErrorMessage(error, 'ثبت بازگشت وجه انجام نشد'));
    }
  }

  async function submitAction() {
    if (action === 'new') {
      const name = draft.name.trim();
      if (!name) {
        setNotice('نام کامل مشتری را وارد کنید');
        return;
      }
      const code = String(Math.max(1049, ...customers.map(item => Number(item.code) || 0)) + 1);
      const username = draft.username.trim() || `user${code}`;
      const dailyHourCap = draft.vip === 'gold' ? 4 : draft.vip === 'silver' ? 5 : undefined;
      const customer: CustomerRecord = {
        id: `c${Date.now()}`,
        code,
        name,
        alias: draft.alias.trim(),
        mobile: draft.mobile.trim(),
        nationalId: draft.nationalId.trim(),
        vip: draft.vip,
        wallet: 0,
        debt: 0,
        giftCredit: 0,
        discountLevel: draft.vip === 'gold' ? 15 : draft.vip === 'silver' ? 10 : 0,
        packageName: draft.vip === 'none' ? undefined : draft.vip === 'gold' ? 'Gold VIP' : 'Silver VIP',
        username,
        lastSeen: 'هرگز',
        status: 'active',
        hoursUsedToday: 0,
        dailyHourCap,
        transactionHistory: [],
      };
      setCustomers(current => [customer, ...current]);
      setSelectedId(customer.id);
      setAction('');
      setNotice(`مشتری با کد ${code} ساخته شد`);
      return;
    }

    if (!selected) return;
    const value = parseAmount(amount);

    if (action === 'edit') {
      const name = editName.trim();
      if (!name) { setNotice('نام مشتری نمی‌تواند خالی باشد'); return; }
      updateCustomer(selected.id, { name }, 'اطلاعات مشتری ویرایش شد');
      setAction('');
      setNotice('اطلاعات مشتری ویرایش شد');
      return;
    }

    if (action === 'refund') {
      if (value <= 0) { setNotice('مبلغ معتبر وارد کنید'); return; }
      if (value > selected.wallet) { setNotice('مبلغ بازگشت بیشتر از موجودی کیف پول مشتری است'); return; }
      if (!actionNote.trim()) { setNotice('دلیل بازگشت وجه را وارد کنید'); return; }
      if (!refundSourceId) { setNotice('تراکنش مبدأ بازگشت وجه را انتخاب کنید'); return; }
      if (role === 'operator' && value > 100000) {
        setRefundApproval({ amount: value, reason: actionNote.trim(), sourceTransactionId: refundSourceId });
        return;
      }
      await executeRefund(value, actionNote.trim(), refundSourceId);
      return;
    }
    if (action === 'password') {
      if (!editPassword.trim()) { setNotice('رمز جدید را وارد کنید'); return; }
      setAction('');
      setNotice('رمز ورود مشتری به‌روزرسانی شد');
      return;
    }

    if (['wallet', 'debt', 'gift'].includes(action) && value <= 0) {
      setNotice('مبلغ معتبر وارد کنید');
      return;
    }

    if (action === 'wallet') {
      try {
        const entry = await recordWalletTransaction(selected.id, { amount: value, type: 'credit', description: 'شارژ کیف پول توسط اپراتور' });
        updateCustomer(selected.id, { wallet: entry.balanceAfter }, 'شارژ کیف پول · ' + money(value) + ' تومان');
        setWalletLedger(current => [entry, ...current]);
      } catch (error) {
        setNotice(userErrorMessage(error, 'ثبت شارژ کیف پول انجام نشد'));
        return;
      }
    }
    else if (action === 'debt') updateCustomer(selected.id, { debt: selected.debt + value }, `ثبت بدهی · ${money(value)} تومان`);
    else if (action === 'gift') {
      try {
        const benefits = await changeFreeBenefits(selected.id, { moneyAmount: value, mode: 'credit', description: 'اعطای اعتبار مالی رایگان توسط اپراتور' });
        if (benefits) {
          updateCustomer(selected.id, { giftCredit: benefits.freeMoney, freeTimeMinutes: benefits.freeTimeMinutes }, 'اعتبار مالی رایگان · ' + money(value) + ' تومان');
          setServerFreeBenefits({ freeMoney: benefits.freeMoney, freeTimeMinutes: benefits.freeTimeMinutes });
        } else {
          updateCustomer(selected.id, { giftCredit: selected.giftCredit + value }, 'اعتبار مالی رایگان · ' + money(value) + ' تومان');
        }
      } catch (error) {
        setNotice(userErrorMessage(error, 'ثبت اعتبار مالی رایگان انجام نشد'));
        return;
      }
    }
    else if (action === 'freeTime') {
      const minutes = value;
      if (minutes <= 0) { setNotice('تعداد دقیقه معتبر وارد کنید'); return; }
      try {
        const benefits = await changeFreeBenefits(selected.id, { minutes, mode: 'credit', description: 'اعطای زمان رایگان توسط اپراتور' });
        if (benefits) {
          updateCustomer(selected.id, { freeTimeMinutes: benefits.freeTimeMinutes, giftCredit: benefits.freeMoney }, minutes + ' دقیقه زمان رایگان');
          setServerFreeBenefits({ freeMoney: benefits.freeMoney, freeTimeMinutes: benefits.freeTimeMinutes });
        } else {
          updateCustomer(selected.id, { freeTimeMinutes: (selected.freeTimeMinutes ?? 0) + minutes }, minutes + ' دقیقه زمان رایگان');
        }
      } catch (error) {
        setNotice(userErrorMessage(error, 'ثبت زمان رایگان انجام نشد'));
        return;
      }
    }
    else if (action === 'package') {
      const vip = selected.vip === 'none' ? 'silver' : selected.vip;
      updateCustomer(selected.id, {
        vip,
        packageName: vip === 'gold' ? 'Gold VIP' : 'Silver VIP',
        dailyHourCap: vip === 'gold' ? 4 : 5,
      }, 'پکیج VIP فعال شد');
    }

    setAction('');
    setAmount('');
    setNotice('عملیات مشتری ثبت شد');
  }

  return <>
    <div className="page-header"><div><p>مدیریت مشتریان و حساب‌ها</p><h1>مشتریان</h1></div></div>

    <div className="summary-grid">
      {[['کل مشتریان', customers.length, 'blue'], ['اعضای VIP', vipCount, 'orange'], ['بدهکاران', debtCount, 'red'], ['مجموع کیف پول', `${money(walletTotal)} تومان`, 'green'], ['پکیج فعال', packageCount, 'blue']]
        .map(([label, value, color]) => <div key={label} className="summary-card"><div className="label">{label}</div><div className={`value ${color}`}>{value}</div></div>)}
    </div>

    <div className="customer-layout">
      <section className="customer-list">
        <div className="head">
          <div className="search-box"><input aria-label="جستجوی مشتری" value={query} onChange={event => setQuery(event.target.value)} placeholder="کد، نام، لقب، موبایل یا کد ملی…" /></div>
          <div className="view-switch">{([['all', 'همه'], ['vip', 'VIP'], ['debt', 'بدهکار']] as [Filter, string][]).map(([key, label]) =>
            <button key={key} className={filter === key ? 'active' : ''} onClick={() => setFilter(key)}>{label}</button>)}</div>
          <button className="btn primary sm" onClick={openNewCustomer}>+ مشتری جدید</button>
        </div>
        <div className="rows">{visible.map(customer =>
          <button key={customer.id} type="button" className={`customer-row ${customer.id === selected?.id ? 'active' : ''}`} onClick={() => setSelectedId(customer.id)}>
            <span className={`avatar ${customer.vip}`}>{customer.name.slice(0, 1)}</span>
            <span className="main"><b>{customer.name}</b><span>کد {customer.code} · @{customer.username}</span><small>{customer.mobile || 'بدون موبایل'} · {customer.alias || 'بدون لقب'}</small></span>
            <span className="fin"><span className={`vip-tag ${customer.vip}`}>{customer.vip === 'gold' ? 'طلایی' : customer.vip === 'silver' ? 'نقره‌ای' : 'عادی'}</span><strong className="customer-row-wallet">{money(customer.wallet)} تومان</strong>{customer.debt > 0 && <strong className="customer-row-debt">بدهی {money(customer.debt)} تومان</strong>}<small className={`customer-state ${customer.status}`}>{customer.status === 'active' ? 'فعال' : customer.status === 'warning' ? 'نیازمند توجه' : 'قفل'}</small></span>
          </button>)}</div>
      </section>

      {selected && <section className="customer-profile">
        <div className="profile-head"><div className={`profile-avatar ${selected.vip}`}>{selected.name.slice(0, 1)}</div><div><h3>{selected.name}</h3><div className="sub">کد کاربری {selected.code} · @{selected.username} · {selected.alias || 'بدون لقب'}</div></div><span className={`vip-tag ${selected.vip}`}>{selected.vip === 'gold' ? 'طلایی' : selected.vip === 'silver' ? 'نقره‌ای' : 'عادی'}</span></div>
        <div className="profile-stats">{[['کیف پول', selected.wallet, 'green'], ['بدهی', selected.debt, 'red'], ['اعتبار رایگان', selected.giftCredit, 'blue']].map(([label, value, color]) =>
          <div className="profile-stat" key={label}><span className="label">{label}</span><span className={`value ${color}`}>{money(Number(value))} ت</span></div>)}</div>

        <div className="profile-section"><h4>اطلاعات مشتری</h4>
          <div className="info-row"><span>کد کاربری</span><strong>{selected.code}</strong></div>
          <div className="info-row"><span>نام کاربری</span><strong>{selected.username}</strong></div>
          <div className="info-row"><span>موبایل</span><strong>{selected.mobile || '—'}</strong></div>
          <div className="info-row"><span>کد ملی</span><strong>{selected.nationalId || '—'}</strong></div>
          <div className="info-row"><span>سطح تخفیف</span><strong>{selected.discountLevel}%</strong></div>
        </div>

        <div className="profile-section"><h4>پکیج و محدودیت روزانه</h4>
          <div className="package-box">
            <div className="title"><span>{selected.packageName ?? 'بدون پکیج فعال'}</span><span className={`vip-tag ${selected.vip}`}>{selected.vip}</span></div>
            <div className="description">قیمت نمونه پکیج · مدت ۳۰ روز · سقف روزانه {selected.dailyHourCap ?? 0} ساعت · تخفیف بوفه ۱۰٪ · مازاد نیم‌بها</div>
            <div className="info-row"><span>مصرف امروز / باقی‌مانده</span><strong>{selected.hoursUsedToday ?? 0} / {Math.max(0, (selected.dailyHourCap ?? 0) - (selected.hoursUsedToday ?? 0))} ساعت</strong></div>
            {(selected.hoursUsedToday ?? 0) >= (selected.dailyHourCap ?? Infinity) && <strong className="limit-warning">لیمیت خورده · زمان مازاد نیم‌بها محاسبه می‌شود</strong>}
          </div>
        </div>

        <div className="profile-section">
          <div className="profile-section-head">
            <h4>دفتر کیف پول</h4>
            <span>{walletLedger.length.toLocaleString('fa-IR')} تراکنش</span>
          </div>
          {walletLedger.length === 0 ? (
            <div className="customer-ledger-empty">هنوز تراکنش کیف پولی برای این مشتری ثبت نشده است.</div>
          ) : (
            <div className="customer-ledger">
              {walletLedger.slice(0, 12).map(entry => (
                <div className="customer-ledger-row" key={entry.id}>
                  <span className={'customer-ledger-sign ' + entry.direction}>{entry.direction === 'credit' ? '+' : '−'}</span>
                  <div className="customer-ledger-main">
                    <strong>{entry.description}</strong>
                    <small>{new Date(entry.createdAt).toLocaleString('fa-IR', { dateStyle: 'short', timeStyle: 'short' })}</small>
                  </div>
                  <div className="customer-ledger-money">
                    <strong>{entry.direction === 'credit' ? '+' : '−'} {money(entry.amount)} تومان</strong>
                    <small>مانده {money(entry.balanceAfter)} تومان</small>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="profile-section"><div className="profile-section-head"><h4>تاریخچه تراکنش‌ها</h4><span>{selected.transactionHistory?.length ?? 0} مورد</span></div><div className="customer-history-list">{(selected.transactionHistory ?? []).map((item, index) =>
          <div className="customer-history-item" key={`${item}-${index}`}><span className="customer-history-dot" /><div><strong>{item}</strong><small>{index === 0 ? 'آخرین فعالیت' : 'ثبت‌شده در سابقه مشتری'}</small></div></div>)}</div></div>

        <div className="customer-actions">
          <button className="btn sm" onClick={() => openAction('edit')}>ویرایش</button>
          <button className="btn sm" onClick={() => openAction('wallet')}>شارژ کیف پول</button>
          <button className="btn sm" onClick={() => openAction('debt')}>ثبت بدهی</button>
          <button className="btn sm" onClick={() => openAction('gift')}>اعتبار رایگان</button><button className="btn sm" onClick={() => openAction('freeTime')}>زمان رایگان</button>
          <button className="btn sm" onClick={() => openAction('package')}>فعال‌سازی/تغییر VIP</button>
          <button className="btn sm" onClick={() => openAction('refund')}>بازگشت وجه</button>
          <button className="btn sm" onClick={() => openAction('password')}>تغییر رمز ورود</button>
        </div>
      </section>}
    </div>

    {action && <div className="modal-backdrop" onMouseDown={event => event.target === event.currentTarget && setAction('')}>
      <section className="operation-modal" role="dialog" aria-modal="true">
        <button className="modal-close" onClick={() => setAction('')}>×</button>

        {action === 'new' && <>
          <h2>مشتری جدید</h2>
          <label>نام کامل<input autoFocus value={draft.name} onChange={event => setDraft(current => ({ ...current, name: event.target.value }))} /></label>
          <label>لقب<input value={draft.alias} onChange={event => setDraft(current => ({ ...current, alias: event.target.value }))} /></label>
          <div className="modal-grid-2"><label>موبایل<input dir="ltr" value={draft.mobile} onChange={event => setDraft(current => ({ ...current, mobile: event.target.value }))} /></label><label>کد ملی<input dir="ltr" value={draft.nationalId} onChange={event => setDraft(current => ({ ...current, nationalId: event.target.value }))} /></label></div>
          <div className="modal-grid-2"><label>نام کاربری<input dir="ltr" value={draft.username} onChange={event => setDraft(current => ({ ...current, username: event.target.value }))} placeholder="در صورت نیاز دستی وارد کنید" /></label><label>VIP اولیه<select value={draft.vip} onChange={event => setDraft(current => ({ ...current, vip: event.target.value as CustomerRecord['vip'] }))}><option value="none">بدون VIP</option><option value="silver">Silver</option><option value="gold">Gold</option></select></label></div>
          <label>رمز ورود<input dir="ltr" type="password" value={draft.password} onChange={event => setDraft(current => ({ ...current, password: event.target.value }))} placeholder="اختیاری" /></label>
          <div className="modal-actions"><button className="btn primary" onClick={submitAction}>ساخت مشتری</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {action === 'edit' && <>
          <h2>ویرایش مشتری · {selected?.name}</h2>
          <label>نام کامل<input autoFocus value={editName} onChange={event => setEditName(event.target.value)} /></label>
          <div className="modal-actions"><button className="btn primary" onClick={submitAction}>ذخیره تغییرات</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {action === 'password' && <>
          <h2>تغییر رمز ورود · {selected?.name}</h2>
          <label>رمز جدید<input autoFocus dir="ltr" type="password" value={editPassword} onChange={event => setEditPassword(event.target.value)} /></label>
          <div className="modal-actions"><button className="btn primary" onClick={submitAction}>ذخیره رمز</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {['wallet', 'debt', 'gift'].includes(action) && <>
          <h2>{({ wallet: 'شارژ کیف پول', debt: 'ثبت بدهی', gift: 'اعتبار رایگان', refund: 'کسر اعتبار / بازگشت وجه' } as Record<string, string>)[action]} · {selected?.name}</h2>
          <label>{action === 'gift' ? 'مبلغ (تومان)' : 'مقدار (تومان)'}<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label>
          
          <div className="modal-actions"><button className="btn primary" onClick={submitAction}>ثبت عملیات</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {action === 'refund' && <>
          <h2>بازگشت وجه · {selected?.name}</h2>
          <label>تراکنش مبدأ
            <select value={refundSourceId} onChange={event => setRefundSourceId(event.target.value)}>
              <option value="">انتخاب کنید</option>
              {walletLedger.filter(entry => entry.direction === 'credit' && entry.type !== 'refund').map(entry => (
                <option key={entry.id} value={entry.id}>
                  {money(entry.amount)} تومان · {entry.description}
                </option>
              ))}
            </select>
          </label>
          <label>مبلغ بازگشت (تومان)<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label>
          <label>دلیل بازگشت<textarea value={actionNote} onChange={event => setActionNote(event.target.value)} placeholder="مثلاً لغو شارژ به درخواست مشتری" /></label>
          <div className="modal-actions"><button className="btn danger" onClick={() => void submitAction()}>ثبت بازگشت وجه</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {action === 'freeTime' && <>
          <h2>زمان رایگان · {selected?.name}</h2>
          <div className="info-row"><span>زمان رایگان فعلی</span><strong>{money(serverFreeBenefits?.freeTimeMinutes ?? selected?.freeTimeMinutes ?? 0)} دقیقه</strong></div>
          <label>تعداد دقیقه اضافه<input autoFocus inputMode="numeric" value={amount} onChange={event => setAmount(event.target.value)} /></label>
          <div className="modal-actions"><button className="btn primary" onClick={() => void submitAction()}>ثبت زمان رایگان</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}

        {action === 'package' && <>
          <h2>پکیج VIP · {selected?.name}</h2>
          <div className="info-row"><span>پکیج فعلی</span><strong>{selected?.packageName ?? 'بدون پکیج'}</strong></div>
          <div className="info-row"><span>مصرف امروز</span><strong>{selected?.hoursUsedToday ?? 0} ساعت</strong></div>
          <label>سطح جدید<select value={selected?.vip === 'gold' ? 'gold' : selected?.vip === 'silver' ? 'silver' : 'none'} onChange={event => {
            const vip = event.target.value as CustomerRecord['vip'];
            setCustomers(current => current.map(item => item.id === selected?.id ? {
              ...item,
              vip,
              packageName: vip === 'gold' ? 'Gold VIP' : vip === 'silver' ? 'Silver VIP' : undefined,
              dailyHourCap: vip === 'gold' ? 4 : vip === 'silver' ? 5 : undefined,
            } : item));
          }}><option value="none">بدون VIP</option><option value="silver">Silver VIP · ۵ ساعت/روز</option><option value="gold">Gold VIP · ۴ ساعت/روز</option></select></label>
          <div className="modal-actions"><button className="btn primary" onClick={() => { setAction(''); setNotice('پکیج VIP به‌روزرسانی شد'); }}>ذخیره پکیج</button><button className="btn" onClick={() => setAction('')}>انصراف</button></div>
        </>}
      </section>
    </div>}

    {refundApproval && (
      <ApprovalDialog
        open
        title="تأیید بازگشت وجه"
        detail={'بازگشت ' + money(refundApproval.amount) + ' تومان برای «' + (selected?.name ?? 'مشتری') + '» به تأیید مدیر نیاز دارد. دلیل: ' + refundApproval.reason}
        requestLabel="تأیید و ثبت بازگشت وجه"
        onReject={() => setRefundApproval(null)}
        onApprove={() => executeRefund(refundApproval.amount, refundApproval.reason, refundApproval.sourceTransactionId)}
      />
    )}

    {notice && <div className="operation-toast" role="status">{notice}<button onClick={() => setNotice('')}>×</button></div>}
  </>;
}
