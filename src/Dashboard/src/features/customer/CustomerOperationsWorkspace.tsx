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

const actionDefinitions: Array<{
  key: FlowAction;
  title: string;
  detail: string;
  tone: string;
  hotkeyKey: string;
}> = [
  { key: 'walletAdd', title: 'شارژ کیف پول', detail: 'افزایش موجودی قابل‌مصرف مشتری', tone: 'primary', hotkeyKey: 'walletAdd' },
  { key: 'debtAdd', title: 'ثبت بدهی', detail: 'افزودن مستقیم مبلغ به بدهی مشتری', tone: 'warning', hotkeyKey: 'debtAdd' },
  { key: 'walletDeduct', title: 'کسر از کیف پول', detail: 'برداشت از موجودی بدون ایجاد بدهی', tone: 'danger', hotkeyKey: 'walletDeduct' },
  { key: 'walletDebt', title: 'کیف پول + بدهی', detail: 'ابتدا کیف پول، باقی‌مانده به بدهی', tone: 'neutral', hotkeyKey: 'walletDebt' },
];

function money(value: number) {
  return new Intl.NumberFormat('fa-IR').format(Math.round(value));
}

function minutes(value: number | null | undefined) {
  if (value == null) return '—';
  const total = Math.max(0, Math.round(value));
  const hours = Math.floor(total / 60);
  const mins = total % 60;
  if (hours === 0) return mins + ' دقیقه';
  if (mins === 0) return hours + ' ساعت';
  return hours + ' ساعت و ' + mins + ' دقیقه';
}

function vipLabel(value: CustomerRecord['vip']) {
  if (value === 'gold') return 'طلایی';
  if (value === 'silver') return 'نقره‌ای';
  if (value === 'bronze') return 'برنزی';
  if (value === 'custom') return 'ویژه';
  return 'عادی';
}

function statusLabel(value: CustomerRecord['status']) {
  if (value === 'active') return 'فعال';
  if (value === 'warning') return 'نیازمند توجه';
  return 'مسدود';
}

function stationStateLabel(value: StationDto['state']) {
  if (value === 'busy') return 'در حال استفاده';
  if (value === 'paused') return 'متوقف';
  if (value === 'reserved') return 'رزرو';
  if (value === 'off') return 'خارج از سرویس';
  return 'آماده';
}

