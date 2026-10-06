import { useMemo } from 'react';
import type { CustomerRecord, StationDto } from '../../types';

type FlowAction = 'walletAdd' | 'debtAdd' | 'walletDeduct' | 'walletDebt';

type Props = {
  customers: CustomerRecord[];
  stations: StationDto[];
  hotkeys: Record<string, string>;
  customerId: string | null;
  search: string;
  amount: string;
  busy?: boolean;
  onSearchChange: (value: string) => void;
  onSearchSubmit: () => void;
  onSelectCustomer: (customer: CustomerRecord) => void;
  onAmountChange: (value: string) => void;
  onAction: (action: FlowAction) => void;
};

const actionDefinitions: Array<{ key: FlowAction; title: string; detail: string; tone: string; hotkeyKey: string }> = [
  { key: 'walletAdd', title: 'شارژ کیف پول', detail: 'افزایش موجودی قابل‌مصرف مشتری', tone: 'primary', hotkeyKey: 'walletAdd' },
  { key: 'debtAdd', title: 'ثبت بدهی', detail: 'افزودن مستقیم مبلغ به بدهی مشتری', tone: 'warning', hotkeyKey: 'debtAdd' },
  { key: 'walletDeduct', title: 'کسر از کیف پول', detail: 'برداشت از موجودی بدون ایجاد بدهی', tone: 'danger', hotkeyKey: 'walletDeduct' },
  { key: 'walletDebt', title: 'کیف پول + بدهی', detail: 'اول کیف پول، باقی‌مانده به بدهی', tone: 'neutral', hotkeyKey: 'walletDebt' },
];

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(Math.round(value));
}

function vipLabel(value: CustomerRecord['vip']) {
  if (value === 'gold') return 'طلایی';
  if (value === 'silver') return 'نقره‌ای';
  if (value === 'bronze') return 'برنزی';
  if (value === 'custom') return 'ویژه';
  return 'عادی';
}

function findMatches(customers: CustomerRecord[], search: string) {
  const needle = search.trim().toLocaleLowerCase('fa-IR');
  if (!needle) return [];
  return customers
    .map(customer => {
      const fields = [customer.code, customer.username, customer.mobile, customer.id, customer.name, customer.alias]
        .filter(Boolean)
        .map(value => String(value).toLocaleLowerCase('fa-IR'));
      const exact = fields.some(value => value === needle);
      const partial = fields.some(value => value.includes(needle));
      return { customer, score: exact ? 3 : partial ? 1 : 0 };
    })
    .filter(item => item.score > 0)
    .sort((a, b) => b.score - a.score || a.customer.name.localeCompare(b.customer.name, 'fa'))
    .slice(0, 6)
    .map(item => item.customer);
}