function findMatches(customers: CustomerRecord[], search: string) {
  const needle = search.trim().toLocaleLowerCase('fa-IR');
  if (!needle) return [];

  return customers
    .map(customer => {
      const fields = [
        customer.code,
        customer.username,
        customer.mobile,
        customer.id,
        customer.name,
        customer.alias,
        customer.nationalId,
      ]
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
        station.customerCode === customer.username ||
        station.customerFullName === customer.name
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
      <section className="customer-flow-search-panel">
        <div className="customer-flow-search-heading">
          <div>
            <span className="customer-flow-eyebrow">ورود سریع مشتری</span>
            <strong>مشتری را از همین پنجره پیدا و انتخاب کن</strong>
          </div>
          <div className="customer-flow-shortcuts">
            <span>{hotkeys.flow || 'F1'} Workspace</span>
            <span>{hotkeys.amount || 'F4'} مبلغ</span>
            <span>{hotkeys.walletAdd || 'F5'}–{hotkeys.walletDebt || 'F8'} عملیات</span>
          </div>
        </div>

        <div className="customer-flow-search-row">
          <label>
            <span>شناسه مشتری، username، موبایل، کد ملی یا نام</span>
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
              placeholder="مثلاً 1006 یا ali123 یا 0912..."
              disabled={busy}
            />
          </label>
          <button type="button" className="btn primary" onClick={onSearchSubmit} disabled={!search.trim() || busy}>
            نمایش مشتری
          </button>
        </div>

        {!customer && matches.length > 0 && (
          <div className="customer-flow-search-results">
            {matches.map(item => (
              <button type="button" key={item.id} onClick={() => onSelectCustomer(item)} disabled={busy}>
                <span>
                  <strong>{item.name}</strong>
                  <small>@{item.username} · {item.code || item.mobile || 'بدون شناسه'}</small>
                </span>
                <em>{money(item.wallet)} تومان</em>
              </button>
            ))}
          </div>
        )}
      </section>

      {customer ? (
        <>
          <section className="customer-flow-profile">
            <div className="customer-flow-profile-head">
              <div className="customer-flow-identity">
                <div className="customer-flow-avatar">{customer.name.trim().charAt(0) || 'م'}</div>
                <div className="customer-flow-identity-text">
                  <div className="customer-flow-name-line">
                    <strong>{customer.name}</strong>
                    <span className={'status-pill ' + customer.status}>{statusLabel(customer.status)}</span>
                    <span className={'vip-tag ' + customer.vip}>{vipLabel(customer.vip)}</span>
                  </div>
                  <span className="customer-flow-meta">
                    @{customer.username} · کد {customer.code || '—'} · موبایل {customer.mobile || '—'}
                  </span>
                </div>
              </div>
              <div className="customer-flow-profile-tags">
                <span>پروفایل فعال</span>
                {customer.alias && <span>نام مستعار: {customer.alias}</span>}
              </div>
            </div>

            <div className="customer-flow-info-grid">
              <div>
                <span>کد ملی</span>
                <strong>{customer.nationalId || 'ثبت نشده'}</strong>
              </div>
              <div>
                <span>پکیج / تعرفه</span>
                <strong>{customer.packageName || 'بدون پکیج'}</strong>
              </div>
              <div>
                <span>سقف ورود همزمان</span>
                <strong>{customer.concurrentLoginLimit ? money(customer.concurrentLoginLimit) + ' دستگاه' : 'پیش‌فرض سیستم'}</strong>
              </div>
              <div>
                <span>یادداشت</span>
                <strong>{customer.notes || 'بدون یادداشت'}</strong>
              </div>
            </div>
          </section>

          <section className="customer-flow-finance">
            <div className="customer-flow-section-head">
              <div>
                <span className="customer-flow-eyebrow">وضعیت مالی</span>
                <strong>اعتبار و بدهی</strong>
              </div>
              <span className="customer-flow-selected">آخرین وضعیت از Server</span>
            </div>

            <div className="customer-flow-stats">
              <div className="highlight">
                <span>کیف پول</span>
                <strong className="positive">{money(customer.wallet)} تومان</strong>
              </div>
              <div className={customer.debt > 0 ? 'danger-stat' : ''}>
                <span>بدهی</span>
                <strong className={customer.debt > 0 ? 'negative' : ''}>{money(customer.debt)} تومان</strong>
              </div>
              <div>
                <span>اعتبار رایگان</span>
                <strong>{money(customer.giftCredit)} تومان</strong>
              </div>
              <div>
                <span>زمان رایگان</span>
                <strong>{minutes(customer.freeTimeMinutes)}</strong>
              </div>
              <div>
                <span>تخفیف</span>
                <strong>{money(customer.discountLevel)}٪</strong>
              </div>
              <div>
                <span>مصرف امروز</span>
                <strong>{customer.hoursUsedToday != null ? money(customer.hoursUsedToday) + ' ساعت' : '—'}</strong>
              </div>
            </div>
          </section>

          <section className="customer-flow-live-panel">
            <div className="customer-flow-section-head">
              <div>
                <span className="customer-flow-eyebrow">وضعیت لحظه‌ای</span>
                <strong>جلسه و دستگاه</strong>
              </div>
              <span className="customer-flow-selected">
                {activeStation ? 'جلسه فعال' : 'بدون دستگاه فعال'}
              </span>
            </div>

            {activeStation ? (
              <div className="customer-flow-live-grid">
                <div className="customer-flow-live-primary">
                  <span>دستگاه فعلی</span>
                  <strong>{activeStation.name}</strong>
                  <small>{stationStateLabel(activeStation.state)} · {activeStation.zone === 'pc' ? 'PC' : activeStation.type}</small>
                </div>
                <div>
                  <span>زمان باقی‌مانده</span>
                  <strong>{minutes(activeStation.remainingMinutes)}</strong>
                </div>
                <div>
                  <span>اینترنت</span>
                  <strong>{activeStation.network === 2 ? 'اینترنت ۲' : 'اینترنت ۱'}</strong>
                </div>
                <div>
                  <span>بدهی روی جلسه</span>
                  <strong className={activeStation.customerDebt && activeStation.customerDebt > 0 ? 'negative' : ''}>
                    {money(activeStation.customerDebt ?? customer.debt)} تومان
                  </strong>
                </div>
                <div className="wide">
                  <span>یادداشت جلسه</span>
                  <strong>{activeStation.customerNote || customer.notes || 'یادداشتی برای این جلسه ثبت نشده است.'}</strong>
                </div>
              </div>
            ) : (
              <div className="customer-flow-no-station">
                مشتری در حال حاضر روی هیچ دستگاه فعالی نیست؛ عملیات مالی همچنان از همین پنجره قابل انجام است.
              </div>
            )}
          </section>

          <section className="customer-flow-action-panel">
            <div className="customer-flow-section-head">
              <div>
                <span className="customer-flow-eyebrow">عملیات اپراتور</span>
                <strong>عملیات مالی سریع</strong>
              </div>
              <span className="customer-flow-selected">{busy ? 'در حال ثبت...' : 'کلیک یا میانبر صفحه‌کلید'}</span>
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
                disabled={busy}
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
                <span className="customer-flow-eyebrow">سابقه</span>
                <strong>آخرین فعالیت‌های مشتری</strong>
              </div>
              <span className="customer-flow-selected">۵ مورد اخیر</span>
            </div>

            {customer.transactionHistory?.length ? (
              <div className="customer-flow-history-list">
                {customer.transactionHistory.slice(0, 5).map((item, index) => (
                  <div key={index}>
                    <span className="customer-flow-history-dot">●</span>
                    <strong>{item}</strong>
                  </div>
                ))}
              </div>
            ) : (
              <div className="customer-flow-empty-history">برای این مشتری سابقه‌ای در خلاصه پروفایل ثبت نشده است.</div>
            )}
          </section>
        </>
      ) : (
        <section className="customer-flow-empty">
          <div className="customer-flow-empty-icon">⌕</div>
          <strong>هنوز مشتری انتخاب نشده است</strong>
          <span>شناسه، username، موبایل یا نام مشتری را وارد کن. بعد از انتخاب، اطلاعات هویتی، مالی، دستگاه و عملیات در همین پنجره نمایش داده می‌شود.</span>
        </section>
      )}
    </div>
  );
}