export function CustomerOperationsWorkspace({
  customers,
  stations,
  hotkeys,
  customerId,
  search,
  amount,
  busy = false,
  onSearchChange,
  onSearchSubmit,
  onSelectCustomer,
  onAmountChange,
  onAction,
}: Props) {
  const customer = customers.find(item => item.id === customerId) ?? null;
  const matches = useMemo(() => findMatches(customers, search), [customers, search]);
  const activeStation = customer
    ? stations.find(station =>
        station.customerUsername === customer.username ||
        station.customerCode === customer.code ||
        station.customerCode === customer.username
      ) ?? null
    : null;
  const actions = actionDefinitions.map(item => ({
    ...item,
    hotkey: hotkeys[item.hotkeyKey] ||
      (item.key === 'walletAdd'
        ? hotkeys.walletAdd || 'F5'
        : item.key === 'debtAdd'
          ? hotkeys.debtAdd || 'F6'
          : item.key === 'walletDeduct'
            ? hotkeys.walletDeduct || 'F7'
            : hotkeys.walletDebt || 'F8'),
  }));

  return (
    <div className="customer-flow-workspace">
      <div className="customer-flow-kicker">
        <span>Workspace مشتری</span>
        <div className="customer-flow-shortcuts">
          <span>{hotkeys.flow || 'F1'} جستجو</span>
          <span>{hotkeys.amount || 'F4'} مبلغ</span>
          <span>{hotkeys.walletAdd || 'F5'}–{hotkeys.walletDebt || 'F8'} عملیات</span>
        </div>
      </div>

      {customer ? (
        <>
          <div className="customer-flow-profile">
            <section className="customer-flow-identity">
              <div className="customer-flow-avatar">{customer.name.trim().charAt(0) || 'م'}</div>
              <div className="customer-flow-identity-text">
                <strong>{customer.name}</strong>
                <span>@{customer.username} · {customer.code || 'بدون کد'} · {customer.mobile || 'بدون موبایل'}</span>
                <div className="customer-flow-badges">
                  <span className={'vip-tag ' + customer.vip}>{vipLabel(customer.vip)}</span>
                  <span className={'status-pill ' + customer.status}>{customer.status === 'active' ? 'فعال' : customer.status === 'warning' ? 'هشدار' : 'مسدود'}</span>
                </div>
              </div>
              <span className="customer-flow-selected">پروفایل فعال</span>
            </section>

            <div className="customer-flow-stats">
              <div><span>کیف پول</span><strong className="positive">{money(customer.wallet)} تومان</strong></div>
              <div><span>بدهی</span><strong className={customer.debt > 0 ? 'negative' : ''}>{money(customer.debt)} تومان</strong></div>
              <div><span>اعتبار رایگان</span><strong>{money(customer.giftCredit)} تومان</strong></div>
              <div><span>تخفیف</span><strong>{money(customer.discountLevel)}٪</strong></div>
            </div>

            <div className="customer-flow-live">
              <div>
                <span>وضعیت حضور</span>
                <strong>{activeStation ? 'روی ' + activeStation.name : 'در حال حاضر روی دستگاهی نیست'}</strong>
              </div>
              <div>
                <span>زمان باقیمانده</span>
                <strong>{activeStation?.remainingMinutes != null ? money(activeStation.remainingMinutes) + ' دقیقه' : '—'}</strong>
              </div>
              <div>
                <span>یادداشت</span>
                <strong>{customer.notes || activeStation?.customerNote || 'یادداشتی ثبت نشده'}</strong>
              </div>
            </div>
          </div>

          <section className="customer-flow-action-panel">
            <div className="customer-flow-section-head">
              <div>
                <strong>عملیات مالی سریع</strong>
                <span>مبلغ را وارد کن، سپس یکی از عملیات را با کلیک یا میانبر اجرا کن.</span>
              </div>
              <span className="customer-flow-selected">@{customer.username}</span>
            </div>
            <label className="customer-flow-amount">
              <span>مبلغ عملیات · تومان</span>
              <input
                id="flow-amount"
                autoComplete="off"
                inputMode="numeric"
                value={amount}
                onChange={event => onAmountChange(event.target.value)}
                onKeyDown={event => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    onAction('walletAdd');
                  }
                }}
                placeholder="مثلاً ۱۰۰٬۰۰۰"
              />
            </label>
            <div className="customer-flow-actions">
              {actions.map(action => (
                <button
                  type="button"
                  key={action.key}
                  className={'customer-flow-action ' + action.tone}
                  onClick={() => onAction(action.key)}
                  disabled={busy}
                >
                  <span className="customer-flow-action-key">{action.hotkey}</span>
                  <strong>{action.title}</strong>
                  <small>{action.detail}</small>
                </button>
              ))}
            </div>
          </section>

          <section className="customer-flow-history">
            <div className="customer-flow-section-head">
              <div>
                <strong>آخرین فعالیت‌ها</strong>
                <span>پس از هر عملیات، اطلاعات از Server دوباره خوانده می‌شود.</span>
              </div>
            </div>
            {customer.transactionHistory?.length ? (
              <div className="customer-flow-history-list">
                {customer.transactionHistory.slice(0, 5).map((item, index) => <div key={index}><span>●</span><strong>{item}</strong></div>)}
              </div>
            ) : (
              <div className="customer-flow-empty-history">برای این مشتری سابقه‌ای در خلاصه پروفایل ثبت نشده است.</div>
            )}
          </section>
        </>
      ) : (
        <section className="customer-flow-empty">
          <div className="customer-flow-empty-icon">⌕</div>
          <strong>مشتری را پیدا و انتخاب کن</strong>
          <span>کد مشتری، نام کاربری، موبایل یا نام را در نوار جستجوی پایین وارد کن و Enter بزن.</span>
        </section>
      )}

      <div className="customer-flow-searchbar">
        <label>
          <span>جستجوی مشتری</span>
          <input
            autoFocus={!customer}
            value={search}
            onChange={event => onSearchChange(event.target.value)}
            onKeyDown={event => {
              if (event.key === 'Enter') {
                event.preventDefault();
                onSearchSubmit();
              }
            }}
            placeholder="کد، username، موبایل یا نام مشتری"
          />
        </label>
        <button type="button" className="btn primary" onClick={onSearchSubmit} disabled={!search.trim() || busy}>نمایش پروفایل</button>
      </div>

      {!customer && matches.length > 0 && (
        <div className="customer-flow-search-results">
          {matches.map(item => (
            <button type="button" key={item.id} onClick={() => onSelectCustomer(item)}>
              <span>
                <strong>{item.name}</strong>
                <small>@{item.username} · {item.code || item.mobile || 'بدون شناسه'}</small>
              </span>
              <em>{money(item.wallet)} تومان</em>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
